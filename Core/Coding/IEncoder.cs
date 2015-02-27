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

namespace McSherry.Zener.Core.Coding
{
    /// <summary>
    /// The interface for classes which implement a 
    /// streaming encoder.
    /// </summary>
    /// <remarks>
    /// This interface is intended to be used by encoders which
    /// can be streamed, and can produce partial output for a
    /// partial input by means of stored internal state.
    /// </remarks>
    public interface IEncoder
    {
        /// <summary>
        /// The name of the encoding this encoder encodes
        /// to.
        /// </summary>
        /// <remarks>
        /// If this encoding has an 'Accept-Encoding' value
        /// associated with it, that value should be used as
        /// the name.
        /// </remarks>
        string Name
        {
            get;
        }

        /// <summary>
        /// Encodes the provided data.
        /// </summary>
        /// <param name="data">
        /// The data to encode.
        /// </param>
        /// <returns>
        /// The encoded data as a byte array.
        /// </returns>
        byte[] Encode(byte[] data);
        /// <summary>
        /// Performs any required finalising action.
        /// </summary>
        /// <returns>
        /// A byte array containing any finalisation
        /// data. If no finalisation data need be
        /// appended, this will return an empty byte
        /// array.
        /// </returns>
        byte[] Finalise();
    }
}
