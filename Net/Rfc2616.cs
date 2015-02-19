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
    /// A class that provides RFC 2616-related methods.
    /// </summary>
    public static class Rfc2616
    {
        /// <summary>
        /// Parses, from an HTTP 'Host' header, the domain name and
        /// port the client has requested.
        /// </summary>
        /// <param name="hdrValue">
        /// A string containing the value of the 'Host' header.
        /// </param>
        /// <returns>
        /// A Tuple containing the domain name and port parsed from the
        /// HTTP 'Host' header.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided string header value is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided header value is malformed.
        /// </exception>
        public static Tuple<string, ushort> ParseHostHeader(string hdrValue)
        {
            if (String.IsNullOrWhiteSpace(hdrValue))
            {
                throw new ArgumentNullException(
                    "The provided value must not be null, empty, or whitespace."
                    );
            }

            var parts = hdrValue
                .ToLower()
                .Trim(' ', '.', '/')
                .Split(':');

            ushort port;
            // The greatest number of parts we expect is two. This means
            // that there is a domain name and a port. If we have more,
            // the 'Host' header is invalid.
            if (parts.Length > 2)
            {
                throw new ArgumentException(
                    "The provided \"Host\" header value is malformed."
                    );
            }
            else if (parts.Length == 2)
            {
                // If there are two parts, the first should contain
                // the domain name.
                hdrValue = parts[0].TrimEnd();

                // The second part contains the port, which should be
                if (!UInt16.TryParse(parts[1], out port))
                {
                    throw new ArgumentException(
                        "The provided \"Host\" header value is malformed; " +
                        "the port is not a valid integer in the range 1 to" +
                        " 65.535"
                        );
                }

                if (port == 0)
                {
                    throw new ArgumentException(
                        "The provided \"Host\" header is malformed; " +
                        "port zero is not a valid port."
                        );
                }
            }
            else
            {
                // If there is a single part, we only have a domain name.
                hdrValue = parts[0].TrimEnd();
                // If the 'Host' header doesn't include a port, we are to
                // assume that port 80 is requested.
                port = 80;
            }

            return new Tuple<string, ushort>(hdrValue, port);
        }
    }
}
