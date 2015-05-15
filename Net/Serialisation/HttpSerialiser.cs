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
    /// The base class for classes implementing an HttpResponse
    /// serialiser.
    /// </summary>
    /// <remarks>
    /// The values of all public properties must be retained even
    /// after closing. Implementations should prohibit modification
    /// after closing, but must allow retrieval.
    /// </remarks>
    public abstract class HttpSerialiser
        : IDisposable
    {
        /// <summary>
        /// A class containing methods for creating the encoders
        /// used in response to the contents of a client's
        /// 'Accept-Encoding' header.
        /// </summary>
        public static class Encoders
        {
            /// <summary>
            /// The names (case-insensitive) of the encodings with the
            /// associated types that implement them.
            /// </summary>
            private static Dictionary<string, Type> _encNamesAndTypes;

            static Encoders()
            {
                // We're going to consider names to be case-insensitive.
                _encNamesAndTypes = new Dictionary<string, Type>(
                    StringComparer.OrdinalIgnoreCase
                    );
            }

            /// <summary>
            /// Determines whether there is an encoder for the specified
            /// encoding based on the encoding's name.
            /// </summary>
            /// <param name="encodingName">
            /// The name of the encoding.
            /// </param>
            /// <returns>
            /// True if we have an encoder for the specified encoding name.
            /// </returns>
            public static bool Contains(string encodingName)
            {
                return _encNamesAndTypes.ContainsKey(encodingName);
            }
            /// <summary>
            /// Attempts to retrieve an encoder by name.
            /// </summary>
            /// <param name="encodingName">
            /// The name of the encoding to retrieve the encoder for.
            /// </param>
            /// <returns>
            /// Null if no encoder for the specified encoding was found,
            /// otherwise an appropriate instance of IEncoder.
            /// </returns>
            public static IEncoder Get(string encodingName)
            {
                // Attempt to retrieve the type associated with
                // the provided encoding name.
                Type T;
                if (!_encNamesAndTypes.TryGetValue(encodingName, out T))
                {
                    // If we can't retrieve it (i.e. we don't have an encoding
                    // by that name), return null.
                    return null;
                }

                // If we can retrieve an encoder, create an instance of it
                // and return it to the caller.
                return (IEncoder)Activator.CreateInstance(T);
            }
            /// <summary>
            /// Adds an encoder to the list of encoders.
            /// </summary>
            /// <typeparam name="T">The type of the encoder to add.</typeparam>
            /// <param name="name">
            /// The name to register the encoder with. This must be the name
            /// used in the "Accept-Encoding" header sent by the client. This
            /// name is case-insensitive.
            /// </param>
            public static void Register<T>(string name)
                where T : IEncoder, new()
            {
                _encNamesAndTypes[name] = typeof(T);
            }
        }

        /// <summary>
        /// Creates an HttpSerialiser instance based on the provided
        /// version.
        /// </summary>
        /// <param name="httpVersion">
        /// The version of HTTP to create a serialiser for. Only the
        /// major and minor versions are considered.
        /// </param>
        /// <param name="response">
        /// The HttpResponse to be serialised.
        /// </param>
        /// <param name="output">
        /// The Stream to write the serialised data to.
        /// </param>
        /// <returns>
        /// An HttpSerialiser instance for the specified HTTP version.
        /// </returns>
        public static HttpSerialiser Create(
            Version httpVersion,
            HttpResponse response, Stream output
            )
        {
            if (httpVersion == null)
            {
                throw new ArgumentNullException(
                    "The provided Version must not be null."
                    );
            }

            if (response == null)
            {
                throw new ArgumentNullException(
                    "The provided HttpResponse must not be null."
                    );
            }

            if (output == null)
            {
                throw new ArgumentNullException(
                    "The provided output Stream must not be null."
                    );
            }

            // We first need to switch based on the major version.
            switch (httpVersion.Major)
            {
                // We're using HTTP/1.x as the default, so any
                // unrecognised versions will end up going to
                // here.
                case 1:
                default:
                {
                    // Now that we know our major version, we can
                    // narrow it down using the minor version.
                    switch (httpVersion.Minor)
                    {
                        /* Right now we only support HTTP/1.1, so
                         * any HTTP/1.x protocols get pushed through 
                         * here.
                        **/
                        default:
                        /* RFC 7230 is the first (of several) RFCs that
                         * specify HTTP/1.1, and RFC 2616 is the analogue
                         * for HTTP/1.0. We're using RFC naming to avoid
                         * hard-to-read class names. Consider:
                         * 
                         *      +------------------+-------------------+
                         *      |     Version      |        RFC        |
                         *      |------------------+-------------------|
                         *      | Http10Serialiser | Rfc2616Serialiser |
                         *      | Http11Serialiser | Rfc7230Serialiser |
                         *      +------------------+-------------------+
                         * 
                         * Although the version is not immediately
                         * evident if you don't know the RFC numbers, I
                         * would argue that RFC-based class names are
                         * easier to read when you know what you're after.
                        **/
                        case 0: return new Rfc2616Serialiser(response, output);
                        case 1: return new Rfc7230Serialiser(response, output);
                    }
                }
            }
        }
        /// <summary>
        /// Creates an HttpSerialiser instance based on the provided
        /// version and configures it using the provided HttpRequest.
        /// </summary>
        /// <param name="httpVersion">
        /// The version of HTTP to create a serialiser for. Only the
        /// major and minor versions are considered.
        /// </param>
        /// <param name="request">
        /// The HttpRequest to use when configuring the serialiser.
        /// </param>
        /// <param name="response">
        /// The HttpResponse to be serialised.
        /// </param>
        /// <param name="output">
        /// The Stream to write the serialised data to.
        /// </param>
        /// <returns>
        /// An HttpSerialiser instance for the specified HTTP version.
        /// </returns>
        public static HttpSerialiser Create(
            Version httpVersion,
            HttpRequest request, HttpResponse response,
            Stream output
            )
        {
            // Create a serialiser using the version/etc that was
            // passed to the method.
            var httpSer = HttpSerialiser.Create(httpVersion, response, output);
            // Configure the created serialiser using the provided
            // HttpRequest.
            httpSer.Configure(request);
            // Return the created+configured serialiser.
            return httpSer;
        }

        /// <summary>
        /// The stream to which any response data should be
        /// written.
        /// </summary>
        protected readonly Stream ResponseStream;
        /// <summary>
        /// The HttpResponse that we will be serialising.
        /// </summary>
        protected readonly HttpResponse Response;

        /// <summary>
        /// Checks whether the serialiser has been closed,
        /// and throws an exception if it has been.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when the serialiser has been closed.
        /// </exception>
        protected internal void CheckClosed()
        {
            if (this.IsClosed)
            {
                throw new InvalidOperationException(
                    "The serialiser has been closed."
                    );
            }
        }
        /// <summary>
        /// Checks whether the serialiser is accepting modifications
        /// to its headers, and throws an exception if it is not.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when the serialiser is not accepting modifications
        /// to the headers.
        /// </exception>
        protected internal void CheckCanModify()
        {
            if (!this.CanModifyHeaders)
            {
                throw new InvalidOperationException(
                    "The serialiser is not accepting header modifications."
                    );
            }
        }

        /// <summary>
        /// Creates a new HttpSerialiser.
        /// </summary>
        /// <param name="response">
        /// The HttpResponse to be serialised.
        /// </param>
        /// <param name="output">
        /// The Stream that should underlie the serialiser,
        /// and to which any serialised response data should
        /// be written.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided response stream is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided response stream does not
        /// support writing.
        /// </exception>
        public HttpSerialiser(HttpResponse response, Stream output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(
                    "The provided response stream must not be null."
                    );
            }

            if (response == null)
            {
                throw new ArgumentNullException(
                    "The provided HttpResponse must not be null."
                    );
            }

            if (!output.CanWrite)
            {
                throw new ArgumentException(
                    "The provided response stream must support writing."
                    );
            }

            this.ResponseStream = output;
            this.Response = response;
            this.Response.Serialiser = this;
        }

        /// <summary>
        /// Instructs the serialiser on how to handle the connection
        /// once the response has been serialised and sent.
        /// </summary>
        public abstract HttpConnection Connection
        {
            get;
            set;
        }
        /// <summary>
        /// Whether the serialiser should buffer its
        /// output and only send when flushed.
        /// </summary>
        public abstract bool BufferOutput
        {
            get;
            set;
        }
        /// <summary>
        /// Whether the serialiser should compress any
        /// data the protocol specifies may be compressed.
        /// </summary>
        /// <remarks>
        /// It is recommended that implementations of
        /// IResponseSerialiser take an HttpRequest as a
        /// constructor parameter so that they may determine
        /// which, if any, compression methods they will
        /// use when this property is set to true.
        /// </remarks>
        public abstract bool Compress
        {
            get;
            set;
        }
        /// <summary>
        /// Whether the serialiser will accept modifications
        /// to the headers, response status, connection details,
        /// and so on.
        /// </summary>
        public abstract bool CanModifyHeaders
        {
            get;
        }
        /// <summary>
        /// Whether the serialiser has been closed. This
        /// does not necessarily indicate that the stream
        /// has been closed.
        /// </summary>
        public virtual bool IsClosed
        {
            get;
            protected set;
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
        public abstract void Configure(HttpRequest request);
        
        /// <summary>
        /// Writes data to the serialiser. This data will
        /// generally be placed in the response body.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to write to the serialiser.
        /// </param>
        public abstract void WriteData(byte[] bytes);

        /// <summary>
        /// <para>
        /// Retrieves the underlying response stream the serialiser
        /// is writing to. It is recommended to use Write unless you
        /// know you need direct Stream access.
        /// </para>
        /// <para>
        /// Calling this method will close the serialiser, but will
        /// leave the underlying Stream open to be written to.
        /// </para>
        /// </summary>
        /// <param name="flush">
        /// Whether the serialiser should flush its buffer to the
        /// network before providing the stream. If this is false,
        /// any data stored by the serialiser is discarded.
        /// </param>
        /// <returns>
        /// The Stream the serialiser is writing to.
        /// </returns>
        public virtual Stream GetStream(bool flush)
        {
            // If the serialiser is not already closed,
            // close it and pass on the parameter "flush."
            if (!this.IsClosed) this.Close(flush);

            // Return the response stream that underlies
            // this serialiser.
            return this.ResponseStream;
        }
        /// <summary>
        ///     <para>
        ///     Retrieves the underlying response stream the serialiser
        ///     is writing to, and discards any data buffered by the
        ///     serialiser. It is recommended to use Write unless you
        ///     know you need direct Stream access.
        ///     </para>
        ///     <para>
        ///     Calling this method will close the serialiser, but will
        ///     leave the underlying Stream open to be written to.
        ///     </para>
        /// </summary>
        /// <returns>
        /// The Stream the serialiser is writing to.
        /// </returns>
        public Stream GetStream()
        {
            return this.GetStream(flush: false);
        }
        /// <summary>
        /// Flushes any data stored by the serialiser to the
        /// network. This includes the headers and the response
        /// body.
        /// </summary>
        public abstract void Flush();
        /// <summary>
        /// Causes the serialiser to perform any finalising
        /// actions.
        /// </summary>
        /// <param name="flush">
        /// Whether the serialiser should flush its buffer to the
        /// network before performing the finalising actions. If
        /// this is false, any data stored by the serialiser is
        /// discarded.
        /// </param>
        public virtual void Close(bool flush)
        {
            // If we've already closed the serialiser,
            // we've got nothing to do.
            if (this.IsClosed) return;

            // If we are to flush data to the network,
            // we need to call the method flush.
            //
            // If we're not flushing, we don't need to
            // do anything more.
            if (flush) this.Flush();
            // We've just closed the serialiser, and we
            // are safe in assuming that dispose will not
            // close the response stream, so we can call
            // it since any serialiser-internal resources
            // won't be getting used after closing.
            this.Dispose();
            // This needs to be set here, otherwise
            // Flush will throw an exception.
            this.IsClosed = true;
        }
        /// <summary>
        /// Causes the serialiser to perform any finalising
        /// actions and to discard any buffered data.
        /// </summary>
        public void Close()
        {
            this.Close(flush: false);
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
        public abstract void Dispose();
    }
}
