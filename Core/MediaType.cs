/*
 *      Copyright (c) 2014-2015, Liam McSherry
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Networking = McSherry.Zener.Net.Networking;

namespace McSherry.Zener.Core
{
    /// <summary>
    /// The category the media type is in.
    /// </summary>
    public enum MediaTypeRegTree
    {
        /// <summary>
        /// The media type is a standard media type, and does not
        /// have a prefix.
        /// </summary>
        Standard,
        /// <summary>
        /// The media type is a vendor-specific type, and bears the
        /// "vnd." prefix.
        /// </summary>
        Vendor,
        /// <summary>
        /// The media type is a personal or experimental type, and
        /// bears the "prs." prefix.
        /// </summary>
        Personal,
        /// <summary>
        /// The media type is an unregistered or private type, and
        /// bears the "x." prefix.
        /// </summary>
        Unregistered
    }

    /// <summary>
    /// A class representing a media type.
    /// </summary>
    public sealed class MediaType
    {
        /// <summary>
        /// Internal state values for the MediaType parser.
        /// </summary>
        private enum PState
        {
            /// <summary>
            /// Currently parsing the media type's super-type.
            /// </summary>
            SuperType,
            /// <summary>
            /// Currently parsing the media type's subtype prefix.
            /// </summary>
            Prefix,
            /// <summary>
            /// Currently parsing the media type's subtype.
            /// </summary>
            SubType,
            /// <summary>
            /// Currently parsing the media type's subtype suffix.
            /// </summary>
            Suffix,
            /// <summary>
            /// Currently parsing the media type's parameter(s).
            /// </summary>
            Parameter
        }

        /// <summary>
        /// Implements an IEqualityComparer for MediaType that uses
        /// the MediaType.IsEquivalent method to determine equality.
        /// </summary>
        public class EquivalencyComparer : IEqualityComparer<MediaType>
        {
            /// <summary>
            /// Determines whether the left-hand MediaType is
            /// equivalent to the right-hand MediaType.
            /// </summary>
            /// <param name="lhs">The left-hand MediaType.</param>
            /// <param name="rhs">The right-hand MediaType.</param>
            /// <returns>
            /// True if the left-hand MediaType is equivalent
            /// to the right-hand MediaType.
            /// </returns>
            public bool Equals(MediaType lhs, MediaType rhs)
            {
                return lhs.IsEquivalent(rhs);
            }
            /// <summary>
            /// Retrieves a hash code for the MediaType.
            /// </summary>
            /// <param name="mt">
            /// The MediaType to retrieve the hash code for.
            /// </param>
            /// <returns>
            /// The hash code of the provided MediaType.
            /// </returns>
            public int GetHashCode(MediaType mt)
            {
                unchecked
                {
                    return
                        (mt.SuperType.GetHashCode()         * 2)    +
                        (mt.RegistrationTree.GetHashCode()  * 3)    +
                        (mt.SubType.GetHashCode()           * 5)    +
                        0x4389AB3 // Random prime number as a seed
                        ;
                        
                }
            }
        }

        private static readonly Dictionary<MediaTypeRegTree, string>
            MediaTypeRegTreeStrings = new Dictionary<MediaTypeRegTree, string>()
        {
            {   MediaTypeRegTree.Standard,       ""        },
            {   MediaTypeRegTree.Vendor,         "vnd"     },
            {   MediaTypeRegTree.Personal,       "prs"     },
            {   MediaTypeRegTree.Unregistered,   "x"       },
        };
        /***********************************
         * DO NOT MOVE THESE STATIC FIELDS *
         * ------------------------------- *
         * Static fields are created in    *
         * the order they are in the file. *
         *                                 *
         * Moving this one after the below *
         * dictionary will result in the   *
         * throwing of an exception.       *
         ***********************************
         */
        private static readonly HashSet<char>
            SuperSubTypeCharacters = new HashSet<char>(SuperSubTypeCharactersS);
        // A map of suffixes to equivalent media types.
        private static readonly Dictionary<string, MediaType>
            MTSfxEquivalencyMap = new Dictionary<string, MediaType>()
        {
            {   "json",         "application/json"          },
            {   "fastinfoset",  "application/fastinfoset"   },
            {   "wbxml",        "application/vnd.wab.wbxml" },
            {   "zip",          "application/zip"           },
            {   "xml",          "application/xml"           },
            {   "cbor",         "application/cbor"          },
        };
        private const string
            // Valid characters for the super/subtypes in a media type. All
            // comparisons we do will be in lowercase, so we don't need capitals.
            SuperSubTypeCharactersS = "abcedfghijklmnopqrstuvwxyz0123456789-.",
            WildcardString          = "*"
            ;
        private const char
            SubTypeSeparator        = '/',
            ParameterSeparator      = ';',
            SuffixSeparator         = '+',
            PrefixSeparator         = '.',
            Dash                    = '-',
            EqualsSign              = '=',
            WildcardChar            = '*'
            ;

        /// <summary>
        /// Retrieves the string associated with the provided media type
        /// category/registration tree. This string will not include the
        /// trailing period.
        /// </summary>
        /// <param name="category">The category to retrieve the string for.</param>
        /// <returns>
        /// The string associated with the category, sans trailing period.
        /// </returns>
        public static string GetCategoryString(MediaTypeRegTree category)
        {
            return MediaTypeRegTreeStrings[category];
        }
        /// <summary>
        /// Creates a MediaType class from a string.
        /// </summary>
        /// <param name="mediaType">The string to parse.</param>
        /// <returns>A MediaType class equivalent to the string.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided media type string is null., empty,
        /// or white-space.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided media type string is invalid.
        /// </exception>
        public static MediaType Create(string mediaType)
        {
            if (String.IsNullOrWhiteSpace(mediaType))
            {
                throw new ArgumentNullException(
                    "The provided string must not be null, empty, or white-space."
                    );
            }
            mediaType = mediaType.Trim();

            MediaType type = new MediaType()
            {
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
            StringBuilder storage = new StringBuilder();
            PState state = PState.SuperType;
            // Gets the length of the longest prefix we can
            // recognise.
            var prefixes = MediaType.MediaTypeRegTreeStrings
                .OrderByDescending(kvp => kvp.Value.Length)
                .Select(kvp => kvp.Value.Length);
            int longestPrefix = prefixes.First(),
               shortestPrefix = prefixes.Last();

            int sectionStart = 0;
            for (int i = 0; i < mediaType.Length; i++)
            {
                char c = mediaType[i];
                if (state == PState.SuperType)
                {
                    // There are certain characters we will
                    // consider invalid if they're at the start
                    // of the super-type section.
                    if (i == sectionStart)
                    {
                        if (c == PrefixSeparator)
                        {
                            throw new ArgumentException(
                                "The media type's super-type cannot start with" +
                                " a period."
                                );
                        }
                        else if (c == Dash)
                        {
                            throw new ArgumentException(
                                "The media type's super-type cannot start with" +
                                " a dash."
                                );
                        }
                    }

                    // If we reach this character, we've reached the end
                    // of the super-type, and now have to switch to the
                    // subtype.
                    if (c == SubTypeSeparator)
                    {
                        // Set the section start variable.
                        sectionStart = i + 1;
                        // It's possible that there will be a prefix before
                        // the subtype, so we need to switch to this state
                        // before the subtype state.
                        state = PState.Prefix;
                        // Set the SuperType property of the MediaType.
                        type.SuperType = storage.ToString().ToLower();
                        // Clear the storage.
                        storage.Clear();
                    }
                    // If the character is a wildcard character, there
                    // are some special rules we need to apply.
                    else if (c == WildcardChar)
                    {
                        if (
                            // The wildcard must be the only character in
                            // a media type's super-type. So we need to
                            // make sure that there are no characters
                            // already in the storage.
                            storage.Length > 0 ||
                            // And we need to make sure that the next
                            // character is the super-to-subtype separator.
                            mediaType[i + 1] != SubTypeSeparator
                            )
                        {
                            throw new ArgumentException(
                                "The media type super-type contains a wildcard " +
                                "in an invalid location."
                                );
                        }

                        // If we're here, the presence of a wildcard is fine,
                        // so we can add it to storage.
                        storage.Append(c);
                    }
                    // Otherwise, we need to check that the character is
                    // valid.
                    else if (SuperSubTypeCharacters.Contains(c))
                    {
                        // The character is valid, so we add it to
                        // storage.
                        storage.Append(c);
                    }
                    // If we end up here, the character is invalid.
                    else
                    {
                        throw new ArgumentException(
                            "The media type contains an invalid super-type."
                            );
                    }
                }
                else if (state == PState.Prefix)
                {
                    // As with the super-type, certain characters
                    // are not permitted at the start of the subtype.
                    if (i == sectionStart)
                    {
                        if (c == PrefixSeparator)
                        {
                            throw new ArgumentException(
                                "The media type's subtype must not start with " +
                                "a period."
                                );
                        }
                        else if (c == Dash)
                        {
                            throw new ArgumentException(
                                "The media type's subtype must not start with " +
                                "a dash."
                                );
                        }
                    }


                    if (
                        // If the length of the data in storage is longer
                        // than the longest prefix, it won't be a prefix
                        // we recognise.
                        storage.Length > longestPrefix ||
                        // If the longest possible length is shorter than
                        // the shortest prefix, it, again, won't be a prefix
                        // that we recognise.
                        (mediaType.Length - i) + storage.Length < shortestPrefix
                        )
                    {
                        // Cancel out increment.
                        --i;
                        // We won't be recognising a prefix, so switch to the
                        // subtype state.
                        state = PState.SubType;
                    }
                    // If we encounter the prefix separator, we need to check
                    // that it's a prefix we recognise.
                    else if (c == PrefixSeparator)
                    {
                        var pfx = MediaTypeRegTreeStrings
                            // Check the prefix string against all prefix strings
                            // we know of.
                            .Where(
                                kvp => kvp.Value.Equals(storage.ToString().ToLower())
                                )
                            // Select the enum value from the results.
                            .Select(kvp => kvp.Key)
                            // If there are no matches, set it to a default
                            // value that we'll be able to recognise.
                            .DefaultIfEmpty((MediaTypeRegTree)(-1))
                            // Get the first result.
                            .First();

                        // If this is equal, we don't recognise the string.
                        if ((int)pfx == -1)
                        {
                            // Append the separator character to storage.
                            storage.Append(c);
                        }
                        // If not, we know what the prefix is.
                        else
                        {
                            // Clear the storage of the prefix string.
                            storage.Clear();
                            // Set the MediaType's property.
                            type.RegistrationTree = pfx;
                            // Set the new section start value.
                            sectionStart = i + 1;
                        }

                        // Regardless of the above outcome, we need to
                        // switch to the subtype state.
                        state = PState.SubType;
                    }
                    // If the character is a wildcard character, there
                    // are some special rules we need to apply.
                    else if (c == WildcardChar)
                    {
                        if (
                            // The wildcard must be the only character in
                            // a media type's subtype. So we need to
                            // make sure that there are no characters
                            // already in the storage.
                            storage.Length > 0 ||
                            // This also needs to be the end of the prefix/subtype,
                            // either because it's the end of the string; and
                            i + 1 < mediaType.Length &&
                            (
                            // That the next character is the separator that
                            // indicates the start of a suffix; or
                                mediaType[i + 1] != SuffixSeparator ||
                            // That the next character is a parameter separator
                            // character.
                                mediaType[i + 1] != ParameterSeparator
                            ))
                        {
                            throw new ArgumentException(
                                "The media type prefix/subtype contains a wildcard " +
                                "in an invalid location."
                                );
                        }

                        // If we're here, the presence of a wildcard is fine,
                        // so we can add it to storage.
                        storage.Append(c);
                    }
                    // If we find one of these characters, we probably aren't
                    // in a prefix, and we may instead be in a subtype.
                    else if (c == SuffixSeparator || c == ParameterSeparator)
                    {
                        // Cancel out the for-loop's increment.
                        --i;
                        // Switch to the subtype-parsing state.
                        state = PState.SubType;
                    }
                    // Otherwise, we need to check that the character is valid.
                    else if (SuperSubTypeCharacters.Contains(c))
                    {
                        // Append the character to storage if it is valid.
                        storage.Append(c);
                    }
                    // The character is invalid.
                    else
                    {
                        throw new ArgumentException(
                            "The media type's subtype/prefix contains invalid " +
                            "characters."
                            );
                    }
                }
                // We're parsing the media type's subtype.
                else if (state == PState.SubType)
                {
                    if (sectionStart == i)
                    {
                        if (c == PrefixSeparator)
                        {
                            throw new ArgumentException(
                                "The media type's subtype must not start with " +
                                "a period."
                                );
                        }
                        else if (c == Dash)
                        {
                            throw new ArgumentException(
                                "The media type's subtype must not start with " +
                                "a dash."
                                );
                        }
                    }

                    // If we meet one of these characters, we're going to
                    // have to switch to a new state.
                    if (c == SuffixSeparator || c == ParameterSeparator)
                    {
                        // Set the MediaType's subtype property.
                        type.SubType = storage.ToString().ToLower();
                        // Clear storage.
                        storage.Clear();

                        if (c == SuffixSeparator)
                        {
                            state = PState.Suffix;
                        }
                        else
                        {
                            state = PState.Parameter;
                        }
                    }
                    // If the character is a wildcard character, there
                    // are some special rules we need to apply.
                    else if (c == WildcardChar)
                    {
                        if (
                            // The wildcard must be the only character in
                            // a media type's subtype. So we need to
                            // make sure that there are no characters
                            // already in the storage.
                            storage.Length > 0 ||
                            // This also needs to be the end of the subtype,
                            // either because it's the end of the string; and
                            i + 1 < mediaType.Length &&
                            (
                            // That the next character is the separator that
                            // indicates the start of a suffix; or
                                mediaType[i + 1] != SuffixSeparator ||
                            // That the next character is a parameter separator
                            // character.
                                mediaType[i + 1] != ParameterSeparator
                            ))
                        {
                            throw new ArgumentException(
                                "The media type subtype contains a wildcard " +
                                "in an invalid location."
                                );
                        }

                        // If we're here, the presence of a wildcard is fine,
                        // so we can add it to storage.
                        storage.Append(c);
                    }
                    // The character is valid.
                    else if (SuperSubTypeCharacters.Contains(c))
                    {
                        storage.Append(c);
                    }
                    else
                    {
                        throw new ArgumentException(
                            "The media type's subtype contains an invalid character."
                            );
                    }
                }
                else if (state == PState.Suffix)
                {
                    if (sectionStart == i)
                    {
                        if (c == PrefixSeparator)
                        {
                            throw new ArgumentException(
                                "The media type's suffix must not start with " +
                                "a period."
                                );
                        }
                        else if (c == Dash)
                        {
                            throw new ArgumentException(
                                "The media type's suffix must not start with " +
                                "a dash."
                                );
                        }
                    }

                    // If we hit one of these characters, we've reached
                    // the end of the suffix and now have to move on to
                    // parameter parsing.
                    if (c == ParameterSeparator)
                    {
                        // Set the new section start value.
                        sectionStart = i + 1;
                        // Set the MediaType's suffix value.
                        type.Suffix = storage.ToString().ToLower();
                        // Clear the storage.
                        storage.Clear();
                        // Set the new state.
                        state = PState.Parameter;
                    }
                    else if (SuperSubTypeCharacters.Contains(c))
                    {
                        storage.Append(c);
                    }
                    else
                    {
                        throw new ArgumentException(
                            "The media type's suffix contains an invalid character."
                            );
                    }
                }
                // If we're in the parameter state, we no longer need to loop.
                else if (state == PState.Parameter)
                {
                    sectionStart = i;
                    break;
                }
            }

            if (state == PState.Parameter)
            {
                try
                {
                    type.Parameters = Networking.ParseUnquotedKeyValues(
                            mediaType.Substring(sectionStart)
                            );
                }
                catch (ArgumentException aex)
                {
                    throw new ArgumentException(
                        "The provided string contains invalid or malformed parameters.",
                        aex
                        );
                }
            }
            else if (state == PState.SuperType)
            {
                throw new ArgumentException(
                    "The provided string does not contain a subtype."
                    );
            }
            else if (state == PState.Prefix || state == PState.SubType)
            {
                type.SubType = storage.ToString().ToLower();
            }
            else if (state == PState.Suffix)
            {
                type.Suffix = storage.ToString().ToLower();
            }

            return type;
        }
        /// <summary>
        /// Attempts to create a MediaType class from a string.
        /// </summary>
        /// <param name="mediaType">The string to parse.</param>
        /// <param name="type">
        /// The MediaType that will be given the parsed value if
        /// creation succeeds.
        /// </param>
        /// <returns>True if creation succeeds.</returns>
        public static bool TryCreate(string mediaType, out MediaType type)
        {
            bool success;

            try
            {
                type = MediaType.Create(mediaType);
                success = true;
            }
            // Yes, I know, bad practice to catch all exceptions. However,
            // the whole point of this method is to not throw an exception
            // when something goes wrong with parsing the MediaType.
            catch (Exception)
            {
                type = null;
                success = false;
            }

            return success;
        }
        
        /// <summary>
        /// Converts a string to a MediaType by passing the string
        /// to MediaType.Create.
        /// </summary>
        /// <param name="mediaType">
        /// The string containing the media type to convert to.
        /// </param>
        /// <returns>
        /// A MediaType equivalent to the provided string.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided media type string is null., empty,
        /// or white-space.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided media type string is invalid.
        /// </exception>
        public static implicit operator MediaType(string mediaType)
        {
            return MediaType.Create(mediaType);
        }
        /// <summary>
        /// Converts a MediaType to a string by calling the MediaType's
        /// overload of the method ToString.
        /// </summary>
        /// <param name="type">
        /// The MediaType to convert to a string.
        /// </param>
        /// <returns>
        /// A string representing the provided MediaType.
        /// </returns>
        public static implicit operator string(MediaType type)
        {
            return type.ToString();
        }

        /// <summary>
        /// The media type for plain text content.
        /// </summary>
        public static MediaType PlainText
        {
            get { return "text/plain"; }
        }
        /// <summary>
        /// The media type for Hypertext Markup Language content.
        /// </summary>
        public static MediaType Html
        {
            get { return "text/html"; }
        }
        /// <summary>
        /// The media type for Extensible Markup Language content.
        /// </summary>
        public static MediaType Xml
        {
            get { return "application/xml"; }
        }
        /// <summary>
        /// The media type for JavaScript content.
        /// </summary>
        public static MediaType JavaScript
        {
            get { return "application/javascript"; }
        }
        /// <summary>
        /// The media type for JavaScript Object Notation content.
        /// </summary>
        public static MediaType JSON
        {
            get { return "application/json"; }
        }
        /// <summary>
        /// A wildcard media type that will match any media type.
        /// </summary>
        public static MediaType Wildcard
        {
            get { return "*/*"; }
        }
        /// <summary>
        /// The media type used for arbitrary binary data.
        /// </summary>
        public static MediaType OctetStream
        {
            get { return "application/octet-stream"; }
        }

        private MediaType() { }

        /// <summary>
        /// The media type's category, or registration tree.
        /// </summary>
        public MediaTypeRegTree RegistrationTree
        {
            get;
            private set;
        }
        /// <summary>
        /// The type of this media type. Usually "text," "image," "video,"
        /// et cetera.
        /// </summary>
        public string SuperType
        {
            get;
            private set;
        }
        /// <summary>
        /// The subtype of this media type, identifying a specific format
        /// within the remit of the type (for example, "html," "png").
        /// </summary>
        public string SubType
        {
            get;
            private set;
        }
        /// <summary>
        /// The media type subtype suffix. If no suffix is
        /// present, this is null.
        /// </summary>
        public string Suffix
        {
            get;
            private set;
        }
        /// <summary>
        /// The parameters included with the media type. This
        /// is typically zero or one name-value pairs, but there
        /// may be any number of parameters.
        /// </summary>
        public IDictionary<string, string> Parameters
        {
            get;
            private set;
        }

        /// <summary>
        /// Determines whether the provided media type is
        /// equal to this media type.
        /// </summary>
        /// <param name="type">The media type to compare.</param>
        /// <returns>True if the media types are equal.</returns>
        public bool Equals(MediaType type)
        {
            return
                type.RegistrationTree   == this.RegistrationTree    &&
                type.SuperType          == this.SuperType           &&
                type.SubType            == this.SubType             &&
                type.Parameters         == this.Parameters          &&
                type.Suffix             == this.Suffix              ;;
        }
        /// <summary>
        /// Determines whether the provided MediaType
        /// may be compatible, based on the suffix (if
        /// one is present).
        /// </summary>
        /// <param name="type">
        /// The MediaType to determine compatibility with.
        /// </param>
        /// <returns>
        /// True if the provided MediaType could be compatible
        /// with this media type.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided MediaType is null.
        /// </exception>
        public bool IsCompatible(MediaType type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(
                    "The MediaType to compare must not be null."
                    );
            }

            bool
                // Whether this MediaType has no suffix.
                thisNoSfx = this.Suffix == null,
                // Whether the MediaType to compare has
                // no suffix.
                thatNoSfx = type.Suffix == null;

            // If both of the suffixes are null, it isn't
            // possible to determine tentative compatibility.
            if (thisNoSfx && thatNoSfx) return false;
            // If both MediaTypes have a suffix, we can just
            // compare the suffixes.
            else if (!thisNoSfx && !thatNoSfx)
            {
                return this.Suffix == type.Suffix;
            }
            // If this MediaType doesn't have a suffix, we have
            // to compare this MediaType's Super+SubType with the
            // Super+SubType pair that is equivalent to the provided
            // MediaType's suffix.
            else
            {
                MediaType suffixed, unsuffixed;
                if (thisNoSfx)
                {
                    suffixed    = type;
                    unsuffixed  = this;
                }
                else
                {
                    suffixed    = this;
                    unsuffixed  = type;
                }

                // The MediaType that the suffix is equivalent to.
                MediaType equiv;
                // Attempt to retrieve the equivalent media type for
                // the suffix.
                if (!MTSfxEquivalencyMap.TryGetValue(suffixed.Suffix, out equiv))
                {
                    // If we can't retrieve the equivalent media type, it
                    // isn't a suffix we recognise so we can't determine
                    // tentative compatibility. Return false.
                    return false;
                }

                // We then determine tentative compatibility via the IsEquivalent
                // method, which does all the comparison for us. As we called
                // IsEquivalent on the MediaType we retrieved from our suffix
                // equivalency map, it won't matter whether the unsuffixed media
                // type has parameters.
                return equiv.IsEquivalent(unsuffixed);
            }
        }
        /// <summary>
        /// Determines whether this MediaType is equivalent
        /// to the provided MediaType.
        /// </summary>
        /// <param name="type">The type to determine rough equivalency for.</param>
        /// <returns>True if the MediaType is roughly equivalent.</returns>
        /// <remarks>
        /// This method compares the registration tree, super-type, and
        /// sub-type. The latter two are considered case-insensitive.
        /// 
        /// Additionally, if this MediaType has any parameters, this method
        /// will only consider the provided MediaType equivalent if it has
        /// parameters which are exactly equal.
        /// 
        /// Further, if both MediaTypes have a suffix, the suffixes will be
        /// compared. Suffixes are considered case-insensitive.
        /// </remarks>
        public bool IsEquivalent(MediaType type)
        {
            // True by default.
            bool paramEqual = true, suffixEqual = true;
            // If we have parameters, we need to compare them with the provided
            // type's parameters. If not, we won't compare them.
            if (this.Parameters.Count > 0)
            {
                // If the lengths don't match, there's no point in
                // comparing values.
                if (type.Parameters.Count != this.Parameters.Count)
                {
                    paramEqual = false;
                }
                else
                {
                    // paramEqual will be true if all values within both the
                    // dictionaries are equal.
                    paramEqual = this.Parameters
                        .All(kvp => kvp.Value == type.Parameters[kvp.Key]);
                }
            }
            // If there are suffixes, compare them.
            if (this.Suffix != null && type.Suffix != null)
            {
                suffixEqual = this.Suffix
                    .Equals(type.Suffix, StringComparison.OrdinalIgnoreCase);
            }

            bool superEqual = 
                // Check to see whether either super-type is a wildcard.
                this.SuperType == WildcardString ||
                type.SuperType == WildcardString ||
                // Just in case it isn't, case-insensitive comparison.
                type.SuperType.Equals(this.SuperType, StringComparison.OrdinalIgnoreCase);
            // Same as we did with the super-type.
            bool subEqual =
                this.SubType == WildcardString ||
                type.SubType == WildcardString ||
                type.SubType.Equals(this.SubType, StringComparison.OrdinalIgnoreCase);

            return
                paramEqual && suffixEqual &&
                this.RegistrationTree == type.RegistrationTree &&
                superEqual && subEqual;
        }

        /// <summary>
        /// Determines whether the provided object is equal
        /// to this media type.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the object is equal to this media type.</returns>
        public override bool Equals(object obj)
        {
            var mt = obj as MediaType;

            if (mt == null) return false;
            else            return mt.Equals(type: mt);
        }
        /// <summary>
        /// Retrieves a hash code for the MediaType. This is
        /// not guaranteed to be unique, but will be identical for
        /// identical MediaType instances.
        /// </summary>
        /// <returns>An integer containing the hash code.</returns>
        public override int GetHashCode()
        {
            int hashCode;

            unchecked
            {
                hashCode =
                    RegistrationTree.GetHashCode()  +
                    SuperType.GetHashCode()         +
                    SubType.GetHashCode()           +
                    (Suffix ?? "").GetHashCode()    +
                    Parameters.GetHashCode()        ;
            }

            return hashCode;
        }
        /// <summary>
        /// Returns the string representation of the
        /// media type.
        /// </summary>
        /// <returns>The string representation of the media type.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            // The media type's super type identifies the general
            // remit of the media type (e.g. text, image, video),
            // and is separated from the subtype/etc by a forward
            // slash.
            sb.AppendFormat("{0}/", this.SuperType);

            // The standard registration tree/category does not
            // have a prefix. If it isn't a standard-category
            // media type, it will have a prefix.
            if (this.RegistrationTree != MediaTypeRegTree.Standard)
            {
                sb.AppendFormat(
                    "{0}.",
                    MediaType.GetCategoryString(this.RegistrationTree)
                    );
            }

            // The subtype comes after the vendor prefix, and specifies
            // within the general remit of the media type the specific
            // application. HTML text, for example, is "text/html," and
            // PNG images are "image/png."
            sb.Append(this.SubType);

            // Media types may have a suffix. If this media type
            // doesn't have a suffix, the value of the property
            // will be null.
            if (this.Suffix != null)
            {
                // Suffixes are separated from the subtype by a plus sign.
                // The suffix specifies what the media type is based on.
                // The most common suffix is probably "+xml," which indicates
                // that the media type is based on XML (for example, in
                // Atom feeds or XHTML documents).
                sb.AppendFormat("+{0}", this.Suffix);
            }

            // Media types can have parameters appended to them. The text/html
            // media type, for example, can have a "charset" parameter to
            // specify character encoding.
            //
            //      text/html; charset=UTF-8
            //
            // Multiple parameters are generally separated by semicolons.
            //
            //      video/example; parameter=value; parameter=value
            foreach (var kvp in this.Parameters)
            {
                // If the value is null, it means that the parameter had
                // no value. Since there was no value, we'll have to use
                // a different format when appending.
                if (kvp.Value == null)
                {
                    sb.AppendFormat("; {0}", kvp.Key);
                }
                else
                {
                    // Parameters are separated from the media type by a semicolon.
                    sb.AppendFormat("; {0}={1}", kvp.Key, kvp.Value);
                }
            }

            return sb.ToString();
        }
    }
}
