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

namespace McSherry.Zener.Core.Coding
{
    /// <summary>
    /// Implements an encoder for chunked transfer encoding.
    /// </summary>
    public sealed class ChunkedEncoder
        : IEncoder
    {
        /// <summary>
        /// This encoding's name.
        /// </summary>
        private const string NameStr = "chunked";
        /// <summary>
        /// The bytes of the character zero (0).
        /// </summary>
        private static readonly byte[] ZeroChar;
        /// <summary>
        /// The bytes of a CRLF sequence.
        /// </summary>
        private static readonly byte[] CRLF;
        /// <summary>
        /// The chunk to be used as a terminator chunk.
        /// </summary>
        private static readonly byte[] Terminator;
        /// <summary>
        /// ChunkedEncoder doesn't need to maintain any state,
        /// and there are no thread-safety concerns, so it makes
        /// sense to only ever have one instance.
        /// </summary>
        private static readonly ChunkedEncoder Singleton;

        static ChunkedEncoder()
        {
            ZeroChar = Encoding.ASCII.GetBytes("0");
            CRLF = Encoding.ASCII.GetBytes("\r\n");

            // A terminator chunk is a length field with the hex digit
            // zero as its contents, followed by two CRLFs.
            Terminator = new byte[(2 * CRLF.Length) + ZeroChar.Length];

            Buffer.BlockCopy(ZeroChar, 0, Terminator, 0, ZeroChar.Length);
            Buffer.BlockCopy(CRLF, 0, Terminator, ZeroChar.Length, CRLF.Length);
            Buffer.BlockCopy(
                CRLF, 0,
                Terminator, Terminator.Length - CRLF.Length, CRLF.Length
                );

            Singleton = new ChunkedEncoder();
        }

        /// <summary>
        /// Creates a new chunked encoder.
        /// </summary>
        /// <returns>The created chunked encoder.</returns>
        public static ChunkedEncoder Create()
        {
            return Singleton;
        }

        private ChunkedEncoder()
        {
            
        }

        /// <summary>
        /// The name of this encoding.
        /// </summary>
        public string Name
        {
            get { return NameStr; }
        }

        /// <summary>
        /// Encodes the data using the chunked transfer
        /// encoding scheme.
        /// </summary>
        /// <param name="data">The data to encode.</param>
        /// <returns>The provided data, as a chunk.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided data is null.
        /// </exception>
        public byte[] Encode(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(
                    "The provided data must not be null."
                    );
            }

            // We want to catch any zero-data before attempting
            // to encode it. A "zero-length" (length field with
            // hex digit 0 and no data between CRLFs) chunk
            // indicates the end of the stream of chunks, so we
            // don't want the user to accidentally send that.
            if (data.Length == 0) return data;

            // Convert the length of the data, in bytes, to
            // a hexadecimal string, then convert that string
            // to a set of ASCII bytes.
            byte[] lenBytes = Encoding.ASCII.GetBytes(
                data.Length.ToString("x")
                );

            // The final data will be the hex string containing
            // the length of the data, followed by a CRLF, followed
            // by the data itself, and terminated with a CRLF.
            byte[] fin = new byte[lenBytes.Length + (2 * CRLF.Length) + data.Length];

            Buffer.BlockCopy(lenBytes, 0, fin, 0, lenBytes.Length);
            Buffer.BlockCopy(CRLF, 0, fin, lenBytes.Length, CRLF.Length);
            Buffer.BlockCopy(data, 0, fin, CRLF.Length + lenBytes.Length, data.Length);
            Buffer.BlockCopy(CRLF, 0, fin, fin.Length - CRLF.Length, CRLF.Length);

            return fin;
        }
        /// <summary>
        /// Returns the terminator chunk for a chunked encoding stream.
        /// </summary>
        /// <returns>
        /// The terminator chunk for a chunked encoding stream.
        /// </returns>
        public byte[] Finalise()
        {
            return (byte[])Terminator.Clone();
        }
    }
}
