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
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Dynamic;

using McSherry.Zener.Net;
using McSherry.Zener.Core;

using StrObjDict = System.Collections.Generic.Dictionary<string, object>;

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
        private const string 
            INTERNAL_PREFIX     = ":",
            HDR_HOST            = "Host",

            URI_HTTP            = "http:",
            HTTP_VERSION_1_1    = "HTTP/1.1",

            // The names of items stored within
            // thread-local storage.
            TLS_VHOST           = "VirtualHost"
            ;
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
        private static readonly ThreadLocal<StrObjDict> TLS;

        static ZenerCore()
        {
            _ver = Assembly.GetCallingAssembly().GetName().Version;
            TLS = new ThreadLocal<StrObjDict>(() => new StrObjDict());
        }

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

                    /* To properly use virtual hosting, we need to make sure that
                     * the client is sending HTTP 'Host' headers. These headers are
                     * what we'll use to determine the virtual host to add.
                     */
                    var hostHdr = req.Headers[HDR_HOST].FirstOrDefault();
                    VirtualHost host;
                    dynamic hostParams;
                    // If hostHdr is the default value, the client hasn't sent a
                    // 'Host' header.
                    if (hostHdr == default(HttpHeader))
                    {
                        throw new HttpRequestException(
                            request:    req,
                            message:    "The client did not provide a \"Host\" " +
                                        "header with its request."
                            );
                    }

                    // If there is a 'Host' header, we need to make sure that it is
                    // valid. If the header isn't valid, we need to return a Bad
                    // Request response.
                    Tuple<string, ushort> hostHdrInfo;
                    try
                    {
                        hostHdrInfo = Rfc2616.ParseHostHeader(hostHdr.Value);
                    }
                    catch (ArgumentException aex)
                    {
                        throw new HttpRequestException(
                            request:        req,
                            innerException: aex,
                            message:        "The client send a malformed " +
                                            "\"Host\" header."
                            );
                    }

                    // We need to check whether there's a match within our
                    // host router.
                    var vh = this.Hosts.Find(hostHdrInfo.Item1, hostHdrInfo.Item2);
                    // If there's no match, the value will be the default.
                    if (vh == default(Tuple<VirtualHost, dynamic>))
                    {
                        // If we don't recognise the host, we have to respond
                        // with Bad Request.
                        throw new HttpRequestException(
                            request:    req,
                            message:    "The host the client requested does not " +
                                        "exist on this server."
                            );
                    }

                    // Set the fields we'll use to as easier-to-remember
                    // names.
                    host = vh.Item1;
                    hostParams = vh.Item2;
                    // Add the virtual host to our thread-local storage.
                    TLS.Value.Add(TLS_VHOST, host);

                    /* We now need to find, in the virtual host, the
                     * correct route.
                     */

                    // As with before, we want to be able to provide two
                    // error messages: 405 for when the method isn't acceptable,
                    // and 404 for when there are absolutely no routes.
                    var rt = host.Router.Find(req.Path);

                    // There are absolutely no results, so we return 404.
                    if (rt.Count == 0)
                    {
                        throw new HttpException(
                            status:     HttpStatus.NotFound,
                            request:    req
                            );
                    }

                    // Okay, so we've got at least one possible match. We now
                    // need to determine whether the match accepts the current
                    // request method.
                    rt = rt
                        .Where(t => t.Item1.MethodIsAcceptable(req.Method))
                        .ToList();

                    // None of the matches accept the current request method, so
                    // we need to report to the client that the method is not
                    // acceptable.
                    if (rt.Count == 0)
                    {
                        throw new HttpException(
                            status:     HttpStatus.MethodNotAllowed,
                            request:    req
                            );
                    }

                    // There's a match!
                    var match = rt.First();
                    // Both the virtual host and the route can have parameters.
                    // For simplicity, we're going to merge the two ExpandoObjects.
                    // Before we can do this, we need to make sure that both actually
                    // have parameters.
                    var hostParamsDict = hostParams as IDictionary<string, object>;
                    var rtParamsDict = match.Item2 as IDictionary<string, object>;
                    // If both have parameters, we can merge.
                    if (hostParamsDict != null && rtParamsDict != null)
                    {
                        hostParams = (ExpandoObject)hostParamsDict.Concat(rtParamsDict);
                    }
                    // If we get here, one of the sets of parameters is empty. We'll
                    // be using the hostParams variable to store our final dictionary,
                    // so it makes sense to check the route parameters variable.
                    else if (rtParamsDict != null)
                    {
                        // hostParams is empty, route parameters is not.
                        // Set hostParams to route parameters.
                        hostParams = match.Item2;
                    }

                    // Call the handler with all the parameters.
                    match.Item1.Handler(req, res, hostParams);
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
