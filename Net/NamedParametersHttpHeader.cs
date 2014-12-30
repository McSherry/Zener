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
using System.Threading.Tasks;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// Represents an HTTP header where the value has a set of associated
    /// name-value parameters.
    /// </summary>
    public class NamedParametersHttpHeader : BasicHttpHeader
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
            _nvPairs = new Dictionary<string, string>();
            StringBuilder secb = new StringBuilder();
            string tStore = String.Empty;

            bool
                hdr = true,
                str = false,
                key = true;

            for (int i = 0; i < value.Length; i++)
            {
                if (hdr)
                {                
                    if (value[i] == ';')
                    {
                        base.Value = secb.ToString();
                        secb.Clear();
                        hdr = false;
                    }
                    else
                    {
                        secb.Append(value[i]);
                    }
                }
                else if (key && !str && value[i] == '=')
                {
                    tStore = secb.ToString();
                    secb.Clear();
                    key = false;
                }
                else if (!str && value[i] == '"')
                {
                    str = true;
                }
                else if (str && value[i] == '\\')
                {
                    // Backward-slash (\) is used to escape
                    // other characters, like quotes.
                    secb.Append(value[++i]);
                }
                else if (str && value[i] == '"')
                {
                    str = false;
                }
                else if (!str && value[i] == ' ' && value[i - 1] == ' ')
                {
                    continue;
                }
                else if (!str && value[i] == ';')
                {
                    _nvPairs[tStore.Trim(BasicHttpHeader.TRIM_CHARS)] = secb.ToString();
                    secb.Clear();
                    key = true;
                }
                else
                {
                    secb.Append(value[i]);
                }
            }

            if (key && secb.Length > 0)
            {
                _nvPairs[secb.ToString().Trim(BasicHttpHeader.TRIM_CHARS)] = String.Empty;
            }
            else
            {
                _nvPairs[tStore.Trim(BasicHttpHeader.TRIM_CHARS)] = secb.ToString();
                    
            }
        }
        /// <summary>
        /// Creates a new NamedParametersHttpHeader from a BasicHttpHeader.
        /// </summary>
        /// <param name="header">The header to create from.</param>
        public NamedParametersHttpHeader(BasicHttpHeader header)
            : this(header.Field, header.Value) { }
        /// <summary>
        /// Creates a NamedParametersHttpHeader from a raw header string.
        /// </summary>
        /// <param name="httpHeader">The header string to parse.</param>
        public NamedParametersHttpHeader(string httpHeader)
            : this(BasicHttpHeader.Parse(httpHeader)) { }

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
