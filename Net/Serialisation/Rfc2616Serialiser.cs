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
    /// A class for serialising an HttpResponse instance to
    /// an HTTP/1.0 (RFC 2616) response.
    /// </summary>
    public sealed class Rfc2616Serialiser
        : Rfc7230Serialiser, IDisposable
    {
        
    }
}
