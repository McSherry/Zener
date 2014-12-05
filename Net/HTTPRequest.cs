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
    /// Thrown when there is an issue with a request.
    /// </summary>
    public sealed class HttpRequestException : Exception
    {
        public HttpRequestException() : base() { }
        public HttpRequestException(string message) : base(message) { }
        public HttpRequestException(string message, Exception innerException)
            : base(message, innerException) { }
    }
    /// <summary>
    /// Used to indicate when a dynamic property has no value.
    /// </summary>
    public enum Empty { }

    /// <summary>
    /// A class encapsulating an HTTP request from the client.
    /// </summary>
    public class HttpRequest
    {
        private static Regex _dynReplRegex;
        private static ASCIIEncoding _ascii;

        static HttpRequest()
        {
            _ascii = new ASCIIEncoding();
            // Used to replace characters which might turn up
            // in multipart/form-data names with a safe character,
            // since we're using that name to create dynamic
            // properties.
            _dynReplRegex = new Regex("[- +]");
        }

        private HttpHeaderCollection _headers;
        private dynamic _get;
        private readonly Lazy<dynamic> _post;
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
        private dynamic ParseMultipartFormData()
        {
            throw new Exception();
        }

        /// <summary>
        /// Creates a new HTTPRequest class using the raw contents of the
        /// request.
        /// </summary>
        /// <param name="requestStream">The stream of data containing the request.</param>
        /// <param name="requestStatus">Set to true if request parsing failed.</param>
        internal HttpRequest(StreamReader requestStream, out bool requestFailed) 
        {
            this.Headers = new HttpHeaderCollection();
            // If the request does fail, this will be overwritten anyway.
            requestFailed = false;

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

                _raw = requestStream.ReadToEnd();
            }
            // BasicHttpHeader throws an argument exception when there's an
            // issue with parsing.
            catch (ArgumentException)
            {
                requestFailed = true;
            }
            catch (HttpRequestException)
            {
                requestFailed = true;
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
            get { return _post.Value; }
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
