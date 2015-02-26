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
using System.Threading.Tasks;

using McSherry.Zener.Net;

namespace McSherry.Zener.Core
{
    /// <summary>
    /// A class used to route requests to their handlers.
    /// </summary>
    public sealed class Router
        : ICollection<Route>
    {
        private List<Route> _routes;
        private MediaTypeMap _map;
        private object _lockbox;

        /// <summary>
        /// Creates a new Router class.
        /// </summary>
        public Router()
        {
            _routes = new List<Route>();
            this.MediaTypes = MediaTypeMap.Default.Copy();
            _lockbox = new object();
        }

        /// <summary>
        /// Retrieves all handlers that match the specified path,
        /// ordered by the best match.
        /// </summary>
        /// <param name="path">The path to find matches for.</param>
        /// <param name="method">
        /// An HTTP request method to attempt to match with, if any.
        /// </param>
        /// <returns>A list containing all matches.</returns>
        public IList<Tuple<Route, dynamic>> Find(string path, string method = null)
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
            List<Route> validHandlers;

            List<Route> routes;
            lock (_lockbox)
            {
                // Create a copy of the routes. We want to reduce the
                // time spent locking, and the eaiest way to do this
                // is to lock during the copy then run the intensive
                // method (TryMatch) outside the lock. This might have
                // the disadvantage of increased memory usage.
                routes = new List<Route>(_routes);
            }

            validHandlers = routes
                .Where(r => r.TryMatch(path, method, validParams.Add))
                .ToList();

            if (validHandlers.Count == 0)
            {
                return new Tuple<Route, dynamic>[0].ToList();
            }

            return validHandlers
                .Zip(validParams, (r, p) => new Tuple<Route, dynamic>(r, p))
                .OrderByDescending(testc => testc.Item2 is Empty)
                .ToList()
                ;
        }
        /// <summary>
        /// Attempts to retrieve the handler for a route with the
        /// specified name.
        /// </summary>
        /// <param name="name">The name of the route.</param>
        /// <param name="parameters">The parameters to pass to the route.</param>
        /// <param name="handler">This will contain the handler if successful, null if otherwise.</param>
        /// <returns>True if a handler was found.</returns>
        public bool TryFind(
            string name, dynamic parameters,
            out HttpRequestHandler handler
            )
        {
            Route named;
            lock (_lockbox)
            {
                named = _routes
                    .Where(r => r.Name.Equals(name, StringComparison.Ordinal))
                    .FirstOrDefault();
            }

            bool ret = named == default(Route);
            if (ret)
            {
                handler = null;
            }
            else
            {
                handler = (rq, rs) => named.Handler(rq, rs, parameters);
            }

            return ret;
        }

        /// <summary>
        /// A mapping of media types to file extensions. Used when
        /// serving files to determine the media type to transmit
        /// them with.
        /// </summary>
        public MediaTypeMap MediaTypes
        {
            get { return _map; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(
                        "The media type map must not be set to null."
                        );
                }

                _map = value;
            }
        }

        /// <summary>
        /// Adds a route to a handler to the router's route-set.
        /// </summary>
        /// <param name="format">The format to be associated with the route.</param>
        /// <param name="handler">The handler to be associated with the route.</param>
        public void AddHandler(string format, RouteHandler handler)
        {
            this.AddHandler(format, new string[0], handler);
        }
        /// <summary>
        /// Adds a route to a handler to the router's route-set.
        /// </summary>
        /// <param name="format">The format to be associated with the route.</param>
        /// <param name="method">The method for this route to be constrained to.</param>
        /// <param name="handler">The handler to be associated with the route.</param>
        public void AddHandler(string format, string method, RouteHandler handler)
        {
            this.AddHandler(format, new string[1] { method }, handler);
        }
        /// <summary>
        /// Adds a route to a handler to the router's route-set.
        /// </summary>
        /// <param name="format">The format to be associated with the route.</param>
        /// <param name="methods">The methods for this route to be constrained to.</param>
        /// <param name="handler">The handler to be associated with the route.</param>
        public void AddHandler(
            string format,
            IEnumerable<string> methods,
            RouteHandler handler
            )
        {
            this.AddHandler(format, methods, handler, format);
        }
        /// <summary>
        /// Adds a route to a handler to the router's route-set.
        /// </summary>
        /// <param name="format">The format to be associated with the route.</param>
        /// <param name="methods">The methods for this route to be constrained to.</param>
        /// <param name="handler">The handler to be associated with the route.</param>
        /// <param name="name">The name to give the route.</param>
        public void AddHandler(
            string format,
            IEnumerable<string> methods,
            RouteHandler handler,
            string name
            )
        {
            var route = new Route(format, handler, methods) { Name = name };

            lock (_lockbox)
            {
                _routes.RemoveAll(r => r.Format.Equals(route.Format) || r.Name.Equals(route.Name));

                _routes.Add(route);
            }
        }

        void ICollection<Route>.Add(Route route)
        {
            this.AddHandler(
                format:     route.Format,
                methods:    route.Methods,
                handler:    route.Handler,
                name:       route.Name
                );
        }
        void ICollection<Route>.Clear()
        {
            _routes.Clear();
        }
        bool ICollection<Route>.Contains(Route route)
        {
            return _routes.Contains(route);
        }
        void ICollection<Route>.CopyTo(Route[] array, int arrayIndex)
        {
            _routes.CopyTo(array, arrayIndex);
        }
        bool ICollection<Route>.Remove(Route route)
        {
            return _routes.Remove(route);
        }
        IEnumerator<Route> IEnumerable<Route>.GetEnumerator()
        {
            return _routes.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _routes.GetEnumerator();
        }
        int ICollection<Route>.Count
        {
            get { return _routes.Count; }
        }
        bool ICollection<Route>.IsReadOnly
        {
            get { return false; }
        }
    }
}
