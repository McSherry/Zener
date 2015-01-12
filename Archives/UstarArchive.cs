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

namespace SynapLink.Zener.Archives
{
    /// <summary>
    /// Represents a POSIX IEEE P1003.1 Uniform Standard
    /// Tape Archive file.
    /// </summary>
    public sealed class UstarArchive
        : TarArchive
    {
        /// <summary>
        /// Creates a new UstarArchive class.
        /// </summary>
        /// <param name="stream">The stream containing the archive's bytes.</param>
        /// <exception cref="System.IOException">
        ///     Thrown when a temporary file could not be opened for writing.
        /// </exception>
        /// <exception cref="System.InvalidDataException">
        ///     Thrown when the stream's length is shorter than the length of
        ///     one Tape Archive block.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        ///     Thrown when the stream's length is not an integral multiple of
        ///     the Tape Archive block size.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the stream does not support the required operations.
        /// </exception>
        public UstarArchive(Stream stream)
            : base(stream)
        {

        }
    }
}
