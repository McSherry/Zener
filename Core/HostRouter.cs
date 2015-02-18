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
    /// A class for routing to virtual hosts.
    /// </summary>
    public sealed class HostRouter
        : ICollection<VirtualHost>
    {
        private List<VirtualHost> _hosts;
        private IPAddress _defaultIp;
        private object _lockbox;

        /// <summary>
        /// Creates a new HostRouter.
        /// </summary>
        /// <param name="defaultBindAddress">
        ///     The default IP address for each virtual
        ///     host. This will be used if an IP address
        ///     to bind to is not specified.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided default IP
        ///     address is null.
        /// </exception>
        public HostRouter(IPAddress defaultBindAddress)
        {
            if (defaultBindAddress == null)
            {
                throw new ArgumentNullException(
                    "The default IP address must not be null."
                    );
            }

            _hosts = new List<VirtualHost>();
            _defaultIp = defaultBindAddress;
            _lockbox = new object();
        }

        /// <summary>
        /// The IP address that virtual hosts will be bound to by
        /// default. This is used when no IP address is specified
        /// whilst adding virtual hosts to the router.
        /// </summary>
        public IPAddress DefaultBindAddress
        {
            get { return _defaultIp; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(
                        "Cannot set the default bind address to null."
                        );
                }

                _defaultIp = value;
            }
        }
        /// <summary>
        /// The number of virtual hosts within the host router.
        /// </summary>
        public int Count
        {
            get { return _hosts.Count; }
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
        public Tuple<VirtualHost, dynamic> Find(string host, ushort port)
        {
            /* Virtual host formats are much the same as
             * route formats. They can contain variables
             * (although not unbounded ones), and, as with
             * route formats, any formats without variables
             * should be considered a better match than
             * any formats with variables.
             */

            List<dynamic> hostParams = new List<dynamic>();

            lock (_lockbox)
            {
                return _hosts
                    .Where(v => v.TryMatch(host, port, hostParams.Add))
                    .ToList()
                    .Zip(hostParams, (v, p) => new Tuple<VirtualHost, dynamic>(v, p))
                    .OrderBy(t => t.Item1.IsWildcard())
                    .ThenByDescending(t => t.Item2 is Empty)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Adds a virtual host to the set of hosts, using
        /// the default IP address and accepting any port.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <param name="port">The port to bind the virtual host to.</param>
        public void AddHost(string format, ushort port)
        {
            var vhost = new VirtualHost(
                format:         format,
                bindAddress:    this.DefaultBindAddress,
                port:           port,
                routes:         new Router()
                );

            this.AddHost(vhost);
        }
        /// <summary>
        /// Adds a virtual host to the set of hosts,
        /// using the default bind address as the address
        /// to bind to.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <param name="port">The port to bind the virtual host to.</param>
        /// <param name="routes">The set of routes associated with the virtual host.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the Router passed to the method is null.
        /// </exception>
        public void AddHost(string format, ushort port, Router routes)
        {
            var vhost = new VirtualHost(
                format:         format,
                bindAddress:    this.DefaultBindAddress,
                port:           port,
                routes:         routes
                );

            this.AddHost(vhost);
        }
        /// <summary>
        /// Adds a virtual host to the set of hosts.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <param name="bindAddress">The IP address to bind the virtual host to.</param>
        /// <param name="port">The port to bind the virtual host to.</param>
        /// <param name="routes">The set of routes associated with the virtual host.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the IPAddress or Router passed to
        ///     the method is null.
        /// </exception>
        public void AddHost(
            string format,
            IPAddress bindAddress, ushort port,
            Router routes
            )
        {
            var vhost = new VirtualHost(
                format:         format,
                bindAddress:    bindAddress,
                port:           port,
                routes:         routes
                );

            this.AddHost(vhost);
        }
        /// <summary>
        /// Adds a virtual host to the set of hosts.
        /// </summary>
        /// <param name="host">The virtual host to add.</param>
        public void AddHost(VirtualHost host)
        {
            lock (_lockbox)
            {
                _hosts.RemoveAll(
                    v => v.Format.Equals(host.Format) && v.Port == host.Port
                    );

                _hosts.Add(host);

                // If there are no handlers, this will be
                // null, and we won't be able to fire it.
                if (this.HostAdded != null)
                {
                    // Fire the event.
                    this.HostAdded(this, host);
                }
            }
        }

        /// <summary>
        /// Fired when a virtual host is added to the host router.
        /// </summary>
        public event EventHandler<VirtualHost> HostAdded;

        void ICollection<VirtualHost>.Add(VirtualHost vhost)
        {
            this.AddHost(vhost);
        }
        void ICollection<VirtualHost>.CopyTo(VirtualHost[] vhosts, int arrayIndex)
        {
            _hosts.CopyTo(vhosts, arrayIndex);
        }
        bool ICollection<VirtualHost>.Contains(VirtualHost vhost)
        {
            return _hosts.Contains(vhost);
        }
        void ICollection<VirtualHost>.Clear()
        {
            throw new NotSupportedException();
        }
        bool ICollection<VirtualHost>.Remove(VirtualHost vhost)
        {
            throw new NotSupportedException();
        }
        bool ICollection<VirtualHost>.IsReadOnly
        {
            get { return false; }
        }

        IEnumerator<VirtualHost> IEnumerable<VirtualHost>.GetEnumerator()
        {
            return _hosts.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _hosts.GetEnumerator();
        }
    }
}
