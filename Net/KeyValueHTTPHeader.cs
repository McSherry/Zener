using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// Implements an HTTP header with key-value pairs as its value.
    /// </summary>
    public sealed class KeyValueHTTPHeader : BasicHTTPHeader
    {
        private Dictionary<string, object> _values;

        public KeyValueHTTPHeader(string fieldName, Dictionary<string, object> values)
            : base(
                fieldName,
                values
                    .Aggregate(new StringBuilder(), (b, o) => b.AppendFormat("{0}={1};", o.Key, o.Value))
                    .ToString()
            ) 
        {
            _values = values;
        }

        /// <summary>
        /// The key-value pairs stored within the header.
        /// </summary>
        public Dictionary<string, object> Values
        {
            get
            {
                return new Dictionary<string, object>(_values);
            }
            internal set { _values = value; }
        }

        /// <summary>
        /// Converts a string to a KeyValueHTTPHeader.
        /// </summary>
        /// <param name="headerText">The full text of the header.</param>
        /// <returns>A KeyValueHTTPHeader equivalent to the provided string.</returns>
        /// <exception cref="System.ArgumentException"></exception>
        public static KeyValueHTTPHeader Parse(string headerText)
        {
            var basic = BasicHTTPHeader.Parse(headerText);

            StringBuilder fBuilder = new StringBuilder(),
                vBuilder = new StringBuilder();
            Dictionary<string, object> dict = new Dictionary<string, object>();
            bool inVal = false, inString = false;
            foreach (char c in basic.Value)
            {
                if (c == '=' && !inVal && !inString)
                {
                    inVal = true;
                    continue;
                }
                else if (c == ';' && !inString)
                {
                    inVal = false;
                    dict.Add(fBuilder.ToString().Trim(), vBuilder.ToString().Trim());
                    fBuilder.Clear();
                    vBuilder.Clear();
                }
                else if (c == '"')
                {
                    if (!inVal) throw new ArgumentException
                    ("Cannot have quoted string in key-value key.");
                    else if (inString)
                    {
                        inString = true;
                        continue;
                    }
                    else
                    {
                        inString = false;
                        continue;
                    }
                }
                else
                {
                    if (inVal) vBuilder.Append(c);
                    else fBuilder.Append(c);
                }
            }

            return new KeyValueHTTPHeader(basic.Field, dict);
        }
    }
}
