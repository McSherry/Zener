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
using System.Threading.Tasks;
using System.IO;

namespace McSherry.Zener
{
    /// <summary>
    /// Provides a set of methods that can ease Zener's use,
    /// and that are used within Zener itself.
    /// </summary>
    public static class Zener
    {
        /// <summary>
        /// This version string is used both by the Version static property
        /// (which is used in the 'Server' header) and in setting the version
        /// string for the assembly.
        /// </summary>
        internal const string VersionString = "0.13.0";

        static Zener()
        {
            Version = new Version(VersionString);
        }

        /// <summary>
        /// The current version of Zener.
        /// </summary>
        public static Version Version
        {
            get;
            private set;
        }

        /// <summary>
        /// Reads a single line from a stream and returns the ASCII-encoded string.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>A single line from the stream, ASCII-encoded.</returns>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the provided stream cannot be read from.
        /// </exception>
        public static string ReadAsciiLine(this Stream stream)
        {
            if (!stream.CanRead)
                throw new ArgumentException
                ("Provided stream cannot be read from.");

            List<byte> buf = new List<byte>();
            ReadUntilFound(stream, "\r\n", Encoding.ASCII, buf.Add);

            return Encoding.ASCII.GetString(buf.ToArray());
        }
        /// <summary>
        /// Reads bytes until the specified boundary is found.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="boundary">The boundary to read to.</param>
        /// <param name="encoding">The encoding of the data and boundary.</param>
        /// <param name="readCall">The callback that is passed each byte as it is read.</param>
        public static void ReadUntilFound(
            this Stream stream,
            string boundary, Encoding encoding,
            Action<byte> readCall
            )
        {
            byte[] boundaryBytes = encoding.GetBytes(boundary);

            stream.ReadUntilFound(boundaryBytes, readCall);
        }
        /// <summary>
        /// Reads bytes until the specified boundary is found.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="boundary">The boundary to read to.</param>
        /// <param name="readCall">The callback that is passed each byte as it is read.</param>
        public static void ReadUntilFound(
            this Stream stream,
            byte boundary, Action<byte> readCall
            )
        {
            stream.ReadUntilFound(new[] { boundary }, readCall);
        }
        /// <summary>
        /// Reads bytes until the specified boundary is found.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="boundary">The boundary to read to.</param>
        /// <param name="readCall">The callback that is passed each byte as it is read.</param>
        public static void ReadUntilFound(
            this Stream stream,
            byte[] boundary, Action<byte> readCall
            )
        {
            byte[] window = new byte[boundary.Length];

            //if (stream.Length - stream.Position < boundaryBytes.Length)
            //    throw new InvalidOperationException
            //    ("Too few bytes left in stream to find boundary.");

            stream.Read(window, 0, window.Length);

            while (true)
            {
                // We've reached the boundary!
                if (window.SequenceEqual(boundary)) break;

                int next = stream.ReadByte();
                // Looks like we've hit the end of the stream.
                // Nothing more to read.
                if (next == -1) break;

                // Return the byte we're about to discard so
                // it can be used by the caller.
                readCall(window[0]);
                // Shift the window ahead by a single byte.
                Buffer.BlockCopy(window, 1, window, 0, window.Length - 1);
                window[window.Length - 1] = (byte)next;
            }
        }

        /// <summary>
        /// Calculates the Levenshtein distance between the
        /// two provided sequences.
        /// </summary>
        /// <typeparam name="T">The type of value each sequence contains.</typeparam>
        /// <param name="lhs">
        /// The sequence used as the reference. The distance calculated is
        /// the distance from this sequence.
        /// </param>
        /// <param name="rhs">
        /// The sequence to compare against the reference.
        /// </param>
        /// <returns>
        /// An integer, the value of which is the Levenshtein distance between
        /// the sequences.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when one or more of the provided sequences is null.
        /// </exception>
        public static int Levenshtein<T>(
            this IEnumerable<T> lhs,
            IEnumerable<T> rhs
            )
        {
            if (lhs == null || rhs == null)
            {
                throw new ArgumentNullException(
                    "Neither of the provided sequences can be null."
                    );
            }

            // The current count of Levenshtein distance.
            int count = 0;
            // Whether the lhs param is longer than rhs.
            bool lhsLongest;

            // We can make this a bit faster depending on what
            // type of sequence we've been passed.
            if (lhs is string)
            {
                string  slhs = lhs as string,
                        srhs = rhs as string;

                lhsLongest = slhs.Length > srhs.Length;
                // If one of the strings is longer than the other,
                // the distance is going to be, minimally, the difference
                // in length of the two strings.
                count += lhsLongest
                    ? slhs.Length - srhs.Length
                    : srhs.Length - slhs.Length
                    ;
            }
            else if (lhs is T[])
            {
                T[] alhs = (T[])lhs,
                    arhs = (T[])rhs;

                lhsLongest = alhs.Length > arhs.Length;

                count += lhsLongest
                    ? alhs.Length - arhs.Length
                    : arhs.Length - alhs.Length
                    ;
            }
            else if (lhs is IList<T>)
            {
                IList<T>    llhs = (IList<T>)lhs,
                            lrhs = (IList<T>)rhs;

                lhsLongest = llhs.Count > lrhs.Count;

                count += lhsLongest
                    ? llhs.Count - lrhs.Count
                    : lrhs.Count - llhs.Count
                    ;
            }
            else
            {
                int lhsCount = lhs.Count(),
                    rhsCount = rhs.Count();

                lhsLongest = lhsCount > rhsCount;

                count += lhsLongest
                    ? lhsCount - rhsCount
                    : rhsCount - lhsCount
                    ;
            }

            // We need to iterate over the shortest sequence. This
            // is because we've already taken in to account the "extra"
            // characters at the end of the longest sequence.
            IEnumerator<T> shortest, longest;
            if (lhsLongest)
            {
                shortest    = rhs.GetEnumerator();
                longest     = lhs.GetEnumerator();
            }
            else
            {
                shortest    = lhs.GetEnumerator();
                longest     = rhs.GetEnumerator();
            }

            // We need to increment both at the same time, as we're
            // going to be comparing them.
            while (shortest.MoveNext() && longest.MoveNext())
            {
                // If the items at the same position are not equal, then
                // we can count that as a required modification and increment
                // the variable tracking the Levenshtein distance.
                if (!shortest.Current.Equals(longest.Current))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
