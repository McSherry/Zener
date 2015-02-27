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
    public abstract class HttpSerialiser
        : IDisposable
    {
        /// <summary>
        /// A class providing a set of constants for common
        /// HTTP header names.
        /// </summary>
        protected internal static class Headers
        {
            /// <summary>
            /// The header that will contain the encodings that the
            /// client may find acceptable.
            /// </summary>
            public const string AcceptEncoding      = "Accept-Encoding";
        }
        /// <summary>
        /// A class containing methods for creating the encoders
        /// used in response to the contents of a client's
        /// 'Accept-Encoding' header.
        /// </summary>
        protected internal static class Encoders
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
                    )
                    {

                    };

                // We need a parameterless constructor for this stuff
                // to work, so we check all of the types in the encoder
                // dictionary to make sure that they all have one. If
                // one or more doesn't, throw an exception.
                if (_encNamesAndTypes.Values.Any(
                    T => T.GetConstructor(Type.EmptyTypes) == null
                    ))
                {
                    throw new ApplicationException(
                        "All IEncoders must have a parameterless constructor."
                        );
                }
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
        protected void CheckClosed()
        {
            if (this.IsClosed)
            {
                throw new InvalidOperationException(
                    "The serialiser has been closed."
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
        public abstract bool EnableCompression
        {
            get;
            set;
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

            // If we haven't, we now need to mark it
            // as closed.
            this.IsClosed = true;
            // If we are to flush data to the network,
            // we need to call the method flush.
            //
            // If we're not flushing, we don't need to
            // do anything more.
            if (flush) this.Flush();
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
        public abstract void Dispose();
    }
}
