/*
 *      Copyright (c) 2014-2015, SynapLink, LLC
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SynapLink.Zener
{
    /// <summary>
    /// The type of message from the HTTP server.
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// The type used when the server receives a successful request.
        /// </summary>
        RequestReceived,
        /// <summary>
        /// The type used when the server wishes to invoke an error handler.
        /// </summary>
        InvokeErrorHandler
    }

    /// <summary>
    /// The struct used to represent a message from the HTTP server.
    /// </summary>
    public struct HttpServerMessage
    {
        /// <summary>
        /// Create a new HttpServerMessage.
        /// </summary>
        public HttpServerMessage(MessageType type, IEnumerable<object> args)
        {
            this.Type = type;
            this.Arguments = args;
        }

        /// <summary>
        /// The message's type.
        /// </summary>
        public readonly MessageType Type;
        /// <summary>
        /// The arguments/data passed with the message.
        /// </summary>
        public readonly IEnumerable<object> Arguments;
    }
}
