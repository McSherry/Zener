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
    /// A static class providing encoding-related methods.
    /// </summary>
    public static class Coding
    {
        /// <summary>
        /// Encodes the provided data.
        /// </summary>
        /// <param name="encoder">The encoder to use.</param>
        /// <param name="bytes">The data to encode.</param>
        /// <returns>
        /// The encoded data as a byte array.
        /// </returns>
        public static byte[] Encode(this IEncoder encoder, byte[] bytes)
        {
            return encoder.Encode(bytes, 0, bytes.Length);
        }
        /// <summary>
        /// Decodes the provided data.
        /// </summary>
        /// <param name="decoder">The decoder to use.</param>
        /// <param name="bytes">The data to decode.</param>
        /// <returns>
        /// The decoded data as a byte array.
        /// </returns>
        public static byte[] Decode(this IDecoder decoder, byte[] bytes)
        {
            return decoder.Decode(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Checks the provided bounds against the specified array,
        /// and throws an exception if they are invalid.
        /// </summary>
        /// <param name="data">The array to check.</param>
        /// <param name="startIndex">
        /// The index at which to start encoding the array.
        /// </param>
        /// <param name="count">
        /// The number of bytes within the array to encode.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided source array is null.
        /// </exception>
        /// <exception cref="System.IndexOutOfRangeException">
        /// <para>
        /// Thrown when the starting index or the count is
        /// negative.
        /// </para>
        /// <para>
        /// Thrown when the count is zero.
        /// </para>
        /// <para>
        /// Thrown when the sum of the starting index and the
        /// count is greater than the length of the array.
        /// </para>
        /// </exception>
        public static void ValidateBounds(byte[] data, int startIndex, int count)
        {
            if (data == null)
            {
                throw new ArgumentNullException(
                    "The provided source array must not be null."
                    );
            }

            if (startIndex < 0 || count <= 0)
            {
                throw new IndexOutOfRangeException(
                    "The start index and count must be positive, " +
                    "and the count must be non-zero."
                    );
            }

            if (startIndex >= data.Length)
            {
                throw new IndexOutOfRangeException(
                    "The provided start index must be less than the array's length."
                    );
            }

            if (startIndex + count > data.Length)
            {
                throw new IndexOutOfRangeException(
                    "The specified byte range extends past the end of the array."
                    );
            }
        }
    }
}
