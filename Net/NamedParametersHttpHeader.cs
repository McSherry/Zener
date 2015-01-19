/*
 *      Copyright (c) 2014-2015, SynapLink, LLC
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

namespace SynapLink.Zener.Net
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
        public NamedParametersHttpHeader(string field, string value)
            : base(field, value)
        {
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
            else _nvPairs = new Dictionary<string, string>();
        }
        /// <summary>
        /// Creates a new NamedParametersHttpHeader from an HttpHeader.
        /// </summary>
        /// <param name="header">The header to create from.</param>
        public NamedParametersHttpHeader(HttpHeader header)
            : this(header.Field, header.Value) { }
        /// <summary>
        /// Creates a NamedParametersHttpHeader from a raw header string.
        /// </summary>
        /// <param name="httpHeader">The header string to parse.</param>
        public NamedParametersHttpHeader(string httpHeader)
            : this(HttpHeader.Parse(httpHeader)) { }

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
