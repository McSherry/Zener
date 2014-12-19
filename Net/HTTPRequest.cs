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

        private static ASCIIEncoding _ascii;
        private static UTF8Encoding _utf8;
        private static Dictionary<string, Encoding> _encodersByName
            = new Dictionary<string, Encoding>()
            {
                { "ascii", _ascii },
                { "us-ascii", _ascii },
                { "utf-8", _utf8 },
                { "utf8", _utf8 }
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

        static HttpRequest()
        {
            _ascii = new ASCIIEncoding();
            _utf8 = new UTF8Encoding();
        }

        private HttpHeaderCollection _headers;
        private dynamic _get, _post;
        private string _raw;

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
            byte[] boundaryBytes = _ascii.GetBytes(String.Format("--{0}", boundary));

            while (formatBody.Position != formatBody.Length)
            {
                // We know headers are going to be ASCII, so we can read lines
                // with our ASCIIEncoding until we hit an empty line.
                StringBuilder partHdrBuilder = new StringBuilder();
                using (StreamReader sr = new StreamReader(formatBody, _ascii))
                {
                    while (true)
                    {
                        string line = sr.ReadLine();
                        // An empty line indicates the break between the part
                        // headers and the part body. If we get one when reading
                        // headers, we can safely assume that no more headers are
                        // associated with this part.
                        if (String.IsNullOrEmpty(line)) break;

                        partHdrBuilder.AppendLine(line);
                    }
                }
                HttpHeaderCollection partHeaders = new HttpHeaderCollection(
                    BasicHttpHeader.ParseMany(new StringReader(partHdrBuilder.ToString()))
                    );

                if (!partHeaders.Contains(HDR_CDISPOSITION))
                    throw new HttpRequestException
                    ("Multipart data is malformed; no Content-Disposition.");
                var cdis = new NameValueHttpHeader(partHeaders[HDR_CDISPOSITION].Last());

                string name = cdis.Pairs
                    .Where(p => p.Key.Equals("name", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Value)
                    .DefaultIfEmpty(null)
                    .First();
                if (name == null)
                    throw new HttpRequestException
                    ("Multipart form data is malformed; no name.");

                long remLeng = formatBody.Position - formatBody.Length;
                List<byte> buffer = new List<byte>();
                byte[] window = new byte[boundaryBytes.Length];

                // If the remaining number of bytes is less than the length of the
                // boundary (plus 2, for the trailing --), then the body is malformed.
                if (boundaryBytes.Length + 2 > remLeng)
                {
                    throw new HttpRequestException
                    ("Multi-part form data is malformed.");
                }

                formatBody.Read(window, 0, window.Length);

                for (int i = 0; i < remLeng; i++)
                {
                    // If this evaluates to true, we've reached a
                    // boundary within the request body. The boundary
                    // indicates the end of the part body.
                    if (window.SequenceEqual(boundaryBytes)) break;

                    int next = formatBody.ReadByte();
                    // We haven't reached a boundary, but we have
                    // reached the end of the data we were sent.
                    if (next == -1) break;

                    // Add the byte we're about to drop from the window
                    // to the buffer.
                    buffer.Add(window[0]);
                    // Shift the array by one item. The item in [0] is dropped,
                    // and the next byte in the data stream is placed at the end.
                    Buffer.BlockCopy(window, 1, window, 0, window.Length - 1);
                    window[window.Length - 1] = (byte)next;
                }

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
                        var nvCtype = new NameValueHttpHeader(cType);

                        if (nvCtype.Pairs.ContainsKey(HDR_CTYPE_KCHAR))
                        {
                            var encName = nvCtype.Pairs[HDR_CTYPE_KCHAR].ToLower();

                            if (_encodersByName.ContainsKey(encName))
                            {
                                encoding = _encodersByName[encName];
                            }
                            else
                            {
                                encoding = _ascii;
                            }
                        }
                        else
                        {
                            encoding = _ascii;
                        }
                    }
                }
                else
                {
                    encoding = _ascii;
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
                if (formatBody.Length - formatBody.Position == 2)
                    break;
            }

            // If we added anything to dynObj, return it. Else,
            // return an Empty to indicate that there is no data.
            return dynObj.Count == 0 ? (dynamic)new Empty() : (dynamic)dynObj;

            //var parts = formatBody.Split(
            //    new string[] { string.Format("--{0}", boundary) },
            //    StringSplitOptions.None
            //    )
            //    .ToList();

            //parts.RemoveAll(p => string.IsNullOrWhiteSpace(p) || p.Equals("--\r\n"));
            //parts = parts.Select(p => p.Trim(' ', '\r', '\n')).ToList();

            //foreach (var part in parts)
            //{
            //    // Each part has its own set of headers, and they're
            //    // always the first thing in the part. We need to parse
            //    // out the headers so we know how to handle the content.
            //    bool inHeader = true;
            //    HttpHeaderCollection partHeaders = new HttpHeaderCollection();
            //    using (StringReader tr = new StringReader(part))
            //    {
            //        while (inHeader)
            //        {
            //            string line = tr.ReadLine().Trim();

            //            // As with the main HTTP headers, the headers of a
            //            // part are separated from the part's body by two
            //            // CRLFs. The ReadLine method will remove the CRLFs,
            //            // so we're left with an empty line signifying the
            //            // separator.
            //            if (string.IsNullOrWhiteSpace(line))
            //            {
            //                inHeader = false;
            //                continue;
            //            }

            //            partHeaders.Add(BasicHttpHeader.Parse(line));
            //        }

            //        // We need the content disposition header to determine
            //        // how we should handle the part. Currently, we'll only
            //        // handle it if it's form data.
            //        if (!partHeaders.Contains(HDR_CDISPOSITION))
            //        {
            //            throw new HttpRequestException
            //            ("One or more parts do not contain content disposition data.");
            //        }

            //        var cdis = new NameValueHttpHeader(
            //            partHeaders["Content-Disposition"].Last()
            //            );

            //        // Checks to see whether the disposition indicates form
            //        // data.
            //        if (!cdis.Value.Equals(CDIS_FORMDATA, StringComparison.OrdinalIgnoreCase))
            //        {
            //            // If it isn't form data, we don't care. Skip it and
            //            // move on to the next one.
            //            continue;
            //        }

            //        // If it is form data, make sure it has a name. If there's
            //        // no name, we can't identify it, and it can't be accessed.
            //        if (!cdis.Pairs.Any(
            //            p => p.Key.Equals("name", StringComparison.OrdinalIgnoreCase)
            //            ))
            //        {
            //            // If it has no name, skip it.
            //            continue;
            //        }

            //        // Extract the name of the part, URL-decode it,
            //        // and remove any characters which can't be used
            //        // in a name.
            //        string partName = _filterInvChars(
            //            WebUtility.UrlDecode(
            //                cdis.Pairs.Where(
            //                    p => p.Key.Equals("name", StringComparison.OrdinalIgnoreCase)
            //                    )
            //                .Last()
            //                .Value
            //                )
            //            );


            //        NameValueHttpHeader ctype;
            //        if (partHeaders.Contains(HDR_CTYPE))
            //        {
            //            ctype = new NameValueHttpHeader(partHeaders[HDR_CTYPE].Last());
            //        }
            //        else
            //        {
            //            ctype = new NameValueHttpHeader(HDR_CTYPE, "text/plain");
            //        }

            //        // We're only handling text/* media types. If binary data
            //        // is required, use one of the APIs provided instead of
            //        // HTML forms.
            //        if (!ctype.Value.ToLower().StartsWith("text/"))
            //        {
            //            continue;
            //        }

            //        dynObj[partName] = tr.ReadToEnd();
            //    }
            //}

            //return new Empty();
        }

        /// <summary>
        /// Creates a new HTTPRequest class using the raw contents of the
        /// request.
        /// </summary>
        /// <param name="requestStream">The stream of data containing the request.</param>
        /// <param name="requestStatus">Set to true if request parsing failed.</param>
        /// <exception cref="SynapLink.Zener.Net.HttpRequestException"></exception>
        internal HttpRequest(Stream requestStream) 
        {
            this.Headers = new HttpHeaderCollection();

            // If any of this throws an HttpRequestException, we can be sure that
            // the request is malformed (well, as long as the HTTP server is really
            // compliant).
            try
            {
                using (var rStrReader = new StreamReader(requestStream))
                {
                    this.SetPropertiesFromRequestLine(rStrReader.ReadLine());

                    _headers = new HttpHeaderCollection(
                        BasicHttpHeader.ParseMany(rStrReader),
                        true
                        );

                    var contenttype = this.Headers.Where(
                        h => h.Field.Equals(HDR_CTYPE, StringComparison.OrdinalIgnoreCase)
                        );

                    if (contenttype.Count() == 0) _post = new Empty();
                    else
                    {
                        var ctype = new NameValueHttpHeader(contenttype.Last());

                        if (ctype.Value.Equals(MT_FORMURLENCODED, StringComparison.OrdinalIgnoreCase))
                        {
                            _post = ParseFormUrlEncoded(rStrReader.ReadToEnd());
                        }
                        else if (ctype.Value.Equals(MT_FORMMULTIPART, StringComparison.OrdinalIgnoreCase))
                        {
                            var bdry = ctype.Pairs.Where(
                                p => p.Key.Equals("boundary", StringComparison.OrdinalIgnoreCase)
                                ).ToList();

                            if (bdry.Count == 0)
                            {
                                throw new HttpRequestException
                                ("No boundary provided for multipart data.");
                            }

                            _post = ParseMultipartFormData(requestStream, bdry[0].Value);
                        }
                        else _post = new Empty();
                    }
                }

            }
            // BasicHttpHeader throws an argument exception when there's an
            // issue with parsing.
            catch (ArgumentException aex)
            {
                throw new HttpRequestException
                ("User agent provided malformed headers.", aex);
            }
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
    }
}
