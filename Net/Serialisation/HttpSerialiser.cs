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
    /// The base class for classes implementing an HttpResponse
    /// serialiser.
    /// </summary>
    public abstract class HttpSerialiser
        : IDisposable
    {
        /// <summary>
        /// The stream to which any response data should be
        /// written.
        /// </summary>
        protected readonly Stream ResponseStream;

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
        public HttpSerialiser(Stream response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(
                    "The provided response stream must not be null."
                    );
            }

            if (!response.CanWrite)
            {
                throw new ArgumentException(
                    "The provided response stream must support writing."
                    );
            }

            this.ResponseStream = response;
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
        /// Writes the specified headers to the
        /// serialiser.
        /// </summary>
        /// <param name="headers">
        /// The HTTP headers to write to the serialiser.
        /// </param>
        public abstract void WriteHeaders(IEnumerable<HttpHeader> headers);
        /// <summary>
        /// Writes a single header to the serialiser.
        /// </summary>
        /// <param name="header">
        /// The HTTP header to write to the serialiser.
        /// </param>
        public virtual void WriteHeader(HttpHeader header)
        {
            this.WriteHeaders(new[] { header });
        }
        /// <summary>
        /// Writes bytes to the serialiser.
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
