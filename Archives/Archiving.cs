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

using McSherry.Zener.Net;
using McSherry.Zener.Core;

namespace McSherry.Zener.Archives
{
    /// <summary>
    /// Provides a set of methods related to archiving.
    /// </summary>
    public static class Archiving
    {
        private const string RTR_ADDARCHIVE_PARAM = "file";

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
            router.AddArchive(format, new string[0], archive, caseSensitive);
        }
        /// <summary>
        /// Adds a handler which serves from an archive
        /// to the router.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="method">The method for this route to be constrained to.</param>
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
            string format, string method,
            Archive archive, bool caseSensitive = false
            )
        {
            router.AddArchive(
                format, new string[1] { method }, archive, caseSensitive
                );
        }
        /// <summary>
        /// Adds a handler which serves from an archive
        /// to the router.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="methods">The methods for this route to be constrained to.</param>
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
            string format, IEnumerable<string> methods,
            Archive archive, bool caseSensitive = false
            )
        {
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

            // If the archive inherits from SingleFileArchive, it
            // will only contain ever contain a single file. Additionally,
            // it isn't guaranteed that this file will have a name stored.
            if (archive is SingleFileArchive)
            {
                var sfa = (SingleFileArchive)archive;
                var data = sfa.Data as byte[] ?? sfa.Data.ToArray();
                var mt = router
                    .MediaTypes
                    .FindMediaType(sfa.Filename, FindParameterType.NameOrPath);

                router.AddHandler(
                    format, methods,
                    (request, response, parameters) =>
                    {
                        response.Headers.Add("Content-Type", mt.Item1);
                        response.Write(mt.Item2(data));
                    });
            }
            else
            {
                if (!Routing.GetParameters(format).Contains(RTR_ADDARCHIVE_PARAM))
                    throw new FormatException(
                        String.Format(
                            @"The provided format string does not contain a variable named ""{0}"".",
                            RTR_ADDARCHIVE_PARAM
                        ));

                StringComparison comparison = caseSensitive
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase
                    ;

                router.AddHandler(
                    format, methods,
                    (request, response, parameters) =>
                    {
                        string file = parameters.file;
                        string name = archive.Files
                                .Where(s => s.Equals(file, comparison))
                                .FirstOrDefault();

                        if (name == default(string))
                            throw new HttpException(HttpStatus.NotFound);

                        var data = archive.GetFile(name) as byte[]
                            ?? archive.GetFile(name).ToArray();
                        var mt = router.MediaTypes
                            .FindMediaType(name, FindParameterType.NameOrPath);

                        response.Headers.Add("Content-Type", mt.Item1);
                        response.Write(mt.Item2(data));
                    });
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
            router.AddTarArchive(format, new string[0], stream, caseSensitive);
        }
        /// <summary>
        /// Adds a handler to the router which serves
        /// files from a UNIX V6 Tape Archive or a
        /// POSIX IEEE P1003.1 Uniform Standard Tape
        /// Archive.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="method">The method for this route to be constrained to.</param>
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
            string format, string method,
            Stream stream, bool caseSensitive = false
            )
        {
            router.AddTarArchive(
                format, new string[1] { method }, stream, caseSensitive
                );
        }
        /// <summary>
        /// Adds a handler to the router which serves
        /// files from a UNIX V6 Tape Archive or a
        /// POSIX IEEE P1003.1 Uniform Standard Tape
        /// Archive.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="methods">The methods for this route to be constrained to.</param>
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
            string format, IEnumerable<string> methods,
            Stream stream, bool caseSensitive = false
            )
        {
            UstarArchive ustar;

            try
            {
                using (stream) ustar = new UstarArchive(stream);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "The stream did not contain a valid Tar/Ustar archive.",
                    ex
                    );
            }

            router.AddArchive(format, methods, ustar, caseSensitive);
        }

        /// <summary>
        /// Adds a handler to the router which serves files
        /// from a GNU Zip (Gzip) archive.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="stream">The stream containing the archive.</param>
        /// <param name="caseSensitive">
        /// Whether the names of the files in the archive should be
        /// considered case-sensitive.
        /// </param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used as the file name.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the constructor for GzipArchive throws
        ///     an exception.
        /// </exception>
        public static void AddGzipArchive(
            this Router router,
            string format, Stream stream,
            bool caseSensitive = false
            )
        {
            router.AddGzipArchive(format, new string[0], stream, caseSensitive);
        }
        /// <summary>
        /// Adds a handler to the router which serves files
        /// from a GNU Zip (Gzip) archive.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="method">The method for this route to be constrained to.</param>
        /// <param name="stream">The stream containing the archive.</param>
        /// <param name="caseSensitive">
        /// Whether the names of the files in the archive should be
        /// considered case-sensitive.
        /// </param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used as the file name.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the constructor for GzipArchive throws
        ///     an exception.
        /// </exception>
        public static void AddGzipArchive(
            this Router router,
            string format, string method,
            Stream stream, bool caseSensitive = false
            )
        {
            router.AddGzipArchive(
                format, new string[1] { method }, stream, caseSensitive
                );
        }
        /// <summary>
        /// Adds a handler to the router which serves files
        /// from a GNU Zip (Gzip) archive.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="methods">The methods for this route to be constrained to.</param>
        /// <param name="caseSensitive">
        /// Whether the names of the files in the archive should be
        /// considered case-sensitive.
        /// </param>
        /// <param name="stream">The stream containing the archive.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used as the file name.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the constructor for GzipArchive throws
        ///     an exception.
        /// </exception>
        public static void AddGzipArchive(
            this Router router,
            string format, IEnumerable<string> methods,
            Stream stream, bool caseSensitive = false
            )
        {
            GzipArchive gzip;

            try
            {
                using (stream) gzip = new GzipArchive(stream);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "The provided stream did not contain a valid Gzip archive.",
                    ex
                    );
            }

            router.AddArchive(format, methods, gzip, caseSensitive);
        }

        /// <summary>
        /// Adds a handler to the router which serves files from
        /// a Microsoft Cabinet (MS-CAB) archive.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="stream">The stream containing the archive.</param>
        /// <param name="caseSensitive">
        /// Whether the names of the files in the archive should be
        /// considered case-sensitive.
        /// </param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used as the file name.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the stream does not contain a valid cabinet
        ///     archive.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        ///     Thrown when the cabinet archive uses features which are
        ///     not supported.
        /// </exception>
        public static void AddCabinetArchive(
            this Router router,
            string format, Stream stream,
            bool caseSensitive = false
            )
        {
            router.AddCabinetArchive(format, new string[0], stream, caseSensitive);
        }
        /// <summary>
        /// Adds a handler to the router which serves files from
        /// a Microsoft Cabinet (MS-CAB) archive.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="method">The method for this route to be constrained to.</param>
        /// <param name="caseSensitive">
        /// Whether the names of the files in the archive should be
        /// considered case-sensitive.
        /// </param>
        /// <param name="stream">The stream containing the archive.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used as the file name.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the stream does not contain a valid cabinet
        ///     archive.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        ///     Thrown when the cabinet archive uses features which are
        ///     not supported.
        /// </exception>
        public static void AddCabinetArchive(
            this Router router,
            string format, string method,
            Stream stream, bool caseSensitive = false
            )
        {
            router.AddCabinetArchive(
                format, new string[1] { method }, stream, caseSensitive
                );
        }
        /// <summary>
        /// Adds a handler to the router which serves files from
        /// a Microsoft Cabinet (MS-CAB) archive.
        /// </summary>
        /// <param name="router">The router to add the handler to.</param>
        /// <param name="format">The format string for the handler.</param>
        /// <param name="methods">The methods for this route to be constrained to.</param>
        /// <param name="stream">The stream containing the archive.</param>
        /// <param name="caseSensitive">
        /// Whether the names of the files in the archive should be
        /// considered case-sensitive.
        /// </param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when <paramref name="format"/> does not contain a
        ///     variable to be used as the file name.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the stream does not contain a valid cabinet
        ///     archive.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        ///     Thrown when the cabinet archive uses features which are
        ///     not supported.
        /// </exception>
        public static void AddCabinetArchive(
            this Router router,
            string format, IEnumerable<string> methods,
            Stream stream, bool caseSensitive = false
            )
        {
            CabinetArchive cab;

            try
            {
                using (stream) cab = new CabinetArchive(stream);
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "The provided stream did not contain a valid cabinet archive.",
                    ex
                    );
            }

            router.AddArchive(format, methods, cab, caseSensitive);
        }
    }
}
