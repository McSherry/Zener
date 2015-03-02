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
using System.Text.RegularExpressions;
using System.IO;

using MediaType = McSherry.Zener.Core.MediaType;

namespace McSherry.Zener.Net.Serialisation
{
    /// <summary>
    /// A deserialiser for HTTP/1.x-series protocols.
    /// </summary>
    /// <remarks>
    /// This deserialiser is based on the HTTP/1.1 standard,
    /// but should function properly when deserialising HTTP/1.0.
    /// </remarks>
    public sealed class Http1Deserialiser
        : HttpDeserialiser, IDisposable
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
        private delegate dynamic PostDataHandler(HttpRequest request, Stream body);

        /// <summary>
        /// The "short circuit" timeout is used as a timeout for sending the
        /// first header. If we don't receive the first header before the end
        /// of the timeout, we consider the client incompetent or malicious,
        /// and terminate the connection.
        /// </summary>
        private const int ShortCircuitTimeout = 750;

        private static readonly char[] RequestLineWhiteSpaceArray;
        /// <summary>
        /// The characters that are considered valid white-space in a
        /// request line.
        /// </summary>
        private static readonly HashSet<char> RequestLineWhiteSpace;
        /// <summary>
        /// The regular expression used to validate the HTTP version
        /// sent in the client's request line.
        /// </summary>
        private static readonly Regex RequestVersionRegex;
        /// <summary>
        /// A which maps MediaType instances to their handlers. Used to
        /// determine how to parse POST data from the client.
        /// </summary>
        private static readonly Dictionary<MediaType, PostDataHandler> PostHandlers;

        /// <summary>
        /// Parses the provided line as an HTTP request line, and sets
        /// the relevant properties in the provided HttpRequest appropriately.
        /// </summary>
        /// <param name="rq">The request to set the properties on.</param>
        /// <param name="rqLine">The client's request line.</param>
        /// <exception cref="McSherry.Zener.Net.HttpFatalException">
        /// Thrown when the client sends an invalid or malformed request
        /// line.
        /// </exception>
        /// <exception cref="System.HttpRequestException">
        /// <para>
        /// Thrown when the client sends a query string with invalid
        /// percent-encoded characters.
        /// </para>
        /// </exception>
        private static void ParseRequestLine(HttpRequest rq, string rqLine)
        {
            // As is written in Deserialise, a client may send as many empty
            // lines as it pleases before the request line. However, the lines
            // it does send must not contain any characters, including white-space.
            //
            // If the client has sent us a line with whitespace in it, the client
            // is broken/wrong.
            if (String.IsNullOrWhiteSpace(rqLine))
            {
                throw new HttpFatalException(
                    "The client sent an empty line where it was not permitted to."
                    );
            }

            // We're expecting to have three parts in the list, so it
            // makes sense to make the capacity the number we expect to
            // (and, with compliant clients, should) receive.
            IList<string> parts = new List<string>(3);

            // Due to the laxity in what is considered a valid
            // separator in the request line, we can't use the nice
            // and easy method of calling String.Split. Instead, we
            // need to iterate over the string.
            var rqChars = rqLine.ToCharArray();
            for (int i = 0, s = i; i < rqChars.Length; i++)
            {
                // We need to check whether the current character
                // is white-space, since that's what we're using
                // to separate the parts of the request line.
                if (RequestLineWhiteSpace.Contains(rqChars[i]))
                {
                    // We need to check whether we're at the start
                    // of a part. If we are, it means that there
                    // are no characters in the current part.
                    if (s == i)
                    {
                        // There are no characters in this part,
                        // so we want to advance one character past
                        // the current start and try again.
                        s++;
                        // There's nothing to add to the list of
                        // parts, so we can just skip this iteration.
                        continue;
                    }
                    // We're going to need to copy the characters.
                    // The variable i stores our current position but,
                    // since the current position is white-space, we want
                    // the previous character. The variable s stores the
                    // start of the current part.
                    //
                    // By subtracting the start of the current part from
                    // the current position (minus one), we can determine
                    // how long the current part is.
                    parts.Add(rqLine
                        .Substring(s, (i - 1) - s)
                        // Just in case we've let some white-space slip
                        // in, we'll trim the substring.
                        .Trim(RequestLineWhiteSpaceArray)
                        );
                    // We've reached the end of one part, which means we
                    // now need to update the variable containing the start
                    // of the current part.
                    //
                    // We want to avoid the white-space we're currently on,
                    // so we set the start of the current part to one ahead
                    // of our current position.
                    s = i + 1;
                }
                // If the character isn't white-space, we want to make sure
                // that we're not at the end of the string.
                else if (i == rqChars.Length - 1)
                {
                    // If we're here, we're at the last iteration of the
                    // loop, and the last character in the loop is not
                    // white-space. This means that we now need to add
                    // whatever characters remain in this (non-whitespace-
                    // terminated) part.
                    parts.Add(rqLine
                        .Substring(s)
                        // Just in case we've let some white-space slip
                        // in, we'll trim the substring.
                        .Trim(RequestLineWhiteSpaceArray)
                        );
                }
            }

            // A well-formed request line should contain only three
            // parts. We want to check to make sure that this request
            // line is well-formed.
            if (parts.Count != 3)
            {
                // If the request line doesn't have three parts, it's
                // malformed and we need to close the connection.
                throw new HttpFatalException(
                    "The client sent a malformed HTTP request line."
                    );
            }

            // The first part contains the request method. Technically,
            // these are meant to be case-sensitive. However, to be friendly
            // to potentially non-compliant clients, we're converting
            // them to uppercase.
            rq.Method = parts[0].ToUpper();

            // The second part contains the requested path and any GET
            // (query string) variables. As such, we have to parse it before
            // we can hand off the values to the HttpRequest.
            //
            // We're going to iterate through all the characters in this
            // part, and we're going to check for a question mark (?). This
            // character prefixes a query string, so its presence means
            // we've probably been sent a query string.
            int strIndex = 0;
            bool hasQuery = false;
            string path = String.Intern(parts[1]);
            for (; strIndex < path.Length; strIndex++)
            {
                // We're iterating through until we find the
                // first question mark.
                if (path[strIndex] == '?')
                {
                    // We've found one, so we need to set the bool
                    // to indicate that we did find one.
                    hasQuery = true;
                    // We no longer need to read through the string,
                    // so we can break out of the loop.
                    break;
                }
            }

            try
            {
                // What we do next depends on whether we have a query string.
                if (hasQuery)
                {
                    // If we have one, we take the bottom section of the
                    // string (the bit before the question mark) and set
                    // the request's path to its value.
                    rq.Path = path.Substring(0, strIndex).UrlDecode();

                    // We only want to parse the query string if it isn't
                    // zero length. It is possible that the client sent us
                    // a URL with a question mark but no actual variables.
                    // 
                    //  For example:     example.org?
                    //
                    // Except for the prefixing question mark, query string
                    // variables obey exactly the format used in POST requests
                    // with the application/x-www-form-urlencoded media type.
                    // This means that we can just call the same method we
                    // use with POST requests for GET variables.
                    //
                    // At this point, strIndex will be on the query string's
                    // prefixing question mark. We don't want this in the
                    // string we're parsing, so we need to increment past it.
                    rq._get = ParseFormUrlEncoded(path.Substring(++strIndex));
                }
                else
                {
                    // If there's no query string, it means the entire part
                    // comprises the request path, so we just set the path
                    // to the entirety of the received path string.
                    rq.Path = path.UrlDecode();
                }
            }
            // The method ParseFormUrlEncoded calls UrlDecode internally.
            // This method will, if the client sends invalid percent-encoded
            // characters, throw a FormatException.
            //
            // In addition, the path may contain encoded characters, which
            // may result in the setting of the HttpRequest.Path property
            // throwing an exception.
            //
            // This fault isn't necessarily fatal (i.e. it may be worthwhile
            // to send a response to the client rather than just closing the
            // connection), so we throw an HttpRequestException instead of
            // an HttpFatalException.
            catch (FormatException fex)
            {
                throw new HttpRequestException(
                    "The client sent invalid percent-encoded characters " +
                    "in its request line.",
                    fex
                    );
            }

            // We're using a regular expression both to check whether the HTTP
            // version string sent with the request is valid and to extract the
            // version identifier from that string (i.e. HTTP/1.1 -> 1.1).
            var match = RequestVersionRegex.Match(parts[2]);
            // If the client's version string matches our regular expression,
            // we can proceed.
            if (match.Success)
            {
                // If we end up here, we know that the version string is going
                // to be something that Version can parse without issue, so we
                // don't need a try-catch.
                //
                // We used a named group in our regular expression, which means
                // we can access the value via the key "Version".
                rq.HttpVersion = new Version(match.Groups["Version"].Value);
            }
            // If it doesn't, it means we can't be sure whether the client speaks
            // HTTP. We're not throwing HttpRequestException because a malformed
            // HTTP version is much more likely to mean that the request has come
            // from a non-HTTP client than a malformed percent-encoded string, which
            // might have come from a poorly-written HTTP client.
            else
            {
                throw new HttpFatalException(
                    "The client sent an invalid HTTP version string."
                    );
            }
        }

        static Http1Deserialiser()
        {
            RequestLineWhiteSpace = new HashSet<char>(new[] 
            {
                ' ', '\t', '\v', '\r', '\xFF'
            });
            RequestLineWhiteSpaceArray = RequestLineWhiteSpace.ToArray();

            RequestVersionRegex = new Regex(
                @"HTTP/(?<Version>[0-9]{1}\.[0-9]{1})",
                RegexOptions.Compiled
                );

            PostHandlers = new Dictionary<MediaType, PostDataHandler>(
                new MediaType.EquivalencyComparer()
                )
            {
                /*
                { "multipart/form-data",                ParseMultipartFormData  },
                */
                { "application/x-www-form-urlencoded",  ParseFormUrlEncoded     },
            };
        }

        /// <summary>
        /// The method which implements deserialisation using the
        /// protected RequestStream property, and which assigns the
        /// deserialised data to the protected pRequest
        /// </summary>
        protected override void Deserialise()
        {
            // We'll be using the starting time to implement a timeout.
            DateTime start = DateTime.Now;

            // The client is permitted to send any number of blank lines
            // before the request line and its headers. These lines may
            // not contain any characters (not even white-space).
            string line;
            do
            {
                // Each line is terminated with a CRLF, so we need to read
                // until we find one of these.
                line = base.RequestStream.ReadAsciiLine();

                // If more time than the short-circuit timeout permits has
                // elapsed, it means that the client took too long to send
                // its first header/the request line.
                if ((DateTime.Now - start).TotalMilliseconds >= ShortCircuitTimeout)
                {
                    // At this point, we don't know what the client speaks.
                    // It might speak HTTP, it might not. It might even be
                    // a malicious client. The best thing to do is to close
                    // the connection without sending a response.
                    //
                    // To do this, we throw an HttpFatalException. This will be
                    // caught by code in HttpServer, which will terminate the
                    // connection.
                    throw new HttpFatalException(
                        "The client took too long to send its first header."
                        );
                }
            } while (String.IsNullOrEmpty(line));

            // We've received our first non-empty line. This line should, then,
            // contain the client's request line. To parse it, we hand it off to
            // the ParseRequestLine function along with the HttpRequest we want to
            // populate with values.
            ParseRequestLine(base.Request, line);
            // The next request line should contain headers. It would be highly
            // unusual for a client to send a request with no headers, but it
            // would be completely valid.
            line = base.RequestStream.ReadAsciiLine();

            // Regardless of whether there are headers to parse, we're going to
            // need to 
            base.Request._headers = new HttpHeaderCollection();
            // How we proceed depends on whether the line is empty or not. If it
            // is empty, it means that the client has sent no headers, so we don't
            // have anything more to parse.
            if (String.IsNullOrEmpty(line))
            {
                // If we're here, there's nothing left to parse from the
                // request. Set anything we would have parsed to its default
                // value.
                base.Request._post = new Empty();
                base.Request._cookies = new Empty();

                // Since there's nothing left to do, skip to the exit
                // point of the method. This will perform any required finishing
                // up (like making Headers read-only) and will return from the
                // function.
                goto deserialiseExit;
            }

            // HTTP headers can span multiple lines. While this is uncommon in the
            // wild, it is still something we need to support. It's already
            // by HttpHeader (in ParseMany), we just need to use a StringBuilder to
            // read multiple lines in to one string.
            StringBuilder hdrBdr = new StringBuilder();
            // Headers are separated from the body by a blank line. This means we
            // can read until we reach a blank line.
            do
            {
                // We're reading from the network again, so we need to make sure
                // that we don't time out. This time we won't be using the short
                // circuit timeout, but the default timeout value provided by the
                // HttpDeserialiser base class.
                if ((DateTime.Now - start).TotalMilliseconds > DefaultTimeout)
                {
                    // The timeout has elapsed, so we need to throw the
                    // appropriate exception and provide a suitable error
                    // message to go with it.
                    throw new HttpRequestTimeoutException(
                        "The client took too long to send its headers."
                        );
                }

                // We've already read a line from the stream and verified that it
                // is not null or empty, so we can append it to the StringBuilder.
                hdrBdr.AppendLine(line);
                // We now need to read the next line from the stream.
                line = base.RequestStream.ReadAsciiLine();
            }
            while (!String.IsNullOrEmpty(line));

            // The method ParseMany will throw an exception if we received an
            // invalid or malformed header, so we need to wrap this in a try-catch.
            try
            {
                // ParseMany takes a TextReader as an argument, and we've got a
                // source string. This means we just need to feed the string in to
                // a StringReader, and the StringReader in to ParseMany.
                using (var sr = new StringReader(hdrBdr.ToString()))
                {
                    base.Request.Headers.AddRange(HttpHeader.ParseMany(sr));
                }
            }
            catch (ArgumentException aex)
            {
                // ParseMany threw an exception, which means that the client sent
                // invalid headers. If we got to this stage, it means the client
                // sent a valid request line. This means it would make sense to
                // respond to the client rather than close the connection. For this
                // reason, we throw an HttpRequestException instead of an
                // HttpFatalException.
                throw new HttpRequestException(
                    "The client sent invalid or malformed HTTP headers.",
                    aex
                    );
            }

            // The 'Content-Length' header tells us two things: 1) whether the
            // client has sent a request body; and 2) how long, in bytes, that
            // request body is.
            var ctnLen = base.Request
                .Headers[Rfc7230Serialiser.Headers.ContentLength]
                // It is possible that the client has sent multiple
                // 'Content-Length' headers (it could be a programmable client
                // and the programmer has opted, intentionally or otherwise, to
                // send multiple). We will consider the last header to be the
                // valid one, as that is the header that will have been set
                // the closest to the sending of the request.
                .LastOrDefault();
            // If the client sent a request body, we need to know its type
            // to be able to parse it. This means that the client should also
            // have sent a 'Content-Type' header.
            var ctnType = base.Request
                .Headers[Rfc7230Serialiser.Headers.ContentType]
                // For the same reason as with the 'Content-Length' header,
                // we take the last 'Content-Type' header present in the
                // request.
                .LastOrDefault();

            // If the retrieved header is the default value, it means
            // that the client sent no 'Content-Length' header. This is not
            // an error, as in most cases the client won't be sending a
            // request with a body.
            //
            // We only want to proceed with request body parsing if there
            // is a request body, and we're using the presence of the header
            // to determine the presence of the request body.
            //
            // We also need to check whether the client sent a 'Content-Type'
            // with its request body. We can only proceed if it has, as otherwise
            // we would not be able to determine whether we could parse the body.
            if (ctnLen != default(HttpHeader) && ctnType != default(HttpHeader))
            {
                // We receive and parse the header value as a string, which
                // means we won't catch an invalid value when parsing the
                // header.
                //
                // The 'Content-Length' header needs to be a valid positive
                // integer. We are unlikely to be receiving 4GiB+ files, so
                // a UInt32 is fine. Using an unsigned integer has the
                // additional benefit of saving us having to check to see
                // whether the value is positive.
                uint contentLength;
                if (!UInt32.TryParse(ctnLen.Value, out contentLength))
                {
                    throw new HttpRequestException(
                        "The client sent an invalid \"Content-Length\" header."
                        );
                }
                // It is perfectly valid for the client to send a
                // 'Content-Length' header for a zero-length body. We need
                // to check for this.
                if (contentLength == 0)
                {
                    base.Request._post = new Empty();
                    // If the body is zero-length, we have nothing to read,
                    // so we skip to the end of the body-reading block.
                    goto readBodyExit;
                }

                // As with the 'Content-Length' header, we need to make sure
                // that the 'Content-Type' header is valid.
                MediaType contentType;
                if (!MediaType.TryCreate(ctnType.Value, out contentType))
                {
                    throw new HttpRequestException(
                        "The client sent an invalid \"Content-Type\" header."
                        );
                }
                // If we don't have a handler for the media type specified
                // by the client, there's no point wasting time and memory
                // reading the body.
                if (!PostHandlers.ContainsKey(contentType))
                {
                    // We're not going to be parsing any POST data, so we
                    // need to make sure the HttpRequest.POST property gives
                    // an Empty.
                    base.Request._post = new Empty();
                    goto readBodyExit;
                }

                // If we're here, we're going to be reading the request body.
                // To do that, we'll need somewhere to store the data before
                // processing. This somewhere will be an array. We are, however,
                // also going to need a MemoryStream for passing to our POST
                // parser methods.
                //
                // We're also entering a new scope here to encourage early
                // garbage collection of the resources. Premature optimisation,
                // maybe, but there's no performance or readability detraction,
                // so we might as well.
                {
                    byte[] bodyBuffer = new byte[contentLength];
                    using (var bodyStream = new MemoryStream(bodyBuffer))
                    {
                        // The running total lets us know how much we've read,
                        // and allows us to calculate how much we have to read.
                        //
                        // We need to keep a running total because calls to
                        // NetworkStream.Read will not block. Instead, they
                        // will read what is available and then return, regardless
                        // of whether what is read is the same length as what
                        // was requested.
                        int runningTotal = 0;
                        // We need to iterate until we've read as many bytes
                        // as we can store in the byte array.
                        while (runningTotal != bodyBuffer.Length)
                        {
                            // If the timeout expires while we're reading, we need
                            // to throw an exception.
                            if ((DateTime.Now - start).TotalMilliseconds > DefaultTimeout)
                            {
                                throw new HttpRequestTimeoutException(
                                    "The client took too long to send its request body."
                                    );
                            }

                            // The method Read returns an int indicating how many
                            // bytes have been read from the stream. We can add this
                            // to our running total variable to keep track of how
                            // much we've read.
                            runningTotal += base.RequestStream.Read(
                                // As we instantiated the MemoryStream with a reference
                                // to this byte array, writing to the byte array will
                                // also write to the MemoryStream. It also has the
                                // benefit of not advancing the MemoryStream's position.
                                buffer: bodyBuffer,
                                // We don't want to overwrite any previously-read bytes,
                                // so we need to start reading bytes from the Stream in
                                // to the buffer at the location we left off at.
                                offset: runningTotal,
                                // The count is just the number of bytes read subtracted
                                // from the number of bytes to be read.
                                count:  bodyBuffer.Length - runningTotal
                                );
                        }

                        // We've read the body, and we know that we support the media
                        // type that the client requested, so now all that's left is
                        // to put the body data through the handler for that media type.
                        //
                        // These handlers return a dynamic, and so they deal with 
                        // returning Empty. They should also only throw exceptions that
                        // are subclasses of HttpException, so we shouldn't need a
                        // try-catch around them.
                        base.Request._post = PostHandlers[contentType](
                            request:    base.Request,
                            body:       bodyStream
                            );
                    }
                }
            // There are situations where we'll need to exit from the parsing
            // of the request body. To make this easier, we're using a label
            // right at the end that we can jump to.
            readBodyExit: ;
            }

        deserialiseExit:
            // The user shouldn't be able to modify the headers, since they
            // were sent in a request and are concrete. To ensure that the
            // user doesn't, we mark the HttpHeaderCollection read-only.
            base.Request._headers.IsReadOnly = true;
        }

        /// <summary>
        /// Creates a new Http1Deserialiser.
        /// </summary>
        /// <param name="input">The stream containing the request.</param>
        public Http1Deserialiser(Stream input)
            : base(input)
        {

        }

        /// <summary>
        /// Disposes any resources held by the deserialiser.
        /// This method is not implemented.
        /// </summary>
        public override void Dispose()
        {
            return;
        }
    }
}
