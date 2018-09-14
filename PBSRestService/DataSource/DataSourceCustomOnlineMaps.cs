//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml.Linq;
using PBS.Util;
using System.Data.SQLite;
using PBS.Service;
using System.Diagnostics;

namespace PBS.DataSource
{
    public class DataSourceCustomOnlineMaps:DataSourceBase,IFormatConverter
    {
        private string _mapName;
        private string _baseUrl;
        private string[] _subDomains;
        private static string CONFIG_FILE_NAME;
        private static List<CustomOnlineMap> _customOnlineMaps;
        public static List<CustomOnlineMap> CustomOnlineMaps
        {
            get
            {
                if (_customOnlineMaps == null)
                    ReadOnlineMapsConfigFile();
                return _customOnlineMaps;
            }
        }
        /// <summary>
        /// Read custom online maps conifgs from xml file.
        /// </summary>
        private static void ReadOnlineMapsConfigFile()
        {            
            try
            {
                CONFIG_FILE_NAME = AppDomain.CurrentDomain.BaseDirectory + "CustomOnlineMaps.xml";
                _customOnlineMaps = new List<CustomOnlineMap>();
                if (!File.Exists(CONFIG_FILE_NAME))
                {
                    throw new FileNotFoundException(CONFIG_FILE_NAME + " does not exist!");
                }
                XDocument xDoc = XDocument.Load(CONFIG_FILE_NAME);
                var maps = from map in xDoc.Descendants("onlinemapsources").Elements()
                           select new
                           {
                               Name = map.Element("name"),
                               Url = map.Element("url"),
                               Servers = map.Element("servers")
                           };
                foreach (var map in maps)
                {
                    _customOnlineMaps.Add(new CustomOnlineMap()
                    {
                        Name = map.Name.Value,
                        Servers = map.Servers.Value.Split(new char[] { ',' }),
                        Url = map.Url.Value
                    });
                }
            }
            catch (Exception e)
            {
                throw new Exception("Could not parse" + CONFIG_FILE_NAME + " file!\r\n" + e.Message);
            }
        }

        /// <summary>
        /// when isForConvert==true, gettile() method will return null instead of returning an error image byte[]
        /// </summary>
        /// <param name="name"></param>
        public DataSourceCustomOnlineMaps(string name)
        {
            _mapName = name;
            Initialize("N/A");
            if (ConfigManager.App_AllowFileCacheOfOnlineMaps)
            {
                //init local cache file if does not exist.
                string localCacheFileName = ConfigManager.App_FileCachePath + "\\" + _mapName.Trim().ToLower() + ".cache";
                ValidateLocalCacheFile(localCacheFileName);
                TileLoaded += new EventHandler<TileLoadEventArgs>(InternalOnTileLoaded);
            }
            ConvertingStatus = new ConvertStatus();
        }

        //public DataSourceCustomOnlineMaps(string name)
        //    : this(name, false)<param name="isForConvert">indicate this datasource is for format convert purpose, not for PBS service purpose. when this is true, gettile() method will return null instead of returning an error image byte[]</param>
        //{

        //}

        protected override void Initialize(string path)
        {
            this.Type = _mapName;
            base.Initialize(path);

            CustomOnlineMap map = CustomOnlineMaps.Where(m => m.Name == _mapName).ToList()[0];
            _baseUrl = map.Url.Replace("{$s}", "{0}").Replace("{$x}", "{2}").Replace("{$y}", "{3}").Replace("{$z}", "{1}");
            _subDomains = map.Servers;
            //for bing maps imagery, add bing key if has one
            if (_mapName.ToLower().Contains("bing") && _mapName.ToLower().Contains("image") && !string.IsNullOrEmpty(ConfigManager.App_BingMapsAppKey))
                _baseUrl += "&token=" + ConfigManager.App_BingMapsAppKey;
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            ReadGoogleMapsTilingScheme(out tilingScheme);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);

            if (_mapName.ToLower().Contains("image"))
                this.TilingScheme.CacheTileFormat = ImageFormat.JPG;
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            string baseUrl = _baseUrl;
            //for bing maps, using quadkey
            if (_mapName.ToLower().Contains("bing"))
            {
                baseUrl = baseUrl.Replace("{$q}", Utility.TileXYToQuadKey(col, row, level));
            }
            string subdomain = string.Empty;
            string uri = string.Empty;
            subdomain = _subDomains[(level + col + row) % _subDomains.Length];
            byte[] bytes;
            try
            {
                uri = string.Format(baseUrl, subdomain, level, col, row);
                if (!ConvertingStatus.IsInProgress)//accessing from PBSServiceProvider by PBS service client
                {                    
                    bytes = HttpGetTileBytes(uri);
                    return bytes;
                }
                else
                //accessing from DataSource directly when do format converting
                //Because when convert to MBTiles, bytes are retriving from datasource directly other than from PBSService, so TileLoaded event and local file cache checking in PBSServiceProvider.cs will not fire. Need to check if local file cache exist first, if not, fire TileLoaded event to let internal function to save tile to local file cache.
                {
                    TileLoadEventArgs tileLEA = new TileLoadEventArgs()
                    {
                        Level = level,
                        Row = row,
                        Column = col
                    };
                    //check if tile exist in local file cache
                    bytes = GetTileBytesFromLocalCache(level, row, col);
                    if (bytes != null)
                    {
                        tileLEA.GeneratedMethod = TileGeneratedSource.FromFileCache;
                    }
                    else
                    {
                        bytes = HttpGetTileBytes(uri);
                        tileLEA.GeneratedMethod = TileGeneratedSource.DynamicOutput;
                    }
                    tileLEA.TileBytes = bytes;
                    if (TileLoaded != null)
                        TileLoaded(this, tileLEA);
                }
                return bytes;
            }
            catch (Exception e)
            {                
                //when this datasource is using for converting online tiles to offline format, return null if there is something wrong with downloading, otherwise, return a error image for PBS service to display.
                if (ConvertingStatus.IsInProgress)
                    return null;
                //if server has response(not a downloading error) and tell pbs do not have the specific tile, return null
                if (e is WebException && (e as WebException).Response != null && ((e as WebException).Response as HttpWebResponse).StatusCode == HttpStatusCode.NotFound)
                    return null;

                string suffix = this.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
                Stream stream = this.GetType().Assembly.GetManifestResourceStream("PBS.Assets.badrequest" + this.TilingScheme.TileCols + "." + suffix);
                bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }

        #region IFormatConverter        
        /// <summary>
        /// not implemented, using public void ConvertToMBTiles(string outputPath, string name, string description, string attribution, int[] levels, Envelope extent) instead.
        /// </summary>
        /// <param name="outputPath">The output path and file name of .mbtiles file.</param>
        /// <param name="name">The plain-english name of the tileset, required by MBTiles.</param>
        /// <param name="description">A description of the tiles as plain text., required by MBTiles.</param>
        /// <param name="attribution">An attribution string, which explains in English (and HTML) the sources of data and/or style for the map., required by MBTiles.</param>
        /// <param name="doCompact">implementing the reducing redundant tile bytes part of MBTiles specification?</param>
        public void ConvertToMBTiles(string outputPath, string name, string description, string attribution,bool doCompact)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Convert to MBTiles format.
        /// </summary>
        /// <param name="outputPath">The output path and file name of .mbtiles file.</param>
        /// <param name="name">The plain-english name of the tileset, required by MBTiles.</param>
        /// <param name="description">A description of the tiles as plain text., required by MBTiles.</param>
        /// <param name="attribution">An attribution string, which explains in English (and HTML) the sources of data and/or style for the map., required by MBTiles.</param>
        /// <param name="levels">tiles in which levels to convert to mbtiles.</param>
        /// <param name="geometry">convert/download extent or polygon, sr=3857. If this is Envelope, download by rectangle, if this is polygon, download by polygon's shape.</param>
        /// <param name="doCompact">implementing the reducing redundant tile bytes part of MBTiles specification?</param>
        public void ConvertToMBTiles(string outputPath, string name, string description, string attribution, int[] levels, Geometry geometry,bool doCompact)
        {
            try
            {
                DoConvertToMBTiles(outputPath, name, description, attribution, levels, geometry,doCompact);
                if (ConvertCancelled != null && ConvertingStatus.IsCancelled)
                {
                    ConvertCancelled(this, new EventArgs());
                }
                if (ConvertCompleted != null)
                    ConvertCompleted(this, new ConvertEventArgs(ConvertingStatus.IsCompletedSuccessfully));
            }
            catch (Exception e)
            {
                throw new Exception("Online maps converting to MBTiles error!\r\n" + e.Message);
            }
        }
        /// <summary>
        /// Cancel any pending converting progress, and fire the ConvertCancelled event  when cancelled successfully.
        /// </summary>
        public void CancelConverting()
        {
            CancelDoConvertToMBTiles();
        }
        /// <summary>
        /// Fire when converting completed.
        /// </summary>
        public event ConvertEventHandler ConvertCompleted;
        /// <summary>
        /// Fire when converting cancelled gracefully.
        /// </summary>
        public event EventHandler ConvertCancelled;        
        #endregion
    }

    public class CustomOnlineMap
    {
        public string Name { get; set; }
        public string[] Servers { get; set; }
        public string Url { get; set; }
    }
}
