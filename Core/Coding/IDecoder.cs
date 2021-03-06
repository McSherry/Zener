﻿/*
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
    /// streaming decoder.
    /// </summary>
    /// <remarks>
    /// This interface is intended to be used by
    /// encoders which can be streamed, and can
    /// produce partial output for a partial input
    /// by means of stored internal state.
    /// </remarks>
    public interface IDecoder
    {        
        /// <summary>
        /// The name of the encoding this decoder decodes
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
        /// Decodes the provided data.
        /// </summary>
        /// <param name="data">
        /// The data to decode.
        /// </param>
        /// <param name="startIndex">
        /// The index within the array to begin decoding
        /// from.
        /// </param>
        /// <param name="count">
        /// The number of bytes in the array, from the start
        /// index, to decode.
        /// </param>
        /// <returns>
        /// The decoded data as a byte array.
        /// </returns>
        byte[] Decode(byte[] data, int startIndex, int count);
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
