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

namespace McSherry.Zener.Core
{
    /// <summary>
    /// A class representing a virtual host.
    /// </summary>
    public sealed class VirtualHost
    {
        private const char 
            DELIMITER   = '.',
            ASTERISK    = '*'
            ;

        public VirtualHost(string format)
        {
            this.Format = format.Trim(' ', DELIMITER);
        }

        /// <summary>
        /// The format of the virtual host's hostname.
        /// </summary>
        public string Format
        {
            get;
            private set;
        }
        /// <summary>
        /// The set of routes associated with this virtual host.
        /// </summary>
        public Router Router
        {
            get;
            private set;
        }

        /// <summary>
        /// Attempts to match the provided host with this
        /// virtual host's domain.
        /// </summary>
        /// <param name="host">The hostname to attempt to match.</param>
        /// <param name="parameters">
        ///     The parameters extracted from the format string and
        ///     provided hostname
        /// </param>
        /// <returns>True if the provided hostname is a match.</returns>
        public bool TryMatch(string host, out dynamic parameters)
        {
            // If our format string is null/empty/whitespace, or
            // is an asterisk, it will be considered a wildcard
            // and/or default virtual host.
            if (this.IsDefault())
            {
                parameters = new Empty();
                return true;
            }

            return Routing.IsFormatMatch(
                path:           host,
                format:         this.Format,
                parameters:     out parameters,
                delimiter:      DELIMITER,
                allowUnbounded: false
                );
        }
    }
}
