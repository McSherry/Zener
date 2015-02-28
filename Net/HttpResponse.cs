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
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;

using McSherry.Zener.Net.Serialisation;

namespace McSherry.Zener.Net
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
        : IDisposable
    {
        private const string 
            HDR_SETCOOKIE       = "Set-Cookie",
            HDR_CONTENTLENGTH   = "Content-Length",
            HDR_XFERENCODING    = "Transfer-Encoding",
            HDR_SERVER          = "Server",
            HDR_CONTENTTYPE     = "Content-Type",
            HDR_DATE            = "Date",
            HDR_ACCEPTENCODING  = "Accept-Encoding",
            HDR_CONTENTENCODING = "Content-Encoding",

            HDRF_CHUNKEDXFER    = "chunked",
            HDRF_SERVER         = "Zener/{0}",
            HDRF_CONTENTTYPE    = "text/html",
            HDRF_ENCODING_GZIP  = "gzip",
            HDRF_ENCODING_ANY   = "*",

            HTTP_VERSION        = "1.1",
            HTTP_NEWLINE        = "\r\n"
            ;
        internal const int
            TX_BUFFER_SIZE      = 16384
            ;

        private HttpStatus _httpStatus;
        private HttpHeaderCollection _headers;
        private HttpCookieCollection _cookies;
        // Response stream, output buffer stream
        private Stream _rstr, _obstr;
        // Set to true when the first write is made. When this is
        // true, it indicates that the response headers have been
        // sent to the client.
        private bool _beginRespond, 
            // True when the Close() method has been called.
            _closed, 
            // Whether output buffering is enabled.
            _bufferOutput,
            // Whether to compress output. Requires output
            // buffering be enabled.
            _enableCompression
            ;
        private Encoding _encoding;

        private void _Write(byte[] bytes)
        {
            if (this.BufferOutput)
            {
                _BufferedWrite(bytes);
            }
            else
            {
                _ChunkedNetworkWrite(bytes);
            }
        }
        
        /// <summary>
        /// Checks whether the response has been closed, and
        /// throws an InvalidOperationException if it has.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the resonse has been closed.
        /// </exception>
        internal void CheckClosed()
        {
            if (_closed) throw new InvalidOperationException
            ("Cannot modify the response after the connection has been closed.");
        }
        /// <summary>
        /// Checks whether the headers have been sent, and
        /// throws an InvalidOperationException if they have.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the response headers have been sent.
        /// </exception>
        internal void CheckSerialiser()
        {
            if (this.Serialiser == null)
            {
                throw new ApplicationException(
                    "The HttpResponse has not been configured with a " +
                    "serialiser."
                    );
            }
        }

        /// <summary>
        /// Creates a new HttpResponse.
        /// </summary>
        /// <param name="responseStream">
        /// The stream to write the contents of the response to.
        /// </param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided stream does not support the
        ///     required operations.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided stream is null.
        /// </exception>
        internal HttpResponse(Stream responseStream)
        {
            if (responseStream == null)
            {
                throw new ArgumentNullException(
                    "The response stream cannot be null."
                    );
            }

            if (
                !responseStream.CanRead ||
                !responseStream.CanWrite)
            {
                throw new ArgumentException
                ("Provided stream must support reading and writing.");
            }

            this.StatusCode = HttpStatus.OK;
            this.BufferOutput = false;
            this.Encoding = Encoding.UTF8;
            _rstr = responseStream;
            _headers = new HttpHeaderCollection();
            _cookies = new HttpCookieCollection();
            _beginRespond = false;
            _closed = false;
        }

        /// <summary>
        /// The response line that will be sent with the server's response.
        /// </summary>
        public string ResponseLine
        {
            get
            {
                return String.Format(
                    "HTTP/{0} {1} {2}\r\n",
                    HTTP_VERSION,
                    this.StatusCode.GetCode(),
                    this.StatusCode.GetMessage()
                    );
            }
        }
        /// <summary>
        /// The HTTP status code to be returned by the server.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the response body has already been sent
        ///     to the client.
        ///     
        ///     Throws when the connection between the client and
        ///     server has been closed.
        /// </exception>
        public HttpStatus StatusCode
        {
            get { return _httpStatus; }
            set
            {
                this.CheckClosed();
                this.CheckSerialiser();

                _httpStatus = value;
            }
        }
        /// <summary>
        /// The headers to be sent with the response.
        /// </summary>
        public HttpHeaderCollection Headers
        {
            get { return _headers; }
        }
        /// <summary>
        /// The cookies to be sent with the response.
        /// </summary>
        public HttpCookieCollection Cookies
        {
            get { return _cookies; }
        }
        /// <summary>
        /// The serialiser that is being used to
        /// transform the HttpResponse in to the
        /// format it will be transmitted over the
        /// network in.
        /// </summary>
        public HttpSerialiser Serialiser
        {
            get;
            internal set;
        }
        /// <summary>
        /// Whether to enable output buffering. Output buffering delays
        /// the sending of the response body.
        /// </summary>
        public bool BufferOutput
        {
            get { return _bufferOutput; }
            set
            {
                this.CheckSerialiser();

                if (value && _obstr == null)
                {
                    _obstr = new MemoryStream();
                }

                _bufferOutput = value;
            }
        }
        /// <summary>
        /// The encoding used when writing strings to the response. Defaults
        /// to UTF-8.
        /// </summary>
        public Encoding Encoding
        {
            get { return _encoding; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(
                        "The response's encoding cannot be null."
                        );
                }

                _encoding = value;
            }
        }
        /// <summary>
        /// Whether HTTP compression is currently enabled
        /// for this response.
        /// </summary>
        public bool IsCompressed
        {
            get { return _enableCompression; }
            private set { _enableCompression = value; }
        }
        /// <summary>
        /// Whether the response has been closed.
        /// </summary>
        public bool Closed
        {
            get { return this.Serialiser.IsClosed; }
        }

        /// <summary>
        /// Writes the provided value to the response.
        /// </summary>
        /// <param name="obj">The value to write.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the method is called after the headers or
        ///     sent, or when it is called after the connection has been
        ///     closed.
        /// </exception>
        public void Write(object obj)
        {
            this.Write(value: obj.ToString());
        }
        /// <summary>
        /// Writes the provided string to the response.
        /// </summary>
        /// <param name="value">The string to write.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the method is called after the headers or
        ///     sent, or when it is called after the connection has been
        ///     closed.
        /// </exception>
        public void Write(string value)
        {
            this.Write(this.Encoding.GetBytes(value));
        }
        /// <summary>
        /// Writes the provided bytes to the response.
        /// </summary>
        /// <param name="bytes">The value to write to the response.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the method is called after the headers or
        ///     sent, or when it is called after the connection has been
        ///     closed.
        /// </exception>
        public void Write(IEnumerable<byte> bytes)
        {
            // Ensure that we've been configured with a
            // serialiser.
            this.CheckSerialiser();
            // Ensure that the response has not been closed.
            this.CheckClosed();

            // Write data to the serialiser. The serialiser will
            // then perform any necessary action (such as sending
            // the data over the network).
            this.Serialiser.WriteData(
                bytes is byte[] ? (byte[])bytes : bytes.ToArray()
                );
        }
        /// <summary>
        /// Writes the provided values to the response in the
        /// specified format.
        /// </summary>
        /// <param name="format">The format to write the values in.</param>
        /// <param name="args">The values to write.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the method is called after the headers or
        ///     sent, or when it is called after the connection has been
        ///     closed.
        /// </exception>
        public void Write(string format, params object[] args)
        {
            this.Write(String.Format(format, args));
        }
        /// <summary>
        /// Writes the provided value to the response, followed
        /// by a new-line.
        /// </summary>
        /// <param name="obj">The value to write.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the method is called after the headers or
        ///     sent, or when it is called after the connection has been
        ///     closed.
        /// </exception>
        public void WriteLine(object obj)
        {
            this.Write("{0}{1}", obj.ToString(), HTTP_NEWLINE);
        }
        /// <summary>
        /// Writes the provided string to the response,
        /// followed by a new-line.
        /// </summary>
        /// <param name="value">The string to write.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the method is called after the headers or
        ///     sent, or when it is called after the connection has been
        ///     closed.
        /// </exception>
        public void WriteLine(string value)
        {
            this.Write("{0}{1}", value, HTTP_NEWLINE);
        }
        /// <summary>
        /// Writes the provided values to the response in the
        /// specified format, followed by a new-line.
        /// </summary>
        /// <param name="format">The format to write the values in.</param>
        /// <param name="args">The values to write.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the method is called after the headers or
        ///     sent, or when it is called after the connection has been
        ///     closed.
        /// </exception>
        public void WriteLine(string format, params object[] args)
        {
            StringBuilder formatBuilder = new StringBuilder();
            formatBuilder.AppendFormat(format, args);
            formatBuilder.Append(HTTP_NEWLINE);

            this.Write(value: formatBuilder.ToString());
        }

        /// <summary>
        /// Closes the connection between the server and the client.
        /// </summary>
        public void Close()
        {
            if (!_closed)
            {
                // The _closed variable must be set to true
                // for the headers/body to be sent properly
                // when output buffering is enabled.
                _closed = true;
                // Send the headers and, if output buffering
                // is enabled, flush the buffer to the network.
                _ConditionalSendHeaders();

                // If output buffering is disabled, we're sending
                // with chunked transfer encoding. This means that
                // we need to send a terminating chunk to signify
                // that we've finished sending chunked data.
                // 
                // Terminating chunks are "zero-length." Quoted
                // because we still send bytes, but the bytes
                // indicate that the chunk has no data.
                if (!this.BufferOutput)
                {
                    _ChunkedNetworkWrite(new byte[0]);
                }

                if (_obstr != null)
                {
                    _obstr.Close();
                    _obstr.Dispose();
                }
            }
        }
        /// <summary>
        /// Calls HttpResponse.Close.
        /// </summary>
        void IDisposable.Dispose()
        {
            this.Close();
        }
    }
}
