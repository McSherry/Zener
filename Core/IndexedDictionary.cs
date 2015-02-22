/*
 *      Copyright (c) 2014-2015, Liam McSherry
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace McSherry.Zener.Core
{
    /// <summary>
    /// Provides an ordered dictionary that can be accessed using
    /// an index or a key.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type to use for the keys.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The type to use for the values.
    /// </typeparam>
    /// <remarks>
    /// .NET 4.0 doesn't provide a generic OrderedDictionary, so we
    /// need to provide our own implementation. To prevent any conflicts
    /// with future generic OrderedDictionary classes, we've named this
    /// one IndexedDictionary.
    /// </remarks>
    public sealed class IndexedDictionary<TKey, TValue>
        : IDictionary<TKey, TValue>, IList<KeyValuePair<TKey, TValue>>
    {
        // We'll be using this to provide quick ContainsKey
        // calls.
        private HashSet<TKey> _keyHashes;
        // And we'll be using the List<T>s for everything
        // else.
        private List<TKey> _keys;
        private List<TValue> _values;
        // We'll be properly supporting making the collection
        // read-only.
        private bool _readonly;
        // Thread-safety is also a concern.
        private object _lockbox;

        /// <summary>
        /// Checks whether the dictionary is read-only, and throws
        /// an exception if it is.
        /// </summary>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only.
        /// </exception>
        private void CheckIsReadOnly()
        {
            if (this.IsReadOnly)
            {
                throw new InvalidOperationException(
                    "The dictionary is read-only."
                    );
            }
        }

        /// <summary>
        /// Creates a new empty IndexedDictionary.
        /// </summary>
        public IndexedDictionary()
            : this(EqualityComparer<TKey>.Default)
        {

        }
        /// <summary>
        /// Creates a new empty IndexedDictionary.
        /// </summary>
        /// <param name="comparer">
        /// The comparer used to determine key equality.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided equality comparer is null.
        /// </exception>
        public IndexedDictionary(IEqualityComparer<TKey> comparer)
        {
            if (comparer == null)
            {
                throw new ArgumentNullException(
                    "The provided key comparer must not be null."
                    );
            }

            _keyHashes = new HashSet<TKey>(comparer);
            _keys = new List<TKey>();
            _values = new List<TValue>();
            _lockbox = new object();
            _readonly = false;

            this.KeyComparer = comparer;
        }

        /// <summary>
        /// Retrieves the value in the dictionary associated with
        /// the specified key.
        /// </summary>
        /// <param name="key">
        /// The key to retrieve the associated value of.
        /// </param>
        /// <returns>
        /// The value associated with the specified key.
        /// </returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Thrown when the specified key is not present within
        /// the dictionary.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only and an attempt
        /// is made to set a value.
        /// </exception>
        public TValue this[TKey key]
        {
            get 
            {
                lock (_lockbox)
                {
                    if (!this.ContainsKey(key))
                    {
                        throw new KeyNotFoundException(
                            "The specified key does not exist within the dictionary."
                            );
                    }

                    return _values[_keys.IndexOf(key)];
                }
            }
            set 
            {
                lock (_lockbox)
                {
                    this.CheckIsReadOnly();

                    if (!this.ContainsKey(key))
                    {
                        this.Add(key, value);
                    }
                    else
                    {
                        _values[this.IndexOf(key)] = value;
                    }
                }
            }
        }
        /// <summary>
        /// Retrieves the key and value in the dictionary at
        /// the specified index.
        /// </summary>
        /// <param name="index">
        /// The index of the value to retrieve.
        /// </param>
        /// <returns>
        /// A key-value pair containing the key and value at
        /// the specified index within the dictionary.
        /// </returns>
        /// <exception cref="System.IndexOutOfRangeException">
        /// Thrown when the specified index is outside the valid range
        /// for the number of items currently in the dictionary.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only and an attempt is
        /// made to set a value.
        /// </exception>
        public KeyValuePair<TKey, TValue> this[int index]
        {
            get
            {
                lock (_lockbox)
                {
                    if (index < 0 || index >= _keys.Count)
                    {
                        throw new IndexOutOfRangeException(
                            "The specified index is outside the valid range for this " +
                            "dictionary."
                            );
                    }

                    return new KeyValuePair<TKey, TValue>(
                        _keys[index], _values[index]
                        );
                }
            }
            set
            {
                lock (_lockbox)
                {
                    this.CheckIsReadOnly();

                    if (index < 0 || index >= _keys.Count)
                    {
                        throw new IndexOutOfRangeException(
                            "The specified index is outside the valid range for this " +
                            "dictionary."
                            );
                    }

                    this[value.Key] = value.Value;
                }
            }
        }

        /// <summary>
        /// Whether the dictionary is read-only.
        /// </summary>
        public bool IsReadOnly
        {
            get { return _readonly; }
            private set { _readonly = value; }
        }
        /// <summary>
        /// The number of items currently in the dictionary.
        /// </summary>
        public int Count
        {
            get { return _keys.Count; }
        }
        /// <summary>
        /// The keys this dictionary contains.
        /// </summary>
        public ICollection<TKey> Keys
        {
            get { return _keys.AsReadOnly(); }
        }
        /// <summary>
        /// The values this dictionary contains.
        /// </summary>
        public ICollection<TValue> Values
        {
            get { return _values.AsReadOnly(); }
        }
        /// <summary>
        /// The comparer used to compare keys.
        /// </summary>
        public IEqualityComparer<TKey> KeyComparer
        {
            get;
            private set;
        }

        /// <summary>
        /// Adds a new key and value to the dictionary.
        /// </summary>
        /// <param name="key">The key of the item to add.</param>
        /// <param name="value">The value of the item to add.</param>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the specified key is already present within
        /// the dictionary.
        /// </exception>
        public void Add(TKey key, TValue value)
        {
            lock (_lockbox)
            {
                this.CheckIsReadOnly();

                if (this.ContainsKey(key))
                {
                    throw new ArgumentException(
                        "The specified key already exists within the dictionary."
                        );
                }

                _keys.Add(key);
                _keyHashes.Add(key);
                _values.Add(value);
            }
        }
        /// <summary>
        /// Adds a new key and value to the dictionary.
        /// </summary>
        /// <param name="pair">
        /// The key-value pair containing the key and value to add
        /// to the dictionary.
        /// </param>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only.
        /// </exception>
        public void Add(KeyValuePair<TKey, TValue> pair)
        {
            this.Add(pair.Key, pair.Value);
        }
        /// <summary>
        /// Inserts a key-value pair at the specified index within
        /// the dictionary.
        /// </summary>
        /// <param name="index">The index to insert at.</param>
        /// <param name="key">The key to insert.</param>
        /// <param name="value">The value to associate with the key.</param>
        /// <exception cref="System.IndexOutOfRangeException">
        /// Thrown when the specified index is outside the valid range
        /// for the number of items currently in the dictionary.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the specified key already exists within the dictionary.
        /// </exception>
        public void Insert(int index, TKey key, TValue value)
        {
            lock (_lockbox)
            {
                this.CheckIsReadOnly();

                if (this.ContainsKey(key))
                {
                    throw new ArgumentException(
                        "The specified key already exists within the dictionary."
                        );
                }

                if (index < 0 || index >= _keys.Count)
                {
                    throw new IndexOutOfRangeException(
                        "The specified index is outside the valid range for this " +
                        "dictionary."
                        );
                }

                _keyHashes.Add(key);
                _keys.Insert(index, key);
                _values.Insert(index, value);
            }
        }
        /// <summary>
        /// Inserts a key-value pair at the specified index within
        /// the dictionary.
        /// </summary>
        /// <param name="index">The index to insert at.</param>
        /// <param name="pair">The key-value pair to insert.</param>
        /// <exception cref="System.IndexOutOfRangeException">
        /// Thrown when the specified index is outside the valid range
        /// for the number of items currently in the dictionary.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only.
        /// </exception>
        public void Insert(int index, KeyValuePair<TKey, TValue> pair)
        {
            this.Insert(index, pair.Key, pair.Value);
        }

        /// <summary>
        /// Copies the contents of the dictionary to the specified
        /// array of key-value pairs.
        /// </summary>
        /// <param name="pairs">
        /// The key-value pair array to copy the contents of the
        /// dictionary to.
        /// </param>
        /// <param name="arrayIndex">
        /// The index within the array at which to start inserting.
        /// </param>
        /// <exception cref="System.IndexOutOfRangeException">
        /// Thrown when the specified index is outside the valid range
        /// of indices for the specified array to copy in to.
        /// </exception>
        public void CopyTo(KeyValuePair<TKey, TValue>[] pairs, int arrayIndex)
        {
            if (arrayIndex < 0 || arrayIndex >= _keys.Count)
            {
                throw new IndexOutOfRangeException(
                    "The specified index is outside the valid range for this " +
                    "dictionary."
                    );
            }

            _keys
                .Zip(_values, (k, v) => new KeyValuePair<TKey, TValue>(k, v))
                .ToArray()
                .CopyTo(pairs, arrayIndex);
        }

        /// <summary>
        /// Determines whether the dictionary contains an item
        /// with the specified key.
        /// </summary>
        /// <param name="key">The key to check for.</param>
        /// <returns>
        /// True if the specified key exists within the dictionary.
        /// </returns>
        public bool ContainsKey(TKey key)
        {
            lock (_lockbox)
            {
                return _keyHashes.Contains(key);
            }
        }
        /// <summary>
        /// Determines whether the dictionary contains an item with
        /// the specified key and value.
        /// </summary>
        /// <param name="pair">
        /// The key and value to check for.
        /// </param>
        /// <returns>
        /// True if the dictionary contains a key with the
        /// specified value.
        /// </returns>
        public bool Contains(KeyValuePair<TKey, TValue> pair)
        {
            lock (_lockbox)
            {
                return this.ContainsKey(pair.Key) && this[pair.Key].Equals(pair.Value);
            }
        }        
        /// <summary>
        /// Determines the index of the specified key.
        /// </summary>
        /// <param name="key">The key to dteermine the index of.</param>
        /// <returns>The index of the specified key.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Thrown when the key does not exist within the dictionary.
        /// </exception>
        public int IndexOf(TKey key)
        {
            lock (_lockbox)
            {
                if (!this.ContainsKey(key))
                {
                    throw new ArgumentOutOfRangeException(
                        "No element with the specified key exists in the dictionary."
                        );
                }

                var index = Enumerable
                    .Range(0, _keys.Count)
                    .Zip(_keys, (i, k) => new { i, k })
                    .Where(o => this.KeyComparer.Equals(key, o.k))
                    .Select(o => o.i)
                    .DefaultIfEmpty(-1)
                    .First();

                if (index == -1)
                {
                    throw new KeyNotFoundException(
                        "The specified key does not exist within the dictionary."
                        );
                }

                return index;
            }
        }
        /// <summary>
        /// Determines the index of the specified key-value pair.
        /// </summary>
        /// <param name="pair">The pair to determine the index of.</param>
        /// <returns>The index of the specified key-value pair.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the key-value pair does not exist within the
        /// dictionary.
        /// </exception>
        public int IndexOf(KeyValuePair<TKey, TValue> pair)
        {
            lock (_lockbox)
            {
                int kIndex = this.IndexOf(pair.Key);

                if (!this[kIndex].Equals(pair.Value))
                {
                    throw new ArgumentOutOfRangeException(
                        "The specified key-value pair does not exist within the " +
                        "dictionary."
                        );
                }

                return kIndex;
            }
        }

        /// <summary>
        /// Attempts to retrieve the value associated with the
        /// specified key.
        /// </summary>
        /// <param name="key">
        /// The key to attempt to retrieve the associated value of.
        /// </param>
        /// <param name="value">
        /// The variable to place the retrieved value in.
        /// </param>
        /// <returns>
        /// True if the value was successfully retrieved.
        /// </returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            bool contains;
            lock (_lockbox)
            {
                if ((contains = this.ContainsKey(key)))
                {
                    value = this[key];
                }
                else
                {
                    value = default(TValue);
                }
            }

            return contains;
        }

        /// <summary>
        /// Removes all items from the dictionary.
        /// </summary>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only.
        /// </exception>
        public void Clear()
        {
            lock (_lockbox)
            {
                this.CheckIsReadOnly();

                _keyHashes.Clear();
                _keys.Clear();
                _values.Clear();
            }
        }
        /// <summary>
        /// Removes the item with the specified key from the
        /// dictionary.
        /// </summary>
        /// <param name="key">
        /// The key of the item to remove.
        /// </param>
        /// <returns>
        /// True if the item was removed from the dictionary.
        /// </returns>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only.
        /// </exception>
        public bool Remove(TKey key)
        {
            bool removed;
            lock (_lockbox)
            {
                this.CheckIsReadOnly();

                if ((removed = this.ContainsKey(key)))
                {
                    int kIndex = this.IndexOf(key);

                    _keyHashes.Remove(key);
                    _keys.Remove(key);
                    _values.RemoveAt(kIndex);
                }
            }

            return removed;
        }
        /// <summary>
        /// Removes the specified item from the dictionary.
        /// </summary>
        /// <param name="pair">
        /// The item to remove.
        /// </param>
        /// <returns>
        /// True if the specified item was removed.
        /// </returns>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only.
        /// </exception>
        public bool Remove(KeyValuePair<TKey, TValue> pair)
        {
            return this.Remove(pair.Key);
        }
        /// <summary>
        /// Removes an item from the dictionary based on its index.
        /// </summary>
        /// <param name="index">The index of the item to remove.</param>
        /// <exception cref="System.IndexOutOfRangeException">
        /// Thrown when the specified index is outside the valid range
        /// for the number of items currently in the dictionary.
        /// </exception>
        /// <exception cref="System.NotSupportedException">
        /// Thrown when the dictionary is read-only.
        /// </exception>
        public void RemoveAt(int index)
        {
            lock (_lockbox)
            {
                this.CheckIsReadOnly();

                if (index < 0 || index >= _keys.Count)
                {
                    throw new IndexOutOfRangeException(
                        "The specified index is outside the valid range for this " +
                        "dictionary."
                        );
                }

                _keyHashes.Remove(_keys[index]);
                _keys.RemoveAt(index);
                _values.RemoveAt(index);
            }
        }

        /// <summary>
        /// Creates a read-only copy of this IndexedDictionary.
        /// </summary>
        /// <returns>
        /// The read-only copy of this IndexedDictionary.
        /// </returns>
        public IndexedDictionary<TKey, TValue> AsReadOnly()
        {
            var ixDict = new IndexedDictionary<TKey, TValue>(this.KeyComparer);

            lock (_lockbox) foreach (var kvp in this) ixDict.Add(kvp);

            ixDict.IsReadOnly = true;

            return ixDict;
        }

        /// <summary>
        /// Gets an enumerator that iterates through this IndexedDictionary's
        /// items.
        /// </summary>
        /// <returns>
        /// An enumerator for the dictionary.
        /// </returns>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when the dictionary is modified during enumeration.
        /// </exception>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            lock (_lockbox)
            {
                int originalCount = this.Count;

                for (int i = 0; i < originalCount; i++)
                {
                    if (this.Count != originalCount)
                    {
                        throw new InvalidOperationException(
                            "The dictionary was modified."
                            );
                    }

                    yield return new KeyValuePair<TKey, TValue>(
                        _keys[i], _values[i]
                        );
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)this).GetEnumerator();
        }
    }
}
