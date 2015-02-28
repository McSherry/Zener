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
using System.Net;
using System.Net.Sockets;
using System.IO;

using McSherry.Zener.Net.Serialisation;

namespace McSherry.Zener.Net
{
    /// <summary>
    /// The exception thrown when there is an error related to
    /// the HTTP server or client.
    /// </summary>
    public class HttpException : Exception
    {
        /// <summary>
        /// Creates a new HttpException.
        /// </summary>
        /// <param name="status">The HTTP status code representing the error.</param>
        public HttpException(HttpStatus status)
            : base()
        {
            this.StatusCode = status;
        }
        /// <summary>
        /// Creates a new HttpException.
        /// </summary>
        /// <param name="status">The HTTP status code representing the error.</param>
        /// <param name="message">The message to send with the exception.</param>
        public HttpException(HttpStatus status, string message)
            : base(message)
        {
            this.StatusCode = status;
        }
        /// <summary>
        /// Creates a new HttpException.
        /// </summary>
        /// <param name="status">The HTTP status code representing the error.</param>
        /// <param name="message">The message to send with the exception.</param>
        /// <param name="innerException">The exception that caused this exception to be raised.</param>
        public HttpException(
            HttpStatus status,
            string message, Exception innerException
            )
            : base(message, innerException)
        {
            this.StatusCode = status;
        }

        /// <summary>
        /// The status code associated with this exception.
        /// </summary>
        public HttpStatus StatusCode
        {
            get;
            protected set;
        }
    }
    /// <summary>
    /// The exception thrown when the client's request does not
    /// include a Content-Length header.
    /// </summary>
    public sealed class HttpLengthRequiredException : HttpException
    {
        /// <summary>
        /// Creates a new HttpLengthrequiredException.
        /// </summary>
        public HttpLengthRequiredException()
            : base(HttpStatus.LengthRequired)
        {

        }
        /// <summary>
        /// Creates a new HttpLengthrequiredException.
        /// </summary>
        /// <param name="message">The message to send with the exception.</param>
        public HttpLengthRequiredException(string message)
            : base(HttpStatus.LengthRequired, message)
        {

        }
        /// <summary>
        /// Creates a new HttpLengthrequiredException.
        /// </summary>
        /// <param name="message">The message to send with the exception.</param>
        /// <param name="innerException">The exception that is the cause of this exception.</param>
        public HttpLengthRequiredException(
            string message, Exception innerException
            )
            : base(HttpStatus.LengthRequired, message, innerException)
        {

        }
    }
    /// <summary>
    /// The exception thrown when a request handler wishes to terminate
    /// the connection to the client.
    /// </summary>
    /// <remarks>
    /// This class is handled specially by HttpServer, and throwing it
    /// will terminate the connection to the client without sending a
    /// response.
    /// </remarks>
    public sealed class HttpFatalException : HttpException
    {
        /// <summary>
        /// Creates a new HttpFatalException.
        /// </summary>
        public HttpFatalException()
            : base((HttpStatus)(-1))
        {

        }
    }

    /// <summary>
    /// The delegate used for handling messages from the HTTP server.
    /// </summary>
    /// <param name="message">The message the server sent.</param>
    public delegate void HttpServerMessageHandler(HttpServerMessage message);
    /// <summary>
    /// The delegate used for responding to HTTP requests.
    /// </summary>
    /// <param name="request">The details of the HTTP request.</param>
    /// <param name="response">The response to be sent to the client.</param>
    public delegate void HttpRequestHandler(HttpRequest request, HttpResponse response);

    /// <summary>
    /// A class implementing a basic HTTP server.
    /// </summary>
    public class HttpServer
    {
        private const int TCP_EPHEMERAL_MIN = 49152;
        private const int TCP_EPHEMERAL_MAX = 65535;
        private static Random _rng;

        /// <summary>
        /// The error handler called when no other handler exists.
        /// </summary>
        internal static void DefaultErrorHandler(
            HttpRequest req, HttpResponse res, dynamic param
            )
        {
            var exception = (HttpException)param.Exception;

            res.BufferOutput = true;
            res.StatusCode = exception.StatusCode;
            res.Headers.Add("Content-Type", "text/plain");

            res.Write(
                "{0} {1}\n\n",
                exception.StatusCode.GetCode(),
                exception.StatusCode.GetMessage()
                );

            res.WriteLine(exception);
        }

        static HttpServer()
        {
            _rng = new Random();
        }

        private TcpListener _listener;
        private Thread _listenThread;
        private IPAddress _ip;
        private int _port;
        private volatile bool _acceptConnections = true;

        private void TcpAcceptor()
        {
            while (_acceptConnections)
            {
                try
                {
                    var tcl = _listener.AcceptTcpClient();
                    /* TODO: Compare performance of ThreadPool against the
                     * performance of creating new Thread instances and any
                     * other relevant methods of multithreading.
                     */
                    ThreadPool.QueueUserWorkItem(HttpRequestHandler, tcl);
                }
                catch (SocketException sex)
                {
                    // This will throw an exception (WSACancelBlockingCall-related)
                    // when Stop is called. We want to catch this exception, but let
                    // any others propagate.
                    if (sex.SocketErrorCode != SocketError.Interrupted)
                        throw;
                }

            }
        }
        private void HttpRequestHandler(object tclo)
        {
            var tcl = (TcpClient)tclo;

            tcl.NoDelay = true;
            tcl.Client.NoDelay = true;

            NetworkStream ns = tcl.GetStream();

            HttpRequest req;
            HttpResponse res = new HttpResponse(ns);

            try
            {
                // Attempt to create a request object.
                req = HttpRequest.Create(ns);

                // If the client supports it, enable
                // HTTP compression. The developer will
                // also need to enable output buffering
                // to make use of HTTP compression.
                //res.EnableCompression(req);

                // If creation succeeds, emit a message
                // with the request and response objects
                // as its arguments.
                this.EmitMessage(
                    MessageType.RequestReceived,
                    new object[] { req, res }.ToList(),
                    res
                    );
            }
            catch (HttpFatalException)
            {
                // The throwing of an HttpFatalException indicates
                // that the request creator or handler wishes to
                // terminate the connection without a response. This
                // is generally done when the connection is not in a
                // recoverable state (e.g. completely malformed data).
                res.Close();
                ns.Close();
                ns.Dispose();
                tcl.Close();

                return;
            }
            catch (IOException ioex)
            {
                // If there's a problem with the socket, there's probably
                // nothing we can do from here. It's unlikely that we'll
                // be able to inform the client of an issue. We'll just
                // skip it and continue on.
                if (ioex.InnerException is SocketException)
                {
                    res.Close();
                    ns.Close();
                    ns.Dispose();
                    tcl.Close();

                    return;
                }
                else throw;
            }

            // We're finished with this request, so we can close it. This
            // will flush any buffers to the network, and with close/dispose
            // any disposable resources.
            res.Close();
            // We need to support HTTP pipelining for HTTP/1.1 compliance, and
            // this is how we're going to do it. Pipelining involves the user
            // agent sending multiple requests, one after the other, without
            // waiting for a response. This means that, if a user agent is using
            // pipelining, we'll still have data in our receive buffers after
            // processing the first request.
            if (ns.DataAvailable)
            {
                // The pipelined requests aren't specially formatted, it's just
                // one HTTP request after another. This means we don't need to
                // write any special code, and we can just call the method we're
                // currently in again.
                this.HttpRequestHandler(tclo);
            }
            else
            {
                // If there's no data remaining in our receive buffers, there
                // are no more requests to process. This means that we can close
                // the streams and connections.
                ns.Close();
                ns.Dispose();
                tcl.Close();
            }
        }
        private void EmitMessage(MessageType msgType, IList<object> args, HttpResponse res)
        {
            try
            {
                if (this.MessageEmitted != null)
                {
                    this.MessageEmitted(new HttpServerMessage(msgType, args));
                }
            }
            catch (HttpException hex)
            {                    
                // HttpFatalException is a special case, and we need to
                // catch it and rethrow it so this try-catch doesn't
                // swallow it.
                //
                // When we throw it, it will be caught in HttpRequestHandler,
                // and HttpRequestHandler will terminate the connection without
                // crashing the program.
                if (hex is HttpFatalException)
                    throw;

                /* Message handlers can throw HttpExceptions or types
                 * that inherit from HttpException to cause the HttpServer
                 * to generate an InvokeErrorHandler message.
                 * 
                 * Handler writers beware, if your handler for InvokeErrorHandler
                 * throws an HttpException, you may end up with an infinite
                 * loop and stack overflow.
                 */
                this.EmitMessage(
                    MessageType.InvokeErrorHandler,
                    new object[] { hex, res }.ToList(),
                    res
                    );
            }
            catch (SocketException sex)
            {
                // As much as I hate just swallowing exceptions, these ones
                // are extremely common (relative to the other SocketExceptions),
                // and, while there is no real way to recover, the connection
                // being reset shouldn't crash the web server.
                if (
                    sex.SocketErrorCode == SocketError.ConnectionAborted ||
                    sex.SocketErrorCode == SocketError.ConnectionReset
                    )
                {
                    return;
                }

                throw;
            }
        }

        /// <summary>
        /// Creates a new instance of the HTTP server, listening on the IPv4 loopback
        /// and a random TCP ephemeral port.
        /// </summary>
        public HttpServer()
            : this(IPAddress.Loopback, (ushort)_rng.Next(TCP_EPHEMERAL_MIN, TCP_EPHEMERAL_MAX))
        {

        }
        /// <summary>
        /// Creates a new instance of the HTTP server, listening on the specified
        /// port and the IPv4 loopback.
        /// </summary>
        /// <param name="port">The TCP port to listen on.</param>
        public HttpServer(ushort port)
            : this(IPAddress.Loopback, port)
        {

        }
        /// <summary>
        /// Creates an instance of the HTTP server, listening on the specified
        /// address and port.
        /// </summary>
        /// <param name="address">The IP address to listen on.</param>
        /// <param name="port">The TCP port to listen on.</param>
        public HttpServer(IPAddress address, ushort port)
        {
            _ip = address;
            _port = port;
            // Recommendation: Go directly to 127.0.0.1 and avoid the use of
            // 'localhost'. Windows seems to have a rather slow DNS resolver,
            // and using 'localhost' seems to add 300ms or so to response times.
            _listener = new TcpListener(address, this.Port);
            _listenThread = new Thread(TcpAcceptor);
        }

        /// <summary>
        /// The IP address thae the server is listening on.
        /// </summary>
        public IPAddress IpAddress
        {
            get { return _ip; }
        }
        /// <summary>
        /// The TCP port the server is listening on.
        /// </summary>
        public int Port
        {
            get { return _port; }
        }

        /// <summary>
        /// Starts the HTTP server and listener.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the address/port combination specified is
        ///     already in use, and so cannot be bound to.
        /// </exception>
        /// <exception cref="System.Net.Sockets.SocketException">
        ///     Thrown when an error occurs with the HttpServer's
        ///     internal TcpListener.
        /// </exception>
        public void Start()
        {
            try
            {
                _listener.Start();
            }
            catch (SocketException sex)
            {
                if (sex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    throw new InvalidOperationException(
                        "The specified address/port combination is in use.",
                        sex
                        );
                }

                throw;
            }
            _listenThread.Start();
        }
        /// <summary>
        /// Stops the HTTP server and listener.
        /// </summary>
        public void Stop()
        {
            _acceptConnections = false;

            try
            {
                _listener.Stop();
            }
            catch (SocketException) { }
        }

        /// <summary>
        /// Fired each time the HttpServer emits a message.
        /// </summary>
        public event HttpServerMessageHandler MessageEmitted;
    }
}
