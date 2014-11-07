/*
 *      Copyright (c) 2014, SynapLink, LLC
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE.txt, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// HTTP status codes. Can be cast to and from Int32 to
    /// get the numeric code.
    /// </summary>
    public enum HttpStatus : int
    {
        /// <summary>
        /// Status code 100. Informs the client that it should
        /// continue with its request, and to expect a second
        /// response.
        /// </summary>
        Continue                        = 100,
        /// <summary>
        /// Status code 101. The server will comply with the client's
        /// upgrade request immediately after sending the 101 response.
        /// </summary>
        SwitchingProtocols              = 101,

        /// <summary>
        /// Status code 200. The client's request was successful.
        /// </summary>
        OK                              = 200,
        /// <summary>
        /// Status code 201. The client's request succeeded, and resulted
        /// in the creation of a new resource. The server sends an entity
        /// containing information about the new resource.
        /// </summary>
        Created                         = 201,
        /// <summary>
        /// Status code 202. The server has accepted the client's request,
        /// and is processing it.
        /// </summary>
        Accepted                        = 202,
        /// <summary>
        /// Status code 203. The information returned by the server may be
        /// sourced from a third party, and not from the origin server.
        /// </summary>
        NonAuthoritativeInformation     = 203,
        /// <summary>
        /// Status code 204. The server has fulfilled but the request, but
        /// the response will not contain a body. It may contain entity headers.
        /// </summary>
        NoContent                       = 204,
        /// <summary>
        /// Status code 205. The server has fulfilled the request, and is
        /// instructing the client to reset its view of the requesting
        /// document.
        /// </summary>
        ResetContent                    = 205,
        /// <summary>
        /// Status code 206. The server has fulfilled the request for a range
        /// of content from the requested resource.
        /// </summary>
        PartialContent                  = 206,

        /// <summary>
        /// Status code 300. The requested resource corresponds to a set of
        /// resources, and the server will return a list for the client to
        /// choose from.
        /// </summary>
        MultipleChoices                 = 300,
        /// <summary>
        /// Status code 301. The requested resource has been permanently
        /// moved to a new location, indicated to in the response headers.
        /// Clients should cache this new location.
        /// </summary>
        MovedPermanently                = 301,
        /// <summary>
        /// Status code 302. The requested resource is temporarily at a
        /// different location, indicated to in the response headers. Clients
        /// should not cache this redirection.
        /// </summary>
        Found                           = 302,
        /// <summary>
        /// Status code 303. The requested resource should be retrieved via a
        /// GET request to the resource specified in the response headers. The
        /// response should not be cached.
        /// </summary>
        SeeOther                        = 303,
        /// <summary>
        /// Status code 304. A client has requested a resource if it has been
        /// updated, and the resource has not been updated. This response should
        /// not include a response body.
        /// </summary>
        NotModified                     = 304,
        /// <summary>
        /// Status code 305. The client should access the requested resource
        /// through the proxy indicated to in the response headers.
        /// </summary>
        UseProxy                        = 305,
        /// <summary>
        /// Status code 307. The requested resource is temporarily at a different
        /// location, indicated to by the response headers. This response should
        /// not be cached.
        /// </summary>
        TemporaryRedirect               = 307,

        /// <summary>
        /// Status code 400. The server was unable to understand the client's
        /// request due to malformed syntax.
        /// </summary>
        BadRequest                      = 400,
        /// <summary>
        /// Status code 401. The requested resource requires authentication.
        /// </summary>
        Unauthorized                    = 401,
        /// <summary>
        /// Status code 403. The client's request was understood by the server, but
        /// the server refuses to fulfill it. The client should not repeat its
        /// request.
        /// </summary>
        Forbidden                       = 403,
        /// <summary>
        /// Status code 404. The resource requested by the client was not found.
        /// </summary>
        NotFound                        = 404,
        /// <summary>
        /// Status code 405. The HTTP method used by the client in its request is
        /// not permitted for the requested resource.
        /// </summary>
        MethodNotAllowed                = 405,
        /// <summary>
        /// Status code 406. The resource requested by the client was not acceptable
        /// under the conditions given by the client's Accept request header.
        /// </summary>
        NotAcceptable                   = 406,
        /// <summary>
        /// Status code 407. The client must authenticate itself with the proxy
        /// indicated to in the response headers before accessing the resource.
        /// </summary>
        ProxyAuthenticationRequired     = 407,
        /// <summary>
        /// Status code 408. The time taken by the client to complete its request
        /// was longer than the server was prepared to wait.
        /// </summary>
        RequestTimeout                  = 408,
        /// <summary>
        /// Status code 409. Due to a conflict with the state of the requested
        /// resource, the client's request could not be fulfilled.
        /// </summary>
        Conflict                        = 409,
        /// <summary>
        /// Status code 410. The requested resource did at one time exist, but has
        /// been permanently deleted or removed.
        /// </summary>
        Gone                            = 410,
        /// <summary>
        /// Status code 411. The client must send a Content-Length header for its
        /// request to be accepted by the server.
        /// </summary>
        LengthRequired                  = 411,
        /// <summary>
        /// Status code 412. A precondition given by the client evaluated to false,
        /// and the server is thus unable to complete the request.
        /// </summary>
        PreconditionFailed              = 412,
        /// <summary>
        /// Status code 413. The request entity provided by the client is larger
        /// than the server is willing or able to process.
        /// </summary>
        RequestEntityTooLarge           = 413,
        /// <summary>
        /// Status code 414. The URI provided in the client's request was longer
        /// than the server was willing to process.
        /// </summary>
        RequestUriTooLarge              = 414,
        /// <summary>
        /// Status code 415. The media type of the entity of the client's request
        /// was not in a format supported for the resource.
        /// </summary>
        UnsupportedMediaType            = 415,
        /// <summary>
        /// Status code 416. The Range request header sent by the client was not
        /// acceptable to the server.
        /// </summary>
        RequestedRangeNotSatisfiable    = 416,
        /// <summary>
        /// Status code 417. The expectation given by the client in its Expect
        /// header could not be met by the server.
        /// </summary>
        ExpectationFailed               = 417,

        /// <summary>
        /// Status code 500. The server encountered an error or issue which
        /// prevented it from fulfilling the client's request.
        /// </summary>
        InternalServerError             = 500,
        /// <summary>
        /// Status code 501. The server does not have the functionality to
        /// fulfill the client's request. 
        /// </summary>
        NotImplemented                  = 501,
        /// <summary>
        /// Status code 502. The server, acting as a gateway or proxy, could
        /// not fulfill the client's request due to an invalid response from
        /// the server it was providing a proxy to.
        /// </summary>
        BadGateway                      = 502,
        /// <summary>
        /// Status code 503. Due to maintenance or overloading, the server is
        /// unable to process the client's request for the specified resource.
        /// </summary>
        ServiceUnavailable              = 503,
        /// <summary>
        /// Status code 504. The server, acting as a gateway or proxy, did not
        /// receive in time a response from the server it was acting as a proxy
        /// to.
        /// </summary>
        GatewayTimeout                  = 504,
        /// <summary>
        /// Status code 505. The HTTP version requested by the client is not
        /// supported by the server, and thus the server could not fulfill the
        /// request.
        /// </summary>
        HttpVersionNotSupported         = 505
    }

    /// <summary>
    /// A class providing the functionality required for a handler to
    /// respond to an HTTP request.
    /// </summary>
    public class HttpResponse
    {
        private static readonly Dictionary<HttpStatus, string> STAT_MSGS
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

        private HttpStatus _httpStatus;
        private List<BasicHttpHeader> _headers;
        private TcpClient _tcl;
        private StreamWriter _nsw;
        // Set to true when the first write is made. When this is
        // true, it indicates that the response headers have been
        // sent to the client.
        private bool _beginRespond;

        internal HttpResponse(TcpClient tcl)
        {
            this.StatusCode = HttpStatus.OK;
            _tcl = tcl;
            _nsw = new StreamWriter(tcl.GetStream());
            _headers = new List<BasicHttpHeader>();
        }

        /// <summary>
        /// The HTTP status code to be returned by the server.
        /// </summary>
        /// <exception cref="System.ArgumentException"></exception>
        /// <exception cref="System.InvalidOperationException"></exception>
        public HttpStatus StatusCode
        {
            get { return _httpStatus; }
            set
            {
                if (_beginRespond) throw new InvalidOperationException
                ("Cannot modify status code after the response body has been written to.");

                if (value.Item1 > 0 && value.Item1 < 1000)
                    throw new ArgumentException
                    ("Invalid HTTP status code provided.");

                if (value.Item2.Any(c => c == '\r' || c == '\n'))
                    throw new ArgumentException
                    ("HTTP status message cannot contain CRLF.");

                _httpStatus = value;
            }
        }

        /// <summary>
        /// Adds a header to the HTTP response.
        /// </summary>
        /// <param name="header">The header to add.</param>
        /// <param name="overwrite">Whether to overwrite any previous headers with the same field name.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void SetHeader(BasicHttpHeader header, bool overwrite)
        {
            if (_beginRespond) throw new InvalidOperationException
            ("Cannot set headers after response body has been written to.");

            if (overwrite)
            {
                _headers.RemoveAll(h => h.Field == header.Field);
            }

            _headers.Add(header);
        }
    }
}
