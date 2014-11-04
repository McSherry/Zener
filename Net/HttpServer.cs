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
    public delegate HTTPResponse HTTPRequestHandler(HTTPRequest request);

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

                new Thread(() => HttpRequestHandler(tcl)).Start();
            }
        }
        private void HttpRequestHandler(TcpClient tcl)
        {
            NetworkStream ns = tcl.GetStream();
            MemoryStream ms = new MemoryStream();

            var copyTask = ns.CopyToAsync(ms);

            while (ns.DataAvailable) { continue; }
            

        }

        /// <summary>
        /// Reads the next line from the stream. Lines should end in CRLF,
        /// which will be removed from the returned line.
        /// </summary>
        /// <param name="ms">The memory stream to read the line from.</param>
        /// <returns>The line (sans CRLF), or an empty byte array if the end of the stream has been reached.</returns>
        private static byte[] ReadNextLine(MemoryStream ms)
        {
            // The number of to-be-read bytes in the memory stream
            // based on its current position and length.
            long remLeng = (ms.Length - ms.Position);
            byte[] buf = new byte[remLeng < 16 ? remLeng : 16];

            // Our current offset within the memory stream.
            int offset = 0;
            while (true)
            {
                // If there are no bytes remaining, it means we've
                // reached the end of the stream, and that there isn't
                // a CRLF indicating the end of a line. In this instance,
                // we return any bytes in the buffer. Any further calls to
                // ReadNextLine() should return an empty byte[].
                if (remLeng == 0) return buf;

                // Since it's likely we'll be iterating, and likely
                // that we'll be growing the buffer before the next
                // iteration, we'll use the (new) buffer size and the
                // offset to find out how many new bytes we need to
                // read in to the buffer.
                ms.Read(buf, offset, buf.Length - offset);

                // If offset is greater than zero at this point, it means
                // that we've performed at least a single iteration, and that
                // at least a second iteration is required. In this case, it is
                // possible that we've only read half of a CRLF (i.e. the last
                // byte of the buffer before a resize was a CR). Leaving the
                // offset as-is could mean a CRLF is missed. By decrementing the
                // offset, we won't miss any CRLFs that were split across stream
                // reads.
                if (offset > 0) --offset;

                // Since we're going through the buffer byte-by-byte,
                // and since we're checking two bytes, we need to make
                // sure the index is never the end of the buffer, or we'll
                // attempt to check an index that doesn't exist, and that
                // won't end well.
                for (; offset < buf.Length - 1; offset++)
                {
                    // We're checking for a CRLF, which indicates the end
                    // of a line. If we find one, we know we've reached the
                    // end of the line, and we can stop reading.
                    if (buf[offset] == (byte)'\r' && buf[offset + 1] == (byte)'\n')
                    {
                        // So first we find out how many of the bytes in our
                        // buffer will be unused (since bytes after the CRLF
                        // are a separate line).
                        int unbytes = buf.Length - offset;
                        // Then we determine how many bytes remain in the buffer
                        // once we remove all unused bytes.
                        int lineLeng = buf.Length - unbytes;
                        // We then resize our buffer so it only contains the bytes
                        // that comprise the current line. The remaining bytes are
                        // lopped off and will no longer be in the buffer. We
                        // subtract one from the line length to ensure we don't get
                        // a CR in our buffer.
                        Array.Resize(ref buf, lineLeng);
                        // We then seek the memory stream backwards by the number of
                        // unused bytes. We can now be sure, when we next go to read
                        // from the memory stream, that it starts on the first byte
                        // of a new line. We increment by 2 to ensure that we skip
                        // the CRLF in any subsequent calls to ReadNextLine().
                        ms.Seek(-unbytes + 2, SeekOrigin.Current);
                        // We've now made sure everything is taken care of, so we
                        // can return the contents of our buffer as the line.
                        return buf;
                    }
                }

                // If we're here, it means that the current buffer does not contain
                // a CRLF. This means that we should read in more bytes from the
                // memory stream.

                // We'll first check to see whether the memory stream has enough
                // data to fill a doubled buffer. Doubling the buffer size offers
                // a reasonable trade-off between wasted memory and new memory
                // allocation.
                int tentativeLeng = buf.Length * 2;
                if (tentativeLeng > remLeng)
                {
                    // If the memory stream doesn't have enough data to fill a
                    // doubled buffer, we'll end up here. We can then resize the
                    // buffer to the size of the memory stream. It's unlikely
                    // that we'll be receiving more than 4GB of data at once in
                    // an HTTP request, so we're fine just casting to int.
                    Array.Resize(ref buf, (int)remLeng);
                }
                else
                {
                    // If we've ended up here, it means that the buffer is large
                    // enough that it will fill a buffer of doubled size, so we
                    // can go ahead and resize the buffer.
                    Array.Resize(ref buf, tentativeLeng);
                }

                // We need to update the remaining number of bytes on each
                // iteration.
                remLeng = (ms.Length - ms.Position);
            }
        }

        /// <summary>
        /// Creates a new instance of the HTTP server on the specified port.
        /// </summary>
        /// <param name="port">The TCP port to listen on (1025-65535).</param>
        public HttpServer(int port)
        {
            _port = port;
            _listener = new TcpListener(IPAddress.Any, this.Port);
            _listenThread = new Thread(TcpAcceptor);
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
