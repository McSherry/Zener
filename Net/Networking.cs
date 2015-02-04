using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace McSherry.Zener.Net
{
    /// <summary>
    /// Provides a set of methods related to networking.
    /// </summary>
    public static class Networking
    {
        #region HttpStatus Messages
        private static readonly Dictionary<HttpStatus, string> HttpStatusMessages
            = new Dictionary<HttpStatus, string>()
            {
                { (HttpStatus)100, "Continue" },
                { (HttpStatus)101, "Switching Protocols" },
                
                { (HttpStatus)200, "OK" },
                { (HttpStatus)201, "Created" },
                { (HttpStatus)202, "Accepted" },
                { (HttpStatus)203, "Non-Authoritative Information" },
                { (HttpStatus)204, "No Content" },
                { (HttpStatus)205, "Reset Content" },
                { (HttpStatus)206, "Partial Content" },
                
                { (HttpStatus)300, "Multiple Choices" },
                { (HttpStatus)301, "Moved Permanently" },
                { (HttpStatus)302, "Found" },
                { (HttpStatus)303, "See Other" },
                { (HttpStatus)304, "Not Modified" },
                { (HttpStatus)305, "Use Proxy" },
                { (HttpStatus)307, "Temporary Redirect" },

                { (HttpStatus)400, "Bad Request" },
                { (HttpStatus)401, "Unauthorized" },
                { (HttpStatus)402, "Payment Required" },
                { (HttpStatus)403, "Forbidden" },
                { (HttpStatus)404, "Not Found" },
                { (HttpStatus)405, "Method Not Allowed" },
                { (HttpStatus)406, "Not Acceptable" },
                { (HttpStatus)407, "Proxy Authentication Required" },
                { (HttpStatus)408, "Request Time-out" },
                { (HttpStatus)409, "Conflict" },
                { (HttpStatus)410, "Gone" },
                { (HttpStatus)411, "Length Required" },
                { (HttpStatus)412, "Precondition Failed" },
                { (HttpStatus)413, "Request Entity Too Large" },
                { (HttpStatus)414, "Request URI Too Large" },
                { (HttpStatus)415, "Unsupported Media Type" },
                { (HttpStatus)416, "Request range not satisfiable" },
                { (HttpStatus)417, "Expectation Failed" },

                { (HttpStatus)500, "Internal Server Error" },
                { (HttpStatus)501, "Not Implemented" },
                { (HttpStatus)502, "Bad Gateway" },
                { (HttpStatus)503, "Service Unavailable" },
                { (HttpStatus)504, "Gateway Time-out" },
                { (HttpStatus)505, "HTTP Version not supported" }
            };
        #endregion

        /// <summary>
        /// Gets the message associated with the status code.
        /// </summary>
        /// <param name="status">The status code to get the message for.</param>
        /// <returns>The status code's associated message.</returns>
        public static string GetMessage(this HttpStatus status)
        {
            return HttpStatusMessages[status];
        }
        /// <summary>
        /// Gets the numeric code associated with the HTTP status.
        /// </summary>
        /// <param name="status">The status to get the code for.</param>
        /// <returns>An integer with the status code's value.</returns>
        public static int GetCode(this HttpStatus status)
        {
            return (int)status;
        }
    }
}
