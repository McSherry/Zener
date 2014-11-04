using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SynapLink.Zener.Net;
using SynapLink.Zener.Core;

namespace SynapLink.Zener
{
    /// <summary>
    /// A class implementing the Zener interface between web server and application.
    /// </summary>
    public class ZenerCore
    {
        private const string WEBROOT_DEFAULT = "./www";

        private int _port;
        private string _webroot;
        private FileSystem _filesystem;
        private HttpServer _http;

        /// <summary>
        /// Initialises the ZenerCore with the server on a random port, and attempts
        /// to source files from the default webroot directory. All files found are
        /// automatically cached in to memory.
        /// </summary>
        /// <exception cref="System.ArgumentException"></exception>
        public ZenerCore()
            : this(WEBROOT_DEFAULT, new Random().Next(1025, 65000), true)
        { }
        /// <summary>
        /// Creates a new ZenerCore with documents sourced from the specified webroot,
        /// and with the server listening on the specified port.
        /// </summary>
        /// <param name="webroot">An accessible directory to be used as the webroot.</param>
        /// <param name="port">A TCP port to use for the web server.</param>
        /// <param name="precache">Whether all files in the webroot and children should be pre-cached.</param>
        /// <exception cref="System.ArgumentException"></exception>
        public ZenerCore(string webroot, int port, bool precache)
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
