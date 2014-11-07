/*
 *      Copyright (c) 2014, SynapLink, LLC
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE.txt, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// Thrown when there is an issue with a request.
    /// </summary>
    sealed class HttpRequestException : Exception
    {
        public HttpRequestException() : base() { }
        public HttpRequestException(string message) : base(message) { }
        public HttpRequestException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    /// <summary>
    /// A class encapsulating an HTTP request from the client.
    /// </summary>
    public class HttpRequest
    {
        private List<BasicHttpHeader> _headers;
        private NameValueCollection _post, _get;
        private byte[] _raw;
        private ASCIIEncoding _ascii;

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
                pathBuilder.Clear();
                string qstring = rlArray[1].Substring(strIndex);
                string section = String.Empty;
                bool inVal = false;

                foreach (char c in qstring)
                {
                    if (!inVal && c == '=')
                    {
                        inVal = true;
                        section = pathBuilder.ToString();
                        pathBuilder.Clear();
                    }
                    else if (inVal && c == '&')
                    {
                        this.GET.Add(section, pathBuilder.ToString());
                        pathBuilder.Clear();
                        inVal = false;
                    }
                    else
                    {
                        pathBuilder.Append(c);
                    }
                }

                this.GET.Add(section, pathBuilder.ToString());
            }

            this.HttpVersion = rlArray[2];
        }

        /// <summary>
        /// Creates a new HTTPRequest class using the raw contents of the
        /// request.
        /// </summary>
        /// <param name="requestStream">The stream of data containing the request.</param>
        /// <param name="requestStatus">Set to true if request parsing failed.</param>
        internal HttpRequest(StreamReader requestStream, out bool requestFailed) 
        {
            _ascii = new ASCIIEncoding();
            this.GET = new NameValueCollection();
            this.POST = new NameValueCollection();
            this.Headers = new List<IHttpHeader>();
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
            }
            // BasicHttpHeader throws an argument exception when there's an
            // issue with parsing.
            catch (ArgumentException ex)
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
        public List<BasicHttpHeader> Headers
        {
            get { return new List<IHttpHeader>(_headers); }
            private set { _headers = value; }
        }
        /// <summary>
        /// All POST parameters sent with the request.
        /// </summary>
        public NameValueCollection POST
        {
            get { return _post; }
            private set { _post = value; }
        }
        /// <summary>
        /// All GET (query string) parameters send with the request.
        /// </summary>
        public NameValueCollection GET
        {
            get { return _get; }
            private set { _get = value; }
        }
        /// <summary>
        /// The raw request received from the client.
        /// </summary>
        public byte[] Raw
        {
            get { return _raw; }
            private set { _raw = value; }
        }
    }
}
