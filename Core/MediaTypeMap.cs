/*
 *      Copyright (c) 2014-2015, SynapLink, LLC
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

namespace SynapLink.Zener.Core
{
    /// <summary>
    /// A class used to map media types to file extensions.
    /// </summary>
    public class MediaTypeMap
    {
        private static MediaTypeMap _default;
        private Dictionary<string, List<string>> _map;

        static MediaTypeMap()
        {
            _default = new MediaTypeMap()
            {
                _map = new Dictionary<string, List<string>>()
                {
                    #region text/* Media Types
                    { 
                        "text/plain",
                        new List<string>() 
                        { 
                            ".txt"
                        } 
                    },
                    {
                        "text/css",
                        new List<string>()
                        {
                            ".css"
                        }
                    },
                    {
                        "text/html",
                        new List<string>()
                        {
                            ".htm", ".html"
                        }
                    },
                    {
                        "text/xml",
                        new List<string>()
                        {
                            ".xml"
                        }
                    },
                    #endregion
                    #region image/* Media Types
                    {
                        "image/png",
                        new List<string>()
                        {
                            ".png", ".apng"
                        }
                    },
                    {
                        "image/tiff",
                        new List<string>()
                        {
                            ".tif", ".tiff"
                        }
                    },
                    {
                        "image/jpeg",
                        new List<string>()
                        {
                            ".jpg", ".jpeg"
                        }
                    },
                    {
                        "image/gif",
                        new List<string>()
                        {
                            ".gif"
                        }
                    },
                    #endregion
                    #region video/* Media Types
                    {
                        "video/mp4",
                        new List<string>()
                        {
                            ".mp4", ".m4v",
                        }
                    },
                    {
                        "video/avi",
                        new List<string>()
                        {
                            ".avi"
                        }
                    },
                    #endregion
                    #region application/* Media Types
                    {
                        "application/javascript",
                        new List<string>()
                        {
                            ".js", ".jsonp"
                        }
                    },
                    {
                        "application/json",
                        new List<string>()
                        {
                            ".json"
                        }
                    },
                    {
                        "application/dart",
                        new List<string>()
                        {
                            ".dart"
                        }
                    },
                    {
                        "application/xhtml+xml",
                        new List<string>()
                        {
                             ".xht", ".xhtml"
                        }
                    }
                    #endregion
                }
            };
        }

        /// <summary>
        /// The default mapping of media types to file extensions.
        /// </summary>
        public static MediaTypeMap Default
        {
            get { return _default; }
        }
        /// <summary>
        /// The fallback media type for use when a matching type isn't found.
        /// </summary>
        public const string FallbackType = "text/plain";

        /// <summary>
        /// Creates a new MediaTypeMap.
        /// </summary>
        public MediaTypeMap()
        {
            _map = new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Associates a media type with file extensions.
        /// </summary>
        public void Add(string mediaType, params string[] fileExtension)
        {
            mediaType = mediaType.ToLower().Trim();

            if (_map.ContainsKey(mediaType))
            {
                var exts = _map[mediaType];

                exts.AddRange(
                    fileExtension
                        .Select(e => e.ToLower().Trim())
                        .Where(e => !exts.Contains(e))
                    );
            }
            else
            {
                _map.Add(mediaType, fileExtension
                    .Select(e => e.ToLower().Trim()).ToList()
                    );
            }
        }
        /// <summary>
        /// Determines the media type for a given file extension.
        /// </summary>
        /// <param name="fileExtension">The file extension to find the media type for.</param>
        public string Find(string fileExtension)
        {
            fileExtension = fileExtension.ToLower().Trim();

            if (fileExtension[0] != '.')
                fileExtension = String.Format(".{0}", fileExtension);

            return _map
                .Where(mt => mt.Value.Contains(fileExtension))
                .Select(mt => mt.Key)
                .DefaultIfEmpty(FallbackType)
                .First();
        }
        /// <summary>
        /// Produces a copy of a MediaTypeMap.
        /// </summary>
        public MediaTypeMap Copy()
        {
            return new MediaTypeMap()
            {
                _map = new Dictionary<string,List<string>>(_map)
            };
        }
    }
}
