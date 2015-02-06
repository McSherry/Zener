using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace McSherry.Zener.Archives
{
    /// <summary>
    /// Represents a file-based buffer for storing sets of
    /// bytes, with each set identified by a unique key.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public sealed class KeyedFileBuffer<TKey>
        : FileBuffer, IDictionary<TKey, IEnumerable<byte>>, IDisposable
    {
        private const int DEFAULT_INDEX = -1;

        private readonly IEqualityComparer<TKey> _comparer;
        private readonly List<TKey> _keys;

        private IEnumerable<KeyValuePair<TKey, IEnumerable<byte>>> _getEnumerator()
        {
            lock (_lockbox)
            {
                _checkDisposed();

                foreach (var key in this.Keys)
                    yield return new KeyValuePair<TKey, IEnumerable<byte>>(
                        key, this[key]
                        );
            }
        }

        /// <summary>
        /// Creates a new KeyedFileBuffer with default equality
        /// comparison rules and the default starting capacity.
        /// </summary>
        public KeyedFileBuffer()
            : this(EqualityComparer<TKey>.Default, DEFAULT_CAPACITY)
        {

        }
        /// <summary>
        /// Creates a new KeyedFileBuffer.
        /// </summary>
        /// <param name="comparer">The comparer to use when comparing keys.</param>
        /// <param name="capacity">The starting capacity of the buffer.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the provided comparer is null.
        /// </exception>
        /// <exception cref="System.IO.IOException">
        ///     Thrown when an error occurs whilst creating a temporary file.
        /// </exception>
        public KeyedFileBuffer(
            IEqualityComparer<TKey> comparer,
            int capacity = DEFAULT_CAPACITY
            )
            : base(capacity)
        {
            if (comparer == null)
            {
                throw new ArgumentNullException(
                    "The string comparer must not be null."
                    );
            }

            _comparer = comparer;
            _keys = new List<TKey>(capacity);
        }

        /// <summary>
        /// Retrieves a set of bytes based on a key.
        /// </summary>
        /// <param name="name">The key associated with the set of bytes to retrieve.</param>
        /// <returns>The set of bytes associated with the specified key.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        ///     Thrown when there is no set of bytes associated with the
        ///     specified key.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the specified key is already present
        ///     within the buffer's set of keys.
        ///     
        ///     Thrown when the buffer is read-only.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">
        ///     Thrown when the buffer has been disposed.
        /// </exception>
        public IEnumerable<byte> this[TKey key]
        {
            get
            {
                IEnumerable<byte> bytes;
                if (this.TryGetValue(key, out bytes))
                {
                    return bytes;
                }

                throw new KeyNotFoundException(
                    "No set of bytes with the specified key could be found."
                    );
            }
            set
            {
                this.Add(key, value);
            }
        }

        /// <summary>
        /// Changes the key associated with a set of bytes.
        /// </summary>
        /// <param name="current">The key to change.</param>
        /// <param name="new">The value to change the key to.</param>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        ///     Thrown when the key to be changed is not found
        ///     within the buffer.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the buffer is read-only.
        ///     
        ///     Thrown when the new key is already present
        ///     within the collection.
        /// </exception>
        public void ChangeKey(TKey current, TKey @new)
        {
            lock (_lockbox)
            {
                _checkCanModify();

                if (!this.ContainsKey(current))
                {
                    throw new KeyNotFoundException(
                        "The key to replace does not exist."
                        );
                }

                if (this.ContainsKey(@new))
                {
                    throw new InvalidOperationException(
                        "The new key already exists within the buffer."
                        );
                }

                _keys[_keys.IndexOf(current)] = @new;
            }
        }

        /// <summary>
        /// Adds a set of bytes to the buffer and associates it
        /// with a key.
        /// </summary>
        /// <param name="pair">
        ///     The data to add to the buffer and the key
        ///     to associate with it.
        /// </param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the specified key is already present
        ///     within the buffer's set of keys.
        ///     
        ///     Thrown when the buffer is read-only.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">
        ///     Thrown when the buffer has been disposed.
        /// </exception>
        public void Add(KeyValuePair<TKey, IEnumerable<byte>> pair)
        {
            this.Add(pair.Key, pair.Value);
        }
        /// <summary>
        /// Adds a set of bytes to the buffer and associates it
        /// with a key.
        /// </summary>
        /// <param name="key">The key to associate the bytes with.</param>
        /// <param name="data">The bytes to associate with the key.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the specified key is already present
        ///     within the buffer's set of keys.
        ///     
        ///     Thrown when the buffer is read-only.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">
        ///     Thrown when the buffer has been disposed.
        /// </exception>
        public void Add(TKey key, IEnumerable<byte> data)
        {
            lock (_lockbox)
            {
                _checkCanModify();

                if (this.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "The specified key already exists within the buffer."
                        );
                }

                _keys.Add(key);
                base.Add(data);
            }
        }
        /// <summary>
        /// Adds a set of bytes to the buffer. Always throws
        /// a NotSupportedException.
        /// </summary>
        /// <param name="data">The data to add.</param>
        /// <exception cref="System.NotSupportedException">
        ///     Always thrown.
        /// </exception>
        public override void Add(IEnumerable<byte> data)
        {
            throw new NotSupportedException(
                "The buffer does not support adding data without a key."
                );
        }
        /// <summary>
        /// Attempts to retrieve the data associated with a key.
        /// </summary>
        /// <param name="key">The key to attempt to retrieve the data for.</param>
        /// <param name="item">The retrieved data.</param>
        /// <returns>True if the data was retrieved successfully.</returns>
        public bool TryGetValue(TKey key, out IEnumerable<byte> item)
        {
            lock (_lockbox)
            {
                _checkDisposed();

                var index = Enumerable
                    .Range(0, _keys.Count)
                    .Zip(_keys, (i, n) => new { i, n })
                    .Where(o => _comparer.Equals(key, o.n))
                    .Select(o => o.i)
                    .DefaultIfEmpty(DEFAULT_INDEX)
                    .First();

                if (index == DEFAULT_INDEX)
                {
                    item = null;
                    return false;
                }
                else
                {
                    item = base[index];
                    return true;
                }
            }
        }
        /// <summary>
        /// Copies all sets of bytes and the associated keys stored
        /// within the buffer to an array of keys and sets of bytes.
        /// </summary>
        /// <param name="array">The array to copy to.</param>
        /// <param name="arrayIndex">The index within the array to start copying to.</param>
        /// <exception cref="System.IndexOutOfRangeException">
        ///     Thrown when the provided array is too short to copy all
        ///     key-data pairs to.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">
        ///     Thrown when the buffer has been disposed.
        /// </exception>
        public void CopyTo(
            KeyValuePair<TKey, IEnumerable<byte>>[] array,
            int arrayIndex
            )
        {
            lock (_lockbox)
            {
                _checkDisposed();

                if (this.Count + arrayIndex > array.Length)
                {
                    throw new IndexOutOfRangeException(
                        "The provided array is too short to copy to."
                        );
                }

                IEnumerable<byte>[] dataArray = new IEnumerable<byte>[this.Count];
                base.CopyTo(dataArray, 0);

                dataArray
                    .Zip(_keys, (e, k) => new KeyValuePair<TKey, IEnumerable<byte>>(k, e))
                    .ToArray()
                    .CopyTo(array, arrayIndex);
            }
        }
        /// <summary>
        /// Determines whether the provided key-data pair exists within
        /// the buffer.
        /// </summary>
        /// <param name="pair">The pair to check for.</param>
        /// <returns>True if the pair is present within the buffer.</returns>
        public bool Contains(KeyValuePair<TKey, IEnumerable<byte>> pair)
        {
            lock (_lockbox)
            {
                return this.ContainsKey(pair.Key) && base.Contains(pair.Value);
            }
        }
        /// <summary>
        /// Determines whether there exists within the buffer a
        /// set of bytes with the specified key.
        /// </summary>
        /// <param name="key">The key to check for.</param>
        /// <returns>
        ///     True if there is a set of bytes with the
        ///     specified key associated with it.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">
        ///     Thrown when the buffer has been disposed.
        /// </exception>
        public bool ContainsKey(TKey key)
        {
            lock (_lockbox)
            {
                _checkDisposed();

                return _keys.Contains(key, _comparer);
            }
        }
        /// <summary>
        /// Removes a key-data pair from the buffer. Always throws
        /// a NotSupportedException.
        /// </summary>
        /// <param name="pair">The key of the key-data pair to remove.</param>
        /// <exception cref="System.NotSupportedException">
        ///     Always thrown. The buffer does not support
        ///     removing individual items.
        /// </exception>
        public bool Remove(TKey key)
        {
            throw new NotSupportedException(
                "The buffer does not support removing individual items."
                );
        }
        /// <summary>
        /// Removes a key-data pair from the buffer. Always throws
        /// a NotSupportedException.
        /// </summary>
        /// <param name="pair">The key-data pair to remove.</param>
        /// <exception cref="System.NotSupportedException">
        ///     Always thrown. The buffer does not support
        ///     removing individual items.
        /// </exception>
        public bool Remove(KeyValuePair<TKey, IEnumerable<byte>> pair)
        {
            throw new NotSupportedException(
                "The buffer does not support removing individual items."
                );
        }
        /// <summary>
        /// Returns an enumerator for iterating through the key-data
        /// pairs stored within the buffer.
        /// </summary>
        /// <returns>
        ///     An enumerator for iterating through the key-data
        ///     pairs stored within the buffer.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">
        ///     Thrown when the buffer has been disposed.
        /// </exception>
        public new IEnumerator<KeyValuePair<TKey, IEnumerable<byte>>> GetEnumerator()
        {
            return _getEnumerator().GetEnumerator();
        }

        /// <summary>
        /// A collection containing all keys stored within the buffer.
        /// </summary>
        public IList<TKey> Keys
        {
            get 
            {
                _checkDisposed();

                return _keys.AsReadOnly(); 
            }
        }

        ICollection<TKey> IDictionary<TKey, IEnumerable<byte>>.Keys
        {
            get { return this.Keys; }
        }
        ICollection<IEnumerable<byte>> IDictionary<TKey, IEnumerable<byte>>.Values
        {
            get
            {
                _checkDisposed();

                var values = new List<IEnumerable<byte>>();
                foreach (var val in this)
                    values.Add(val.Value);

                return values.AsReadOnly();
            }
        }
    }
}
