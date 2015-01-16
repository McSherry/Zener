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
            U1 = 1,
            U2 = 2,
            U4 = 4,
            HEADER_MIN_LENGTH = 36,
            // Mandatory header field offsets. These
            // header fields will always be present within
            // a cabinet file.
            SIGNATURE_OFFSET = 0,
            SIGNATURE_LEN = U4,

            SIZE_OFFSET = 8,
            SIZE_LEN = U4,

            FILE_OFFSET_OFFSET = 16,
            FILE_OFFSET_LEN = U4,

            VERSION_MIN_OFFSET = 24,
            VERSION_MIN_LEN = U1,
            VERSION_MAJ_OFFSET = 25,
            VERSION_MAJ_LEN = U1,

            FOLDER_COUNT_OFFSET = 26,
            FOLDER_COUNT_LEN = U2,
            FILE_COUNT_OFFSET = 28,
            FILE_COUNT_LEN = U2,

            HEADER_FLAGS_OFFSET = 30,
            HEADER_FLAGS_LEN = U2,

            CABINET_SETID_OFFSET = 32,
            CABINET_SETID_LEN = U2,
            CABINET_SETLEN_OFFSET = 34,
            CABINET_SETLEN_LEN = U2,

            // The offset of the field containing
            // the length of the abReserve field in this
            // CFHEADER block.
            HDR_ABRLEN_OFFSET = 36,
            HDR_ABRLEN_LEN = U2,
            // The offset of the abReserve field in this
            // CFHEADER block.
            HDR_ABR_OFFSET = 40,
            // The offset of the field containing the
            // length of the abReserve field in each
            // CFFOLDER block.
            FDR_ABRLEN_OFFSET = 38,
            FDR_ABRLEN_LEN = U1,
            // The offset of the field containing the
            // length of the abReserve field in each
            // CFDATA block.
            DATA_ABRLEN_OFFSET = 39,
            DATA_ABRLEN_LEN = U1
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
        private readonly ushort _filesCount, _foldersCount;

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

            byte[] buffer = new byte[HEADER_MIN_LENGTH];
            stream.Read(buffer, 0, HEADER_MIN_LENGTH);
            #region Verify cabinet signature
            if (!buffer.Take(SIGNATURE_LEN).SequenceEqual(Signature))
                throw new InvalidDataException(
                    "The stream does not represent a cabinet archive."
                    );
            #endregion
            #region Verify cabinet length
            uint cabLength;
            if (BitConverter.IsLittleEndian)
            {
                cabLength = BitConverter.ToUInt32(
                    buffer.Skip(SIZE_OFFSET).Take(SIZE_LEN).ToArray(),
                    0
                    );
            }
            else
            {
                cabLength = BitConverter.ToUInt32(
                    buffer.Skip(SIZE_OFFSET).Take(SIZE_LEN).Reverse().ToArray(),
                    0
                    );
            }

            if (stream.Length != cabLength)
                throw new InvalidDataException(
                    "The stream's length does not match the cabinet's length."
                    );
            #endregion
        }
    }
}
