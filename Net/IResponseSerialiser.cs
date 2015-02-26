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
    /// The interface for classes which transform an HttpResponse
    /// class in to the format to send over the network.
    /// </summary>
    public interface IResponseSerialiser
    {
        /// <summary>
        /// Whether the serialiser should buffer its
        /// output and only send when flushed.
        /// </summary>
        bool BufferOutput { get; }

        /// <summary>
        /// Enables compression based on the contents of
        /// the client's request.
        /// </summary>
        /// <param name="request">
        /// The request to examine to determine whether the
        /// client supports compression.
        /// </param>
        /// <returns>
        /// True if compression was enabled.
        /// </returns>
        bool EnableCompression(HttpRequest request);
        /// <summary>
        /// Disables compression if it is enabled.
        /// </summary>
        /// <returns>
        /// True if compression was disabled.
        /// </returns>
        bool DisableCompression();

        /// <summary>
        /// Writes bytes to the serialiser.
        /// </summary>
        /// <param name="bytes">
        /// The bytes to write to the serialiser.
        /// </param>
        void Write(byte[] bytes);

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
        /// Flushes the contents of the serialiser's buffers,
        /// if any, to the underlying Stream.
        /// </summary>
        void Flush();
        /// <summary>
        /// Causes the serialiser to perform any finalising
        /// actions.
        /// </summary>
        void Close();
    }
}
