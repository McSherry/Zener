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

        private int _getMediaTypeIndex(MediaType mt)
        {
            return Enumerable
                .Range(0, _types.Count)
                .Zip(_types, (i, t) => new { i, t })
                // We want the MediaTypes with the most parameters to
                // be checked first. This allows the user to specify
                // a different handler for MediaTypes with specific
                // parameter values. For example, these three MediaTypes
                // could have three different handlers:
                //
                //      application/example; version=0
                //      application/example; version=1
                //      application/example
                //
                // Most users probably won't use this, but it's nice to
                // have it.
                .OrderByDescending(o => o.t.Parameters.Count)
                .Where(o => mt.IsEquivalent(o.t))
                .Select(o => o.i)
                .DefaultIfEmpty(-1)
                .First();
        }

        /// <summary>
        /// Creates a new MediaTypeMap.
        /// </summary>
        public MediaTypeMap()
        {
            _types = new List<MediaType>();
            _extensions = new List<List<string>>();
            _handlers = new List<MediaTypeHandler>();

            this.DefaultType = MediaType.PlainText;
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
                handler:    MediaTypeMap.DefaultMediaTypeHandler,
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
            params string[] extensions
            )
        {
            return this.Add(
                mediaType:  mediaType,
                handler:    handler,
                extensions: extensions.ToList()
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
        /// Changes the handler associated with a media type.
        /// </summary>
        /// <param name="mediaType">
        /// The MediaType to change the handler for.
        /// </param>
        /// <param name="handler">
        /// The new handler to associate with the MediaType.
        /// </param>
        /// <returns>
        /// The MediaTypeMap the change was made to. This allows
        /// chaining calls to the method.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided MediaType or MediaTypeHandler
        /// is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the specified MediaType does not exist within
        /// the map.
        /// </exception>
        public MediaTypeMap ChangeHandler(
            MediaType mediaType, MediaTypeHandler handler
            )
        {
            if (mediaType == null)
            {
                throw new ArgumentNullException(
                    "The specified media type must not be null."
                    );
            }

            if (handler == null)
            {
                throw new ArgumentNullException(
                    "The specified handler must not be null."
                    );
            }

            int resIndex = _getMediaTypeIndex(mediaType);

            if (resIndex == -1)
            {
                throw new ArgumentException(
                    "The specified media type does not exist within the map."
                    );
            }

            _handlers[resIndex] = handler;

            return this;
        }

        /// <summary>
        /// Gets a reference to the list of extensions associated
        /// with the specified MediaType.
        /// </summary>
        /// <param name="mediaType">
        /// The MediaType to retrieve the associated extensions
        /// for.
        /// </param>
        /// <returns>
        /// A reference to the list of extensions associated with
        /// the provided MediaType.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the provided MediaType is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the provided MediaType does not exist
        /// within the map.
        /// </exception>
        public IList<string> GetExtensions(MediaType mediaType)
        {
            if (mediaType == null)
            {
                throw new ArgumentNullException(
                    "The specified media type must not be null."
                    );
            }

            int resIndex = _getMediaTypeIndex(mediaType);

            if (resIndex == -1)
            {
                throw new ArgumentException(
                    "The specified media type does not exist within the map."
                    );
            }

            return _extensions[resIndex];
        }

        /// <summary>
        /// Finds the handler and extensions for a MediaType.
        /// </summary>
        /// <param name="mediaType">
        /// The MediaType to find the handler and extensions for.
        /// </param>
        /// <param name="handler">
        /// The handler and extensions associated with the MediaType.
        /// </param>
        /// <returns>
        /// True if a result was found.
        /// </returns>
        public bool TryFindHandler(
            MediaType mediaType, out MediaTypeHandler handler
            )
        {
            var resIndex = _getMediaTypeIndex(mediaType);

            bool found;
            if ((found = resIndex > -1))
            {
                handler = _handlers[resIndex];
            }
            else
            {
                handler = null;
            }

            return found;
        }
        /// <summary>
        /// Determines the media type to use based on a file extension,
        /// file path, or file name.
        /// </summary>
        /// <param name="fileExtension">
        /// The file extension, file path, or file name to determine the
        /// media type from.
        /// </param>
        /// <param name="result">
        /// The result, which is the MediaType associated with the file
        /// extension/file path/file name and a handler for transforming
        /// content in that media type's format in to a format that can
        /// be served.
        /// </param>
        /// <param name="findType">
        /// Specifies what has been passed in the <paramref name="fileExtension"/>
        /// parameter; whether the parameter is a file extension on its own, or a
        /// file path/file name.
        /// </param>
        /// <returns>
        /// True if a result was found.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when the <paramref name="fileExtension"/> parameter is null.
        /// </exception>
        public bool TryFindMediaType(
            string fileExtension,
            out Tuple<MediaType, MediaTypeHandler> result,
            FindParameterType findType = FindParameterType.Extension
            )
        {
            if (fileExtension == null)
            {
                throw new ArgumentNullException(
                    "The provided file extension/path/name must not be null."
                    );
            }

            if (findType == FindParameterType.Extension)
            {
                fileExtension = fileExtension.ToLower().Trim(' ', '.');
            }
            else if (findType == FindParameterType.NameOrPath)
            {
                if (!Path.HasExtension(fileExtension))
                {
                    // If there's no extension, we can't attempt to determine
                    // the media type. By returning false instead of throwing
                    // an exception, the Routing.FindMediaType method can return
                    // the default media type.
                    result = null;
                    return false;
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
