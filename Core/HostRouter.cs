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
        }

        /// <summary>
        /// Finds any matching virtual hosts based on the
        /// provided hostname and port.
        /// </summary>
        /// <param name="host">The requested hostname.</param>
        /// <param name="port">The port of the requested host.</param>
        /// <returns>
        ///     A list of tuples containing matching
        ///     VirtualHost classes as well as any parameters
        ///     extracted from the format and host strings.
        /// </returns>
        public Tuple<VirtualHost, dynamic> Find(
            string host,
            ushort port = VirtualHost.AnyPort
            )
        {
            /* Virtual host formats are much the same as
             * route formats. They can contain variables
             * (although not unbounded ones), and, as with
             * route formats, any formats without variables
             * should be considered a better match than
             * any formats with variables.
             */

            List<dynamic> hostParams = new List<dynamic>();

            return _hosts
                .Where(v => v.TryMatch(host, port, hostParams.Add))
                .ToList()
                .Zip(hostParams, (v, p) => new Tuple<VirtualHost, dynamic>(v, p))
                .OrderByDescending(t => t.Item2 is Empty)
                .FirstOrDefault();
        }

        /// <summary>
        /// Adds a virtual host to the set of hosts.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided router is null.
        /// </exception>
        public void AddHost(string format, ushort port = VirtualHost.AnyPort)
        {
            this.AddHost(
                format: format,
                port:   port,
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

            _hosts.Add(vhost);
        }
    }
}
