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
        public HttpException(HttpStatus status)
            : base()
        {
            this.StatusCode = status;
        }
        public HttpException(HttpStatus status, string message)
            : base(message)
        {
            this.StatusCode = status;
        }
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
    /// The exception thrown when there is an error with an HTTP
    /// request.
    /// </summary>
    public sealed class HttpRequestException : HttpException
    {
        public HttpRequestException()
            : base(HttpStatus.BadRequest)
        {

        }
        public HttpRequestException(string message)
            : base(HttpStatus.BadRequest, message)
        {

        }
        public HttpRequestException(string message, Exception innerException)
            : base(HttpStatus.BadRequest, message, innerException)
        {

        }
    }

    public delegate void HttpRequestHandler(HttpRequest request, HttpResponse response);
    public delegate void HttpErrorHandler(HttpException exception, HttpResponse response);

    /// <summary>
    /// A class implementing a basic HTTP server.
    /// </summary>
    public class HttpServer
    {
        private const string HTTP_MTD_HEAD = "HEAD";
        internal const string HTTP_VERSION = "1.1";

        /// <summary>
        /// The error handler called when no other handler exists.
        /// </summary>
        internal static void DefaultErrorHandler(HttpException exception, HttpResponse response)
        {
            response.Headers.Add("Content-Type", "text/plain");
            response.Write(
                "{0} {1}\n\n",
                (int)exception.StatusCode,
                HttpResponse.GetStatusMessage(exception.StatusCode)
                );

            response.WriteLine("{0}\n", exception.Message);
            response.WriteLine(exception.StackTrace);
        }

        private TcpListener _listener;
        private Thread _listenThread;
        private int _port;

        private void TcpAcceptor()
        {
            while (true)
            {
                var tcl = _listener.AcceptTcpClient();

                ThreadPool.QueueUserWorkItem(HttpRequestHandler, tcl);
            }
        }
        private void HttpRequestHandler(object tclo)
        {
            // Apparently, some browsers don't like servers responding
            // in a few milliseconds. This should stop any issues.
            // Tested against Chrome 38, Firefox 31, and IE 11. Chrome
            // was fine with a 1ms delay, Firefox and IE were okay with
            // 2ms. Set to 5ms for a bit of a safe zone.
            Thread.Sleep(5);

            var tcl = (TcpClient)tclo;
   
            tcl.NoDelay = true;
            tcl.Client.NoDelay = true;

            NetworkStream ns = tcl.GetStream();
            MemoryStream ms = new MemoryStream();
            
            List<byte> buf = new List<byte>();
            while (ns.DataAvailable)
            {
                buf.Add((byte)ns.ReadByte());
            }

            if (buf.Count == 0)
            {
                // We've been sent an empty request. Close the connection
                // and stop handling it.
                tcl.Close();
                return;
            }

            ms.Write(buf.ToArray(), 0, buf.Count);
            ms.Position = 0;

            var netStream = tcl.GetStream();
            HttpRequest req;
            HttpResponse res = new HttpResponse(netStream, () => 
            {
                netStream.Close();
                netStream.Dispose();
                tcl.Close();
            });

            try
            {
                req = new HttpRequest(ms);

                // If we've received a HEAD request, we are
                // to send only the headers, and not the
                // response body.
                //
                // To do this, we pass the HttpResponse a
                // MemoryStream instead of a NetworkStream.
                if (req.Method.Equals(HTTP_MTD_HEAD))
                {
                    MemoryStream resMS = new MemoryStream();

                    res = new HttpResponse(resMS, () =>
                    {
                        // We want to read the first line of the
                        // response, and the stream's position will
                        // be at the end, so we need to go back to
                        // the start.
                        resMS.Position = 0;
                        string headerSection = String.Format(
                                    "{0}\r\n{1}\r\n",
                                    // Gets the first line of the response,
                                    // or the "response line" (analogous to
                                    // the client's request line)
                                    HttpRequest.ReadAsciiLine(resMS),
                                    // The headers, correctly formatted
                                    res.Headers.ToString()
                                    );
                        byte[] resBytes = Encoding.ASCII
                            .GetBytes(headerSection);
                        // When Close() is called, we know that the response
                        // is over. Since we know that no new data will be
                        // written, we can take the headers and write them
                        // to the network stream.
                        //
                        // We can discard the body, as it shouldn't be sent
                        // with a HEAD request.
                        netStream.Write(resBytes, 0, resBytes.Length);
                    });
                }

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
            

            // These calls shouldn't throw an exception, so we'll be fine to
            // call them without checking to see if they've already been called.
            res.Close();
            ns.Close();
            ns.Dispose();
            tcl.Close();
            ms.Close();
            ms.Dispose();
        }

        /// <summary>
        /// Creates a new instance of the HTTP server on the specified port.
        /// </summary>
        /// <param name="port">The TCP port to listen on (1025-65535).</param>
        /// <param name="timeout">The maximum time, in milliseconds, the server will wait for requests to be received.</param>
        public HttpServer(int port)
        {
            _port = port;
            // Recommendation: Go directly to 127.0.0.1 and avoid the use of
            // 'localhost'. Windows seems to have a rather slow DNS resolver,
            // and using 'localhost' seems to add 300ms or so to response times.
            _listener = new TcpListener(IPAddress.Loopback, this.Port);
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
            _listenThread.Abort();
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
