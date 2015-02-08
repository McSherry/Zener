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
        /// <summary>
        /// Encodes the provided data in the RFC 2045 Base64 format,
        /// assuming that the provided string is UTF-8.
        /// </summary>
        /// <param name="data">The data to encode.</param>
        /// <returns>The Base64-encoded data as a string.</returns>
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
        public static string Base64Encode(string data, Encoding encoding)
        {
            return Rfc2045.Base64Encode(encoding.GetBytes(data));
        }
        /// <summary>
        /// Encodes the provided data in the RFC 2045 Base64 format.
        /// </summary>
        /// <param name="data">The data to an encode.</param>
        /// <returns>The Base64-encoded data as a string.</returns>
        public static string Base64Encode(IEnumerable<byte> data)
        {
            throw new NotImplementedException();
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
