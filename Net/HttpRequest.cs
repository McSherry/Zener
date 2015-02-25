/*
 *      Copyright (c) 2014-2015, Liam McSherry
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;

using MediaType = McSherry.Zener.Core.MediaType;

namespace McSherry.Zener.Net
{
    /// <summary>
    /// The exception thrown when there is an error with an HTTP
    /// request.
    /// </summary>
    public sealed class HttpRequestException : HttpException
    {
        /// <summary>
        /// Creates a new HttpRequestException.
        /// </summary>
        public HttpRequestException()
            : base(HttpStatus.BadRequest)
        {

        }
        /// <summary>
        /// Creates a new HttpRequestException.
        /// </summary>
        /// <param name="message">The message to send with the exception.</param>
        public HttpRequestException(string message)
            : base(HttpStatus.BadRequest, message)
        {

        }
        /// <summary>
        /// Creates a new HttpRequestException.
        /// </summary>
        /// <param name="message">The message to send with the exception.</param>
        /// <param name="innerException">The exception that is the cause of this exception.</param>
        public HttpRequestException(string message, Exception innerException)
            : base(HttpStatus.BadRequest, message, innerException)
        {

        }
    }

    /// <summary>
    /// A class encapsulating an HTTP request from the client.
    /// </summary>
    public class HttpRequest
    {
        /// <summary>
        /// A class containing HTTP method constants.
        /// </summary>
        public static class Methods
        {
            /// <summary>
            /// The OPTIONS HTTP request method.
            /// </summary>
            public const string OPTIONS     = "OPTIONS";
            /// <summary>
            /// The GET HTTP request method.
            /// </summary>
            public const string GET         = "GET";
            /// <summary>
            /// The HEAD HTTP request method.
            /// </summary>
            public const string HEAD        = "HEAD";
            /// <summary>
            /// The POST HTTP request method.
            /// </summary>
            public const string POST        = "POST";
            /// <summary>
            /// The PUT HTTP request method.
            /// </summary>
            public const string PUT         = "PUT";
            /// <summary>
            /// The DELETE HTTP request method.
            /// </summary>
            public const string DELETE      = "DELETE";
            /// <summary>
            /// The TRACE HTTP request method.
            /// </summary>
            public const string TRACE       = "TRACE";
            /// <summary>
            /// The CONNECT HTTP request method.
            /// </summary>
            public const string CONNECT     = "CONNECT";
        }

        /// <summary>
        /// The handler used to handle the data sent with POST requests.
        /// </summary>
        /// <param name="request">
        /// The request with the POST data in it.
        /// </param>
        /// <param name="body">
        /// The request body containing the data sent in the POST request.
        /// </param>
        /// <returns>
        /// If the POST data is meaningful, an ExpandoObject containing any
        /// key-value pairs. Otherwise, an Empty.
        /// </returns>
        private delegate dynamic PostDataHandler(HttpRequest request, Stream body);

        private static readonly MediaType MT_FORMURLENCODED = "application/x-www-form-urlencoded";
        private static readonly MediaType MT_FORMMULTIPART = "multipart/form-data";
        private const string HDR_CDISPOSITION = "Content-Disposition";
        private const string HDR_CLENGTH = "Content-Length";
        private const string HDR_COOKIES = "Cookie";
        private const string CDIS_FORMDATA = "form-data";
        private const string HDR_CTYPE = "Content-Type";
        private const string HDR_CTYPE_KCHAR = "charset";
        private const string HDR_CTYPE_BOUNDARY = "boundary";
        private const string VAR_WHITELIST = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
        private const string VAR_NOSTART = "0123456789";
        private const int REQUEST_MAXLENGTH = (1024 * 1024) * 32; // 32 MiB
        private const int REQUEST_TIMEOUT = 1000 * 60; // 60 seconds

        private static readonly Dictionary<string, Encoding> _encodersByName
            = new Dictionary<string, Encoding>()
            {
                { "ascii",          Encoding.ASCII                      },
                { "us-ascii",       Encoding.ASCII                      },
                { "utf-8",          Encoding.UTF8                       },
                { "utf8",           Encoding.UTF8                       },
                { "iso-8859-1",     Encoding.GetEncoding("ISO-8859-1")  },
                { "latin-1",        Encoding.GetEncoding("ISO-8859-1")  },
                { "windows-1252",   Encoding.GetEncoding(1252)          },
                { "cp-1252",        Encoding.GetEncoding(1252)          }
            };
        private static readonly Dictionary<MediaType, PostDataHandler> _postHandlers
            = new Dictionary<MediaType, PostDataHandler>()
            {
                { "multipart/form-data",                ParseMultipartFormData  },
                { "application/x-www-form-urlencoded",  ParseFormUrlEncoded     }
            };

        /// <summary>
        /// Filters disallowed characters from a string.
        /// </summary>
        /// <param name="str">The string to filter.</param>
        /// <returns>The provided string, filtered to remove disallowed characters.</returns>
        internal static string FilterInvalidCharacters(string str)
        {
            return str
                .TrimStart(VAR_NOSTART.ToCharArray())
                .Where(c => VAR_WHITELIST.Contains(c))
                .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c))
                .ToString();
        }        

        /// <summary>
        /// Determines which POST data handler to use based on the
        /// "Content-Type" header of an HTTP request.
        /// </summary>
        /// <param name="request">The request to determine the handler for.</param>
        /// <returns>The appropriate handler to use.</returns>
        /// <exception cref="McSherry.Zener.Net.HttpRequestException">
        /// Thrown when the client's "Content-Length" header contains an
        /// invalid media type.
        /// </exception>
        private static PostDataHandler DeterminePostHandler(HttpRequest request)
        {
            PostDataHandler handler;
            // Attempt to retrieve the request's 'Content-Type' header.
            var ctypeHdr = request.Headers[HDR_CTYPE].LastOrDefault();
            // If this is true, the request doesn't have a 'Content-Type'
            // header.
            if (ctypeHdr == default(HttpHeader))
            {
                // There's no 'Content-Type', so we can't determine how
                // to parse the request body, and so we can't return a
                // dynamic containing any key-values. Set the handler to
                // one which returns empty.
                handler = (r, s) => new Empty();
            }
            else
            {
                MediaType ctype;
                try
                {
                    ctype = ctypeHdr.Value;
                }
                catch (ArgumentException aex)
                {
                    throw new HttpRequestException(
                        "The client sent an invalid \"Content-Length\" header.",
                        aex
                        );
                }

                handler = _postHandlers
                    // Filter out all the media types that aren't
                    // equivalent to the one the client sent.
                    .Where(k => k.Key.IsEquivalent(ctype))
                    // Select from the matches the handler.
                    .Select(k => k.Value)
                    // If there are no matches, make sure there's
                    // a default one returning empty.
                    .DefaultIfEmpty((r, s) => new Empty())
                    // Select the first result in the enumerable.
                    .First();
            }

            return handler;
        }
        /// <summary>
        /// Parses the provided string, assuming that it is in the
        /// application/x-www-formurlencoded format.
        /// </summary>
        /// <param name="req">The HTTP request that we are to parse.</param>
        /// <param name="body">The string to parse.</param>
        private static dynamic ParseFormUrlEncoded(HttpRequest req, Stream body)
        {
            string formatBody;
            // We don't need anything special from the request body, so we can
            // just read it to the end as a string.
            using (var sr = new StreamReader(body)) formatBody = sr.ReadToEnd();

            var dynObj = new ExpandoObject() as IDictionary<string, object>;

            var qBuilder = new StringBuilder();

            string section = String.Empty;
            bool inVal = false;

            foreach (char c in formatBody)
            {
                if (!inVal && c == '=')
                {
                    inVal = true;
                    section = FilterInvalidCharacters(
                        qBuilder.ToString().UrlDecode(xformsSpaces: true)
                        );
                    qBuilder.Clear();
                }
                else if (inVal && c == '&')
                {
                    dynObj[section] = qBuilder.ToString().UrlDecode(xformsSpaces: true);
                    qBuilder.Clear();
                    inVal = false;
                }
                else if (!inVal && c == '&')
                {
                    section = FilterInvalidCharacters(
                        qBuilder.ToString().UrlDecode(xformsSpaces: true)
                        );
                    dynObj[section] = String.Empty;
                    qBuilder.Clear();
                }
                else
                {
                    qBuilder.Append(c);
                }
            }

            if (!inVal)
            {
                section = FilterInvalidCharacters(
                    qBuilder.ToString().UrlDecode(xformsSpaces: true)
                    );

                dynObj[section] = String.Empty;
            }
            else dynObj[section] = qBuilder.ToString().UrlDecode(xformsSpaces: true);

            return dynObj;
        }
        /// <summary>
        /// Parses the HTTP request body, assuming that it is in the
        /// multipart/form-data format.
        /// </summary>
        /// <exception cref="McSherry.Zener.Net.HttpRequestException">
        ///     Thrown when the client's HTTP request was invalid and
        ///     could not be parsed.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided stream did not support the
        ///     required operations.
        /// </exception>
        private static dynamic ParseMultipartFormData(HttpRequest req, Stream body)
        {
            if (!body.CanRead || !body.CanSeek)
                throw new ArgumentException
                ("The provided stream must support reading and seeking.", "formatBody");

            MediaType mt;
            try
            {
                mt = req.Headers[HDR_CTYPE].Last().Value;
            }
            catch (ArgumentException aex)
            {
                throw new HttpRequestException(
                    "The client sent an invalid \"Content-Length\" header.",
                    aex
                    );
            }

            string boundary;
            if (!mt.Parameters.TryGetValue(HDR_CTYPE_BOUNDARY, out boundary))
            {
                throw new HttpRequestException(
                    "The client did not provide a boundary with its multipart data."
                    );
            }


            var dynObj = new ExpandoObject() as IDictionary<string, object>;
            byte[] boundaryBytes = Encoding.ASCII.GetBytes(boundary);
            byte[] doubleDash = Encoding.ASCII.GetBytes("--");

            // We should ignore data before the first boundary.
            body.ReadUntilFound(boundary, Encoding.ASCII, b => { });
            // Seek past CRLF
            body.Seek(2, SeekOrigin.Current);
            boundary = String.Format("\r\n--{0}", boundary);

            while (body.Position != body.Length)
            {
                // We know headers are going to be ASCII, so we can read lines
                // with our ASCIIEncoding until we hit an empty line.
                StringBuilder partHdrBuilder = new StringBuilder();
                while (true)
                {
                    string line = body.ReadAsciiLine();
                    // An empty line indicates the break between the part
                    // headers and the part body. If we get one when reading
                    // headers, we can safely assume that no more headers are
                    // associated with this part.
                    if (String.IsNullOrEmpty(line)) break;

                    if (!line.Equals(boundary))
                        partHdrBuilder.AppendLine(line);
                }

                HttpHeaderCollection partHeaders;
                using (StringReader sr = new StringReader(partHdrBuilder.ToString()))
                {
                    partHeaders = new HttpHeaderCollection(
                        HttpHeader.ParseMany(sr)
                        );
                }

                if (!partHeaders.Contains(HDR_CDISPOSITION))
                {
                    throw new HttpRequestException(
                        "Multipart data is malformed; no Content-Disposition."
                        );
                }
                var cdis = new NamedParametersHttpHeader(partHeaders[HDR_CDISPOSITION].Last());

                string name = cdis.Pairs
                    .Where(p => p.Key.Equals("name", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Value)
                    .DefaultIfEmpty(null)
                    .First();
                if (name == null)
                    throw new HttpRequestException(
                        "Multipart form data is malformed; no name."
                        );

                long remLeng = body.Length - body.Position;

                // If the remaining number of bytes is less than the length of the
                // boundary (plus 2, for the trailing --), then the body is malformed.
                if (boundaryBytes.Length + 2 > remLeng)
                {
                    throw new HttpRequestException(
                        "Multi-part form data is malformed."
                        );
                }

                // Read the contents of this part.
                List<byte> buffer = new List<byte>();
                body.ReadUntilFound(boundary, Encoding.ASCII, buffer.Add);
                // Seek past CRLF
                body.Seek(2, SeekOrigin.Current);

                Encoding encoding = null;
                var cType = partHeaders[HDR_CTYPE].LastOrDefault();
                if (cType != default(HttpHeader))
                {
                    MediaType cTypeVal;
                    try
                    {
                        cTypeVal = cType.Value;
                    }
                    catch (ArgumentException aex)
                    {
                        throw new HttpRequestException(
                            "The client sent an invalid \"Content-Type\" header.",
                            aex
                            );
                    }

                    // If the media type of the content is in the text/* group,
                    // we'll need to handle it specially (by selecting the correct
                    // encoding).
                    if (cTypeVal.SuperType == "text")
                    {
                        if (cTypeVal.Parameters.ContainsKey(HDR_CTYPE_KCHAR))
                        {
                            var encName = cTypeVal.Parameters[HDR_CTYPE_KCHAR].ToLower();

                            if (_encodersByName.ContainsKey(encName))
                            {
                                encoding = _encodersByName[encName];
                            }
                            else
                            {
                                encoding = Encoding.ASCII;
                            }
                        }
                        else
                        {
                            encoding = Encoding.ASCII;
                        }
                    }
                }
                else
                {
                    encoding = Encoding.ASCII;
                }

                // If encoding is null, we know that the
                // data in the part wasn't transferred with a
                // text/* media type, so we can just treat it
                // as a byte array.
                if (encoding == null)
                {
                    dynObj[name] = buffer.ToArray();
                }
                // If not, we know it's text, and we'll using the
                // encoding we determined earlier to convert the
                // bytes of the body to a string.
                else
                {
                    dynObj[name] = encoding.GetString(buffer.ToArray());
                }

                // The end of the multipart form data is indicated by
                // the boundary, followed by two dashes (--). If there
                // are only two remaining bytes, we can assume it will
                // be the dashes.
                byte[] next = new byte[2];
                int res = body.Read(next, 0, next.Length);
                if (res == 0 ||
                    body.Length <= body.Position ||
                    next.SequenceEqual(doubleDash)) break;
                else body.Seek(-2, SeekOrigin.Current);
            }

            // If we added anything to dynObj, return it. Else,
            // return an Empty to indicate that there is no data.
            return dynObj.Count == 0 ? (dynamic)new Empty() : (dynamic)dynObj;
        }

        private HttpHeaderCollection _headers;
        private dynamic _get, _post, _cookies;
        private byte[] _raw;

        /// <summary>
        /// Parses the HTTP Request Line and sets the appropriate properties
        /// based on its values.
        /// </summary>
        /// <param name="requestLine">The HTTP request's request line.</param>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the HTTP request line is invalid or
        ///     is malformed.
        /// </exception>
        private void SetPropertiesFromRequestLine(string requestLine)
        {
            List<string> parts = new List<string>();
            StringBuilder partBuilder = new StringBuilder();
            // Per RFC 7230 s3.5p3, a request line's parts may
            // be separated by tabs (vertical or horizontal), spaces,
            // high bytes (0xFF), or carriage returns.
            foreach (char c in requestLine)
            {
                if (
                    c == ' ' ||
                    c == '\t' ||
                    c == '\v' ||
                    c == '\r' ||
                    c == 0xFF
                    )
                {
                    parts.Add(partBuilder.ToString());
                    partBuilder.Clear();
                }
                else
                {
                    partBuilder.Append(c);
                }
            }
            // The very last part won't be added to the list
            // as there is no delimiting whitespace. This means
            // we need to include this extra call to Add to
            // ensure correct parsing.
            parts.Add(partBuilder.ToString());
            // We want to make sure that any empty parts
            // are removed. Doing this means we can still
            // loosely check validity based on the number
            // of parts.
            parts.RemoveAll(String.IsNullOrEmpty);
            // We'll be reusing the StringBuilder further
            // down, since we may as well save on instantiating
            // another one.
            partBuilder.Clear();

            if (parts.Count != 3)
            {
                throw new InvalidDataException(
                    "The HTTP request line is malformed."
                    );
            }

            int strIndex = 0;
            bool hasQueryString = false;
            foreach (char c in parts[1])
            {
                if (c == '?')
                {
                    hasQueryString = true;
                    break;
                }
                partBuilder.Append(c);
                ++strIndex;
            }
            // Cut off any trailing forward-slashes.
            if (partBuilder.Length > 1 && partBuilder[partBuilder.Length - 1] == '/')
            {
                partBuilder.Remove(partBuilder.Length - 1, 1);
            }
            this.Path = partBuilder.ToString();

            // We increment the index so that we're ahead of any
            // question mark indicating the start of the query string.
            // If we're at the end of the string, this will evaluate
            // to false.
            if (hasQueryString && ++strIndex < requestLine.Length)
            {
                using (var ms = new MemoryStream(
                    Encoding.ASCII.GetBytes(parts[1].Substring(strIndex))
                    ))
                {
                    this.GET = ParseFormUrlEncoded(this, ms);
                }
            }
            else
            {
                this.GET = new Empty();
            }

            this.HttpVersion = parts[2];
            this.Method = parts[0].ToUpper();
        }
        /// <summary>
        /// Interprets the contents of the request body and sets
        /// any properties appropriately.
        /// </summary>
        /// <param name="body">A stream containing the request's body.</param>
        /// <exception cref="McSherry.Zener.Net.HttpRequestException">
        ///     Thrown when the data contained within the request's
        ///     body is malformed.
        /// </exception>
        private void InterpretRequestBody(Stream body)
        {
            if (body.CanSeek && body.Length > 0)
            {
                _raw = new byte[body.Length];
                body.Position = 0;
                body.Read(_raw, 0, _raw.Length);
                body.Position = 0;
            }
            else
            {
                _raw = new byte[0];
            }

            // Determine how to handle the POST data, then call the
            // appropriate handler. Set the POST property to the
            // result.
            _post = DeterminePostHandler(this)(this, body);
        }
        /// <summary>
        /// Sets the current instance's Cookie property.
        /// </summary>
        private void SetCookiesFromHeaders()
        {
            if (this.Headers.Contains(HDR_COOKIES))
            {
                var dynObj = new ExpandoObject() as IDictionary<string, object>;

                this.Headers[HDR_COOKIES]
                    .Select(h => NameValueHttpHeader.ParsePairs(h.Value))
                    .SelectMany(d => d)
                    .ToList()
                    .ForEach(
                        nvp => dynObj.Add(
                            FilterInvalidCharacters(nvp.Key),
                            nvp.Value.UrlDecode()
                        ));

                _cookies = dynObj;
            }
            else
            {
                _cookies = new Empty();
            }
        }

        /* If the state of the HttpRequest allows, a
         * HttpRequestException will be thrown. This
         * exception will minimally provide the requested
         * path, the request method, and any query-string
         * variables.
         * 
         * If the request's request line cannot be parsed,
         * an InvalidDataException will be thrown instead.
         * This exception indicates that it is not possible
         * to glean meaningful information from the request.
         */

        /// <summary>
        /// Creates a new HttpRequest from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the raw request.</param>
        /// <returns>An HttpRequest equivalent to the provided stream.</returns>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided stream is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided stream does not support the
        ///     required operations.
        /// </exception>
        /// <exception cref="McSherry.Zener.Net.HttpRequestException">
        ///     Thrown when the request is malformed and cannot be parsed.
        /// </exception>
        /// <exception cref="McSherry.Zener.Net.HttpLengthRequiredException">
        ///     Thrown when the request does not include a Content-Length header.
        /// </exception>
        public static HttpRequest Create(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(
                    "The provided stream cannot be null."
                    );

            if (!stream.CanRead)
                throw new ArgumentException(
                    "The provided stream does not support reading.",
                    "stream"
                    );

            HttpRequest request = new HttpRequest();

            string line;
            // Lines before the request line can be blank.
            // We want to skip these since there's nothing
            // to parse.
            do { line = stream.ReadAsciiLine(); }
            while (String.IsNullOrEmpty(line));

            // We've now hit the first line with content. In
            // a compliant HTTP request, this is the request
            // line.
            request.SetPropertiesFromRequestLine(line);
            // Move past the request line in to what is likely
            // to be the first HTTP header in the request.
            line = stream.ReadAsciiLine();

            StringBuilder headerBuilder = new StringBuilder();
            // Now that we have the start of the header section,
            // we need to keep reading lines until we find a blank
            // one, which indicates the end of the header section.
            while (!String.IsNullOrEmpty(line))
            {
                headerBuilder.AppendLine(line);
                line = stream.ReadAsciiLine();
            }

            // We now have all the HTTP headers in the request.
            // To determine the content length, which we need for
            // reading the rest of the request, we need to parse
            // the headers.
            HttpHeaderCollection headers;
            try
            {
                using (StringReader sr = new StringReader(headerBuilder.ToString()))
                {
                    headers = new HttpHeaderCollection(HttpHeader.ParseMany(sr));
                }
            }
            catch (ArgumentException aex)
            {
                throw new HttpRequestException(
                    "Could not parse HTTP headers.", aex
                    );
            }


            // If there isn't a Content-Length header, we can't
            // know how much data we have to wait for. This means
            // that we can't know if there is a request body or
            // not.
            //
            // To ensure maximum functionality (some browsers
            // don't send Content-Length when there is no body),
            // we will assume that, when no Content-Length header
            // is present, there is no request body. Only the
            // request line and headers will be passed to the
            // request handler.
            using (MemoryStream ms = new MemoryStream())
            {
                if (headers.Contains(HDR_CLENGTH))
                {
                    var cLen = headers[HDR_CLENGTH].Last();

                    Int32 cLenOctets;
                    // Make sure that the value of the Content-Length
                    // header is a valid integer.
                    if (!Int32.TryParse(cLen.Value, out cLenOctets))
                    {
                        throw new HttpRequestException(
                            "Invalid Content-Length header (non-integral value)."
                            );
                    }

                    // The Content-Length cannot be negative.
                    if (cLenOctets < 0)
                    {
                        throw new HttpRequestException(
                            "Invalid Content-Length header (negative value)."
                            );
                    }

                    // Make sure the Content-Length isn't longer
                    // than our maximum length.
                    if (cLenOctets > REQUEST_MAXLENGTH)
                    {
                        throw new HttpException(
                            HttpStatus.RequestEntityTooLarge,
                            "The request body was too large."
                            );
                    }

                    // Read the bytes from the network.
                    byte[] bodyBytes = new byte[cLenOctets];

                    // If the stream is a NetworkStream, we have to handle it
                    // differently. NetworkStreams lie, and if you attempt to
                    // read more data than is currently available in the network
                    // stack's buffer, it won't block unless we set a time-out.
                    //
                    // If no time-out is set, the NetworkStream will return what
                    // it has, padded with null bytes to make up the length.
                    if (stream is System.Net.Sockets.NetworkStream)
                    {
                        ((System.Net.Sockets.NetworkStream)stream).ReadTimeout = REQUEST_TIMEOUT;

                        //while (index < bodyBytes.Length)
                        //{
                        //    if (ns.DataAvailable)
                        //    {
                        //        bodyBytes[index++] = (byte)stream.ReadByte();
                        //    }
                        //}
                    }

                    int totalRead = 0;
                    while (totalRead != bodyBytes.Length)
                    {
                        totalRead += stream.Read(
                            bodyBytes, 
                            totalRead,
                            bodyBytes.Length - totalRead
                            );
                    }

                    ms.Write(bodyBytes, 0, bodyBytes.Length);
                    ms.Position = 0;
                }

                request.Headers = headers;
                request.InterpretRequestBody(ms);
                request.SetCookiesFromHeaders();
            }

            return request;
        }

        /// <summary>
        /// Create a new HttpRequest to be manually initialised.
        /// </summary>
        private HttpRequest()
        {

        }

        /// <summary>
        /// The HTTP method/verb used with the request.
        /// </summary>
        public string Method
        {
            get;
            private set;
        }
        /// <summary>
        /// The path requested by the HTTP user agent, sans any query
        /// string parameters.
        /// </summary>
        public string Path
        {
            get;
            private set;
        }
        /// <summary>
        /// The HTTP version requested by the user agent.
        /// </summary>
        public string HttpVersion
        {
            get;
            private set;
        }
        /// <summary>
        /// The HTTP headers sent with the request.
        /// </summary>
        public HttpHeaderCollection Headers
        {
            get { return _headers; }
            private set { _headers = value; }
        }
        /// <summary>
        /// All POST parameters sent with the request.
        /// </summary>
        public dynamic POST
        {
            get { return _post; }
        }
        /// <summary>
        /// All GET (query string) parameters send with the request.
        /// </summary>
        public dynamic GET
        {
            get { return _get; }
            private set { _get = value; }
        }
        /// <summary>
        /// Any cookies sent with the request.
        /// </summary>
        public dynamic Cookies
        {
            get { return _cookies; }
        }
        /// <summary>
        /// The raw bytes of the client's request.
        /// </summary>
        public byte[] Raw
        {
            get { return _raw; }
        }
    }
}
