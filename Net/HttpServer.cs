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
            ) : base(message, innerException)
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
        /// <summary>
        /// Creates a new HttpFatalException.
        /// </summary>
        /// <param name="message">The message to send with the exception.</param>
        public HttpFatalException(string message)
            : base((HttpStatus)(-1), message)
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
    public sealed class HttpServer
    {
        /// <summary>
        /// The port with the lowest number that can be considered
        /// an ephemeral TCP port.
        /// </summary>
        private const int TCP_EPHEMERAL_MIN = 49152;
        /// <summary>
        /// The port with the greatest number that can be considered
        /// an ephemeral TCP port.
        /// </summary>
        private const int TCP_EPHEMERAL_MAX = 65535;
        /// <summary>
        /// The number of milliseconds the server will wait on keep-alive
        /// requests to send more data.
        /// </summary>
        private const int HTTP_KEEPALIVE_TIMEOUT = 1500;
        /// <summary>
        /// The random number generator used to generate random ports.
        /// </summary>
        private static Random _rng;

        /// <summary>
        /// The error handler called when no other handler exists.
        /// </summary>
        internal static void DefaultErrorHandler(
            HttpRequest req, HttpResponse res, dynamic param
            )
        {
            var exception = (HttpException)param.Exception;

            res.Serialiser.BufferOutput = true;
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
            HttpDeserialiser httpDes;
            HttpResponse res;
            HttpSerialiser httpSer = null;
            try
            {
                // We assume that the client will be speaking HTTP/1.x
                // by default, and set our deserialiser variable appropriately.
                httpDes = new Http1Deserialiser(ns);

                /* There are a few reasons we need to loop when processing HTTP
                 * requests at this level.
                 *
                 *      1.  If HTTP requests are pipelined, we're going to need
                 *          (or want) to reuse the deserialiser.
                 *       
                 *      2.  If we need to switch protocols (say, the client
                 *          requested a change to HTTP/2), we're going to need
                 *          to create a new deserialiser and serialiser, assign
                 *          them to our variables, and then loop to process
                 *          further requests.
                 *          
                 *          It's quite possible that we'll need to make further
                 *          modifications if implementing HTTP/2, as these are
                 *          being made before HTTP/2 has been finalised (still
                 *          being edited before being published in an RFC).
                **/
                do
                {
                    // Attempt to create a request object. We don't want to
                    // retrieve a request cached by the deserialiser, so we
                    // pass False to the method.
                    req = httpDes.Deserialise(returnPrevious: false);
                    // The request created successfully, so we can now go ahead
                    // with creating our response. The first thing to do is create
                    // the empty HttpResponse object.
                    res = new HttpResponse();
                    // We then, using information taken from the request, create
                    // a response serialiser that is appropriate for the client's
                    // request version.
                    httpSer = HttpSerialiser.Create(
                        // The method uses the HTTP version the client specifies
                        // to determine which serialiser to use.
                        httpVersion: req.HttpVersion,
                        // By passing a HttpRequest to the Create method, we then
                        // don't have to call Configure on the serialiser we are
                        // returned, as the Create method will do it for us.
                        request: req,
                        response: res,
                        output: ns
                        );

                    // If creation succeeds, emit a message
                    // with the request and response objects
                    // as its arguments.
                    this.EmitMessage(
                        MessageType.RequestReceived,
                        new object[] { req, res },
                        res
                        );

                    // If we're at this point, deserialisation and serialisation
                    // appear to have gone well, so what we need to do now is close
                    // the serialiser (ensuring it flushes if it has not already).
                    httpSer.Close(flush: true);
                    // We then release any resources that the serialiser held.
                    httpSer.Dispose();

                    // To support HTTP pipelining, we need to check whether the
                    // client wants us to keep the connection alive. If it does,
                    // it's probably going to send us additional requests on the
                    // connection.
                    if (httpSer.Connection == HttpConnection.KeepAlive)
                    {
                        // While we may need to wait for the additional requests,
                        // we can't wait forever. We're going to use a time-out on
                        // keep-alive requests to make sure that the thread doesn't
                        // keep idling on nothing.
                        DateTime start = DateTime.UtcNow;
                        // We'll set this to true when we want to attempt to
                        // deserialise another request.
                        bool attemptDeserialise = false;
                        // We'll keep iterating whilst the time-out has not expired.
                        while (
                            (DateTime.UtcNow - start).TotalMilliseconds <= HTTP_KEEPALIVE_TIMEOUT
                            )
                        {
                            // We'll use the NetworkStream's DataAvailable property to determine
                            // whether there's a request that we need to process.
                            if (attemptDeserialise = ns.DataAvailable)
                            {
                                // If there's data to attempt to process, we want to break
                                // out of the loop.
                                break;
                            }
                            else
                            {
                                // This gives up our current slice of execution time.
                                // We're doing this to attempt to prevent maxing out whichever
                                // CPU core this loop is executing on. Plus, it will allow
                                // other operating system threads (potentially other Zener
                                // threads) to run.
                                //
                                // This isn't a perfect solution, as if no other threads
                                // are waiting we'll retain our execution time slice and
                                // will probably cause a CPU spike.
                                Thread.Yield();
                            }
                        }

                        // This field will tell us whether our keep-alive wait-loop
                        // determined whether we should attempt to process data or
                        // not.
                        if (attemptDeserialise)
                        {
                            // If we are to process data, skip to the next iteration
                            // in the while loop.
                            continue;
                        }
                    }

                    // If we end up here, we don't have anything more to process. Either
                    // we're closing the connection because the serialiser was configured
                    // to do this, or we waited for additional data and the client didn't
                    // send us any within a short enough time.
                    //
                    // Since there's nothing more to do, break out of the loop.
                    break;
                }
                while (true);
            }
            catch (HttpFatalException)
            {
                // The throwing of an HttpFatalException indicates
                // that the request creator or handler wishes to
                // terminate the connection without a response. This
                // is generally done when the connection is not in a
                // recoverable state (e.g. completely malformed data).
                //
                // As the "finally" block will get executed anyway, we
                // have already done everything we need to as we've
                // prevented the HttpFatalException from propagating
                // and crashing the program.
            }
            catch (HttpException hex)
            {
                // If we're here, something has thrown a HttpException
                // before we could locate an appropriate virtual host to
                // grab the error handler from. This will probably be a
                // malformed request or similar.

                // We're going to send a response using our default error
                // handler, so we need to create all the same bumf we would
                // have done with a successful request.
                res = new HttpResponse();
                // The problem here is that we don't know what protocol the
                // client speaks, and we've got no way to determine because
                // we've got no HttpRequest object to work from.
                //
                // HTTP/1.1 is currently the most popular version of the
                // protocol, so we'll take a safe guess and use a serialiser
                // for HTTP/1.1.
                httpSer = new Rfc7230Serialiser(res, ns);
                // We're handing off to any subscribers to the message pump
                // to handle this. We have our own default error handler,
                // but it's possible that whatever subscribes to the message
                // pump does too, and it would probably prefer to use its
                // own over ours.
                this.EmitMessage(
                    MessageType.InvokeErrorHandler,
                    new object[] { hex, res },
                    res
                    );

                // Make sure that all data is sent to the network and that
                // any resources the serialiser used are disposed of before
                // we exit.
                httpSer.Close(flush: true);
                httpSer.Dispose();
            }
            catch (IOException ioex)
            {
                // If there's a problem with the socket, there's probably
                // nothing we can do from here. It's unlikely that we'll
                // be able to inform the client of an issue. We'll just
                // skip it and continue on.
                if (ioex.InnerException is SocketException)
                {
                    // We don't need to do anything, as we'll fall through
                    // to the "finally" block which will handle closing and
                    // disposing for us.
                }
                else throw;
            }
            finally
            {
                // We've nothing left to process, so we can close/dispose of any network
                // resources we were using. We won't be using them. This makes sure that,
                // regardless of how we got here (successfully or via an exception), that
                // everything is closed and disposed.
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
                    new object[] { hex, res },
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
