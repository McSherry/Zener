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
using System.IO;
using System.IO.Compression;

namespace McSherry.Zener.Archives
{
    /// <summary>
    /// Represents an RFC 1950 zlib archive.
    /// </summary>
    public sealed class ZlibArchive
        : Archive, IDisposable
    {
        private const int
            // The offset and length of the compression
            // method and flags byte.
            CMF_OFFSET      = 0x00,
            CMF_LENGTH      = 0x01,
            // The offset and length of the flags byte.
            FLG_OFFSET      = 0x01,
            FLG_LENGTH      = 0x01,
            // The offset and length of the dictionary
            // identifier header.
            DICTID_OFFSET   = 0x02,
            DICTID_LENGTH   = 0x04,
            // The offset from the end of the file of
            // an Adler32 checksum of the uncompressed
            // data stored in the file.
            ADLER32_OFFSET  = 0x04,
            ADLER32_LENGTH  = ADLER32_OFFSET
            ;

        // The name of the file stored within
        // the archive.
        private string _name;
        // The uncompressed data stored within
        // the archive.
        private byte[] _data;

        public ZlibArchive(Stream stream)
        {

        }

        /// <summary>
        /// Retrieves a file based on its name. Always returns the single
        /// file stored within the archive, regardless of the name passed
        /// to it.
        /// </summary>
        /// <param name="name">The name of the file to retrieve.</param>
        /// <param name="contents">The contents of the retrieved file.</param>
        /// <returns>True if a file with the given name exists within the archive.</returns>
        public override bool GetFile(string name, out IEnumerable<byte> contents)
        {
            contents = _data.Clone() as IEnumerable<byte>;

            return true;
        }

        /// <summary>
        /// The number of files stored within the archive.
        /// This always returns 1.
        /// </summary>
        public override int Count
        {
            get { return 1; }
        }
        /// <summary>
        /// The names of the files stored within
        /// the archive. Use of the Filename property
        /// is recommended.
        /// </summary>
        /// <remarks>
        /// A Zlib archive doesn't store the filename
        /// of the file contained within it. As a result,
        /// the filename will always be a hex string of
        /// the archive's Adler32 checksum bytes.
        /// </remarks>
        public override IEnumerable<string> Files
        {
            get { return new[] { _name }; }
        }
    }
}
