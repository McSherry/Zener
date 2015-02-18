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

        // To support virtual hosting, and having individual error handlers
        // for individual virtual hosts, we're going to need to have somewhere
        // to store any type of data.
        //
        // For example, if a handler throws an HttpException, we will receive an
        // InvokeErrorHandler message. The handler for this message will be executed
        // on the same thread the exception was thrown on. By storing in this
        // Dictionary the VirtualHost that we retrieved routes from, we can use the
        // correct router to look up error-handling routes.
        //
        // Implementing it as a Dictionary rather than a ThreadStatic VirtualHost
        // field provides better forwards compatibility, as we can easily expand the
        // number of values we're storing.
        [ThreadStatic]
        private readonly Dictionary<string, object> TLS;

        private List<HttpServer> _httpServers;
        private ZenerContext _context;
        private object _lockbox;

        /// <summary>
        /// The current version of Zener.
        /// </summary>
        public static Version Version
        {
            get { return _ver; }
        }

        private void VirtualHostAddedHandler(object sender, VirtualHost host)
        {
            List<HttpServer> servers;
            lock (_lockbox)
            {
                // We want to check to see if there are any matching HttpServer
                // instances already created.
                servers = _httpServers
                    .Where(
                        sv => sv.IpAddress == host.BindAddress &&
                              sv.Port == host.Port
                        )
                    .ToList();
            }

            if (servers.Count == 0)
            {
                // There are no matching servers at this time, so we
                // need to add one.

                // Create a new HttpServer listening on the specified
                // IP address and port.
                var sv = new HttpServer(host.BindAddress, host.Port);
                // Add it to our set of servers.
                servers.Add(sv);
                // Bind the message handler to this HttpServer's
                // EmitMessage event.
                sv.MessageEmitted += this.HttpServerMessageHandler;
                // Start the server.
                sv.Start();
            }

            // Add any API methods to the host's router.
            _context.AddApiRoutes(host.Router);
            // Add the host to our host router.
            this.Hosts.AddHost(host);
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
                                INTERNAL_PREFIX, exc.StatusCode.GetCode()
                        ))
                        .Where(rt => rt.Item1.Format.StartsWith(INTERNAL_PREFIX))
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
            this.Hosts = new HostRouter(context.DefaultIpAddress);
            _httpServers = new List<HttpServer>();
            _context = context;
            _lockbox = new object();
        }

        /// <summary>
        /// The default wildcard virtual host, if one 
        /// </summary>
        public VirtualHost DefaultHost
        {
            get
            {
                return this.Hosts
                    .Where(v => v.IsWildcard() && v.Port == _context.TcpPort)
                    .FirstOrDefault();
            }
        }
        /// <summary>
        /// The virtual hosts that are associated with this ZenerCore.
        /// </summary>
        public HostRouter Hosts
        {
            get;
            private set;
        }

        /// <summary>
        /// Stops ZenerCore's underlying HTTP server(s).
        /// </summary>
        public void Stop()
        {
            _httpServers.ForEach(sv => sv.Stop());
        }
    }
}
