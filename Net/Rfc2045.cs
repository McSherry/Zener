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

namespace McSherry.Zener.Net
{
    /// <summary>
    /// A class providing RFC 2045-related functions.
    /// </summary>
    public static class Rfc2045
    {
        // Value->Character map (alphabet)
        private const string 
            BASE64_ALPHA    = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
            ;
        private const char 
            BASE64_PADCHAR  = '='
            ;

        /// <summary>
        /// Encodes the provided data in the RFC 2045 Base64 format,
        /// assuming that the provided string is UTF-8.
        /// </summary>
        /// <param name="data">The data to encode.</param>
        /// <returns>The Base64-encoded data as a string.</returns>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided data is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided data is zero-length.
        /// </exception>
        public static string Base64Encode(string data)
        {
            return Rfc2045.Base64Encode(data, Encoding.UTF8);
        }
        /// <summary>
        /// Encodes the provided data in the RFC 2045 Base64 format.
        /// </summary>
        /// <param name="data">The data to encode.</param>
        /// <param name="encoding">The encoding the data is in.</param>
        /// <returns>The Base64-encoded data as a string.</returns>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided data is null.
        ///     
        ///     Thrown when the provided encoding is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided data is zero-length.
        /// </exception>
        public static string Base64Encode(string data, Encoding encoding)
        {
            if (encoding == null)
            {
                throw new ArgumentNullException(
                    "The provided encoding must not be null."
                    );
            }

            return Rfc2045.Base64Encode(encoding.GetBytes(data));
        }
        /// <summary>
        /// Encodes the provided data in the RFC 2045 Base64 format.
        /// </summary>
        /// <param name="data">The data to an encode.</param>
        /// <returns>The Base64-encoded data as a string.</returns>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided data is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided data is zero-length.
        /// </exception>
        public static string Base64Encode(IEnumerable<byte> data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(
                    "The provided data must not be null."
                    );
            }

            byte[] bytes = data is byte[]
                ? (byte[])data : data.ToArray();

            if (bytes.Length == 0)
            {
                throw new ArgumentException(
                    "The provided data must not be zero-length."
                    );
            }

            StringBuilder b64s = new StringBuilder();
            for (int i = 0; i < bytes.Length; i += 3)
            {
                // Get the remaining number of bytes, up to
                // a maximum of three bytes.
                int r = (bytes.Length - i);
                r = r >= 3 ? 3 : r;

                if (r == 3)
                {
                    // The three bytes catenated to form a 24-bit group.
                    uint cat = 
                        (((uint)bytes[i + 0]) << 0x10) |
                        (((uint)bytes[i + 1]) << 0x08) |
                        (((uint)bytes[i + 2]) << 0x00) ;

                    b64s.Append(new[] {
                        // We now need to split our 24-bit group in to four
                        // six-bit groups. These six-bit groups are used as
                        // indices within our 64-character alphabet.
                        BASE64_ALPHA[(int)((cat & 0xFC0000) >> 0x12)],
                        BASE64_ALPHA[(int)((cat & 0x03F000) >> 0x0C)],
                        BASE64_ALPHA[(int)((cat & 0x000FC0) >> 0x06)],
                        BASE64_ALPHA[(int)((cat & 0x00003F) >> 0x00)]
                    });
                }
                else if (r == 2)
                {
                    uint cat = 
                        (((uint)bytes[i + 0]) << 0x10) |
                        (((uint)bytes[i + 1]) << 0x08) ;

                    b64s.Append(new[] {
                        // Similar to what we'd do with the full three bytes.
                        // However, since we're a byte down, the final six
                        // bits would (erroneously) appear as the index 0. To
                        // indicate that we didn't have the fourth index, we
                        // add in our padding character.
                        BASE64_ALPHA[(int)((cat & 0xFC0000) >> 0x12)],
                        BASE64_ALPHA[(int)((cat & 0x03F000) >> 0x0C)],
                        BASE64_ALPHA[(int)((cat & 0x000FC0) >> 0x06)],
                        BASE64_PADCHAR
                    });
                }
                else
                {
                    uint cat = ((uint)bytes[i + 0]) << 0x10;

                    b64s.Append(new[] {
                        // Just like what we do when there are only two bytes,
                        // but with an additional padding character since we're
                        // two bytes down instead of just one.
                        BASE64_ALPHA[(int)((cat & 0xFC0000) >> 0x12)],
                        BASE64_ALPHA[(int)((cat & 0x03F000) >> 0x0C)],
                        BASE64_PADCHAR,
                        BASE64_PADCHAR
                    });
                }
            }

            // We'll now have our full Base64 string, with padding
            // characters, in the StringBuilder. All that's left is
            // to return the built string.
            return b64s.ToString();
        }

        /// <summary>
        /// Decodes the provided data from the RFC 2045 Base64 format,
        /// and returns it as a string in the specified encoding.
        /// </summary>
        /// <param name="data">The data to decode.</param>
        /// <param name="encoding">The encoding to return the data in.</param>
        /// <returns>The decoded data in the specified encoding.</returns>
        public static string Base64Decode(string data, Encoding encoding)
        {
            return encoding.GetString((byte[])Rfc2045.Base64Decode(data));
        }
        /// <summary>
        /// Decodes the provided data from the RFC 2045 Base64 format.
        /// </summary>
        /// <param name="data">The data to decode.</param>
        /// <returns>The bytes decoded from the string.</returns>
        public static IEnumerable<byte> Base64Decode(string data)
        {
            throw new NotImplementedException();
        }
    }
}
