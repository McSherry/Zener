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
using System.Text.RegularExpressions;
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

        private static Regex _dynReplRegex;
        private static ASCIIEncoding _ascii;

        static HttpRequest()
        {
            _ascii = new ASCIIEncoding();
            // Used to replace characters which might turn up
            // in multipart/form-data names with a safe character,
            // since we're using that name to create dynamic
            // properties.
            _dynReplRegex = new Regex("[- +/\\'\"][{}]=()*&^%$£!¬¦`€|?><~#;:@");
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
                    section = _dynReplRegex.Replace(
                        WebUtility.UrlDecode(qBuilder.ToString()),
                        String.Empty
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
                    section = _dynReplRegex.Replace(
                        WebUtility.UrlDecode(qBuilder.ToString()),
                        String.Empty
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
                section = _dynReplRegex.Replace(
                    WebUtility.UrlDecode(qBuilder.ToString()),
                    String.Empty
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
        private static dynamic ParseMultipartFormData(string formatBody, string boundary)
        {
            var dynObj = new ExpandoObject() as IDictionary<string, object>;
            var parts = formatBody.Split(
                new string[] { string.Format("--{0}", boundary) },
                StringSplitOptions.None
                )
                .ToList();

            parts.RemoveAll(p => string.IsNullOrWhiteSpace(p) || p.Equals("--\r\n"));
            parts = parts.Select(p => p.Trim(' ', '\r', '\n')).ToList();

            foreach (var part in parts)
            {
                // Each part has its own set of headers, and they're
                // always the first thing in the part. We need to parse
                // out the headers so we know how to handle the content.
                bool inHeader = true;
                HttpHeaderCollection partHeaders = new HttpHeaderCollection();
                using (StringReader tr = new StringReader(part))
                {
                    while (inHeader)
                    {
                        string line = tr.ReadLine().Trim();

                        // As with the main HTTP headers, the headers of a
                        // part are separated from the part's body by two
                        // CRLFs. The ReadLine method will remove the CRLFs,
                        // so we're left with an empty line signifying the
                        // separator.
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            inHeader = false;
                            continue;
                        }

                        partHeaders.Add(BasicHttpHeader.Parse(line));
                    }

                    // We need the content disposition header to determine
                    // how we should handle the part. Currently, we'll only
                    // handle it if it's form data.
                    if (!partHeaders.Contains(HDR_CDISPOSITION))
                    {
                        throw new HttpRequestException
                        ("One or more parts do not contain content disposition data.");
                    }

                    var cdis = new NameValueHttpHeader(
                        partHeaders["Content-Disposition"].Last()
                        );

                    // Checks to see whether the disposition indicates form
                    // data.
                    if (!cdis.Value.Equals(CDIS_FORMDATA, StringComparison.OrdinalIgnoreCase))
                    {
                        // If it isn't form data, we don't care. Skip it and
                        // move on to the next one.
                        continue;
                    }

                    // If it is form data, make sure it has a name. If there's
                    // no name, we can't identify it, and it can't be accessed.
                    if (!cdis.Pairs.Any(
                        p => p.Key.Equals("name", StringComparison.OrdinalIgnoreCase)
                        ))
                    {
                        // If it has no name, skip it.
                        continue;
                    }

                    // Extract the name of the part, URL-decode it,
                    // and remove any characters which can't be used
                    // in a name.
                    string partName = _dynReplRegex.Replace(
                        WebUtility.UrlDecode(
                            cdis.Pairs.Where(
                                p => p.Key.Equals("name", StringComparison.OrdinalIgnoreCase)
                                )
                            .Last()
                            .Value
                            ),
                        String.Empty
                        );


                    NameValueHttpHeader ctype;
                    if (partHeaders.Contains(HDR_CTYPE))
                    {
                        ctype = new NameValueHttpHeader(partHeaders[HDR_CTYPE].Last());
                    }
                    else
                    {
                        ctype = new NameValueHttpHeader(HDR_CTYPE, "text/plain");
                    }

                    // We're only handling text/* media types. If binary data
                    // is required, use one of the APIs provided instead of
                    // HTML forms.
                    if (!ctype.Value.ToLower().StartsWith("text/"))
                    {
                        continue;
                    }

                    dynObj[partName] = tr.ReadToEnd();
                }
            }

            return new Empty();
        }

        /// <summary>
        /// Creates a new HTTPRequest class using the raw contents of the
        /// request.
        /// </summary>
        /// <param name="requestStream">The stream of data containing the request.</param>
        /// <param name="requestStatus">Set to true if request parsing failed.</param>
        /// <exception cref="SynapLink.Zener.Net.HttpRequestException"></exception>
        internal HttpRequest(StreamReader requestStream) 
        {
            this.Headers = new HttpHeaderCollection();

            // If any of this throws an HttpRequestException, we can be sure that
            // the request is malformed (well, as long as the HTTP server is really
            // compliant).
            try
            {
                this.SetPropertiesFromRequestLine(requestStream.ReadLine());

                while (true)
                {
                    string line = requestStream.ReadLine();
                    // If the line is null, we've reached the end of the
                    // stream. If its length is zero, we've reached an empty
                    // line. An empty line means we've hit the separator between
                    // the headers and the request body, so we will want to stop
                    // parsing headers.
                    if (line == null || line.Length == 0) break;

                    _headers.Add(BasicHttpHeader.Parse(line));
                }

                // Stops headers being added to the collection.
                _headers.WriteProtect(true);

                _raw = requestStream.ReadToEnd();

                var contenttype = this.Headers.Where(
                    h => h.Field.Equals(HDR_CTYPE, StringComparison.OrdinalIgnoreCase)
                    );

                if (contenttype.Count() == 0) _post = new Empty();
                else
                {
                    var ctype = new NameValueHttpHeader(contenttype.Last());

                    if (ctype.Value.Equals(MT_FORMURLENCODED, StringComparison.OrdinalIgnoreCase))
                    {
                        _post = ParseFormUrlEncoded(this.Raw);
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

                        _post = ParseMultipartFormData(this.Raw, bdry[0].Value);
                    }
                    else _post = new Empty();
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
        /// <summary>
        /// The raw request body received from the client.
        /// </summary>
        public string Raw
        {
            get { return _raw; }
            private set { _raw = value; }
        }
    }
}
