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
    public class DataSourceArcGISTiledMapService:DataSourceBase
    {
        string[] _serviceInstaces = null;
        Hashtable _serviceInfoHashtable = null;

        public DataSourceArcGISTiledMapService(string path, string tilingSchemePath)
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
                    string result = wc.DownloadString(new Uri(service + "?f=json&ts="+DateTime.Now.Ticks, UriKind.Absolute));
                    if (lastresult != string.Empty && !string.Equals(lastresult, result))
                        throw new Exception("Services you entered are not exactly identical.");
                    Hashtable ht = JSON.JsonDecode(result) as Hashtable;
                    if (ht == null)
                        throw new Exception("Services does not return valid JSON result.");
                    if (ht["singleFusedMapCache"] == null || !(bool)ht["singleFusedMapCache"])
                        throw new Exception("It does not contain expected singleFusedMapCache:true information.");
                    else if (ht["tileInfo"]==null)
                        throw new Exception("You need to entered a cached map service url, not a Dynamic map service url.");
                    else if (ht["layers"]==null)
                        throw new Exception("It does not contain expected layers information.");
                    else if (ht["spatialReference"]==null)
                        throw new Exception("It does not contain expected spatialReference information.");
                    else if (ht["fullExtent"]==null)
                        throw new Exception("It does not contain expected fullExtent information.");

                    lastresult = result;
                    if (_serviceInfoHashtable == null)
                        _serviceInfoHashtable = ht;
                }
                catch (Exception e)
                {
                    throw new Exception(service + "\r\nerror on checking each service instance.\r\n" + e.Message);
                }
            }
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.ArcGISTiledMapService.ToString();
            base.Initialize(path);
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            ReadArcGISTiledMapServiceTilingScheme(_serviceInfoHashtable, out tilingScheme);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            string baseUrl = string.Empty;
            string subdomain = string.Empty;
            string uri = string.Empty;
            baseUrl = @"{0}/tile/{1}/{2}/{3}";
            subdomain = _serviceInstaces[(level + col + row) % _serviceInstaces.Length];
            uri = string.Format(baseUrl, subdomain, level, row, col);
            try
            {
                return HttpGetTileBytes(uri);
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
