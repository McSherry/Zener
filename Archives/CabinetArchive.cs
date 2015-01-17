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
using System.IO;

namespace SynapLink.Zener.Archives
{
    /// <summary>
    /// Represents a Microsoft Cabinet/MS-CAB archive.
    /// </summary>
    public sealed class CabinetArchive
        : Archive, IDisposable
    {
        private static readonly IEnumerable<byte> Signature;
        private const int
            U1                      = 1,
            U2                      = 2,
            U4                      = 4,
            HEADER_MIN_LENGTH       = 36,
            // Mandatory header field offsets. These
            // header fields will always be present within
            // a cabinet file.
            SIGNATURE_OFFSET        = 0,
            SIGNATURE_LEN           = U4,

            SIZE_OFFSET             = 8,
            SIZE_LEN                = U4,

            FILE_OFFSET_OFFSET      = 16,
            FILE_OFFSET_LEN         = U4,

            VERSION_MIN_OFFSET      = 24,
            VERSION_MIN_LEN         = U1,
            VERSION_MAJ_OFFSET      = 25,
            VERSION_MAJ_LEN         = U1,

            VERSION_MAJ_SUPPORTED   = 1,
            VERSION_MIN_SUPPORTED   = 3,

            FOLDER_COUNT_OFFSET     = 26,
            FOLDER_COUNT_LEN        = U2,
            FILE_COUNT_OFFSET       = 28,
            FILE_COUNT_LEN          = U2,

            HEADER_FLAGS_OFFSET     = 30,
            HEADER_FLAGS_LEN        = U2,

            CABINET_SETID_OFFSET    = 32,
            CABINET_SETID_LEN       = U2,
            CABINET_SETLEN_OFFSET   = 34,
            CABINET_SETLEN_LEN      = U2,

            // The offset of the field containing
            // the length of the abReserve field in this
            // CFHEADER block.
            HDR_ABRLEN_OFFSET       = 36,
            HDR_ABRLEN_LEN          = U2,
            // The offset of the abReserve field in this
            // CFHEADER block.
            HDR_ABR_OFFSET          = 40,
            // The offset of the field containing the
            // length of the abReserve field in each
            // CFFOLDER block.
            FDR_ABRLEN_OFFSET       = 38,
            FDR_ABRLEN_LEN          = U1,
            // The offset of the field containing the
            // length of the abReserve field in each
            // CFDATA block.
            DATA_ABRLEN_OFFSET      = 39,
            DATA_ABRLEN_LEN         = U1
            ;

        /// <summary>
        /// Represents the flags within the
        /// cabinet file's header.
        /// </summary>
        [Flags]
        private enum CabinetHeaderFlags : ushort
        {
            /// <summary>
            /// Indicates that this cabinet file is not
            /// the first in a set of cabinet files.
            /// </summary>
            IsNotFirst      = 0x0001,
            /// <summary>
            /// Indicates that this cabinet file is not
            /// the last in a set of cabinet files.
            /// </summary>
            IsNotLast       = 0x0002,
            /// <summary>
            /// Indicates that the cabinet file contains
            /// the reserved fields cbCFHeader, cbCFFolder,
            /// and cbCFData in the current CFHEADER block.
            /// </summary>
            ReservedPresent = 0x0004
        }

        static CabinetArchive()
        {
            Signature = new byte[4] { 0x4d, 0x53, 0x43, 0x46 };
        }

        private readonly uint _filesFirstOffset;
        private readonly ushort 
            _filesCount, _foldersCount,
            _headerAbLength;
        private readonly byte _folderAbLength, _dataAbLength;
        private readonly CabinetHeaderFlags _flags;

        /// <summary>
        /// Creates a new CabinetArchive.
        /// </summary>
        /// <param name="stream">The stream containing the archive's bytes.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided stream is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided stream does not support the
        ///     required operations.
        /// </exception>
        /// <exception cref="System.IO.InvalidDataException">
        ///     Thrown when the stream's data does not pass verification.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        ///     Thrown when the file format version of the provided cabinet
        ///     file is not supported by the class.
        /// </exception>
        public CabinetArchive(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(
                    "The provided stream cannot be null.",
                    "stream"
                    );

            if (!stream.CanSeek)
                throw new ArgumentException(
                    "The provided stream does not support seeking."
                    );

            if (!stream.CanRead)
                throw new ArgumentException(
                    "The provided stream does not support reading."
                    );

            stream.Position = 0;

            // We know that these headers will be present
            // in every cabinet file, so we can just read
            // them.
            byte[] hdrBuf = new byte[HEADER_MIN_LENGTH];
            stream.Read(hdrBuf, 0, HEADER_MIN_LENGTH);

            #region Verify cabinet signature
            if (!hdrBuf.Take(SIGNATURE_LEN).SequenceEqual(Signature))
                throw new InvalidDataException(
                    "The stream does not represent a cabinet archive."
                    );
            #endregion
            #region Verify cabinet length
            uint cabLength;
            var sizeBytes = hdrBuf.Skip(SIZE_OFFSET).Take(SIZE_LEN);

            if (!BitConverter.IsLittleEndian)
            {
                sizeBytes = sizeBytes.Reverse();
            }

            cabLength = BitConverter.ToUInt32(sizeBytes.ToArray(), 0);

            if (stream.Length < cabLength)
                throw new InvalidDataException(
                    "The stream's length does not match the cabinet's length."
                    );
            #endregion
            #region Verify cabinet version
            if (
                hdrBuf[VERSION_MIN_OFFSET] != VERSION_MIN_SUPPORTED ||
                hdrBuf[VERSION_MAJ_OFFSET] != VERSION_MAJ_SUPPORTED
                )
            {
                throw new NotSupportedException(
                    "The class does not support the provided file format version."
                    );
            }
            #endregion
            #region Set first file offset / file count / folder count / flags
            var fileOffsetBytes = hdrBuf.Skip(FILE_OFFSET_OFFSET).Take(FILE_OFFSET_LEN);
            var fileCountBytes = hdrBuf.Skip(FILE_COUNT_OFFSET).Take(FILE_COUNT_LEN);
            var fdrCountBytes = hdrBuf.Skip(FOLDER_COUNT_OFFSET).Take(FOLDER_COUNT_LEN);
            var flagBytes = hdrBuf.Skip(HEADER_FLAGS_OFFSET).Take(HEADER_FLAGS_LEN);

            if (!BitConverter.IsLittleEndian)
            {
                fileOffsetBytes = fileOffsetBytes.Reverse();
                fileCountBytes = fileCountBytes.Reverse();
                fdrCountBytes = fdrCountBytes.Reverse();
                flagBytes = flagBytes.Reverse();
            }

            _filesFirstOffset = BitConverter.ToUInt32(fileOffsetBytes.ToArray(), 0);
            _filesCount = BitConverter.ToUInt16(fileCountBytes.ToArray(), 0);
            _foldersCount = BitConverter.ToUInt16(fdrCountBytes.ToArray(), 0);
            _flags = (CabinetHeaderFlags)BitConverter.ToUInt16(flagBytes.ToArray(), 0);
            #endregion

            // Check to see if optional variable-length fields are present
            // within the archive's header.
            if ((_flags & CabinetHeaderFlags.ReservedPresent) == CabinetHeaderFlags.ReservedPresent)
            {
                #region Set abReserve lengths
                // Byte buffer for optional fields that are only included
                // if the flag in the above condition is present and set.
                byte[]
                    headerAbLenBytes = new byte[HDR_ABRLEN_LEN],
                    folderAbLenBytes = new byte[FDR_ABRLEN_LEN],
                    dataAbLenBytes = new byte[DATA_ABRLEN_LEN];
                stream.Read(headerAbLenBytes, 0, HDR_ABRLEN_LEN);
                stream.Read(folderAbLenBytes, 0, FDR_ABRLEN_LEN);
                stream.Read(dataAbLenBytes, 0, DATA_ABRLEN_LEN);

                if (!BitConverter.IsLittleEndian)
                {
                    headerAbLenBytes = headerAbLenBytes.Reverse().ToArray();
                }

                _headerAbLength = BitConverter.ToUInt16(headerAbLenBytes, 0);
                _folderAbLength = folderAbLenBytes.First();
                _dataAbLength = dataAbLenBytes.First();
                #endregion

                // We don't care about what's in the abReserve field, since
                // it's application-specific, so we'll just skip past it.
                if (_headerAbLength != 0)
                    stream.Seek(_headerAbLength, SeekOrigin.Current);
            }
        }

        public override int Count
        {
            get { throw new NotImplementedException(); }
        }
        public override IEnumerable<string> Files
        {
            get { throw new NotImplementedException(); }
        }

        public override bool GetFile(string name, out IEnumerable<byte> contents)
        {
            throw new NotImplementedException();
        }
        public override void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
