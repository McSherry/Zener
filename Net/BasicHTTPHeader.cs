﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// Implements a basic HTTP header.
    /// </summary>
    public class BasicHttpHeader : IHttpHeader
    {

        /// <summary>
        /// Creates a new basic HTTP header.
        /// </summary>
        /// <param name="fieldName">The header/field name (e.g. Content-Type).</param>
        /// <param name="value">The value of the header/field (e.g. text/html).</param>
        /// <exception cref="System.ArgumentException"></exception>
        public BasicHttpHeader(string fieldName, string value)
        {
            fieldName = fieldName.Trim();
            value = value.Trim();

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
        public virtual string Field { get; internal set; }
        /// <summary>
        /// The value of the field/header, as a string.
        /// </summary>
        public virtual string Value { get; internal set; }

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
            headerText = headerText.Trim(new[] { ' ', '\r', '\n' });

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
