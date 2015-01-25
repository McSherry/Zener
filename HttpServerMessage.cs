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
    /* HttpServerMessages include a type to identify their meaning
     * and an enumerable of arguments. The arguments contain any
     * values which are useful in handling the type of message.
     * 
     * RequestReceived Message:
     * 
     *      The RequestReceived type is emitted when the HttpServer
     *      receives a request that can be successfully parsed. Its
     *      arguments are as follows.
     *      
     *          [0]     =>  HttpRequest
     *          [1]     =>  HttpResponse
     *          
     *      
     * InvokeErrorHandler Message:
     *      
     *      This message type is emitted when the an error occurs.
     *      It may be emitted when request parsing fails, or may be
     *      emitted when a handler throws an exception inheriting
     *      from HttpException. Its arguments are given below.
     *      
     *          [0]     => HttpException
     */

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
