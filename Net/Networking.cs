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
    /// Provides a set of methods related to networking.
    /// </summary>
    public static class Networking
    {
        #region HttpStatus Messages
        private const string HTTP_STATUSMSG_DEFAULT = "Non-Standard Status Code";
        private static readonly Dictionary<HttpStatus, string> HttpStatusMessages
            = new Dictionary<HttpStatus, string>()
            {
                { (HttpStatus)100, "Continue" },
                { (HttpStatus)101, "Switching Protocols" },
                
                { (HttpStatus)200, "OK" },
                { (HttpStatus)201, "Created" },
                { (HttpStatus)202, "Accepted" },
                { (HttpStatus)203, "Non-Authoritative Information" },
                { (HttpStatus)204, "No Content" },
                { (HttpStatus)205, "Reset Content" },
                { (HttpStatus)206, "Partial Content" },
                
                { (HttpStatus)300, "Multiple Choices" },
                { (HttpStatus)301, "Moved Permanently" },
                { (HttpStatus)302, "Found" },
                { (HttpStatus)303, "See Other" },
                { (HttpStatus)304, "Not Modified" },
                { (HttpStatus)305, "Use Proxy" },
                { (HttpStatus)307, "Temporary Redirect" },

                { (HttpStatus)400, "Bad Request" },
                { (HttpStatus)401, "Unauthorized" },
                { (HttpStatus)402, "Payment Required" },
                { (HttpStatus)403, "Forbidden" },
                { (HttpStatus)404, "Not Found" },
                { (HttpStatus)405, "Method Not Allowed" },
                { (HttpStatus)406, "Not Acceptable" },
                { (HttpStatus)407, "Proxy Authentication Required" },
                { (HttpStatus)408, "Request Time-out" },
                { (HttpStatus)409, "Conflict" },
                { (HttpStatus)410, "Gone" },
                { (HttpStatus)411, "Length Required" },
                { (HttpStatus)412, "Precondition Failed" },
                { (HttpStatus)413, "Request Entity Too Large" },
                { (HttpStatus)414, "Request URI Too Large" },
                { (HttpStatus)415, "Unsupported Media Type" },
                { (HttpStatus)416, "Request range not satisfiable" },
                { (HttpStatus)417, "Expectation Failed" },

                { (HttpStatus)500, "Internal Server Error" },
                { (HttpStatus)501, "Not Implemented" },
                { (HttpStatus)502, "Bad Gateway" },
                { (HttpStatus)503, "Service Unavailable" },
                { (HttpStatus)504, "Gateway Time-out" },
                { (HttpStatus)505, "HTTP Version not supported" }
            };
        #endregion
        private const string
            HDR_CLAUTH          = "Authorization",
            HDR_SVAUTH          = "WWW-Authenticate",

            HDRF_SVAUTH_BASIC   = "Basic realm=\"{0}\"",
            HDRF_CLAUTH_STBSC   = "Basic ",

            // Invalid characters in Basic authentication
            // "realm" field values.
            HTTP_SVBASIC_RLMINV = "\"\r\n"
            ;
        #region String Parsing Method Fields/Constants
        private const char 
            BACKSLASH       = '\\',
            DOUBLEQUOTE     = '"',
            COMMA           = ',',
            SPACE           = ' '
            ;
        private const string
            OCT_NUMERICS    = "01234567",
            HEX_NUMERICS    = "0123456789ABCDEFabcdef"
            ;
        private static readonly Dictionary<char, char>
            C_ESCAPES = new Dictionary<char, char>()
            {
                { 'n', '\n' },
                { 'r', '\r' },
                { '0', '\0' },
                { 'b', '\b' },
                { 't', '\t' },
                { 'v', '\v' },
                { 'a', '\a' },
                { 'f', '\f' }
            };
        #endregion

        /// <summary>
        /// Parses a set of semi-quoted strings, delimited by the
        /// specified delimiter. Semi-quoted means that whitespace
        /// will be ignored outside of a pair of quotes, but will be
        /// preserved within a set of quotes.
        /// </summary>
        /// <param name="source">
        ///     The source string to parse quoted strings from.
        /// </param>
        /// <param name="quote">The quotation character to use.</param>
        /// <param name="delimiter">The delimiting character to use.</param>
        /// <param name="recogniseCEscapes">
        ///     Whether C escape sequences should be recognised within
        ///     the quoted string segments.
        /// </param>
        /// <returns>A set of quoted strings in the order they occur.</returns>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided string source is null, empty, or
        ///     entirely white-space.
        /// </exception>
        public static IList<string> ParseDelimitedSemiQuotedStrings(
            string source,
            char quote = DOUBLEQUOTE, char delimiter = COMMA,
            bool recogniseCEscapes = true
            )
        {
            if (String.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException(
                    "The provided string is null, empty, or white-space."
                    );
            }

            // Whether we're currently in a quoted section.
            bool quoted = false;
            List<string> parts = new List<string>();
            StringBuilder partBuilder = new StringBuilder();
            for (int i = 0; i < source.Length; i++)
            {
                if (quoted)
                {
                    if (source[i] == quote)
                    {
                        quoted = false;
                    }
                    else if (source[i] == BACKSLASH)
                    {
                        if (i + 1 > source.Length)
                        {
                            partBuilder.Append(BACKSLASH);
                        }
                        else if (C_ESCAPES.ContainsKey(source[i + 1]))
                        {
                            partBuilder.Append(C_ESCAPES[source[++i]]);
                        }
                        else if (
                            source[i + 1] == 'x' &&
                            i + 3 < source.Length &&
                            HEX_NUMERICS.Contains(source[i + 2]) &&
                            HEX_NUMERICS.Contains(source[i + 3])
                            )
                        {
                            var hex = source.Substring(i + 2, 2);
                            partBuilder.Append((char)Convert.ToByte(hex, 16));
                            i += 3;
                        }
                        else if (
                            i + 3 < source.Length &&
                            OCT_NUMERICS.Contains(source[i + 1]) &&
                            OCT_NUMERICS.Contains(source[i + 2]) &&
                            OCT_NUMERICS.Contains(source[i + 3])
                            )
                        {
                            var oct = source.Substring(i + 1, 3);
                            partBuilder.Append((char)Convert.ToByte(oct, 8));
                            i += 3;
                        }
                        else
                        {
                            partBuilder.Append(source[i]);
                        }
                    }
                    else
                    {
                        partBuilder.Append(source[i]);
                    }
                }
                else
                {
                    if (source[i] == SPACE)
                    {
                        continue;
                    }
                    else if (source[i] == delimiter)
                    {
                        parts.Add(partBuilder.ToString());
                        partBuilder.Clear();
                    }
                    else if (source[i] == quote)
                    {
                        quoted = true;
                    }
                    else
                    {
                        partBuilder.Append(source[i]);
                    }
                }

            }

            if (partBuilder.Length > 0)
                parts.Add(partBuilder.ToString());

            return parts;
        }

        /// <summary>
        /// Gets the message associated with the status code.
        /// </summary>
        /// <param name="status">The status code to get the message for.</param>
        /// <returns>The status code's associated message.</returns>
        public static string GetMessage(this HttpStatus status)
        {
            string msg;
            if (HttpStatusMessages.TryGetValue(status, out msg))
            {
                return msg;
            }
            else
            {
                return HTTP_STATUSMSG_DEFAULT;
            }
        }
        /// <summary>
        /// Gets the numeric code associated with the HTTP status.
        /// </summary>
        /// <param name="status">The status to get the code for.</param>
        /// <returns>An integer with the status code's value.</returns>
        public static int GetCode(this HttpStatus status)
        {
            return (int)status;
        }

        /// <summary>
        /// Authenticates the client using HTTP Basic authentication.
        /// If authentication fails, responds to the client requesting
        /// credentials and closes the response.
        /// </summary>
        /// <param name="response">
        ///     The HttpResponse to authenticate.
        /// </param>
        /// <param name="username">
        ///     The username to be required for successful authentication.
        /// </param>
        /// <param name="password">
        ///     The password to be required for successful authentication.
        /// </param>
        /// <param name="realm">
        ///     The name of the protection space these credentials are
        ///     used in.
        /// </param>
        /// <returns>
        ///     True if the authentication was successful. If the method
        ///     returns False, the HttpResponse has been closed.
        /// </returns>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the method is called after the headers or
        ///     sent, or when it is called after the connection has been
        ///     closed.
        /// </exception>
        /// <exception cref="McSherry.Zener.Net.HttpRequestException">
        ///     Thrown when the client sents invalid Base64 in its
        ///     Authorization header.
        /// </exception>
        public static bool BasicAuthenticate(
            this HttpResponse response,
            string username, string password,
            string realm = null
            )
        {
            response.CheckClosed();
            response.CheckHeadersSent();

            response.BufferOutput = true;

            var hdr = response.Request.Headers[HDR_CLAUTH].DefaultIfEmpty(null).First();
            // If hdr is null, it means that the client hasn't sent an
            // Authorization header. This means we now need to respond with
            // a 401 and request authorization.
            if (hdr == null)
            {
                if (String.IsNullOrEmpty(realm))
                {
                    realm = response.Request.Path;
                }

                // Remove any characters that cannot be present within
                // the realm field before it is added to the header.
                realm = new string(realm
                    .Where(c => !HTTP_SVBASIC_RLMINV.Contains(c))
                    .ToArray()
                    );

                response.StatusCode = HttpStatus.Unauthorized;
                response.Headers.Add(
                    fieldName: HDR_SVAUTH,
                    fieldValue: String.Format(HDRF_SVAUTH_BASIC, realm)
                    );

                // We've sent out WWW-Authentication header, and we don't
                // want user code to be able to send any data. Close the
                // response.
                response.Close();
                // Authentication of the client did not succeed. Return false.
                return false;
            }
            else
            {
                if (!hdr.Value.StartsWith(HDRF_CLAUTH_STBSC))
                {
                    // If the Authorization header doesn't start
                    // with the string "Basic ", it is invalid. We
                    // remove the Authorization header and recurse.
                    // This results in the above branch being executed
                    // and the sending of a WWW-Authenticate header to
                    // the client.
                    response.Request.Headers.IsReadOnly = false;
                    response.Request.Headers.Remove(hdr.Field);
                    response.Request.Headers.IsReadOnly = true;

                    response.BasicAuthenticate(username, password, realm);
                }

                // The remainder of the Authorization header should be the
                // Base64 string containing the user's authentication details.
                string authStr = hdr.Value.Substring(HDRF_CLAUTH_STBSC.Length);

                // We'll check that the string is valid Base64. This lets us provide
                // better error messages: 400 for invalid data, 401 for invalid
                // credentials.
                try
                {
                    Rfc2045.Base64Decode(authStr);
                }
                catch (ArgumentException aex)
                {
                    throw new HttpRequestException(
                        response.Request,
                        "The client's Authorization header contains an invalid Base64 string.",
                        aex
                        );
                }
                catch (InvalidOperationException ioex)
                {
                    throw new HttpRequestException(
                        response.Request,
                        "The client's Authorization header contains an invalid Base64 string.",
                        ioex
                        );
                }

                string creds = Rfc2045.Base64Encode(String.Format("{0}:{1}", username, password));
                if (!creds.Equals(authStr))
                {
                    // If the Authorization header doesn't contain
                    // valid credentials, it cannot authorise the client.
                    // We remove the Authorization header and recurse.
                    // This results in the above branch being executed
                    // and the sending of a WWW-Authenticate header to
                    // the client.
                    response.Request.Headers.IsReadOnly = false;
                    response.Request.Headers.Remove(hdr.Field);
                    response.Request.Headers.IsReadOnly = true;

                    response.BasicAuthenticate(username, password, realm);
                }
            }

            // Authentication of the client succeeded. Return true.
            return true;
        }
    }
}
