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

namespace McSherry.Zener.Core
{
    /// <summary>
    /// The category the media type is in.
    /// </summary>
    public enum MediaTypeCategory
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
        /// bears the "x." prefix. This prefix is officially deprecated.
        /// </summary>
        Unregistered
    }

    /// <summary>
    /// A class representing a media type.
    /// </summary>
    public sealed class MediaType
    {
        private enum PState
        {
            SuperType,
            Prefix,
            SubType,
            Suffix,
            Parameter
        }
        private static readonly Dictionary<MediaTypeCategory, string>
            MediaTypeCategoryStrings = new Dictionary<MediaTypeCategory, string>()
        {
            {   MediaTypeCategory.Standard,       ""        },
            {   MediaTypeCategory.Vendor,         "vnd"     },
            {   MediaTypeCategory.Personal,       "prs"     },
            {   MediaTypeCategory.Unregistered,   "x"       },
        };
        private static readonly Dictionary<string, string>
            MTSfxEquivalencyMap = new Dictionary<string, string>()
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
            SuperSubTypeCharacters  = "abcedfghijklmnopqrstuvwxyz0123456789-."
            ;
        private const char
            SubTypeSeparator        = '/',
            ParameterSeparator      = ';',
            SuffixSeparator         = '+',
            PrefixSeparator         = '.',
            Dash                    = '-',
            EqualsSign              = '='
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
        public static string GetCategoryString(MediaTypeCategory category)
        {
            return MediaTypeCategoryStrings[category];
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
                Parameters = new Dictionary<string, string>()
            };
            StringBuilder storage = new StringBuilder();
            PState state = PState.SuperType;

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

                    // Gets the length of the longest prefix we can
                    // recognise.
                    var prefixes = MTSfxEquivalencyMap
                        .OrderByDescending(kvp => kvp.Key.Length)
                        .Select(kvp => kvp.Key.Length);
                    int longestPrefix = prefixes.First(),
                       shortestPrefix = prefixes.Last();


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
                        // We won't be recognising a prefix, so switch to the
                        // subtype state.
                        state = PState.SubType;
                    }
                    // If we encounter the prefix separator, we need to check
                    // that it's a prefix we recognise.
                    else if (c == PrefixSeparator)
                    {
                        var pfx = MediaTypeCategoryStrings
                            // Check the prefix string against all prefix strings
                            // we know of.
                            .Where(
                                kvp => kvp.Value.Equals(storage.ToString().ToLower())
                                )
                            // Select the enum value from the results.
                            .Select(kvp => kvp.Key)
                            // If there are no matches, set it to a default
                            // value that we'll be able to recognise.
                            .DefaultIfEmpty((MediaTypeCategory)(-1))
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
                            type.Category = pfx;
                            // Set the new section start value.
                            sectionStart = i + 1;
                        }

                        // Regardless of the above outcome, we need to
                        // switch to the subtype state.
                        state = PState.SubType;
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
                else if (state == PState.Parameter)
                {
                    // Move past any leading whitespace.
                    while (Char.IsWhiteSpace(mediaType[i])) i++;
                    // Set new current character.
                    c = mediaType[i];

                    int paramStart = i;
                    // Read up to the first equals sign, which signifies
                    // the end of the parameter's key.
                    while (
                        i < mediaType.Length &&
                        mediaType[i] != EqualsSign
                        ) i++;

                    // There is no equals sign in the parameter. We'll still
                    // consider this valid, but we need to handle it differently.
                    if (i >= mediaType.Length)
                    {
                        // Add the extracted value with the string as the key
                        // and with an empty value.
                        type.Parameters.Add(
                            mediaType.Substring(paramStart),
                            String.Empty
                            );
                    }
                    // There IS an equals sign in the parameter. This means we need
                    // to treat is as a key-value parameter.
                    else
                    {
                        // Extract the key from the media type string.
                        string key = mediaType.Substring(
                            paramStart, i - paramStart
                            );
                        // Skip past the equals sign.
                        i++;
                        // Set the new start as the index.
                        paramStart = i;
                        // Read up until a semicolon, which separates
                        // parameters.
                        while (
                            i < mediaType.Length &&
                            mediaType[i] != ParameterSeparator
                            ) i++;

                        string val = mediaType.Substring(
                            paramStart, i - paramStart);
                        type.Parameters.Add(
                            key,
                            val
                            );

                        if (i < mediaType.Length)
                        {
                            // Skip past the semicolon.
                            i++;
                        }
                    }
                }
            }

            if (state == PState.SuperType)
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

        private MediaType() { }

        /// <summary>
        /// The media type's category, or registration tree.
        /// </summary>
        public MediaTypeCategory Category
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
        /// is typically zero or one name-value pairs.
        /// 
        /// This property is null if no parameters are present.
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
                type.Category           == this.Category        &&
                type.SuperType          == this.SuperType       &&
                type.SubType            == this.SubType         &&
                type.Parameters         == this.Parameters      &&
                type.Suffix             == this.Suffix          ;;
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

                string
                    // Will contain the media type Super+SubType pair
                    // that is equivalent to the suffix.
                    equiv,
                    // Will contain the Super+SubType pair that is being
                    // compared to the suffix's equivalent pair.
                    actual = String.Format(
                        "{0}/{1}",
                        unsuffixed, SuperType, unsuffixed.SubType
                        );
                // Attempt to retrieve a media type Super+SubType pair
                // from the equivalency map.
                if (!MTSfxEquivalencyMap.TryGetValue(suffixed.Suffix, out equiv))
                {
                    // We don't know what the suffix means, and so
                    // we aren't able to determine its equivalent
                    // Super+SubType pair.
                    return false;
                }

                return equiv == actual;
            }
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
                    Category.GetHashCode()      +
                    SuperType.GetHashCode()     +
                    SubType.GetHashCode()       +
                    Suffix.GetHashCode()        +
                    Parameters.GetHashCode()    ;
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
            if (this.Category != MediaTypeCategory.Standard)
            {
                sb.AppendFormat("{0}.", MediaType.GetCategoryString(this.Category));
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
                // Parameters are separated from the media type by a semicolon.
                sb.AppendFormat("; {0}={1}", kvp.Key, kvp.Value);
            }

            return sb.ToString();
        }
    }
}
