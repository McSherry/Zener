/*
 *      Copyright (c) 2014, SynapLink, LLC
 *      All Rights reserved.
 *      
 *      Released under BSD 3-Clause licence. See terms in
 *      /LICENCE.txt, or online: http://opensource.org/licenses/BSD-3-Clause
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SynapLink.Zener.Core
{
    /// <summary>
    /// A class used to cache files in memory.
    /// </summary>
    public class FileSystem
    {
        // Key is the path to the file. Value is the file's content.
        private Dictionary<string, byte[]> _cache;
        private bool _doCache;
        private string _root;

        /// <summary>
        /// Creates a new FileSystem instance with caching enabled using
        /// the provided directory as the root.
        /// </summary>
        /// <param name="root">The directory to use as the root of the filesystem.</param>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        public FileSystem(string root)
            : this(root, true)
        { }
        /// <summary>
        /// Creates a new FileSystem instance using the provided directory
        /// as the root, with caching optionally enabled.
        /// </summary>
        /// <param name="root">The directory to use as the root of the filesystem.</param>
        /// <param name="cache">Whether to cache files that are requested.</param>
        public FileSystem(string root, bool cache)
        {
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException
                ("The specified root directory could not be found.");
            }

            _doCache = cache;
            _root = root;
            if (cache)
            {
                _cache = new Dictionary<string, byte[]>();
            }
        }

        /// <summary>
        /// The directory the file system is using as its root.
        /// </summary>
        public string Root
        {
            get { return _root; }
        }
        /// <summary>
        /// Whether the file system will maintain an in-memory cache
        /// of requested files.
        /// </summary>
        public bool Caching
        {
            get { return _doCache; }
        }

        /// <summary>
        /// Retrieves a file from the file system.
        /// </summary>
        /// <param name="path">The path, relative to the specified root, to find the file at.</param>
        /// <returns>The contents of the file.</returns>
        /// <exception cref="System.IO.IOException"></exception>
        public List<byte> GetFile(string path)
        {
            if (path[0] != '/') path = '/' + path;

            if (this.Caching && _cache.ContainsKey(path))
            {
                return _cache[path].ToList();
            }

            FileStream fs;

            fs = new FileStream(this.Root + path, FileMode.Open);

            byte[] fBytes = new byte[fs.Length];
            fs.Read(fBytes, 0, fBytes.Length);

            if (this.Caching) _cache.Add(path, fBytes);

            fs.Close();
            fs.Dispose();

            return fBytes.ToList();
        }
        /// <summary>
        /// Invalidates the item cached with the specified path,
        /// removing it from the cache.
        /// </summary>
        /// <param name="path">The path to invalidate the cache for.</param>
        public void InvalidateCacheItem(string path)
        {
            if (path[0] != '/') path = '/' + path;

            if (!_cache.ContainsKey(path)) return;

            _cache.Remove(path);
        }
        /// <summary>
        /// Invalidates the cache, removing all items
        /// from it.
        /// </summary>
        public void InvalidateCache()
        {
            _cache.Clear();
        }
    }
}
