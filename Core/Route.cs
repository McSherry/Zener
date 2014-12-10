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
         * retain their casing. Parameters should be separated
         * from the rest of the format by a forward slash. For
         * this reason, the below examples are not guaranteed to
         * work.
         * 
         *      test/path[param]
         *      [param]test/path
         *      
         * 
         */

        private RouteHandler _handler;
        private string _format;
        private string _name;

#if DEBUG
        static Route()
        {
            RouteHandler rh = (rs, rq, p) => { };
            Dictionary<string, string> result = new Dictionary<string, string>();

            var trues = new[] 
            {
                new { route = new Route("a/b", rh), str = "/a/b/", pnum = 0 },
                new { route = new Route("a/b", rh), str = "/a/b", pnum = 0 },
                new { route = new Route("a/b", rh), str = "a/b/", pnum = 0 },
                new { route = new Route("a/b", rh), str = "a/b", pnum = 0 },
                new { route = new Route("a/b", rh), str = "a/B", pnum = 0 },

                new { route = new Route("a/[p]", rh), str = "/a/v/", pnum = 1 },
                new { route = new Route("a/[p]", rh), str = "/a/v", pnum = 1 },
                new { route = new Route("a/[p]", rh), str = "a/v/", pnum = 1 },
                new { route = new Route("a/[p]", rh), str = "a/v", pnum = 1 },

                new { route = new Route("a/[p0]/b", rh), str = "/a/v0/b/", pnum = 1 },
                new { route = new Route("a/[p0]/b", rh), str = "/a/v0/b", pnum = 1 },
                new { route = new Route("a/[p0]/b", rh), str = "a/v0/b/", pnum = 1 },
                new { route = new Route("a/[p0]/b", rh), str = "a/v0/b", pnum = 1 },

                new { route = new Route("a/[p0]/b/[p1]", rh), str = "/a/v0/b/v1/", pnum = 2 },
                new { route = new Route("a/[p0]/b/[p1]", rh), str = "/a/v0/b/v1", pnum = 2 },
                new { route = new Route("a/[p0]/b/[p1]", rh), str = "a/v0/b/v1/", pnum = 2 },
                new { route = new Route("a/[p0]/b/[p1]", rh), str = "a/v0/b/v1", pnum = 2 },
            };
            var falses = new[]
            {
                new { route = new Route("a/b", rh), str = "/a/b/c/" },
                new { route = new Route("a/b", rh), str = "/a/b/c" },
                new { route = new Route("a/b", rh), str = "a/b/c/" },
                new { route = new Route("a/b", rh), str = "a/b/c" },
                
                new { route = new Route("a/[p]", rh), str = "/a/v0/b/" },
                new { route = new Route("a/[p]", rh), str = "/a/v0/b" },
                new { route = new Route("a/[p]", rh), str = "a/v0/b/" },
                new { route = new Route("a/[p]", rh), str = "a/v0/b" },
                
                new { route = new Route("a/[p]", rh), str = "/a/" },
                new { route = new Route("a/[p]", rh), str = "/a" },
                new { route = new Route("a/[p]", rh), str = "a/" },
                new { route = new Route("a/[p]", rh), str = "a" },
                
                new { route = new Route("a/[p]/b", rh), str = "/a/v0/" },
                new { route = new Route("a/[p]/b", rh), str = "/a/v0" },
                new { route = new Route("a/[p]/b", rh), str = "a/v0/" },
                new { route = new Route("a/[p]/b", rh), str = "a/v0" },
                
                new { route = new Route("a/[p0]/b/[p1]", rh), str = "/a/v0/b/" },
                new { route = new Route("a/[p0]/b/[p1]", rh), str = "/a/v0/b" },
                new { route = new Route("a/[p0]/b/[p1]", rh), str = "a/v0/b/" },
                new { route = new Route("a/[p0]/b/[p1]", rh), str = "a/v0/b" },
            };

            foreach (var test in trues)
            {
                bool succeed = test.route.TryMatch(test.str, result);
                bool paramCorrect = result.Count == test.pnum;

                if (!succeed || !paramCorrect)
                    throw new Exception(
                        String.Format(
                            "Test failed (True Tests): {0}, {1}, {2}/{3}",
                            test.route.Format, test.str, test.pnum,
                            result.Count   
                        ));

                result.Clear();
            }
            foreach (var test in falses)
            {
                bool succeed = !test.route.TryMatch(test.str, result);

                if (!succeed)
                    throw new Exception(
                        String.Format(
                            "Test failed (False Tests): {0}, {1}",
                            test.route.Format, test.str,
                            result.Count
                        ));
            }
        }
#endif

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
        /// <param name="param">If the path matches, where any parameters are stored.</param>
        /// <returns>True if the route matches the path.</returns>
        public bool TryMatch(string path, Dictionary<string, string> param)
        {
            path = path.Trim(' ', '/');

            // Indices within format and path
            int fIndex = 0, pIndex = 0;
            StringBuilder
                formatBuilder = new StringBuilder(),
                pathBuilder = new StringBuilder(),
                paramNameBuilder = new StringBuilder(),
                paramValBuilder = new StringBuilder();
            string paramName = String.Empty;
            bool inParam = false, loop = true;

            while (loop)
            {
                while (fIndex < this.Format.Length)
                {
                    // Find start of a param wild-card
                    if (!inParam && this.Format[fIndex] == '[')
                    {
                        inParam = true;
                        fIndex++;
                    }
                    // Find end of a param wild-card
                    else if (inParam && this.Format[fIndex] == ']')
                    {
                        inParam = false;
                        fIndex++;
                        break;
                    }
                    // General format text
                    else if (!inParam)
                    {
                        formatBuilder.Append(this.Format[fIndex++]);
                    }
                    // Param name
                    else
                    {
                        paramNameBuilder.Append(this.Format[fIndex++]);
                    }
                }

                while (pIndex < path.Length)
                {
                    if (!inParam)
                    {
                        pathBuilder.Append(path[pIndex]);

                        if (formatBuilder.ToString().StartsWith(
                            pathBuilder.ToString(), true,
                            System.Globalization.CultureInfo.InvariantCulture
                            ))
                        {
                            pIndex++;
                        }
                        else
                        {
                            if (paramNameBuilder.Length == 0)
                            {
                                pIndex = int.MaxValue;
                                break;
                            }

                            inParam = true;
                            pathBuilder.Remove(pathBuilder.Length - 1, 1);
                        }
                    }
                    else if (inParam && path[pIndex] == '/')
                    {
                        inParam = false;
                        param[paramNameBuilder.ToString()] = paramValBuilder.ToString();
                        paramNameBuilder.Clear();
                        paramValBuilder.Clear();
                        break;
                    }
                    else if (inParam)
                    {
                        paramValBuilder.Append(path[pIndex++]);
                    }
                }

                if (!(pIndex < path.Length) && !(fIndex < this.Format.Length))
                {
                    if (inParam) param[paramNameBuilder.ToString()] = paramValBuilder.ToString();
                    break;
                }
                else
                {
                    inParam = false;
                }
            }


            return formatBuilder.ToString().Equals(
                pathBuilder.ToString(), StringComparison.OrdinalIgnoreCase
                );
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
