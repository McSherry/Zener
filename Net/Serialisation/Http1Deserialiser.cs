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

namespace McSherry.Zener.Net.Serialisation
{
    /// <summary>
    /// A deserialiser for HTTP/1.x-series protocols.
    /// </summary>
    /// <remarks>
    /// This deserialiser is based on the HTTP/1.1 standard,
    /// but should function properly when deserialising HTTP/1.0.
    /// </remarks>
    public sealed class Http1Deserialiser
        : HttpDeserialiser, IDisposable
    {
        /// <summary>
        /// Disposes any resources held by the deserialiser.
        /// This method is not implemented.
        /// </summary>
        public override void Dispose()
        {
            return;
        }
    }
}
