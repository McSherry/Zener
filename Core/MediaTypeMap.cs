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

using Path = System.IO.Path;

namespace McSherry.Zener.Core
{
    /// <summary>
    /// The type of parameter being passed to the
    /// MediaTypeMap's Find method.
    /// </summary>
    public enum FindParameterType
    {
        /// <summary>
        /// Indicates that the parameter is only the
        /// file extension.
        /// </summary>
        Extension,
        /// <summary>
        /// Indicates that the parameter is the file's
        /// name or its path.
        /// </summary>
        NameOrPath
    }

    /// <summary>
    /// The delegate used by media type handlers. Media type handlers
    /// are passed data before it is served to the user, and the data
    /// they return is what is served to the user.
    /// 
    /// Media type handlers could, for example, be used to implement
    /// server-side scripting.
    /// </summary>
    /// <param name="data">The data for the handler to transform.</param>
    /// <returns>The transformed data.</returns>
    public delegate byte[] MediaTypeHandler(byte[] data);

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
                    {
                        "image/bmp",
                        new List<string>()
                        {
                            ".bmp", ".dib"
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
        /// <param name="str">The string representing the file to find the media type for.</param>
        /// <param name="type">Indicates what is being passed in the <paramref name="str"/> parameter.</param>
        /// <param name="default">The media type to default to if no match is found.</param>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the value passed in <paramref name="type"/> is
        ///     not recognised or is invalid.
        /// </exception>
        public string Find(
            string str, 
            FindParameterType type = FindParameterType.Extension,
            string @default = FallbackType
            )
        {
            string fileExtension;

            if (type == FindParameterType.Extension)
            {
                fileExtension = str.ToLower().Trim();

                if (str[0] != '.')
                    fileExtension = String.Format(".{0}", str);
            }
            else if (type == FindParameterType.NameOrPath)
            {
                if (!Path.HasExtension(str))
                {
                    return @default;
                }

                fileExtension = Path.GetExtension(str);
            }
            else throw new ArgumentException(
                "Unrecognised enum value.", "type"
                );


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
