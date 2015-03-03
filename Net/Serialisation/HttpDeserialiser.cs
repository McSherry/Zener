/*
 *      Copyright (c) 2014-2015, Liam McSherry
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Dynamic;

namespace McSherry.Zener.Net.Serialisation
{
    /// <summary>
    /// The exception used when an HTTP request times out
    /// (status code 408).
    /// </summary>
    public sealed class HttpRequestTimeoutException
        : HttpException
    {
        /// <summary>
        /// Creates a new HttpRequestTimeoutException.
        /// </summary>
        public HttpRequestTimeoutException()
            : base(HttpStatus.RequestTimeout)
        {

        }
        /// <summary>
        /// Creates a new HttpRequestTimeoutException.
        /// </summary>
        /// <param name="message">The message to send with the exception.</param>
        public HttpRequestTimeoutException(string message)
            : base(HttpStatus.RequestTimeout, message)
        {

        }
        /// <summary>
        /// Creates a new HttpRequestTimeoutException.
        /// </summary>
        /// <param name="message">The message to send with the exception.</param>
        /// <param name="innerException">
        /// The exception that caused this exception to be raised.
        /// </param>
        public HttpRequestTimeoutException(string message, Exception innerException)
            : base(HttpStatus.RequestTimeout, message, innerException)
        {

        }
    }

    /// <summary>
    /// The base class for implementing a deserialiser that
    /// transforms data in to an HttpRequest class.
    /// </summary>
    public abstract class HttpDeserialiser
        : IDisposable
    {
        /// <summary>
        /// The default/recommended timeout value, in milliseconds.
        /// </summary>
        protected const int DefaultTimeout = 30000;
        /// <summary>
        /// The maximum length of the headers, in octets, a
        /// HTTP deserialiser should accept.
        /// </summary>
        protected const int MaxHeaderLength = 8192;

        /// <summary>
        /// A hashset of the characters permitted in a variable name.
        /// Used with key-value parsers (such as for POST/GET variables).
        /// </summary>
        private static readonly HashSet<char> VarPermitted;
        /// <summary>
        /// A hashset of the characters prohibited to be at the start of
        /// a variable name. Used with key-value parsers (such as for
        /// POST/GET variables).
        /// </summary>
        private static readonly char[] VarStartProhibited;

        /// <summary>
        /// A dictionary of character encodings by name. The names are
        /// considered case-insensitive.
        /// </summary>
        protected static readonly Dictionary<string, Encoding> CharacterEncodings;

        /// <summary>
        /// Filters any characters that are not permitted in C# variable
        /// names from the provided string.
        /// </summary>
        /// <param name="str">The string to filter.</param>
        /// <returns>
        /// The string, with any prohibited characters filtered out.
        /// </returns>
        protected static string FilterInvalidNameCharacters(string str)
        {
            return new string(str.Where(VarPermitted.Contains).ToArray())
                .TrimStart(VarStartProhibited);
        }
        /// <summary>
        /// Parses the provided string, assuming it is in the
        /// application/x-www-form-urlencoded format.
        /// </summary>
        /// <param name="req">The HttpRequest sending the data.</param>
        /// <param name="body">The stream containing the request body.</param>
        /// <returns>
        /// If the string contains any key-value pairs, an
        /// ExpandoObject containing them. Else, an Empty.
        /// </returns>
        /// <exception cref="System.FormatException">
        /// Thrown when one or more of the key-value pairs in the
        /// string contains an invalid or malformed percent-encoded
        /// character.
        /// </exception>
        protected static dynamic ParseFormUrlEncoded(HttpRequest req, Stream body)
        {
            using (var sr = new StreamReader(body))
            {
                return ParseFormUrlEncoded(sr.ReadToEnd());
            }
        }
        /// <summary>
        /// Parses the provided string, assuming it is in the
        /// application/x-www-form-urlencoded format.
        /// </summary>
        /// <param name="body">The string to parse.</param>
        /// <returns>
        /// If the string contains any key-value pairs, an
        /// ExpandoObject containing them. Else, an Empty.
        /// </returns>
        /// <exception cref="System.FormatException">
        /// Thrown when one or more of the key-value pairs in the
        /// string contains an invalid or malformed percent-encoded
        /// character.
        /// </exception>
        protected static dynamic ParseFormUrlEncoded(string body)
        {
            // If the string is empty, there's nothing we can
            // parse. This means we should just return an Empty.
            if (body.Length == 0)
            {
                return new Empty();
            }

            var dyn = new ExpandoObject() as IDictionary<string, object>;
            var bdr = new StringBuilder();

            string section = null;
            bool inValue = false;

            foreach (char c in body)
            {
                // The path we take depends on whether we're in a
                // value or not.
                if (inValue)
                {
                    // The ampersand is used to separate key-value pairs.
                    // A value cannot contain an unencoded ampersand, so
                    // the presence of one must indicate the end of this
                    // key-value pair and the start of another.
                    if (c == '&')
                    {
                        // Since we're at the end of the key-value pair, we can add
                        // what we have to our ExpandoObject. We need to decode the
                        // value so that the user gets an accurate representation and
                        // doesn't need to deal with encoding/decoding it.
                        dyn[section] = bdr.ToString().UrlDecode(xformsSpaces: true);
                    }
                    else
                    {
                        // The ampersand is the only special character in a value, so
                        // if the character isn't an ampersand we can just add it to
                        // our StringBuilder.
                        bdr.Append(c);
                    }
                }
                else
                {
                    // The equals character is used to separate the key and the
                    // value in a key-value pair. If we encounter one whilst in
                    // the key, it means we've reached the end of our key and need
                    // to start on the value.
                    if (c == '=')
                    {
                        // Set the variable to indicate that the next iteration should
                        // treat the characters it iterates over as the characters in
                        // the key-value pair's value.
                        inValue = true;
                        // We need to store the name somewhere as we'll be using the
                        // StringBuilder to build the value. We also need to URL-decode
                        // it, and filter out any characters which cannot be present
                        // in identifiers. Because the values are being added to an
                        // ExpandoObject instead of your standard dictionary, the
                        // keys need to be valid C# identifiers, or the user won't
                        // be able to access them.
                        //
                        // For example:
                        //
                        //      test-value      ->      testvalue
                        //      0123value4      ->      value4
                        //
                        section = FilterInvalidNameCharacters(
                            bdr.ToString().UrlDecode(xformsSpaces: true)
                            );
                        // As said, we're using the StringBuilder in building the
                        // value, too. This means we need to clear it before we
                        // end this iteration, or the key will be mixed in with
                        // the value.
                        bdr.Clear();
                    }
                    // We also need to support the presence of the pair separator
                    // inside the key. While good convention would say that you should
                    // give each key-value pair a value, we need to support pairs
                    // without values.
                    else if (c == '&')
                    {
                        // As we did before, we need to URL-decode and filter out any
                        // invalid characters that are present in the key.
                        section = FilterInvalidNameCharacters(
                            bdr.ToString().UrlDecode(xformsSpaces: true)
                            );
                        // We then assign the value <null> to the specified key in
                        // our ExpandoObject. In previous versions, we assigned the
                        // value of an empty string. However, this does not accurately
                        // represent the value we were (or, rather, weren't) given.
                        dyn[section] = null;
                        // We're not going in to a value this time, but we potentially
                        // going in to another key. This means we need to clear the
                        // StringBuilder before going to the next iteration.
                        bdr.Clear();
                    }
                    // If we get here, the character has no special meaning. This means
                    // we're free to append it to our StringBuilder without first
                    // specially handling its value.
                    else
                    {
                        bdr.Append(c);
                    }
                }
            }
            // In the majority of cases, the string won't end with a terminator. This
            // means that we will, quite possibly, be left with data in our variable
            // "section" and the StringBuilder. To ensure that this data makes it to
            // the user, we need to add it outside the loop.
            if (inValue)
            {
                // If we're in a value, we've already retrieved and decoded the key
                // of the key-value pair. All that's left is to take the value (which
                // will be in the StringBuilder), decode it, and assign it to the
                // ExpandoObject with the retrieved key.
                dyn[section] = bdr.ToString().UrlDecode(xformsSpaces: true);
            }
            else
            {
                // If we're not in a value, we do the same as we would in the loop
                // and take the current value in the StringBuilder, URL-decode it,
                // and filter out any invalid characters.
                section = FilterInvalidNameCharacters(
                    bdr.ToString().UrlDecode(xformsSpaces: true)
                    );
                // Just like we did in the loop, we add the key to our ExpandoObject
                // and give it the value <null>.
                dyn[section] = null;
            }

            // If we get here, we've populated the ExpandoObject with at least one
            // key-value pair. All we need to do now is return it to the caller.
            return dyn;
        }

        static HttpDeserialiser()
        {
            VarPermitted = new HashSet<char>(
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_"
                );
            VarStartProhibited = "0123456789".ToCharArray();

            CharacterEncodings = new Dictionary<string, Encoding>(
                StringComparer.OrdinalIgnoreCase
                )
                {
                    { "ascii",          Encoding.ASCII                      },
                    { "us-ascii",       Encoding.ASCII                      },
                    { "utf-8",          Encoding.UTF8                       },
                    { "utf8",           Encoding.UTF8                       },
                    { "iso-8859-1",     Encoding.GetEncoding("ISO-8859-1")  },
                    { "latin-1",        Encoding.GetEncoding("ISO-8859-1")  },
                    { "windows-1252",   Encoding.GetEncoding(1252)          },
                    { "cp-1252",        Encoding.GetEncoding(1252)          },
                };
        }

        /// <summary>
        /// The stream containing the request to deserialise.
        /// </summary>
        protected readonly Stream RequestStream;
        /// <summary>
        /// The request to assign deserialised values to.
        /// </summary>
        protected readonly HttpRequest pRequest;

        /// <summary>
        /// The method which implements deserialisation using the
        /// protected RequestStream property, and which assigns the
        /// deserialised data to the protected pRequest
        /// </summary>
        protected abstract void Deserialise();

        /// <summary>
        /// Creates a new HttpDeserialiser.
        /// </summary>
        /// <param name="input">
        /// The stream containing the HTTP request to deserialise.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the input stream provided is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided input stream does not support reading.
        /// </exception>
        public HttpDeserialiser(Stream input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(
                    "The provided input stream must not be null."
                    );
            }

            if (!input.CanRead)
            {
                throw new ArgumentException(
                    "The provided input stream must support reading."
                    );
            }

            this.RequestStream = input;
            this.pRequest = new HttpRequest();

            this.Deserialise();
        }

        /// <summary>
        /// The request that was created by deserialising
        /// the data in the provided stream.
        /// </summary>
        public HttpRequest Request
        {
            get { return this.pRequest; }
        }

        /// <summary>
        /// Releases any resources held by the deserialiser.
        /// </summary>
        /// <remarks>
        /// Implementations of Dispose should not dispose of the
        /// stream containing the request's data. This will be
        /// handled by the code creating the deserialiser
        /// (typically, this will be HttpServer).
        /// </remarks>
        public abstract void Dispose();
    }
}
