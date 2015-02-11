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

namespace McSherry.Zener.Net
{
    /// <summary>
    /// Represents an HTTP header where the value is a list of
    /// comma-separated values.
    /// </summary>
    public sealed class CsvHttpHeader
        : HttpHeader
    {
        private const char 
            SEPARATOR   = ',',
            QUOTE       = '"'
            ;

        /// <summary>
        /// Creates a new CsvHttpHeader from an HttpHeader.
        /// </summary>
        /// <param name="header">The header to create from.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when:
        ///         1. The provided field name is null, zero-length, or whitespace.
        ///         2. The provided value is null, zero-length, or whitespace.
        ///         3. The field name contains a carriage return or line feed character.
        ///         4. The value contains a carriage return or line feed character.
        /// </exception>
        public CsvHttpHeader(HttpHeader header)
            : this(header.Field, header.Value)
        {

        }
        /// <summary>
        /// Creates a new CsvHttpHeader
        /// </summary>
        /// <param name="fieldName">The header/field name (e.g. Content-Type).</param>
        /// <param name="fieldValue">The value of the header/field (e.g. text/html).</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when:
        ///         1. The provided field name is null, zero-length, or whitespace.
        ///         2. The provided value is null, zero-length, or whitespace.
        ///         3. The field name contains a carriage return or line feed character.
        ///         4. The value contains a carriage return or line feed character.
        /// </exception>
        public CsvHttpHeader(string fieldName, string fieldValue)
            : base(fieldName, fieldValue)
        {
            base.Value = base.Value.Trim(Whitespace.ToCharArray());
            this.Items = Networking.ParseDelimitedSemiQuotedStrings(base.Value);
        }

        /// <summary>
        /// The items present within the comma-separated list
        /// of values.
        /// </summary>
        public ICollection<string> Items
        {
            get;
            private set;
        }
    }
}
