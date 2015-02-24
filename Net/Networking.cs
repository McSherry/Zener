﻿/*
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

using McSherry.Zener.Core;

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
            HDR_ACCEPT          = "Accept",

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
        #region URL-encoding constants
        // Bytes that we won't touch when percent-encoding.
        private static readonly HashSet<byte> NoUrlEncodeBytes
            = new HashSet<byte>(
                Encoding.UTF8.GetBytes(
                    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~"
                ));
        // Characters that are allowed within a percent-encoded
        // string unencoded. These are reserved characters used within
        // URI/URLs as delimiters.
        private static readonly HashSet<byte> UrlEncodeReserved
            = new HashSet<byte>(
                Encoding.UTF8.GetBytes(
                    ":/?#[]@!$&'()*+,;="
                ));
        // Bytes that we'll consider as valid hexadecimal characters.
        private static readonly HashSet<byte> UrlEncodeHexChars
            = new HashSet<byte>(
                Encoding.UTF8.GetBytes(
                    "0123456789ABCDEFabcdef"
                ));

        private const byte
            PctEncodingStart    = (byte)'%',
            XFormsSpace         = (byte)'+',
            SpaceCharacter      = (byte)' '
            ;
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
        public static List<string> ParseDelimitedSemiQuotedStrings(
            string source,
            char quote = DOUBLEQUOTE, char delimiter = COMMA,
            bool recogniseCEscapes = true
            )
        {
            if (source == null)
            {
                throw new ArgumentException(
                    "The provided source string must not be null."
                    );
            }

            // If the source string is empty, there's no point in
            // checking it.
            if (String.IsNullOrWhiteSpace(source))
            {
                return new List<string>(0);
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
        /// Parses a set of key-value pairs where quoted strings are
        /// not taken in to account.
        /// </summary>
        /// <param name="source">
        /// The source string containing the key-value pairs.
        /// </param>
        /// <param name="kvDelimiter">
        /// The character that separates each key-value pair.
        /// </param>
        /// <param name="keySeparator">
        /// The character that separates the key from the value
        /// in a key-value pair.
        /// </param>
        /// <param name="validKeyCharacters">
        /// The characters that are considered valid within a key.
        /// Set this to null to allow any characters.
        /// </param>
        /// <param name="validValueCharacters">
        /// The characters that are considered valid within a value.
        /// Set this to null to allow any characters.
        /// </param>
        /// <returns>
        /// A dictionary containing all parsed key-value pairs.
        /// </returns>
        /// <remarks>
        /// Any key-value pairs without a value will have their
        /// values set to null.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided source string is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided source string is invalid.
        /// </exception>
        public static IndexedDictionary<string, string> ParseUnquotedKeyValues(
            string source,
            char kvDelimiter = ';', char keySeparator = '=',
            HashSet<char> validKeyCharacters = null,
            HashSet<char> validValueCharacters = null
            )
        {
            if (source == null)
            {
                throw new ArgumentNullException(
                    "The provided source string must not be null."
                    );
            }

            // If the string is only white-space, we can't parse
            // anything in it.
            if (String.IsNullOrWhiteSpace(source))
            {
                return new IndexedDictionary<string, string>().AsReadOnly();
            }

            source = source.Trim();

            var ret = new IndexedDictionary<string, string>();
            bool inKey = true,
                hasKeyValidSet = validKeyCharacters != null,
                hasValueValidSet = validValueCharacters != null;
            string tempKey = String.Empty;
            StringBuilder storage = new StringBuilder();
            for (int i = 0; i < source.Length; i++)
            {
                // Check to see whether we're in a key.
                if (inKey)
                {
                    // If we hit a key separator, we need
                    // to switch to parsing a value.
                    if (source[i] == keySeparator)
                    {
                        // If we hit a separator and the key is zero-length,
                        // throw an exception.
                        if (storage.Length == 0)
                        {
                            throw new ArgumentException(
                                "The string contains a pair with an empty key."
                                );
                        }

                        // Set the variable to indicate we're no
                        // longer in a key.
                        inKey = false;
                        // Stick the key value in a variable for later.
                        tempKey = storage.ToString();
                        // Clear the string builder.
                        storage.Clear();
                    }
                    // We will accept keys with no values. This
                    // means we need to check for delimiters.
                    else if (source[i] == kvDelimiter)
                    {
                        // We add the key, but set the value
                        // to null.
                        ret[storage.ToString()] = null;
                        // Clear the storage.
                        storage.Clear();
                    }
                    // If the character is white-space, ignore it.
                    else if (Char.IsWhiteSpace(source[i])) continue;
                    // If there is no set of valid characters, we don't
                    // need to do any checks.
                    else if (!hasKeyValidSet)
                    {
                        storage.Append(source[i]);
                    }
                    // If there is a specific set of characters that can be
                    // allowed within a key, check to make sure that this
                    // character is allowable.
                    else if (validKeyCharacters.Contains(source[i]))
                    {
                        // The character is allowable, so we add it to storage.
                        storage.Append(source[i]);
                    }
                    // If the character isn't allowable, throw an
                    // exception.
                    else
                    {
                        throw new ArgumentException(
                            "A key contains an invalid character."
                            );
                    }
                }
                // If we're here, we're parsing a value.
                else
                {
                    // If we hit a delimiter, it means we're at
                    // the end of our key-value pair.
                    if (source[i] == kvDelimiter)
                    {
                        // Add the pair to the dictionary.
                        ret[tempKey] = storage.ToString();
                        // Clear the string builder.
                        storage.Clear();
                        // Set our state back to being in a key.
                        inKey = true;
                    }
                    // If we hit white-space, ignore it.
                    else if (Char.IsWhiteSpace(source[i])) continue;
                    // If there isn't a set of characters to consider
                    // valid, add the character to storage without
                    // performing any checks.
                    else if (!hasValueValidSet)
                    {
                        storage.Append(source[i]);
                    }
                    // If there is a set of valid characters, make
                    // sure the current character is valid.
                    else if (validValueCharacters.Contains(source[i]))
                    {
                        // If it is valid, add it.
                        storage.Append(source[i]);
                    }
                    // If the character isn't within the set of valid
                    // characters, throw an exception.
                    else
                    {
                        throw new ArgumentException(
                            "A value contains an invalid character."
                            );
                    }
                }
            }

            // If there are still characters in storage, we
            // may need to perform further actions.
            if (storage.Length > 0)
            {
                // If we're in a key, there's no value so
                // we add the key with a null value.
                if (inKey) ret[storage.ToString()] = null;
                // If we're not, add both the key and value
                // to the dictionary.
                else ret[tempKey] = storage.ToString();
            }
            // If we're not in a key and there are no characters
            // in the string builder, it means that a key has no
            // associated value.
            else if (!inKey)
            {
                // We'll have a key stored in this variable, so
                // we add it to the dict with a null value.
                ret[tempKey] = null;
            }

            // Return the dictionary.
            return ret;
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
        /// <param name="request">
        ///     The request containing authentication details.
        /// </param>
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
            this HttpRequest request, HttpResponse response,
            string username, string password,
            string realm = null
            )
        {
            response.CheckClosed();
            response.CheckHeadersSent();

            response.BufferOutput = true;

            var hdr = request.Headers[HDR_CLAUTH].DefaultIfEmpty(null).First();
            // If hdr is null, it means that the client hasn't sent an
            // Authorization header. This means we now need to respond with
            // a 401 and request authorization.
            if (hdr == null)
            {
                if (String.IsNullOrEmpty(realm))
                {
                    realm = request.Path;
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
                    request.Headers.IsReadOnly = false;
                    request.Headers.Remove(hdr.Field);
                    request.Headers.IsReadOnly = true;

                    request.BasicAuthenticate(response, username, password, realm);
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
                        "The client's Authorization header contains an invalid Base64 string.",
                        aex
                        );
                }
                catch (InvalidOperationException ioex)
                {
                    throw new HttpRequestException(
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
                    request.Headers.IsReadOnly = false;
                    request.Headers.Remove(hdr.Field);
                    request.Headers.IsReadOnly = true;

                    request.BasicAuthenticate(response, username, password, realm);
                }
            }

            // Authentication of the client succeeded. Return true.
            return true;
        }

        /// <summary>
        /// Determines whether the provided MediaType is acceptable
        /// as a response to the provided HttpRequest.
        /// </summary>
        /// <param name="request">
        /// The request to determine acceptability for.
        /// </param>
        /// <param name="mediaType">
        /// The media type to test against the request.
        /// </param>
        /// <returns>
        /// True if the specified MediaType is acceptable in a
        /// response to the provided request.
        /// </returns>
        /// <remarks>
        /// This method will consider any HttpRequest without an
        /// "Accept" header as accepting any and all media types.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided HttpRequest or MediaType is null.
        /// </exception>
        /// <exception cref="McSherry.Zener.Net.HttpRequestException">
        /// Thrown when the client's "Accept" header is invalid, or
        /// when one or more of the media types in the "Accept" header
        /// is invalid.
        /// </exception>
        public static bool IsAcceptable(
            this HttpRequest request,
            MediaType mediaType
            )
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    "The provided request must not be null."
                    );
            }

            if (mediaType == null)
            {
                throw new ArgumentNullException(
                    "The provided media type must not be null."
                    );
            }

            bool isAcceptable;
            var accHdr = request.Headers[HDR_ACCEPT].FirstOrDefault();

            // If it's the default value, the client hasn't sent an
            // 'Accept' header with its request.
            if (accHdr == default(HttpHeader))
            {
                // We'll consider no 'Accept' header as meaning the
                // client accepts everything.
                isAcceptable = true;
            }
            else
            {
                // The client has sent an 'Accept' header, so we need
                // to parse it. As with 'Accept-Encoding', 'Accept' can
                // have q-values to indicate preference. For this reason,
                // we use the OrderedCsvHttpHeader class.
                OrderedCsvHttpHeader ocsv;
                try
                {
                    // There's no point leaving any unacceptable values
                    // in the collection of items, so we pass 'true' to
                    // remove them.
                    ocsv = new OrderedCsvHttpHeader(accHdr, true);
                }
                catch (ArgumentException aex)
                {
                    // This method is most likely going to be called from
                    // a route handler. This means that we can delegate to
                    // the virtual host's error handler by throwing an
                    // exception inheriting from HttpException.
                    throw new HttpRequestException(
                        "The client's \"Accept\" header is invalid.",
                        aex
                        );
                }

                try
                {
                    // If we're here, the client's 'Accept' header parsed
                    // successfully. We now want to determine whether any
                    // of the media types it specified can be considered
                    // equivalent to the one we were passed.
                    isAcceptable = ocsv.Items.Any(s => mediaType.IsEquivalent(s));
                }
                // As the items are strings, they'll be run through MediaType.Create
                // by the implicit conversion operator on the MediaType class. The
                // method Create throws an ArgumentException if the string is not a
                // valid MediaType.
                catch (ArgumentException aex)
                {
                    throw new HttpRequestException(
                        "One or more of the media types in the client's " +
                        "\"Accept\" header are invalid or malformed.",
                        aex
                        );
                }
            }

            return isAcceptable;
        }
        /// <summary>
        /// Determines whether any of the provided MediaTypes are
        /// acceptable.
        /// </summary>
        /// <param name="request">
        /// The request to determine acceptability for.
        /// </param>
        /// <param name="mediaTypes">
        /// The media types to test against the request.
        /// </param>
        /// <returns>
        /// A collection containing any acceptable media types from
        /// those provided.
        /// </returns>
        public static ICollection<MediaType> IsAcceptable(
            this HttpRequest request,
            IEnumerable<MediaType> mediaTypes
            )
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    "The specified request must not be null."
                    );
            }

            if (mediaTypes == null)
            {
                throw new ArgumentNullException(
                    "The specified media types enumerable must not be null."
                    );
            }

            // If the caller hasn't provided any media types to
            // check, there's no point going through all this.
            if (mediaTypes.Count() == 0)
            {
                return new List<MediaType>(0).AsReadOnly();
            }

            ICollection<MediaType> accTypes;
            var accHdr = request.Headers[HDR_ACCEPT].FirstOrDefault();

            // As with our other IsAcceptable method, no 'Accept' header means
            // the client will be considered to accept anything.
            if (accHdr == default(HttpHeader))
            {
                // As the client accepts anything, we can just
                // return the media types that the caller provided.
                accTypes = new List<MediaType>(mediaTypes).AsReadOnly();
            }
            else
            {
                OrderedCsvHttpHeader ocsv;
                try
                {
                    // Pass true to remove any unacceptable types
                    // from the list.
                    ocsv = new OrderedCsvHttpHeader(accHdr, true);
                }
                catch (ArgumentException aex)
                {
                    throw new HttpRequestException(
                        "The client's \"Accept\" header is invalid.",
                        aex
                        );
                }

                accTypes = mediaTypes
                    .Where(m => ocsv.Items.Any(s => m.IsEquivalent(s)))
                    .ToList()
                    .AsReadOnly();
            }

            return accTypes;
        }

        /// <summary>
        /// Converts the string to a URL-safe encoding using
        /// the percent-encoding scheme.
        /// </summary>
        /// <param name="plain">
        /// The string to convert to a percent-encoded form.
        /// </param>
        /// <param name="xformsSpaces">
        /// Whether to use application/x-www-form-urlencoded
        /// spaces. When this is true, spaces are encoded
        /// using the character '+' instead of the
        /// percent-encoded sequence '%20'.
        /// </param>
        /// <returns>
        /// The percent-encoded representation of the source
        /// string.
        /// </returns>
        /// <remarks>
        /// This method assumes that the provided source string
        /// is a UTF-8 string.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided plain-text source string
        /// is null.
        /// </exception>
        public static string UrlEncode(
            this string plain,
            bool xformsSpaces = false
            )
        {
            if (plain == null)
            {
                throw new ArgumentNullException(
                    "The provided plain-text string must not be null."
                    );
            }

            // As we do in UrlDecode, we convert the string to a
            // series of UTF-8 bytes. This is done because .NET strings
            // are, internally, UTF-16.
            byte[] strBytes = Encoding.UTF8.GetBytes(plain);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < strBytes.Length; i++)
            {
                byte b = strBytes[i];
                // We first check to see whether the byte is in
                // the set of bytes that shouldn't be percent-encoded.
                if (NoUrlEncodeBytes.Contains(b))
                {
                    // If we shouldn't encode the byte, we just
                    // append it without change.
                    sb.Append((char)b);
                }
                // If the byte isn't in the set of bytes that shouldn't
                // be encoded, it means we need to encode it.
                //
                // First, we check to see whether application/x-www-form-urlencoded
                // space encoding is enabled, and whether the current byte is a space.
                else if (xformsSpaces && b == SpaceCharacter)
                {
                    // If we're using application/x-www-form-urlencoded space encoding
                    // and the character to encode is a space, append a '+' instead of
                    // '%20' to the StringBuilder.
                    sb.Append((char)XFormsSpace);
                }
                // If it isn't, we need to percent-encode the byte. Percent encoding
                // is very simple. You take the value of the byte as two hexadecimal
                // digits and append them (upper- or lowercase, it doesn't matter),
                // prefixed by a percent sign (%), to the string.
                //
                // For example, the ASCII space character has the decimal value 32. In
                // hexadecimal, this is 20. To encode an ASCII space, we append %20 to
                // the string. Leading zeroes are always included (so the hexadecimal
                // value 5 is always encoded %05, never %5).
                else
                {
                    sb.AppendFormat(
                        "%{0}{1}",
                        // The ToString method passed "X" prints out the value
                        // in hexadecimal. However, it does not return leading
                        // zeroes. To get around this, we just take the high and
                        // low nybbles and call ToString on them separately.
                        ((b & 0xF0) >> 4).ToString("X"),
                        (b & 0xF).ToString("X")
                        );
                }
            }

            return sb.ToString();
        }
        /// <summary>
        /// Converts a percent-encoded (URL-encoded) string in
        /// to a non-URL-safe string.
        /// </summary>
        /// <param name="encoded">
        /// The string to convert from percent-encoded form.
        /// </param>
        /// <param name="xformsSpaces">
        /// Whether to support spaces used with the
        /// application/x-www-form-urlencoded format. When this
        /// is true, any plus (+) characters are decoded to
        /// spaces.
        /// </param>
        /// <param name="strict">
        /// Whether the method should throw an exception
        /// when a non-URL-safe character is present within
        /// the encoded string.
        /// </param>
        /// <returns>
        /// The decoded representation of the provided percent-encoded
        /// string.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided source string is null.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// Thrown when the format of the percent-encoded string is
        /// invalid.
        /// </exception>
        public static string UrlDecode(
            this string encoded,
            bool xformsSpaces = false,
            bool strict = true
            )
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(
                    "The provided source string must not be null."
                    );
            }

            // If the string is whitespace/empty, there's nothing
            // we can decode, so we might as well not try.
            if (String.IsNullOrWhiteSpace(encoded))
            {
                return encoded;
            }

            // We're treating it as UTF-8. Since .NET uses UTF-16 internally,
            // we need to work with the string as a set of bytes.
            var strBytes = Encoding.UTF8.GetBytes(encoded);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < strBytes.Length; i++)
            {
                byte b = strBytes[i];
                // If the byte is the byte that indicates the start of a
                // percent-encoded byte, we need to decode it.
                if (b == PctEncodingStart)
                {
                    #region Handle Pct-Enc'd bytes (%hh)
                    // We need to make sure that there enough
                    // characters to make up a percent-encoded byte.
                    if (i + 2 < strBytes.Length)
                    {
                        byte hNybble = strBytes[i + 1],
                            lNybble = strBytes[i + 2];

                        // We need to check to make sure that both of
                        // the bytes following the percent sign are
                        // valid hex digits.
                        if (
                            UrlEncodeHexChars.Contains(hNybble) &&
                            UrlEncodeHexChars.Contains(lNybble)
                            )
                        {
                            // We need to join together the higher and lower
                            // nybbles to form a single byte that will be our
                            // character.
                            sb.Append(
                                (char)(
                                    // Convert the nybble to a character. This gives us the
                                    // hex digit to convert to a byte. We then shift this byte
                                    // 4 bits left, as the single hex digit represents the
                                    // four higher bits of the byte.
                                    (Convert.ToByte(((char)hNybble).ToString(), 16) << 4) |
                                    Convert.ToByte(((char)lNybble).ToString(), 16)
                                ));
                            // Advance past the two characters we've just interpreted
                            // as a percent-encoded byte.
                            i += 2;
                        }
                        // If strict parsing is enabled and the characters are not
                        // valid hex digits, we'll throw an exception.
                        else if (strict)
                        {
                            throw new FormatException(
                                "A percent-encoded byte within the string contains " +
                                "non-hexadecimal characters."
                                );
                        }
                        // If strict parsing is disabled, we append the percent character
                        // to the string, treating it as a literal.
                        else
                        {
                            sb.Append((char)b);
                        }
                    }
                    // If we get here, there are too few characters to form a
                    // percent-encoded byte. There are two things we can do.
                    //
                    // 1: If strict parsing is enabled, we can throw an exception
                    //    to indicate that we've come across an invalid sequence.
                    else if (strict)
                    {
                        throw new FormatException(
                            "The string contains a percent character in a location " +
                            "where it cannot prefix two hexadecimal digits."
                            );
                    }
                    // 2: If strict parsing is disabled, append the character to
                    //    the StringBuilder and treat it as a literal character.
                    else
                    {
                        sb.Append((char)b);
                    }
                    #endregion
                }
                // The media type application/x-www-form-urlencoded specifies that
                // the '+' character may be used instead of %20 for spaces. If the
                // caller has enabled support for this in the call, and if the current
                // byte is a '+' character, we'll append a space to the StringBuilder.
                else if (xformsSpaces && b == XFormsSpace)
                {
                    sb.Append(' ');
                }
                // If the character isn't a percent character, we need to
                // determine whether it's one that we're free to add to the
                // StringBuilder.
                //
                // If it is a character we're free to add, or it isn't a
                // character we're free to add but strict parsing is disabled,
                // we can add it to the StringBuilder.
                else if (
                    NoUrlEncodeBytes.Contains(b)  || 
                    UrlEncodeReserved.Contains(b) ||
                    !strict
                    )
                {
                    sb.Append((char)b);
                }
                // If the character is non-URL-safe and strict parsing is
                // enabled, throw an exception.
                else
                {
                    throw new FormatException(
                        "The provided percent-encoded string contains " +
                        "one or more non-URL-safe characters."
                        );
                }
            }

            return sb.ToString();
        }
    }
}
