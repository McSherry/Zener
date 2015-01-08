/*
 *      Copyright (c) 2014-2015, SynapLink, LLC
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IPAddress = System.Net.IPAddress;
using Method = System.Func<dynamic, string>;

namespace SynapLink.Zener
{
    /// <summary>
    /// Used to pass state information to a ZenerCore.
    /// </summary>
    public class ZenerContext
    {
        private const int
            API_QUANTITY        = 1,

            API_FILESYSTEM      = 0x00
            ;
        private bool[] _activeApis = new bool[API_QUANTITY];

        /// <summary>
        /// All active APIs.
        /// </summary>
        internal IEnumerable<int> ActiveApis
        {
            get 
            {
                return Enumerable
                    .Range(0, API_QUANTITY)
                    .Where(i => _activeApis[i]);
            }
        }

        /// <summary>
        /// Creates a new ZenerCoreContext with the IP address
        /// set to the IPv4 loopback.
        /// </summary>
        public ZenerContext()
            : this(IPAddress.Loopback)
        {

        }
        /// <summary>
        /// Creates a new ZenerCoreContext.
        /// </summary>
        /// <param name="address">The IP address for the ZenerCore to bind to.</param>
        /// <param name="port">The TCP port for the ZenerCore to bind to.</param>
        /// <param name="useFilesystem">Whether to enable the file system API.</param>
        /// <param name="methods">The methods to make available via the method call API.</param>
        public ZenerContext(
            IPAddress address, 
            ushort port = 80,

            bool useFilesystem = false,
            Dictionary<string, Method> methods = null
            )
        {
            this.IpAddress = address;
            this.TcpPort = port;

            this.EnableFileSystemApi = useFilesystem;

            if (methods == null)
                this.Methods = new Dictionary<string, Method>(0, StringComparer.OrdinalIgnoreCase);
            else
                new Dictionary<string, Method>(methods, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The IP address for the ZenerCore to bind to.
        /// </summary>
        public IPAddress IpAddress
        {
            get;
            set;
        }
        /// <summary>
        /// The TCP port to bind this ZenerCore to.
        /// </summary>
        public ushort TcpPort
        {
            get;
            set;
        }

        /// <summary>
        /// Whether the file-system API should be enabled
        /// for the ZenerCore.
        /// </summary>
        public bool EnableFileSystemApi
        {
            get { return _activeApis[API_FILESYSTEM]; }
            set { _activeApis[API_FILESYSTEM] = value; }
        }
        /// <summary>
        /// The methods to make available via the method
        /// call API.
        /// </summary>
        public Dictionary<string, Method> Methods
        {
            get;
            private set;
        }
    }
}
