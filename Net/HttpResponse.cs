/*
 *      Copyright (c) 2014-2015, SynapLink, LLC
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
        private const string 
            HDR_SETCOOKIE       = "Set-Cookie",
            HDR_CONTENTLENGTH   = "Content-Length",
            HDR_XFERENCODING    = "Transfer-Encoding",
            HDR_SERVER          = "Server",
            HDR_CONTENTTYPE     = "Content-Type",

            HDRF_CHUNKEDXFER    = "Chunked",
            HDRF_SERVER         = "Zener/{0}",
            HDRF_CONTENTTYPE    = "text/html",

            HTTP_VERSION        = "1.1",
            HTTP_NEWLINE        = "\r\n"
            ;

        private HttpStatus _httpStatus;
        private HttpHeaderCollection _headers;
        private HttpCookieCollection _cookies;
        private Action _closeCallback;
        // Response stream, output buffer stream
        private Stream _rstr, _obstr;
        // Set to true when the first write is made. When this is
        // true, it indicates that the response headers have been
        // sent to the client.
        private bool _beginRespond, 
            // True when the Close() method has been called.
            _closed, 
            // Whether output buffering is enabled.
            _bufferOutput;

        private void _BufferedWrite(byte[] bytes)
        {
            _obstr.Write(bytes, 0, bytes.Length);
        }
        private void _ChunkedNetworkWrite(byte[] bytes)
        {
            // The chunked transfer encoding mechanism specifies
            // the length of its chunks using a hexadecimal string.
            // Thankfully, Int32.ToString provides an easy-to-use
            // way of doing this.
            //
            // This hexadecimal integer is ASCII-encoded.
            var len = Encoding.ASCII.GetBytes(bytes.Length.ToString("x"));
            // The length is separated from the chunk data by a
            // newline, and the chunk itself is terminated by a
            // newline.
            var nl = Encoding.ASCII.GetBytes(HTTP_NEWLINE);

            // The length comes first.
            _rstr.Write(len, 0, len.Length);
            // Followed by a separating newline.
            _rstr.Write(nl, 0, nl.Length);
            // Followed by the chunk body.
            _rstr.Write(bytes, 0, bytes.Length);
            // And then a final terminating newline.
            _rstr.Write(nl, 0, nl.Length);
        }
        private void _ConditionalSendHeaders()
        {
            // If we've already begun responding, we've already
            // sent the headers, so we can just return.
            if (_beginRespond) return;
            // If we're buffering output, the headers will be
            // sent later.
            if (this.BufferOutput) return;

            // If we're here, it means output buffering is
            // disabled. This means that we can't know the
            // content length in advance, so we need to remove
            // any Content-Length headers.
            this.Headers.Remove(HDR_CONTENTLENGTH);
            // With buffering disabled, we'll be using chunked
            // transfer encoding to transfer data. Set the
            // Transfer-Encoding header, overwriting any previous
            // such headers.
            this.Headers.Add(
                fieldName:  HDR_XFERENCODING,
                fieldValue: HDRF_CHUNKEDXFER,
                overwrite:  true
                );
            // Add the server identification header. This includes
            // the name of the server software, and the current
            // version.
            this.Headers.Add(
                fieldName:  HDR_SERVER,
                fieldValue: String.Format(HDRF_SERVER, ZenerCore.Version),
                overwrite:  true
                );
            // Responses should always be sent with a Content-Type
            // header. We need to check to see if the response
            // already has such a header.
            if (!this.Headers.Contains(HDRF_CONTENTTYPE))
            {
                // If the response does not have a Content-Type header,
                // we'll add one with a sane default value. In this case,
                // text/html since it's reasonably likely that whatever
                // is being served will be HTML.
                this.Headers.Add(
                    fieldName:  HDR_CONTENTTYPE,
                    fieldValue: HDRF_CONTENTTYPE
                    );
            }
            // We want to ensure that the user knows that they can no longer
            // modify headers. To do this, we make the headers collection
            // read-only. The collection will now throw an exception should
            // the user try to add elements to or remove elements from the
            // collection.
            this.Headers.IsReadOnly = true;

            // To save on calls to an encoder and calls to the response stream,
            // we'll use a string builder to build our headers before sending
            // them. Headers are always ASCII text, so this is perfectly fine.
            StringBuilder headerBuilder = new StringBuilder();
            // The response line comes first. This contains the HTTP version,
            // status code, and an optional textual status message. We'll be
            // using AppendFormat to ensure that newlines are compliant. It
            // is possible that other platforms may append different new-lines
            // (for example, a *nix system may use \n alone, while HTTP requires
            // the use of \r\n).
            headerBuilder.AppendFormat("{0}{1}", this.ResponseLine, HTTP_NEWLINE);
            // The HttpHeaderCollection class provides an overload of
            // Object.ToString, so we are able to just call that. The
            // class handles the newlines at the end of headers, but
            // we still need to provide the second newline that indicates
            // to the HTTP client (such as a browser) the end of the headers
            // and the start of the response body.
            headerBuilder.AppendFormat("{0}{1}", this.Headers.ToString(), HTTP_NEWLINE);

            // It is possible that the user enabled output buffering, wrote
            // to the response, then disabled it. If this is the case, we
            // need to ensure that we write whatever was in the buffer
            // before anything else is written.
            if (_obstr != null && _obstr.Length > 0)
            {
                // Read the contents of the buffer in to a byte array.
                byte[] outBuf = new byte[_obstr.Length];
                _obstr.Read(outBuf, 0, outBuf.Length);
                // Write the contents of the byte array to the network.
                // The method called will write in the chunked transfer
                // encoding format.
                _ChunkedNetworkWrite(outBuf);
            }

            // We've written our headers, so we want to ensure that they are
            // not written again, and that the user cannot make modifications
            // to any other header-related properties. This field indicates
            // that we have begun responding, and so cannot modify headers.
            _beginRespond = true;
        }
        /// <summary>
        /// Writes the headers to the StreamWriter if they have not
        /// already been written.
        /// </summary>
        [Obsolete("", true)]
        private void _ConditionalWriteHeaders()
        {
            if (!_beginRespond)
            {
                _nsw.Write(this.ResponseLine);

                // Ensures that the content is transferred with a media type.
                // Defaults to HTML, since what else is an HTTP server most likely
                // to be serving?
                if (!this.Headers.Contains("Content-Type"))
                {
                    this.Headers.Add("Content-Type", "text/html");
                }

                // Since cookies are sent in headers, we want to make sure that
                // the collection cannot be modified after the headers are sent.
                this.Cookies.IsReadOnly = true;
                foreach (var cookie in this.Cookies)
                {
                    this.Headers.Add(
                        fieldName:  HDR_SETCOOKIE,
                        fieldValue: cookie.ToString(),
                        overwrite:  false
                        );
                }

                if (!this.Headers.Contains("Server"))
                {
                    this.Headers.Add("Server", "Zener/" + ZenerCore.Version.ToString(3), true);
                }

                // As with cookies, headers cannot be modified once we
                // start sending the response body.
                this.Headers.IsReadOnly = true;
                _nsw.Write(this.Headers.ToString());
                // The end of the header block is indicated by using two CRLFs,
                // so we need to write an extra one to our stream before the
                // body can be sent.
                _nsw.WriteLine();

                _beginRespond = true;
            }
        }
        /// <summary>
        /// Checks if the connection between client and server has
        /// been closed, and throws an exception if it has.
        /// </summary>
        /// <exception cref="System.InvalidOperationException"></exception>
        private void _CheckClosed()
        {
            if (_closed) throw new InvalidOperationException
            ("Cannot modify the response after the connection has been closed.");
        }
        /// <summary>
        /// Checks if the server has begun responding to the client,
        /// and throws if it has.
        /// </summary>
        /// <exception cref="System.InvalidOperationException"></exception>
        private void _CheckResponding()
        {
            if (_beginRespond) throw new InvalidOperationException(
                "Cannot modify the headers after the response body has been written to."
                );
        }

        /// <summary>
        /// Creates a new HttpResponse.
        /// </summary>
        /// <param name="responseStream">The stream to write the response to.</param>
        /// <param name="closeCallback">The method to call when the response is closed.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided stream does not support the
        ///     required operations.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided stream is null.
        /// </exception>
        internal HttpResponse(Stream responseStream, Action closeCallback)
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
                ("Provided stream must support reading, seeking, and writing.");
            }

            this.StatusCode = HttpStatus.OK;
            _closeCallback = closeCallback;
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
                this._CheckClosed();
                this._CheckResponding();

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
        /// Whether to enable output buffering. Output buffering delays
        /// the sending of the response body.
        /// </summary>
        public bool BufferOutput
        {
            get { return _bufferOutput; }
            set
            {
                _CheckResponding();

                if (value && _obstr == null)
                {
                    _obstr = new MemoryStream();
                }

                _bufferOutput = value;
            }
        }

        /// <summary>
        /// Writes the provided value to the response.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void Write(object value)
        {
            _CheckClosed();
            _ConditionalWriteHeaders();

            _nsw.Write(value);
        }
        /// <summary>
        /// Writes the provided string to the response.
        /// </summary>
        /// <param name="value">The string to write.</param>
        public void Write(string value)
        {
            this.Write((object)value);
        }
        /// <summary>
        /// Writes the provided bytes to the response.
        /// </summary>
        /// <param name="value"></param>
        public void Write(IEnumerable<byte> value)
        {
            _nsw.BaseStream.Write(
                value.ToArray(), 0, value.Count()
                );
        }
        /// <summary>
        /// Writes the provided values to the response in the
        /// specified format.
        /// </summary>
        /// <param name="format">The format to write the values in.</param>
        /// <param name="args">The values to write.</param>
        public void Write(string format, params object[] args)
        {
            _CheckClosed();
            _ConditionalWriteHeaders();

            _nsw.Write(format, args);
        }
        /// <summary>
        /// Writes the provided value to the response, followed
        /// by a new-line.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void WriteLine(object value)
        {
            _CheckClosed();
            _ConditionalWriteHeaders();

            _nsw.WriteLine(value);
        }
        /// <summary>
        /// Writes the provided string to the response,
        /// followed by a new-line.
        /// </summary>
        /// <param name="value">The string to write.</param>
        public void WriteLine(string value)
        {
            this.WriteLine(value as object);
        }
        /// <summary>
        /// Writes the provided values to the response in the
        /// specified format, followed by a new-line.
        /// </summary>
        /// <param name="format">The format to write the values in.</param>
        /// <param name="args">The values to write.</param>
        public void WriteLine(string format, params object[] args)
        {
            _CheckClosed();
            _ConditionalWriteHeaders();

            _nsw.WriteLine(format, args);
        }

        /// <summary>
        /// Closes the connection between the server and the client.
        /// </summary>
        public void Close()
        {
            if (!_closed)
            {
                _closeCallback();
                _closed = true;
            }
        }
    }
}
