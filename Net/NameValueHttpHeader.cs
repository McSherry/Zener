/*
 *      Copyright (c) 2014-2015, SynapLink, LLC
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
    /// Represents an HTTP header where the value is a set
    /// of name-value pairs.
    /// </summary>
    public class NameValueHttpHeader : BasicHttpHeader
    {
        private static Dictionary<char, char> EscapeCodes
            = new Dictionary<char, char>()
            {
                { 'n', '\n' },
                { 'r', '\r' },
                { '0', '\0' },
                { 'b', '\b' },
                { 't', '\t' }
            };

        private Dictionary<string, string> _nvPairs;

        /// <summary>
        /// Parses a set of name-value pairs from a string.
        /// </summary>
        /// <param name="pairString">The string containing the pairs to parse.</param>
        /// <returns>A dictionary containing the pairs.</returns>
        internal static Dictionary<string, string> ParsePairs(string pairString)
        {
            Dictionary<string, string> pairs = new Dictionary<string, string>();

            StringBuilder nBuilder = new StringBuilder();
            StringBuilder vBuilder = new StringBuilder();
            bool inName = true,
                inQString = false;
            for (int i = 0; i < pairString.Length; i++)
            {
                // If we're not in a quoted string, white-space
                // can be ignored.
                if (!inQString && pairString[i] == ' ') continue;
                // Backward-slash is used to escape other characters.
                else if (pairString[i] == '\\')
                {
                    // Determine the character represented by the
                    // escape code. If we don't have a match, use the
                    // character that comes after the backward-slash.
                    char appChar = EscapeCodes
                        .Where(ec => ec.Key.Equals(pairString[i]))
                        .Select(ec => ec.Value)
                        .DefaultIfEmpty(pairString[i + 1])
                        .First();
                    // Advance past the escape code (\ + 1 char)
                    i += 2;

                    // Determine which StringBuilder to append to,
                    // then append to it.
                    if (inName) nBuilder.Append(appChar);
                    else vBuilder.Append(appChar);
                }
                // If we're not in a quoted string, and not in the
                // name-value pair's name, a double-quote indicates
                // the start of a quoted string.
                else if (!inQString && !inName && pairString[i] == '"')
                {
                    inQString = true;
                }
                // If we're in a quoted string and find a double-quote,
                // it means we've reached the end of the quoted string.
                else if (inQString && pairString[i] == '"')
                {
                    inQString = false;
                }
                // If we're not in a quoted string and find a semicolon,
                // we've reached the end of the current pair.
                else if (!inQString && pairString[i] == ';')
                {
                    inName = true;
                    pairs[nBuilder.ToString()] = vBuilder.ToString();
                    nBuilder.Clear();
                    vBuilder.Clear();
                }
                else if (inName && pairString[i] == '=')
                {
                    inName = false;
                }
                else if (inName)
                {
                    nBuilder.Append(pairString[i]);
                }
                else
                {
                    vBuilder.Append(pairString[i]);
                }
            }

            // If the last pair isn't terminated with a semicolon, it won't
            // be added. To ensure it is, we check to see if name is empty.
            // If name isn't empty, we know that there is an additional pair
            // to be appended.
            if (!String.IsNullOrEmpty(nBuilder.ToString()))
                pairs.Add(nBuilder.ToString(), vBuilder.ToString());

            return pairs;
        }

        internal static string PairsToString(IDictionary<string, string> pairs)
        {
            return pairs
                .Aggregate(
                    new StringBuilder(),
                    (sb, pair) => sb.AppendFormat("{0}={1};", pair.Key, pair.Value)
                    )
                .ToString();
        }

        /// <summary>
        /// Creates a NameValueHttpHeader from a field-name and value.
        /// </summary>
        /// <param name="field">The field-name of the header.</param>
        /// <param name="value">The contents of the header.</param>
        public NameValueHttpHeader(string field, string value)
            : base(field, value)
        {
            _nvPairs = NameValueHttpHeader.ParsePairs(value);
        }
        /// <summary>
        /// Creates a NameValueHttpHeader from a BasicHttpHeader.
        /// </summary>
        /// <param name="header">The BasicHttpHeader to create from.</param>
        public NameValueHttpHeader(BasicHttpHeader header)
            : this(header.Field, header.Value)
        {

        }
        /// <summary>
        /// Creates a NameValueHttpHeader from a header string.
        /// </summary>
        /// <param name="httpHeader">The header string, containing the field and content.</param>
        public NameValueHttpHeader(string httpHeader)
            : this(BasicHttpHeader.Parse(httpHeader))
        {

        }
        /// <summary>
        /// Creates a NameValueHttpHeader from a field name and a
        /// set of name-value pairs.
        /// </summary>
        /// <param name="field">The field name of the header.</param>
        /// <param name="pairs">The pairs that the header's value comprises.</param>
        public NameValueHttpHeader(string field, IDictionary<string, string> pairs)
            : this(field, PairsToString(pairs))
        {

        }

        /// <summary>
        /// The name-value pairs contained within the header's
        /// content.
        /// </summary>
        public Dictionary<string, string> Pairs
        {
            get { return _nvPairs; }
        }
    }
}
