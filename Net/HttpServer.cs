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
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace SynapLink.Zener.Net
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
        public HttpException(HttpStatus status, string message, Exception innerException)
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
        public HttpLengthRequiredException(string message, Exception innerException)
            : base(HttpStatus.LengthRequired, message, innerException)
        {

        }
    }

    /// <summary>
    /// The signature that handlers of HTTP requests must fit.
    /// </summary>
    /// <param name="request">The received HTTP request.</param>
    /// <param name="response">The class representing the handler's response.</param>
    public delegate void HttpRequestHandler(HttpRequest request, HttpResponse response);
    public delegate void HttpErrorHandler(HttpException exception, HttpResponse response);

    /// <summary>
    /// A class implementing a basic HTTP server.
    /// </summary>
    public class HttpServer
    {
        private const string CRLF = "\r\n";
        private const int TCP_EPHEMERAL_MIN = 49152;
        private const int TCP_EPHEMERAL_MAX = 65535;
        private static Random _rng;

        /// <summary>
        /// The error handler called when no other handler exists.
        /// </summary>
        internal static void DefaultErrorHandler(HttpException exception, HttpResponse response)
        {
            response.Headers.Add("Content-Type", "text/plain");
            response.Write(
                "{0} {1}\n\n",
                (int)exception.StatusCode,
                exception.StatusCode.GetMessage()
                );

            response.WriteLine("{0}\n", exception.ToString());
        }

       static HttpServer()
        {
            _rng = new Random();
        }

        private TcpListener _listener;
        private Thread _listenThread;
        private int _port;
        private bool _acceptConnections = true;

        private void TcpAcceptor()
        {
            while (_acceptConnections)
            {
                var tcl = _listener.AcceptTcpClient();

                ThreadPool.QueueUserWorkItem(HttpRequestHandler, tcl);
            }
        }
        private void HttpRequestHandler(object tclo)
        {
            var tcl = (TcpClient)tclo;

            tcl.NoDelay = true;
            tcl.Client.NoDelay = true;

            NetworkStream ns = tcl.GetStream();

            HttpRequest req;
            HttpResponse res = new HttpResponse(ns, () => 
            {
                ns.Close();
                ns.Dispose();
                tcl.Close();
            });

            try
            {
                req = HttpRequest.Create(ns);

                if (this.RequestHandler != null)
                {
                    this.RequestHandler(req, res);
                }
            }
            catch (HttpException hex)
            {
                if (this.ErrorHandler == null)
                {
                    DefaultErrorHandler(hex, res);
                }
                else
                {
                    this.ErrorHandler(hex, res);
                }
            }
            catch (IOException ioex)
            {
                // If the connection's been reset, none of our handlers will
                // be much use, since they all write to the connection. The
                // best thing to do is to just return and stop handling it.
                //
                // I'm sure this is preferable to throwing an exception every
                // time the client resets the connection.
                if (ioex.InnerException is SocketException)
                {
                    if (((SocketException)ioex.InnerException).ErrorCode == (int)SocketError.ConnectionReset)
                    {
                        return;
                    }
                }
                
                throw;
            }
            
            res.Close();
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
            _port = port;
            // Recommendation: Go directly to 127.0.0.1 and avoid the use of
            // 'localhost'. Windows seems to have a rather slow DNS resolver,
            // and using 'localhost' seems to add 300ms or so to response times.
            _listener = new TcpListener(address, this.Port);
            _listenThread = new Thread(TcpAcceptor);
            ThreadPool.SetMinThreads(30, 30);
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
        public void Start()
        {
            _listener.Start();
            _listenThread.Start();
        }
        /// <summary>
        /// Stops the HTTP server and listener.
        /// </summary>
        public void Stop()
        {
            _acceptConnections = false;
            _listener.Stop();
        }

        /// <summary>
        /// The method called when the server receives a request.
        /// </summary>
        public HttpRequestHandler RequestHandler
        {
            get;
            set;
        }
        /// <summary>
        /// The method called when there is an error related to the
        /// server's HTTP functions (i.e. a request error). It is
        /// passed the HTTP status code of the error.
        /// </summary>
        public HttpErrorHandler ErrorHandler
        {
            get;
            set;
        }
    }
}
