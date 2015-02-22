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
    /// comma-separated values with associated weightings to
    /// specify preference.
    /// </summary>
    public sealed class OrderedCsvHttpHeader
        : CsvHttpHeader
    {
        private const int
            // The number of decimal places to take in
            // to account in q-values.
            DecimalPlaces       = 3
            ;
        private const decimal
            // The default q-value we'll use if an item
            // does not have a q-value.
            // The minimum acceptable weighting.
            MinimumWeighting    = 0.000M,
            // The maximum acceptable weighting.
            MaximumWeighting    = 1.000M,
            DefaultWeighting    = MaximumWeighting
            ;
        private const char
            Delimiter = ';'
            ;
        private const string
            // The key used to store the weighting value.
            WeightingKey        = "q"
            ;

        /// <summary>
        /// Creates a new OrderedCsvHttpHeader from an HttpHeader class.
        /// </summary>
        /// <param name="header">
        /// The HttpHeader class to create this OrderedCsvHttpHeader from.
        /// </param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when:
        ///         1. The provided field name is null, zero-length, or whitespace.
        ///         2. The provided value is null, zero-length, or whitespace.
        ///         3. The field name contains a carriage return or line feed character.
        ///         4. The value contains a carriage return or line feed character.
        /// </exception>
        public OrderedCsvHttpHeader(HttpHeader header)
            : this(header.Field, header.Value)
        {

        }
        /// <summary>
        /// Creates a new OrderedCsvHttpHeader from a header field and value.
        /// </summary>
        /// <param name="fieldName">
        /// The field name of the header (for example, "Accept-Encoding").
        /// </param>
        /// <param name="fieldValue">
        /// The value of the header.
        /// </param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when:
        ///         1. The provided field name is null, zero-length, or whitespace.
        ///         2. The provided value is null, zero-length, or whitespace.
        ///         3. The field name contains a carriage return or line feed character.
        ///         4. The value contains a carriage return or line feed character.
        ///         5. One or more of the comma-separated items cannot be parsed as a
        ///            set of key-value pairs.
        ///         6. One or more of the q-values is invalid.
        /// </exception>
        public OrderedCsvHttpHeader(string fieldName, string fieldValue)
            : base(fieldName, fieldValue)
        {
            decimal[] weightings = new decimal[base.Items.Count];
            for (int i = 0; i < base.Items.Count; i++)
            {
                IDictionary<string, string> kvp;
                try
                {
                    kvp = Networking
                        .ParseUnquotedKeyValues(base.Items.ElementAt(i));
                }
                catch (ArgumentException aex)
                {
                    throw new ArgumentException(
                        "The header contains a comma-separated item that " +
                        "cannot be parsed as a set of key-value pairs.",
                        aex
                        );
                }

                // Attempt to retrieve the item's weighting.
                string wStr;
                if (kvp.TryGetValue(WeightingKey, out wStr))
                {
                    // Attempt to parse the value as a decimal.
                    decimal iWeight;
                    if (Decimal.TryParse(wStr, out iWeight))
                    {
                        // Round the q-value to the number of decimal
                        // places we're considering valid. By default,
                        // this is three decimal places.
                        iWeight = Math.Round(iWeight, DecimalPlaces);

                        // If the value is greater than or less than
                        // the maximum or minimum, set it to the
                        // maximum or minimum.
                        if (iWeight > MaximumWeighting)
                        {
                            iWeight = MaximumWeighting;
                        }
                        else if (iWeight < MinimumWeighting)
                        {
                            iWeight = MinimumWeighting;
                        }

                        // Set the appropriate index with the normalised
                        // weighting.
                        weightings[i] = iWeight;
                    }
                    // If we can't parse it as a decimal, throw an exception.
                    else
                    {
                        throw new ArgumentException(
                            "The header contains an invalid q-value (invalid decimal)."
                            );
                    }
                }
                // If we get here, the item doesn't have a specified
                // weighting.
                else
                {
                    // Assign the item the default weighting. By default,
                    // this is the maximum weighting.
                    weightings[i] = DefaultWeighting;
                }
            }

            base.Items = base.Items
                // Associate the weightings with the appropriate
                // comma-separated values.
                .Zip(weightings, (i, w) => new { i, w })
                // Order them, ensuring that the largest weighting
                // is placed first.
                .OrderByDescending(o => o.w)
                // Retrieve the now-ordered items. We'll leave the
                // item strings unmodified (this means that the
                // q-values are still present).
                .Select(o => o.i)
                // base.Items is an ICollection, so we need to
                // set it as something that implements that interface.
                .ToList();
        }
    }
}
