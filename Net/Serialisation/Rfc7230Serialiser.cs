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

using McSherry.Zener.Core.Coding;

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
        /// Checks whether the writing of the body has
        /// already started, and throws an exception if
        /// it has.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when the writing of the body has started.
        /// </exception>
        private void CheckWritten()
        {
            if (_bodyWritten)
            {
                throw new InvalidOperationException(
                    "The response body has been sent, or is being sent."
                    );
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
        /// Thrown when the provided response stream is null.
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
        /// Thrown when the provided response stream is null.
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
                    goto endOfAcceptEncodingParsing;
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

            // We use this label to get to the end of this 'if' statement. This
            // is more future-proof than a 'return', as it means we can add further
            // code below this 'if' statement without modifying this code.
            endOfAcceptEncodingParsing: ;
            }
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
                this.CheckWritten();

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
        public override bool EnableCompression
        {
            get { return _canCompress && _useCompression; }
            set
            {
                base.CheckClosed();
                this.CheckWritten();

                // If it isn't possible to use compression,
                // there's no point making an assignment.
                if (_canCompress) _useCompression = value;
            }
        }
    }
}
