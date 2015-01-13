/*
 *      Copyright (c) 2014-2015, SynapLink, LLC
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
using System.Threading.Tasks;
using System.Net;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// A class used to store HTTP header values.
    /// </summary>
    public sealed class HttpHeaderCollection 
        : ICollection<BasicHttpHeader>
    {
        private List<BasicHttpHeader> _headerList;
        private bool _readOnly;

        /// <summary>
        /// Checks whether the collection is read-only, and throws an
        /// InvalidOperationException if it is.
        /// </summary>
        private void _readOnlyCheck()
        {
            if (this.IsReadOnly) 
                throw new InvalidOperationException
                ("Cannot modify a read-only collection.");
        }

        /// <summary>
        /// Used to change the read-only state of the collection.
        /// </summary>
        /// <param name="enable">True to make the collection read-only.</param>
        internal void WriteProtect(bool enable)
        {
            _readOnly = enable;
        }

        /// <summary>
        /// Creates an empty collection of HTTP headers.
        /// </summary>
        public HttpHeaderCollection()
        {
            _headerList = new List<BasicHttpHeader>();
            _readOnly = false;
        }
        /// <summary>
        /// Creates a collection of HTTP headers from the provided enumerable.
        /// </summary>
        /// <param name="enumerable">The enumerable to create from.</param>
        public HttpHeaderCollection(IEnumerable<BasicHttpHeader> enumerable)
            : this(enumerable.ToList()) { }
        /// <summary>
        /// Creates a collection of HTTP headers from the provided enumerable.
        /// </summary>
        /// <param name="enumerable">The enumerable to create from.</param>
        /// <param name="readOnly">Whether the collection should be read-only.</param>
        public HttpHeaderCollection(IEnumerable<BasicHttpHeader> enumerable, bool readOnly)
            : this(enumerable.ToList(), readOnly) { }
        /// <summary>
        /// Creates a collection of HTTP headers from the provided collection.
        /// </summary>
        /// <param name="collection">The collection to create the header collection from.</param>
        public HttpHeaderCollection(ICollection<BasicHttpHeader> collection)
            : this(collection, false) { }
        /// <summary>
        /// Creates a collection of HTTP headers from the provided enumerable.
        /// </summary>
        /// <param name="collection">The collection to create the header collection from.</param>
        /// <param name="readOnly">Whether the collection should be read-only.</param>
        public HttpHeaderCollection(ICollection<BasicHttpHeader> collection, bool readOnly)
        {
            _headerList = new List<BasicHttpHeader>(collection);
            _readOnly = readOnly;
        }

        /// <summary>
        /// Retrieves all headers with the specified field name.
        /// </summary>
        /// <param name="fieldName">The field name of the headers to retrieve.</param>
        /// <returns>The retrieved headers.</returns>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the collection is read-only.
        /// </exception>
        public List<BasicHttpHeader> this[string fieldName]
        {
            get
            {

                return _headerList.Where(
                    h => h.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
            }
            set
            {
                _readOnlyCheck();

                this.Remove(fieldName);

                value.ForEach(this.Add);
            }
        }
        /// <summary>
        /// Retrieves the header at the specified index in the collection.
        /// </summary>
        /// <param name="index">The index of the header.</param>
        /// <returns>The header at the specified index.</returns>
        internal BasicHttpHeader this[int index]
        {
            get { return _headerList[index]; }
            set { _headerList[index] = value; }
        }

        /// <summary>
        /// Whether the collection can be modified.
        /// </summary>
        public bool IsReadOnly
        {
            get { return _readOnly; }
        }
        /// <summary>
        /// The number of headers the collection currently holds.
        /// </summary>
        public int Count
        {
            get { return _headerList.Count; }
        }

        /// <summary>
        /// Converts the stored headers in to their string representation.
        /// </summary>
        /// <returns>The headers as they would be formatted in an HTTP request/response.</returns>
        public override string ToString()
        {
            return _headerList
                .Aggregate(
                    new StringBuilder(),
                    (sb, h) => sb.AppendFormat("{0}\r\n", h.ToString())
                    )
                .ToString();
        }

        /// <summary>
        /// Adds a header to the collection.
        /// </summary>
        /// <param name="fieldName">The field name of the header to be added.</param>
        /// <param name="fieldValue">The value of the header to be added.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void Add(string fieldName, string fieldValue)
        {
            this.Add(fieldName, fieldValue, false);
        }
        /// <summary>
        /// Adds a header to the collection, optionally overwriting previous headers.
        /// </summary>
        /// <param name="fieldName">The field name of the header to be added.</param>
        /// <param name="fieldValue">The value of the header to be added.</param>
        /// <param name="overwrite">Whether to overwrite any previous headers with the same field name.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void Add(string fieldName, string fieldValue, bool overwrite)
        {
            _readOnlyCheck();

            if (overwrite) this.Remove(fieldName);

            _headerList.Add(new BasicHttpHeader(fieldName, fieldValue));
        }
        /// <summary>
        /// Adds a header to the collection.
        /// </summary>
        /// <param name="header">The header to add to the collection.</param>
        public void Add(BasicHttpHeader header)
        {
            _readOnlyCheck();

            _headerList.Add(header);
        }

        /// <summary>
        /// Removes all headers with the specified field name.
        /// </summary>
        /// <param name="fieldName">The field name of the headers to remove.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void Remove(string fieldName)
        {
            _readOnlyCheck();

            _headerList.RemoveAll(
                h => h.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase)
                );
        }
        /// <summary>
        /// Determines whether the collection contains a header equivalent to the provided.
        /// </summary>
        /// <param name="header">The header to search for.</param>
        /// <returns>True if an equivalent header exists.</returns>
        public bool Contains(BasicHttpHeader header)
        {
            return _headerList.Contains(header);
        }
        /// <summary>
        /// Determines whether the collection contains a header with the provided field name.
        /// </summary>
        /// <param name="fieldName">The field name to search for.</param>
        /// <returns>True if a header with the provided field name exists.</returns>
        public bool Contains(string fieldName)
        {
            return _headerList.Any(
                h => h.Field.Equals(fieldName, StringComparison.OrdinalIgnoreCase)
                );
        }
        /// <summary>
        /// Removes all headers from the collection.
        /// </summary>
        public void Clear()
        {
            _readOnlyCheck();

            _headerList.Clear();
        }

        void ICollection<BasicHttpHeader>.CopyTo(BasicHttpHeader[] array, int arrayIndex)
        {
            _headerList.CopyTo(array, arrayIndex);
        }
        bool ICollection<BasicHttpHeader>.Remove(BasicHttpHeader header)
        {
            _readOnlyCheck();

            return _headerList.Remove(header);
        }
        IEnumerator<BasicHttpHeader> IEnumerable<BasicHttpHeader>.GetEnumerator()
        {
            return _headerList.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_headerList).GetEnumerator();
        }
    }
}
