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
            Routing.MediaTypeMap = MediaTypeMap.Default.Copy();
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

            if (!Directory.Exists(dirPath))
                throw new DirectoryNotFoundException
                ("The specified directory could not be found.");

            router.AddHandler(format, (rq, rs, pr) => {
                string filePath = String.Format(@"{0}\{1}", dirPath, pr.file);

                if (!File.Exists(filePath))
                    throw new HttpException(HttpStatus.NotFound);

                using (FileStream fs = new FileStream(filePath, FileMode.Open))
                {
                    if (!Path.HasExtension(filePath))
                    {
                        rs.SetHeader("Content-Type", "text/plain");
                    }
                    else
                    {
                        string ext = Path.GetExtension(filePath);
                        rs.SetHeader("Content-Type", MediaTypeMap.Find(ext));
                    }

                    byte[] response = new byte[fs.Length];
                    fs.Read(response, 0, response.Length);

                    rs.Write(response);
                }
            });
        }

        /// <summary>
        /// A map of media types and file extensions used to determine the media type of files.
        /// </summary>
        public static MediaTypeMap MediaTypeMap
        {
            get;
            set;
        }
    }
}
