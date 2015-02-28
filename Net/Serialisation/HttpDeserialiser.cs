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
    /// The base class for implementing a deserialiser that
    /// transforms data in to an HttpRequest class.
    /// </summary>
    public abstract class HttpDeserialiser
        : IDisposable
    {
        /// <summary>
        /// The stream containing the request to deserialise.
        /// </summary>
        protected readonly Stream RequestStream;

        /// <summary>
        /// Creates a new HttpDeserialiser.
        /// </summary>
        /// <param name="input">
        /// The stream containing the HTTP request to deserialise.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the input stream provided is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided input stream does not support reading.
        /// </exception>
        public HttpDeserialiser(Stream input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(
                    "The provided input stream must not be null."
                    );
            }

            if (!input.CanRead)
            {
                throw new ArgumentException(
                    "The provided input stream must support reading."
                    );
            }

            this.RequestStream = input;
        }

        /// <summary>
        /// Releases any resources held by the deserialiser.
        /// </summary>
        /// <remarks>
        /// Implementations of Dispose should not dispose of the
        /// stream containing the request's data. This will be
        /// handled by the code creating the deserialiser
        /// (typically, this will be HttpServer).
        /// </remarks>
        public abstract void Dispose();
    }
}
