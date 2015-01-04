/*
 *      Copyright (c) 2014-2015, SynapLink, LLC
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
using System.Reflection;
using System.IO;

using SynapLink.Zener.Net;
using SynapLink.Zener.Core;

using WebUtility = System.Net.WebUtility;
using RouteList = System.Collections.Generic.Dictionary<string, SynapLink.Zener.Core.RouteHandler>;

namespace SynapLink.Zener
{
    /// <summary>
    /// Used to indicate when a dynamic property has no value.
    /// </summary>
    public enum Empty { }

    /// <summary>
    /// A class implementing the Zener interface between web server and application.
    /// </summary>
    public class ZenerCore
    {
        /// <summary>
        /// Contains the handlers which provide Zener's
        /// route-based APIs.
        /// </summary>
        internal static class Api
        {
            private const string FILESYSTEM_CONTENTS = "fileContent";
            private static List<RouteList> _apiRoutes;

            static Api()
            {
                _apiRoutes = new List<RouteList>()
                {
                    new RouteList()
                    {
                        { ":fs", Api.Filesystem },
                        { ":fs/[*path]", Api.Filesystem }
                    }
                };
            }

            /// <summary>
            /// The routes provided by Zener for its API.
            /// </summary>
            public static List<RouteList> Routes
            {
                get { return _apiRoutes; }
            }

            /// <summary>
            /// The route providing a file-system API.
            /// </summary>
            /// <param name="rq">The HttpRequest to respond to.</param>
            /// <param name="rs">The HttpResponse to respond with.</param>
            /// <param name="params">The route's parameters.</param>
            public static void Filesystem(HttpRequest rq, HttpResponse rs, dynamic @params)
            {
                /* The filesystem API provides client-side code (such as JavaScript)
                 * with an easy way to access the underlying file system. It is accessed
                 * via the ':fs' route.
                 * 
                 * Through this API, client-side code can:
                 * 
                 *      - Read files
                 *      - Write (and create) files
                 *      - Check files exist
                 *      - Delete files
                 *      - Retrieve directory listings
                 *      - Delete directories
                 *      - Check directories exist
                 *      
                 * See issue #2 (or relevant documentation when available) for more
                 * information.
                 */

                string path;
                if (@params is Empty) path = Environment.CurrentDirectory;
                else path = WebUtility.UrlDecode(@params.path);

                rs.Headers.Add("Content-Type", "application/json");
                StringBuilder jsonBuilder = new StringBuilder("{");
                try
                {

                    if (rq.Method == HttpRequest.Methods.GET)
                    {
                        if (File.Exists(path))
                        {
                            #region Read file contents
                            using (FileStream fs = File.Open(path, FileMode.Open))
                            {
                                byte[] fileBytes = new byte[fs.Length];
                                fs.Read(fileBytes, 0, fileBytes.Length);

                                jsonBuilder.AppendFormat(
                                    @"""file"": ""{0}""",
                                    WebUtility.UrlEncode(Encoding.ASCII.GetString(fileBytes))
                                    );
                            }
                            #endregion
                        }
                        else if (Directory.Exists(path))
                        {
                            #region Get directory listing
                            string[] files = Directory.GetFiles(path),
                                subdirs = Directory.GetDirectories(path);
                            string fileArray = files
                                .Aggregate(
                                    new StringBuilder(),
                                    (sb, f) => sb.AppendFormat(@"""{0}"", ", WebUtility.UrlEncode(f)))
                                .ToString();
                            string dirsArray = subdirs
                                .Aggregate(
                                    new StringBuilder(),
                                    (sb, d) => sb.AppendFormat(@"""{0}"", ", WebUtility.UrlEncode(d)))
                                .ToString();

                            jsonBuilder.AppendFormat(
                                @"""files"": [ {0} ], ""directories"": [ {1} ]",
                                fileArray, dirsArray
                                );
                            #endregion
                        }
                        else
                        {
                            rs.StatusCode = HttpStatus.NotFound;
                        }
                    }
                    else if (rq.Method == HttpRequest.Methods.POST)
                    {
                        #region File writing/creation
                        var dict = rq.POST as IDictionary<string, object>;
                        // POST is empty or doesn't contain any data to write
                        // to the file, so we can just create an empty file.
                        if (dict == null || !dict.ContainsKey(FILESYSTEM_CONTENTS))
                        {
                            using (File.Create(path)) { }
                        }
                        // POST contains content to write to the file.
                        else
                        {
                            using (StreamWriter sw = File.CreateText(path))
                            {
                                sw.Write(dict[FILESYSTEM_CONTENTS]);
                            }
                        }

                        jsonBuilder.Append(@"""message"": ""File created.""");
                        #endregion
                    }
                    else if (rq.Method == HttpRequest.Methods.DELETE)
                    {
                        #region File/Directory deletion
                        if (File.Exists(path))
                        {
                            File.Delete(path);

                            jsonBuilder.Append(@"""message"": ""File deleted.""");
                        }
                        else if (Directory.Exists(path))
                        {
                            jsonBuilder.Append(@"""message"": ""Directory deleted.""");
                        }
                        else
                        {
                            rs.StatusCode = HttpStatus.NotFound;
                        }
                        #endregion
                    }
                    else if (rq.Method == HttpRequest.Methods.HEAD)
                    {
                        #region File/Directory existence check
                        if (File.Exists(path))
                        {
                            jsonBuilder.Append(
                                @"""message"": ""File exists."""
                                );
                        }
                        else if (Directory.Exists(path))
                        {
                            jsonBuilder.Append(
                                @"""message"": ""Directory exists."""
                                );
                        }
                        else
                        {
                            rs.StatusCode = HttpStatus.NotFound;
                        }
                        #endregion
                    }
                    else
                    {
                        rs.StatusCode = HttpStatus.MethodNotAllowed;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    rs.StatusCode = HttpStatus.Forbidden;
                }
                catch (ArgumentException)
                {
                    rs.StatusCode = HttpStatus.BadRequest;
                }
                catch (NotSupportedException)
                {
                    rs.StatusCode = HttpStatus.BadRequest;
                }
                catch (PathTooLongException)
                {
                    rs.StatusCode = HttpStatus.RequestUriTooLarge;
                }
                catch (DirectoryNotFoundException)
                {
                    rs.StatusCode = HttpStatus.NotFound;
                }
                catch (FileNotFoundException)
                {
                    rs.StatusCode = HttpStatus.NotFound;
                }
                catch (IOException)
                {
                    rs.StatusCode = HttpStatus.InternalServerError;
                }

                jsonBuilder.AppendFormat(
                    "{0}\"status\": {1}}}",
                    jsonBuilder.Length == 1 ? String.Empty : ", ",
                    (int)rs.StatusCode
                    );

                rs.Write(jsonBuilder.ToString());
            }
        }

        private const string INTERNAL_PREFIX = ":";
        private static Version _ver;

        private HttpServer _http;

        /// <summary>
        /// The current version of Zener.
        /// </summary>
        public static Version Version
        {
            get { return _ver; }
        }

        private void HandleHttpRequestSuccessful(HttpRequest req, HttpResponse res)
        {
            HttpRequestHandler hrh;
            bool found = this.Routes.TryFind(req.Path, out hrh);

            if (!found) throw new HttpException(HttpStatus.NotFound);

            hrh(req, res);
        }
        private void HandleHttpRequestError(HttpException exception, HttpResponse res)
        {
            HttpRequestHandler hrh;
            bool found = this.Routes.TryFind(
                String.Format("{0}{1}", INTERNAL_PREFIX, (int)exception.StatusCode),
                out hrh
                );

            if (!found) HttpServer.DefaultErrorHandler(exception, res);
            else
            {
                hrh(null, res);
            }
        }

        static ZenerCore()
        {
            _ver = Assembly.GetCallingAssembly().GetName().Version;
        }

        /// <summary>
        /// Creates a new ZenerCore.
        /// </summary>
        /// <param name="context">The context to use when creating the ZenerCore.</param>
        public ZenerCore(ZenerContext context)
        {
            this.Routes = new Router();
            _http = new HttpServer(context.IpAddress, context.TcpPort)
            {
                RequestHandler = HandleHttpRequestSuccessful,
                ErrorHandler = HandleHttpRequestError
            };

            // Load API routes based on those enabled in the
            // ZenerContext we were passed.
            foreach (var api in context.ActiveApis)
                foreach (var route in Api.Routes[api])
                    this.Routes.AddHandler(route.Key, route.Value);

            _http.Start();
        }

        /// <summary>
        /// The TCP port the server is listening on.
        /// </summary>
        public int Port
        {
            get { return _http.Port; }
        }
        /// <summary>
        /// The routes that will be used to call handlers and
        /// serve content to the user agent.
        /// </summary>
        public Router Routes
        {
            get;
            set;
        }
    }
}
