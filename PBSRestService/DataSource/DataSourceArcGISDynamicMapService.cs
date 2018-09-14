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
using System.Web;

namespace PBS.DataSource
{
    public class DataSourceArcGISDynamicMapService:DataSourceBase
    {
        private string _agsVersion;
        private string[] _supportedImageFormatTypes;

        public string AGSVersion { get{ return _agsVersion; }}
        public string[] SupportedImageFormatTypes { get { return _supportedImageFormatTypes; } }
        public string exportParam_layers { get; set; }
        public string exportParam_layerDefs { get; set; }
        public string exportParam_time { get; set; }
        public string exportParam_layerTimeOptions { get; set; }

        string[] _serviceInstaces = null;

        public DataSourceArcGISDynamicMapService(string path,string tilingSchemePath)
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
            Hashtable ht = null;
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
                    ht = JSON.JsonDecode(result) as Hashtable;
                    if (ht == null)
                        throw new Exception("Services does not return valid JSON result.");
                    if (ht["singleFusedMapCache"] == null || (bool)ht["singleFusedMapCache"])
                        throw new Exception("It does not contain valid singleFusedMapCache:false information.");
                    else if (ht["layers"]==null)
                        throw new Exception("It does not contain expected layers information.");
                    else if (ht["spatialReference"]==null)
                        throw new Exception("It does not contain expected spatialReference information.");
                    else if (ht["fullExtent"]==null)
                        throw new Exception("It does not contain expected fullExtent information.");

                    lastresult = result;
                }
                catch (Exception e)
                {
                    throw new Exception(service + "\r\nerror on checking each service instance.\r\n" + e.Message);
                }
            }
            _agsVersion = ht["currentVersion"] != null ? ht["currentVersion"].ToString() : "9.3";
            _supportedImageFormatTypes = ht["supportedImageFormatTypes"].ToString().Split(new char[]{','});
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.ArcGISDynamicMapService.ToString();
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
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            string subdomain = string.Empty;
            string uri = string.Empty;            
            //calculate the bbox
            double xmin, ymin, xmax, ymax;
            Utility.CalculateBBox(TilingScheme.TileOrigin, TilingScheme.LODs[level].Resolution, TilingScheme.TileRows, TilingScheme.TileCols, row, col, out xmin, out ymin, out xmax, out ymax);
            int tileRows = this.TilingScheme.TileRows;
            int tileCols = this.TilingScheme.TileCols;
            string format = this.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? this.TilingScheme.CacheTileFormat.ToString() : "jpg";
            StringBuilder queryString = new StringBuilder();
            queryString.Append("dpi=" + this.TilingScheme.DPI + "&");
            queryString.Append("transparent=true" + "&");
            queryString.Append("format=" + format + "&");
            queryString.Append("layers=" + HttpUtility.UrlEncode(exportParam_layers) + "&");
            queryString.Append("layerDefs=" + HttpUtility.UrlEncode(exportParam_layerDefs) + "&");
            queryString.Append("time=" + exportParam_time + "&");
            queryString.Append("layerTimeOptions=" + exportParam_layerTimeOptions + "&");
            queryString.AppendFormat("bbox={0}%2C{1}%2C{2}%2C{3}&",xmin,ymin,xmax,ymax);
            queryString.Append("bboxSR=" + this.TilingScheme.WKID + "&");
            queryString.Append("imageSR=" + this.TilingScheme.WKID + "&");
            queryString.Append("size=" + tileCols + "%2C" + tileRows + "&");
            queryString.Append("f=image");
            subdomain = _serviceInstaces[(level + col + row) % _serviceInstaces.Length];
            uri=subdomain+"/export?"+queryString;
            
            //not asynchronize, to ensure only return result until download complete
            try
            {
                if (uri.Length < 2048)
                {
                    return HttpGetTileBytes(uri);
                }
                else
                {
                    return HttpPostTileBytes(subdomain + "/export?", queryString.ToString());
                }
            }
            catch (Exception e)
            {
                //if server has response(not a downloading error) and tell pbs do not have the specific tile, return null
                if (e is WebException && (e as WebException).Response != null && ((e as WebException).Response as HttpWebResponse).StatusCode == HttpStatusCode.NotFound)
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
