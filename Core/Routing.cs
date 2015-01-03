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
        public static IReadOnlyList<string> GetParameters(string format)
        {
            List<string> @params = new List<string>();

            bool inParam = false;
            StringBuilder nameBuilder = new StringBuilder();
            foreach (char c in format)
            {
                if (!inParam && c == '[')
                {
                    inParam = true;
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

            return @params.AsReadOnly();
        }
        /// <summary>
        /// Adds a directory handler to the router. A directory handler
        /// is used to serve files from a directory, using a parameter in
        /// the format string as the file-name.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="dirPath">The directory to serve files from.</param>
        public static void AddDirectory(
            this Router router,
            string format, string dirPath
            )
        {
            var paramNames = Routing.GetParameters(format);

            if (!paramNames.Contains(RTR_ADDDIR_PARAM))
                throw new ArgumentException(
                    String.Format(
                        "Format string does not contain a parameter named \"{0}\".",
                        RTR_ADDDIR_PARAM
                    ));

            router.AddHandler(format, (rq, rs, pr) => {
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
                        "The specified directory could not be found.",
                        dnfex
                        );
                }
                catch (FileNotFoundException fnfex)
                {
                    throw new HttpException(
                        HttpStatus.NotFound,
                        "The specified file could not be found.",
                        fnfex
                        );
                }
                catch (UnauthorizedAccessException uaex)
                {
                    throw new HttpException(
                        HttpStatus.Forbidden,
                        "You do not have permission to access this file.",
                        uaex
                        );
                }
                catch (PathTooLongException ptlex)
                {
                    throw new HttpException(
                        HttpStatus.RequestUriTooLarge,
                        "The specified file path was too long.",
                        ptlex
                        );
                }
                catch (IOException ioex)
                {
                    throw new HttpException(
                        HttpStatus.InternalServerError,
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
        /// <param name="filePath">The file to serve.</param>
        public static void AddFile(
            this Router router,
            string format, string filePath
            )
        {
            router.AddHandler(format, (rq, rs, prm) =>
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
                        "The specified directory could not be found.",
                        dnfex
                        );
                }
                catch (FileNotFoundException fnfex)
                {
                    throw new HttpException(
                        HttpStatus.NotFound,
                        "The specified file could not be found.",
                        fnfex
                        );
                }
                catch (UnauthorizedAccessException uaex)
                {
                    throw new HttpException(
                        HttpStatus.Forbidden,
                        "You do not have permission to access this file.",
                        uaex
                        );
                }
                catch (PathTooLongException ptlex)
                {
                    throw new HttpException(
                        HttpStatus.RequestUriTooLarge,
                        "The specified file path was too long.",
                        ptlex
                        );
                }
                catch (IOException ioex)
                {
                    throw new HttpException(
                        HttpStatus.InternalServerError,
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
            this Router router,
            string format, byte[] content, string mediaType
            )
        {
            router.AddHandler(format, (rq, rs, prm) =>
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
            this Router router,
            string format, string html
            )
        {
            router.AddResource(
                format,
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
