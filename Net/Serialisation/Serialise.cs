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

namespace McSherry.Zener.Net.Serialisation
{
    /// <summary>
    /// A class providing serialisation-related utility methods.
    /// </summary>
    public static class Serialise
    {
        private static readonly Dictionary<HttpConnection, string> ConnMsgs
            = new Dictionary<HttpConnection, string>()
            {
                { HttpConnection.Close,         "close"         },
                { HttpConnection.KeepAlive,     "keep-alive"    },
            };

        /// <summary>
        /// Retrieves the plain-text header value for the specified
        /// HttpConnection value.
        /// </summary>
        /// <param name="connection">
        /// The HttpConnection value to retrieve the plain-text
        /// value for.
        /// </param>
        /// <returns>
        /// A string representing the HttpConnection value, or null
        /// if the value is unrecognised.
        /// </returns>
        public static string GetValue(this HttpConnection connection)
        {
            string retVal;
            if (!ConnMsgs.TryGetValue(connection, out retVal))
            {
                retVal = null;
            }

            return retVal;
        }
    }
}
