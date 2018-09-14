//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.Xml.Linq;
using System.IO;
using PBS.Util;
using System.Collections;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Collections.Concurrent;

namespace PBS.DataSource
{
    public abstract class DataSourceBase
    {
        ~DataSourceBase()
        {
            if (_connLocalCacheFile != null)
                _connLocalCacheFile.Close();
            _connLocalCacheFile = null;
        }
        /// <summary>
        /// including predefined datasourcetype and custom online maps.
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// data source path
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// to store some metadata about service
        /// </summary>
        public TilingScheme TilingScheme { get; set; }
        /// <summary>
        /// Indicate if this datasource is Online Map.
        /// several methods which heavily frequent load depends on this property.
        /// </summary>
        public bool IsOnlineMap { get; set; }

        /// <summary>
        /// TODO: Initialize properties of DataSourceBase && generate tiling scheme
        /// </summary>
        protected virtual void Initialize(string path)
        {
            //TODO:set type
            this.Path = path;
            TilingScheme ts;
            try
            {
                ReadTilingScheme(out ts);
            }
            catch (Exception e)
            {
                throw new Exception("Reading tiling shceme failed!\r\n" + e.Message+"\r\n"+e.StackTrace);
            }
            TilingScheme = ts;
            IsOnlineMap = IsOnlineMaps(Type);
        }

        /// <summary>
        /// TODO: Read the tiling scheme correspond to the data source type
        /// </summary>
        /// <param name="tilingScheme"></param>
        /// <param name="lodsJson"></param>
        protected abstract void ReadTilingScheme(out TilingScheme tilingScheme);

        /// <summary>
        /// Generate json response string
        /// </summary>
        /// <param name="tilingScheme"></param>
        /// <returns></returns>
        protected TilingScheme TilingSchemePostProcess(TilingScheme tilingScheme)
        {
            #region ArcGIS REST Service Info
            string pjson = @"{
  ""currentVersion"" : 10.01, 
  ""serviceDescription"" : ""This service is populated from PortableBasemapServer by diligentpig. For more information goto http://newnaw.com"", 
  ""mapName"" : ""Layers"", 
  ""description"" : ""none"", 
  ""copyrightText"" : ""PBS by diligentpig, REST API by Esri"", 
  ""layers"" : [
    {
      ""id"" : 0, 
      ""name"" : ""YourServiceNameHere"", 
      ""parentLayerId"" : -1, 
      ""defaultVisibility"" : true, 
      ""subLayerIds"" : null, 
      ""minScale"" : 0, 
      ""maxScale"" : 0
    }
  ], 
  ""tables"" : [
    
  ], 
  ""spatialReference"" : {
    ""wkid"" : " + tilingScheme.WKID + @"
  }, 
  ""singleFusedMapCache"" : true, 
  ""tileInfo"" : {
    ""rows"" : " + tilingScheme.TileRows + @", 
    ""cols"" : " + tilingScheme.TileCols + @", 
    ""dpi"" : " + tilingScheme.DPI + @", 
    ""format"" : """ + tilingScheme.CacheTileFormat + @""", 
    ""compressionQuality"" : " + tilingScheme.CompressionQuality + @",
    ""origin"" : {
      ""x"" : " + tilingScheme.TileOrigin.X + @", 
      ""y"" : " + tilingScheme.TileOrigin.Y + @"
    }, 
    ""spatialReference"" : {
      ""wkid"" : " + tilingScheme.WKID + @"
    }, 
    ""lods"" : [" + tilingScheme.LODsJson + @"
    ]
  }, 
  ""initialExtent"" : {
    ""xmin"" : " + tilingScheme.InitialExtent.XMin + @", 
    ""ymin"" : " + tilingScheme.InitialExtent.YMin + @", 
    ""xmax"" : " + tilingScheme.InitialExtent.XMax + @", 
    ""ymax"" : " + tilingScheme.InitialExtent.YMax + @", 
    ""spatialReference"" : {
      ""wkid"" : " + tilingScheme.WKID + @"
    }
  }, 
  ""fullExtent"" : {
    ""xmin"" : " + tilingScheme.FullExtent.XMin + @", 
    ""ymin"" : " + tilingScheme.FullExtent.YMin + @", 
    ""xmax"" : " + tilingScheme.FullExtent.XMax + @", 
    ""ymax"" : " + tilingScheme.FullExtent.YMax + @", 
    ""spatialReference"" : {
      ""wkid"" : " + tilingScheme.WKID + @"
    }
  }, 
  ""units"" : ""esriMeters"", 
  ""supportedImageFormatTypes"" : ""PNG24,PNG,JPG,DIB,TIFF,EMF,PS,PDF,GIF,SVG,SVGZ,AI,BMP"", 
  ""documentInfo"" : {
    ""Title"" : ""none"", 
    ""Author"" : ""none"", 
    ""Comments"" : ""none"", 
    ""Subject"" : ""none"", 
    ""Category"" : ""none"", 
    ""Keywords"" : ""none"", 
    ""Credits"" : ""diligentpig""
  }, 
  ""capabilities"" : ""Map,Query,Data""
}
";
            #endregion
            tilingScheme.RestResponseArcGISPJson = pjson;
            tilingScheme.RestResponseArcGISJson = pjson.Replace("\r\n", "").Replace("\n", "");
            return tilingScheme;
        }

        /// <summary>
        /// TODO: Return the tile image in byte[] indexed by level/row/col, dynamically from PBS.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public abstract byte[] GetTileBytes(int level, int row, int col);
        /// <summary>
        /// Occurs when tile byte[] loaded.
        /// </summary>
        public EventHandler<TileLoadEventArgs> TileLoaded;

        #region read tiling scheme
        protected void ReadSqliteTilingScheme(out TilingScheme tilingScheme, SQLiteConnection sqlConn)
        {
            tilingScheme = new TilingScheme();
            StringBuilder sb;
            #region read MBTile tiling scheme
            tilingScheme.Path = "N/A";
            bool isMACFile = false;
            //check if info table exists, MAC has this table while MBTile not.
            using (SQLiteCommand cmd = new SQLiteCommand(sqlConn))
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='info'";
                long i = (long)cmd.ExecuteScalar();
                if (i == 0)
                    isMACFile = false;
                else
                    isMACFile = true;
                //query for tile image format in MBTiles metadata table
                if (!isMACFile)
                {
                    //format
                    cmd.CommandText = string.Format("SELECT value FROM metadata WHERE name='format'");
                    object o = cmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                    if (o != null)
                    {
                        string f = o.ToString();
                        tilingScheme.CacheTileFormat = f.ToUpper().Contains("PNG") ? ImageFormat.PNG : ImageFormat.JPG;
                    }
                    else
                    {
                        tilingScheme.CacheTileFormat = ImageFormat.JPG;
                    }
                }
            }
            tilingScheme.CompressionQuality = 75;
            tilingScheme.DPI = 96;
            //LODs
            tilingScheme.LODs = new LODInfo[20];
            const double cornerCoordinate = 20037508.342787;
            double resolution = cornerCoordinate * 2 / 256;
            double scale = 591657527.591555;
            for (int i = 0; i < tilingScheme.LODs.Length; i++)
            {
                tilingScheme.LODs[i] = new LODInfo()
                {
                    Resolution = resolution,
                    LevelID = i,
                    Scale = scale
                };
                resolution /= 2;
                scale /= 2;
            }
            //create json
            sb = new StringBuilder("\r\n");
            //{"level" : 0, "resolution" : 156543.033928, "scale" : 591657527.591555}, 
            foreach (LODInfo lod in tilingScheme.LODs)
            {
                sb.Append(@"      {""level"":" + lod.LevelID + "," + @"""resolution"":" + lod.Resolution + "," + @"""scale"":" + lod.Scale + @"}," + "\r\n");
            }
            tilingScheme.LODsJson = sb.ToString().Remove(sb.ToString().Length - 3);//remove last "," and "\r\n"
            //two extent
            try
            {
                using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlConn))
                {
                    sqlCmd.CommandText = string.Format("SELECT value FROM metadata WHERE name='bounds'");//will raise exception if metadata table not exists
                    object o = sqlCmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                    if (o != null)
                    {
                        string[] bounds = o.ToString().Split(new char[] { ',' });
                        Point leftBottom = new Point(double.Parse(bounds[0]), double.Parse(bounds[1]));
                        Point rightTop = new Point(double.Parse(bounds[2]), double.Parse(bounds[3]));
                        leftBottom = Utility.GeographicToWebMercator(leftBottom);
                        rightTop = Utility.GeographicToWebMercator(rightTop);
                        tilingScheme.InitialExtent = new Envelope(leftBottom.X, leftBottom.Y, rightTop.X, rightTop.Y);
                        tilingScheme.FullExtent = tilingScheme.InitialExtent;
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            }
            catch (Exception)
            {
                tilingScheme.InitialExtent = new Envelope(-cornerCoordinate, -cornerCoordinate, cornerCoordinate, cornerCoordinate);
                tilingScheme.FullExtent = tilingScheme.InitialExtent;
            }
            tilingScheme.PacketSize = 0;
            tilingScheme.StorageFormat = StorageFormat.esriMapCacheStorageModeExploded;
            tilingScheme.TileCols = tilingScheme.TileRows = 256;
            tilingScheme.TileOrigin = new Point(-cornerCoordinate, cornerCoordinate);
            tilingScheme.WKID = 3857;
            tilingScheme.WKT = @"PROJCS[""WGS_1984_Web_Mercator_Auxiliary_Sphere"",GEOGCS[""GCS_WGS_1984"",DATUM[""D_WGS_1984"",SPHEROID[""WGS_1984"",6378137.0,298.257223563]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Mercator_Auxiliary_Sphere""],PARAMETER[""False_Easting"",0.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",0.0],PARAMETER[""Standard_Parallel_1"",0.0],PARAMETER[""Auxiliary_Sphere_Type"",0.0],UNIT[""Meter"",1.0],AUTHORITY[""ESRI"",""3857""]]";
            #endregion
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">the tiling scheme file path, not data source path</param>
        /// <param name="tilingScheme"></param>
        protected void ReadArcGISTilingSchemeFile(string path, out TilingScheme tilingScheme)
        {
            tilingScheme = new TilingScheme();
            StringBuilder sb;
            #region read arcgis cache tiling scheme
            if (!System.IO.File.Exists(path))//when datasourcetype is arcgis cache, path is a directory
            {
                path += "\\conf.xml";
            }
            tilingScheme.Path = path;//record the full tiling scheme file path
            //read conf.xml
            XElement confXml = XElement.Load(System.IO.Path.GetDirectoryName(path) + @"\\conf.xml");
            tilingScheme.WKT = confXml.Element("TileCacheInfo").Element("SpatialReference").Element("WKT").Value;
            if (confXml.Element("TileCacheInfo").Element("SpatialReference").Element("WKID") != null)
                tilingScheme.WKID = int.Parse(confXml.Element("TileCacheInfo").Element("SpatialReference").Element("WKID").Value);
            else
                tilingScheme.WKID = -1;
            tilingScheme.TileOrigin = new Point(
                double.Parse(confXml.Element("TileCacheInfo").Element("TileOrigin").Element("X").Value),
                double.Parse(confXml.Element("TileCacheInfo").Element("TileOrigin").Element("Y").Value));
            tilingScheme.DPI = int.Parse(confXml.Element("TileCacheInfo").Element("DPI").Value);
            //LODInfos
            int lodsCount = confXml.Element("TileCacheInfo").Element("LODInfos").Elements().Count();
            tilingScheme.LODs = new LODInfo[lodsCount];
            for (int i = 0; i < lodsCount; i++)
            {
                tilingScheme.LODs[i] = new LODInfo()
                {
                    LevelID = i,
                    Scale = double.Parse(confXml.Element("TileCacheInfo").Element("LODInfos").Elements().ElementAt(i).Element("Scale").Value),
                    Resolution = double.Parse(confXml.Element("TileCacheInfo").Element("LODInfos").Elements().ElementAt(i).Element("Resolution").Value)
                };
            }
            sb = new StringBuilder("\r\n");
            //{"level" : 0, "resolution" : 156543.033928, "scale" : 591657527.591555}, 
            foreach (LODInfo lod in tilingScheme.LODs)
            {
                sb.Append(@"      {""level"":" + lod.LevelID + "," + @"""resolution"":" + lod.Resolution + "," + @"""scale"":" + lod.Scale + @"}," + "\r\n");
            }
            tilingScheme.LODsJson = sb.ToString().Remove(sb.ToString().Length - 3);//remove last "," and "\r\n"
            tilingScheme.TileCols = int.Parse(confXml.Element("TileCacheInfo").Element("TileCols").Value);
            tilingScheme.TileRows = int.Parse(confXml.Element("TileCacheInfo").Element("TileRows").Value);
            tilingScheme.CacheTileFormat = (ImageFormat)Enum.Parse(typeof(ImageFormat), confXml.Element("TileImageInfo").Element("CacheTileFormat").Value.ToUpper());
            tilingScheme.CompressionQuality = int.Parse(confXml.Element("TileImageInfo").Element("CompressionQuality").Value);
            tilingScheme.StorageFormat = (StorageFormat)Enum.Parse(typeof(StorageFormat), confXml.Element("CacheStorageInfo").Element("StorageFormat").Value);
            tilingScheme.PacketSize = int.Parse(confXml.Element("CacheStorageInfo").Element("PacketSize").Value);
            //read conf.cdi
            XElement confCdi = XElement.Load(System.IO.Path.GetDirectoryName(path) + @"\\conf.cdi");
            try
            {
                tilingScheme.FullExtent = new Envelope(
                double.Parse(confCdi.Element("XMin").Value),
                double.Parse(confCdi.Element("YMin").Value),
                double.Parse(confCdi.Element("XMax").Value),
                double.Parse(confCdi.Element("YMax").Value));
            }
            catch (Exception e)
            {
                throw new Exception("the content of conf.cdi file is not valid!" + e.Message);
            }
            tilingScheme.InitialExtent = tilingScheme.FullExtent;
            #endregion
        }

        /// <summary>
        /// Parse ArcGISTiledMapService info to create TilingScheme
        /// </summary>
        /// <param name="ht">hashtable contains all ArcGISTiledMapService info parsed by JSON util class.</param>
        /// <param name="tilingScheme"></param>
        protected void ReadArcGISTiledMapServiceTilingScheme(Hashtable ht, out TilingScheme tilingScheme)
        {
            tilingScheme = new TilingScheme();
            StringBuilder sb;
            #region read arcgis cache tiling scheme
            tilingScheme.Path = "N/A";//record the full tiling scheme file path
            tilingScheme.WKT = (ht["spatialReference"] as Hashtable)["wkt"] == null ? "Absent" : (string)(ht["spatialReference"] as Hashtable)["wkt"];
            tilingScheme.WKID = int.Parse((ht["spatialReference"] as Hashtable)["wkid"].ToString());
            tilingScheme.TileOrigin = new Point(
                (double)((ht["tileInfo"] as Hashtable)["origin"] as Hashtable)["x"],
                (double)((ht["tileInfo"] as Hashtable)["origin"] as Hashtable)["y"]);
            tilingScheme.DPI = int.Parse((ht["tileInfo"] as Hashtable)["dpi"].ToString());
            //LODInfos
            int lodsCount = ((ht["tileInfo"] as Hashtable)["lods"] as ArrayList).Count;
            tilingScheme.LODs = new LODInfo[lodsCount];
            for (int i = 0; i < lodsCount; i++)
            {
                tilingScheme.LODs[i] = new LODInfo()
                {
                    LevelID = i,
                    Scale = (double)(((ht["tileInfo"] as Hashtable)["lods"] as ArrayList)[i] as Hashtable)["scale"],
                    Resolution = (double)(((ht["tileInfo"] as Hashtable)["lods"] as ArrayList)[i] as Hashtable)["resolution"]
                };
            }
            sb = new StringBuilder("\r\n");
            //{"level" : 0, "resolution" : 156543.033928, "scale" : 591657527.591555}, 
            foreach (LODInfo lod in tilingScheme.LODs)
            {
                sb.Append(@"      {""level"":" + lod.LevelID + "," + @"""resolution"":" + lod.Resolution + "," + @"""scale"":" + lod.Scale + @"}," + "\r\n");
            }
            tilingScheme.LODsJson = sb.ToString().Remove(sb.ToString().Length - 3);//remove last "," and "\r\n"
            tilingScheme.TileCols = int.Parse((ht["tileInfo"] as Hashtable)["cols"].ToString());
            tilingScheme.TileRows = int.Parse((ht["tileInfo"] as Hashtable)["rows"].ToString());
            tilingScheme.CacheTileFormat = (ImageFormat)Enum.Parse(typeof(ImageFormat), (ht["tileInfo"] as Hashtable)["format"].ToString().ToUpper());
            tilingScheme.CompressionQuality = int.Parse((ht["tileInfo"] as Hashtable)["compressionQuality"].ToString());
            tilingScheme.StorageFormat = StorageFormat.unknown;//ArcGISTiledMapService doesn't expose this property
            tilingScheme.PacketSize = int.MinValue;

            tilingScheme.FullExtent = new Envelope(
            (double)(ht["fullExtent"] as Hashtable)["xmin"],
            (double)(ht["fullExtent"] as Hashtable)["ymin"],
            (double)(ht["fullExtent"] as Hashtable)["xmax"],
            (double)(ht["fullExtent"] as Hashtable)["ymax"]);
            tilingScheme.InitialExtent = new Envelope(
                (double)(ht["initialExtent"] as Hashtable)["xmin"],
                (double)(ht["initialExtent"] as Hashtable)["ymin"],
                (double)(ht["initialExtent"] as Hashtable)["xmax"],
                (double)(ht["initialExtent"] as Hashtable)["ymax"]);
            #endregion
        }

        protected void ReadArcGISTilePackageTilingSchemeFile(string path, out TilingScheme tilingScheme)
        {            
            tilingScheme = new TilingScheme();
            StringBuilder sb;
            #region read arcgis cache tiling scheme
            tilingScheme.Path = "v101/Layers/conf.xml";
            using (Stream streamConfxml = new MemoryStream(Utility.GetEntryBytesFromZIPFile(path, "v101/Layers/conf.xml")))
            {
                if (streamConfxml == null)
                {
                    throw new Exception("conf.xml not found in " + "v101/Layers/conf.xml of " + System.IO.Path.GetFileName(path));
                }
                //read conf.xml
                XElement confXml = XElement.Load(streamConfxml);
                tilingScheme.WKT = confXml.Element("TileCacheInfo").Element("SpatialReference").Element("WKT").Value;
                if (confXml.Element("TileCacheInfo").Element("SpatialReference").Element("WKID") != null)
                    tilingScheme.WKID = int.Parse(confXml.Element("TileCacheInfo").Element("SpatialReference").Element("WKID").Value);
                else
                    tilingScheme.WKID = -1;
                tilingScheme.TileOrigin = new Point(
                    double.Parse(confXml.Element("TileCacheInfo").Element("TileOrigin").Element("X").Value),
                    double.Parse(confXml.Element("TileCacheInfo").Element("TileOrigin").Element("Y").Value));
                tilingScheme.DPI = int.Parse(confXml.Element("TileCacheInfo").Element("DPI").Value);
                //LODInfos
                int lodsCount = confXml.Element("TileCacheInfo").Element("LODInfos").Elements().Count();
                tilingScheme.LODs = new LODInfo[lodsCount];
                for (int i = 0; i < lodsCount; i++)
                {
                    tilingScheme.LODs[i] = new LODInfo()
                    {
                        LevelID = i,
                        Scale = double.Parse(confXml.Element("TileCacheInfo").Element("LODInfos").Elements().ElementAt(i).Element("Scale").Value),
                        Resolution = double.Parse(confXml.Element("TileCacheInfo").Element("LODInfos").Elements().ElementAt(i).Element("Resolution").Value)
                    };
                }
                sb = new StringBuilder("\r\n");
                //{"level" : 0, "resolution" : 156543.033928, "scale" : 591657527.591555}, 
                foreach (LODInfo lod in tilingScheme.LODs)
                {
                    sb.Append(@"      {""level"":" + lod.LevelID + "," + @"""resolution"":" + lod.Resolution + "," + @"""scale"":" + lod.Scale + @"}," + "\r\n");
                }
                tilingScheme.LODsJson = sb.ToString().Remove(sb.ToString().Length - 3);//remove last "," and "\r\n"
                tilingScheme.TileCols = int.Parse(confXml.Element("TileCacheInfo").Element("TileCols").Value);
                tilingScheme.TileRows = int.Parse(confXml.Element("TileCacheInfo").Element("TileRows").Value);
                tilingScheme.CacheTileFormat = (ImageFormat)Enum.Parse(typeof(ImageFormat), confXml.Element("TileImageInfo").Element("CacheTileFormat").Value.ToUpper());
                tilingScheme.CompressionQuality = int.Parse(confXml.Element("TileImageInfo").Element("CompressionQuality").Value);
                tilingScheme.StorageFormat = (StorageFormat)Enum.Parse(typeof(StorageFormat), confXml.Element("CacheStorageInfo").Element("StorageFormat").Value);
                tilingScheme.PacketSize = int.Parse(confXml.Element("CacheStorageInfo").Element("PacketSize").Value);
            }
            using (Stream streamConfcdi = new MemoryStream(Utility.GetEntryBytesFromZIPFile(path, "v101/Layers/conf.cdi")))
            {
                if (streamConfcdi == null)
                {
                    throw new Exception("conf.cdi not found in " + path);
                }
                //read conf.cdi
                XElement confCdi = XElement.Load(streamConfcdi);
                try
                {
                    tilingScheme.FullExtent = new Envelope(
                    double.Parse(confCdi.Element("XMin").Value),
                    double.Parse(confCdi.Element("YMin").Value),
                    double.Parse(confCdi.Element("XMax").Value),
                    double.Parse(confCdi.Element("YMax").Value));
                }
                catch (Exception e)
                {
                    throw new Exception("the content of conf.cdi file is not valid!" + e.Message);
                }
                tilingScheme.InitialExtent = tilingScheme.FullExtent;
            }
            #endregion
        }

        protected void ReadGoogleMapsTilingScheme(out TilingScheme tilingScheme)
        {
            tilingScheme = new TilingScheme();
            StringBuilder sb;
            #region Google/Bing maps tiling scheme
            tilingScheme.Path = "N/A";
            tilingScheme.CacheTileFormat = ImageFormat.JPG;
            tilingScheme.CompressionQuality = 75;
            tilingScheme.DPI = 96;
            //LODs
            tilingScheme.LODs = new LODInfo[20];
            const double cornerCoordinate = 20037508.342787;
            double resolution = cornerCoordinate * 2 / 256;
            double scale = 591657527.591555;
            for (int i = 0; i < tilingScheme.LODs.Length; i++)
            {
                tilingScheme.LODs[i] = new LODInfo()
                {
                    Resolution = resolution,
                    LevelID = i,
                    Scale = scale
                };
                resolution /= 2;
                scale /= 2;
            }
            //create json
            sb = new StringBuilder("\r\n");
            //{"level" : 0, "resolution" : 156543.033928, "scale" : 591657527.591555}, 
            foreach (LODInfo lod in tilingScheme.LODs)
            {
                sb.Append(@"      {""level"":" + lod.LevelID + "," + @"""resolution"":" + lod.Resolution + "," + @"""scale"":" + lod.Scale + @"}," + "\r\n");
            }
            tilingScheme.LODsJson = sb.ToString().Remove(sb.ToString().Length - 3);//remove last "," and "\r\n"
            //two extent
            tilingScheme.InitialExtent = new Envelope(-cornerCoordinate, -cornerCoordinate, cornerCoordinate, cornerCoordinate);
            tilingScheme.FullExtent = tilingScheme.InitialExtent;
            tilingScheme.PacketSize = 0;
            tilingScheme.StorageFormat = StorageFormat.esriMapCacheStorageModeExploded;
            tilingScheme.TileCols = tilingScheme.TileRows = 256;
            tilingScheme.TileOrigin = new Point(-cornerCoordinate, cornerCoordinate);
            tilingScheme.WKID = 3857;//102100;
            tilingScheme.WKT = @"PROJCS[""WGS_1984_Web_Mercator_Auxiliary_Sphere"",GEOGCS[""GCS_WGS_1984"",DATUM[""D_WGS_1984"",SPHEROID[""WGS_1984"",6378137.0,298.257223563]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Mercator_Auxiliary_Sphere""],PARAMETER[""False_Easting"",0.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",0.0],PARAMETER[""Standard_Parallel_1"",0.0],PARAMETER[""Auxiliary_Sphere_Type"",0.0],UNIT[""Meter"",1.0],AUTHORITY[""ESRI"",""3857""]]";
            #endregion
        }

        protected void ReadTianDiTuTilingScheme(out TilingScheme tilingScheme)
        {
            //ref:http://www.tianditu.com/guide/resource.jsp
            tilingScheme = new TilingScheme();
            StringBuilder sb;
            #region TianDiTu Tiling Scheme
            tilingScheme.Path = "N/A";
            tilingScheme.CacheTileFormat = ImageFormat.JPEG;
            tilingScheme.CompressionQuality = 75;
            tilingScheme.DPI = 96;
            //LODs
            tilingScheme.LODs = new LODInfo[17];
            double resolution = 90 / 256d;
            double scale = 147914677.73;//147748799.285417;
            for (int i = 0; i < tilingScheme.LODs.Length; i++)
            {
                tilingScheme.LODs[i] = new LODInfo()
                {
                    Resolution = resolution,
                    LevelID = i,
                    Scale = scale
                };
                resolution /= 2;
                scale /= 2;
            }
            //create json
            sb = new StringBuilder("\r\n");
            //{"level" : 0, "resolution" : 156543.033928, "scale" : 591657527.591555}, 
            foreach (LODInfo lod in tilingScheme.LODs)
            {
                sb.Append(@"      {""level"":" + lod.LevelID + "," + @"""resolution"":" + lod.Resolution + "," + @"""scale"":" + lod.Scale + @"}," + "\r\n");
            }
            tilingScheme.LODsJson = sb.ToString().Remove(sb.ToString().Length - 3);//remove last "," and "\r\n"
            //two extent
            tilingScheme.InitialExtent = new Envelope(-180, -90, 180, 90);
            tilingScheme.FullExtent = tilingScheme.InitialExtent;
            tilingScheme.PacketSize = 0;
            tilingScheme.StorageFormat = StorageFormat.esriMapCacheStorageModeExploded;
            tilingScheme.TileCols = tilingScheme.TileRows = 256;
            tilingScheme.TileOrigin = new Point(-180, 90);
            tilingScheme.WKID = 4326;
            tilingScheme.WKT = @"GEOGCS[""WGS 84"",DATUM[""WGS_1984"",SPHEROID[""WGS 84"",6378137,298.257223563,AUTHORITY[""EPSG"",""7030""]],AUTHORITY[""EPSG"",""6326""]],PRIMEM[""Greenwich"",0,AUTHORITY[""EPSG"",""8901""]],UNIT[""degree"",0.01745329251994328,AUTHORITY[""EPSG"",""9122""]],AUTHORITY[""EPSG"",""4326""]]";
            #endregion
        }
        #endregion

        public static bool IsOnlineMaps(string type)
        {
            bool isPredefinedDataSource = false;
            foreach (var str in Enum.GetValues(typeof(DataSourceTypePredefined)))
            {
                if (string.Equals(type, str.ToString()))
                {
                    isPredefinedDataSource = true;
                    break;
                }
            }
            return !isPredefinedDataSource;
        }

        protected byte[] HttpGetTileBytes(string uri)
        {            
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Accept = "*/*";
            request.KeepAlive = true;
            request.Method = "GET";
            //request.Headers.Add("Accept-Encoding", "gzip,deflate,sdch");
            if (this.Type == DataSourceTypePredefined.ArcGISTiledMapService.ToString())
            {
                request.Referer = this.Path + "?f=jsapi";
            }
            
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.4 (KHTML, like Gecko) Chrome/22.0.1229.94 Safari/537.4";
            
            request.Proxy = null;//==no proxy
            request.Timeout = 20000;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (!response.ContentType.ToLower().Contains("image"))
                    throw new Exception("download(http get) result is not image");
                return Util.Utility.StreamToBytes(response.GetResponseStream());
            }
        }

        protected byte[] HttpPostTileBytes(string url, string queryData)
        {
            HttpWebRequest request;
            request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Accept = "text/html, image/png, image/jpeg, image/gif, */*;q=0.1";
            request.KeepAlive = true;
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.4 (KHTML, like Gecko) Chrome/22.0.1229.94 Safari/537.4";
            request.Proxy = null;//GlobalProxySelection.GetEmptyWebProxy();   
            byte[] data = Encoding.UTF8.GetBytes(queryData);
            request.ContentLength = data.Length;
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (!response.ContentType.ToLower().Contains("image"))
                    throw new Exception("download(http post) result is not image");
                return Util.Utility.StreamToBytes(response.GetResponseStream());
            }
        }

        #region Local File Cache
        /// <summary>
        /// by checking keyword to determin if sqlite file is PBS created local cache file.
        /// </summary>
        private string constPBSFileCacheKeyword = "___PBSLOCALCACHE___";
        private string _localCacheFileName;
        private SQLiteConnection _connLocalCacheFile;
        private ConcurrentDictionary<string, byte[]> _dictTilesToBeLocalCached = new ConcurrentDictionary<string, byte[]>();
        /// <summary>
        /// Check if the local cache file of corresponding onlinemap is valid. If not valid or not exists, create new one.
        /// </summary>
        /// <param name="localCacheFileName">the name of the local cache file(.cache)</param>
        protected void ValidateLocalCacheFile(string localCacheFileName)
        {
            _localCacheFileName = localCacheFileName;
            try
            {
                if (File.Exists(localCacheFileName))
                {
                    #region check if the file valid
                    using (SQLiteConnection conn = new SQLiteConnection("Data source = " + localCacheFileName))
                    {
                        conn.Open();
                        using (SQLiteCommand cmd = new SQLiteCommand(conn))
                        {
                            cmd.CommandText = "SELECT value FROM metadata WHERE name='name'";
                            object o = cmd.ExecuteScalar();
                            if (o != null && o.ToString().Equals(constPBSFileCacheKeyword))
                            {
                                _connLocalCacheFile = new SQLiteConnection("Data Source=" + _localCacheFileName);
                                _connLocalCacheFile.Open();
                                return;
                            }
                            else
                            {
                                try
                                {
                                    File.Delete(localCacheFileName);
                                }
                                catch (Exception e)
                                {
                                    throw new Exception("Init online maps local cache file failed.\r\n" + e.Message);
                                }
                            }
                        }
                    }
                    #endregion
                }
                #region create new sqlite database
                SQLiteConnection.CreateFile(localCacheFileName);
                using (SQLiteConnection conn = new SQLiteConnection("Data source = " + localCacheFileName))
                {
                    conn.Open();
                    using (SQLiteTransaction transaction = conn.BeginTransaction())
                    {
                        using (SQLiteCommand cmd = new SQLiteCommand(conn))
                        {
                            #region create tables and indexes
                            //ref http://mapbox.com/developers/mbtiles/
                            //metadata table
                            cmd.CommandText = "CREATE TABLE metadata (name TEXT, value TEXT)";
                            cmd.ExecuteNonQuery();
                            //images table
                            cmd.CommandText = "CREATE TABLE images (tile_data BLOB, tile_id TEXT)";
                            cmd.ExecuteNonQuery();
                            //map table
                            cmd.CommandText = "CREATE TABLE map (zoom_level INTEGER, tile_column INTEGER, tile_row INTEGER, tile_id TEXT)";
                            cmd.ExecuteNonQuery();
                            //tiles view
                            cmd.CommandText = @"CREATE VIEW tiles AS SELECT
    map.zoom_level AS zoom_level,
    map.tile_column AS tile_column,
    map.tile_row AS tile_row,
    images.tile_data AS tile_data
FROM map JOIN images ON images.tile_id = map.tile_id";
                            cmd.ExecuteNonQuery();
                            //indexes
                            cmd.CommandText = "CREATE UNIQUE INDEX images_id on images (tile_id)";
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = "CREATE UNIQUE INDEX map_index on map (zoom_level, tile_column, tile_row)";
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = @"CREATE UNIQUE INDEX name ON metadata (name)";
                            cmd.ExecuteNonQuery();
                            #endregion
                            #region write metadata
                            //name
                            cmd.CommandText = @"INSERT INTO metadata(name,value) VALUES (""name"",""" + constPBSFileCacheKeyword + @""")";
                            cmd.ExecuteNonQuery();
                            //type
                            cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('type','baselayer')";
                            cmd.ExecuteNonQuery();
                            //version
                            cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('version','1.2')";
                            cmd.ExecuteNonQuery();
                            //no description
                            //format
                            string f = TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
                            cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('format','" + f + "')";
                            cmd.ExecuteNonQuery();
                            //no bounds
                            //no attribution
                            #endregion
                        }
                        transaction.Commit();
                    }
                }
                #endregion
                _connLocalCacheFile = new SQLiteConnection("Data Source=" + _localCacheFileName);
                _connLocalCacheFile.Open();
            }
            catch (Exception e)
            {
                throw new Exception("Validating local cache file error!\r\n" + e.Message);
            }
        }

        /// <summary>
        /// Try to retrieve tile byte[] from local .cache file. Return byte[] if succeed, null if failed.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public byte[] GetTileBytesFromLocalCache(int level, int row, int col)
        {
            if (!IsOnlineMap && !(this is DataSourceRasterImage))
                return null;
            int tmsCol, tmsRow;
            Utility.ConvertGoogleTileToTMSTile(level, row, col, out tmsRow, out tmsCol);
            string commandText = string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2} AND zoom_level={3}", "tile_data", tmsCol, tmsRow, level);
            using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, _connLocalCacheFile))
            {
                object o = sqlCmd.ExecuteScalar();
                if (o != null)
                {
                    return (byte[])o;
                }
                return null;
            }
        }
        /// <summary>
        /// occurs when tile loaded and to save tile to local file cache if necessary.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="a">TileLoadEventArgs</param>
        protected void InternalOnTileLoaded(object o, TileLoadEventArgs a)
        {
            if (a.GeneratedMethod != TileGeneratedSource.DynamicOutput)
                return;
            if (o is DataSourceRasterImage && !ConfigManager.App_AllowFileCacheOfRasterImage ||
                IsOnlineMap && !ConfigManager.App_AllowFileCacheOfOnlineMaps)
                return;

            int tmsRow, tmsCol;
            Utility.ConvertGoogleTileToTMSTile(a.Level, a.Row, a.Column, out tmsRow, out tmsCol);
            string key = string.Format("{0}/{1}/{2}", a.Level, tmsCol, tmsRow);
            if (_dictTilesToBeLocalCached.ContainsKey(key))
                return;
            _dictTilesToBeLocalCached.TryAdd(key, a.TileBytes);
            if (_dictTilesToBeLocalCached.Count ==1000)
            {
                WriteTilesToLocalCacheFile(_dictTilesToBeLocalCached);
                _dictTilesToBeLocalCached.Clear();
            }
        }
        /// <summary>
        /// write tiles to .cache local cache file. using for local cache of onlinemaps and rasterimage datasource.
        /// </summary>
        /// <param name="dict"></param>
        protected void WriteTilesToLocalCacheFile(ConcurrentDictionary<string, byte[]> dict)
        {
            lock (_locker)
            {
                try
                {
                    using (SQLiteConnection conn = new SQLiteConnection("Data source = " + _localCacheFileName))
                    {
                        conn.Open();
                        using (SQLiteTransaction transaction = conn.BeginTransaction())
                        {
                            using (SQLiteCommand cmd = new SQLiteCommand(conn))
                            {
                                foreach (KeyValuePair<string, byte[]> kvp in dict)
                                {
                                    //key = "level/col/row"
                                    int level = int.Parse(kvp.Key.Split(new char[] { '/' })[0]);
                                    int col = int.Parse(kvp.Key.Split(new char[] { '/' })[1]);
                                    int row = int.Parse(kvp.Key.Split(new char[] { '/' })[2]);
                                    string guid = Guid.NewGuid().ToString();
                                    cmd.CommandText = "INSERT INTO images VALUES (@tile_data,@tile_id)";
                                    cmd.Parameters.AddWithValue("tile_data", kvp.Value);
                                    cmd.Parameters.AddWithValue("tile_id", guid);
                                    cmd.ExecuteNonQuery();
                                    cmd.CommandText = "INSERT INTO map VALUES (@zoom_level,@tile_column,@tile_row,@tile_id)";
                                    cmd.Parameters.AddWithValue("zoom_level", level);
                                    cmd.Parameters.AddWithValue("tile_column", col);
                                    cmd.Parameters.AddWithValue("tile_row", row);
                                    cmd.Parameters.AddWithValue("tile_id", guid);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            transaction.Commit();
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Writing tiles to local .cache file error!\r\n" + e.Message);
                }
            }
        }
        #endregion

        #region IFormatConverter
        private ConvertStatus _convertingStatus;
        /// <summary>
        /// cache format converting status
        /// </summary>
        public ConvertStatus ConvertingStatus
        {
            get { return _convertingStatus; }
            protected set { _convertingStatus = value; }
        }
        #region ToMBTiles
        private string _outputFile;
        //using for multi threading
        private static readonly object _locker = new object();
        private long _completeCount, _levelCompleteCount, _totalCount, _levelTotalCount, _errorCount, _levelErrorCount, _completeTotalBytes;
        /// <summary>
        /// constrain the tile downloading to the extent of an envelope or boundary shape of a polygon.
        /// </summary>
        private Geometry _downloadGeometry;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        /// <summary>
        /// Do convert jobs from various datasource to MBTiles. Datasource must be in 3857 tilingscheme.
        /// </summary>
        /// <param name="outputPath">full output file name and path.</param>
        /// <param name="name">optional by mbtiles.</param>
        /// <param name="description">optional by mbtiles.</param>
        /// <param name="attribution">optional by mbtiles.</param>
        /// <param name="levels">tiles in which levels to convert to mbtiles.</param>
        /// <param name="geometry">convert/download extent, sr=3857. If this is Envelope, download by rectangle, if this is polygon, download by polygon's shape.</param>
        /// <param name="doCompact">implementing the reducing redundant tile bytes part of MBTiles specification?</param>
        protected void DoConvertToMBTiles(string outputPath, string name, string description, string attribution, int[] levels, Geometry geometry, bool doCompact)
        {
            _outputFile = outputPath;
            _downloadGeometry = geometry;
            _convertingStatus.IsInProgress = true;
            try
            {
                CreateMBTilesFileAndWriteMetaData(outputPath, name, description, attribution, geometry);
                #region calculate startCol/Row and endCol/Row and tiles count of each level
                int constTileSize = 256;
                _convertingStatus.TotalCount = 0;
                string[] keyTileInfos = new string[levels.Length];
                int[] tilesCountOfLevel = new int[levels.Length];
                for (int i = 0; i < levels.Length; i++)
                {
                    LODInfo lod = TilingScheme.LODs[levels[i]];
                    double oneTileDistance = lod.Resolution * constTileSize;
                    int startTileRow = (int)(Math.Abs(TilingScheme.TileOrigin.Y - geometry.Extent.YMax) / oneTileDistance);
                    int startTileCol = (int)(Math.Abs(TilingScheme.TileOrigin.X - geometry.Extent.XMin) / oneTileDistance);
                    int endTileRow = (int)(Math.Abs(TilingScheme.TileOrigin.Y - geometry.Extent.YMin) / oneTileDistance);
                    int endTileCol = (int)(Math.Abs(TilingScheme.TileOrigin.X - geometry.Extent.XMax) / oneTileDistance);
                    keyTileInfos[i] = string.Format("{0},{1},{2},{3}", startTileRow, startTileCol, endTileRow, endTileCol);
                    tilesCountOfLevel[i] = Math.Abs((endTileCol - startTileCol + 1) * (endTileRow - startTileRow + 1));
                    _convertingStatus.TotalCount += tilesCountOfLevel[i];
                }
                _totalCount = _convertingStatus.TotalCount;
                _completeCount = _errorCount = 0;
                #endregion
                for (int i = 0; i < levels.Length; i++)
                {
                    int level, startR, startC, endR, endC;//startTileRow,startTileCol,...
                    level = TilingScheme.LODs[levels[i]].LevelID;
                    startR = int.Parse(keyTileInfos[i].Split(new char[] { ',' })[0]);
                    startC = int.Parse(keyTileInfos[i].Split(new char[] { ',' })[1]);
                    endR = int.Parse(keyTileInfos[i].Split(new char[] { ',' })[2]);
                    endC = int.Parse(keyTileInfos[i].Split(new char[] { ',' })[3]);
                    _convertingStatus.Level = level;
                    _convertingStatus.LevelTotalCount = tilesCountOfLevel[i];
                    _convertingStatus.LevelCompleteCount = _convertingStatus.LevelErrorCount = 0;
                    _levelTotalCount = _convertingStatus.LevelTotalCount;
                    _levelCompleteCount = _levelErrorCount = 0;           
                    SaveOneLevelTilesToMBTiles(level, startR, startC, endR, endC);
                    if (_convertingStatus.IsCancelled)
                    {
                        _convertingStatus.IsCompletedSuccessfully = false;
                        break;
                    }
                }
                if (doCompact)
                {
                    _convertingStatus.IsDoingCompact = true;
                    _convertingStatus.SizeBeforeCompact = new FileInfo(_outputFile).Length;
                    CompactMBTiles(_outputFile);
                    _convertingStatus.IsDoingCompact = false;
                    _convertingStatus.SizeAfterCompact = new FileInfo(_outputFile).Length;
                }
                if (!_convertingStatus.IsCancelled)
                    _convertingStatus.IsCompletedSuccessfully = true;
            }
            finally
            {
                _convertingStatus.IsInProgress = false;
                _convertingStatus.IsCommittingTransaction = false;
                _convertingStatus.IsDoingCompact = false;
            }
        }

        /// <summary>
        /// process all tiles in a level and save them in sqlite db, with multi threads
        /// </summary>
        /// <param name="level">level number</param>
        /// <param name="startRowLevel">start row number of full extent of this level</param>
        /// <param name="startColLevel"></param>
        /// <param name="endRowLevel"></param>
        /// <param name="endColLevel"></param>
        private void SaveOneLevelTilesToMBTiles(int level, int startRowLevel, int startColLevel, int endRowLevel, int endColLevel)
        {
            int bundleSize = IsOnlineMap ? 16 : 128;
            Bundle startBundle = new Bundle(bundleSize, level, startRowLevel / bundleSize, startColLevel / bundleSize, TilingScheme);
            Bundle endBundle = new Bundle(bundleSize, level, endRowLevel / bundleSize, endColLevel / bundleSize, TilingScheme);
            List<Bundle> allBundles = new List<Bundle>();
            for (int bRow = startBundle.Row; bRow <= endBundle.Row; bRow++)
            {
                for (int bCol = startBundle.Col; bCol <= endBundle.Col; bCol++)
                {
                    Bundle b = new Bundle(bundleSize, level, bRow, bCol, TilingScheme);
                    if (_downloadGeometry is Polygon)
                    {
                        bool bPolygonTouchesWithBundle = false;
                        Polygon polygon = _downloadGeometry as Polygon;
                        if (polygon.ContainsPoint(b.Extent.LowerLeft) || polygon.ContainsPoint(b.Extent.LowerRight) || polygon.ContainsPoint(b.Extent.UpperLeft) || polygon.ContainsPoint(b.Extent.UpperRight))
                            bPolygonTouchesWithBundle = true;
                        if (b.Extent.ContainsPoint(polygon.Extent.LowerLeft) && b.Extent.ContainsPoint(polygon.Extent.LowerRight) && b.Extent.ContainsPoint(polygon.Extent.UpperLeft) && b.Extent.ContainsPoint(polygon.Extent.UpperRight))
                            bPolygonTouchesWithBundle = true;
                        if (polygon.IsIntersectsWithPolygon(b.Extent.ToPolygon()))
                            bPolygonTouchesWithBundle = true;
                        if (!bPolygonTouchesWithBundle)
                            continue;
                    }
                    allBundles.Add(b);
                }
            }
            
            int maxThreadCount = IsOnlineMap ? 50 : 5;
            int queueCount = allBundles.Count % maxThreadCount == 0 ? allBundles.Count / maxThreadCount : allBundles.Count / maxThreadCount + 1;
            for (int queue = 0; queue < queueCount; queue++)
            {
                int startBundleIndex = maxThreadCount * queue;
                int endBundleIndex = startBundleIndex + maxThreadCount - 1;
                endBundleIndex = endBundleIndex > allBundles.Count ? allBundles.Count - 1 : endBundleIndex;
                List<Task> tasks = new List<Task>();
                for (int i = startBundleIndex; i <= endBundleIndex; i++)
                {
                    if (_convertingStatus.IsCancelled)
                        return;
                    Bundle b = allBundles[i];
                    int startR = startRowLevel > b.StartTileRow ? startRowLevel : b.StartTileRow;
                    int startC = startColLevel > b.StartTileCol ? startColLevel : b.StartTileCol;
                    int endR = endRowLevel > b.EndTileRow ? b.EndTileRow : endRowLevel;
                    int endC = endColLevel > b.EndTileCol ? b.EndTileCol : endColLevel;
                    Task t = Task.Factory.StartNew(() => { WriteTilesToSqlite(GetTilesByExtent(level, startR, startC, endR, endC)); }, _cts.Token);
                    tasks.Add(t);
                }
                _convertingStatus.ThreadCount = tasks.Count;
                try
                {
                    Task.WaitAll(tasks.ToArray());
                }
                catch (AggregateException)
                {
                }
            }
        }

        /// <summary>
        /// Notify all converting tasks to cancel. The db transaction has began will still need sometime to be completed.
        /// </summary>
        protected void CancelDoConvertToMBTiles()
        {
            _convertingStatus.IsCancelled = true;
            _cts.Cancel();
        }

        /// <summary>
        /// Get tiles of specified extent.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="startRow"></param>
        /// <param name="startCol"></param>
        /// <param name="endRow"></param>
        /// <param name="endCol"></param>
        /// <returns>A Dictionary which stores the tiles bytes. key patten is "level/col/row".</returns>
        private Dictionary<string, byte[]> GetTilesByExtent(int level, int startRow, int startCol, int endRow, int endCol)
        {
            Dictionary<string, byte[]> dict = new Dictionary<string, byte[]>();
            for (int r = startRow; r <= endRow; r++)
            {
                for (int c = startCol; c <= endCol; c++)
                {
                    byte[] bytes = GetTileBytes(level, r, c);
                    if (bytes != null)
                    {
                        dict.Add(string.Format("{0}/{1}/{2}", level, c, r), bytes);
                        
                        _convertingStatus.LevelCompleteCount = Interlocked.Increment(ref _levelCompleteCount);
                        _convertingStatus.CompleteCount = Interlocked.Increment(ref _completeCount);
                        _convertingStatus.CompleteTotalBytes = Interlocked.Add(ref _completeTotalBytes, bytes.Length);
                    }
                    else
                    {
                        _convertingStatus.LevelErrorCount = Interlocked.Increment(ref _levelErrorCount);
                        _convertingStatus.ErrorCount = Interlocked.Increment(ref _errorCount);
                    }
#if Debug
                    System.Diagnostics.Debug.WriteLine(_convertingStatus.LevelCompleteCount + " / " + _convertingStatus.LevelTotalCount + "  |||  " + level + "/" + r + "/" + c + "thread:" + Thread.CurrentThread.ManagedThreadId);
#endif
                }
            }
            return dict;
        }

        /// <summary>
        /// Write tiles in Dictionary to Sqlite file.
        /// </summary>
        /// <param name="dict">the Dictionary which contains tiles bytes to write. key patten is "level/col/row".</param>
        private void WriteTilesToSqlite(Dictionary<string, byte[]> dict)
        {
            lock (_locker)
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data source = " + _outputFile))
                {
                    conn.Open();
                    SQLiteTransaction transaction = conn.BeginTransaction();
                    try
                    {
                        using (SQLiteCommand cmd = new SQLiteCommand(conn))
                        {
                            foreach (KeyValuePair<string, byte[]> kvp in dict)
                            {
                                int level = int.Parse(kvp.Key.Split(new char[] { '/' })[0]);
                                int col = int.Parse(kvp.Key.Split(new char[] { '/' })[1]);
                                int row = int.Parse(kvp.Key.Split(new char[] { '/' })[2]);
                                int tmsRow, tmsCol;
                                Utility.ConvertGoogleTileToTMSTile(level, row, col, out tmsRow, out tmsCol);
                                string guid = Guid.NewGuid().ToString();
                                cmd.CommandText = "INSERT INTO images VALUES (@tile_data,@tile_id)";
                                cmd.Parameters.AddWithValue("tile_data", kvp.Value);
                                cmd.Parameters.AddWithValue("tile_id", guid);
                                cmd.ExecuteNonQuery();
                                cmd.CommandText = "INSERT INTO map VALUES (@zoom_level,@tile_column,@tile_row,@tile_id)";
                                cmd.Parameters.AddWithValue("zoom_level", level);
                                cmd.Parameters.AddWithValue("tile_column", tmsCol);
                                cmd.Parameters.AddWithValue("tile_row", tmsRow);
                                cmd.Parameters.AddWithValue("tile_id", guid);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        _convertingStatus.IsCommittingTransaction = true;
                        transaction.Commit();
                        _convertingStatus.IsCommittingTransaction = false;
                    }
                    finally
                    {
                        if (transaction != null)
                            transaction.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// implementing the reducing redundant tile bytes part of MBTiles specification.
        /// </summary>
        /// <param name="fileName">MBTiles file name.</param>
        private void CompactMBTiles(string fileName)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data source = " + fileName))
                {
                    conn.Open();
                    List<int> duplicateLength = new List<int>();
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    {
                        cmd.CommandText = "SELECT tile_data,count(*) as counts FROM images GROUP BY tile_data HAVING count(*) > 1";
                        SQLiteDataReader dr = cmd.ExecuteReader();
                        while (dr.Read())
                        {
                            if (!duplicateLength.Contains(((byte[])dr[0]).Length))
                                duplicateLength.Add(((byte[])dr[0]).Length);
                        }
                        dr.Close();
                        foreach (int length in duplicateLength)
                        {
                            cmd.CommandText = "SELECT tiles.zoom_level as zoom_level,tiles.tile_column as tile_column,tiles.tile_row as tile_row,tiles.tile_data as tile_data,map.tile_id as tile_id FROM tiles JOIN map ON map.zoom_level=tiles.zoom_level AND map.tile_column=tiles.tile_column AND map.tile_row=tiles.tile_row WHERE length(tile_data)=" + length;
                            SQLiteDataReader row = cmd.ExecuteReader();
                            Dictionary<string, byte[]> uniqueBytes = new Dictionary<string, byte[]>();                  
                            using (SQLiteTransaction transaction = conn.BeginTransaction())
                            {
                                using (SQLiteCommand cmd1 = new SQLiteCommand(conn))
                                {
                                    while (row.Read())
                                    {
                                        bool isRedundant = false;
                                        if (uniqueBytes.Count == 0)
                                        {
                                            uniqueBytes.Add(row["tile_id"].ToString(), (byte[])row["tile_data"]);
                                            continue;
                                        }
                                        foreach (KeyValuePair<string, byte[]> kvp in uniqueBytes)
                                        {
                                            if (IsByteArrayEquivalent((byte[])row["tile_data"], kvp.Value))
                                            {
                                                isRedundant = true;

                                                cmd1.CommandText = string.Format("UPDATE map SET tile_id = '{0}' WHERE zoom_level = {1} AND tile_column = {2} AND tile_row = {3} ", kvp.Key, int.Parse(row["zoom_level"].ToString()), int.Parse(row["tile_column"].ToString()), int.Parse(row["tile_row"].ToString()));
                                                cmd1.ExecuteNonQuery();
                                                cmd1.CommandText = "DELETE FROM images WHERE tile_id='" + row["tile_id"].ToString() + "'";
                                                cmd1.ExecuteNonQuery();

                                                break;
                                            }
                                        }
                                        if (!isRedundant)
                                            uniqueBytes.Add(row["tile_id"].ToString(), (byte[])row["tile_data"]);
                                    }
                                }
                                row.Close();
                                transaction.Commit();
                            }
                        }
                        cmd.CommandText = "VACUUM";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("compacting MBTiles error!\r\n" + e.Message);
            }
        }

        private bool IsByteArrayEquivalent(byte[] bytes1, byte[] bytes2)
        {
            if (bytes1.Length != bytes2.Length)
                return false;
            for (int i = 0; i < bytes1.Length; i++)
            {
                if (bytes1[i] != bytes2[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// create the .mbtiles file and initialize tables, views and write metadata.
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="attribution"></param>
        private void CreateMBTilesFileAndWriteMetaData(string outputPath, string name, string description, string attribution, Geometry geometry)
        {
            //check the wkid
            if (!TilingScheme.WKID.Equals(102100) && !TilingScheme.WKID.Equals(3857))
            {
                throw new Exception("The WKID of ArcGIS Cache is not 3857 or 102100!");
            }
            //check the numbers of lods
            if (TilingScheme.LODs.Length != 20)
            {
                throw new Exception("The count of levels must be 20! Current levels count = " + TilingScheme.LODs.Length);
            }
            //check tiling scheme origin
            if (Math.Abs(TilingScheme.TileOrigin.X + 20037508.342787) > 0.1)
            {
                throw new Exception("The tiling scheme origin is not correct!");
            }
            //check the tile dimension
            if (!TilingScheme.TileCols.Equals(256) || !TilingScheme.TileRows.Equals(256))
            {
                throw new Exception("The size of a tile is not 256*256!");
            }
            //create new sqlite database
            SQLiteConnection.CreateFile(outputPath);
            using (SQLiteConnection conn = new SQLiteConnection("Data source = " + outputPath))
            {
                conn.Open();
                using (SQLiteTransaction transaction = conn.BeginTransaction())
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(conn))
                    {
                        #region create tables and indexes
                        //ref http://mapbox.com/developers/mbtiles/
                        //metadata table
                        cmd.CommandText = "CREATE TABLE metadata (name TEXT, value TEXT)";
                        cmd.ExecuteNonQuery();
                        //images table
                        cmd.CommandText = "CREATE TABLE images (tile_data BLOB, tile_id TEXT)";
                        cmd.ExecuteNonQuery();
                        //map table
                        cmd.CommandText = "CREATE TABLE map (zoom_level INTEGER, tile_column INTEGER, tile_row INTEGER, tile_id TEXT)";
                        cmd.ExecuteNonQuery();
                        //tiles view
                        cmd.CommandText = @"CREATE VIEW tiles AS SELECT
    map.zoom_level AS zoom_level,
    map.tile_column AS tile_column,
    map.tile_row AS tile_row,
    images.tile_data AS tile_data
FROM map JOIN images ON images.tile_id = map.tile_id";
                        cmd.ExecuteNonQuery();
                        //indexes
                        cmd.CommandText = "CREATE UNIQUE INDEX images_id on images (tile_id)";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "CREATE UNIQUE INDEX map_index on map (zoom_level, tile_column, tile_row)";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = @"CREATE UNIQUE INDEX name ON metadata (name)";
                        cmd.ExecuteNonQuery();
                        #endregion
                        #region write metadata
                        //name
                        cmd.CommandText = @"INSERT INTO metadata(name,value) VALUES (""name"",""" + name + @""")";
                        cmd.ExecuteNonQuery();
                        //type
                        cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('type','baselayer')";
                        cmd.ExecuteNonQuery();
                        //version
                        cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('version','1.2')";
                        cmd.ExecuteNonQuery();
                        //description
                        cmd.CommandText = @"INSERT INTO metadata(name,value) VALUES (""description"",""" + description + @""")";
                        cmd.ExecuteNonQuery();
                        //format
                        string f = TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
                        cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('format','" + f + "')";
                        cmd.ExecuteNonQuery();
                        //bounds
                        Point bottomLeft = Utility.WebMercatorToGeographic(new Point(geometry.Extent.XMin, geometry.Extent.YMin));
                        Point upperRight = Utility.WebMercatorToGeographic(new Point(geometry.Extent.XMax, geometry.Extent.YMax));
                        cmd.CommandText = "INSERT INTO metadata(name,value) VALUES ('bounds','" + string.Format("{0},{1},{2},{3}", bottomLeft.X.ToString(), bottomLeft.Y.ToString(), upperRight.X.ToString(), upperRight.Y.ToString()) + "')";
                        cmd.ExecuteNonQuery();
                        //attribution
                        cmd.CommandText = @"INSERT INTO metadata(name,value) VALUES (""attribution"",""" + attribution + @""")";
                        cmd.ExecuteNonQuery();
                        #endregion
                    }
                    transaction.Commit();
                }
            }
        }
        #endregion
        #endregion
    }

    /// <summary>
    /// base map provider source format
    /// </summary>
    public enum DataSourceTypePredefined
    {
        MobileAtlasCreator,
        MBTiles,
        ArcGISCache,
        ArcGISTilePackage,
        ArcGISDynamicMapService,
        ArcGISTiledMapService,
        ArcGISImageService,
        RasterImage,
        OGCWMSService,
        AutoNaviCache,        
        TianDiTuAnnotation,
        TianDiTuMap,
    }

    public class TilingScheme
    {
        /// <summary>
        /// tiling scheme file path
        /// </summary>
        public string Path { get; set; }
        public string RestResponseArcGISJson { get; set; }
        public string RestResponseArcGISPJson { get; set; }
        public int WKID { get; set; }
        public string WKT { get; set; }
        public int TileCols { get; set; }
        public int TileRows { get; set; }
        /// <summary>
        /// PNG/PNG32/PNG24/PNG8/JPEG/JPG/Mixed
        /// </summary>
        public ImageFormat CacheTileFormat { get; set; }
        /// <summary>
        /// png=0,jpg=XX
        /// </summary>
        public int CompressionQuality { get; set; }
        public Point TileOrigin { get; set; }
        public LODInfo[] LODs { get; set; }
        public string LODsJson { get; set; }
        public Envelope InitialExtent { get; set; }
        public Envelope FullExtent { get; set; }
        public StorageFormat StorageFormat { get; set; }
        public int PacketSize { get; set; }
        public int DPI { get; set; }
    }

    public class LODInfo
    {
        public int LevelID { get; set; }
        public double Scale { get; set; }
        public double Resolution { get; set; }
    }

    public enum StorageFormat
    {
        esriMapCacheStorageModeExploded,
        esriMapCacheStorageModeCompact,
        unknown//ArcGISTiledMapService doesn't expose this property
    }

    public enum ImageFormat
    {
        PNG,
        PNG32,
        PNG24,
        PNG8,
        JPG,
        JPEG,
        MIXED
    }

    public class TileLoadEventArgs : EventArgs
    {
        public int Level { get; set; }
        public int Column { get; set; }
        public int Row { get; set; }
        public byte[] TileBytes { get; set; }
        public TileGeneratedSource GeneratedMethod { get; set; }
    }

    /// <summary>
    /// by which means the tile bytes generated
    /// </summary>
    public enum TileGeneratedSource
    {
        DynamicOutput,
        FromMemcached,
        FromFileCache
    }
}
