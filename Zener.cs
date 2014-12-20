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
    /// Used to indicate when a dynamic property has no value.
    /// </summary>
    public enum Empty { }

    /// <summary>
    /// A class implementing the Zener interface between web server and application.
    /// </summary>
    public class Zener
    {
        private const string INTERNAL_PREFIX = ":";
        private static Version _ver;

        private HttpServer _http;

        /// <summary>
        /// The current version of Zener.
        /// </summary>
        public static Version Version
        {
            get { return _ver; }
        }

        private void HandleHttpRequestSuccessful(HttpRequest req, HttpResponse res)
        {
            HttpRequestHandler hrh;
            bool found = this.Routes.TryFind(req.Path, out hrh);

            if (!found) throw new HttpException(HttpStatus.NotFound);

            hrh(req, res);
        }
        private void HandleHttpRequestError(HttpException exception, HttpResponse res)
        {
            HttpRequestHandler hrh;
            bool found = this.Routes.TryFind(
                String.Format("{0}{1}", INTERNAL_PREFIX, (int)exception.StatusCode),
                out hrh
                );

            if (!found) HttpServer.DefaultErrorHandler(exception, res);
            else
            {
                hrh(null, res);
            }
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
            : this(new Random().Next(49152, 65534))
        { }
        /// <summary>
        /// Creates a new ZenerCore with documents sourced from the specified webroot,
        /// and with the server listening on the specified port.
        /// </summary>
        /// <param name="webroot">An accessible directory to be used as the webroot.</param>
        /// <param name="port">A TCP port to use for the web server.</param>
        /// <param name="precache">Whether all files in the webroot and children should be pre-cached.</param>
        /// <exception cref="System.ArgumentException"></exception>
        public Zener(int port)
        {
            _http = new HttpServer(port)
            {
                RequestHandler = HandleHttpRequestSuccessful,
                ErrorHandler = HandleHttpRequestError
            };
            this.Routes = new Router();
        }

        /// <summary>
        /// The TCP port the server is listening on.
        /// </summary>
        public int Port
        {
            get { return _http.Port; }
        }
        /// <summary>
        /// The routes that will be used to call handlers and
        /// serve content to the user agent.
        /// </summary>
        public Router Routes
        {
            get;
            set;
        }
    }
}
