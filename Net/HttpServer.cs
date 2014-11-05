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
    public delegate HttpResponse HTTPRequestHandler(HttpRequest request);

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
            ms.Write(buf.ToArray(), 0, buf.Count);
            ms.Seek(0, SeekOrigin.Begin);

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
            ThreadPool.SetMinThreads(10, 10);
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
        /// An event for when the server receives an HTTP request.
        /// </summary>
        public event HTTPRequestHandler RequestReceived;
    }
}
