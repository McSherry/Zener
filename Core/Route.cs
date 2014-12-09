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
using System.Threading.Tasks;

using SynapLink.Zener.Net;

namespace SynapLink.Zener.Core
{
    /// <summary>
    /// The delegate used with handler functions for routes.
    /// </summary>
    /// <param name="urlParams">Any parameters included in the URL.</param>
    public delegate void RouteHandler(
        HttpRequest request, HttpResponse response, 
        Dictionary<string, string> routeParam
        );

    /// <summary>
    /// A class representing a single route, to be used with Zener's router.
    /// </summary>
    public class Route
    {
        private RouteHandler _handler;
        private string _format;
        private string _name;

        /// <summary>
        /// Creates a new route.
        /// </summary>
        /// <param name="format">The format to be associated with this route.</param>
        /// <param name="handler">The handler to be associated with this route.</param>
        public Route(string format, RouteHandler handler)
            : this(format, handler, false) { }
        /// <summary>
        /// Creates a new route.
        /// </summary>
        /// <param name="format">The format to be associated with this route.</param>
        /// <param name="handler">The handler to be associated with this route.</param>
        /// <param name="caseInsensitive">Whether the format should be case-insensitive.</param>
        public Route(string format, RouteHandler handler, bool caseInsensitive)
        {
            _format = format.ToLower().Trim(' ', '/');
            _handler = handler;
            _name = this.Format;
        }

        /// <summary>
        /// The name of the route. If no name is specified, defaults
        /// to the route's format.
        /// </summary>
        public string Name
        {
            get { return _name; }
        }
        /// <summary>
        /// The format that should be associated with this route.
        /// </summary>
        public string Format
        {
            get { return _format; }
        }
    }
}
