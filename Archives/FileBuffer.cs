using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

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
        protected const int DEFAULT_CAPACITY = 8;
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
        // Used to lock when writing to/reading from the
        // filestream.
        protected object _lockbox;
        // ICollection requires IsReadOnly to be implemented, so we
        // might as well add functionality for making it read-only.
        private bool _readonly,
            // Whether the file buffer has been disposed.
            _disposed;

        protected void _checkReadOnly()
        {
            if (_readonly)
            {
                throw new InvalidOperationException(
                    "A read-only collection cannot be modified."
                    );
            }
        }
        protected void _checkDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    "The buffer has been disposed."
                    );
            }
        }
        protected void _checkCanModify()
        {
            _checkDisposed();
            _checkReadOnly();
        }
        private IEnumerable<IEnumerable<byte>> _getEnumerator()
        {
            int initialCount = this.Count;

            for (int i = 0; i < initialCount; i++)
            {
                if (this.Count != initialCount)
                {
                    throw new InvalidOperationException(
                        "The buffer was modified."
                        );
                }

                yield return this[i];
            }
        }

        /// <summary>
        /// Creates a new FileBuffer.
        /// </summary>
        /// <param name="capacity">The starting capacity of the buffer.</param>
        /// <exception cref="System.IO.IOException">
        ///     Thrown when an error occurs whilst creating a temporary file.
        /// </exception>
        public FileBuffer(int capacity = DEFAULT_CAPACITY)
        {
            _marks = new List<Filemark>(capacity);

            try
            {
                _file = new FileStream(
                    // The file won't have a meaningful name, because there
                    // is no need for one. Thankfully, the BCL provides a
                    // handy method for generating temporary filenames.
                    path: Path.GetTempFileName(),
                    // We're going to need to open the file, but this also
                    // handles having the file deleted between the call to
                    // Path.GetTempFileName.
                    mode: FileMode.OpenOrCreate,
                    // Fairly evident.
                    access: FileAccess.ReadWrite,
                    // There's no reason to need to share access to the file.
                    share: FileShare.None,
                    // Temporary files aren't, by default, deleted, so we want
                    // to ensure that our temporary file is deleted once we're
                    // done with it, especially considering this class will be
                    // used with archives dealing with compressed files.
                    //
                    // We'll also be skipping around in the file, so any minor
                    // optimisations the RandomAccess flag gives can't hurt.
                    options: FileOptions.DeleteOnClose | FileOptions.RandomAccess,
                    // This is required for the constructor we're using. I believe
                    // (but don't quote me on this) that the value in use is the
                    // default for a FileStream.
                    bufferSize: BUFFER_BUFFER_SIZE
                    );
            }
            catch (IOException ioex)
            {
                throw new IOException(
                    "An I/O error occured when creating a temporary file.",
                    ioex
                    );
            }
            catch (UnauthorizedAccessException uaex)
            {
                throw new IOException(
                    "A permissions error occured when creating a temporary file.",
                    uaex
                    );
            }

            _lockbox = new object();
            _readonly = false;
            _disposed = false;
        }

        /// <summary>
        /// Retrieves a set of bytes from the buffer.
        /// </summary>
        /// <param name="index">The index of the set of bytes within the buffer.</param>
        /// <returns>The set of bytes at the specified index.</returns>
        /// <exception cref="System.IndexOutOfRangeException">
        ///     Thrown when the specified index is outside the bounds of
        ///     the buffer.
        /// </exception>
        public IEnumerable<byte> this[int index]
        {
            get
            {
                if (index >= _marks.Count)
                {
                    throw new IndexOutOfRangeException(
                        "The provided index was outside the bounds of the buffer."
                        );
                }

                byte[] data;
                lock (_lockbox)
                {
                    var mark = _marks.ElementAt(index);
                    data = new byte[mark.Length];

                    // Seek to the first byte of the set of bytes
                    // we want to return.
                    _file.Position = mark.Offset;
                    // Then we just need to read the data in to
                    // the byte array.
                    _file.Read(data, 0, data.Length);
                }

                return data;
            }
        }

        /// <summary>
        /// Adds the provided set of bytes to the buffer.
        /// </summary>
        /// <param name="bytes">The bytes to add.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the buffer is read-only.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">
        ///     Thrown when the buffer has been disposed.
        /// </exception>
        public virtual void Add(IEnumerable<byte> bytes)
        {
            lock (_lockbox)
            {
                _checkCanModify();

                // Calls to ToArray/ToList/etc are fairly slow,
                // so checking if the provided enumerable is a
                // byte array first could improve performance.
                byte[] data;
                if (bytes is byte[])
                {
                    data = (byte[])bytes;
                }
                else
                {
                    data = bytes.ToArray();
                }

                // We need to be at the end of the stream so
                // we don't overwrite any other bits of data.
                _file.Seek(0, SeekOrigin.End);
                // We need to add the Filemark so we can find
                // our data in future.
                _marks.Add(new Filemark(data.Length, _file.Position));
                // Then all that's left is to write our data to
                // the stream.
                _file.Write(data, 0, data.Length);
            }
        }
        /// <summary>
        /// Copies all sets of bytes stored within the buffer to an
        /// array of sets of bytes.
        /// </summary>
        /// <param name="array">The array to copy to.</param>
        /// <param name="arrayIndex">
        ///     The index within the array to start copying to.
        /// </param>
        /// <exception cref="System.IndexOutOfRangeException">
        ///     Thrown when the provided array is too short to
        ///     have all sets of bytes stored within the buffer
        ///     copied in to it.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">
        ///     Thrown when the buffer has been disposed.
        /// </exception>
        public void CopyTo(IEnumerable<byte>[] array, int arrayIndex)
        {
            lock (_lockbox)
            {
                _checkDisposed();

                // Check to make sure that the array we've been passed
                // is long enough to contain all sets of bytes stored
                // within the buffer.
                if (_marks.Count + arrayIndex > array.Length)
                {
                    throw new IndexOutOfRangeException(
                        "The provided array is too short to copy to."
                        );
                }

                for (int i = 0; i < _marks.Count; i++)
                {
                    array[arrayIndex + i] = this[i];
                }
            }
        }
        /// <summary>
        /// Determines whether the buffer contains a set of bytes equal to
        /// the provided sequence of bytes.
        /// </summary>
        /// <param name="bytes">The set of bytes to search for.</param>
        /// <returns>True if the buffer contains a set equal to the provided set.</returns>
        /// <exception cref="System.ObjectDisposedException">
        ///     Thrown when the buffer has been disposed.
        /// </exception>
        public bool Contains(IEnumerable<byte> bytes)
        {
            lock (_lockbox)
            {
                _checkDisposed();

                bool hasMatch = false;
                // Doing this in serial would likely be fairly slow, as
                // a file has to be read on each iteration. Making the
                // comparisons parallel should help mitigate any
                // performance hits.
                Parallel.ForEach(
                    source: this,
                    body: (item, state) =>
                    {
                        if (bytes.SequenceEqual(item))
                        {
                            hasMatch = true;
                            state.Stop();
                        }
                    });
            }

            return hasMatch;
        }
        /// <summary>
        /// Removes a set of bytes from the buffer. Always throws
        /// a NotSupportedException.
        /// </summary>
        /// <param name="bytes">The item to remove.</param>
        /// <exception cref="System.NotSupportedException">
        ///     Always thrown. The buffer does not support
        ///     removing individual items.
        /// </exception>
        public bool Remove(IEnumerable<byte> bytes)
        {
            throw new NotSupportedException(
                "The buffer does not support removing individual items."
                );
        }
        /// <summary>
        /// Removes all items from the file buffer.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the buffer is read-only.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">
        ///     Thrown when the buffer has been disposed.
        /// </exception>
        public virtual void Clear()
        {
            lock (_lockbox)
            {
                _checkCanModify();

                _marks.Clear();
                _file.SetLength(0);
            }
        }

        /// <summary>
        ///  Returns an enumerator that iterates through the sets
        ///  of bytes stored in the buffer.
        /// </summary>
        /// <returns>
        ///     An enumerator that iterates through the sets of bytes
        ///     stored within the buffer.
        /// </returns>
        public IEnumerator<IEnumerable<byte>> GetEnumerator()
        {
            lock (_lockbox)
            {
                _checkDisposed();

                return _getEnumerator().GetEnumerator();
            }
        }

        /// <summary>
        /// Releases the resources used by the buffer.
        /// </summary>
        public virtual void Dispose()
        {
            lock (_lockbox)
            {
                if (!_disposed)
                {
                    _disposed = true;

                    _file.Close();
                    _file.Dispose();
                    _marks.Clear();
                }
            }
        }

        /// <summary>
        /// The number of sets of bytes stored within the buffer.
        /// </summary>
        public int Count
        {
            get { return _marks.Count; }
        }
        /// <summary>
        /// Whether the buffer is read-only.
        /// </summary>
        public bool IsReadOnly
        {
            get { return _readonly; }
            set { _readonly = value; }
        }

        /// <summary>
        /// Whether the file buffer has been disposed.
        /// </summary>
        public virtual bool Disposed
        {
            get 
            {
                lock (_lockbox)
                {
                    return _disposed;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
