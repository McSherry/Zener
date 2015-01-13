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
using System.IO.Compression;

namespace SynapLink.Zener.Archives
{
    /// <summary>
    /// Represents an RFC 1952 GZip archive file.
    /// </summary>
    public sealed class GzipArchive
        : Archive, IDisposable
    {
        // The gzip flag bit positions.
        private const byte
            // If set, the file is likely to be ASCII text.
            // However, setting this bit is not mandatory.
            FLG_FTEXT       = 0 << 0,
            // If this is set, a CRC-16 header is present.
            // This CRC-16 is the lower two bytes of a CRC-32
            // of the header (excluding the checksum).
            // Older versions of GNU zip didn't set this.
            FLG_FHCRC       = 1 << 0,
            // Indicates the presence of optional extra fields
            // when set.
            FLG_FEXTRA      = 1 << 2,
            // Indicates when set that an ISO 8869-1 original
            // file name is present.
            FLG_FNAME       = 1 << 3,
            // Indicates that an ISO 8859-1 comment is present
            // within the file.
            FLG_FCOMMENT    = 1 << 4,
            FLG_RESERVED    = 0xF0;
        private const string ENCODING = "ISO-8859-1";
        /// <summary>
        /// The encoding to use for file names and comments.
        /// </summary>
        private static readonly Encoding IsoEncoding;

        static GzipArchive()
        {
            IsoEncoding = Encoding.GetEncoding(ENCODING);
        }

        public GzipArchive()
        {
            
        }
    }
}
