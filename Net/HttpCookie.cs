/*
 *      Copyright (c) 2014, SynapLink, LLC
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE.txt, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// Specifies how cookies should be compared
    /// when checking for equality.
    /// </summary>
    public enum CookieComparison
    {
        /// <summary>
        /// Name comparison. A case-sensitive comparison
        /// of the cookie names.
        /// </summary>
        Name,
        /// <summary>
        /// Attribute comparison. A comparison of all of
        /// the cookie attributes.
        /// </summary>
        Attributes
    }

    /// <summary>
    /// A class representing an HTTP cookie.
    /// </summary>
    public class HttpCookie
    {
        private const int NAME_LENGTH_MIN = 1;
        private const string NAME_PERMITTED =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!#$%&'*+-.^_~`";
        private const string DOMAIN_PERMITTED = 
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-";
        private const string PATH_PERMITTED =
            " !\"#$%&'()*+,-./0123456789:<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        private bool _httpOnly, _secure;
        private Nullable<DateTime> _expiry;
        private string _domain, _path, _name, _value;

        /// <summary>
        /// Verifies that a string contains only permitted characters.
        /// </summary>
        /// <param name="value">The string to verify.</param>
        /// <param name="permitted">The permitted characters.</param>
        /// <returns>True if the value passes verification.</returns>
        private static bool VerifyString(string value, IEnumerable<char> permitted)
        {
            bool noInvalids = true;
            int nameIndex = 0;
            while (noInvalids & (nameIndex < value.Length))
            {
                noInvalids &= permitted.Contains(value[nameIndex++]);
            }

            return noInvalids;
        }

        /// <summary>
        /// Creates a new HttpCookie with the specified
        /// name and value.
        /// </summary>
        /// <param name="name">The cookie's name.</param>
        /// <param name="value">The value of the cookie.</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <exception cref="System.ArgumentNullException"></exception>
        public HttpCookie(string name, string value)
        {
            if (name == null)
                throw new ArgumentNullException(
                    "The cookie's name cannot be null."
                    );

            if (name.Length < NAME_LENGTH_MIN)
                throw new ArgumentException(
                    String.Format(
                        "The provided name was too short (minimum length {0}).",
                        NAME_LENGTH_MIN
                    ));

            if (!HttpCookie.VerifyString(name, NAME_PERMITTED))
                throw new ArgumentException(
                    "The provided name was invalid."
                    );

            _name = name;
            _value = value;
            _expiry = null;
            _httpOnly = false;
            _secure = false;
            _domain = null;
            _path = null;
        }

        /// <summary>
        /// The cookie's name.
        /// </summary>
        public string Name
        {
            get { return _name; }
        }
        /// <summary>
        /// The cookie's value, or the contents of the cookie.
        /// </summary>
        public string Value
        {
            get { return _value; }
            set { _value = value; }
        }
        /// <summary>
        /// The date and time the cookie expires. Set to null
        /// to make the cookie expire at the end of the session.
        /// </summary>
        public Nullable<DateTime> Expiry
        {
            get { return _expiry; }
            set { _expiry = value; }
        }
        /// <summary>
        /// The domain the cookie is valid for. Set to
        /// null to let the browser infer the domain.
        /// </summary>
        public string Domain
        {
            get { return _domain; }
            set
            {
                if (value != null && !HttpCookie.VerifyString(value, DOMAIN_PERMITTED))
                    throw new ArgumentException(
                        "The provided domain contains invalid characters."
                        );

                _domain = value;
            }
        }
        /// <summary>
        /// The path the cookie is valid for. Set to
        /// null to let the browser infer the path.
        /// </summary>
        public string Path
        {
            get { return _path; }
            set
            {
                if (value != null && !HttpCookie.VerifyString(value, PATH_PERMITTED))
                    throw new ArgumentException(
                        "The provided path contains invalid characters."
                        );

                _path = value;
            }
        }
        /// <summary>
        /// Whether the cookie should only be sent
        /// over HTTP requests. Defaults to false.
        /// </summary>
        public bool HttpOnly
        {
            get { return _httpOnly; }
            set { _httpOnly = value; }
        }
        /// <summary>
        /// Whether the cookie should only be sent
        /// over secure (HTTPS) connections. Defaults
        /// to false.
        /// </summary>
        public bool Secure
        {
            get { return _secure; }
            set { _secure = value; }
        }

        /// <summary>
        /// Gets the string representation of the cookie.
        /// Can be inserted directly in to a Set-Cookie
        /// HTTP header.
        /// </summary>
        /// <returns>The string representation of the cookie.</returns>
        public override string ToString()
        {
            StringBuilder cookieBuilder = new StringBuilder();

            cookieBuilder.AppendFormat("{0}=", this.Name);

            if (this.Value != null)
                cookieBuilder.AppendFormat("{0}; ", this.Value);
            else cookieBuilder.Append(";");

            if (this.Expiry.HasValue)
                cookieBuilder.AppendFormat(
                    "Expires={0}; ",
                    this.Expiry.Value.ToString("R")
                    );

            if (!String.IsNullOrWhiteSpace(this.Domain))
                cookieBuilder.AppendFormat(
                    "Domain={0}; ",
                    this.Domain
                    );

            if (!String.IsNullOrWhiteSpace(this.Path))
                cookieBuilder.AppendFormat(
                    "Path={0}; ",
                    this.Path
                    );

            if (this.HttpOnly)
                cookieBuilder.Append("HttpOnly; ");

            if (this.Secure)
                cookieBuilder.Append("Secure; ");

            return cookieBuilder.ToString().Trim();
        }
        /// <summary>
        /// Retrieves the hash code for this object.
        /// </summary>
        public override int GetHashCode()
        {
            int hash = 0x5f3759df;
            unchecked
            {
                hash *= (hash & 0xFFF) + this.Name.GetHashCode();
                hash *= (hash & 0xFFF) + this.Value.GetHashCode();
                hash *= (hash & 0xFFF) + this.Expiry.GetHashCode();
                hash *= (hash & 0xFFF) + (this.Domain ?? "NULL_DOMAIN").GetHashCode();
                hash *= (hash & 0xFFF) + (this.Path ?? "NULL_PATH").GetHashCode();
                hash *= (hash & 0xFFF) + this.HttpOnly.GetHashCode();
                hash *= (hash & 0xFFF) + this.Secure.GetHashCode();
            }

            return hash;
        }
        /// <summary>
        /// Compares this cookie to an object using
        /// CookieComparison.Attributes.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>True if the object equals this cookie.</returns>
        public override bool Equals(object obj)
        {
            return HttpCookie.Equals(this, obj as HttpCookie, CookieComparison.Attributes);
        }
        /// <summary>
        /// Compares this cookie to another.
        /// </summary>
        /// <param name="cookie">The cookie to compare to.</param>
        /// <param name="comparison">How the comparison should be made.</param>
        /// <returns>True if <paramref name="cookie"/> is considered equal.</returns>
        public bool Equals(HttpCookie cookie, CookieComparison comparison)
        {
            return HttpCookie.Equals(this, cookie, comparison);
        }

        /// <summary>
        /// Compares two cookies and determines whether they are equal.
        /// </summary>
        /// <param name="c0">The first cookie to compare.</param>
        /// <param name="c1">The second cookie to compare.</param>
        /// <param name="comparison">The comparison semantics.</param>
        /// <returns>True if the two cookies are considered equal.</returns>
        public static bool Equals(HttpCookie c0, HttpCookie c1, CookieComparison comparison)
        {
            if (c0 == null ^ c1 == null) return false;
            if (c0 == null && c1 == null) return true;

            bool equal = false;
            if (comparison == CookieComparison.Name)
            {
                equal = c0.Name.Equals(c1.Name, StringComparison.Ordinal);
            }
            else
            {
                equal = c0.ToString().Equals(c1.ToString(), StringComparison.Ordinal);
            }

            return equal;
        }
    }
}
