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
        /*
         * Routes use a format, based on the path requested by
         * the client. Formats are case-insensitive, and leading
         * and trailing forward-slashes (/) are dropped. For
         * example, the following paths are considered identical
         * by the router:
         * 
         *      /test/path/
         *      /test/path
         *      test/path
         *      
         * Additionally, routes may contain wild-card parameters.
         * These parameters are passed to the route handler in a
         * dictionary. Parameters are identified by a set of square
         * brackets ([]) with the name enclosed. Examples are
         * given below.
         * 
         *      user/[username]
         *      user/[username]/profile
         *      user/[username]/messages/[message-id]
         * 
         * Parameter values must not contain forward-slashes.
         * Parameter names are converted to lowercase (for example,
         * "[USERname]" becomes "[username]").Parameter values
         * retain their casing.
         */

        private RouteHandler _handler;
        private string _format;
        private string _name;

        /// <summary>
        /// Creates a new route.
        /// </summary>
        /// <param name="format">The format to be associated with this route.</param>
        /// <param name="handler">The handler to be associated with this route.</param>
        public Route(string format, RouteHandler handler)
        {
            _format = format.ToLower().Trim(' ', '/');
            _handler = handler;
            this.Name = this.Format;
        }

        /// <summary>
        /// Determines whether the route matches a path.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <returns>True if the route matches the path.</returns>
        public bool Matches(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The name of the route. If no name is specified, defaults
        /// to the route's format.
        /// </summary>
        public string Name
        {
            get;
            set;
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
