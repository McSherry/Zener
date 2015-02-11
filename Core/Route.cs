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
using System.Dynamic;

using McSherry.Zener.Net;

namespace McSherry.Zener.Core
{
    /// <summary>
    /// The delegate used with handler functions for routes.
    /// </summary>
    /// <param name="request">The HTTP request for this route.</param>
    /// <param name="response">The HTTP response this route will generate.</param>
    /// <param name="routeParameters">The parameters provided to this route.</param>
    public delegate void RouteHandler(
        HttpRequest request, HttpResponse response, 
        dynamic routeParameters
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
         * Parameters should be separated from the rest of the
         * format by a forward slash. For this reason, the below
         * examples are not guaranteed to work.
         * 
         *      test/path[param]
         *      [param]test/path
         *      
         * Additionally, unbounded parameters (parameters which
         * accept any character) may be created by prefixing the
         * name with an asterisk, as below.
         * 
         *      user/[*username]
         *      
         * As these unbounded variables may contain any character,
         * they must appear at the end of a format string.
         * 
         *      a/[*b]/c
         *      
         * The format string shown above will never have any
         * matches, because the unbounded variable is infixed.
         */

        private static readonly IEnumerable<string> _defaultMethods;

        private readonly Lazy<IEnumerable<string>> _paramNames;
        private readonly string _formatOriginal;

        static Route()
        {
            Route.MethodComparer = StringComparer.OrdinalIgnoreCase;
            _defaultMethods = new[] 
            {
                "GET", "POST", "HEAD", "PUT", "DELETE",
                "OPTIONS", "CONNECT", "TRACE"
            };
        }

        /// <summary>
        /// Creates a new route.
        /// </summary>
        /// <param name="format">The format to be associated with this route.</param>
        /// <param name="handler">The handler to be associated with this route.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when at least one of the provided arguments is null.
        /// </exception>
        public Route(
            string format,
            RouteHandler handler
            )
            : this(format, handler, new string[0])
        {
            
        }
        /// <summary>
        /// Creates a new route.
        /// </summary>
        /// <param name="format">The format to be associated with this route.</param>
        /// <param name="handler">The handler to be associated with this route.</param>
        /// <param name="methods">The methods this route should be constrained to.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when at least one of the provided arguments is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the set of acceptable HTTP methods does not contain at
        ///     least one method.
        /// </exception>
        public Route(
            string format,
            RouteHandler handler,
            IEnumerable<string> methods
            )
        {
            #region Param validation
            if (format == null)
            {
                throw new ArgumentNullException(
                    "format",
                    "The format string cannot be null."
                    );
            }

            if (handler == null)
            {
                throw new ArgumentNullException(
                    "methods",
                    "The route handler cannot be null."
                    );
            }

            if (methods == null)
            {
                throw new ArgumentNullException(
                    "methods",
                    "The set of acceptable HTTP methods cannot be null."
                    );
            }
            #endregion

            _formatOriginal = Routing.TrimFormatString(format);
            this.Format = _formatOriginal.ToLower();
            this.Handler = handler;
            this.Name = this.Format;
            this.Methods = methods;

            _paramNames = new Lazy<IEnumerable<string>>(
                () => Routing.GetParameters(this.Format)
                );
        }

        /// <summary>
        /// Determines whether the route matches a path.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <param name="param">If the path matches, where any parameters are stored.</param>
        /// <returns>True if the route matches the path.</returns>
        public bool TryMatch(string path, string method, out dynamic param)
        {
            if (!this.MethodIsAcceptable(method))
            {
                param = new Empty();
                return false;
            }

            path = Routing.TrimFormatString(path);

            return Routing.IsFormatMatch(path, this.Format, out param);
        }
        /// <summary>
        /// Determines whether the route matches a path, and passes
        /// any parameters in the path to a callback method.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <param name="callback">The callback to pass parameters to.</param>
        /// <returns>True if the route matches the path.</returns>
        public bool TryMatch(string path, string method, Action<dynamic> callback)
        {
            dynamic result = null;
            bool success = this.TryMatch(path, method, out result);

            if (success) callback(result);

            return success;
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
        /// The format that is associated with this route.
        /// </summary>
        public string Format
        {
            get;
            private set;
        }
        /// <summary>
        /// The handler delegate associated with this route.
        /// </summary>
        public RouteHandler Handler
        {
            get;
            private set;
        }
        /// <summary>
        /// The names of all parameters within the route's
        /// format string.
        /// </summary>
        public IEnumerable<string> Parameters
        {
            get { return _paramNames.Value; }
        }
        /// <summary>
        /// The HTTP methods this route should be constrained to.
        /// </summary>
        public IEnumerable<string> Methods
        {
            get;
            private set;
        }

        /// <summary>
        /// The comparer to be used when comparing HTTP methods.
        /// </summary>
        public static StringComparer MethodComparer
        {
            get;
            private set;
        }
    }
}
