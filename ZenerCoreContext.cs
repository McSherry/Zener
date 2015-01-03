/*
 *      Copyright (c) 2014-2015, SynapLink, LLC
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE.txt, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IPAddress = System.Net.IPAddress;

namespace SynapLink.Zener
{
    /// <summary>
    /// Used to pass state information to a ZenerCore.
    /// </summary>
    public class ZenerCoreContext
    {
        private const int
            API_QUANTITY        = 3,

            API_FILESYSTEM      = 0x00,
            API_METHODCALL      = 0x01,
            API_EVENTS          = 0x02
            ;
        private bool[] _activeApis;

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
        public ZenerCoreContext()
            : this(IPAddress.Loopback)
        {

        }
        /// <summary>
        /// Creates a new ZenerCoreContext.
        /// </summary>
        /// <param name="address">The IP address for the ZenerCore to bind to.</param>
        /// <param name="port">The TCP port for the ZenerCore to bind to.</param>
        public ZenerCoreContext(IPAddress address, ushort port = 80)
        {
            this.IpAddress = address;
            this.TcpPort = port;

            _activeApis = new bool[API_QUANTITY];
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
    }
}
