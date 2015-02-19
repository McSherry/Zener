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
        private ushort _defaultPort;
        private object _lockbox;

        /// <summary>
        /// Creates a new HostRouter.
        /// </summary>
        /// <param name="defaultBindAddress">
        ///     The default IP address for each virtual
        ///     host. This will be used if an IP address
        ///     to bind to is not specified.
        /// </param>
        /// <param name="defaultBindPort">
        /// The TCP port to bind VirtualHosts to by default.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided default IP
        ///     address is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided default bind port is
        /// zero.
        /// </exception>
        public HostRouter(
            IPAddress defaultBindAddress,
            ushort defaultBindPort = 80
            )
        {
            if (defaultBindAddress == null)
            {
                throw new ArgumentNullException(
                    "The default IP address must not be null."
                    );
            }

            if (defaultBindPort == 0)
            {
                throw new ArgumentException(
                    "The default TCP port cannot be zero."
                    );
            }

            _hosts = new List<VirtualHost>();
            _defaultIp = defaultBindAddress;
            _defaultPort = defaultBindPort;
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
        /// The TCP port that virtual hosts will be bound to by
        /// default. This is used when no port is specified whilst
        /// adding virtual hosts to the router.
        /// </summary>
        public ushort DefaultBindPort
        {
            get { return _defaultPort; }
            set
            {
                if (value == 0)
                {
                    throw new ArgumentException(
                        "The port to bind to cannot be port zero."
                        );
                }

                _defaultPort = value;
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
        /// Adds a virtual host to the set of hosts, using the
        /// default IP address and the default TCP port.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <returns>The VirtualHost that was added to the HostRouter.</returns>
        public VirtualHost AddHost(string format)
        {
            return this.AddHost(
                format: format,
                port:   this.DefaultBindPort
                );
        }
        /// <summary>
        /// Adds a virtual host to the set of hosts, using
        /// the default IP address and the specified port.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <param name="port">The port to bind the virtual host to.</param>
        /// <returns>The VirtualHost that was added to the HostRouter.</returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the specified port is outside the allowable range.
        /// </exception>
        public VirtualHost AddHost(string format, ushort port)
        {
            var vhost = new VirtualHost(
                format:         format,
                bindAddress:    this.DefaultBindAddress,
                port:           port,
                routes:         new Router()
                );

            return this.AddHost(vhost);
        }
        /// <summary>
        /// Adds a virtual host to the set of hosts.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <param name="bindAddress">The IP address to bind the virtual host to.</param>
        /// <param name="port">The port to bind the virtual host to.</param>
        /// <returns>The VirtualHost that was added to the HostRouter.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the specified IPAddress is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the specified port is outside the allowable range.
        /// </exception>
        public VirtualHost AddHost(string format, IPAddress bindAddress, ushort port)
        {
            if (bindAddress == null)
            {
                throw new ArgumentNullException(
                    "The specified address to bind to must not be null."
                    );
            }

            return this.AddHost(
                format:         format,
                bindAddress:    bindAddress,
                port:           port,
                routes:         new Router()
                );
        }
        /// <summary>
        /// Adds a virtual host to the set of hosts,
        /// using the default IP address, the specified
        /// port, and the provided set of routes.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <param name="port">The port to bind the virtual host to.</param>
        /// <param name="routes">The set of routes associated with the virtual host.</param>
        /// <returns>The VirtualHost that was added to the HostRouter.</returns>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the Router passed to the method is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the specified port is outside the allowable range.
        /// </exception>
        public VirtualHost AddHost(string format, ushort port, Router routes)
        {
            var vhost = new VirtualHost(
                format:         format,
                bindAddress:    this.DefaultBindAddress,
                port:           port,
                routes:         routes
                );

            return this.AddHost(vhost);
        }
        /// <summary>
        /// Adds a virtual host to the set of hosts.
        /// </summary>
        /// <param name="format">The hostname of the virtual host.</param>
        /// <param name="bindAddress">The IP address to bind the virtual host to.</param>
        /// <param name="port">The port to bind the virtual host to.</param>
        /// <param name="routes">The set of routes associated with the virtual host.</param>
        /// <returns>The VirtualHost that was added to the HostRouter.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the IPAddress or Router passed to
        /// the method is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the specified port is outside the allowable range.
        /// </exception>
        public VirtualHost AddHost(
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

            return this.AddHost(vhost);
        }
        /// <summary>
        /// Adds a virtual host to the set of hosts.
        /// </summary>
        /// <param name="host">The virtual host to add.</param>
        /// <returns>The VirtualHost that was added to the HostRouter.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the VirtualHost provided to the method is null.
        /// </exception>
        public VirtualHost AddHost(VirtualHost host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(
                    "The provided VirtualHost cannot be null."
                    );
            }

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

            return host;
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
