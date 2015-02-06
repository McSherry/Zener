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

namespace McSherry.Zener.Archives
{
    /// <summary>
    /// Represents a POSIX IEEE P1003.1 Uniform Standard
    /// Tape Archive file.
    /// </summary>
    public sealed class UstarArchive
        : TarArchive, IDisposable
    {
        private static readonly byte[] USTAR_ID;
        private const int 
            USTAR_ID_POS            = 257,
            USTAR_FILEPREFIX_POS    = 345,
            USTAR_FILEPREFIX_MAX    = 155
            ;

        static UstarArchive()
        {
            USTAR_ID = Encoding.ASCII.GetBytes("ustar");
        }

        /// <summary>
        /// Creates a new UstarArchive class.
        /// </summary>
        /// <param name="stream">The stream containing the archive's bytes.</param>
        /// <exception cref="System.IO.IOException">
        ///     Thrown when a temporary file could not be opened for writing.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the stream's length is shorter than the length of
        ///     one Tape Archive block, or when the tar file size field does
        ///     not contain a valid sequence of octal digits.
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
            byte[] idBuffer = new byte[USTAR_ID.Length];
            for (int i = 0; i < base._headers.Count; i++)
            {
                string originalName = base._files.Keys[i];
                // If this header is from a Ustar archive, it will have
                // the ASCII string "ustar" starting at position 257 within
                // the header block.
                //
                // If this identifier is present, we'll continue. If not, we
                // won't make any modifications.
                Buffer.BlockCopy(_headers[i], USTAR_ID_POS, idBuffer, 0, idBuffer.Length);
                if (idBuffer.SequenceEqual(USTAR_ID))
                {
                    StringBuilder nameBuilder = new StringBuilder();
                    // We only care about one of the fields present
                    // in the ustar header: the filename prefix field.
                    // This field allows stored files to have file-names
                    // up to 255 bytes in length.
                    //
                    // As with the normal UNIX V6 tar filename, this should
                    // be terminated with an ASCII NUL, so we'll iterate from
                    // its position, adding the bytes we iterate over, until
                    // we meet a NUL, or until we've iterated over 155 bytes
                    // (the maximum length of the filename prefix field).
                    for (int j = 0; j < USTAR_FILEPREFIX_MAX; j++)
                    {
                        if (_headers[i][USTAR_FILEPREFIX_POS + j] == TarArchive.ASCII_NUL) break;

                        nameBuilder.Append(_headers[i][USTAR_FILEPREFIX_POS + j]);
                    }

                    // This prefix then needs to be prepended to the filename
                    // we already have associated with this header.
                    nameBuilder.Append(originalName);
                    string newName = nameBuilder.ToString();

                    // If the new key is equal to the old key, we don't need
                    // to make any modifications.
                    if (base._files.CompareKeys(newName, originalName))
                    {
                        continue;
                    }

                    // We then need to replace our currently-stored filename
                    // with the new one.
                    base._files.ChangeKey(originalName, nameBuilder.ToString());
                }
            }
        }
    }
}
