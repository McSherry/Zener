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

namespace McSherry.Zener.Net
{
    /// <summary>
    /// A class providing RFC 3896-related methods.
    /// </summary>
    public static class Rfc3896
    {               
        // Bytes that we won't touch when percent-encoding.
        private static readonly HashSet<byte> NoUrlEncodeBytes
            = new HashSet<byte>(
                Encoding.UTF8.GetBytes(
                    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~"
                ));
        // Characters that are allowed within a percent-encoded
        // string unencoded. These are reserved characters used within
        // URI/URLs as delimiters.
        private static readonly HashSet<byte> UrlEncodeReserved
            = new HashSet<byte>(
                Encoding.UTF8.GetBytes(
                    ":/?#[]@!$&'()*+,;="
                ));
        // Bytes that we'll consider as valid hexadecimal characters.
        private static readonly HashSet<byte> UrlEncodeHexChars
            = new HashSet<byte>(
                Encoding.UTF8.GetBytes(
                    "0123456789ABCDEFabcdef"
                ));

        private const byte
            PctEncodingStart = (byte)'%',
            XFormsSpace = (byte)'+',
            SpaceCharacter = (byte)' '
            ; 

        /// <summary>
        /// Converts the string to a URL-safe encoding using
        /// the percent-encoding scheme.
        /// </summary>
        /// <param name="plain">
        /// The string to convert to a percent-encoded form.
        /// </param>
        /// <param name="xformsSpaces">
        /// Whether to use application/x-www-form-urlencoded
        /// spaces. When this is true, spaces are encoded
        /// using the character '+' instead of the
        /// percent-encoded sequence '%20'.
        /// </param>
        /// <returns>
        /// The percent-encoded representation of the source
        /// string.
        /// </returns>
        /// <remarks>
        /// This method assumes that the provided source string
        /// is a UTF-8 string.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided plain-text source string
        /// is null.
        /// </exception>
        public static string UrlEncode(
            this string plain,
            bool xformsSpaces = false
            )
        {
            if (plain == null)
            {
                throw new ArgumentNullException(
                    "The provided plain-text string must not be null."
                    );
            }

            // As we do in UrlDecode, we convert the string to a
            // series of UTF-8 bytes. This is done because .NET strings
            // are, internally, UTF-16.
            byte[] strBytes = Encoding.UTF8.GetBytes(plain);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < strBytes.Length; i++)
            {
                byte b = strBytes[i];
                // We first check to see whether the byte is in
                // the set of bytes that shouldn't be percent-encoded.
                if (NoUrlEncodeBytes.Contains(b))
                {
                    // If we shouldn't encode the byte, we just
                    // append it without change.
                    sb.Append((char)b);
                }
                // If the byte isn't in the set of bytes that shouldn't
                // be encoded, it means we need to encode it.
                //
                // First, we check to see whether application/x-www-form-urlencoded
                // space encoding is enabled, and whether the current byte is a space.
                else if (xformsSpaces && b == SpaceCharacter)
                {
                    // If we're using application/x-www-form-urlencoded space encoding
                    // and the character to encode is a space, append a '+' instead of
                    // '%20' to the StringBuilder.
                    sb.Append((char)XFormsSpace);
                }
                // If it isn't, we need to percent-encode the byte. Percent encoding
                // is very simple. You take the value of the byte as two hexadecimal
                // digits and append them (upper- or lowercase, it doesn't matter),
                // prefixed by a percent sign (%), to the string.
                //
                // For example, the ASCII space character has the decimal value 32. In
                // hexadecimal, this is 20. To encode an ASCII space, we append %20 to
                // the string. Leading zeroes are always included (so the hexadecimal
                // value 5 is always encoded %05, never %5).
                else
                {
                    sb.AppendFormat(
                        "%{0}{1}",
                        // The ToString method passed "X" prints out the value
                        // in hexadecimal. However, it does not return leading
                        // zeroes. To get around this, we just take the high and
                        // low nybbles and call ToString on them separately.
                        ((b & 0xF0) >> 4).ToString("X"),
                        (b & 0xF).ToString("X")
                        );
                }
            }

            return sb.ToString();
        }
        /// <summary>
        /// Converts a percent-encoded (URL-encoded) string in
        /// to a non-URL-safe string.
        /// </summary>
        /// <param name="encoded">
        /// The string to convert from percent-encoded form.
        /// </param>
        /// <param name="xformsSpaces">
        /// Whether to support spaces used with the
        /// application/x-www-form-urlencoded format. When this
        /// is true, any plus (+) characters are decoded to
        /// spaces.
        /// </param>
        /// <param name="strict">
        /// Whether the method should throw an exception
        /// when a non-URL-safe character is present within
        /// the encoded string.
        /// </param>
        /// <returns>
        /// The decoded representation of the provided percent-encoded
        /// string.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided source string is null.
        /// </exception>
        /// <exception cref="System.FormatException">
        /// Thrown when the format of the percent-encoded string is
        /// invalid.
        /// </exception>
        public static string UrlDecode(
            this string encoded,
            bool xformsSpaces = false,
            bool strict = true
            )
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(
                    "The provided source string must not be null."
                    );
            }

            // If the string is whitespace/empty, there's nothing
            // we can decode, so we might as well not try.
            if (String.IsNullOrWhiteSpace(encoded))
            {
                return encoded;
            }

            // We're treating it as UTF-8. Since .NET uses UTF-16 internally,
            // we need to work with the string as a set of bytes.
            var strBytes = Encoding.UTF8.GetBytes(encoded);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < strBytes.Length; i++)
            {
                byte b = strBytes[i];
                // If the byte is the byte that indicates the start of a
                // percent-encoded byte, we need to decode it.
                if (b == PctEncodingStart)
                {
                    #region Handle Pct-Enc'd bytes (%hh)
                    // We need to make sure that there enough
                    // characters to make up a percent-encoded byte.
                    if (i + 2 < strBytes.Length)
                    {
                        byte hNybble = strBytes[i + 1],
                            lNybble = strBytes[i + 2];

                        // We need to check to make sure that both of
                        // the bytes following the percent sign are
                        // valid hex digits.
                        if (
                            UrlEncodeHexChars.Contains(hNybble) &&
                            UrlEncodeHexChars.Contains(lNybble)
                            )
                        {
                            // We need to join together the higher and lower
                            // nybbles to form a single byte that will be our
                            // character.
                            sb.Append(
                                (char)(
                                // Convert the nybble to a character. This gives us the
                                // hex digit to convert to a byte. We then shift this byte
                                // 4 bits left, as the single hex digit represents the
                                // four higher bits of the byte.
                                    (Convert.ToByte(((char)hNybble).ToString(), 16) << 4) |
                                    Convert.ToByte(((char)lNybble).ToString(), 16)
                                ));
                            // Advance past the two characters we've just interpreted
                            // as a percent-encoded byte.
                            i += 2;
                        }
                        // If strict parsing is enabled and the characters are not
                        // valid hex digits, we'll throw an exception.
                        else if (strict)
                        {
                            throw new FormatException(
                                "A percent-encoded byte within the string contains " +
                                "non-hexadecimal characters."
                                );
                        }
                        // If strict parsing is disabled, we append the percent character
                        // to the string, treating it as a literal.
                        else
                        {
                            sb.Append((char)b);
                        }
                    }
                    // If we get here, there are too few characters to form a
                    // percent-encoded byte. There are two things we can do.
                    //
                    // 1: If strict parsing is enabled, we can throw an exception
                    //    to indicate that we've come across an invalid sequence.
                    else if (strict)
                    {
                        throw new FormatException(
                            "The string contains a percent character in a location " +
                            "where it cannot prefix two hexadecimal digits."
                            );
                    }
                    // 2: If strict parsing is disabled, append the character to
                    //    the StringBuilder and treat it as a literal character.
                    else
                    {
                        sb.Append((char)b);
                    }
                    #endregion
                }
                // The media type application/x-www-form-urlencoded specifies that
                // the '+' character may be used instead of %20 for spaces. If the
                // caller has enabled support for this in the call, and if the current
                // byte is a '+' character, we'll append a space to the StringBuilder.
                else if (xformsSpaces && b == XFormsSpace)
                {
                    sb.Append(' ');
                }
                // If the character isn't a percent character, we need to
                // determine whether it's one that we're free to add to the
                // StringBuilder.
                //
                // If it is a character we're free to add, or it isn't a
                // character we're free to add but strict parsing is disabled,
                // we can add it to the StringBuilder.
                else if (
                    NoUrlEncodeBytes.Contains(b) ||
                    UrlEncodeReserved.Contains(b) ||
                    !strict
                    )
                {
                    sb.Append((char)b);
                }
                // If the character is non-URL-safe and strict parsing is
                // enabled, throw an exception.
                else
                {
                    throw new FormatException(
                        "The provided percent-encoded string contains " +
                        "one or more non-URL-safe characters."
                        );
                }
            }

            return sb.ToString();
        }
    }
}
