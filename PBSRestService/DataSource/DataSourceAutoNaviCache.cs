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
    public class DataSourceAutoNaviCache:DataSourceBase
    {
        string _format;
        public DataSourceAutoNaviCache(string path)
        {
            Initialize(path);
            try
            {
                _format = new DirectoryInfo(path).GetDirectories()[0].GetDirectories()[0].GetDirectories()[0].GetFiles()[0].Extension;
            }
            catch (Exception)
            {
                throw new Exception("Folder structure error!");
            }
            if (!string.Equals(_format.ToLower(),".png")&&!string.Equals(_format.ToLower(),".gif")&&!string.Equals(_format.ToLower(),".jpg")&&!string.Equals(_format.ToLower(),".jpeg"))
                throw new Exception("file format error!\r\nformat=" + _format);
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.AutoNaviCache.ToString();
            base.Initialize(path);
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            ReadGoogleMapsTilingScheme(out tilingScheme);
            this.TilingScheme = tilingScheme;
            try
            {
                this.TilingScheme.InitialExtent =this.TilingScheme.FullExtent= GetGaodeCacheExtent();
            }
            catch (Exception e)
            {
                throw new Exception("Get init extent error!\r\n" + e.Message);
            }
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            if(!Directory.Exists(this.Path+"\\"+string.Format("{0:d2}",level)))
                return null;
            string colFolder = col.ToString().Substring(0, col.ToString().Length - 1);
            string rowFolder = row.ToString().Substring(0, row.ToString().Length - 1);
            string str = string.Format(@"{0}\{1:d2}\{2}\{3}\{4}_{5}{6}", this.Path, level, colFolder, rowFolder, col, row, _format);
            if (string.IsNullOrEmpty(str) || !File.Exists(str))
            {
                return null;
            }
            return File.ReadAllBytes(str);
        }

        /// <summary>
        /// get the cache extent by union the extent of upper-left tile and lower-right tile of the first cache level.
        /// </summary>
        /// <returns></returns>
        private Envelope GetGaodeCacheExtent()
        {
            DirectoryInfo diFirstCacheLevel = new DirectoryInfo(this.Path).GetDirectories()[0];
            DirectoryInfo diFirstFolder = diFirstCacheLevel.GetDirectories()[0].GetDirectories()[0];
            DirectoryInfo diLastFolder = diFirstCacheLevel.GetDirectories().Last().GetDirectories().Last();
            FileInfo fiUpperLeftTile = diFirstFolder.GetFiles()[0];
            FileInfo fiLowerRightTile = diLastFolder.GetFiles().Last();
            double resolution = this.TilingScheme.LODs[int.Parse(diFirstCacheLevel.Name)].Resolution;
            double xminUL, yminUL, xmaxUL, ymaxUL;
            int colUL = int.Parse(System.IO.Path.GetFileNameWithoutExtension(fiUpperLeftTile.Name).Split(new char[] { '_' })[0]);
            int rowUL = int.Parse(System.IO.Path.GetFileNameWithoutExtension(fiUpperLeftTile.Name).Split(new char[] { '_' })[1]);
            Utility.CalculateBBox(this.TilingScheme.TileOrigin, resolution, 256, 256, rowUL, colUL, out xminUL, out yminUL, out xmaxUL, out ymaxUL);
            double xminLR, yminLR, xmaxLR, ymaxLR;
            int colLR = int.Parse(System.IO.Path.GetFileNameWithoutExtension(fiLowerRightTile.Name).Split(new char[] { '_' })[0]);
            int rowLR = int.Parse(System.IO.Path.GetFileNameWithoutExtension(fiLowerRightTile.Name).Split(new char[] { '_' })[1]);
            Utility.CalculateBBox(this.TilingScheme.TileOrigin, resolution, 256, 256, rowLR, colLR, out xminLR, out yminLR, out xmaxLR, out ymaxLR);
            return new Envelope(xminUL, yminLR, xmaxLR, ymaxUL);
        }
    }
}
