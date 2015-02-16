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
using System.Dynamic;

using McSherry.Zener.Net;

namespace McSherry.Zener.Core
{
    /// <summary>
    /// Provides methods for working with Route and Router classes.
    /// </summary>
    public static class Routing
    {
        private const string RTR_ADDDIR_PARAM = "file";

        static Routing()
        {
            Routing.MediaTypes = MediaTypeMap.Default.Copy();
        }

        /// <summary>
        /// Extracts parameter names from a format string.
        /// </summary>
        /// <param name="format">The format string to extract parameters from.</param>
        /// <returns>A read-only list containing the names of parameters.</returns>
        public static IEnumerable<string> GetParameters(string format)
        {
            List<string> @params = new List<string>();

            bool inParam = false;
            StringBuilder nameBuilder = new StringBuilder();
            for (int i = 0; i < format.Length; i++)
            {
                char c = format[i];

                if (!inParam && c == '[')
                {
                    inParam = true;

                    // If this is an unbounded parameter,
                    // it will have an asterisk before its
                    // name. This isn't part of the name,
                    // so we need to skip it.
                    if (format[i + 1] == '*')
                    {
                        i++;
                    }

                    continue;
                }
                else if (inParam && c == ']')
                {
                    inParam = false;
                    @params.Add(nameBuilder.ToString());
                    nameBuilder.Clear();
                }
                else if (inParam)
                {
                    nameBuilder.Append(c);
                }
                else continue;
            }

            return @params;
        }
        /// <summary>
        /// Trims a format string to allow comparison between equal (but not identical)
        /// format strings.
        /// </summary>
        /// <param name="format">The format string to trim.</param>
        /// <returns>The trimmed format string.</returns>
        public static string TrimFormatString(string format)
        {
            return format.Trim(' ', '/');
        }
        /// <summary>
        /// Determines whether the specified HTTP method is acceptable
        /// for the provided route.
        /// </summary>
        /// <param name="route">The route to check the method against.</param>
        /// <param name="method">The method to check.</param>
        /// <returns>
        ///     True if the <paramref name="method"/> is acceptable for
        ///     the <paramref name="route"/>.
        /// </returns>
        public static bool MethodIsAcceptable(this Route route, string method)
        {
            // Null/empty can be used to indicate that any method is
            // an acceptable method. Passing null as the method parameter
            // can also be used as an "any" wildcard.
            if (
                route.Methods == null ||
                route.Methods.Count() == 0 ||
                String.IsNullOrWhiteSpace(method) ||
                route.Methods.Any(String.IsNullOrWhiteSpace)
                )
            {
                return true;
            }

            return route.Methods.Contains(method, Route.MethodComparer);
        }
        /// <summary>
        /// Determines whether the provided path matches the specified
        /// format string. The format string is not a C# format string,
        /// but is the format string used by Zener in its routes.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <param name="parameters">
        ///     The parameters, if any, extracted from the path and
        ///     format string.
        /// </param>
        /// <param name="format">The format string to check against.</param>
        /// <param name="delimiter">The character to delimit sections.</param>
        /// <param name="allowUnbounded">
        ///     Whether to permit the use of unbounded variables in
        ///     format strings.
        /// </param>
        /// <returns>True if the specified path matches the given format.</returns>
        public static bool IsFormatMatch(
            string path, string format,
            out dynamic parameters,
            char delimiter      = '/',
            bool allowUnbounded = true
            )
        {
            format = format.Trim(' ', delimiter);
            string formatOriginal = format;
            format = format.ToLower();

            var dynObj = new ExpandoObject() as IDictionary<string, object>;
            // Indices within format and path
            int fIndex = 0, pIndex = 0;
            StringBuilder
                formatBuilder = new StringBuilder(),
                pathBuilder = new StringBuilder(),
                paramNameBuilder = new StringBuilder(),
                paramValBuilder = new StringBuilder();
            string paramName = String.Empty;
            bool inParam = false, loop = true;
            bool paramIsUnbounded = false;

            while (loop)
            {
                while (fIndex < format.Length)
                {
                    // Find start of a param wild-card
                    if (!inParam && format[fIndex] == '[')
                    {
                        // If the next character after the opening
                        // square bracket is an asterisk, the
                        // variable is unbounded (i.e. its value
                        // can contain any characters).
                        if (format[fIndex + 1] == '*' && allowUnbounded)
                        {
                            paramIsUnbounded = true;
                            // The asterisk isn't part of the
                            // variable's name, so we can skip
                            // past it.
                            fIndex++;
                        }

                        inParam = true;
                        fIndex++;
                    }
                    // Find end of a param wild-card
                    else if (inParam && format[fIndex] == ']')
                    {
                        inParam = false;
                        fIndex++;
                        break;
                    }
                    // General format text
                    else if (!inParam)
                    {
                        formatBuilder.Append(format[fIndex++]);
                    }
                    // Param name
                    else
                    {
                        paramNameBuilder.Append(formatOriginal[fIndex++]);
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
                    else if (inParam && !paramIsUnbounded && path[pIndex] == delimiter)
                    {
                        // Zero-length parameters should be rejected.
                        // See GitHub issue #7.
                        if (paramValBuilder.Length == 0)
                        {
                            pIndex++;
                            continue;
                        }

                        inParam = false;
                        dynObj[paramNameBuilder.ToString()] = paramValBuilder.ToString();
                        paramNameBuilder.Clear();
                        paramValBuilder.Clear();
                        break;
                    }
                    else if (inParam)
                    {
                        paramValBuilder.Append(path[pIndex++]);
                    }
                }

                if (!(pIndex < path.Length) && !(fIndex < format.Length))
                {
                    if (inParam)
                    {
                        dynObj[paramNameBuilder.ToString()] = paramValBuilder.ToString();
                        paramNameBuilder.Clear();
                        paramValBuilder.Clear();
                    }
                    break;
                }
                else
                {
                    inParam = false;
                }
            }

            if (dynObj.Count > 0) parameters = dynObj;
            else parameters = new Empty();

            if (paramNameBuilder.Length > 0)
            {
                // If there is a parameter in the format, but the path is
                // zero-length, we cannot consider it a match, because
                // parameters cannot be empty.
                //
                // This change resolves a bug where a format with a variable
                // at the very start (such as "/[file]") would match a request
                // for the index (path "/").
                if (pathBuilder.Length == 0)
                {
                    parameters = new Empty();
                    return false;
                }
            }

            return formatBuilder.ToString().Equals(
                pathBuilder.ToString(), StringComparison.OrdinalIgnoreCase
                );
        }
        /// <summary>
        /// Determines whether the virtual host's hostname is a wildcard.
        /// </summary>
        /// <param name="host">The host to check.</param>
        /// <returns>True if the virtual host's hostname is a wildcard.</returns>
        public static bool IsWildcard(this VirtualHost host)
        {
            return host.Format.Equals(VirtualHost.AnyHostname);
        }

        /// <summary>
        /// Adds a directory handler to the router. A directory handler
        /// is used to serve files from a directory, using a parameter in
        /// the format string as the file-name.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="dirPath">The directory to serve files from.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used to identify files within
        ///     <paramref name="dirPath"/>.
        /// </exception>
        public static void AddDirectory(
            this Router router, string format, string dirPath
            )
        {
            router.AddDirectory(format, new string[0], dirPath);
        }
        /// <summary>
        /// Adds a directory handler to the router. A directory handler
        /// is used to serve files from a directory, using a parameter in
        /// the format string as the file-name.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="method">The method for this route to be constrained to.</param>
        /// <param name="dirPath">The directory to serve files from.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used to identify files within
        ///     <paramref name="dirPath"/>.
        /// </exception>
        public static void AddDirectory(
            this Router router,
            string format, string method,
            string dirPath
            )
        {
            router.AddDirectory(format, new string[1] { method }, dirPath);
        }
        /// <summary>
        /// Adds a directory handler to the router. A directory handler
        /// is used to serve files from a directory, using a parameter in
        /// the format string as the file-name.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="methods">The methods for this route to be constrained to.</param>
        /// <param name="dirPath">The directory to serve files from.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used to identify files within
        ///     <paramref name="dirPath"/>.
        /// </exception>
        public static void AddDirectory(
            this Router router,
            string format, IEnumerable<string> methods,
            string dirPath
            )
        {
            var paramNames = Routing.GetParameters(format);

            if (!paramNames.Contains(RTR_ADDDIR_PARAM))
                throw new ArgumentException(
                    String.Format(
                        "Format string does not contain a parameter named \"{0}\".",
                        RTR_ADDDIR_PARAM
                    ));

            router.AddHandler(format, methods, (rq, rs, pr) => {
                string filePath = String.Format(@"{0}\{1}", dirPath, pr.file);

                try
                {
                    using (FileStream fs = File.OpenRead(filePath))
                    {
                        if (!Path.HasExtension(filePath))
                        {
                            rs.Headers.Add("Content-Type", "text/plain");
                        }
                        else
                        {
                            string ext = Path.GetExtension(filePath);
                            rs.Headers.Add("Content-Type", MediaTypes.Find(ext));
                        }

                        byte[] response = new byte[fs.Length];
                        fs.Read(response, 0, response.Length);

                        rs.Write(response);
                    }
                }
                catch (DirectoryNotFoundException dnfex)
                {
                    throw new HttpException(
                        HttpStatus.NotFound,
                        rq,
                        "The specified directory could not be found.",
                        dnfex
                        );
                }
                catch (FileNotFoundException fnfex)
                {
                    throw new HttpException(
                        HttpStatus.NotFound,
                        rq,
                        "The specified file could not be found.",
                        fnfex
                        );
                }
                catch (UnauthorizedAccessException uaex)
                {
                    throw new HttpException(
                        HttpStatus.Forbidden,
                        rq,
                        "You do not have permission to access this file.",
                        uaex
                        );
                }
                catch (PathTooLongException ptlex)
                {
                    throw new HttpException(
                        HttpStatus.RequestUriTooLarge,
                        rq,
                        "The specified file path was too long.",
                        ptlex
                        );
                }
                catch (IOException ioex)
                {
                    throw new HttpException(
                        HttpStatus.InternalServerError,
                        rq,
                        "An unspecified I/O error occured.",
                        ioex
                        );
                }
            });
        }

        /// <summary>
        /// Adds a file handler to the router. A file handler is used to
        /// serve a single file.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="filePath">The path of the file to serve.</param>
        public static void AddFile(
            this Router router, string format, string filePath
            )
        {
            router.AddFile(format, new string[0], filePath);
        }
        /// <summary>
        /// Adds a file handler to the router. A file handler is used to
        /// serve a single file.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="method">The method for this route to be constrained to.</param>
        /// <param name="filePath">The path of the file to serve.</param>
        public static void AddFile(
            this Router router,
            string format, string method,
            string filePath
            )
        {
            router.AddFile(format, new string[1] { method }, filePath);
        }
        /// <summary>
        /// Adds a file handler to the router. A file handler is used to
        /// serve a single file.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="methods">The methods for this route to be constrained to.</param>
        /// <param name="filePath">The path of the file to serve.</param>
        public static void AddFile(
            this Router router,
            string format, IEnumerable<string> methods,
            string filePath
            )
        {
            router.AddHandler(format, methods, (rq, rs, prm) =>
            {
                string mediaType = MediaTypes.Find(filePath, FindParameterType.NameOrPath);
                rs.Headers.Add("Content-Type", mediaType);

                try
                {
                    using (FileStream fs = File.OpenRead(filePath))
                    {
                        // The running total of bytes read from the stream
                        int runningTotal = 0;
                        byte[] buf = new byte[HttpResponse.TX_BUFFER_SIZE];
                        while (runningTotal != fs.Length)
                        {
                            runningTotal += fs.Read(buf, 0, buf.Length);

                            rs.Write(buf);
                        }
                    }
                }
                catch (DirectoryNotFoundException dnfex)
                {
                    throw new HttpException(
                        HttpStatus.NotFound,
                        rq,
                        "The specified directory could not be found.",
                        dnfex
                        );
                }
                catch (FileNotFoundException fnfex)
                {
                    throw new HttpException(
                        HttpStatus.NotFound,
                        rq,
                        "The specified file could not be found.",
                        fnfex
                        );
                }
                catch (UnauthorizedAccessException uaex)
                {
                    throw new HttpException(
                        HttpStatus.Forbidden,
                        rq,
                        "You do not have permission to access this file.",
                        uaex
                        );
                }
                catch (PathTooLongException ptlex)
                {
                    throw new HttpException(
                        HttpStatus.RequestUriTooLarge,
                        rq,
                        "The specified file path was too long.",
                        ptlex
                        );
                }
                catch (IOException ioex)
                {
                    throw new HttpException(
                        HttpStatus.InternalServerError,
                        rq,
                        "An unspecified I/O error occured.",
                        ioex
                        );
                }
            });
        }

        /// <summary>
        /// Adds a handler which will serve the provided bytes when called.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="content">The content to serve.</param>
        /// <param name="mediaType">The media type of the content (e.g. text/plain).</param>
        public static void AddResource(
            this Router router, string format,
            byte[] content, string mediaType
            )
        {
            router.AddResource(format, new string[0], content, mediaType);
        }
        /// <summary>
        /// Adds a handler which will serve the provided bytes when called.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="method">The method for this route to be constrained to.</param>
        /// <param name="content">The content to serve.</param>
        /// <param name="mediaType">The media type of the content (e.g. text/plain).</param>
        public static void AddResource(
            this Router router,
            string format, string method,
            byte[] content, string mediaType
            )
        {
            router.AddResource(format, new string[1] { method }, content, mediaType);
        }
        /// <summary>
        /// Adds a handler which will serve the provided bytes when called.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="methods">The methods for this route to be constrained to.</param>
        /// <param name="content">The content to serve.</param>
        /// <param name="mediaType">The media type of the content (e.g. text/plain).</param>
        public static void AddResource(
            this Router router,
            string format, IEnumerable<string> methods, 
            byte[] content, string mediaType
            )
        {
            router.AddHandler(format, methods, (rq, rs, prm) =>
            {
                rs.Headers.Add("Content-Type", mediaType);

                rs.Write(content);
            });
        }
        /// <summary>
        /// Adds a handler which will serve the provided string as UTF-8
        /// HTML when called.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="html">The HTML to be served.</param>
        public static void AddResource(
            this Router router, string format, string html
            )
        {
            router.AddResource(format, new string[0], html);
        }
        /// <summary>
        /// Adds a handler which will serve the provided string as UTF-8
        /// HTML when called.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="method">The method for this route to be constrained to.</param>
        /// <param name="html">The HTML to be served.</param>
        public static void AddResource(
            this Router router,
            string format, string method,
            string html
            )
        {
            router.AddResource(format, new string[1] { method }, html);
        }
        /// <summary>
        /// Adds a handler which will serve the provided string as UTF-8
        /// HTML when called.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="methods">The methods for this route to be constrained to.</param>
        /// <param name="html">The HTML to be served.</param>
        public static void AddResource(
            this Router router,
            string format, IEnumerable<string> methods,
            string html
            )
        {
            router.AddResource(
                format, methods,
                Encoding.UTF8.GetBytes(html),
                "text/html"
            );
        }

        /// <summary>
        /// Determines the media type to used based on a file extension,
        /// file path, or file name.
        /// </summary>
        /// <param name="fileExtension">
        /// The file extension, file path, or file name to determine the
        /// media type from.
        /// </param>
        /// <param name="findType">
        /// Specifies what has been passed in the <paramref name="fileExtension"/>
        /// parameter; whether the parameter is a file extension on its own, or a
        /// file path/file name.
        /// </param>
        /// <returns>
        /// The result, which is the MediaType associated with the file
        /// extension/file path/file name and a handler for transforming
        /// content in that media type's format in to a format that can
        /// be served.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the <paramref name="fileExtension"/> parameter is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when, after being passed FindParameterType.NameOrPath, the
        /// parameter <paramref name="fileExtension"/> does not contain a file
        /// extension.
        /// </exception>
        public static Tuple<MediaType, MediaTypeHandler> FindMediaType(
            this MediaTypeMap map, string fileExtension,
            FindParameterType findType = FindParameterType.Extension
            )
        {
            Tuple<MediaType, MediaTypeHandler> res;
            if (!map.TryFindMediaType(fileExtension, out res, findType))
            {
                MediaTypeHandler handler;
                if (!map.TryFindHandler(map.DefaultType, out handler))
                {
                    handler = MediaTypeMap.DefaultMediaTypeHandler;
                }
                // Set 'res' to the default type and the appropriate
                // handler.
                res = new Tuple<MediaType, MediaTypeHandler>(
                    map.DefaultType, handler
                    );
            }

            return res;
        }

        /// <summary>
        /// A map of media types and file extensions used to determine the media type of files.
        /// </summary>
        public static MediaTypeMap MediaTypes
        {
            get;
            set;
        }
    }
}
