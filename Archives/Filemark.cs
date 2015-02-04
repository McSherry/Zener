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

namespace McSherry.Zener.Archives
{
    /// <summary>
    /// Stores the location of an extracted file within a
    /// sequence of bytes.
    /// </summary>
    public struct Filemark
    {
        private readonly long _length, _offset;

        /// <summary>
        /// Creates a new filemark.
        /// </summary>
        /// <param name="length">The length of the marked file.</param>
        /// <param name="offset">The offset of the marked file.</param>
        public Filemark(long length, long offset)
        {
            _length = length;
            _offset = offset;
        }

        /// <summary>
        /// The length of the file, in bytes.
        /// </summary>
        public long Length
        {
            get { return _length; }
        }
        /// <summary>
        /// The file's offset within the byte sequence.
        /// </summary>
        public long Offset
        {
            get { return _offset; }
        }
    }
}
