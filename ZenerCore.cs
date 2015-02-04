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
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Dynamic;

using McSherry.Zener.Net;
using McSherry.Zener.Core;

namespace McSherry.Zener
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
        private static readonly Version _ver;

        private HttpServer _http;

        /// <summary>
        /// The current version of Zener.
        /// </summary>
        public static Version Version
        {
            get { return _ver; }
        }

        private void HttpServerMessageHandler(HttpServerMessage msg)
        {
            switch (msg.Type)
            {
                case MessageType.RequestReceived:
                {
                    HttpRequest req = (HttpRequest)msg.Arguments[0];
                    HttpResponse res = (HttpResponse)msg.Arguments[1];

                    /* We want to be able to provide two different error
                     * messages to aid debugging. If no route matches exist,
                     * we want to provide a 404 (Not Found). However, if
                     * matches exist but the request method is not acceptable,
                     * we want to provide 405 (Method Not Acceptable).
                     */
                    var routes = this.Routes.Find(req.Path);

                    // Absolutely no matches, 404.
                    if (routes.Count() == 0)
                    {
                        throw new HttpException(HttpStatus.NotFound, req);
                    }

                    routes = routes
                        .Where(t => t.Item1.MethodIsAcceptable(req.Method))
                        .ToList();

                    // No acceptable matches, 405.
                    if (routes.Count() == 0)
                    {
                        throw new HttpException(HttpStatus.MethodNotAllowed, req);
                    }

                    // We've got a match!
                    var route = routes.First();
                    route.Item1.Handler(req, res, route.Item2);
                } break;
                case MessageType.InvokeErrorHandler:
                {
                    var exc = (HttpException)msg.Arguments[0];
                    var res = (HttpResponse)msg.Arguments[1];

                    var route = this.Routes
                        .Find(
                            String.Format(
                                "{0}{1}",
                                INTERNAL_PREFIX, (int)exc.StatusCode
                        ))
                        .DefaultIfEmpty(null)
                        .First();

                    RouteHandler handler;
                    if (route == null)
                    {
                        handler = HttpServer.DefaultErrorHandler;
                    }
                    else
                    {
                        handler = route.Item1.Handler;
                    }

                    var rPr = new ExpandoObject() as IDictionary<string, object>;
                    rPr.Add("Exception", exc);

                    handler(null, res, rPr);
                } break;
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
            _http = new HttpServer(context.IpAddress, context.TcpPort);
            _http.MessageEmitted += this.HttpServerMessageHandler;

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
