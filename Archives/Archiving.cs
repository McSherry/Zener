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
using SynapLink.Zener.Core;

namespace SynapLink.Zener.Archives
{
    /// <summary>
    /// Provides a set of methods related to archiving.
    /// </summary>
    public static class Archiving
    {
        private const string RTR_ADDARCHIVE_PARAM = "file";

        /// <summary>
        /// Adds a handler to the router which serves
        /// files from a UNIX V6 Tape Archive or a
        /// POSIX IEEE P1003.1 Uniform Standard Tape
        /// Archive.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="filepath">The file path of the archive.</param>
        /// <param name="caseSensitive">Whether file names should be case-sensitive.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used as the file name.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the TarArchive/UstarArchive constructor throws
        ///     an exception.
        /// </exception>
        public static void AddTarArchive(
            this Router router,
            string format, string filepath,
            bool caseSensitive = false
            )
        {
            using (FileStream fs = File.Open(filepath, FileMode.Open))
            {
                router.AddTarArchive(format, fs, caseSensitive);
            }
        }
        /// <summary>
        /// Adds a handler to the router which serves
        /// files from a UNIX V6 Tape Archive or a
        /// POSIX IEEE P1003.1 Uniform Standard Tape
        /// Archive.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="stream">The stream containing the archive.</param>
        /// <param name="caseSensitive">Whether file names should be case-sensitive.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used as the file name.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the TarArchive/UstarArchive constructor throws
        ///     an exception.
        /// </exception>
        public static void AddTarArchive(
            this Router router,
            string format, Stream stream,
            bool caseSensitive = false
            )
        {
            router.AddArchive(format, new UstarArchive(stream), caseSensitive);
        }
        /// <summary>
        /// Adds a handler which serves from an archive
        /// to the router.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="archive">The archive to serve from.</param>
        /// <param name="caseSensitive">Whether the names of archived files are case-sensitive.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the archive passed to the method does not contain
        ///     any files.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when either the router or the archive passed to the
        ///     method is null.
        /// </exception>
        /// <exception cref="System.FormatException">
        ///     Thrown when the format string passed to the method does not
        ///     contain a variable to be used for file names.
        /// </exception>
        public static void AddArchive(
            this Router router,
            string format, Archive archive,
            bool caseSensitive = false
            )
        {
            if (!Routing.GetParameters(format).Contains(RTR_ADDARCHIVE_PARAM))
                throw new FormatException(
                    String.Format(
                        @"The provided format string does not contain a variable named ""{0}"".",
                        RTR_ADDARCHIVE_PARAM
                    ));

            if (router == null)
                throw new ArgumentNullException(
                    "The provided router cannot be null."
                    );

            if (archive == null)
                throw new ArgumentNullException(
                    "The provided archive cannot be null."
                    );

            if (archive.Count <= 0)
                throw new ArgumentException(
                    "The provided archive contains no files."
                    );

            StringComparison comparison = caseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase
                ;

            router.AddHandler(
                format,
                (request, response, parameters) =>
                {
                    string file = parameters.file;
                    string name = archive.Files
                            .Where(s => s.Equals(file, comparison))
                            .FirstOrDefault();

                    if (name == default(string))
                        throw new HttpException(HttpStatus.NotFound);

                    response.Headers.Add(
                        "Content-Type",
                        Routing.MediaTypes.Find(name, FindParameterType.NameOrPath)
                        );
                    response.Write(archive.GetFile(name));
                });
        }
    }
}
