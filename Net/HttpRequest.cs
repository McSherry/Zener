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

        internal HttpHeaderCollection _headers;
        internal dynamic _get, _post, _cookies;

        /// <summary>
        /// Create a new HttpRequest to be manually initialised.
        /// </summary>
        internal HttpRequest()
        {

        }

        /// <summary>
        /// The HTTP method/verb used with the request.
        /// </summary>
        public string Method
        {
            get;
            internal set;
        }
        /// <summary>
        /// The path requested by the HTTP user agent, sans any query
        /// string parameters.
        /// </summary>
        public string Path
        {
            get;
            internal set;
        }
        /// <summary>
        /// The HTTP version requested by the user agent.
        /// </summary>
        public string HttpVersion
        {
            get;
            internal set;
        }
        /// <summary>
        /// The HTTP headers sent with the request.
        /// </summary>
        public HttpHeaderCollection Headers
        {
            get { return _headers; }
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
        }
        /// <summary>
        /// Any cookies sent with the request.
        /// </summary>
        public dynamic Cookies
        {
            get { return _cookies; }
        }
    }
}
