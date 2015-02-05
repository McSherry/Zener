using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace McSherry.Zener.Archives
{
    /// <summary>
    /// Represents a file-based buffer for storing multiple
    /// sets of bytes.
    /// </summary>
    public class FileBuffer
        : ICollection<IEnumerable<byte>>, IDisposable
    {
        // The default capacity of the List<Filemark>.
        private const int DEFAULT_CAPACITY = 8;
        // The size of the buffer used when reading/writing
        // from the FileStream representing the file being
        // used as a backing buffer.
        private const int BUFFER_BUFFER_SIZE = 4096;

        // We'll be storing all sets of bytes contiguously within a
        // single file. So we know where each file starts and ends,
        // we need to keep a record of the location of its first byte
        // and a record of its length. The Filemark struct does this,
        // and allows for large files by using longs. The use of two
        // longs also keeps Filemark within the recommended 16-byte
        // size limit for structs.
        private ICollection<Filemark> _marks;
        // We'll be storing the sets of bytes in a temporary file to
        // reduce memory usage. For all but the slowest of storage
        // media, this should be perfectly fine in all but the most
        // performance-sensitive of applications.
        private Stream _file;

        public FileBuffer(int capacity = DEFAULT_CAPACITY)
        {
            _marks = new List<Filemark>(capacity);
            _file = new FileStream(
                // The file won't have a meaningful name, because there
                // is no need for one. Thankfully, the BCL provides a
                // handy method for generating temporary filenames.
                path:       Path.GetTempFileName(),
                // We're going to need to open the file, but this also
                // handles having the file deleted between the call to
                // Path.GetTempFileName.
                mode:       FileMode.OpenOrCreate,
                // Fairly evident.
                access:     FileAccess.ReadWrite,
                // There's no reason to need to share access to the file.
                share:      FileShare.None,
                // Temporary files aren't, by default, deleted, so we want
                // to ensure that our temporary file is deleted once we're
                // done with it, especially considering this class will be
                // used with archives dealing with compressed files.
                //
                // We'll also be skipping around in the file, so any minor
                // optimisations the RandomAccess flag gives can't hurt.
                options:    FileOptions.DeleteOnClose | FileOptions.RandomAccess,
                // This is required for the constructor we're using. I believe
                // (but don't quote me on this) that the value in use is the
                // default for a FileStream.
                bufferSize: BUFFER_BUFFER_SIZE
                );
        }
    }
}
