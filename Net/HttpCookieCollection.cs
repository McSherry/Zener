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
using System.Threading.Tasks;

namespace McSherry.Zener.Net
{
    /// <summary>
    /// Represents a collection of HttpCookies.
    /// </summary>
    public sealed class HttpCookieCollection
        : ICollection<HttpCookie>
    {
        private List<HttpCookie> _cookies;
        private bool _readOnly;

        private void _readOnlyCheck()
        {
            if (this.IsReadOnly)
                throw new InvalidOperationException(
                    "Cannot modify a read-only collection."
                    );
        }

        /// <summary>
        /// Creates a new empty HttpCookieCollection.
        /// </summary>
        public HttpCookieCollection()
        {
            _cookies = new List<HttpCookie>();
            _readOnly = false;
        }

        /// <summary>
        /// Adds a new cookie to the collection.
        /// </summary>
        /// <param name="name">The name of the cookie.</param>
        /// <param name="value">The cookie's value.</param>
        /// <param name="expiry">The date and time the cookie expires.</param>
        /// <param name="domain">The domain the cookie is restricted to.</param>
        /// <param name="path">The path the cookie is restricted to.</param>
        /// <param name="secure">Whether the cookie should only be served over secure connections..</param>
        /// <param name="httpOnly">Whether the cookie should only be served over an HTTP protocol.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when the cookie name is null.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the collection is read-only.
        /// </exception>
        public void Add(
            string name,
            string value,
            Nullable<DateTime> expiry = null,
            string domain = null,
            string path = null,
            bool secure = false,
            bool httpOnly = false
            )
        {
            this.Add(new HttpCookie(name, value)
            {
                Expiry = expiry,
                Domain = domain,
                Path = path,
                Secure = secure,
                HttpOnly = httpOnly
            });
        }
        /// <summary>
        /// Adds a cookie to the collection.
        /// </summary>
        /// <param name="item">The cookie to add.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Thrown when <paramref name="item"/> is null.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the collection is read-only.
        /// </exception>
        public void Add(HttpCookie item)
        {
            _readOnlyCheck();

            if (item == null) throw new ArgumentNullException(
                "The provided cookie was null."
                );

            this.Remove(item);

            _cookies.Add(item);
        }
        /// <summary>
        /// Removes a cookie from the collection.
        /// </summary>
        /// <param name="item">The cookie to remove.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the collection is read-only.
        /// </exception>
        public bool Remove(HttpCookie item)
        {
            _readOnlyCheck();

            if (item == null) throw new ArgumentNullException(
                "The provided cookie was null."
                );

            return this.Remove(item.Name);
        }
        /// <summary>
        /// Removes a cookie based on its name.
        /// </summary>
        /// <param name="name">The name of the cookie to remove.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the collection is read-only.
        /// </exception>
        public bool Remove(string name)
        {
            _readOnlyCheck();

            return 0 < _cookies.RemoveAll(c => c.Equals(name));
        }
        /// <summary>
        /// Determines whether the collection contains the specified cookie.
        /// </summary>
        /// <param name="item">The item to attempt to find.</param>
        /// <returns>True if the collection contains the cookie.</returns>
        public bool Contains(HttpCookie item)
        {
            return _cookies.Any(c => c.Equals(item));
        }
        /// <summary>
        /// Determines whether the collection contains a cookie with the
        /// specified name.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <returns>True if the collection contains a cookie with the specified name.</returns>
        public bool Contains(string name)
        {
            return _cookies.Any(c => c.Equals(name));
        }
        /// <summary>
        /// Removes all cookies from the collection.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        ///     Thrown when the collection is read-only.
        /// </exception>
        public void Clear()
        {
            _readOnlyCheck();

            _cookies.Clear();
        }

        /// <summary>
        /// The number of cookies within the collection.
        /// </summary>
        public int Count
        {
            get { return _cookies.Count; }
        }
        /// <summary>
        /// Whether the collection is read-only.
        /// </summary>
        public bool IsReadOnly
        {
            get { return _readOnly; }
            internal set { _readOnly = value; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _cookies.GetEnumerator();
        }
        IEnumerator<HttpCookie> IEnumerable<HttpCookie>.GetEnumerator()
        {
            return _cookies.GetEnumerator();
        }
        void ICollection<HttpCookie>.CopyTo(HttpCookie[] items, int arrayIndex)
        {
            _cookies.CopyTo(items, arrayIndex);
        }
    }
}
