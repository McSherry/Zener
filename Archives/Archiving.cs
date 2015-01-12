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
        /// <param name="caseSensitive">Whether file names should be case-sensitive.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used as the file name.
        /// </exception>
        /// <exception cref="System.InvalidDataException">
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
        /// <exception cref="System.InvalidDataException">
        ///     Thrown when the TarArchive/UstarArchive constructor throws
        ///     an exception.
        /// </exception>
        public static void AddTarArchive(
            this Router router,
            string format, Stream stream,
            bool caseSensitive = false
            )
        {
            var paramNames = Routing.GetParameters(format);

            if (!paramNames.Contains(RTR_ADDARCHIVE_PARAM))
                throw new ArgumentException(
                    String.Format(
                        @"The provided format does not contain a variable named ""{0}""",
                        RTR_ADDARCHIVE_PARAM
                    ));

            Archive tar;
            try
            {
                tar = new UstarArchive(stream);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "The provided data was not a valid tar or ustar archive.",
                    ex
                    );
            }

            router.AddHandler(format, (rq, rs, pr) =>
            {
                string fname = (string)pr.file;
                var cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var fmatches = tar.Files
                    .Where(f => fname.Equals(f, cmp))
                    .FirstOrDefault();

                if (fmatches == null)
                {
                    throw new HttpException(HttpStatus.NotFound);
                }

                IEnumerable<byte> file;
                tar.GetFile(fmatches, out file);

                rs.Headers.Add(
                    "Content-Type",
                    Routing.MediaTypes.Find(fmatches, FindParameterType.NameOrPath)
                    );

                rs.Write(file);
            });
        }
    }
}
