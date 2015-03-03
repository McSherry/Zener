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

using McSherry.Zener.Core;
using McSherry.Zener.Core.Coding;

namespace McSherry.Zener.Net.Serialisation
{
    /// <summary>
    /// A class for serialising an HttpResponse class to
    /// an HTTP/1.1 (RFC 7230) response.
    /// </summary>
    public sealed class Rfc7230Serialiser
        : HttpSerialiser, IDisposable
    {        
        /// <summary>
        /// A class providing a set of constants for common
        /// HTTP header names.
        /// </summary>
        internal static class Headers
        {
            /// <summary>
            /// The header that will contain the encodings that the
            /// client may find acceptable.
            /// </summary>
            public const string AcceptEncoding = "Accept-Encoding";
            /// <summary>
            /// The header that indicates to the client which encoding has
            /// been applied to the response body.
            /// </summary>
            public const string ContentEncoding = "Content-Encoding";
            /// <summary>
            /// The header that indicates how the response has been encoded
            /// for transfer.
            /// </summary>
            public const string TransferEncoding = "Transfer-Encoding";

            /// <summary>
            /// The header providing the length, in bytes, of the response
            /// body.
            /// </summary>
            public const string ContentLength = "Content-Length";
            /// <summary>
            /// The header which tells the client what type of content the
            /// server is responding with.
            /// </summary>
            public const string ContentType = "Content-Type";
            /// <summary>
            /// The header that provides information about the content,
            /// such as its file-name.
            /// </summary>
            public const string ContentDisposition = "Content-Disposition";

            /// <summary>
            /// The header used by the server to provide date and time the
            /// response was generated on to the client.
            /// </summary>
            public const string Date = "Date";
            /// <summary>
            /// The header used by the server to provide the client with
            /// information about the server software in use.
            /// </summary>
            public const string Server = "Server";
            /// <summary>
            /// The header field used to send cookies.
            /// </summary>
            public const string SetCookie = "Set-Cookie";
            /// <summary>
            /// The header used by the client to send any cookies
            /// it has stored.
            /// </summary>
            public const string Cookie = "Cookie";
        }

        /// <summary>
        /// The format string used for the response line.
        /// </summary>
        private const string ResponseLineFormat = "HTTP/1.1 {0} {1}\r\n";
        /// <summary>
        /// The format string used for HTTP/1.1 headers.
        /// </summary>
        private const string HeaderFormat       = "{0}: {1}\r\n";
        // It's quite likely that we'll be using chunked
        // encoding when writing to the stream, so we might
        // as well preëmptively get the instance. Having it
        // as a private field will save us a method call on
        // each write, too.
        private static readonly IEncoder Chunker = ChunkedEncoder.Create();
        /// <summary>
        /// The bytes used as a newline in HTTP/1.1 headers.
        /// </summary>
        private static readonly byte[] HttpNewline;

        static Rfc7230Serialiser()
        {
            HttpNewline = Encoding.ASCII.GetBytes("\r\n");
        }

        /// <summary>
        /// Whether we are able to enable HTTP compression.
        /// </summary>
        private bool _canCompress;
        /// <summary>
        /// Whether we are going to use HTTP compression.
        /// </summary>
        private bool _useCompression;
        /// <summary>
        /// Whether output buffering is enabled.
        /// </summary>
        private bool _buffer;
        /// <summary>
        /// Whether we've started writing the
        /// response body to the network.
        /// </summary>
        private bool _bodyWritten;

        /// <summary>
        /// Evaluates the capabilities of the client and
        /// sets any private fields appropriately.
        /// </summary>
        /// <param name="request">
        /// The client's request.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided HttpRequest is null.
        /// </exception>
        internal void EvaluateClient(HttpRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    "The provided HttpRequest must not be null."
                    );
            }

            // Attempt to retrieve the client's 'Accept-Encoding' header. We
            // can use this to determine which headers are considered acceptable.
            var acceptEncoding = request.Headers[Headers.AcceptEncoding].FirstOrDefault();
            // We only want to do something if the client has sent an 'Accept-Encoding'
            // header. If it hasn't, no changes need be made.
            if (acceptEncoding != default(HttpHeader))
            {
                // Attempt to parse the 'Accept-Encoding' header as an ordered
                // HTTP header. This will allow us to filter out any encodings
                // the client has explicitly forbade.
                OrderedCsvHttpHeader accEncOcsv;
                try
                {
                    accEncOcsv = new OrderedCsvHttpHeader(
                                    header: acceptEncoding,
                        removeUnacceptable: true
                        );
                }
                catch (ArgumentException)
                {
                    // We can't parse the HTTP header, so we'll consider
                    // the client as not accepting any "special" encodings.
                    //
                    // Thanks to our other constructor being called before
                    // the body of this constructor, all values that would
                    // otherwise be set are defaulted to the "no encoding"
                    // values.
                    return;
                }

                // If we get here, we managed to parse at least one item
                // from the HTTP header. We now need to try to select an
                // encoder based on the items.
                //
                // By iterating through the items, we should maintain order
                // the order of preference specified by the client.
                foreach (string encName in accEncOcsv.Items)
                {
                    // Attempt to retrieve an encoder based on the name.
                    // If the value we retrieve isn't null, then we've
                    // found an encoding that both we and the client support.
                    if ((_compressor = Encoders.Get(encName)) != null)
                    {
                        // We've got an encoder, so we can now allow
                        // compression to be enabled.
                        _canCompress = true;
                        // We'll use "enabled" as a sane default for
                        // compression. There aren't a great deal of
                        // situations where you wouldn't want the
                        // output to be compressed.
                        _useCompression = true;
                        // We've found our compressor, so break out of
                        // the loop. We don't need to check for any other
                        // supported encodings.
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// The stream we'll buffer output to.
        /// </summary>
        private MemoryStream _outputBuffer;
        /// <summary>
        /// The encoder we'll be using when compressing
        /// our response.
        /// </summary>
        private IEncoder _compressor;

        /// <summary>
        /// Creates a new Rfc7230Serialiser.
        /// </summary>
        /// <param name="response">
        /// The HttpResponse to be serialised.
        /// </param>
        /// <param name="output">
        /// The stream that should underlie the serialiser,
        /// and to which any serialised data should be written.
        /// </param>
        /// <remarks>
        /// Using this constructor will result in HTTP
        /// compression being disabled, and it will not
        /// be possible to enable compression.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when one of the provided parameters is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided response stream does not
        /// support writing.
        /// </exception>
        public Rfc7230Serialiser(HttpResponse response, Stream output)
            : base(response, output)
        {
            // We haven't been provided with an HttpRequest, so it
            // isn't possible for us to determine whether the client
            // supports compression.
            _canCompress = false;
            _useCompression = false;
            _compressor = null;
            // We're disabling output buffering by default, as it doing
            // so will generally provide better performance. Output buffering
            // being disabled means we'll be sending using chunked encoding,
            // which means we can send a little at a time and the client can
            // start displaying earlier.
            _buffer = false;
            // As buffering is disabled by default, we want to null our output
            // buffer.
            _outputBuffer = null;
        }
        /// <summary>
        /// Creates a new Rfc7230Serialiser.
        /// </summary>
        /// <param name="request">
        /// The request for which this serialiser will be serialising
        /// a response. This is used to determine acceptable encodings
        /// and similar.
        /// </param>
        /// <param name="response">
        /// The HttpResponse to be serialised.
        /// </param>
        /// <param name="output">
        /// The stream that should underlie the serialiser,
        /// and to which any serialised data should be written.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when one of the provided parameters is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided response stream does not
        /// support writing.
        /// </exception>
        public Rfc7230Serialiser(
            HttpRequest request,
            HttpResponse response, Stream output
            ) : this(response, output)
        {
            this.EvaluateClient(request);
        }

        /// <summary>
        /// Whether the serialiser should buffer its output,
        /// and only send when flushed.
        /// </summary>
        public override bool BufferOutput
        {
            get { return _buffer; }
            set
            {
                base.CheckClosed();
                base.CheckCanModify();

                // If the value isn't changing, we don't need
                // to take any action.
                if (this.BufferOutput == value) return;

                // What action to take depends on whether output
                // buffering is being enabled or disabled.
                if (value)
                {
                    // Output buffering is disabled by default, so
                    // we need to create our buffer when we're enabling
                    // it.
                    _outputBuffer = new MemoryStream();
                }
                // If output buffering is being disabled, we want to
                // check whether there is any data stored in it.
                else
                {
                    if (_outputBuffer.Length == 0)
                    {
                        // If nothing's been written to the buffer and we're
                        // disabling use of it, it'd be a waste of resources
                        // to leave it there.
                        //
                        // Close and dispose the output buffering to release
                        // any resources it's using.
                        _outputBuffer.Close();
                        _outputBuffer.Dispose();
                        // Set the buffer back to null. We'll be using this
                        // null value in other methods to determine whether
                        // there is anything in the buffer to be written to
                        // the network.
                        _outputBuffer = null;
                    }
                }

                // Finally, set the private field with the value.
                _buffer = value;
            }
        }
        /// <summary>
        /// Whether HTTP compression is enabled.
        /// </summary>
        /// <remarks>
        /// Depending on how the Rfc7230Serialiser class
        /// was initialised, setting this to true may not
        /// enable compression. See constructor remarks
        /// for more details.
        /// </remarks>
        public override bool Compress
        {
            get { return _canCompress && _useCompression; }
            set
            {
                base.CheckClosed();
                base.CheckCanModify();

                // If it isn't possible to use compression,
                // there's no point making an assignment.
                if (_canCompress) _useCompression = value;
            }
        }
        /// <summary>
        /// Whether the serialiser will accept modifications
        /// to the headers, response status, connection details,
        /// and so on.
        /// </summary>
        public override bool CanModifyHeaders
        {
            get { return !_bodyWritten; }
        }

        /// <summary>
        /// Writes the specified data to the serialiser.
        /// </summary>
        /// <param name="bytes">
        /// The data to write.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided byte array of data is null.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when the serialiser has been closed.
        /// </exception>
        public override void WriteData(byte[] bytes)
        {
            this.CheckClosed();

            if (bytes == null)
            {
                throw new ArgumentNullException(
                    "The specified data must not be null."
                    );
            }

            // If we're buffering output, we aren't writing to the response
            // stream straight away. Instead, we write to a buffer in memory
            // and wait until the serialiser is flushed.
            if (this.BufferOutput)
            {
                _outputBuffer.Write(bytes, 0, bytes.Length);
            }
            else
            {
                // If we're not buffering the output, the data is, if necessary,
                // encoded, and is then written directly to the response stream.

                // Calling flush will send the headers if they have not already
                // been sent. As is to be expected, we need to send the headers
                // before we can send the body.
                this.Flush();
                // If we're compressing the data, we need to pass it through
                // the encoder first. This will give us the (typically) compressed
                // data.
                if (this.Compress)
                {
                    bytes = _compressor.Encode(bytes);
                }

                // Output buffering is enabled, which means we're sending data
                // in chunks. This means we need to pass it through out chunked
                // encoder first.
                bytes = Chunker.Encode(bytes);
                // All that's left is to write the data to the response stream.
                base.ResponseStream.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Flushes any data stored by the serialiser to the
        /// network. This includes the headers and the response
        /// body.
        /// </summary>
        /// <remarks>
        /// If this is called while output buffering is enabled,
        /// the contents of the output buffer will be flushed to
        /// the network, and output buffering will be disabled.
        /// </remarks>
        public override void Flush()
        {
            this.CheckClosed();

            // If we haven't written the body yet, it means we need
            // to write the headers. 
            if (!_bodyWritten)
            {
                // Set the field containing the bool indicating whether we've
                // written the body yet to true.
                _bodyWritten = true;

                // If compression is enabled, we need to add the appropriate
                // 'Content-Encoding' header so the client knows that the data
                // is encoded, and can determine how to decode it.
                if (this.Compress)
                {
                    // The encoder provides us with the appropriate name. As long
                    // as the encoders have been set up with the correct name, this
                    // should never throw an exception.
                    base.Response.Headers.Add(
                        fieldName:  Headers.ContentEncoding,
                        fieldValue: _compressor.Name,
                        overwrite:  true
                        );

                    // If the output has been buffered AND we're compressing, it
                    // means we need to compress the buffer before sending it.
                    if (this.BufferOutput)
                    {
                        // TODO: Test this output buffer-compressing piece of code.

                        // Retrieve the memory stream's internal buffer. This
                        // lets us directly manipulate its buffer, rather than
                        // creating a new array and copying the value.
                        byte[] buf = _outputBuffer.GetBuffer();
                        // The buffer is likely to be larger than the actual
                        // length of data in the stream, so we resize it. We will
                        // hit a problem if there is more than about 2^31 bytes in
                        // the buffer.
                        Array.Resize(ref buf, (int)_outputBuffer.Length);
                        // Compress the contents of the buffer.
                        buf = _compressor.Encode(buf);
                        // Seek to the start of the stream.
                        _outputBuffer.Position = 0;
                        // Empty the memory stream.
                        _outputBuffer.SetLength(0);
                        // Write the compressed content to the stream.
                        _outputBuffer.Write(buf, 0, buf.Length);
                        // Get any finalising data from the compressor.
                        buf = _compressor.Finalise();

                        // If the length of the finalising data is zero,
                        // there isn't anything to write.
                        if (buf.Length > 0)
                        {
                            // If the length is greater than zero, however,
                            // we do need to write the finalising data.
                            _outputBuffer.Write(buf, 0, buf.Length);
                        }
                    }
                }

                // If output is being buffered, we need to flush it from the
                // memory stream we're using as a buffer. 
                if (this.BufferOutput)
                {
                    // The output was buffered, which means we're sending it all
                    // at once. This means that we need to specify the length of
                    // the content.
                    base.Response.Headers.Add(
                        fieldName:  Headers.ContentLength,
                        fieldValue: _outputBuffer.Length.ToString(),
                        overwrite:  true
                        );
                }
                else
                {
                    // If the output isn't being buffered, it means we're going to
                    // be sending it in chunks. To do this, we need to indicate to
                    // the client that we're using chunked transfer encoding.
                    base.Response.Headers.Add(
                        fieldName:  Headers.TransferEncoding,
                        fieldValue: Chunker.Name,
                        overwrite:  true
                        );
                }

                // We're required to send a 'Date' header in a specific format
                // to indicate to the client when the response was generated.
                // Thankfully, the DateTime class provides us with a formatter
                // that will spit out an appropriately-formatted string for the
                // HTTP 'Date' header.
                base.Response.Headers.Add(
                    fieldName:  Headers.Date,
                    fieldValue: DateTime.UtcNow.ToString("R"),
                    overwrite:  true
                    );
                // While we're not required to send this header, we will send a
                // 'Server' header to advertise the name of the server and the
                // version. Who knows, maybe it could aid in troubleshooting
                // one day?
                base.Response.Headers.Add(
                    fieldName:  Headers.Server,
                    fieldValue: String.Format(
                                    "Zener/{0}",
                                    ZenerCore.Version.ToString(3)
                                    ),
                    overwrite:  true
                    );

                // We need to send a 'Content-Type' header with the response.
                // However, it is possible that the programmer has already set
                // one.
                if (!base.Response.Headers.Contains(Headers.ContentType))
                {
                    // The programmer hasn't set a 'Content-Type', we'll add
                    // a default one with the content type for HTML.
                    base.Response.Headers.Add(
                        fieldName:  Headers.ContentType,
                        fieldValue: MediaType.Html
                        );
                }

                // Cookies are sent to the client as headers. This means
                // that, before we can mark the headers read-only, we need
                // to add all the cookie headers.
                foreach (var cookie in base.Response.Cookies)
                {
                    StringBuilder cBdr = new StringBuilder();

                    // First comes the name of the cookie and the cookie's
                    // value. The cookie may or may not have a value, so we
                    // insert an empty string if the cookie doesn't have a
                    // value.
                    cBdr.AppendFormat("{0}={1}; ",
                        cookie.Name, cookie.Value ?? String.Empty
                        );

                    // All of the options after this point are optional, so
                    // all of them require checks to ensure that they have a
                    // value.

                    // The expiry instructs the user agent to discard the cookie
                    // after a certain time.
                    if (cookie.Expiry.HasValue)
                    {
                        cBdr.AppendFormat("Expires={0}; ",
                            // Expiries are given in UTC and are formatted in the
                            // same HTTP date format used in the 'Date' header.
                            cookie.Expiry.Value.ToUniversalTime().ToString("R")
                            );
                    }
                    // The Domain field instructs the client to only send this cookie
                    // with requests to a specific domain.
                    if (!String.IsNullOrWhiteSpace(cookie.Domain))
                    {
                        cBdr.AppendFormat("Domain={0}; ", cookie.Domain);
                    }
                    // The Path field specifies the path within the domain to limit
                    // the sending of this cookie to.
                    if (!String.IsNullOrWhiteSpace(cookie.Path))
                    {
                        cBdr.AppendFormat("Path={0}; ", cookie.Path);
                    }
                    
                    if (cookie.HttpOnly)
                    {
                        cBdr.Append("HttpOnly; ");
                    }
                    // This flag requires that the user agent only send the cookie
                    // over a secure connection.
                    if (cookie.Secure)
                    {
                        cBdr.Append("Secure; ");
                    }

                    base.Response.Headers.Add(
                        fieldName:  Headers.SetCookie,
                        fieldValue: cBdr.ToString().TrimEnd(' ', ';'),
                        // Each cookie is sent in a separate header field.
                        // This means that we don't want to overwrite any
                        // previous headers with this name.
                        overwrite:  false
                        );
                }

                // We're sending the headers now, so we want to make sure that
                // the programmer knows they cannot be modified. Mark the headers
                // read-only so that an attempt to modify the collection will
                // result in an exception.
                base.Response.Headers.IsReadOnly = true;

                byte[] hbuf = Encoding.ASCII
                    .GetBytes(String.Format(
                        ResponseLineFormat,
                        base.Response.StatusCode.GetCode(),
                        base.Response.StatusCode.GetMessage()
                        ));
                // The first thing we need to write is the response line. This
                // lets the client know the HTTP version of the response, and
                // provides a status code indicating the success or failure of
                // the client's request.
                base.ResponseStream.Write(hbuf, 0, hbuf.Length);
                // We now need to write out each HTTP header to the response.
                foreach (var header in base.Response.Headers)
                {
                    hbuf = Encoding.ASCII
                        .GetBytes(
                            String.Format(
                                HeaderFormat,
                                header.Field, header.Value
                            ));

                    base.ResponseStream.Write(hbuf, 0, hbuf.Length);
                }

                // Write the final terminating newline used to separate the
                // HTTP headers from the response body.
                base.ResponseStream.Write(HttpNewline, 0, HttpNewline.Length);
            }

            // If there is any data in the output buffer, we need to
            // write it out to the response first.
            if (_outputBuffer != null && _outputBuffer.Length > 0)
            {
                // Seek to the very start of the output buffer.
                _outputBuffer.Position = 0;
                // Copy the contents of the output buffer to
                // the response stream.
                _outputBuffer.CopyTo(base.ResponseStream);
            }

            // We don't have to handle closing the output stream, as
            // that will be handled in Dispose. With the output buffer
            // flushed to the response stream and the _bodyWritten property
            // set to true, our job here is done.
        }

        /// <summary>
        /// Disposes any resources held by the serialiser.
        /// </summary>
        /// <remarks>
        /// Implementations of HttpSerialiser must not dispose
        /// or close the response stream. This is handled by
        /// HttpServer. Code is written with the assumption
        /// that HttpSerialiser does not close the response
        /// stream.
        /// </remarks>
        public override void Dispose()
        {
            // If the output buffer has been used, we need to
            // close and dispose it to encourage collection of
            // the memory stream's resources.
            if (_outputBuffer != null)
            {
                _outputBuffer.Close();
                _outputBuffer.Dispose();
                _outputBuffer = null;
            }

            // We may need to send finalising data. If we do,
            // this is where it'll be stored.
            byte[] finData = new byte[0];
            // If we had a compressor/encoder, it's possible that
            // it will need to be disposed. First we perform a null
            // check to determine whether the encoder was used. If it
            // was, we then check whether it implements IDisposable.
            if (_compressor != null && _compressor is IDisposable)
            {
                // If output wasn't being buffered, it means that
                // finalising data hasn't been sent yet. We now need
                // to send it.
                if (!this.BufferOutput) finData = _compressor.Finalise();
                // If we're here, the encoder implements IDisposable.
                // We cast the encoder to IDisposable, then dispose it
                // since we will now be done with it
                ((IDisposable)_compressor).Dispose();
            }

            // If we weren't buffering output, we were using chunked
            // transfer encoding. Using chunked transfer encoding
            // means we need to send a terminating zero-size chunk
            // to indicate that we've finished sending.
            if (!this.BufferOutput)
            {
                // However, if the compressor had finalising data,
                // we need to send that first. We can tell whether
                // the compressor had finalising data by testing
                // the length of the array. An array length of
                // greater than zero means there is finalising data
                // to be sent.
                if (finData.Length == 0)
                {
                    // Chunked-encode the finalising data.
                    finData = Chunker.Encode(finData);
                    // Write the compressor's finalising data to the
                    // response stream.
                    base.ResponseStream.Write(finData, 0, finData.Length);
                }

                // Regardless of whether the compressor had finalising
                // data, we can now assign the chunker's finalising data
                // to the array.
                finData = Chunker.Finalise();
                // And, to finish, we send the chunker's finalising data.
                base.ResponseStream.Write(finData, 0, finData.Length);
            }
        }
    }
}
