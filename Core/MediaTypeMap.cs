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

        static MediaTypeMap()
        {
            _default = new MediaTypeMap()
            #region text/* Media Types
                .Add("text/plain", ".txt")
                .Add("text/css", ".css")
                .Add("text/html", ".htm", ".html")
            #endregion
            #region image/* Media Types
                .Add("image/png", ".png", ".apng")
                .Add("image/tiff", ".tif", ".tiff")
                .Add("image/jpeg", ".jpg", ".jpeg")
                .Add("image/gif", ".gif")
                .Add("image/bmp", ".bmp", ".dib")
            #endregion
            #region video/* Media Types
                .Add("video/mp4", ".mp4", ".m4v")
                .Add("video/avi", ".avi")
            #endregion
            #region application/* Media Types
                .Add("application/javascript", ".js", ".jsonp")
                .Add("application/json", ".json")
                .Add("application/xml", ".xml")
                .Add("application/xhtml+xml", ".xht", ".xhtml")
            #endregion
                ;
        }

        /// <summary>
        /// The fallback media type for use when a matching type isn't found.
        /// </summary>
        public const string FallbackType = "text/plain";
        /// <summary>
        /// The default media type handler.
        /// </summary>
        /// <param name="data">The data for the handler to transform.</param>
        /// <returns>The transformed data.</returns>
        public static byte[] DefaultMediaTypeHandler(byte[] data)
        {
            return data;
        }
        /// <summary>
        /// The default mapping of media types to file extensions.
        /// </summary>
        public static MediaTypeMap Default
        {
            get { return _default; }
        }

        /*****/

        private List<MediaType> _types;
        private List<List<string>> _extensions;
        private List<MediaTypeHandler> _handlers;
        private MediaType _defaultType;

        /// <summary>
        /// Creates a new MediaTypeMap.
        /// </summary>
        public MediaTypeMap()
        {
            _types = new List<MediaType>();
            _extensions = new List<List<string>>();
            _handlers = new List<MediaTypeHandler>();
        }

        /// <summary>
        /// Adds a new media type definition to the map with the
        /// default handler.
        /// </summary>
        /// <param name="mediaType">The media type to add.</param>
        /// <param name="extensions">
        /// The list of file extensions to associated with this media
        /// type and handler.
        /// </param>
        /// <returns>
        /// The MediaTypeMap that the media type was added to. This permits
        /// chaining calls to MediaTypeMap.Add.
        /// </returns>
        public MediaTypeMap Add(
            MediaType mediaType,
            params string[] extensions
            )
        {
            return this.Add(
                mediaType:  mediaType,
                extensions: extensions.ToList()
                );
        }
        /// <summary>
        /// Adds a new media type definition to the map with the
        /// default handler.
        /// </summary>
        /// <param name="mediaType">The media type to add.</param>
        /// <param name="extensions">
        /// The list of file extensions to associated with this media
        /// type and handler.
        /// </param>
        /// <returns>
        /// The MediaTypeMap that the media type was added to. This permits
        /// chaining calls to MediaTypeMap.Add.
        /// </returns>
        public MediaTypeMap Add(
            MediaType mediaType, List<string> extensions
            )
        {
            return this.Add(
                mediaType:  mediaType,
                handler:    MediaTypeMap.DefaultMediaTypeHandler,
                extensions: extensions
                );
        }
        /// <summary>
        /// Adds a new media type definition to the map.
        /// </summary>
        /// <param name="mediaType">The media type to add.</param>
        /// <param name="handler">
        /// The function to use when serving content that uses this
        /// media type. This may be a transformation function (for example,
        /// decompressing a Gzip archive before serving it).
        /// </param>
        /// <param name="extensions">
        /// The list of file extensions to associated with this media
        /// type and handler.
        /// </param>
        /// <returns>
        /// The MediaTypeMap that the media type was added to. This permits
        /// chaining calls to MediaTypeMap.Add.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided MediaType is null, the provided
        /// MediaTypeHandler is null, or when the provided list of
        /// extensions is null or empty.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the MediaTypeMap contains a MediaType identical
        /// to the one passed to the method.
        /// 
        /// Thrown when one or more of the extensions passed in the list
        /// of extensions is already present within the map.
        /// </exception>
        public MediaTypeMap Add(
            MediaType mediaType,
            MediaTypeHandler handler,
            List<string> extensions
            )
        {
            if (mediaType == null)
            {
                throw new ArgumentNullException(
                    "The provided MediaType must not be null."
                    );
            }
            if (handler == null)
            {
                throw new ArgumentNullException(
                    "The provided handler must not be null."
                    );
            }
            if (extensions == null || extensions.Count == 0)
            {
                throw new ArgumentNullException(
                    "The provided list of extensions must not be null or empty."
                    );
            }

            // We can't have exact duplicates within the list
            // of media types.
            if (_types.Any(m => m.Equals(mediaType)))
            {
                throw new ArgumentException(
                    "There is an identical media type already within the map."
                    );
            }

            // Trim the extensions of any whitespace, and remove any
            // leading or trailing periods.
            extensions = extensions
                .Select(s => s.Trim(' ', '.').ToLower())
                .ToList();
            
            // Check that the map does not contain any of the extensions
            // that have been passed to this method.
            if (_extensions.SelectMany(l => l).Any(extensions.Contains))
            {
                throw new ArgumentException(
                    "There is a file extension already within the map."
                    );
            }

            // Add the new media type/etc to the map.
            _types.Add(mediaType);
            _extensions.Add(extensions);
            _handlers.Add(handler);

            return this;
        }

        /// <summary>
        /// Determines the media type to use based on a file
        /// </summary>
        /// <param name="fileExtension"></param>
        /// <param name="fallbackType"></param>
        /// <param name="findType"></param>
        /// <returns></returns>
        public bool TryFind(
            string fileExtension,
            out Tuple<MediaType, MediaTypeHandler> result,
            FindParameterType findType = FindParameterType.Extension
            )
        {
            if (findType == FindParameterType.Extension)
            {
                fileExtension = fileExtension.ToLower().Trim(' ', '.');
            }
            else if (findType == FindParameterType.NameOrPath)
            {
                if (!Path.HasExtension(fileExtension))
                {
                    throw new ArgumentException(
                        "The specified name or path has no file extension."
                        );
                }

                fileExtension = Path.GetExtension(fileExtension).Trim('.');
            }

            var resultIndex = Enumerable
                .Range(0, _types.Count)
                .Zip(_extensions, (i, x) => new { i, x })
                .Where(o => o.x.Contains(fileExtension))
                .Select(o => o.i)
                .DefaultIfEmpty(-1)
                .First();

            // No results.
            if (resultIndex == -1)
            {
                result = null;
                return false;
            }

            // A result!
            result = new Tuple<MediaType, MediaTypeHandler>(
                _types[resultIndex], _handlers[resultIndex]
                );
            return true;
        }
        /// <summary>
        /// Produces a copy of a MediaTypeMap.
        /// </summary>
        public MediaTypeMap Copy()
        {
            return new MediaTypeMap()
            {
                _defaultType = this._defaultType,
                _types = new List<MediaType>(this._types),
                _handlers = new List<MediaTypeHandler>(this._handlers),
                _extensions = new List<List<string>>(this._extensions)
            };
        }
        /// <summary>
        /// The media type to use as a fallback when a call to Find
        /// returns no results.
        /// </summary>
        public MediaType DefaultType
        {
            get { return _defaultType; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(
                        "The default media type cannot be null."
                        );
                }

                _defaultType = value;
            }
        }
    }
}
