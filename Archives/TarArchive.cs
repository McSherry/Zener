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
    /// Represents a UNIX V6 Tape Archive file.
    /// </summary>
    public class TarArchive
        : Archive, IDisposable
    {
        /* To mitigate memory usage, the class will
         * mantain only a "table of contents." The
         * contents of all files will be written,
         * sequentially, to a temporary file.
         * 
         * The table of contents will store file names,
         * file lengths, and the offset of the file's
         * data within the temporary storage file.
         * 
         * Fields within this class are marked protected
         * to allow a class implementing the ustar format
         * to inherit from this class.
         */

        /// <summary>
        /// The number representing base 8/octal.
        /// </summary>
        protected const int RadixOctal          = 8;
        /// <summary>
        /// The size of a TAR block, in bytes.
        /// </summary>
        protected const int TarBlockSize        = 512;
        /// <summary>
        /// The length of the filename field in a TAR archive.
        /// </summary>
        protected const int TarFilenameLength   = 100;
        /// <summary>
        /// The offset, in bytes, of the file size field in
        /// a TAR archive.
        /// </summary>
        protected const int TarFileSizeOffset   = 124;
        /// <summary>
        /// The length, in bytes, of the TAR file size field.
        /// </summary>
        protected const int TarFileSizeLength   = 11;
        /// <summary>
        /// The offset, in bytes, of the TAR file type field.
        /// </summary>
        protected const int TarFileTypeOffset   = 156;

        /// <summary>
        /// The ASCII NUL byte.
        /// </summary>
        protected const byte AsciiNul           = 0x00;
        /// <summary>
        /// The first value of the TAR file type field that
        /// indicates a normal file.
        /// </summary>
        protected const byte FileTypeNormalA    = AsciiNul;
        /// <summary>
        /// The second value of the TAR file type field that
        /// indicates a normal file.
        /// </summary>
        protected const byte FileTypeNormalB    = 0x30;
        /// <summary>
        /// The value of the TAR file type field that
        /// indicates a hard link.
        /// </summary>
        protected const byte FileTypeHardLink   = 0x31;
        /// <summary>
        /// The value of the TAR file type field that
        /// indicates a soft link.
        /// </summary>
        protected const byte FileTypeSoftLink   = 0x32;

        /// <summary>
        /// A keyed file buffer containing file marks
        /// keyed by file names.
        /// </summary>
        protected KeyedFileBuffer<string> FileMarks;
        /// <summary>
        /// A list of byte arrays containing the headers
        /// for each file in the archive.
        /// </summary>
        protected List<byte[]> FileHeaders;
        /// <summary>
        /// The object to lock on in TarArchive and its
        /// subclasses.
        /// </summary>
        protected object LockObject;

        /// <summary>
        /// Parses from a stream a TAR archive, and sets
        /// the properties of this class appropriately.
        /// </summary>
        /// <param name="source">
        /// The stream containing the TAR archive's contents.
        /// </param>
        protected void ParseTarFile(Stream source)
        {
            // A file header is, minimally, 512 bytes. If we
            // don't have 512 bytes, we can't have a valid file
            // header.
            if (source.Length < TarBlockSize)
                throw new InvalidDataException(
                    "The provided stream does not contain enough data."
                    );
            // File lengths should be padded to a multiple of the
            // block length. If it isn't, we won't accept it.
            if (source.Length % TarBlockSize != 0)
                throw new ArgumentOutOfRangeException(
                    "The provided stream's length is invalid."
                    );

            byte[] buffer = new byte[TarBlockSize];
            StringBuilder builder = new StringBuilder();
            bool lastWasEmpty = false;
            long dumpLength = 0;

            do
            {
                builder.Clear();
                int readStatus = source.Read(buffer, 0, TarBlockSize);

                // Two blocks of NUL in a row means we've reached the
                // end of the meaningful data in the file. If we encounter
                // this, we should be able to safely stop.
                if (Array.TrueForAll(buffer, AsciiNul.Equals))
                {
                    if (lastWasEmpty) break;
                    else
                    {
                        lastWasEmpty = true;
                        continue;
                    }
                }
                else lastWasEmpty = false;

                // We've reached the end of the stream. There
                // isn't anything more to be read.
                if (readStatus == -1) break;

                // The offset within the stream of the buffer's
                // first byte.
                long fbOffset = source.Position - TarBlockSize;

                // Parse the name from the headers. The name is,
                // at most, 100 ASCII characters in length. It will
                // be terminated by an ASCII_NUL.
                for (int i = 0; i < TarFilenameLength; i++)
                {
                    if (buffer[i] == AsciiNul) break;
                    
                    builder.Append((char)buffer[i]);
                }

                try
                {
                    string octStr = Encoding.ASCII.GetString(
                        buffer, TarFileSizeOffset, TarFileSizeLength
                        ).Trim();
                    dumpLength = Convert.ToInt64(octStr, RadixOctal);
                }
                catch (FormatException fex)
                {
                    throw new InvalidDataException(
                        String.Format(
                            "Header contains an invalid length field (header first byte offset {0}).",
                            fbOffset
                            ),
                        fex
                        );
                }

                // The number of bytes comprising the current
                // entry within the archive. If the length is zero,
                // it is kept as zero. Otherwise, we round to the
                // next highest integral multiple of the tar block
                // length.
                //
                // For example:
                //
                //  Content length      Entry length
                //   0              =>   0
                //   56             =>   512
                //   511            =>   512
                //   513            =>   1024
                //
                long entryBytes = dumpLength > 0
                    ? dumpLength + (TarBlockSize - (dumpLength % TarBlockSize))
                    : 0;

                if (entryBytes > ByteArrayMaxLength)
                {
                    throw new InternalBufferOverflowException(
                        "Files larger than 2,147,483,591 bytes are not supported."
                        );
                }

                // Check whether the entry is a normal file.
                byte type = buffer[TarFileTypeOffset];
                if (
                    type != FileTypeNormalA &&
                    type != FileTypeNormalB
                    )
                {
                    // The entry isn't a normal file, so skip
                    // past it.
                    source.Seek(entryBytes, SeekOrigin.Current);
                    continue;
                }

                // If we've made it this far, we're going to be adding
                // the file and its contents to our KeyedFileBuffer.
                //
                // The first thing we need to do is add the tar header
                // block to our list of headers.
                FileHeaders.Add((byte[])buffer.Clone());
                // Next, we will need to read all of the
                // tar blocks from the stream. It's quite possible that
                // the data we'll be storing won't be the full length of
                // the blocks, but it'll mean the stream is seeked to
                // the first byte of the next block.
                byte[] data = new byte[entryBytes];
                source.Read(data, 0, data.Length);
                // We then resize the array to the actual length of
                // the data. This will truncate off any padding bytes.
                Array.Resize(ref data, (int)dumpLength);
                // Then all we need to do is add this data, with
                // our file-name, to our buffer.
                FileMarks.Add(builder.ToString(), data);

                // If we've made it this far, we're going to be adding
                // the file to our table of contents.
                //
                // First on the list, add the header buffer to our list
                // of headers.
                //_headers.Add((byte[])buffer.Clone());

                //Array.Resize(ref buffer, dumpLength);
                //// We then add the file to our file-backed buffer.
                //_files.Add(builder.ToString(), buffer);

                //// Then we add the name.
                //_names.Add(builder.ToString());
                //// Then we create a mark for it, and add that.
                //_marks.Add(new Filemark(dumpLength, dumpOffset));
                //// Increment the offset within the file dump
                //// by the number of bytes (sans padding).
                //dumpOffset += dumpLength;
                //// Then we read the data from the archive to the
                //// data dump.
                //long lim = entryBytes / TAR_BLOCK_SIZE;
                //for (int i = 0; i < lim; i++)
                //{
                //    source.Read(buffer, 0, TAR_BLOCK_SIZE);

                //    if (i == lim - 1)
                //    {
                //        _dataDump.Write(
                //            buffer, 0,
                //            (int)(dumpLength % TAR_BLOCK_SIZE)
                //            );
                //    }
                //    else
                //    {
                //        _dataDump.Write(buffer, 0, TAR_BLOCK_SIZE);
                //    }
                //}

                //_dataDump.Flush();
            } 
            while (true);

        }

        /// <summary>
        /// Creates a new TarArchive.
        /// </summary>
        /// <param name="stream">The stream containing the archive's bytes.</param>
        /// <exception cref="System.IO.IOException">
        ///     Thrown when a temporary file could not be opened.
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
        /// <exception cref="System.IO.InternalBufferOverflowException">
        ///     Thrown when at least one file in the archive is larger than is
        ///     supported by the class (2,147,483,591 bytes).
        /// </exception>
        public TarArchive(Stream stream)
        {
            if (!stream.CanSeek) throw new ArgumentException(
                "The provided stream must be seekable.", "stream"
                );
            if (!stream.CanRead) throw new ArgumentException(
                "The provided stream must be readable.", "stream"
                );

            stream.Position = 0;
            LockObject = new object();
            FileHeaders = new List<byte[]>();
            FileMarks = new KeyedFileBuffer<string>();

            this.ParseTarFile(stream);
        }
        /// <summary>
        /// Calls the Dispose method of TarArchive.
        /// </summary>
        ~TarArchive()
        {
            this.Dispose();
        }

        /// <summary>
        /// The number of files in the archive. Excludes
        /// hard and soft links.
        /// </summary>
        public override int Count
        {
            get { return FileMarks.Keys.Count; }
        }
        /// <summary>
        /// The names of all files within the archive.
        /// Excludes hard and soft links.
        /// </summary>
        public override IEnumerable<string> Files
        {
            get { return FileMarks.Keys; }
        }

        /// <summary>
        /// Retrieves a file based on its name.
        /// </summary>
        /// <param name="name">The name of the file to retrieve.</param>
        /// <param name="contents">The contents of the retrieved file.</param>
        /// <returns>True if a file with the given name exists within the archive.</returns>
        public override bool GetFile(string name, out IEnumerable<byte> contents)
        {
            return FileMarks.TryGetValue(name, out contents);
        }
        /// <summary>
        /// Releases the resources used by this class.
        /// </summary>
        public override void Dispose()
        {
            FileMarks.Dispose();
        }
    }
}
