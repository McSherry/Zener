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

using SynapLink.Zener.Net;

namespace SynapLink.Zener.Core
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
            // an acceptable method.
            if (route.Methods == null || route.Methods.Count() == 0)
            {
                return true;
            }

            return route.Methods.Contains(method, Route.MethodComparer);
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
                string mediaType = MediaTypeMap.FallbackType;
                if (Path.HasExtension(filePath))
                {
                    mediaType = MediaTypes.Find(
                        Path.GetExtension(filePath)
                        );
                }
                rs.Headers.Add("Content-Type", mediaType);

                try
                {
                    using (FileStream fs = File.OpenRead(filePath))
                    {
                        byte[] content = new byte[fs.Length];

                        fs.Read(content, 0, content.Length);

                        rs.Write(content);
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
        /// A map of media types and file extensions used to determine the media type of files.
        /// </summary>
        public static MediaTypeMap MediaTypes
        {
            get;
            set;
        }
    }
}
