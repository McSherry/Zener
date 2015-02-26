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

namespace McSherry.Zener.Net.Serialisation
{
    /// <summary>
    /// A class for serialising an HttpResponse class to
    /// an HTTP/1.1 (RFC 7230) response.
    /// </summary>
    public sealed class Rfc7230Serialiser
        : HttpSerialiser
    {
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
        /// Creates a new Rfc7230Serialiser instance.
        /// </summary>
        /// <remarks>
        /// Using this constructor will result in HTTP
        /// compression being disabled, and it will not
        /// be possible to enable compression.
        /// </remarks>
        public Rfc7230Serialiser()
        {
            // We haven't been provided with an HttpRequest, so it
            // isn't possible for us to determine whether the client
            // supports compression.
            _canCompress = false;
            _useCompression = false;
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

                throw new NotImplementedException();
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
        public override bool EnableCompression
        {
            get { return _canCompress && _useCompression; }
            set
            {
                base.CheckClosed();

                // If it isn't possible to use compression,
                // there's no point making an assignment.
                if (_canCompress) _useCompression = value;
            }
        }
        /// <summary>
        /// Whether the serialiser has been closed.
        /// </summary>
        public override bool IsClosed
        {
            get;
            private set;
        }
    }
}
