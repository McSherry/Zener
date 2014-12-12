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
    /// A class used to route requests to their handlers.
    /// </summary>
    public class Router
    {
        private List<Route> _routes;

        public Router()
        {
            _routes = new List<Route>();
        }

        /// <summary>
        /// Attempts retrieve the handler for the specified path.
        /// </summary>
        /// <param name="path">The path to find a handler for.</param>
        /// <param name="handler">The handler for the path.</param>
        /// <returns>True if retrieval was successful.</returns>
        public bool TryFind(string path, out HttpRequestHandler handler)
        {
            /*
             * It should be possible to have routes with
             * variable and non-variable sections in the
             * same location. For example:
             * 
             *      /users/list
             *      /users/[username]
             *      
             * In this case, "/users/list" will have greater
             * "priority" than "/users/[username]," as failure
             * to have this would result in "list" being
             * inaccessible.
             */

            List<dynamic> validParams = new List<dynamic>();

            var validHandlers = _routes
                .Where(r => r.TryMatch(path, validParams.Add))
                .ToList();

            if (validHandlers.Count == 0)
            {
                handler = null;
                return false;
            }

            var rhandler = validHandlers
                .Zip(validParams, (r, pr) => new { Route = r, Params = pr })
                .OrderBy(hwp => ((IDictionary<string, object>)hwp.Params).Count > 0)
                .FirstOrDefault()
                ;

            handler = new HttpRequestHandler(
                (req, res) => rhandler.Route.Handler(req, res, rhandler.Params)
                );

            return rhandler != default(object);
        }

        /// <summary>
        /// Adds a route to a handler to the router's route-set.
        /// </summary>
        /// <param name="format">The format to be associated with the route.</param>
        /// <param name="handler">The handler to be associated with the route.</param>
        public void AddHandler(string format, RouteHandler handler)
        {
            this.AddHandler(format, handler, format);
        }
        /// <summary>
        /// Adds a route to a handler to the router's route-set.
        /// </summary>
        /// <param name="format">The format to be associated with the route.</param>
        /// <param name="handler">The handler to be associated with the route.</param>
        /// <param name="name">The name to give the route.</param>
        public void AddHandler(string format, RouteHandler handler, string name)
        {
            _routes.Add(new Route(format, handler) { Name = name });
        }
    }
}
