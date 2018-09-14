//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using OSGeo.GDAL;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using PBS.Util;

namespace PBS.DataSource
{
    public class DataSourceRasterImage:DataSourceBase
    {
        ///dlls using:
        ///gdal main:gdal_csharp.dll,gdal_wrap.dll,gdal18.dll
        ///ecw format:NCSUtil4.dll,NCSEcw4_RO.dll,NCScnet4.dll,tbb.dll,gdal_ECW_JP2ECW.dll
        ///sid format:lti_lidar_dsdk.dll,lti_dsdk.dll,gdal_MrSID.dll
        ///c++ runtime:msvcr100.dll,msvcp100.dll
        public bool HasProjectionFromGDAL;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="tilingSchemePath"></param>
        /// <param name="serviceName">In case tile need to be cached to local file, using for determine the local cache file name.</param>
        public DataSourceRasterImage(string path, string tilingSchemePath,string serviceName)
        {
            TilingScheme = new TilingScheme();
            if (tilingSchemePath != null)
            {
                this.TilingScheme.Path = tilingSchemePath;
            }
            Initialize(path);
            if (ConfigManager.App_AllowFileCacheOfRasterImage)
            {
                //init local cache file if does not exist.
                string localCacheFileName = ConfigManager.App_FileCachePath + "\\" + serviceName.Trim().ToLower() + ".cache";
                ValidateLocalCacheFile(localCacheFileName);
                TileLoaded += new EventHandler<TileLoadEventArgs>(InternalOnTileLoaded);
            }
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.RasterImage.ToString();
            base.Initialize(path);

            try
            {                
                Gdal.AllRegister();                
            }
            catch (Exception e)
            {
                throw new Exception("Can not load GDAL assembly!\r\n" + e.Message);
            }
            //关于GDAL180中文路径不能打开的问题分析与解决:http://blog.csdn.net/liminlu0314/article/details/6610069
            Dataset ds = Gdal.Open(this.Path, Access.GA_ReadOnly);
            if (ds == null)
            {
                throw new Exception("Raster format is not supported.");
            }
            Driver drv = ds.GetDriver();
            if (drv == null)
            {
                throw new Exception("Can't get driver for gdal.");
            }
            if (ds.RasterCount < 3)
            {
                throw new Exception("The number of the raster bands is not enough.(bands count must = 3)");
            }            
            //提取两个sp第一个,之前的字符串中的所有字母和数字，判断是否想等
            ////if (ds.GetProjectionRef() != string.Empty)//GetProjectionRef() can't be trust
            ////{
            ////    HasProjectionFromGDAL = true;
            ////    if (!string.Equals(Regex.Replace(TilingScheme.WKT.Split(new char[] { ',' })[0], @"[^a-zA-Z0-9]", ""), Regex.Replace(ds.GetProjectionRef().Split(new char[] { ',' })[0], @"[^a-zA-Z0-9]", "")))
            ////        throw new Exception("The spatial reference in TilingScheme file is not equal to the spatial reference of Raster file!\r\nReprojection is not supported.");
            ////}
            else
            {
                HasProjectionFromGDAL = false;
            }
            ds.Dispose();
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            if (this.TilingScheme.Path==null)
            {
                ReadGoogleMapsTilingScheme(out tilingScheme);
            }
            else
            {
                ReadArcGISTilingSchemeFile(this.TilingScheme.Path, out tilingScheme);
            }
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            double xmin, ymin, xmax, ymax;
            Utility.CalculateBBox(TilingScheme.TileOrigin, TilingScheme.LODs[level].Resolution, TilingScheme.TileRows, TilingScheme.TileCols, row, col, out xmin, out ymin, out xmax, out ymax);
            using (Dataset ds = Gdal.Open(this.Path, Access.GA_ReadOnly))
            {
                //Dataset ds = Services[serviceName].RasterDataset;
                //reprojection if necessary
                //TODO:reproject raster
                double[] adfGeoTransform = new double[6];
                ds.GetGeoTransform(adfGeoTransform);
                double upperLeftPixelX, upperLeftPixelY, bottomRightPixelX, bottomRightPixelY;
                GDALConvertGeoCoordsToPixels(adfGeoTransform, xmin, ymax, out upperLeftPixelX, out upperLeftPixelY);
                GDALConvertGeoCoordsToPixels(adfGeoTransform, xmax, ymin, out bottomRightPixelX, out bottomRightPixelY);
                int tileRows = this.TilingScheme.TileRows;
                int tileCols = this.TilingScheme.TileCols;
                using (Bitmap bitmap = ReadRasterByGDAL(ds, upperLeftPixelX, upperLeftPixelY, bottomRightPixelX, bottomRightPixelY, tileCols, tileRows))
                {
                    if (bitmap == null)
                        return null;
                    //convert bitmap to byte[]
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        ms.Flush();
                        return ms.GetBuffer();
                    }
                }
            }           
        }

        private Bitmap ReadRasterByGDAL(Dataset ds, double upperLeftPixelX, double upperLeftPixelY, double bottomRightPixelX, double bottomRightPixelY, int imageWidth, int imageHeight)
        {
            //ref:http://trac.osgeo.org/gdal/browser/trunk/gdal/swig/csharp/apps/GDALReadDirect.cs
            Band redBand = ds.GetRasterBand(1);
            Band greenBand = ds.GetRasterBand(2);
            Band blueBand = ds.GetRasterBand(3);
            // Create a Bitmap to store the GDAL image in
            Bitmap bitmap = new Bitmap(imageWidth, imageHeight, PixelFormat.Format32bppRgb);
            // Use GDAL raster reading methods to read the image data directly into the Bitmap
            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, imageWidth, imageHeight), ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
            IntPtr buf = bitmapData.Scan0;
            IntPtr buf1 = new IntPtr(buf.ToInt32() + 1);
            IntPtr buf2 = new IntPtr(buf.ToInt32() + 2);
            try
            {
                int stride = bitmapData.Stride;                
                blueBand.ReadRaster((int)upperLeftPixelX, (int)upperLeftPixelY, (int)(bottomRightPixelX - upperLeftPixelX), (int)(bottomRightPixelY - upperLeftPixelY), buf, imageWidth, imageHeight, DataType.GDT_Byte, 4, stride);
                greenBand.ReadRaster((int)upperLeftPixelX, (int)upperLeftPixelY, (int)(bottomRightPixelX - upperLeftPixelX), (int)(bottomRightPixelY - upperLeftPixelY), buf1, imageWidth, imageHeight, DataType.GDT_Byte, 4, stride);
                redBand.ReadRaster((int)upperLeftPixelX, (int)upperLeftPixelY, (int)(bottomRightPixelX - upperLeftPixelX), (int)(bottomRightPixelY - upperLeftPixelY), buf2, imageWidth, imageHeight, DataType.GDT_Byte, 4, stride);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
                Utility.DeleteObject(buf);
                Utility.DeleteObject(buf1);
                Utility.DeleteObject(buf2);
            }

            ////ref:http://trac.osgeo.org/gdal/browser/trunk/gdal/swig/csharp/apps/GDALDatasetRasterIO.cs
            //// Create a Bitmap to store the GDAL image in
            //Bitmap bitmap = new Bitmap(imageWidth, imageHeight, PixelFormat.Format32bppRgb);
            //// Use GDAL raster reading methods to read the image data directly into the Bitmap
            //BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, imageWidth, imageHeight), ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);

            //try
            //{
            //    int stride = bitmapData.Stride;
            //    IntPtr buf = bitmapData.Scan0;

            //    ds.ReadRaster((int)upperLeftPixelX, (int)upperLeftPixelY, (int)(bottomRightPixelX - upperLeftPixelX), (int)(bottomRightPixelY - upperLeftPixelY), buf, imageWidth, imageHeight, DataType.GDT_Byte,
            //        ds.RasterCount, null, 4, stride, 1);
            //}
            //catch (Exception)
            //{
            //    return null;
            //}
            //finally
            //{
            //    bitmap.UnlockBits(bitmapData);
            //}
            return bitmap;
        }

        public static void GDALConvertPixelsToGeoCoords(double[] transform, double pixelX, double pixelY, out double geoX, out double geoY)
        {
            geoX = transform[0] + pixelX * transform[1] + pixelY * transform[2];
            geoY = transform[3] + pixelX * transform[4] + pixelY * transform[5];
        }

        public static void GDALConvertGeoCoordsToPixels(double[] transform, double geoX, double geoY, out double pixelX, out double pixelY)
        {
            //ref:GDAL栅格图像操作 http://blog.csdn.net/cleverysm/article/details/2147016
            pixelY = (geoY - transform[3] - transform[4] / transform[1] * (geoX - transform[0] - transform[2])) / (transform[5] - transform[4] / transform[1]);
            pixelX = (geoX - transform[0] - pixelY * transform[2]) / transform[1];
        }
    }
}
