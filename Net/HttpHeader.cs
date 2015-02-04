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
using System.IO;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// Implements a basic HTTP header.
    /// </summary>
    public class HttpHeader
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
        /// <exception cref="System.ArgumentException">
        ///     Thrown when:
        ///         1. The provided field name is null, zero-length, or whitespace.
        ///         2. The provided value is null, zero-length, or whitespace.
        ///         3. The field name contains a carriage return or line feed character.
        ///         4. The value contains a carriage return or line feed character.
        /// </exception>
        public HttpHeader(string fieldName, string value)
        {
            fieldName = fieldName.Trim(TRIM_CHARS);
            value = value.Trim(TRIM_CHARS);

            if (String.IsNullOrWhiteSpace(fieldName))
            {
                throw new ArgumentException
                ("A field name must be provided.", "fieldName");
            }

            if (String.IsNullOrWhiteSpace(value))
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
        /// Converts a string to an HttpHeader.
        /// </summary>
        /// <param name="headerLine">A single line containing the header text.</param>
        /// <returns>An HttpHeader equivalent to the provided string.</returns>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when:
        ///         1. The provided field name is null, zero-length, or whitespace.
        ///         2. The provided value is null, zero-length, or whitespace.
        ///         3. The field name contains a carriage return or line feed character.
        ///         4. The value contains a carriage return or line feed character.
        ///         5. There is a space between the field name and colon.
        /// </exception>
        public static HttpHeader Parse(string headerLine)
        {
            headerLine = headerLine.Trim(
                TRIM_CHARS
                    .Concat(new[] { ' ', '\r', '\n' })
                    .ToArray()
                );

            StringBuilder fieldBuilder = new StringBuilder();

            int i = 0;
            for (; i < headerLine.Length;)
            {
                if (headerLine[i] == ':')
                {
                    // RFC 7230, section 3.2.4
                    //
                    // RFC 7230 specifies that there must not be
                    // whitespace between the header field name and
                    // the colon at the end of the field name.
                    if (headerLine[i - 1] == ' ')
                    {
                        throw new ArgumentException(
                            "There must not be whitespace between the colon and field name."
                            );
                    }

                    break;
                }
                fieldBuilder.Append(headerLine[i++]);
            }

            return new HttpHeader(fieldBuilder.ToString(), headerLine.Substring(++i));
        }
        /// <summary>
        /// Converts a sequence of characters to a set of HttpHeaders.
        /// </summary>
        /// <param name="text">The text containing the headers.</param>
        /// <returns>An enumerable containing parsed headers.</returns>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when:
        ///         1. A provided field name is null, zero-length, or whitespace.
        ///         2. A provided value is null, zero-length, or whitespace.
        ///         3. A field name contains a carriage return or line feed character.
        ///         4. A value contains a carriage return or line feed character.
        /// </exception>
        public static IEnumerable<HttpHeader> ParseMany(TextReader text)
        {
            List<string> lines = new List<string>();

            while (true)
            {
                string line = text.ReadLine();
                // If the line's empty, we're at the end of the
                // headers, so there aren't any further lines to
                // add to our list of lines. The TextReader will
                // also return null if we've run out of lines to
                // read.
                if (String.IsNullOrEmpty(line)) break;

                lines.Add(line);
            }

            int initialCount = lines.Count;
            // Although now deprecated in RFC 7230, it is possible
            // that some clients will send multi-line headers. We
            // can determine which lines are part of multi-line headers
            // by checking for a space at the start of the line.
            //
            // We use an anonymous object with the index of the line to
            // merge and the contents of the line itself.
            var linesToMerge = Enumerable
                .Range(0, lines.Count)
                .Zip(lines, (i, l) => new { i, l })
                .Where(ao => TRIM_CHARS.Contains(ao.l[0]))
                .ToList();

            foreach (var ml in linesToMerge)
            {
                // We're going to be modifying our list, which means
                // indices are going to change. To work with the
                // changing indices, we need to find the difference
                // from our initial count of lines.
                int lengDiff = (initialCount - lines.Count);

                // We then take the index of the line to merge, subtract
                // the difference to make sure we've adjusted for any removed
                // lines, then subtract one so we get the index of the line to
                // merge into.
                int mergeIntoIndex = ml.i - lengDiff - 1;
                lines[mergeIntoIndex] = String.Format(
                    // Per RFC 7230 section 3.2.4, each newline in a multi-line
                    // header should be replaced with a single space. This is
                    // pretty simple to do.
                    "{0} {1}",
                    // The first part of the line needs to go first.
                    lines[mergeIntoIndex],
                    // The bit we're merging needs to go after. Before we can
                    // merge it, we need to trim from the start and end any
                    // whitespace.
                    ml.l.Trim(TRIM_CHARS)
                    );

                // We've merged the lines, so now we need to remove the line
                // fragment that we've just merged so we don't need to merge it
                // again.
                lines.RemoveAt(ml.i - lengDiff);
            }

            foreach (string line in lines)
                yield return HttpHeader.Parse(line);
        }
    }
}
