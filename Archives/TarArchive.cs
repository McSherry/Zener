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
    /// Represents a UNIX V6 Tape Archive file.
    /// </summary>
    public class TarArchive
        : Archive
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

        protected const long
            TAR_HEADER_LENGTH   = 512
            ;

        protected List<string> _names;
        protected List<byte[]> _headers;
        protected List<Filemark> _marks;
        protected FileStream _dataDump;

        /// <summary>
        /// Creates a new TarArchive.
        /// </summary>
        /// <param name="stream">The stream containing </param>
        /// <exception cref="System.IOException"></exception>
        public TarArchive(Stream stream)
        {
            // If we can seek in the stream, it's likely
            // that we should be at position 0, since it's
            // like we'll be reading from a file or memory
            // stream.
            //
            // If not, it's possible we're reading from the
            // network, so we won't be able to seek.
            if (stream.CanSeek) stream.Position = 0;
            if (!stream.CanRead) throw new ArgumentException(
                "The provided stream could not be read from.", "stream"
                );

            _names = new List<string>();
            _headers = new List<byte[]>();
            _marks = new List<Filemark>();

            try
            {
                _dataDump = File.Open(
                    Path.GetTempFileName(),
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None
                    );
            }
            catch (IOException ioex)
            {
                throw new IOException(
                    "Could not open temporary file.",
                    ioex
                    );
            }
        }
    }
}
