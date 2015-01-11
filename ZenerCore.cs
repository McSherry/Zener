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
using System.Reflection;
using System.IO;

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
    public class ZenerCore
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

        static ZenerCore()
        {
            _ver = Assembly.GetCallingAssembly().GetName().Version;
        }

        /// <summary>
        /// Creates a new ZenerCore.
        /// </summary>
        /// <param name="context">The context to use when creating the ZenerCore.</param>
        public ZenerCore(ZenerContext context)
        {
            this.Routes = new Router();
            _http = new HttpServer(context.IpAddress, context.TcpPort)
            {
                RequestHandler = HandleHttpRequestSuccessful,
                ErrorHandler = HandleHttpRequestError
            };

            context.AddApiRoutes(this.Routes);

            _http.Start();
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

        /// <summary>
        /// Stops ZenerCore's underlying HTTP server.
        /// </summary>
        public void Stop()
        {
            _http.Stop();
        }
    }
}
