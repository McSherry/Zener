using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// An interface providing a contract for HTTP headers.
    /// </summary>
    public interface IHTTPHeader
    {
        /// <summary>
        /// The field or header name (e.g. Content-Type).
        /// </summary>
        string Field { get; }
        /// <summary>
        /// The value of the field/header, as a string.
        /// </summary>
        string Value { get; }
    }
}
