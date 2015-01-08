/*
 *      Copyright (c) 2014-2015, SynapLink, LLC
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

namespace SynapLink.Zener.Archives
{
    /// <summary>
    /// Defines the interface for working with archive files.
    /// </summary>
    public abstract class Archive
    {
        /// <summary>
        /// The number of files in the archive.
        /// </summary>
        public abstract int Count { get; }
        /// <summary>
        /// The names of the files in the archive.
        /// </summary>
        public abstract IEnumerable<string> Files { get; }

        /// <summary>
        /// Retrieves a file from the archive based on its name.
        /// </summary>
        /// <param name="name">The name of the file to retrieve.</param>
        /// <returns>An enumerable containing the file's contents.</returns>
        public abstract IEnumerable<byte> GetFile(string name);
        /// <summary>
        /// Retrieves a file from the archive based on its name, and
        /// returns it after converting it to a string with the specified
        /// encoding.
        /// </summary>
        /// <param name="name">The name of the file to retrieve.</param>
        /// <param name="encoding">The encoding to apply to the file.</param>
        /// <returns>A string containing the encoded contents of the file.</returns>
        public virtual string GetFile(string name, Encoding encoding)
        {
            return encoding.GetString(this.GetFile(name).ToArray());
        }
    }
}
