using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SynapLink.Zener.Net
{
    /// <summary>
    /// The available HTTP methods (verbs).
    /// </summary>
    enum HTTPRequestMethod
    {
        GET, POST
    }

    /// <summary>
    /// A class encapsulating an HTTP request from the client.
    /// </summary>
    public class HTTPRequest
    {
        private List<IHTTPHeader> _headers;
        private List<string> _post, _get;
        private string _raw;

        internal HTTPRequest() {}

        /// <summary>
        /// The HTTP method/verb used with the request.
        /// </summary>
        public HTTPRequestMethod Method { get; internal set; }
        /// <summary>
        /// The HTTP headers sent with the request.
        /// </summary>
        public List<IHTTPHeader> Headers
        {
            get { return new List<IHTTPHeader>(_headers); }
            internal set { _headers = value; }
        }
        /// <summary>
        /// All POST parameters sent with the request.
        /// </summary>
        public List<string> POST
        {
            get { return new List<string>(_post); }
            internal set { _post = value; }
        }
        /// <summary>
        /// All GET (query string) parameters send with the request.
        /// </summary>
        public List<string> GET
        {
            get { return new List<string>(_get); }
            internal set { _get = value; }
        }
        /// <summary>
        /// The raw request received from the client.
        /// </summary>
        public string Raw
        {
            get { return _raw; }
            internal set { _raw = value; }
        }
    }
}
