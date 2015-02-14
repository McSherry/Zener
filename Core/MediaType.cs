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
        private static readonly Dictionary<MediaTypeCategory, string>
            MediaTypeCategoryStrings = new Dictionary<MediaTypeCategory, string>()
        {
            {   MediaTypeCategory.Standard,       ""        },
            {   MediaTypeCategory.Vendor,         "vnd"     },
            {   MediaTypeCategory.Personal,       "prs"     },
            {   MediaTypeCategory.Unregistered,   "x"       },
        };
        private const char
            SubTypeSeparator        = '/',
            ParameterSeparator      = ';',
            SuffixSeparator         = '+',
            PrefixSeparator         = '.'
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

        public MediaType()
        {
            throw new NotImplementedException();
        }

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
        public string Type
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
        /// Determines whether the provided media type is
        /// equal to this media type.
        /// </summary>
        /// <param name="type">The media type to compare.</param>
        /// <returns>True if the media types are equal.</returns>
        public bool Equals(MediaType type)
        {
            return
                type.Category == this.Category    &&
                type.Type     == this.Type        &&
                type.SubType  == this.SubType     &&
                type.Suffix   == this.Suffix      ;;
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
                    Category.GetHashCode()  +
                    Type.GetHashCode()      +
                    SubType.GetHashCode()   +
                    Suffix.GetHashCode()    ;
            }

            return hashCode;
        }
    }
}
