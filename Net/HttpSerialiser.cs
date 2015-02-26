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

namespace McSherry.Zener.Net
{
    /// <summary>
    /// The recommended base class for classes implementing a
    /// subclass of IHttpSerialiser.
    /// </summary>
    public abstract class HttpSerialiser
        : IHttpSerialiser
    {
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
        /// Retrieves the underlying response stream the serialiser
        /// is writing to. It is recommended to use Write unless you
        /// know you need direct Stream access.
        /// </summary>
        /// <param name="flush">
        /// Whether the serialiser should flush its buffer to the
        /// network before providing the stream. If this is false,
        /// any data stored by the serialiser is discarded.
        /// </param>
        /// <returns>
        /// The Stream the serialiser is writing to.
        /// </returns>
        public abstract Stream GetStream(bool flush);
        /// <summary>
        /// Retrieves the underlying response stream the serialiser
        /// is writing to, and discards any data buffered by the
        /// serialiser. It is recommended to use Write unless you
        /// know you need direct Stream access.
        /// </summary>
        /// <returns>
        /// The Stream the serialiser is writing to.
        /// </returns>
        public Stream GetStream()
        {
            return this.GetStream(flush: false);
        }
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
        public abstract void Close(bool flush);
        /// <summary>
        /// Causes the serialiser to perform any finalising
        /// actions and to discard any buffered data.
        /// </summary>
        public void Close()
        {
            this.Close(flush:false);
        }
    }
}
