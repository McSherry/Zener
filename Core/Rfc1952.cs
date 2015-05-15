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
using System.IO;

namespace McSherry.Zener.Core
{
    /// <summary>
    /// A class providing RFC 1952-related functions.
    /// </summary>
    public static class Rfc1952
    {
        #region CRC-32 lookup table
        private const uint CRC_INIT = 0x00000000;
        private static readonly uint[] CRC_TABLE;
        private static void _GenerateCrc32Lookup(out uint[] array)
        {
            /* This function adapted from the example code in
             * RFC 1952's appendix 8. This function (_GenerateCrc32Lookup)
             * is released in to the public domain.
             */
            array = new uint[256];

            uint c;
            for (int n = 0; n < array.Length; n++)
            {
                c = (uint)n;

                for (int k = 0; k < 8; k++)
                {
                    if ((c & 1) == 1)
                    {
                        c = 0xEDB88320U ^ (c >> 1);
                    }
                    else
                    {
                        c >>= 1;
                    }
                }

                array[n] = c;
            }
        }
        #endregion

        static Rfc1952()
        {
            _GenerateCrc32Lookup(out CRC_TABLE);
        }

        /// <summary>
        /// Generates an RFC 1952-compliant CRC-32 checksum from
        /// the provided string. Assumes that the string is UTF-8.
        /// </summary>
        /// <param name="data">The string to generate a checksum from.</param>
        /// <returns>
        ///     An unsigned 32-bit integer containing the generated
        ///     checksum.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided data or encoding is
        ///     null.
        /// </exception>
        public static uint GenerateCrc32(string data)
        {
            return Rfc1952.GenerateCrc32(data, Encoding.UTF8);
        }
        /// <summary>
        /// Generates an RFC 1952-compliant CRC-32 checksum from
        /// the provided string.
        /// </summary>
        /// <param name="data">The string to generate a checksum from.</param>
        /// <param name="encoding">The encoding of the string.</param>
        /// <returns>
        ///     An unsigned 32-bit integer containing the generated
        ///     checksum.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided data or encoding is
        ///     null.
        /// </exception>
        public static uint GenerateCrc32(string data, Encoding encoding)
        {
            if (data == null)
            {
                throw new ArgumentNullException(
                    "The provided string must not be null."
                    );
            }

            if (encoding == null)
            {
                throw new ArgumentNullException(
                    "The provided encoding must not be null."
                    );
            }

            return Rfc1952.GenerateCrc32(encoding.GetBytes(data));
        }
        /// <summary>
        /// Generates an RFC 1952-compliant CRC-32 checksum
        /// from the provided bytes.
        /// </summary>
        /// <param name="data">The bytes to generate a checksum from.</param>
        /// <returns>
        ///     An unsigned 32-bit integer containing the generated
        ///     checksum.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided set of bytes is null.
        /// </exception>
        public static uint GenerateCrc32(IEnumerable<byte> data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(
                    "The provided set of bytes must not be null."
                    );
            }

            byte[] dataArr = data is byte[]
                ? (byte[])data : data.ToArray();

            using (MemoryStream ms = new MemoryStream(dataArr))
            {
                return Rfc1952.GenerateCrc32(ms);
            }
        }
        /// <summary>
        /// Generates an RFC 1952-compliant CRC-32 checksum
        /// from the provided stream.
        /// </summary>
        /// <param name="stream">The stream to generate a checksum from.</param>
        /// <returns>
        ///     An unsigned 32-bit integer containing the generated
        ///     checksum.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided stream is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided stream does not
        ///     support reading.
        /// </exception>
        public static uint GenerateCrc32(Stream stream)
        {
            /* This function adapted from the example code in
             * RFC 1952's appendix 8. This function (GenerateCrc32)
             * is released in to the public domain.
             */

            if (stream == null)
            {
                throw new ArgumentNullException(
                    "The provided stream must not be null."
                    );
            }

            if (!stream.CanRead)
            {
                throw new ArgumentException(
                    "The provided stream must support reading."
                    );
            }

            uint crc = CRC_INIT,
                c = crc ^ 0xFFFFFFFF;

            int b = 0;
            while ((b = stream.ReadByte()) != -1)
            {
                c = CRC_TABLE[(c ^ (byte)b) & 0xFF] ^ (c >> 8);
            }

            return c ^ 0xFFFFFFFF;
        }
    }
}
