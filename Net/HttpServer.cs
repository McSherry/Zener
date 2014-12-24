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
        private const string HTTP_HDR_CTNLEN = "Content-Length";
        private const string CRLF = "\r\n";
        private const int REQ_READTIMEOUT = 60000; // 60s
        private const int MAX_REQUEST_BODY = (1024 * 1024) * 16; // 16 MiB
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

            response.WriteLine("{0}\n", exception.ToString());
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
            var tcl = (TcpClient)tclo;

            tcl.NoDelay = true;
            tcl.Client.NoDelay = true;

            NetworkStream ns = tcl.GetStream();
            MemoryStream ms = new MemoryStream();

            // NetworkStream/TcpSocket/etc's timeout methods all
            // close the socket when a timeout occurs. We want to
            // be able to send a response to the client notifying
            // them of the timeout (specifically, an HTTP 408).
            //
            // To do this, we need our own separate timer, and to
            // run the read on a separate thread that we can kill
            // if a timeout occurs.
            //var timeoutTimer = new System.Timers.Timer(REQ_READTIMEOUT);
            Exception threadException = null;
            string requestLine = null;
            HttpHeaderCollection headers = null;
            bool timedOut = true;
            var readThread = new Thread(() =>
            {
                #region Request Handler Thread #2
                string line;
                // Lines before the request line can be blank.
                // We want to skip these since there's nothing
                // to parse.
                do { line = HttpRequest.ReadAsciiLine(ns); }
                while (String.IsNullOrEmpty(line));
                // We've now hit the first line with content. In
                // a compliant HTTP request, this is the request
                // line.
                requestLine = line;
                // Move past the request line in to what is likely
                // to be the first HTTP header in the request.
                line = HttpRequest.ReadAsciiLine(ns);

                StringBuilder headerBuilder = new StringBuilder();
                // Now that we have the start of the header section,
                // we need to keep reading lines until we find a blank
                // one, which indicates the end of the header section.
                while (!String.IsNullOrEmpty(line))
                {
                    headerBuilder.AppendLine(line);
                    line = HttpRequest.ReadAsciiLine(ns);
                }

                // We now have all the HTTP headers in the request.
                // To determine the content length, which we need for
                // reading the rest of the request, we need to parse
                // the headers.
                try
                {
                    using (StringReader sr = new StringReader(headerBuilder.ToString()))
                    {
                        headers = new HttpHeaderCollection(BasicHttpHeader.ParseMany(sr));
                    }
                }
                catch (ArgumentException aex)
                {
                    threadException = new HttpRequestException(
                        "Could not parse HTTP headers.", aex
                        );

                    return;
                }

                // If there isn't a Content-Length header, we can't
                // know how much data we have to wait for. This means
                // that we can't know if there is a request body or
                // not.
                //
                // To ensure maximum functionality (some browsers
                // don't send Content-Length when there is no body),
                // we will assume that, when no Content-Length header
                // is present, there is no request body. Only the
                // request line and headers will be passed to the
                // request handler.
                if (!headers.Contains(HTTP_HDR_CTNLEN))
                {

                    var cLen = headers[HTTP_HDR_CTNLEN].Last();

                    Int32 cLenOctets;
                    // Make sure that the value of the Content-Length
                    // header is a valid integer.
                    if (!Int32.TryParse(cLen.Value, out cLenOctets))
                    {
                        threadException = new HttpRequestException(
                            "Invalid Content-Length header."
                            );

                        return;
                    }

                    // The Content-Length cannot be negative.
                    if (cLenOctets < 0)
                    {
                        threadException = new HttpRequestException(
                            "Invalid Content-Length header."
                            );

                        return;
                    }

                    // Make sure the Content-Length isn't longer
                    // than our maximum length.
                    if (cLenOctets > MAX_REQUEST_BODY)
                    {
                        threadException = new HttpException(
                            HttpStatus.RequestEntityTooLarge,
                            "The request body was too large."
                            );

                        return;
                    }

                    // Read the bytes from the network.
                    byte[] bodyBytes = new byte[cLenOctets];
                    ns.Read(bodyBytes, 0, bodyBytes.Length);

                    // We've finished reading from the network,
                    // so we can stop our timer.
                    //timeoutTimer.Stop();
                    //timeoutTimer.Dispose();

                    // Write the bytes back to our memory stream.
                    ms.Write(bodyBytes, 0, bodyBytes.Length);
                }
                timedOut = false;
                #endregion
            });

            readThread.Start();
            readThread.Join(REQ_READTIMEOUT);

            if (timedOut)
                threadException = new HttpException(
                    HttpStatus.RequestTimeout,
                    "Client request timed out."
                    );

            HttpRequest req;
            HttpResponse res = new HttpResponse(ns, () => 
            {
                ns.Close();
                ns.Dispose();
                tcl.Close();
                ms.Close();
                ms.Dispose();
            });

            try
            {
                // If this variable isn't null, it means our reading thread
                // "threw" an exception that it wants propagated back to this
                // thread.
                if (threadException != null) throw threadException;

                req = new HttpRequest(
                    requestLine, headers, ms
                    );

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
                        ns.Write(resBytes, 0, resBytes.Length);
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
            
            res.Close();
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
