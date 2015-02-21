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
using System.Net;

namespace McSherry.Zener.Core
{
    /// <summary>
    /// A class representing a virtual host.
    /// </summary>
    public sealed class VirtualHost
        : EventArgs
    {
        private const char 
            DELIMITER   = '.',
            ASTERISK    = '*'
            ;

        static VirtualHost()
        {
            VirtualHost.NameComparer = StringComparer.OrdinalIgnoreCase;
        }

        /// <summary>
        /// The IEqualityComparer to be used when
        /// comparing the names of virtual hosts.
        /// </summary>
        public static StringComparer NameComparer
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a new VirtualHost.
        /// </summary>
        /// <param name="format">The hostname for this virtual host.</param>
        /// <param name="bindAddress">The IP address to bind this virtual host to.</param>
        /// <param name="port">The port that this virtual host will accept.</param>
        /// <param name="routes">The routes associated with this host.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided bind address or the provided
        ///     Router are null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the specified port is outside the allowable range.
        /// </exception>
        public VirtualHost(
            string format, 
            IPAddress bindAddress, ushort port, 
            Router routes
            )
        {
            if (String.IsNullOrWhiteSpace(format))
            {
                throw new ArgumentNullException(
                    "The provided format string must not be empty, whitespace, or null."
                    );
            }

            if (bindAddress == null)
            {
                throw new ArgumentNullException(
                    "The provided IPAddress must not be null."
                    );
            }

            if (routes == null)
            {
                throw new ArgumentNullException(
                    "The provided Router must not be null."
                    );
            }

            if (port == 0)
            {
                throw new ArgumentException(
                    "The specified port must be in the range 1-65535."
                    );
            }

            this.Format = format.Trim(' ', DELIMITER);
            this.BindAddress = bindAddress;
            this.Port = port;
            this.Routes = routes;
        }

        /// <summary>
        /// The value to use if any hostname is acceptable for the
        /// virtual host.
        /// </summary>
        public const string AnyHostname = "*";

        /// <summary>
        /// The name given to the virtual host.
        /// </summary>
        public string Name
        {
            get;
            private set;
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
        /// The IP address to bind this virtual host to.
        /// </summary>
        public IPAddress BindAddress
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
        public Router Routes
        {
            get;
            private set;
        }

        /// <summary>
        /// Attempts to match the provided host with this
        /// virtual host's domain.
        /// </summary>
        /// <param name="host">The hostname to attempt to match.</param>
        /// <param name="port">The port to attempt to match to.</param>
        /// <param name="parameters">
        ///     The parameters extracted from the format string and
        ///     provided hostname
        /// </param>
        /// <returns>True if the provided hostname is a match.</returns>
        public bool TryMatch(string host, ushort port, out dynamic parameters)
        {
            // If the port doesn't match, there's no point in checking whether
            // the hostname matches. As a result, we set the parameters to empty
            // and return false.
            if (port != this.Port)
            {
                parameters = new Empty();

                return false;
            }

            // Domain wildcards match any hostname.
            if (this.IsWildcard())
            {
                // If it's a wildcard, it can't have any parameters, so
                // we set the parameters argument to empty.
                parameters = new Empty();
                // Wildcards match everything, so we return true.
                return true;
            }

            // If we're here, we know that ports are a match (so we don't need a
            // complex condition), and we know that the hostname isn't a wildcard.
            //
            // Thankfully, route-matching code has been split off from the Route
            // class in to its own method, so we can eliminate dupe code and check
            // hostname/host format equality with a simple call to a single method.
            //
            // Hostname variables cannot be unbounded, as the variables are likely
            // to only ever occur at the start (or in the middle) of a virtual host
            // format string (see below).
            //
            //      [username].example.com
            //      directory.[server].example.org
            //
            // Unbounded variables in these locations would never have a match.
            return Routing.IsFormatMatch(
                path:           host,
                format:         Format,
                delimiter:      DELIMITER,
                parameters:     out parameters,
                allowUnbounded: false
                );
        }
        /// <summary>
        /// Attempts to match the provided host with this
        /// virtual host's domain.
        /// </summary>
        /// <param name="host">The hostname to attempt to match.</param>
        /// <param name="port">The port to attempt to match to.</param>
        /// <param name="callback">A callback that is passed any parameters.</param>
        /// <returns>True if the provided hostname is a match.</returns>
        public bool TryMatch(string host, ushort port, Action<dynamic> callback)
        {
            dynamic parameters;
            bool success = this.TryMatch(host, port, out parameters);

            if (success) callback(parameters);

            return success;
        }

        /// <summary>
        /// Retrieves the string representation of this virtual host.
        /// </summary>
        /// <returns>The string representation of the virtual host.</returns>
        public override string ToString()
        {
            return String.Format(
                "{0}::{1}",
                this.Format,
                this.Port
                );
        }
    }
}
