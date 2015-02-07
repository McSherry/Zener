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
        : SingleFileArchive, IDisposable
    {
        private const int
            // The minimum length of the Zlib
            // archive's header.
            HEADER_MIN_LENGTH   = 0x02,
            // The offset and length of the compression
            // method and flags byte.
            CMF_OFFSET          = 0x00,
            CMF_LENGTH          = 0x01,
            // The offset and length of the flags byte.
            FLG_OFFSET          = 0x01,
            FLG_LENGTH          = 0x01,
            // The offset and length of the dictionary
            // identifier header.
            DICTID_OFFSET       = 0x02,
            DICTID_LENGTH       = 0x04,
            // The offset from the end of the file of
            // an Adler32 checksum of the uncompressed
            // data stored in the file.
            ADLER32_OFFSET      = 0x04,
            ADLER32_LENGTH      = ADLER32_OFFSET
            ;

        /// <summary>
        /// An enum containing the compression method
        /// identifiers used within Zlib archives.
        /// </summary>
        private enum ZlibCompressionMethod : byte
        {
            /// <summary>
            /// The bit mask used to extract the
            /// relevant bits from the CMF byte.
            /// </summary>
            BitMask     = 0x0F,
            /// <summary>
            /// The DEFLATE compression algorithm.
            /// </summary>
            DEFLATE     = 0x08
        }
        /// <summary>
        /// An enum containing values relevant to the
        /// compression info field in Zlib archives.
        /// </summary>
        private enum ZlibCompressionInfo : byte
        {
            /// <summary>
            /// The bit mask for extracting the bits of
            /// the compression information field from
            /// the CMF byte.
            /// </summary>
            BitMask     = 0xF0
        }
        /// <summary>
        /// An enum containing flags used within the
        /// Zlib archvie flags byte.
        /// </summary>
        private enum ZlibFlags : byte
        {
            /// <summary>
            /// A bit mask used to extract the check bits
            /// from the FLG field.
            /// </summary>
            CheckBits               = 0x1F,
            /// <summary>
            /// The flag that indicates whether the DICTID
            /// field is present within the archive.
            /// </summary>
            DictionaryIdPresent     = 0x20,
            /// <summary>
            /// The bit mask used to extract the compression
            /// level information from the FLG field.
            /// </summary>
            CompressionLevelBits    = 0xC0,
            /// <summary>
            /// The fastest DEFLATE compression level.
            /// </summary>
            FastestCompression      = 0x00,
            /// <summary>
            /// The fast DEFLATE compression level.
            /// </summary>
            FastCompression         = 0x40,
            /// <summary>
            /// The default DEFLATE compression level.
            /// </summary>
            DefaultCompression      = 0x80,
            /// <summary>
            /// The best but slowest DEFLATE compression level.
            /// </summary>
            MaximumCompression      = 0xC0
        }

        // The name of the file stored within
        // the archive.
        private string _name;
        // The uncompressed data stored within
        // the archive.
        private byte[] _data;
        // The Adler32 checksum stored in the
        // archive's trailer.
        private byte[] _checksum;
        // The compression method identifier from the archive's
        // headers.
        private ZlibCompressionMethod _compMethod;
        // The compression method information from the archive's
        // headers.
        private ZlibCompressionInfo _compInfo;
        // The flags from the archive's headers.
        private ZlibFlags _flags;

        /// <summary>
        /// Creates a new ZlibArchive.
        /// </summary>
        /// <param name="stream">The stream containing the archive's bytes.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided stream does not support reading
        ///     or does not support seeking.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided stream is null.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        ///     Thrown when the archive uses a compression method that is
        ///     not supported by the class.
        /// </exception>
        /// <exception cref="System.IO.InternalBufferOverflowException">
        ///     Thrown when the file in the archive is greater than
        ///     2,147,483,591 bytes in length.
        /// </exception>
        public ZlibArchive(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(
                    "The provided stream cannot be null."
                    );
            }

            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException(
                    "The provided stream must support reading/seeking."
                    );
            }

            byte[] hdrBuf = new byte[HEADER_MIN_LENGTH];
            stream.Read(hdrBuf, 0, HEADER_MIN_LENGTH);

            byte cmf = hdrBuf[CMF_OFFSET],
                flg = hdrBuf[FLG_OFFSET];
            _compMethod = (ZlibCompressionMethod)(cmf & (byte)ZlibCompressionMethod.BitMask);
            _compInfo = (ZlibCompressionInfo)(cmf & (byte)ZlibCompressionInfo.BitMask);
            _flags = (ZlibFlags)flg;

            // RFC 1950 only specifies DEFLATE as a compression method. As this
            // is the document we're working to be compatible with, we'll only
            // be supporting DEFLATE.
            //
            // If the archive specifies a different compression method, throw an
            // exception to indicate that the method is unsupported.
            if (_compMethod != ZlibCompressionMethod.DEFLATE)
            {
                throw new NotSupportedException(
                    "The archive uses an unsupported compression method."
                    );
            }

            // We won't be making use of the DICTID field, but we still need
            // to know whether it's present so we can skip past it to the compressed
            // data.
            if ((_flags & ZlibFlags.DictionaryIdPresent) == ZlibFlags.DictionaryIdPresent)
            {
                stream.Seek(DICTID_LENGTH, SeekOrigin.Current);
            }

            long remainingBytes = stream.Length - stream.Position;
            // The array storing file data will be limited to 2,147,483,591 bytes in length.
            // We need to check that the quantity of bytes is not too great.
            if (remainingBytes > ByteArrayMaxLength)
            {
                throw new InternalBufferOverflowException(
                    "The class does not support files greater than 2,147,483,591 bytes in length."
                    );
            }

            // The remaining data, minus the trailer, will be our compressed
            // data. We can read all the data, copy out the trailer, then
            // resize this array.
            byte[] cData = new byte[remainingBytes];
            byte[] trlBuf = new byte[ADLER32_LENGTH];

            stream.Read(cData, 0, (int)remainingBytes);
            Buffer.BlockCopy(
                src:        cData,
                srcOffset:  cData.Length - trlBuf.Length,
                dst:        trlBuf,
                dstOffset:  0,
                count:      trlBuf.Length
                );
            Array.Resize(ref cData, cData.Length - trlBuf.Length);

            _checksum = trlBuf;
            _name = _checksum
                .Aggregate(
                    new StringBuilder(),
                    (sb, b) => sb.Append(b.ToString("x"))
                    )
                .ToString();

            using (var oms = new MemoryStream())
            {
                using (var ims = new MemoryStream(cData))
                using (var ds = new DeflateStream(ims, CompressionMode.Decompress))
                {
                    ds.CopyTo(oms);
                }

                _data = oms.GetBuffer();
                Array.Resize(ref _data, (int)oms.Length);
            }
        }

        /// <summary>
        /// The data contained within the Zlib archive.
        /// </summary>
        public override IEnumerable<byte> Data
        {
            get { return _data.Clone() as IEnumerable<byte>; }
        }

        /// <summary>
        /// Releases the resources held by the class. Does nothing.
        /// </summary>
        public override void Dispose()
        {
            return;
        }
    }
}
