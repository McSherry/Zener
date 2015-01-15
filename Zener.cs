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
using System.Threading.Tasks;
using System.IO;

namespace SynapLink.Zener
{
    /// <summary>
    /// Provides a set of methods that can ease Zener's use,
    /// and that are used within Zener itself.
    /// </summary>
    public static class Zener
    {
        /// <summary>
        /// Reads a single line from a stream and returns the ASCII-encoded string.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>A single line from the stream, ASCII-encoded.</returns>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided stream cannot be read from.
        /// </exception>
        public static string ReadAsciiLine(this Stream stream)
        {
            if (!stream.CanRead)
                throw new ArgumentException
                ("Provided stream cannot be read from.");

            List<byte> buf = new List<byte>();
            ReadUntilFound(stream, "\r\n", Encoding.ASCII, buf.Add);

            return Encoding.ASCII.GetString(buf.ToArray());
        }
        /// <summary>
        /// Reads bytes until the specified boundary is found.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="boundary">The boundary to read to.</param>
        /// <param name="encoding">The encoding of the data and boundary.</param>
        /// <param name="readCall">The callback that is passed each byte as it is read.</param>
        public static void ReadUntilFound(
            this Stream stream,
            string boundary, Encoding encoding,
            Action<byte> readCall
            )
        {
            byte[] boundaryBytes = encoding.GetBytes(boundary);

            stream.ReadUntilFound(boundaryBytes, readCall);
        }
        /// <summary>
        /// Reads bytes until the specified boundary is found.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="boundary">The boundary to read to.</param>
        /// <param name="readCall">The callback that is passed each byte as it is read.</param>
        public static void ReadUntilFound(
            this Stream stream,
            byte boundary, Action<byte> readCall
            )
        {
            stream.ReadUntilFound(new[] { boundary }, readCall);
        }
        /// <summary>
        /// Reads bytes until the specified boundary is found.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="boundary">The boundary to read to.</param>
        /// <param name="readCall">The callback that is passed each byte as it is read.</param>
        public static void ReadUntilFound(
            this Stream stream,
            byte[] boundary, Action<byte> readCall
            )
        {
            byte[] window = new byte[boundary.Length];

            //if (stream.Length - stream.Position < boundaryBytes.Length)
            //    throw new InvalidOperationException
            //    ("Too few bytes left in stream to find boundary.");

            stream.Read(window, 0, window.Length);

            while (true)
            {
                // We've reached the boundary!
                if (window.SequenceEqual(boundary)) break;

                int next = stream.ReadByte();
                // Looks like we've hit the end of the stream.
                // Nothing more to read.
                if (next == -1) break;

                // Return the byte we're about to discard so
                // it can be used by the caller.
                readCall(window[0]);
                // Shift the window ahead by a single byte.
                Buffer.BlockCopy(window, 1, window, 0, window.Length - 1);
                window[window.Length - 1] = (byte)next;
            }
        }
    }
}
