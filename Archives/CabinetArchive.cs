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
    /// Represents a Microsoft Cabinet/MS-CAB archive.
    /// </summary>
    public sealed class CabinetArchive
        : Archive, IDisposable
    {
        private enum CFFOLDERCompressionType : ushort
        {
            None        = 0x0000,
            MSZIP       = 0x0001,
            Quantum     = 0x0002,
            LZX         = 0x0003
        }
        private struct CFFOLDER
        {
            public CFFOLDER(Stream source, byte abReserveLength)
            {
                byte[] cffolderBuf = new byte[CFFOLDER_MIN_LENGTH];
                source.Read(cffolderBuf, 0, CFFOLDER_MIN_LENGTH);
                // We don't care about the application-specific bit
                // after the main CFFOLDER header, but we do need to
                // skip past it.
                source.Seek(abReserveLength, SeekOrigin.Current);

                var ccsBytes = cffolderBuf.Take(4);
                var ccfdBytes = cffolderBuf.Skip(4).Take(2);
                var tcBytes = cffolderBuf.Skip(6).Take(2);

                if (!BitConverter.IsLittleEndian)
                {
                    ccsBytes = ccsBytes.Reverse();
                    ccfdBytes = ccfdBytes.Reverse();
                    tcBytes = tcBytes.Reverse();
                }

                coffCabStart = BitConverter.ToUInt32(ccsBytes.ToArray(), 0);
                cCFData = BitConverter.ToUInt16(ccfdBytes.ToArray(), 0);
                typeCompress = (CFFOLDERCompressionType)BitConverter
                    .ToUInt16(tcBytes.ToArray(), 0);
            }

            public readonly uint coffCabStart;
            public readonly ushort cCFData;
            public readonly CFFOLDERCompressionType typeCompress;
        }
        private enum CFFILEFolderIndex : ushort
        {
            ContinuedFromPrevious       = 0xFFFD,
            ContinuedToNext             = 0xFFFE,
            ContinuedPreviousAndNext    = 0xFFFF
        }
        [Flags]
        private enum CFFILEAttributes : ushort
        {
            ReadOnly    = 1 << 1,
            Hidden      = 1 << 2,
            System      = 1 << 3,
            Modified    = 1 << 6,
            Execute     = 1 << 7,
            UtfName     = 1 << 8,
            Reserved    = 0xFE31
        }
        private struct CFFILE
        {
            public CFFILE(Stream source)
            {
                byte[] minHdrBuf = new byte[CFFILE_MIN_LENGTH];
                source.Read(minHdrBuf, 0, CFFILE_MIN_LENGTH);

                var cfBytes = minHdrBuf.Skip(0).Take(4);
                var ufsBytes = minHdrBuf.Skip(4).Take(4);
                var ifBytes = minHdrBuf.Skip(8).Take(2);
                var dBytes = minHdrBuf.Skip(10).Take(2);
                var tBytes = minHdrBuf.Skip(12).Take(2);
                var aBytes = minHdrBuf.Skip(14).Take(2);

                if (!BitConverter.IsLittleEndian)
                {
                    cfBytes = cfBytes.Reverse();
                    ufsBytes = ufsBytes.Reverse();
                    ifBytes = ifBytes.Reverse();
                    dBytes = dBytes.Reverse();
                    tBytes = tBytes.Reverse();
                    aBytes = aBytes.Reverse();
                }

                cbFile = BitConverter.ToUInt32(cfBytes.ToArray(), 0);
                uoffFolderStart = BitConverter.ToUInt32(ufsBytes.ToArray(), 0);
                iFolder = (CFFILEFolderIndex)BitConverter.ToUInt16(ifBytes.ToArray(), 0);
                date = BitConverter.ToUInt16(dBytes.ToArray(), 0);
                time = BitConverter.ToUInt16(tBytes.ToArray(), 0);
                attribs = (CFFILEAttributes)BitConverter.ToUInt16(aBytes.ToArray(), 0);

                List<byte> snBytes = new List<byte>();
                source.ReadUntilFound(ASCII_NUL, snBytes.Add);

                Encoding nameEncoder;
                if ((attribs & CFFILEAttributes.UtfName) == CFFILEAttributes.UtfName)
                {
                    nameEncoder = Encoding.Unicode;
                }
                else
                {
                    nameEncoder = Encoding.ASCII;
                }

                szName = nameEncoder.GetString(snBytes.ToArray());
            }

            public readonly uint cbFile;
            public readonly uint uoffFolderStart;
            public readonly CFFILEFolderIndex iFolder;
            public readonly ushort date;
            public readonly ushort time;
            public readonly CFFILEAttributes attribs;
            public readonly string szName;
        }
        private struct CFDATA
        {
            public CFDATA(Stream source, int abReserveLength)
            {
                byte[] minHdr = new byte[CFDATA_MIN_LENGTH];
                source.Read(minHdr, 0, CFDATA_MIN_LENGTH);

                var cBytes = minHdr.Skip(0).Take(4);
                var cdBytes = minHdr.Skip(4).Take(2);
                var cuBytes = minHdr.Skip(6).Take(2);

                if (!BitConverter.IsLittleEndian)
                {
                    cBytes = cBytes.Reverse();
                    cdBytes = cdBytes.Reverse();
                    cuBytes = cuBytes.Reverse();
                }

                csum = BitConverter.ToUInt32(cBytes.ToArray(), 0);
                cbData = BitConverter.ToUInt16(cdBytes.ToArray(), 0);
                cbUncomp = BitConverter.ToUInt16(cuBytes.ToArray(), 0);
                ab = new byte[cbData];

                source.Seek(abReserveLength, SeekOrigin.Current);

                source.Read(ab, 0, cbData);
            }

            public readonly uint csum;
            public readonly ushort cbData, cbUncomp;
            public readonly byte[] ab;
        }

        private static readonly IEnumerable<byte> Signature;
        private const int
            ASCII_NUL               = 0x00,

            U1                      = 1,
            U2                      = 2,
            U4                      = 4,
            HEADER_MIN_LENGTH       = 36,
            CFFOLDER_MIN_LENGTH     = 8,
            CFFILE_MIN_LENGTH       = 16,
            CFDATA_MIN_LENGTH       = 8,
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
            DATA_ABRLEN_LEN         = U1,
            // The bytes comprising an MSZIP signature.
            MSZIP_SIGBYTE_0         = 0x43,
            MSZIP_SIGBYTE_1         = 0x4B
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

        // The absolute offset of the first CFFILE block.
        private uint _filesFirstOffset;
        private ushort 
            // The number of files/folders stored within
            // the archive.
            _filesCount, _foldersCount,
            // The length of the CFHEADER block's abReserve
            // field.
            _headerAbLength;
        // The lengths of the abReserve fields in CFFOLDER
        // and CFDATA blocks, respectively.
        private byte _folderAbLength, _dataAbLength;
        // Flags indicating the presence of optional fields
        // within the cabinet's main header.
        private CabinetHeaderFlags _flags;

        // The buffer storing all data extracted
        // from the cabinet archive.
        private KeyedFileBuffer<string> _files;

        private void _parseCabStream(Stream stream)
        {
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

            #region Skip past optional headers
            // If this isn't the first archive in a set, there'll be additional
            // header fields providing information about any previous archives.
            if ((_flags & CabinetHeaderFlags.IsNotFirst) == CabinetHeaderFlags.IsNotFirst)
            {
                // There are two null-terminated fields giving information
                // about previous archives. This makes it fairly simple to
                // skip past them.
                stream.ReadUntilFound(ASCII_NUL, b => { });
                stream.ReadUntilFound(ASCII_NUL, b => { });
            }
            // As with not being the first in a set, not being the last in a set
            // means that there are additional fields present within the archive's
            // headers.
            if ((_flags & CabinetHeaderFlags.IsNotLast) == CabinetHeaderFlags.IsNotLast)
            {
                // And, as with information about previous archives, information
                // about archives to come is null-terminated.
                stream.ReadUntilFound(ASCII_NUL, b => { });
                stream.ReadUntilFound(ASCII_NUL, b => { });
            }
            #endregion
            #region Read all folder details from the cabinet
            List<CFFOLDER> folders = new List<CFFOLDER>(_foldersCount);
            // CFFOLDER entries are sequential within the cabinet
            // archive, and the first immediately follows the end
            // of the CFHEADER block.
            for (int i = 0; i < _foldersCount; i++)
            {
                var fdr = new CFFOLDER(stream, _folderAbLength);

                if (
                    fdr.typeCompress == CFFOLDERCompressionType.Quantum ||
                    fdr.typeCompress == CFFOLDERCompressionType.LZX
                    )
                {
                    throw new NotSupportedException(
                        String.Format(
                            "{0} {1} (folder {2}).",
                            "The cabinet contains data compressed with an",
                            "unsupported algorithm",
                            i
                        ));
                }

                folders.Add(fdr);
            }
            #endregion
            #region Read all file details from the cabinet
            List<CFFILE> files = new List<CFFILE>();
            // The first CFFILE entry is at an offset given within the
            // CFHEADER block. We need to skip to this offset before
            // we start reading.
            stream.Position = _filesFirstOffset;
            // Once at the specified offset, CFFILE blocks are 
            // contiguous.
            for (int i = 0; i < _filesCount; i++)
            {
                var file = new CFFILE(stream);

                // We're not going to bother to support multi-cabinet
                // archives right now. If the file indicates that it
                // spans multiple archives, skip it.
                if (
                    file.iFolder == CFFILEFolderIndex.ContinuedFromPrevious ||
                    file.iFolder == CFFILEFolderIndex.ContinuedPreviousAndNext ||
                    file.iFolder == CFFILEFolderIndex.ContinuedToNext
                    )
                {
                    continue;
                }

                if (folders.Count <= (ushort)file.iFolder)
                    throw new InvalidDataException(
                        String.Format(
                            "The cabinet references an invalid folder (folder {0}).",
                            file.iFolder
                        ));

                files.Add(file);
            }
            #endregion

            #region Read file data, add to CabinetArchive's list
            MemoryStream dataBuf = new MemoryStream();
            for (int i = 0; i < folders.Count; i++)
            {
                var fdr = folders[i];

                var fdrFiles = files.Where(f => (int)f.iFolder == i);

                dataBuf.Position = 0;
                stream.Position = fdr.coffCabStart;
                for (int j = 0; j < fdr.cCFData; j++)
                {
                    var data = new CFDATA(stream, _dataAbLength);

                    if (fdr.typeCompress == CFFOLDERCompressionType.None)
                    {
                        _uncompGet(data, dataBuf);
                    }
                    else if (fdr.typeCompress == CFFOLDERCompressionType.MSZIP)
                    {
                        _mszipInflate(data, dataBuf);
                    }
                    else
                    {
                        throw new NotSupportedException(
                            "The cabinet contains data compressed with an unsupported algorithm."
                            );
                    }
                }

                foreach (var file in fdrFiles)
                {
                    if (file.cbFile > Int32.MaxValue)
                    {
                        throw new InternalBufferOverflowException(
                            "Files larger than 2GiB - 1 byte are not supported."
                            );
                    }

                    byte[] fileBytes = new byte[file.cbFile];
                    dataBuf.Position = file.uoffFolderStart;
                    dataBuf.Read(fileBytes, 0, fileBytes.Length);

                    _files.Add(file.szName, fileBytes);
                }
            }
            #endregion
        }
        private void _mszipInflate(CFDATA data, Stream dump)
        {
            // MSZIP block have a 2-byte signature. If the signature
            // isn't present, either we're not dealing with an MSZIP
            // block, or the block is corrupt.
            if (
                data.ab[0] != MSZIP_SIGBYTE_0 ||
                data.ab[1] != MSZIP_SIGBYTE_1
                )
            {
                throw new InvalidDataException(
                    "The cabinet contains an invalid MSZIP block."
                    );
            }

            // Each CFDATA block is compressed, so we need
            // to decompress them.
            using (var ms = new MemoryStream(data.ab, 2, data.ab.Length - 2))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                // We then copy the decompressed contents to the buffer
                // we're using to contain all data from CFDATA blocks for
                // this folder.
                ds.CopyTo(dump);
            }
        }
        private void _uncompGet(CFDATA data, Stream dump)
        {
            dump.Write(data.ab, 0, data.ab.Length);
        }

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
        ///     
        ///     Thrown when the cabinet makes references to an invalid
        ///     folder.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        ///     Thrown when the file format version of the provided cabinet
        ///     file is not supported by the class.
        ///     
        ///     Thrown when the archive contained in the passed Stream
        ///     uses a compression method that is not supported.
        /// </exception>
        /// <exception cref="System.IO.IOException">
        ///     Thrown when a temporary file could not be opened.
        /// </exception>
        /// <exception cref="System.IO.InternalBufferOverflowException">
        ///     Thrown when the file in the archive is greater than
        ///     2GiB - 1 byte in length.
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

            _files = new KeyedFileBuffer<string>();

            _parseCabStream(stream);
        }

        /// <summary>
        /// The number of files contained within the cabinet.
        /// </summary>
        public override int Count
        {
            get { return _files.Count; }
        }
        /// <summary>
        /// The names of the files contained within the cabinet.
        /// </summary>
        public override IEnumerable<string> Files
        {
            get { return _files.Keys; }
        }

        /// <summary>
        /// Retrieves a file based on its name.
        /// </summary>
        /// <param name="name">The name of the file to retrieve.</param>
        /// <param name="contents">The contents of the retrieved file.</param>
        /// <returns>True if a file with the given name exists within the archive.</returns>
        public override bool GetFile(string name, out IEnumerable<byte> contents)
        {
            return _files.TryGetValue(name, out contents);
        }
        /// <summary>
        /// Releases the resources used by this class.
        /// </summary>
        public override void Dispose()
        {
            _files.Dispose();
        }
    }
}
