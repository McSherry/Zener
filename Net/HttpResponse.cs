/*
 *      Copyright (c) 2014, SynapLink, LLC
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE.txt, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// A class providing the functionality required for a handler to
    /// respond to an HTTP request.
    /// </summary>
    public class HttpResponse
    {
        private Tuple<int, string> _httpStatus;
        private List<IHttpHeader> _headers;
        private TcpClient _tcl;
        // Set to true when the first write is made. When this is
        // true, it indicates that the response headers have been
        // sent to the client.
        private bool _beginRespond;

        internal HttpResponse(TcpClient tcl)
        {
            this.StatusCode = new Tuple<int, string>(200, "OK");
            _tcl = tcl;
            _headers = new List<IHttpHeader>();
        }

        /// <summary>
        /// The HTTP status code to be returned by the server.
        /// </summary>
        /// <exception cref="System.ArgumentException"></exception>
        /// <exception cref="System.InvalidOperationException"></exception>
        public Tuple<int, string> StatusCode
        {
            get { return _httpStatus; }
            set
            {
                if (_beginRespond) throw new InvalidOperationException
                ("Cannot modify status code after the response body has been written to.");

                if (value.Item1 > 0 && value.Item1 < 1000)
                    throw new ArgumentException
                    ("Invalid HTTP status code provided.");

                if (value.Item2.Any(c => c == '\r' || c == '\n'))
                    throw new ArgumentException
                    ("HTTP status message cannot contain CRLF.");

                _httpStatus = value;
            }
        }

        /// <summary>
        /// Adds a header to the HTTP response.
        /// </summary>
        /// <param name="header">The header to add.</param>
        /// <param name="overwrite">Whether to overwrite any previous headers with the same field name.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void SetHeader(IHttpHeader header, bool overwrite)
        {
            if (_beginRespond) throw new InvalidOperationException
            ("Cannot set headers after response body has been written to.");

            if (overwrite)
            {
                _headers.RemoveAll(h => h.Field == header.Field);
            }

            _headers.Add(header);
        }
    }
}
