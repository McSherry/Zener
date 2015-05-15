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

using McSherry.Zener.Core;

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
            // The minimum acceptable weighting.
            MinimumWeighting    = 0.000M,
            // The maximum acceptable weighting.
            MaximumWeighting    = 1.000M,
            // The default q-value we'll use if an item
            // does not have a q-value.
            DefaultWeighting    = MaximumWeighting
            ;
        private const char
            Delimiter           = ';'
            ;
        private const string
            // The key used to store the weighting value.
            WeightingKey        = "q"
            ;

        /// <summary>
        /// Converts an indexed dictionary to a string that would be accepted
        /// by the Networking.ParseUnquotedKeyValues method called in
        /// OrderedCsvHttpHeader's constructor.
        /// </summary>
        /// <param name="dict">The dictionary to convert.</param>
        /// <returns>A key-value string representation of the dictionary.</returns>
        private string GetIxDictString(IndexedDictionary<string, string> dict)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var kvp in dict)
            {
                if (kvp.Value == null)
                {
                    sb.AppendFormat("{0}; ", kvp.Key);
                }
                else
                {
                    sb.AppendFormat("{0}={1}; ", kvp.Key, kvp.Value);
                }
            }

            return sb.ToString().Trim(' ', ';');
        }

        /// <summary>
        /// Creates a new OrderedCsvHttpHeader from an HttpHeader class.
        /// </summary>
        /// <param name="header">
        /// The HttpHeader class to create this OrderedCsvHttpHeader from.
        /// </param>
        /// <param name="removeUnacceptable">
        /// Whether to remove any comma-separated values which have a q-value
        /// indicating that they are not acceptable.
        /// </param>
        /// <exception cref="System.ArgumentException">
        /// Thrown when:
        ///     1. The provided field name is null, zero-length, or whitespace.
        ///     2. The provided value is null, zero-length, or whitespace.
        ///     3. The field name contains a carriage return or line feed character.
        ///     4. The value contains a carriage return or line feed character.
        ///     5. One or more of the comma-separated items cannot be parsed as a
        ///        set of key-value pairs.
        ///     6. One or more of the q-values is invalid.
        /// </exception>
        public OrderedCsvHttpHeader(
            HttpHeader header,
            bool removeUnacceptable = false
            ) : this(header.Field, header.Value, removeUnacceptable)
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
        /// <param name="removeUnacceptable">
        /// Whether to remove any comma-separated values which have a q-value
        /// indicating that they are not acceptable.
        /// </param>
        /// <exception cref="System.ArgumentException">
        /// Thrown when:
        ///     1. The provided field name is null, zero-length, or whitespace.
        ///     2. The provided value is null, zero-length, or whitespace.
        ///     3. The field name contains a carriage return or line feed character.
        ///     4. The value contains a carriage return or line feed character.
        ///     5. One or more of the comma-separated items cannot be parsed as a
        ///        set of key-value pairs.
        ///     6. One or more of the q-values is invalid.
        /// </exception>
        public OrderedCsvHttpHeader(
            string fieldName, string fieldValue,
            bool removeUnacceptable = false
            ) : base(fieldName, fieldValue)
        {
            IndexedDictionary<string, string>[] ixDicts = 
                new IndexedDictionary<string,string>[base.Items.Count];
            decimal[] weightings = new decimal[base.Items.Count];
            for (int i = 0; i < base.Items.Count; i++)
            {
                try
                {
                    ixDicts[i] = Networking
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

                var kvp = ixDicts[i];

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

            // Remove any q-values from the indexed dictionaries.
            foreach (var ix in ixDicts) ix.Remove(WeightingKey);

            // Just having two separate code paths is probably going to
            // be faster and more readable than having some weird combinatorial
            // logic in LINQ calls.
            if (removeUnacceptable)
            {
                base.Items = ixDicts
                    // Associate the weightings with the appropriate
                    // comma-separated values.
                    .Zip(weightings, (d, w) => new { d, w })
                    // Remove any items which have the minimum weighting.
                    .Where(o => o.w != MinimumWeighting)
                    // Order them, ensuring that the largest weighting
                    // is placed first.
                    .OrderByDescending(o => o.w)
                    // We then convert the IndexedDictionary instances
                    // to a string. We previously removed any q-values,
                    // so this should now just be the comma-separated
                    // value.
                    .Select(o => this.GetIxDictString(o.d))
                    // base.Items needs to be an ICollection, so we
                    // use the appropriate method. We also make it
                    // read-only.
                    .ToList()
                    .AsReadOnly();
            }
            else
            {
                base.Items = ixDicts
                    // Associate the weightings with the appropriate
                    // comma-separated values.
                    .Zip(weightings, (d, w) => new { d, w })
                    // Order them, ensuring that the largest weighting
                    // is placed first.
                    .OrderByDescending(o => o.w)
                    // We then convert the IndexedDictionary instances
                    // to a string. We previously removed any q-values,
                    // so this should now just be the comma-separated
                    // value.
                    .Select(o => this.GetIxDictString(o.d))
                    // base.Items needs to be an ICollection, so we
                    // use the appropriate method. We also make it
                    // read-only.
                    .ToList()
                    .AsReadOnly();
            }
        }
    }
}
