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
        private readonly IList<TKey> _keys;

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
                        throw new KeyNotFoundException(
                            "No set of bytes with the specified key could be found."
                            );
                    }

                    return base[index];
                }
            }
            set
            {
                this.Add(key, value);
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

                if (this.Contains(key))
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
        /// Adds data to the buffer. Always throws a
        /// NotSupportedException.
        /// </summary>
        /// <param name="bytes">The data to add to the buffer.</param>
        /// <exception cref="System.NotSupportedException">
        ///     Always thrown by this method.
        /// </exception>
        public override void Add(IEnumerable<byte> bytes)
        {
            throw new NotSupportedException(
                "This file buffer requires keys be added with data."
                );
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
        public bool Contains(TKey key)
        {
            lock (_lockbox)
            {
                _checkDisposed();

                return _keys.Contains(key, _comparer);
            }
        }
    }
}
