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
using System.Threading.Tasks;
using System.IO;

using SynapLink.Zener.Core;
using SynapLink.Zener.Net;

using IPAddress = System.Net.IPAddress;
using Method = System.Func<dynamic, string>;
using WebUtility = System.Web.HttpUtility;
using RouteList = System.Collections.Generic
    .Dictionary<string, System.Func<SynapLink.Zener.ZenerContext, SynapLink.Zener.Core.RouteHandler>>;

namespace SynapLink.Zener
{
    /// <summary>
    /// Used to pass state information to a ZenerCore.
    /// </summary>
    public class ZenerContext
    {
        /// <summary>
        /// Contains the handlers which provide Zener's route-based APIs.
        /// </summary>
        private static class Api
        {
            private const string FILESYSTEM_CONTENTS = "fileContent";

            public static RouteHandler FilesystemWrapper(ZenerContext context)
            {
                return Api.Filesystem;
            }
            /// <summary>
            /// The route providing a file-system API.
            /// </summary>
            /// <param name="rq">The HttpRequest to respond to.</param>
            /// <param name="rs">The HttpResponse to respond with.</param>
            /// <param name="pr">The route's parameters.</param>
            public static void Filesystem(HttpRequest rq, HttpResponse rs, dynamic pr)
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
                 * See API reference for more: 
                 *      https://github.com/SynapLink/Zener/wiki/Filesystem-API
                 */

                string path;
                if (pr is Empty) path = Environment.CurrentDirectory;
                else path = WebUtility.UrlDecode(pr.path);

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
                                .Select(p => Path.GetFullPath(p))
                                .Aggregate(
                                    new StringBuilder(),
                                    (sb, f) => sb.AppendFormat(@"""{0}"", ", WebUtility.UrlEncode(f)))
                                .ToString();
                            string dirsArray = subdirs
                                .Select(p => Path.GetFullPath(p))
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
                            jsonBuilder.Append(@"""message"": ""File created.""");
                        }
                        // POST contains content to write to the file.
                        else
                        {
                            using (StreamWriter sw = File.CreateText(path))
                            {
                                sw.Write(dict[FILESYSTEM_CONTENTS]);
                            }

                            jsonBuilder.Append(@"""message"": ""File modified.""");
                        }

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
                        if (!File.Exists(path) || !Directory.Exists(path))
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
            /// <summary>
            /// The route providing a method call API.
            /// </summary>
            /// <param name="context"></param>
            /// <returns></returns>
            public static RouteHandler MethodCall(ZenerContext context)
            {
                return (rq, rs, pr) =>
                {
                    rs.Headers.Add("Content-Type", "application/json");
                    StringBuilder jsonBuilder = new StringBuilder();
                    jsonBuilder.Append("{ ");
                    if (context.Methods.ContainsKey(pr.method))
                    {
                        var ret = context.Methods[pr.method](rq.POST);
                        var retIsNull = ret == null;
                        jsonBuilder.AppendFormat(
                            @"""returned"": {0}, ""return"": ""{1}"", ",
                            (!retIsNull).ToString().ToLower(),
                            retIsNull ? String.Empty : WebUtility.UrlEncode(ret)
                            );
                    }
                    else
                    {
                        rs.StatusCode = HttpStatus.NotFound;
                    }
                    jsonBuilder.AppendFormat(
                        @"""status"": {0} }}",
                        (int)rs.StatusCode
                        );

                    rs.Write(jsonBuilder.ToString());
                };
            }
        }

        /// <summary>
        /// Adds the routes for any active APIs to the
        /// provided router. To be called from the ZenerCore's
        /// constructor.
        /// </summary>
        internal void AddApiRoutes(Router router)
        {
            if (this.EnableFileSystemApi)
            {
                router.AddHandler(":fs", Api.Filesystem);
                router.AddHandler(":fs/[*file]", Api.Filesystem);
            }

            if (this.Methods.Count > 0)
            {
                router.AddHandler(":call/[*method]", Api.MethodCall(this));
            }
        }

        /// <summary>
        /// Creates a new ZenerCoreContext with the IP address
        /// set to the IPv4 loopback.
        /// </summary>
        public ZenerContext()
            : this(IPAddress.Loopback)
        {

        }
        /// <summary>
        /// Creates a new ZenerCoreContext.
        /// </summary>
        /// <param name="address">The IP address for the ZenerCore to bind to.</param>
        /// <param name="port">The TCP port for the ZenerCore to bind to.</param>
        /// <param name="useFilesystem">Whether to enable the file system API.</param>
        /// <param name="methods">The methods to make available via the method call API.</param>
        public ZenerContext(
            IPAddress address, 
            ushort port = 80,

            bool useFilesystem = false,
            Dictionary<string, Method> methods = null
            )
        {
            this.IpAddress = address;
            this.TcpPort = port;

            this.EnableFileSystemApi = useFilesystem;

            this.Methods = methods ?? new Dictionary<string, Method>();
        }

        /// <summary>
        /// The IP address for the ZenerCore to bind to.
        /// </summary>
        public IPAddress IpAddress
        {
            get;
            set;
        }
        /// <summary>
        /// The TCP port to bind this ZenerCore to.
        /// </summary>
        public ushort TcpPort
        {
            get;
            set;
        }

        /// <summary>
        /// Whether the file-system API should be enabled
        /// for the ZenerCore.
        /// </summary>
        public bool EnableFileSystemApi
        {
            get;
            set;
        }
        /// <summary>
        /// The methods to make available via the method
        /// call API.
        /// </summary>
        public Dictionary<string, Method> Methods
        {
            get;
            private set;
        }
    }
}
