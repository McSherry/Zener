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

namespace McSherry.Zener.Core
{
    /// <summary>
    /// A class providing RFC 1952-related functions.
    /// </summary>
    public static class Rfc1952
    {
        #region CRC-32 lookup table
        private static readonly ulong[] CRC_TABLE;
        private static void _GenerateCrc32Lookup(out ulong[] array)
        {
            /* This function adapted from the example code in
             * RFC 1952's appendix 8. This function (_GenerateCrc32Lookup)
             * is released in to the public domain.
             */
            array = new ulong[256];

            ulong c;
            for (int n = 0; n < array.Length; n++)
            {
                c = (ulong)n;

                for (int k = 0; k < 8; k++)
                {
                    if ((c & 1) == 1)
                    {
                        c = 0xEDB88320L ^ (c >> 1);
                    }
                    else
                    {
                        c >>= 1;
                    }
                }

                array[n] = c;
            }
        }
        #endregion

        static Rfc1952()
        {
            _GenerateCrc32Lookup(out CRC_TABLE);
        }
    }
}
