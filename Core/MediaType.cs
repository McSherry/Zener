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
        Unregistered,
        /// <summary>
        /// The media type bears an unofficial or
        /// unrecognised prefix.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// A class representing a media type.
    /// </summary>
    public sealed class MediaType
    {
        private const string
            StandardTreePrefix      = "",
            VendorTreePrefix        = "vnd",
            PersonalTreePrefix      = "prs",
            UnregisteredTreePrefix  = "x"
            ;
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
        /// <exception cref="System.ArgumentException">
        /// Thrown when the "Unknown" category is passed to the method, or when
        /// the MediaTypeCategory value passed to the method is not recognised.
        /// </exception>
        public static string GetCategoryString(MediaTypeCategory category)
        {
            if (category == MediaTypeCategory.Unknown)
            {
                throw new ArgumentException(
                    @"The category ""Unknown"" has no standardised prefix."
                    );
            }

            switch (category)
            {
                case MediaTypeCategory.Standard:        return StandardTreePrefix;
                case MediaTypeCategory.Vendor:          return VendorTreePrefix;
                case MediaTypeCategory.Personal:        return PersonalTreePrefix;
                case MediaTypeCategory.Unregistered:    return UnregisteredTreePrefix;
            }

            throw new ArgumentException(
                "The provided category enum value is invalid."
                );
        }

        public MediaType()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The media type's full type string, including type,
        /// subtype, prefix, suffix, and parameters.
        /// </summary>
        public string FullType
        {
            get { throw new NotImplementedException(); }
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
    }
}
