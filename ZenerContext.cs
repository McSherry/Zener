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
using System.IO;

using McSherry.Zener.Core;
using McSherry.Zener.Net;

using IPAddress = System.Net.IPAddress;
using Method = System.Func<dynamic, string>;
using WebUtility = System.Web.HttpUtility;
using RouteList = System.Collections.Generic
    .Dictionary<string, System.Func<McSherry.Zener.ZenerContext, McSherry.Zener.Core.RouteHandler>>;

namespace McSherry.Zener
{
    /// <summary>
    /// Used to indicate how ZenerCore API routes should be
    /// added to virtual hosts.
    /// </summary>
    public enum ZenerApiAdditionRule
    {
        /// <summary>
        /// The API routes should be added to the first host only.
        /// </summary>
        FirstHost,
        /// <summary>
        /// The API routes should be added to every host.
        /// </summary>
        AllHosts
    }

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

        // If the config settings dictate that we're only going to
        // add API routes to the first virtual host, we're going to
        // need to store somewhere whether we've already added the
        // routes to a host.
        private bool _firstRouteOnlyAdded;
        // Whether the ZenerContext is locked to prevent changes.
        private bool _locked;
        // Checks whether the ZenerContext is locked, and throws an
        // InvalidOperationException if it is.
        private void _checkLocked()
        {
            if (_locked)
            {
                throw new InvalidOperationException(
                    "Cannot modify configuration settings after the ZenerContext " +
                    "has been passed to the ZenerCore."
                    );
            }
        }

        private IPAddress _defIpAddr;
        private ushort _defTcpPort;

        private bool _incDefHost;

        private ZenerApiAdditionRule _apiAddRule;
        private bool _enableFsApi;

        /// <summary>
        /// Adds the routes for any active APIs to the
        /// provided router. To be called from the ZenerCore's
        /// constructor.
        /// </summary>
        internal void AddApiRoutes(Router router)
        {
            // If the ZenerContext is configured to only add API routes to the
            // first host and we've already added it to a host, we mustn't add
            // it to another, so we return.
            if (
                this.ApiAdditionRule == ZenerApiAdditionRule.FirstHost &&
                _firstRouteOnlyAdded
                )
            {
                return;
            }

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
        /// Locks the ZenerContext to prevent modification.
        /// </summary>
        internal void Lock()
        {
            _locked = true;
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
        /// Creates a new ZenerContext.
        /// </summary>
        /// <param name="defaultAddress">
        /// The default IP address to bind to when adding virtual
        /// hosts to the ZenerCore.
        /// </param>
        /// <param name="defaultPort">
        /// The default port to bind to when adding virtual hosts
        /// to the ZenerCore.
        /// </param>
        /// <param name="addDefaultHost">
        /// Whether to add a wildcard virtual host to the ZenerCore
        /// during instantiation. This host uses the default IP address
        /// and port.
        /// </param>
        /// <param name="apiAdd">
        /// Specifies how API routes should be added to virtual hosts.
        /// </param>
        /// <param name="useFilesystem">Whether to enable the file system API.</param>
        /// <param name="methods">The methods to make available via the method call API.</param>
        public ZenerContext(
            IPAddress defaultAddress,
            ushort defaultPort          = 80,

            bool addDefaultHost         = true,

            ZenerApiAdditionRule apiAdd = ZenerApiAdditionRule.FirstHost,
            bool useFilesystem          = false,
            Dictionary<string, Method> methods = null
            )
        {
            this.DefaultIpAddress = defaultAddress;
            this.DefaultTcpPort = defaultPort;

            this.IncludeDefaultHost = addDefaultHost;

            this.ApiAdditionRule = apiAdd;
            this.EnableFileSystemApi = useFilesystem;
            this.Methods = methods ?? new Dictionary<string, Method>();

            _firstRouteOnlyAdded = false;
        }

        /// <summary>
        /// The IP address to use by default when adding virtual
        /// hosts to the ZenerCore.
        /// </summary>
        public IPAddress DefaultIpAddress
        {
            get { return _defIpAddr; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(
                        "The default IP address cannot be set to null."
                        );

                _checkLocked();

                _defIpAddr = value;
            }
        }
        /// <summary>
        /// The TCP port to use by default when adding virtual
        /// hosts to the ZenerCore.
        /// </summary>
        public ushort DefaultTcpPort
        {
            get { return _defTcpPort; }
            set
            {
                if (value == 0)
                    throw new ArgumentException(
                        "The default TCP port cannot be set to zero."
                        );

                _checkLocked();

                _defTcpPort = value;
            }
        }

        /// <summary>
        /// Whether to include a default wildcard virtual host in
        /// the ZenerCore's router. This route binds to the default
        /// IP address and port.
        /// </summary>
        public bool IncludeDefaultHost
        {
            get { return _incDefHost; }
            set
            {
                _checkLocked();

                _incDefHost = value;
            }
        }

        /// <summary>
        /// Specifies how ZenerCore APIs should be added to
        /// virtual hosts.
        /// </summary>
        public ZenerApiAdditionRule ApiAdditionRule
        {
            get { return _apiAddRule; }
            set
            {
                if (!Enum.IsDefined(typeof(ZenerApiAdditionRule), value))
                    throw new ArgumentException(
                        "The API addition rule must be set to a valid value."
                        );

                _checkLocked();

                _apiAddRule = value;
            }
        }
        /// <summary>
        /// Whether the file-system API should be enabled
        /// for the ZenerCore.
        /// </summary>
        public bool EnableFileSystemApi
        {
            get { return _enableFsApi; }
            set
            {
                _checkLocked();

                _enableFsApi = value;
            }
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
