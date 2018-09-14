//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;
using PBS.Util;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.Threading;

namespace PBS.DataSource
{
    public class DataSourceArcGISCache : DataSourceBase, IFormatConverter
    {
        public DataSourceArcGISCache(string path)
        {
            if (!Directory.Exists(path + "\\_alllayers"))
                throw new Exception("_alllayers directory does not exist!");
            Initialize(path);            
            ConvertingStatus = new ConvertStatus();
        }

        ~DataSourceArcGISCache()
        {

        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.ArcGISCache.ToString();
            base.Initialize(path);
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            ReadArcGISTilingSchemeFile(this.Path, out tilingScheme);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            #region Exploded
            if (this.TilingScheme.StorageFormat == StorageFormat.esriMapCacheStorageModeExploded)
            {
                string baseUrl = this.Path;//D:\\arcgisserver\\arcgiscache\\inspur_shandong1\\图层   
                string suffix = this.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
                string str = string.Format(@"{0}\_alllayers\L{1:d2}\R{2:x8}\C{3:x8}.{4}", baseUrl, level, row, col, suffix);
                if (string.IsNullOrEmpty(str) || !File.Exists(str))
                {
                    return null;
                }
                //byte[] bytes;
                //using (FileStream fs = new FileStream(str, FileMode.Open, FileAccess.Read))
                //{
                //    bytes = new byte[fs.Length];
                //    fs.Read(bytes, 0, (int)fs.Length);
                //}
                //return bytes;
                return File.ReadAllBytes(str);
            }
            #endregion
            #region Compact
            else
            {
                int packetSize = this.TilingScheme.PacketSize;
                int RECORD_SIZE = 5;
                string baseUrl = this.Path;//D:\\arcgisserver\\arcgiscache\\inspur_shandong1\\图层
                long rowIndex = (row / packetSize) * packetSize;
                long colIndex = (col / packetSize) * packetSize;
                string filepath = string.Format(@"{0}\_alllayers\L{1:d2}\R{2:x4}C{3:x4}", baseUrl, level, rowIndex, colIndex);
                string bundlxFilename = string.Format("{0}.bundlx", filepath);
                string bundleFilename = string.Format("{0}.bundle", filepath);
                if (string.IsNullOrEmpty(bundlxFilename) || !File.Exists(bundlxFilename) || string.IsNullOrEmpty(bundleFilename) || !File.Exists(bundleFilename))
                    return null;
                try
                {
                    #region retrieve the tile offset from bundlx file
                    long tileStartRow = (row / packetSize) * packetSize;
                    long tileStartCol = (col / packetSize) * packetSize;
                    long recordNumber = (((packetSize * (col - tileStartCol)) + (row - tileStartRow)));
                    if (recordNumber < 0)
                        throw new ArgumentException("Invalid level / row / col");
                    long offset = 16 + (recordNumber * RECORD_SIZE);
                    byte[] idxData = new byte[5];
                    using (Stream bundlx = System.IO.File.OpenRead(bundlxFilename))
                    {
                        bundlx.Seek(offset, SeekOrigin.Begin);
                        bundlx.Read(idxData, 0, 5);
                    }
                    var bundleOffset = ((idxData[4] & 0xFF) << 32) | ((idxData[3] & 0xFF) << 24) |
                        ((idxData[2] & 0xFF) << 16) | ((idxData[1] & 0xFF) << 8) | ((idxData[0] & 0xFF));
                    #endregion
                    #region read tile length and tile data from bundle file
                    byte[] imgData;
                    using (Stream bundle = System.IO.File.OpenRead(bundleFilename))
                    {
                        bundle.Seek(bundleOffset, SeekOrigin.Begin);
                        byte[] buffer = new byte[4];
                        bundle.Read(buffer, 0, 4);
                        int recordLen = ((buffer[3] & 0xFF) << 24) | ((buffer[2] & 0xFF) << 16) | ((buffer[1] & 0xFF) << 8) | ((buffer[0] & 0xFF));
                        imgData = new byte[recordLen];
                        bundle.Read(imgData, 0, recordLen);
                    }
                    return imgData;
                    #endregion
                }
                catch
                {
                    return null;
                }
            }
            #endregion
        }

        #region IFormatConverter        
        /// <summary>
        /// Fire when converting completed.
        /// </summary>
        public event ConvertEventHandler ConvertCompleted;
        /// <summary>
        /// Fire when converting cancelled gracefully.
        /// </summary>
        public event EventHandler ConvertCancelled;
        /// <summary>
        /// convert ArcGIS Cache(exploded/compact) tiles to MBTiles format.
        /// </summary>
        /// <param name="outputPath">The output path and file name of .mbtiles file.</param>
        /// <param name="name">The plain-english name of the tileset, required by MBTiles.</param>
        /// <param name="description">A description of the tiles as plain text., required by MBTiles.</param>
        /// <param name="attribution">An attribution string, which explains in English (and HTML) the sources of data and/or style for the map., required by MBTiles.</param>
        /// <param name="doCompact">implementing the reducing redundant tile bytes part of MBTiles specification?</param>
        public void ConvertToMBTiles(string outputPath, string name, string description, string attribution,bool doCompact)
        {
            DirectoryInfo[] directories = new DirectoryInfo(Path + "\\_alllayers").GetDirectories("L*");
            int[] levels = new int[directories.Length];
            for (int i=0;i<directories.Length;i++)
            {
                levels[i] = int.Parse(directories[i].Name.Replace("L", ""));
            }
            try
            {
                DoConvertToMBTiles(outputPath, name, description, attribution, levels, TilingScheme.FullExtent, doCompact);
                if (ConvertCancelled != null && ConvertingStatus.IsCancelled)
                {
                    ConvertCancelled(this, new EventArgs());
                }
                if (ConvertCompleted != null)
                    ConvertCompleted(this, new ConvertEventArgs(ConvertingStatus.IsCompletedSuccessfully));
            }
            catch (Exception e)
            {
                throw new Exception("ArcGIS cache converting to MBTiles error!\r\n" + e.Message);
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
        /// not implemented, using ConvertToMBTiles(string outputPath, string name, string description, string attribution) instead
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="attribution"></param>
        /// <param name="levels"></param>
        /// <param name="geometry"></param>
        /// <param name="doCompact">implementing the reducing redundant tile bytes part of MBTiles specification?</param>
        public void ConvertToMBTiles(string outputPath, string name, string description, string attribution, int[] levels, Geometry geometry,bool doCompact)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
