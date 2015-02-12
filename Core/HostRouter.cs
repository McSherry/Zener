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
    /// A class for routing to virtual hosts.
    /// </summary>
    public sealed class HostRouter
    {
        private List<VirtualHost> _hosts;

        /// <summary>
        /// Creates a new HostRouter.
        /// </summary>
        public HostRouter()
        {
            _hosts = new List<VirtualHost>();
            // Make sure the router has a default route after
            // initialisation.
            _hosts.Add(new VirtualHost(String.Empty));
        }

        /// <summary>
        /// Adds a virtual host to the set of hosts.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided router is null.
        /// </exception>
        public void AddHost(string format)
        {
            this.AddHost(
                format: format,
                routes: new Router()
                );
        }
        /// <summary>
        /// Adds a virtual host to the set of hosts.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <param name="routes">The router to use with this virtual host.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided router is null.
        /// </exception>
        public void AddHost(string format, Router routes)
        {
            this.AddHost(
                format:     format,
                port:       VirtualHost.AnyPort,
                routes:     routes
                );
        }
        /// <summary>
        /// Adds a virtual host to the set of hosts.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <param name="port">The port the virtual host will accept.</param>
        /// <param name="routes">The router to use with this virtual host.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided router is null.
        /// </exception>
        public void AddHost(string format, ushort port, Router routes)
        {
            if (routes == null)
            {
                throw new ArgumentNullException(
                    "The provided router must not be null."
                    );
            }

            var vhost = new VirtualHost(format, port, routes);

            // Remove any equivalent virtual hosts from the
            // set of hosts.
            _hosts.RemoveAll(
                v => v.Format.Equals(vhost.Format) && v.Port == vhost.Port
                );
        }
    }
}
