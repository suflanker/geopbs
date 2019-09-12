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
    public class DataSourceArcGISCacheV2 : DataSourceBase, IFormatConverter
    {
        public DataSourceArcGISCacheV2(string path)
        {
            if (!Directory.Exists(path + "\\_alllayers"))
                throw new Exception("_alllayers directory does not exist!");
            var level0files = Directory.GetFiles(path + "\\_alllayers\\L00");
            var fileCount = level0files.Where(file => file.Contains(".bundlx")).Count();
            if (fileCount > 0)
                throw new Exception("此ArcGIS缓存为低版本，请选择另外的类型!");
            
            Initialize(path);            
            ConvertingStatus = new ConvertStatus();
        }

        ~DataSourceArcGISCacheV2()
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
                string baseUrl = this.Path;//D:\\arcgisserver\\arcgiscache\\inspur_shandong1\\图层
                int rowIndex = (row / packetSize) * packetSize;
                int colIndex = (col / packetSize) * packetSize;
                string filepath = string.Format(@"{0}\_alllayers\L{1:d2}\R{2:x4}C{3:x4}", baseUrl, level, rowIndex, colIndex);
                string bundleFilename = string.Format("{0}.bundle", filepath);
                if ( string.IsNullOrEmpty(bundleFilename) || !File.Exists(bundleFilename))
                    return null;
                try
                {
                    //r - rowGroup计算的是请求的瓦片在bundle中处在第几行
                    //c - columnGroup计算的是请求的瓦片在bundle中处在第几列
                    //总起来看，这句代码计算出了所请求瓦片在bundle中的位置
                    int index = 128 * (row - rowIndex) + (col - colIndex);
                    Stream isBundle = System.IO.File.OpenRead(bundleFilename);

                    isBundle.Seek(64 + 8 * index,SeekOrigin.Begin);
                    //获取位置索引并计算切片位置偏移量
                    byte[] indexBytes = new byte[4];
                    isBundle.Read(indexBytes, 0, 4);
                    long offset = (long)(indexBytes[0] & 0xff) + (long)(indexBytes[1] & 0xff) * 256 + (long)(indexBytes[2] & 0xff) * 65536
                            + (long)(indexBytes[3] & 0xff) * 16777216;

                    //获取切片长度索引并计算切片长度
                    long startOffset = offset - 4;
                    isBundle.Seek(startOffset, SeekOrigin.Begin);
                    byte[] lengthBytes = new byte[4];
                    isBundle.Read(lengthBytes, 0, 4);
                    int length = (int)(lengthBytes[0] & 0xff) + (int)(lengthBytes[1] & 0xff) * 256 + (int)(lengthBytes[2] & 0xff) * 65536
                            + (int)(lengthBytes[3] & 0xff) * 16777216;

                    //根据切片位置和切片长度获取切片

                    byte[] tileBytes = new byte[length];
                    int bytesRead = 0;
                    if (length > 0)
                    {
                        bytesRead = isBundle.Read(tileBytes, 0, tileBytes.Length);
                        if (bytesRead > 0)
                        {
                            return tileBytes;
                        }
                    }
                    return null;

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
