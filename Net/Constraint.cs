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
using System.Text.RegularExpressions;

namespace McSherry.Zener.Net
{
    /// <summary>
    /// A class representing a constraint on the value of
    /// a variable in a format string.
    /// </summary>
    public sealed class Constraint
    {
        // The regex we'll use to match values.
        private readonly Regex _regex;
        // Whether we actually make a comparison. If this
        // is false, we always return true for a match.
        private readonly bool _doCompare;

        /// <summary>
        /// Creates a new Constraint.
        /// </summary>
        /// <param name="name">The name of the parameter to constrain.</param>
        /// <param name="regex">The regular expresson to constrain the value to.</param>
        public Constraint(string name, string regex)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "The name of the parameter cannot be null, empty, or white-space."
                    );
            }

            this.Name = name;
            _doCompare = String.IsNullOrEmpty(this.Regex = regex);

            if (_doCompare) _regex = new Regex(regex);
        }

        /// <summary>
        /// The name of the parameter to constrain.
        /// </summary>
        public string Name
        {
            get;
            private set;
        }
        /// <summary>
        /// The regular expression that the value of
        /// the parameter will be checked against.
        /// </summary>
        public string Regex
        {
            get;
            private set;
        }

        /// <summary>
        /// Determines whether the provided value matches the constraint.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the provided value is considered a match.</returns>
        public bool Match(string value)
        {
            return !_doCompare || _regex.IsMatch(value);
        }
    }
}
