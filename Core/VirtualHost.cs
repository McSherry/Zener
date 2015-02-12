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

        /// <summary>
        /// Creates a new VirtualHost.
        /// </summary>
        /// <param name="format">The hostname for this virtual host.</param>
        /// <param name="port">The port that this virtual host will accept.</param>
        /// <param name="routes">The routes associated with this host.</param>
        public VirtualHost(string format, ushort port, Router routes)
        {
            this.Format = format.Trim(' ', DELIMITER);
            this.Port = port;
            this.Router = routes;
        }

        /// <summary>
        /// The value to use if any port is acceptable for the virtual
        /// host.
        /// </summary>
        public const ushort AnyPort = 0x0000;

        /// <summary>
        /// The format of the virtual host's hostname.
        /// </summary>
        public string Format
        {
            get;
            private set;
        }
        /// <summary>
        /// The TCP port that is considered acceptable for
        /// this virtual host.
        /// </summary>
        public ushort Port
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
        public bool TryMatch(string host, ushort port, out dynamic parameters)
        {
            // If our format string is null/empty/whitespace, or
            // is an asterisk, it will be considered a wildcard
            // and/or default virtual host.
            if (this.IsWildcard())
            {
                parameters = new Empty();
                return true;
            }

            return 
                Routing.IsFormatMatch(
                    path:           host,
                    format:         Format,
                    delimiter:      DELIMITER,
                    parameters:     out parameters,
                    allowUnbounded: false
                    )
                && (port == this.Port);
        }
    }
}
