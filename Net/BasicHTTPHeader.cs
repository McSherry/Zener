/*
 *      Copyright (c) 2014, SynapLink, LLC
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE.txt, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// Implements a basic HTTP header.
    /// </summary>
    public class BasicHttpHeader
    {
        /// <summary>
        /// The characters to be trimmed from the start and end of
        /// HTTP headers.
        /// </summary>
        protected static readonly char[] TRIM_CHARS = new[] 
        {
            ' ', '\t'
        };

        /// <summary>
        /// Creates a new basic HTTP header.
        /// </summary>
        /// <param name="fieldName">The header/field name (e.g. Content-Type).</param>
        /// <param name="value">The value of the header/field (e.g. text/html).</param>
        /// <exception cref="System.ArgumentException"></exception>
        public BasicHttpHeader(string fieldName, string value)
        {
            fieldName = fieldName.Trim(TRIM_CHARS);
            value = value.Trim(TRIM_CHARS);

            if (fieldName == null || fieldName.Length == 0)
            {
                throw new ArgumentException
                ("A field name must be provided.", "fieldName");
            }

            if (value == null || value.Length == 0)
            {
                throw new ArgumentException
                ("A value must be provided.", "value");
            }

            if (fieldName.Any(c => c == ':' || c == '\n' || c == '\r'))
            {
                throw new ArgumentException
                ("Header field may not contain colon/CR/LF.", "fieldName");
            }

            if (value.Any(c => c == '\n' || c == '\r'))
            {
                throw new ArgumentException
                ("Header value may not contain CR/LF.", "value");
            }

            this.Field = fieldName;
            this.Value = value;
        }

        /// <summary>
        /// The field or header name (e.g. Content-Type).
        /// </summary>
        public virtual string Field { get; protected set; }
        /// <summary>
        /// The value of the field/header, as a string.
        /// </summary>
        public virtual string Value { get; protected set; }

        public override string ToString()
        {
            return new StringBuilder()
                .AppendFormat("{0}: {1}", this.Field, this.Value)
                    .ToString();
        }

        /// <summary>
        /// Converts a string to a BasicHTTPHeader.
        /// </summary>
        /// <param name="headerText">The full text of the header.</param>
        /// <returns>A BasicHTTPHeader equivalent to the provided string.</returns>
        /// <exception cref="System.ArgumentException"></exception>
        public static BasicHttpHeader Parse(string headerText)
        {
            headerText = headerText.Trim(
                TRIM_CHARS
                    .Concat(new[] { ' ', '\r', '\n' })
                    .ToArray()
                );

            StringBuilder fieldBuilder = new StringBuilder();

            int i = 0;
            for (; i < headerText.Length;)
            {
                if (headerText[i] == ':') break;
                fieldBuilder.Append(headerText[i++]);
            }

            return new BasicHttpHeader(fieldBuilder.ToString(), headerText.Substring(++i));
        }
    }
}
