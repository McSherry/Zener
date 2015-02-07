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
using System.IO.Compression;

namespace McSherry.Zener.Archives
{
    /// <summary>
    /// Represents an RFC 1952 GZip archive file.
    /// </summary>
    public sealed class GzipArchive
        : SingleFileArchive, IDisposable
    {
        [Flags]
        private enum GzipFlags : byte
        {
            /// <summary>
            /// Whether the file is likely to
            /// contain ASCII text.
            /// </summary>
            AsciiText       = 0x01,
            /// <summary>
            /// Whether the archive contains a
            /// Gzip CRC-16 header checksum.
            /// </summary>
            Checksum        = 0x02,
            /// <summary>
            /// Whether the archive uses optional
            /// extra flags.
            /// </summary>
            OptionalExtras  = 0x04,
            /// <summary>
            /// Whether the archive contains an
            /// original file name for the file.
            /// </summary>
            OriginalName    = 0x08,
            /// <summary>
            /// Whether the archive contains a
            /// human-readable comment.
            /// </summary>
            Comment         = 0x10,
            Reserved        = 0xE0
        }
        private enum GzipCompression : byte
        {
            /// <summary>
            /// The DEFLATE compression method
            /// identifier.
            /// </summary>
            DEFLATE         = 0x08
        }
        private enum GzipOs : byte
        {
            /// <summary>
            /// The file is from a FAT file system, such
            /// as is used on MS-DOS, OS/2, or NT/Win32.
            /// </summary>
            FAT         = 0x00,
            /// <summary>
            /// The file is from an Amiga OS.
            /// </summary>
            Amiga       = 0x01,
            /// <summary>
            /// The file is from VMS or OpenVMS.
            /// </summary>
            VMS         = 0x02,
            /// <summary>
            /// The file is from a UNIX system.
            /// </summary>
            UNIX        = 0x03,
            /// <summary>
            /// The file is from a VM/CMS system.
            /// </summary>
            VMCMS       = 0x04,
            /// <summary>
            /// The file is from an Atari TOS system.
            /// </summary>
            AtariTOS    = 0x05,
            /// <summary>
            /// The file is from an HPFS filesystem,
            /// such as is used on OS/2 or NT.
            /// </summary>
            HPFS        = 0x06,
            /// <summary>
            /// The file is from a Macintosh system.
            /// </summary>
            Macintosh   = 0x07,
            /// <summary>
            /// The file is from a Z-System.
            /// </summary>
            ZSystem     = 0x08,
            /// <summary>
            /// The file is from a CP/M system.
            /// </summary>
            CPM         = 0x09,
            /// <summary>
            /// The file is from a TOPS-20 system.
            /// </summary>
            TOPS20      = 0x0A,
            /// <summary>
            /// The file is from an NTFS filesystem.
            /// </summary>
            NTFS        = 0x0B,
            /// <summary>
            /// The file is from a QDOS system.
            /// </summary>
            QDOS        = 0x0C,
            /// <summary>
            /// The file is from an Acorn RISCOS system.
            /// </summary>
            AcornRISCOS = 0x0D,
            /// <summary>
            /// The origin system is unknown.
            /// </summary>
            Unknown     = 0xFF
        }
        private enum GzipExtra : byte
        {
            /// <summary>
            /// The DEFLATE algorithm used slow compression.
            /// </summary>
            DeflateSlow     = 0x02,
            /// <summary>
            /// The DEFLATE algorithm used fast compression.
            /// </summary>
            DeflateFast     = 0x04
        }

        private const byte
            ASCII_NUL       = 0x00,
            // Gzip header is minimally 10 bytes:
            // ID bytes, compression method identifier,
            // flags, modification time, extra flags, and
            // the OS identifier.
            HEADER_LEN_MIN  = 0x0A,
            HEADER_OFFSET   = 0x00,
            // The length of the trailer. Also serves as
            // the offset of the trailer from the end of
            // the archive's bytes.
            TRAILER_LENGTH  = 0x08,
            // The offset of the ISIZE field within the
            // archive's trailer.
            ISIZE_OFFSET    = 0x04,
            
            // The gzip magic number bytes. These are the first
            // two bytes within any gzip archive file.
            ID_1            = 0x1f,
            ID_1_OFFSET     = 0x00,
            ID_2            = 0x8b,
            ID_2_OFFSET     = 0x01,

            // The flags byte
            FLG_OFFSET      = 0x04,
            // Compression method identifier
            CM_OFFSET       = 0x02,
            // Modification time
            MTIME_OFFSET    = 0x03,
            // Extra flags
            XFL_OFFSET      = 0x08,
            // Operating system identifier
            OS_OFFSET       = 0x09,
            // Optional extra length field position
            XLEN_OFFSET     = 0x0A,
            // The number of bytes comprising the XLEN field
            XLEN_LENGTH     = 0x02,
            // The length of the header checksum
            HDRCHKSUM_LEN   = 0x02
            ;
        private const string ISO_ENCODING = "ISO-8859-1";
        /// <summary>
        /// The encoding to use for file names and comments.
        /// </summary>
        private static readonly Encoding IsoEncoding;
        
        static GzipArchive()
        {
            IsoEncoding = Encoding.GetEncoding(ISO_ENCODING);
        }

        // The original file name, if present. Otherwise,
        // the hex representation of the archive's CRC-32.
        private string _name;
        // The length of the uncompressed data.
        private uint _isize;
        // The uncompressed data.
        private byte[] _dcData;
        // Any archive flags.
        private GzipFlags _flags;
        // Extra archive flags
        private GzipExtra _xfl;

        /// <summary>
        /// Creates a new GzipArchive.
        /// </summary>
        /// <param name="stream">The stream containing the archive file's bytes.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided stream does not support the
        ///     required operations.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the provided stream's first two bytes are
        ///     not the Gzip archive magic number.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        ///     Thrown when the compression method specified in the
        ///     archive's header is not supported by the GzipArchive
        ///     class.
        /// </exception>
        /// <exception cref="System.IO.InternalBufferOverflowException">
        ///     Thrown when the file in the archive is greater than
        ///     2,147,483,591 bytes in length.
        /// </exception>
        public GzipArchive(Stream stream)
        {
            if (!stream.CanRead || !stream.CanSeek)
                throw new ArgumentException(
                    "The provided stream does not support reading and seeking.",
                    "stream"
                    );

            stream.Position = HEADER_OFFSET;
            byte[] headerBuf = new byte[HEADER_LEN_MIN];
            stream.Read(headerBuf, 0, HEADER_LEN_MIN);

            // Gzip archives should contain a 2-byte magic number.
            // If it isn't present, we probably don't have a gzip
            // archive file.
            if (
                headerBuf[ID_1_OFFSET] != ID_1 ||
                headerBuf[ID_2_OFFSET] != ID_2
                )
            {
                throw new InvalidDataException(
                    "The provided stream does not contain a Gzip archive."
                    );
            }

            // RFC 1952 specifies a compression method field, but only
            // defines DEFLATE. As we're being RFC 1952-compliant, any
            // value that isn't DEFLATE won't be supported.
            if (headerBuf[CM_OFFSET] != (byte)GzipCompression.DEFLATE)
                throw new NotSupportedException(
                    "The compression method specified in the archive is not supported."
                    );

            _flags = (GzipFlags)headerBuf[FLG_OFFSET];
            _xfl = (GzipExtra)headerBuf[XFL_OFFSET];

            // Our current offset from the Gzip headers.
            long offset = 0;

            // The archive contains an optional extra field. We don't use it,
            // but we need to skip past it.
            if ((_flags & GzipFlags.OptionalExtras) == GzipFlags.OptionalExtras)
            {
                stream.Position = XLEN_OFFSET;
                byte[] xlenBuf = new byte[XLEN_LENGTH];
                stream.Read(xlenBuf, 0, XLEN_LENGTH);

                // Multi-byte fields are stored LSB-first.
                //
                // We won't be reading the XLEN field, but we need
                // to know how long it is so we can read/skip any
                // other fields correctly.
                offset = ((((ushort)xlenBuf[1]) << 8) | xlenBuf[0]) + XLEN_LENGTH;
                stream.Position = HEADER_LEN_MIN + offset;
            }

            // The archive contains the original file name of the stored
            // file. We do want to use this.
            if ((_flags & GzipFlags.OriginalName) == GzipFlags.OriginalName)
            {
                List<byte> ofnBytes = new List<byte>();

                stream.ReadUntilFound(ASCII_NUL, ofnBytes.Add);

                _name = IsoEncoding.GetString(ofnBytes.ToArray());

                offset = stream.Position;
            }
            else _name = null;

            // Some archives will contain a comment. We don't need or
            // want it, but we still need to skip past it.
            if ((_flags & GzipFlags.Comment) == GzipFlags.Comment)
            {
                stream.ReadUntilFound(ASCII_NUL , b => { });
                offset = stream.Position;
            }

            // We won't be doing any integrity checks.
            if ((_flags & GzipFlags.Checksum) == GzipFlags.Checksum)
            {
                offset += HDRCHKSUM_LEN;
                stream.Seek(HDRCHKSUM_LEN, SeekOrigin.Current);
            }

            // The remaining data, minus the file's trailer, should be
            // the compressed blocks. Its length is the length of
            // the stream, minus the length of the headers and the length
            // of the trailer.
            byte[] cDataBuf = new byte[stream.Length - offset - TRAILER_LENGTH];
            stream.Read(cDataBuf, 0, cDataBuf.Length);
            byte[] trailerBuf = new byte[TRAILER_LENGTH];
            stream.Read(trailerBuf, 0, TRAILER_LENGTH);

            if (BitConverter.IsLittleEndian)
                _isize = BitConverter.ToUInt32(trailerBuf, ISIZE_OFFSET);
            else
                _isize = BitConverter.ToUInt32(
                    trailerBuf.Skip(ISIZE_OFFSET).Reverse().ToArray(),
                    0
                    );

            // Arrays only support up to 2^31 - 1 bytes, so we can't store
            // files above this size.
            if (_isize > ByteArrayMaxLength)
            {
                throw new InternalBufferOverflowException(
                    "Files larger than 2,147,483,591 bytes are not supported."
                    );
            }

            using (var ms = new MemoryStream(cDataBuf))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                _dcData = new byte[_isize];

                ds.Read(_dcData, 0, _dcData.Length);
            }

            // If the archive doesn't contain the original file name,
            // set the file name to the file's CRC-32 as a hex string.
            if (_name == null)
                _name = trailerBuf
                    .Take(4)
                    .Aggregate(new StringBuilder(), (sb, b) => sb.Append(b.ToString("x")))
                    .ToString();
        }

        /// <summary>
        /// The name of the file. Uses the Gzip Original Filename
        /// field if present, otherwise uses the hex representation
        /// of the archive's CRC-32.
        /// </summary>
        public override string Filename
        {
            get { return _name; }
        }
        /// <summary>
        /// The contents of the file contained within the Gzip archive.
        /// </summary>
        public override IEnumerable<byte> Data
        {
            get { return _dcData.Clone() as IEnumerable<byte>; }
        }

        /// <summary>
        /// Releases the resources used by the class. This is not implemented.
        /// </summary>
        public override void Dispose()
        {
            return;
        }
    }
}
