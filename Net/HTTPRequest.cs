/*
 *      Copyright (c) 2014, SynapLink, LLC
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE.txt, or online: http://opensource.org/licenses/BSD-3-Clause
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

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// A class encapsulating an HTTP request from the client.
    /// </summary>
    public class HttpRequest
    {
        private const string MT_FORMURLENCODED = "application/x-www-form-urlencoded";
        private const string MT_FORMMULTIPART = "multipart/form-data";
        private const string HDR_CDISPOSITION = "Content-Disposition";
        private const string CDIS_FORMDATA = "form-data";
        private const string HDR_CTYPE = "Content-Type";
        private const string HDR_CTYPE_KCHAR = "charset";
        private const string VAR_WHITELIST = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
        private const string VAR_NOSTART = "0123456789";

        private static Dictionary<string, Encoding> _encodersByName
            = new Dictionary<string, Encoding>()
            {
                { "ascii", Encoding.ASCII },
                { "us-ascii", Encoding.ASCII },
                { "utf-8", Encoding.UTF8 },
                { "utf8", Encoding.UTF8 }
            };

        /// <summary>
        /// Filters disallowed characters from a string.
        /// </summary>
        /// <param name="str">The string to filter.</param>
        /// <returns>The provided string, filtered to remove disallowed characters.</returns>
        private static string _filterInvChars(string str)
        {
            return str
                .TrimStart(VAR_NOSTART.ToCharArray())
                .Where(c => VAR_WHITELIST.Contains(c))
                .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c))
                .ToString();
        }

        private HttpHeaderCollection _headers;
        private dynamic _get, _post;
        private byte[] _raw;

        /// <summary>
        /// Parses the HTTP Request Line and sets the appropriate properties
        /// based on its values.
        /// </summary>
        /// <param name="reqestLine"></param>
        private void SetPropertiesFromRequestLine(string requestLine)
        {
            // The sections of the request line can always be split using
            // spaces. It's in the spec, not lazy parsing.
            string[] rlArray = requestLine.Split(' ');

            if (rlArray.Length != 3) throw new HttpRequestException
            ("HTTP request line is malformed.");

            var pathBuilder = new StringBuilder();
            int strIndex = 0;
            bool hasQueryString = false;
            foreach (char c in rlArray[1])
            {
                if (c == '?')
                {
                    hasQueryString = true;
                    break;
                }
                pathBuilder.Append(c);
                ++strIndex;
            }
            // Cut off any trailing forward-slashes.
            if (pathBuilder.Length > 1 && pathBuilder[pathBuilder.Length - 1] == '/')
            {
                pathBuilder.Remove(pathBuilder.Length - 1, 1);
            }
            this.Path = pathBuilder.ToString();

            // We increment the index so that we're ahead of any
            // question mark indicating the start of the query string.
            // If we're at the end of the string, this will evaluate
            // to false.
            if (hasQueryString && ++strIndex < requestLine.Length)
            {
                this.GET = ParseFormUrlEncoded(rlArray[1].Substring(strIndex));
            }
            else
            {
                this.GET = new Empty();
            }

            this.HttpVersion = rlArray[2];
            this.Method = rlArray[0].ToUpper();
        }
        /// <summary>
        /// Parses the provided string, assuming that it is in the
        /// application/x-www-formurlencoded format.
        /// </summary>
        /// <param name="formatBody">The string to parse.</param>
        private static dynamic ParseFormUrlEncoded(string formatBody)
        {
            var dynObj = new ExpandoObject() as IDictionary<string, object>;

            var qBuilder = new StringBuilder();

            string section = String.Empty;
            bool inVal = false;

            foreach (char c in formatBody)
            {
                if (!inVal && c == '=')
                {
                    inVal = true;
                    section = _filterInvChars(
                        WebUtility.UrlDecode(qBuilder.ToString())
                        );
                    qBuilder.Clear();
                }
                else if (inVal && c == '&')
                {
                    dynObj[section] = WebUtility.UrlDecode(qBuilder.ToString());
                    qBuilder.Clear();
                    inVal = false;
                }
                else if (!inVal && c == '&')
                {
                    section = _filterInvChars(
                        WebUtility.UrlDecode(qBuilder.ToString())
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
                section = _filterInvChars(
                    WebUtility.UrlDecode(qBuilder.ToString())
                    );

                dynObj[section] = String.Empty;
            }
            else dynObj[section] = WebUtility.UrlDecode(qBuilder.ToString());
            
            return dynObj;
        }
        /// <summary>
        /// Parses the HTTP request body, assuming that it is in the
        /// multipart/form-data format.
        /// </summary>
        /// <exception cref="SynapLink.Zener.Net.HttpRequestException"></exception>
        private static dynamic ParseMultipartFormData(Stream formatBody, string boundary)
        {
            if (!formatBody.CanRead || !formatBody.CanSeek)
                throw new ArgumentException
                ("The provided stream must support reading and seeking.", "formatBody");

            var dynObj = new ExpandoObject() as IDictionary<string, object>;
            byte[] boundaryBytes = Encoding.ASCII.GetBytes(boundary);
            byte[] doubleDash = Encoding.ASCII.GetBytes("--");

            // We should ignore data before the first boundary.
            ReadUntilFound(formatBody, boundary, Encoding.ASCII, b => { });
            // Seek past CRLF
            formatBody.Seek(2, SeekOrigin.Current);
            boundary = String.Format("\r\n--{0}", boundary);

            while (formatBody.Position != formatBody.Length)
            {
                // We know headers are going to be ASCII, so we can read lines
                // with our ASCIIEncoding until we hit an empty line.
                StringBuilder partHdrBuilder = new StringBuilder();
                while (true)
                {
                    string line = ReadAsciiLine(formatBody);
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
                        BasicHttpHeader.ParseMany(sr)
                        );
                }

                if (!partHeaders.Contains(HDR_CDISPOSITION))
                    throw new HttpRequestException
                    ("Multipart data is malformed; no Content-Disposition.");
                var cdis = new NamedParametersHttpHeader(partHeaders[HDR_CDISPOSITION].Last());

                string name = cdis.Pairs
                    .Where(p => p.Key.Equals("name", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Value)
                    .DefaultIfEmpty(null)
                    .First();
                if (name == null)
                    throw new HttpRequestException
                    ("Multipart form data is malformed; no name.");

                long remLeng = formatBody.Length - formatBody.Position;

                // If the remaining number of bytes is less than the length of the
                // boundary (plus 2, for the trailing --), then the body is malformed.
                if (boundaryBytes.Length + 2 > remLeng)
                {
                    throw new HttpRequestException
                    ("Multi-part form data is malformed.");
                }

                // Read the contents of this part.
                List<byte> buffer = new List<byte>();
                ReadUntilFound(formatBody, boundary, Encoding.ASCII, buffer.Add);
                // Seek past CRLF
                formatBody.Seek(2, SeekOrigin.Current);

                Encoding encoding = null;
                if (partHeaders.Contains(HDR_CTYPE))
                {
                    var cType = partHeaders[HDR_CTYPE].Last();
                    var cTypeVal = cType.Value.ToLower();

                    // If the media type of the content is in the text/* group,
                    // we'll need to handle it specially (by selecting the correct
                    // encoding).
                    if (cTypeVal.StartsWith("text/"))
                    {
                        // If there's a Content-Type header, it may contain
                        // encoding information. To retrieve it, we'll need
                        // to treat it as a name-value header.
                        var nvCtype = new NamedParametersHttpHeader(cType);

                        if (nvCtype.Pairs.ContainsKey(HDR_CTYPE_KCHAR))
                        {
                            var encName = nvCtype.Pairs[HDR_CTYPE_KCHAR].ToLower();

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
                int res = formatBody.Read(next, 0, next.Length);
                if ( res == 0 ||
                    formatBody.Length <= formatBody.Position ||
                    next.SequenceEqual(doubleDash)) break;
                else formatBody.Seek(-2, SeekOrigin.Current);
            }

            // If we added anything to dynObj, return it. Else,
            // return an Empty to indicate that there is no data.
            return dynObj.Count == 0 ? (dynamic)new Empty() : (dynamic)dynObj;
        }
        /// <summary>
        /// Reads a single line from a stream and returns the ASCII-encoded string.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>A single line from the stream, ASCII-encoded.</returns>
        internal static string ReadAsciiLine(Stream stream)
        {
            if (!stream.CanRead)
                throw new ArgumentException
                ("Provided stream cannot be read from.");

            List<byte> buf = new List<byte>();
            ReadUntilFound(stream, "\r\n", Encoding.ASCII, buf.Add);

            return Encoding.ASCII.GetString(buf.ToArray());
        }
        /// <summary>
        /// Reads bytes until the specified boundary is found.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="boundary">The boundary to read to.</param>
        /// <param name="encoding">The encoding of the data and boundary.</param>
        internal static void ReadUntilFound(Stream stream, string boundary, Encoding encoding, Action<byte> readCall)
        {
            byte[] boundaryBytes = encoding.GetBytes(boundary);
            byte[] window = new byte[boundaryBytes.Length];

            //if (stream.Length - stream.Position < boundaryBytes.Length)
            //    throw new InvalidOperationException
            //    ("Too few bytes left in stream to find boundary.");

            stream.Read(window, 0, window.Length);

            while (true)
            {
                // We've reached the boundary!
                if (window.SequenceEqual(boundaryBytes)) break;

                int next = stream.ReadByte();
                // Looks like we've hit the end of the stream.
                // Nothing more to read.
                if (next == -1) break;

                // Return the byte we're about to discard so
                // it can be used by the caller.
                readCall(window[0]);
                // Shift the window ahead by a single byte.
                Buffer.BlockCopy(window, 1, window, 0, window.Length - 1);
                window[window.Length - 1] = (byte)next;
            }
        }


        internal HttpRequest(string requestLine, HttpHeaderCollection headers, Stream body)
        {
            this.SetPropertiesFromRequestLine(requestLine);
            _headers = headers;

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

            if (this.Headers.Contains(HDR_CTYPE) && _raw.Length > 0)
            {
                var ctype = new NamedParametersHttpHeader(this.Headers[HDR_CTYPE].Last());

                if (ctype.Value.Equals(MT_FORMURLENCODED, StringComparison.OrdinalIgnoreCase))
                {
                    using (StreamReader sr = new StreamReader(body, Encoding.ASCII))
                    {
                        _post = ParseFormUrlEncoded(sr.ReadToEnd());
                    }
                }
                else if (ctype.Value.Equals(MT_FORMMULTIPART, StringComparison.OrdinalIgnoreCase))
                {
                    var bdry = ctype.Pairs
                        .Where(p => p.Key.Equals("boundary", StringComparison.OrdinalIgnoreCase))
                        .Select(p => p.Value)
                        .DefaultIfEmpty(null)
                        .First();

                    if (bdry == null)
                    {
                        throw new HttpRequestException
                        ("No boundary provided for multipart data.");
                    }

                    _post = ParseMultipartFormData(body, bdry);
                }
                else _post = new Empty();
            }
            else _post = new Empty();
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
        /// The raw bytes of the client's request.
        /// </summary>
        public byte[] Raw
        {
            get { return _raw; }
        }
    }
}
