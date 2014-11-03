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
        public KeyValueHTTPHeader(string fieldName, Dictionary<string, object> values)
            : base(
                fieldName,
                values
                    .Aggregate(new StringBuilder(), (b, o) => b.AppendFormat("{0}={1};", o.Key, o.Value))
                    .ToString()
            ) { }

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
                    dict.Add(fBuilder.ToString(), vBuilder.ToString());
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
