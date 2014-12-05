/*
 *      Copyright (c) 2014, SynapLink, LLC
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
using System.Reflection;

using SynapLink.Zener.Net;
using SynapLink.Zener.Core;

namespace SynapLink.Zener
{
    /// <summary>
    /// A class implementing the Zener interface between web server and application.
    /// </summary>
    public class Zener
    {
        private const string WEBROOT_DEFAULT = "./www";
        private static Version _ver;

        private string _webroot;
        private FileSystem _filesystem;
        private HttpServer _http;

        /// <summary>
        /// The current version of Zener.
        /// </summary>
        public static Version Version
        {
            get { return _ver; }
        }

        static Zener()
        {
            _ver = Assembly.GetCallingAssembly().GetName().Version;
        }

        /// <summary>
        /// Initialises the ZenerCore with the server on a random port, and attempts
        /// to source files from the default webroot directory. All files found are
        /// automatically cached in to memory.
        /// </summary>
        /// <exception cref="System.ArgumentException"></exception>
        public Zener()
            : this(WEBROOT_DEFAULT, new Random().Next(49152, 65534))
        { }
        /// <summary>
        /// Creates a new ZenerCore with documents sourced from the specified webroot,
        /// and with the server listening on the specified port.
        /// </summary>
        /// <param name="webroot">An accessible directory to be used as the webroot.</param>
        /// <param name="port">A TCP port to use for the web server.</param>
        /// <param name="precache">Whether all files in the webroot and children should be pre-cached.</param>
        /// <exception cref="System.ArgumentException"></exception>
        public Zener(string webroot, int port)
        {
            _webroot = webroot;

            _http = new HttpServer(port);
            _filesystem = new FileSystem(this.WebRoot);
        }

        /// <summary>
        /// The directory the server will attempt to source files from.
        /// </summary>
        public string WebRoot 
        {
            get { return _webroot; }
        }
        /// <summary>
        /// The TCP port the server is listening on.
        /// </summary>
        public int Port
        {
            get { return _http.Port; }
        }
    }
}
