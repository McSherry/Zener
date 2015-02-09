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
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided data is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided string does not contain
        ///     any valid Base64 characters.
        ///     
        ///     Thrown when the number of valid Base64 characters
        ///     in the provided string is not a multiple of four.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the data contains padding characters
        ///     in an invalid location.
        /// </exception>
        public static IEnumerable<byte> Base64Decode(string data)
        {
            // We need to make sure that the string isn't null before
            // we attempt to filter it. If we don't do this, we may end
            // up with a leaky abstraction throwing NullReferenceException.
            if (data == null)
            {
                throw new ArgumentException(
                    "The provided string must not be null."
                    );
            }

            var alpha = BASE64_ALPHA.ToCharArray();
            var fdata = data
                .Where(c => alpha.Contains(c) || c == BASE64_PADCHAR)
                .ToArray();

            // We couldn't test length before because it was possible that
            // the string contained non-Base64 characters. Now that we've
            // removed these characters, if the enumerable is empty we know
            // that there are no valid characters in the string.
            if (fdata.Length == 0)
            {
                throw new ArgumentException(
                    "The provided string does not contain any valid characters."
                    );
            }
            // Base64 transforms 24 bits of data in to 32 bits of data, padding
            // where required. This means that the length of the Base64 string
            // needs to be a multiple of 32 bits (4 bytes) to be valid. If it
            // isn't a multiple of 4 bytes, it isn't valid Base64.
            if (fdata.Length % 4 != 0)
            {
                throw new ArgumentException(
                    "The number of valid Base64 characters is not a multiple of four."
                    );
            }
            
            char[] cbuf = new char[4];
            var ms = new MemoryStream();
            for (int i = 0; i < fdata.Length; i += 4)
            {
                // Copy the characters in to the buffer.
                Array.ConstrainedCopy(fdata, i, cbuf, 0, cbuf.Length);

                // The padding characters will never be first or second in a
                // four-character group. If it is there, something's gone wrong.
                if (cbuf[0] == BASE64_PADCHAR || cbuf[1] == BASE64_PADCHAR)
                {
                    throw new System.IO.InvalidDataException(
                        "The data contains padding characters in an invalid location."
                        );
                }
                int padCount = 0;
                // Similarly, we need to make sure that the padding characters are
                // at the end of the string. If there are padding characters in the
                // middle of the string, again, something's gone wrong.
                if (cbuf[2] == BASE64_PADCHAR || cbuf[3] == BASE64_PADCHAR)
                {
                    // We're in the middle of the string.
                    if (i + 4 < fdata.Length)
                    {
                        throw new System.IO.InvalidDataException(
                            "The data contains padding characters in an invalid location."
                            );
                    }
                    else
                    {
                        // We're at the end. We need to know how many padding characters
                        // there are.
                        padCount = cbuf.Count(BASE64_PADCHAR.Equals);
                    }
                }

                // No padding characters, or four data characters.
                if (padCount == 0)
                {
                    uint cat =
                        (((uint)BASE64_ALPHA.IndexOf(cbuf[0])) << 0x12) |
                        (((uint)BASE64_ALPHA.IndexOf(cbuf[1])) << 0x0C) |
                        (((uint)BASE64_ALPHA.IndexOf(cbuf[2])) << 0x06) |
                        (((uint)BASE64_ALPHA.IndexOf(cbuf[3])) << 0x00) ;

                    ms.Write(new[] {
                        (byte)((cat & 0x00FF0000) >> 0x10),
                        (byte)((cat & 0x0000FF00) >> 0x08),
                        (byte)((cat & 0x000000FF) >> 0x00)
                    }, 0, 3);
                }
                else if (padCount == 1)
                {
                    uint cat =
                        (((uint)BASE64_ALPHA.IndexOf(cbuf[0])) << 0x0C) |
                        (((uint)BASE64_ALPHA.IndexOf(cbuf[1])) << 0x06) |
                        (((uint)BASE64_ALPHA.IndexOf(cbuf[2])) << 0x00) ;

                    ms.Write(new[] {
                        (byte)((cat & 0x0000FF00) >> 0x08),
                        (byte)((cat & 0x000000FF) >> 0x00)
                    }, 0, 2);
                }
                else
                {
                    uint cat =
                        (((uint)BASE64_ALPHA.IndexOf(cbuf[0])) << 0x06) |
                        (((uint)BASE64_ALPHA.IndexOf(cbuf[1])) << 0x00) ;

                    ms.Write(new[] {
                        (byte)((cat & 0xFF0) >> 0x04)
                    }, 0, 1);
                }
            }

            using (ms)
            {
                return ms.ToArray();
            }
        }
    }
}
