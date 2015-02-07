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

namespace McSherry.Zener.Archives
{
    /// <summary>
    /// An abstract class for archives which will only
    /// ever contain a single file.
    /// </summary>
    public abstract class SingleFileArchive
        : Archive, IDisposable
    {
        /// <summary>
        /// The name of the file stored within the archive. If the
        /// archive does not store file names, returns an empty string.
        /// </summary>
        public virtual string Filename
        {
            get { return String.Empty; }
        }
        /// <summary>
        /// The data stored within the archive.
        /// </summary>
        public abstract IEnumerable<byte> Data
        {
            get;
        }

        /// <summary>
        /// The number of files within the archive. Always returns one.
        /// </summary>
        public override int Count
        {
            get { return 1; }
        }
        /// <summary>
        /// The names of all files within the archive. This will
        /// always return a single-item array containing the value
        /// of the SingleFileArchive.Filename property.
        /// </summary>
        public override IEnumerable<string> Files
        {
            get { return new string[1] { this.Filename }; }
        }

        /// <summary>
        /// Retrieves a file's data based on its name. Always returns true,
        /// regardless of the provided name.
        /// </summary>
        /// <param name="name">The file name of the data to retrieve.</param>
        /// <param name="contents">The data associated with the provided filename.</param>
        /// <returns>True if the data was retrieved successfully.</returns>
        public override bool GetFile(string name, out IEnumerable<byte> contents)
        {
            contents = this.Data;

            return true;
        }
    }
}
