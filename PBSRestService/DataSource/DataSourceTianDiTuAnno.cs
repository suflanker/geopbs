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

namespace PBS.DataSource
{
    class DataSourceTianDiTuAnno:DataSourceBase
    {
        public DataSourceTianDiTuAnno()
        {
            Initialize("N/A");
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.TianDiTuAnnotation.ToString();
            base.Initialize(path);
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            ReadTianDiTuTilingScheme(out tilingScheme);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            string baseUrl = string.Empty;
            string[] subDomains = null;
            string subdomain = string.Empty;
            string uri = string.Empty;
            baseUrl = "http://tile{0}.tianditu.com/DataServer?T=AB0512_Anno&X={1}&Y={2}&L={3}";
            subDomains = new string[] { "0", "1", "2", "3", "4", "5", "6", "7" };
            subdomain = subDomains[(level + col + row) % subDomains.Length];
            if (level + 2 < 11)
                uri = string.Format(baseUrl, subdomain, col, row, level + 2);
            try
            {
                return HttpGetTileBytes(uri);
            }
            catch (Exception e)
            {
                //if server has response(not a downloading error) and tell pbs do not have the specific tile, return null
                if (e is WebException && (e as WebException).Response != null && ((e as WebException).Response as HttpWebResponse).StatusCode == HttpStatusCode.NotFound)
                    return null;

                string suffix = this.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
                Stream stream = this.GetType().Assembly.GetManifestResourceStream("PBS.Assets.badrequest" + this.TilingScheme.TileCols + "." + suffix);
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }
    }
}
