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
        ///     Thrown when there is an attempt to set a key which already
        ///     exists within the buffer.
        /// </exception>
        public IEnumerable<byte> this[TKey key]
        {
            get
            {
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
            set
            {
                if (_keys.Contains(key, _comparer))
                {
                    throw new InvalidOperationException(
                        "The specified key already exists within the buffer."
                        );
                }

                // TODO: call this#Add
            }
        }
    }
}
