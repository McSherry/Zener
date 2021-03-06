﻿/*
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
using System.Threading.Tasks;

namespace McSherry.Zener.Net
{
    /// <summary>
    /// Represents an HTTP header where the value has a set of associated
    /// name-value parameters.
    /// </summary>
    public sealed class NamedParametersHttpHeader 
        : HttpHeader
    {
        private Dictionary<string, string> _nvPairs;

        /// <summary>
        /// Creates a NamedParametersHttpHeader from a header field name
        /// and header value.
        /// </summary>
        /// <param name="field">The header field name.</param>
        /// <param name="value">The header value.</param>
        /// <param name="keyCaseInsensitive">
        /// Whether the names of the keys associated with the
        /// header should be considered case-insensitive.
        /// </param>
        public NamedParametersHttpHeader(
            string field, string value,
            bool keyCaseInsensitive = false
            )
            : base(field, value)
        {
            IEqualityComparer<string> cmp = keyCaseInsensitive
                ? (IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase
                : (IEqualityComparer<string>)EqualityComparer<string>.Default
                ;
            StringBuilder secb = new StringBuilder();
            int index;
            // Extract the header's main value from the string.
            for (index = 0; index < value.Length; index++)
            {         
                if (value[index] == ';')
                {
                    base.Value = secb.ToString();
                    secb.Clear();
                    break;
                }
                else
                {
                    secb.Append(value[index]);
                }
            }

            if (++index < value.Length)
                _nvPairs = NameValueHttpHeader.ParsePairs(
                    value.Substring(index)
                    );
            else _nvPairs = new Dictionary<string, string>(cmp);
        }
        /// <summary>
        /// Creates a new NamedParametersHttpHeader from an HttpHeader.
        /// </summary>
        /// <param name="header">The header to create from.</param>
        /// <param name="keyCaseInsensitive">
        /// Whether the names of the keys associated with the
        /// header should be considered case-insensitive.
        /// </param>
        public NamedParametersHttpHeader(
            HttpHeader header,
            bool keyCaseInsensitive = false
            ) : this(header.Field, header.Value, keyCaseInsensitive)
        { 

        }
        /// <summary>
        /// Creates a NamedParametersHttpHeader from a raw header string.
        /// </summary>
        /// <param name="httpHeader">The header string to parse.</param>
        /// <param name="keyCaseInsensitive">
        /// Whether the names of the keys associated with the
        /// header should be considered case-insensitive.
        /// </param>
        public NamedParametersHttpHeader(
            string httpHeader,
            bool keyCaseInsensitive = false
            ) : this(HttpHeader.Parse(httpHeader), keyCaseInsensitive) 
        {

        }

        /// <summary>
        /// Returns a string that represents the NamedParametersHttpHeader instance.
        /// </summary>
        /// <returns>A string that represents the NamedParametersHttpHeader instance.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}: {1}; ", this.Field, this.Value);

            foreach (var pair in this.Pairs)
                sb.AppendFormat("{0}={1}; ", pair.Key, pair.Value);

            return sb.ToString();
        }

        /// <summary>
        /// The name-value pairs associated with the header.
        /// </summary>
        public Dictionary<string, string> Pairs
        {
            get { return _nvPairs; }
        }
    }
}
