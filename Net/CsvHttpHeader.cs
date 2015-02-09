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
            this.Items = new List<string>();

            fieldValue = fieldValue.Trim(Whitespace.ToCharArray());
            // Whether we're in a quoted string.
            bool qStr = false;
            // What we'll be using to build each item in the collection.
            StringBuilder itemBuilder = new StringBuilder();
            foreach (char c in fieldValue)
            {
                if (qStr)
                {
                    if (c == QUOTE)
                    {
                        qStr = false;
                        this.Items.Add(itemBuilder.ToString());
                    }
                    else
                    {
                        itemBuilder.Append(c);
                    }
                }
                else
                {
                    if (c == QUOTE)
                    {
                        // If the quote is in the middle of an item,
                        // we'll assume it's meant to be a quote literal
                        // and not the start of a quoted string.
                        //
                        // We can tell whether the quote is in the middle
                        // of an item by testing the length of the string
                        // builder. If the length is greater than 0, we're
                        // in the middle of an item.
                        if (itemBuilder.Length == 0)
                        {
                            qStr = true;
                        }
                        else
                        {
                            itemBuilder.Append(c);
                        }
                    }
                    else if (c == SEPARATOR)
                    {
                        this.Items.Add(itemBuilder.ToString());
                        itemBuilder.Clear();
                    }
                    /* If we're not in a quoted string, we'll
                     * ignore any whitespace.
                     */
                    else if (Whitespace.Contains(c))
                    {
                        continue;
                    }
                    else
                    {
                        itemBuilder.Append(c);
                    }
                }
            }

            // The last item won't be added to the collection
            // automatically because there isn't a separator
            // character. This means we need to add it ourselves.
            //
            // If we finish in a quoted string, we'll also want to
            // add a quote to the start of the item. Any opening
            // quotes without a closing quote we'll consider a quote
            // literal.
            if (qStr)
            {
                itemBuilder.Insert(0, QUOTE);
            }

            this.Items.Add(itemBuilder.ToString());
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
