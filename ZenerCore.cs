﻿/*
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
            TLS = new ThreadLocal<StrObjDict>(() => new StrObjDict());
        }

        private List<HttpServer> _httpServers;
        private ZenerContext _context;
        private object _lockbox;

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
                    _httpServers.Add(sv);
                    // Bind the message handler to this HttpServer's
                    // EmitMessage event.
                    sv.MessageEmitted += this.HttpServerMessageHandler;
                    // Start the server.
                    sv.Start();
                }
            }

            // Add any API methods to the host's router.
            _context.AddApiRoutes(host.Routes);
        }
        private void HttpServerMessageHandler(HttpServerMessage msg)
        {
            switch (msg.Type)
            {
                case MessageType.RequestReceived:
                {
                    HttpRequest req = (HttpRequest)msg.Arguments[0];
                    HttpResponse res = (HttpResponse)msg.Arguments[1];

                    /* Due to HTTP pipelining, multiple requests can be handled by
                     * a single thread. As a result, if the TLS_VHOST key is not
                     * cleared and a pipelined request fails, the virtual host for
                     * the previously-processed request will be used to look up
                     * error handlers. This behaviour is incorrect.
                     * 
                     * Avoiding this is a fairly simple fix, we just need to remove
                     * the TLS_VHOST key. The call to 'Remove' won't throw an exception
                     * if the key doesn't exist, so we don't need to check for it.
                     * 
                     * This correction is anticipatory, as the described issue is
                     * difficult to replicate.
                     */
                    TLS.Value.Remove(TLS_VHOST);

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
                            message:    "The host the client requested does not " +
                                        "exist on this server."
                            );
                    }

                    // Set the fields we'll use to as easier-to-remember
                    // names.
                    host = vh.Item1;
                    hostParams = vh.Item2;
                    // Add the virtual host to our thread-local storage.
                    TLS.Value[TLS_VHOST] = host;

                    /* We now need to find, in the virtual host, the
                     * correct route.
                     */

                    // As with before, we want to be able to provide two
                    // error messages: 405 for when the method isn't acceptable,
                    // and 404 for when there are absolutely no routes.
                    var rt = host.Routes.Find(req.Path);

                    // There are absolutely no results, so we return 404.
                    if (rt.Count == 0)
                    {
                        throw new HttpException(
                            status:     HttpStatus.NotFound
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
                            status:     HttpStatus.MethodNotAllowed
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
                        var xdict = new ExpandoObject() as IDictionary<string, object>;

                        foreach (var kvp in hostParamsDict)
                            xdict[kvp.Key] = kvp.Value;

                        foreach (var kvp in rtParamsDict)
                            xdict[kvp.Key] = kvp.Value;

                        hostParams = (ExpandoObject)xdict;
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

                    // Before we use the default error handler, we want
                    // to try to find a handler defined by the user. To
                    // do this, we need to check our thread-local storage
                    // to see if a VirtualHost has been put in it.
                    VirtualHost host;
                    object objHost;
                    if (!TLS.Value.TryGetValue(TLS_VHOST, out objHost))
                    {
                        host = null;
                    }
                    else host = objHost as VirtualHost;

                    RouteHandler handler;
                    // If we got a result, we can try looking up an error
                    // handler.
                    if (host != null)
                    {
                        var route = host.Routes
                            // Error handling routes are prefixed with the
                            // internal prefix (by default, a colon) and use
                            // the status code of the error (for example, a
                            // 404 Not Found handler would use the format ":404").
                            .Find(String.Format(
                                "{0}{1}",
                                INTERNAL_PREFIX, exc.StatusCode.GetCode()
                                ))
                            // The call to Find will also match any formats that
                            // are comprised solely of a variable (for example,
                            // the format "/[file]"), so we need to make sure
                            // the format starts with the internal prefix.
                            .Where(rt => rt.Item1.Format.StartsWith(INTERNAL_PREFIX))
                            .DefaultIfEmpty(null)
                            .First();

                        if (route == null)
                        {
                            // There's no match, so we need to use the default
                            // one.
                            handler = HttpServer.DefaultErrorHandler;
                        }
                        else
                        {
                            // There is a match, so we use its handler.
                            handler = route.Item1.Handler;
                        }
                    }
                    else
                    {
                        // We can't get a user-defined handler, so we
                        // need to use the default one.
                        handler = HttpServer.DefaultErrorHandler;
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
            // Prevent modifications to the ZenerContext.
            context.Lock();

            this.Hosts = new HostRouter(
                context.DefaultIpAddress, context.DefaultTcpPort
                );
            this.Hosts.HostAdded += this.VirtualHostAddedHandler;
            _httpServers = new List<HttpServer>();
            _context = context;
            _lockbox = new object();

            if (context.IncludeDefaultHost)
            {
                this.Hosts.AddHost(
                    format:         "*", // the wildcard format string
                    port:           context.DefaultTcpPort
                    );
            }
        }

        /// <summary>
        /// The default wildcard virtual host, if one has been created.
        /// </summary>
        public VirtualHost DefaultHost
        {
            get
            {
                return this.Hosts
                    .Where(v => v.IsWildcard() && v.Port == _context.DefaultTcpPort)
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
