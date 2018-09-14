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
using System.Threading.Tasks;
using System.Collections;
using PBS.Util;

namespace PBS.DataSource
{
    public class DataSourceArcGISImageService : DataSourceBase
    {
        string[] _serviceInstaces = null;

        public DataSourceArcGISImageService(string path, string tilingSchemePath)
        {
            if (path.Trim() == string.Empty)
                throw new Exception("service url is empty!");
            this.Path = path;
            ValidateServices();
            TilingScheme = new TilingScheme();
            if (tilingSchemePath != null)
            {
                this.TilingScheme.Path = tilingSchemePath;
            }
            Initialize(path);
        }

        private void ValidateServices()
        {
            //invalidate each service instance
            _serviceInstaces = this.Path.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            string lastresult = string.Empty;
            foreach (string service in _serviceInstaces)
            {
                WebClient wc = new WebClient();
                wc.Encoding = Encoding.UTF8;//in case the result including Chinese characters
                try
                {
                    //timestamp for retrieving directly from server but not cache
                    string result = wc.DownloadString(new Uri(service + "?f=json&ts=" + DateTime.Now.Ticks, UriKind.Absolute));
                    if (lastresult != string.Empty && !string.Equals(lastresult, result))
                        throw new Exception("Services you entered are not exactly identical.");
                    Hashtable ht = JSON.JsonDecode(result) as Hashtable;
                    if (ht == null)
                        throw new Exception("Services does not return valid JSON result.");
                    if (ht["pixelSizeX"]==null)
                        throw new Exception("It does not contain expected pixelSizeX information.");
                    else if (ht["bandCount"]==null)
                        throw new Exception("It does not contain expected bandCount information.");
                    else if (ht["serviceDataType"]==null)
                        throw new Exception("It does not contain expected serviceDataType information.");

                    lastresult = result;
                }
                catch (Exception e)
                {
                    throw new Exception(service + "\r\nerror on checking each service instance.\r\n" + e.Message);
                }
            }
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.ArcGISImageService.ToString();
            base.Initialize(path);
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            if (this.TilingScheme.Path == null)
            {
                ReadGoogleMapsTilingScheme(out tilingScheme);
            }
            else
            {
                ReadArcGISTilingSchemeFile(this.TilingScheme.Path, out tilingScheme);
            }
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);

            this.TilingScheme.CacheTileFormat = ImageFormat.JPG;
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            string baseUrl = string.Empty;
            string subdomain = string.Empty;
            string uri = string.Empty;
            //calculate the bbox
            double xmin, ymin, xmax, ymax;
            Utility.CalculateBBox(TilingScheme.TileOrigin, TilingScheme.LODs[level].Resolution, TilingScheme.TileRows, TilingScheme.TileCols, row, col, out xmin, out ymin, out xmax, out ymax);
            int tileRows = this.TilingScheme.TileRows;
            int tileCols = this.TilingScheme.TileCols;
            string format = this.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? this.TilingScheme.CacheTileFormat.ToString() : "jpg";
            baseUrl = @"{0}/exportImage?dpi=" + this.TilingScheme.DPI + @"&format=" + format + @"&bbox={1}%2C{2}%2C{3}%2C{4}&bboxSR=" + this.TilingScheme.WKID + "&imageSR=" + this.TilingScheme.WKID + "&size=" + tileCols + "%2C" + tileRows + "&f=image";
            subdomain = _serviceInstaces[(level + col + row) % _serviceInstaces.Length];
            uri = string.Format(baseUrl, subdomain, xmin, ymin, xmax, ymax);
            try
            {
                return HttpGetTileBytes(uri);
            }
            catch (Exception e)
            {
                //if server has response(not a downloading error) and tell pbs do not have the specific tile, return null
                if (e is WebException && (e as WebException).Response!=null&& ((e as WebException).Response as HttpWebResponse).StatusCode == HttpStatusCode.NotFound)
                    return null;

                string suffix = this.TilingScheme.CacheTileFormat.ToString().Contains("PNG") ? "png" : "jpg";
                Stream stream = this.GetType().Assembly.GetManifestResourceStream("PBS.Assets.badrequest" + this.TilingScheme.TileCols + "." + suffix);
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }
    }
}
