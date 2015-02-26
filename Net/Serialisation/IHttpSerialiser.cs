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
    /// The interface for classes which transform an HttpResponse
    /// class in to the format to send over the network.
    /// </summary>
    public interface IHttpSerialiser
    {
        /// <summary>
        /// Whether the serialiser should buffer its
        /// output and only send when flushed.
        /// </summary>
        bool BufferOutput 
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
        /// IHttpSerialiser take an HttpRequest as a
        /// constructor parameter so that they may determine
        /// which, if any, compression methods they will
        /// use when this property is set to true.
        /// </remarks>
        bool EnableCompression 
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
        void WriteHeaders(IEnumerable<HttpHeader> headers);
        /// <summary>
        /// Writes bytes to the serialiser.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to write to the serialiser.
        /// </param>
        void WriteData(byte[] bytes);

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
        Stream GetStream(bool flush);
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
        void Close(bool flush);
    }
}
