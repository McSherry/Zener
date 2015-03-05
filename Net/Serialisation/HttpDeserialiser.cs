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

using MediaType     = McSherry.Zener.Core.MediaType;

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
        /// The handler used to handle the data sent with POST requests.
        /// </summary>
        /// <param name="request">
        /// The request with the POST data in it.
        /// </param>
        /// <param name="body">
        /// The request body containing the data sent in the POST request.
        /// </param>
        /// <returns>
        /// If the POST data is meaningful, an ExpandoObject containing any
        /// key-value pairs. Otherwise, an Empty.
        /// </returns>
        protected delegate dynamic PostDataHandler(HttpRequest request, Stream body);

        /// <summary>
        /// The media type parameter used to provide the boundary in multipart
        /// requests.
        /// </summary>
        private const string MultipartBoundary = "boundary";

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
        /// The multipart/form-data format makes use of the
        /// double-dash in places. Having these bytes handy
        /// saves having to retrieve them on each call to
        /// ParseMultipartFormData.
        /// </summary>
        private static readonly byte[] MultipartDoubleDash;
        /// <summary>
        /// The CRLF (Carriage Return, Line Feed) bytes are also used
        /// in multipart data, and having them pre-decoded saves us
        /// decoding each time in ParseMultipartFormData.
        /// </summary>
        private static readonly byte[] CRLF;
        /// <summary>
        /// The default encoding to use when deserialising strings
        /// (such as in multipart data).
        /// </summary>
        private static readonly Encoding DefaultCharEncoding;

        /// <summary>
        /// A dictionary of character encodings by name. The names are
        /// considered case-insensitive.
        /// </summary>
        protected static readonly Dictionary<string, Encoding> CharacterEncodings;        
        /// <summary>
        /// A which maps MediaType instances to their handlers. Used to
        /// determine how to parse POST data from the client.
        /// </summary>
        protected static readonly Dictionary<MediaType, PostDataHandler> PostHandlers;

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
                        // Being at the end of a key-value pair, it means that any
                        // further characters will be a key again, so we need to set
                        // the variable to indicate that we're no longer in a value.
                        inValue = false;
                        // Clear the StringBuilder so that the current value doesn't
                        // get mixed in with the next key.
                        bdr.Clear();
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
        /// <summary>
        /// Parses the provided request body, assuming that it is
        /// in the multipart/form-data format.
        /// </summary>
        /// <param name="req">The HttpRequest that included the body.</param>
        /// <param name="body">The Stream containing the request body.</param>
        /// <returns>
        /// If the body contains any key-value pairs/parts, an
        /// ExpandoObject containing them. Else, an Empty.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided stream does not support reading or
        /// seeking.
        /// </exception>
        /// <exception cref="McSherry.Zener.Net.HttpRequestException">
        /// <para>
        /// Thrown when the client does not provide a boundary with its
        /// request.
        /// </para>
        /// <para>
        /// Thrown when one or more of the parts provided by the client
        /// contain invalid or malformed headers.
        /// </para>
        /// <para>
        /// Thrown when one or more of the parts provided by the client
        /// is incomplete.
        /// </para>
        /// </exception>
        protected static dynamic ParseMultipartFormData(HttpRequest req, Stream body)
        {
            if (!body.CanRead || !body.CanSeek)
            {
                throw new ArgumentException(
                    "The provided stream must support reading and seeking."
                    );
            }

            // We already know that the content uses the multipart/form-data
            // media type. However, this media type should have a parameter
            // giving us the boundary used to separate parts.
            //
            // As the MediaType will already have been parsed, we don't need
            // a try-catch or a call to TryCreate, as we already know it is
            // valid.
            MediaType mt = req.Headers[Rfc7230Serialiser.Headers.ContentType]
                .Last()
                .Value;
            // We need to make sure that the request includes a boundary. If it
            // does not, the request is invalid as there is no way for us to
            // differentiate parts.
            string boundary;
            if (!mt.Parameters.TryGetValue(MultipartBoundary, out boundary))
            {
                // The client has not provided us with a boundary for its
                // multipart data. Throw an exception so that the client receives
                // a 400 response and an error message.
                throw new HttpRequestException(
                    "The client's request contained invalid multipart data (" +
                    "no boundary provided)."
                    );
            }

            // The boundary should be an ASCII-encoded string. We're also passing
            // the boundary's bytes to our boundary-finding method. This method
            // takes a byte array, so performing this conversion first rather than
            // using the overload that converts it on each call saves us a bit of
            // execution time.
            byte[] firstBoundaryBytes = Encoding.ASCII.GetBytes(boundary);
            // The boundaries after the first will be preceded by a CRLF and
            // two additional dashes.
            byte[] boundaryBytes = new byte[
                firstBoundaryBytes.Length + MultipartDoubleDash.Length + CRLF.Length
                ];
            // The CRLF comes first, as the boundaries will be on their own lines.
            Array.Copy(CRLF, boundaryBytes, CRLF.Length);
            // Following the CRLF comes the additional two dashes.
            Array.Copy(
                sourceArray:        MultipartDoubleDash,
                sourceIndex:        0,
                destinationArray:   boundaryBytes,
                // We need to make sure that the bytes are copied after the CRLF
                // we've just copied in to the array. We don't want to overwrite
                // those bytes.
                destinationIndex:   CRLF.Length,
                length:             MultipartDoubleDash.Length
                );
            // All that's left now is to copy the bytes of the boundary in to
            // the array.
            Array.Copy(
                sourceArray:        firstBoundaryBytes,
                sourceIndex:        0,
                destinationArray:   boundaryBytes,
                // Just like before, we don't want to overwrite what we've already
                // copied in to the array, so we need to set the destination index
                // to the sum of the lengths of what we've already copied in.
                destinationIndex:   CRLF.Length + MultipartDoubleDash.Length,
                length:             firstBoundaryBytes.Length
                );

            // At this point, we know we're going to be reading data from the body,
            // so creation of the ExpandoObject here isn't premature.
            var dyn = new ExpandoObject() as IDictionary<string, object>;
            // We need to ignore data found before the first boundary, as the first
            // boundary indicates the start of multipart data. Any data before the
            // boundary may have meaning, but it is not meaning we know of or are
            // prepared to receive.
            body.ReadUntilFound(firstBoundaryBytes, b => { });
            // The boundary we've just read up to will have a CRLF after it. We don't
            // want this in our data since it's part of the multipart format and not
            // part of the data the client sent, so we seek past it.
            body.Seek(CRLF.Length, SeekOrigin.Current);

            // We're going to need string-building services at several points in the
            // below code, so it would make sense to only ever keep one instance
            // around.
            StringBuilder partBdr = new StringBuilder();
            // We need to read to the end of the stream to make sure we get all the
            // data.
            while (body.Position != body.Length)
            {
                // The first order of business is to read the headers for each part.
                // The headers give us information about what's in the part, such as
                // the media type of the associated content and the name of the data
                // we've been sent (usually the name of the form element or the
                // uploaded file).

                // As we're reading headers, we're safe to use ASCII encoding.
                string line = body.ReadAsciiLine();
                // Just as with HTTP requests, the headers in a part are separated
                // from the part body by an empty line. The difference here is that
                // part headers may not be prefixed with an empty line.
                while (!String.IsNullOrEmpty(line))
                {
                    // The line isn't empty/null, so we can append it to the
                    // StringBuilder we're using to build the headers.
                    partBdr.AppendLine(line);
                    // Read the next line from the stream.
                    line = body.ReadAsciiLine();
                }

                // We now need to parse the headers the client sent us so
                // we can make use of them.
                HttpHeaderCollection headers;
                using (var sr = new StringReader(partBdr.ToString()))
                {
                    // We've just used the value in the StringBuilder, so
                    // we can clear it for use later.
                    partBdr.Clear();

                    try
                    {
                        // We now need to attempt to parse the headers
                        // that the client provided to us.
                        headers = new HttpHeaderCollection(
                            HttpHeader.ParseMany(sr)
                            );
                    }
                    // If the client has sent us an invalid header, the
                    // method ParseMany will throw an ArgumentException.
                    catch (ArgumentException aex)
                    {
                        // We need to catch the ArgumentException and then
                        // rethrow it as an HttpRequestException so that it
                        // is caught by HttpServer.
                        throw new HttpRequestException(
                            "The client sent a part with invalid headers.",
                            aex
                            );
                    }
                }

                // After reading the headers, there needs to be space at least for
                // one boundary and that boundary's trailing double-dashes. If there
                // is not, the client has sent a malformed request.
                if (boundaryBytes.Length + 2 > (body.Length - body.Position))
                {
                    throw new HttpRequestException(
                        "The client sent an incomplete part."
                        );
                }

                // Each part should have with it a 'Content-Disposition' header.
                // We need to use this header's value to name the key we add to
                // our ExpandoObject.
                HttpHeader ctnDis = headers[Rfc7230Serialiser.Headers.ContentDisposition]
                    .LastOrDefault(),
                // We also need a 'Content-Type' header so we know how to handle the
                // content that was sent with the request. We won't consider a missing
                // 'Content-Type' header an error, but we will have to treat the body
                // associated with that part as a byte array rather than a string.
                    ctnType = headers[Rfc7230Serialiser.Headers.ContentType]
                    .LastOrDefault();
                // If the client hasn't sent a 'Content-Disposition' header, we
                // will consider the request malformed, and need to throw an exception
                // to report this to the client.
                if (ctnDis == default(HttpHeader))
                {
                    throw new HttpRequestException(
                        "The client sent a part without a \"Content-Disposition\" " +
                        " header."
                        );
                }

                string partName;
                // The 'Content-Disposition' header should be a set of named
                // parameters. We've got a HttpHeader subclass just for this.
                var contentDisposition = new NamedParametersHttpHeader(
                    header:             ctnDis,
                    // We're making this a bit friendlier to potentially
                    // badly-written clients. We are considering the case of
                    // the paremeter keys case-insensitive.
                    keyCaseInsensitive: true
                    );
                // We require that the part have a name. If it doesn't, we have
                // no way to identify it.
                if (!contentDisposition.Pairs.TryGetValue("name", out partName))
                {
                    // If there isn't a part name, we need to throw an exception.
                    throw new HttpRequestException(
                        "The client sent a part without a name."
                        );
                }
                // We don't know what the part name may contain, so we need to
                // URL-decode it and then filter out any invalid characters.
                partName = FilterInvalidNameCharacters(partName.UrlDecode());
                // We're going to need somewhere to store the bytes we read from
                // this part.
                List<byte> partBuffer = new List<byte>();
                // Read all the bytes in the part until we find the boundary.
                // The bytes are read in to our part buffer.
                body.ReadUntilFound(boundaryBytes, partBuffer.Add);
                // We then need to seek past the CRLF that suffixes the boundary.
                body.Seek(CRLF.Length, SeekOrigin.Current);

                MediaType contentType;
                // We need to check both that the part contains a 'Content-Type'
                // header and that the provided 'Content-Type' header is valid. If
                // either the part does not contain a header, or the provided header
                // is invalid, 
                if (
                    ctnType == default(HttpHeader) ||
                    !MediaType.TryCreate(ctnType.Value, out contentType)
                    )
                {
                    // If the header is invalid or not present, we will interpret
                    // the data as plain text.
                    contentType = MediaType.PlainText;
                }

                // We've parsed (and, if necessary, defaulted) the media type, so
                // now we need to determine how to handle it. If the media type is
                // in the text/* super-type group, we can attempt to handle it as
                // a string.
                if (MediaType.Text.IsEquivalent(contentType))
                {
                    Encoding encoding;
                    string encodingName;
                    // We attempt to retrieve the 'charset' parameter from the text/*
                    // media type. Some media types will be sent with the specific
                    // character set, some will not.
                    if (contentType.Parameters.TryGetValue("charset", out encodingName))
                    {
                        // We then attempt to, from our map of names to encodings,
                        // retrieve an encoder for the character set the media type
                        // has specified.
                        if (!CharacterEncodings.TryGetValue(encodingName, out encoding))
                        {
                            // If we don't have an encoder for the specified character
                            // set, use the default one.
                            encoding = DefaultCharEncoding;
                        }
                    }
                    // The media type is not required to include a 'charset' parameter,
                    // so it is quite likely we won't have one and will have to pick a
                    // default.
                    else
                    {
                        // If no character set is specified, use the default one.
                        encoding = DefaultCharEncoding;
                    }

                    // We've successfully retrieved an encoding. What this means is that
                    // we have to convert the list of bytes to an array, then use the
                    // encoding to retrieve the text represented by those bytes in the
                    // specified encoding.
                    //
                    // The retrieved string is then assigned to the key with the value
                    // of the part's filtered name.
                    dyn[partName] = encoding.GetString(partBuffer.ToArray());
                }
                // The media type specified by the client is not a text media type, so
                // we have to treat it as bytes and not attempt to interpret it as text.
                else
                {
                    // Convert the part bytes to an array from the list, and assign the
                    // value of the array to the key with the value of the part's
                    // (filtered) name.
                    dyn[partName] = partBuffer.ToArray();
                }

                // The end of the multipart data will be indicated by a boundary suffixed
                // with two dashes. We need to read some bytes from the stream to check
                // whether we've reached the end of the multipart data.
                byte[] next = new byte[MultipartDoubleDash.Length];
                // Read the bytes from the stream and take the return value from the call.
                // This will help us determine whether the end of the multipart data has
                // been reached.
                int read = body.Read(next, 0, next.Length);
                // There are three ways we will use to determine whether we've reached
                // the end of the multipart data:
                if (
                    // #1:  If we read no bytes or Read returns a -1 (end of stream), we
                    //      have probably reached the end of stream and cannot read
                    //      anything more.
                    //
                    //      We won't throw an exception in this instance because leaving
                    //      out the suffixing dashes seems like a mistake that could be
                    //      easily made in a poorly-written client.
                    read == 0 || read == -1 ||
                    // #2:  If the position within the stream is greater than the length
                    //      of the stream, we've read past the end. See last paragraph of
                    //      above comment.
                    body.Length <= body.Position ||
                    // #3:  The bytes we read from the stream are double dashes. This is
                    //      the correct way of indicating that we've reached the end of
                    //      the multipart data.
                    next.SequenceEqual(MultipartDoubleDash)
                    )
                {
                    // We've reached the end, so we don't need to iterate through anything
                    // else and can break out of the loop.
                    break;
                }
                // If we're here, we've not reached the end of the data.
                else
                {
                    // We've not reached the end, but we have read some bytes from the
                    // stream. This will have advanced the stream's position, so we need
                    // to move backwards from where we are so the next iteration can
                    // read the bytes we just read without issue.
                    body.Seek(-next.Length, SeekOrigin.Current);
                }
            }

            // What we return depends on whether we read any parts from the stream.
            // If we did, the ExpandoObject will have a number of items that is greater
            // than zero, and we can return it.
            //
            // If we didn't, the ExpandoObject will have no items, and we have to return
            // an Empty.
            return dyn.Count == 0 ? (dynamic)new Empty() : dyn;
        }

        static HttpDeserialiser()
        {
            VarPermitted = new HashSet<char>(
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_"
                );
            VarStartProhibited = "0123456789".ToCharArray();

            MultipartDoubleDash = Encoding.ASCII.GetBytes("--");
            CRLF = Encoding.ASCII.GetBytes("\r\n");

            DefaultCharEncoding = Encoding.UTF8;

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

            PostHandlers = new Dictionary<MediaType, PostDataHandler>(
                new MediaType.EquivalencyComparer()
                )
            {
                { "multipart/form-data",                ParseMultipartFormData  },
                { "application/x-www-form-urlencoded",  ParseFormUrlEncoded     },
            };
        }

        /// <summary>
        /// The stream containing the request to deserialise.
        /// </summary>
        protected readonly Stream RequestStream;

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
        }

        /// <summary>
        /// Deserialises a request from the input stream the deserialiser
        /// was initialised with.
        /// </summary>
        /// <returns>
        /// A request deserialised from the input stream.
        /// </returns>
        /// <remarks>
        /// If the deserialiser has already deserialised a request, this
        /// method will return that request.
        /// </remarks>
        public HttpRequest Deserialise()
        {
            return this.Deserialise(returnPrevious: true);
        }
        /// <summary>
        /// Deserialises a request from the provided input stream.
        /// </summary>
        /// <param name="returnPrevious">
        /// Whether the deserialiser should return the previously-deserialised
        /// HttpRequest, if available. If this is false, the deserialiser will
        /// attempt to deserialise a new HttpRequest from the Stream.
        /// </param>
        /// <returns>A request deserialised from the input stream.</returns>
        /// <remarks>
        /// <para>
        /// Implementations must cache the previously-deserialised request. 
        /// </para>
        /// <para>
        /// If an implementation does not have a cached request, it must
        /// deserialise one from the stream, cache it, and return it.
        /// </para>
        /// </remarks>
        public abstract HttpRequest Deserialise(bool returnPrevious);

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
