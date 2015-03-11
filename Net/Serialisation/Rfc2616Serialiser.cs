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

namespace McSherry.Zener.Net.Serialisation
{
    /// <summary>
    /// A class for serialising an HttpResponse instance to
    /// an HTTP/1.0 (RFC 2616) response.
    /// </summary>
    public sealed class Rfc2616Serialiser
        : Rfc7230Serialiser, IDisposable
    {
        /// <summary>
        /// Whether the client supports chunked encoding. If this is
        /// false, output buffering cannot be disabled.
        /// </summary>
        private bool _supportsChunked;
        /// <summary>
        /// Whether the client supports HTTP persistent connections. If
        /// this is false, the Connection will always have the value Close.
        /// </summary>
        private bool _supportsPersist;

        /// <summary>
        /// Rfc2616Serialiser's implementation of Configure
        /// as a protected method so it can be called in Configure
        /// implementations of Rfc2616Serialiser subclasses.
        /// </summary>
        /// <param name="request">The request to evaluate.</param>
        protected void Rfc2616IntlConfigure(HttpRequest request)
        {
            // If the client supports chunked encoding, it will indicate
            // it in its 'TE' header. HTTP/1.0 clients are not required, and
            // so are not guaranteed, to support chunked encoding, so we need
            // to check for it.
            var xferEnc = request.Headers[Headers.TE].LastOrDefault();
            // If the client hasn't included a 'TE' header, it doesn't really
            // matter as we default to having chunked encoding disabled.
            if (xferEnc != default(HttpHeader))
            {
                // We're going to treat the 'TE' header as an ordered CSV header,
                // and remove any options marked as unacceptable. This will leave
                // us with all the transfer encodings the client will accept.
                var ocsv = new OrderedCsvHttpHeader(xferEnc, true);
                // We then check whether the client has included a 'chunked' item
                // in its list of supported encodings.
                if (ocsv.Items.Contains(Chunker.Name, StringComparer.OrdinalIgnoreCase))
                {
                    // If it has, it supports chunked encoding. This means we can now
                    // allow the user to modify output-buffering settings.
                    _supportsChunked = true;
                    // We also enable chunked encoding, as it can provide better performance
                    // than buffering. This is as a result of it sending data as soon as it's
                    // written rather than waiting for all the data to be accumulated.
                    base._buffer = false;
                }
            }
        }

        /// <summary>
        /// Creates a new Rfc2616Serialiser.
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
        public Rfc2616Serialiser(HttpResponse response, Stream output)
            : base(response, output)
        {
            // Client's aren't required to support chunked encoding, so
            // we prevent the enabling of chunked encoding by default.
            _supportsChunked = false;
            // As we won't be sure if the client supports chunked encoding,
            // we need to enable output buffering by default.
            base._buffer = true;

            // Persistent connections are an extension to HTTP/1.0, so we
            // need to default to having them disabled.
            _supportsPersist = false;
            // We also have to set the connection property to ensure that
            // connections are closed by default.
            base._connection = HttpConnection.Close;
        }

        /// <summary>
        /// Whether the serialiser should buffer its output,
        /// and only send when flushed. If the client does not
        /// support chunked encoding, this will always be true.
        /// </summary>
        public override bool BufferOutput
        {
            get
            {
                return base.BufferOutput;
            }
            set
            {
                base.CheckClosed();
                base.CheckCanModify();

                // If the client doesn't support chunked encoding,
                // we cannot allow the user to disable output buffering.
                if (!_supportsChunked) return;

                // If the client does support chunked encoding, it doesn't
                // matter whether the user enables or disables output buffering.
                base.BufferOutput = value;
            }
        }
        /// <summary>
        /// Instructs the serialiser on how to handle the connection
        /// once the response has been serialised and sent.
        /// </summary>
        public override HttpConnection Connection
        {
            get
            {
                return base.Connection;
            }
            set
            {
                base.CheckClosed();
                base.CheckCanModify();

                // If the user is trying to enable persistent connections but the
                // client has not reported support for persistent connections, we
                // can't allow it to be enabled.
                //
                // We do this because, unlike with HTTP/1.1, HTTP/1.0 does not, as
                // part of the specification, include support for persistent
                // connections. Instead, persistent connections are an extension to
                // HTTP/1.0, and support for them must be reported explicitly.
                if (!_supportsPersist) return;

                base.Connection = value;
            }
        }

        /// <summary>
        /// Evaluates the capabilities advertised by the client
        /// in the provided request and makes any relevant changes
        /// to the serialiser's configuration.
        /// </summary>
        /// <param name="request">
        /// The request to evaluate the client's capabilities from.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided HttpRequest is null.
        /// </exception>
        public override void Configure(HttpRequest request)
        {
            // Rfc7230Serialiser performs checks that we would
            // perform anyway, so we can call its Configure method.
            base.Rfc7230IntlConfigure(request);
            // We then use our own configuration method to do the
            // rest of the configuring that needs to be done.
            this.Rfc2616IntlConfigure(request);
        }
    }
}
