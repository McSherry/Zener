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
    public delegate HttpResponse HttpRequestHandler(HttpRequest request);
    public delegate HttpResponse HttpErrorHandler(int statusCode);

    /// <summary>
    /// A class implementing a basic HTTP server.
    /// </summary>
    public class HttpServer
    {
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

            bool requestFailed;

            HttpRequest req;
            using (StreamReader sr = new StreamReader(ms))
            {
                req = new HttpRequest(sr, out requestFailed);
            }

            if (requestFailed && this.ErrorHandler != null)
            {
                // requestFailed being true indicates that parsing the
                // request failed. This means that the request was malformed,
                // and so we can use our status code 400 (Bad Request) handler.
                this.ErrorHandler(400);
            }

            tcl.Close();
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
