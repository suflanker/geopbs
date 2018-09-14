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
using System.Xml.Linq;
using PBS.Util;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Web;

namespace PBS.DataSource
{
    class DataSourceWMSService:DataSourceBase
    {
        string[] _serviceInstaces = null;
        string _xmlDoc = null;//GetCapabilities's xml result
        IList<LayerInfo> LayerList { get; set; }
        int[] SupportedSpatialReferenceIDs { get; set; }
        // Coordinate system WKIDs in WMS 1.3 where X,Y (Long,Lat) is switched to Y,X (Lat,Long)
        private static int[,] LatLongCRSRanges = new int[,] { { 4001, 4999 },
						{2044, 2045},   {2081, 2083},   {2085, 2086},   {2093, 2093},
						{2096, 2098},   {2105, 2132},   {2169, 2170},   {2176, 2180},
						{2193, 2193},   {2200, 2200},   {2206, 2212},   {2319, 2319},
						{2320, 2462},   {2523, 2549},   {2551, 2735},   {2738, 2758},
						{2935, 2941},   {2953, 2953},   {3006, 3030},   {3034, 3035},
						{3058, 3059},   {3068, 3068},   {3114, 3118},   {3126, 3138},
						{3300, 3301},   {3328, 3335},   {3346, 3346},   {3350, 3352},
						{3366, 3366},   {3416, 3416},   {20004, 20032}, {20064, 20092},
						{21413, 21423}, {21473, 21483}, {21896, 21899}, {22171, 22177},
						{22181, 22187}, {22191, 22197}, {25884, 25884}, {27205, 27232},
						{27391, 27398}, {27492, 27492}, {28402, 28432}, {28462, 28492},
						{30161, 30179}, {30800, 30800}, {31251, 31259}, {31275, 31279},
						{31281, 31290}, {31466, 31700} };
        private static Version highestSupportedVersion = new Version(1, 3);
        public string Version { get; set; }
        /// <summary>
        /// Gets a collection of image formats supported by the WMS service.
        /// </summary>
        /// <remarks>
        /// This property is only set after layer initialization completes and 
        /// <see cref="SkipGetCapabilities"/> is <c>false</c>.
        /// </remarks>
        public ReadOnlyCollection<string> SupportedImageFormats { get; private set; }
        /// <summary>
        /// Gets or sets the image format being used by the service.
        /// </summary>
        /// <remarks>
        /// The image format must be a supported MimeType name, supported by the service and the framework.
        /// </remarks>
        /// <example>
        /// <code>
        /// myWmsLayer.ImageFormat = "image/png";
        /// </code>
        /// </example>
        public string ImageFormat;
        //{
        //    get { return (string)GetValue(ImageFormatProperty); }
        //    set { SetValue(ImageFormatProperty, value); }
        //}
        /// <summary>
        /// getmap capability endpoint
        /// </summary>
        public string MapUrl { get; set; }

        private List<string> _layersArray = new List<string>();
        public string[] Layers
        {
            get { return _layersArray.ToArray(); }
            set
            {
                _layersArray = value.ToList();
                SetVisibleLayers();
            }
        }

        public DataSourceWMSService(string path,string tilingSchemePath)
        {
            this.Path = path;
            ValidateServices();
            LayerList = new ObservableCollection<LayerInfo>();
            ParseCapabilities(XDocument.Parse(_xmlDoc));
            TilingScheme = new TilingScheme();
            if (tilingSchemePath != null)
            {
                this.TilingScheme.Path = tilingSchemePath;
            }
            Initialize(path);
            if (!SupportedSpatialReferenceIDs.Contains(TilingScheme.WKID))
                throw new Exception(string.Format("Input WMS service doesn't support the WKID({0}) specified in tiling scheme file\r\n\r\nSupported WKIDs:{1}", TilingScheme.WKID,string.Join(",",SupportedSpatialReferenceIDs)));            
        }

        private void ValidateServices()
        {
            //http://sampleserver1.arcgisonline.com/ArcGIS/services/Specialty/ESRI_StatesCitiesRivers_USA/MapServer/WMSServer?service=WMS&request=GetCapabilities&version=1.3.0
            //invalidate each service instance
            _serviceInstaces = this.Path.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            string lastresult = string.Empty;
            foreach (string service in _serviceInstaces)
            {
                WebClient wc = new WebClient();
                try
                {
                    //timestamp for retrieving directly from server but not cache
                    string result = wc.DownloadString(new Uri(service + "?service=WMS&request=GetCapabilities&version=1.3.0&ts=" + DateTime.Now.Ticks, UriKind.Absolute));
                    if (lastresult != string.Empty && !string.Equals(lastresult, result))
                        throw new Exception("Services you entered are not exactly identical.");
                    if (!result.Contains("Capability") || !result.Contains("GetCapabilities") || !result.Contains("GetMap"))
                        throw new Exception("It does not contains one of GetMap/GetCapabilitiesGetMap node.");
                    lastresult = result;
                }
                catch (Exception e)
                {
                    throw new Exception(service + "\r\nis not a valid WMS Service!\r\n" + e.Message);
                }
            }
            _xmlDoc = lastresult;
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.OGCWMSService.ToString();
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
            string format = this.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
            //http://sampleserver1.arcgisonline.com/ArcGIS/services/Specialty/ESRI_StatesCitiesRivers_USA/MapServer/WMSServer?VERSION=1.3.0&REQUEST=GetMap&CRS=CRS:84&BBOX=-178.217598,18.924782,-66.969271,71.406235&WIDTH=765&HEIGHT=360&LAYERS=0,1,2&STYLES=,,&FORMAT=image/png  
            //subdomain = _serviceInstaces[(level + col + row) % _serviceInstaces.Length];
            
            StringBuilder queryString = new StringBuilder();
            if (!MapUrl.EndsWith("?"))
                MapUrl += "?";    
            queryString.Append("SERVICE=WMS&REQUEST=GetMap");
            queryString.AppendFormat("&WIDTH={0}", tileCols);
            queryString.AppendFormat("&HEIGHT={0}", tileRows);
            queryString.AppendFormat("&FORMAT={0}", "image"+"/"+format);
            queryString.AppendFormat("&LAYERS={0}", LayerList.Count == 0 ? "" : String.Join("%2C", GetVisibleLayers(LayerList).ToArray()));
            queryString.Append("&STYLES=");
            queryString.AppendFormat("&BGCOLOR={0}", "0xFFFFFF");
            queryString.AppendFormat("&TRANSPARENT={0}", "TRUE");
            queryString.AppendFormat("&VERSION={0}", GetValidVersionNumber());
            //If one of the WebMercator codes, change to a WKID supported by the service
            if (SupportedSpatialReferenceIDs != null &&
                (TilingScheme.WKID == 102100 || TilingScheme.WKID == 102113 || TilingScheme.WKID == 3857 || TilingScheme.WKID == 900913))
            {
                if (!SupportedSpatialReferenceIDs.Contains(TilingScheme.WKID))
                {
                    if (SupportedSpatialReferenceIDs.Contains(3857))
                        TilingScheme.WKID = 3857;
                    else if (SupportedSpatialReferenceIDs.Contains(102100))
                        TilingScheme.WKID = 102100;
                    else if (SupportedSpatialReferenceIDs.Contains(102113))
                        TilingScheme.WKID = 102113;
                    else if (SupportedSpatialReferenceIDs.Contains(900913))
                        TilingScheme.WKID = 900913;
                }
            }
            if (LowerThan13Version())
            {
                queryString.AppendFormat("&SRS=EPSG%3A{0}", TilingScheme.WKID);
                queryString.AppendFormat(CultureInfo.InvariantCulture,
                        "&bbox={0}%2C{1}%2C{2}%2C{3}", xmin, ymin, xmax, ymax);
            }
            else
            {
                queryString.AppendFormat("&CRS=EPSG%3A{0}", TilingScheme.WKID);
                if (UseLatLon(TilingScheme.WKID))
                    queryString.AppendFormat(CultureInfo.InvariantCulture,
                        "&BBOX={0}%2C{1}%2C{2}%2C{3}", ymin, xmin, ymax, xmax);
                else
                    queryString.AppendFormat(CultureInfo.InvariantCulture,
                        "&BBOX={0}%2C{1}%2C{2}%2C{3}", xmin, ymin, xmax, ymax);
            }
            uri = MapUrl  + queryString;
            //not asynchronize, to ensure only return result until download complete
            try
            {
                if (uri.Length <2048)
                {
                    return HttpGetTileBytes(uri);
                }
                else
                {
                    throw new Exception("PBS does not support to send POST request to WMS service currently.");
                    //if (MapUrl.Contains("ArcGIS/services"))
                    //    throw new Exception("ArcGIS Server does not allow to post request to WMS service.");
                    //return HttpPostTileBytes(MapUrl, queryString.ToString());
                }                
            }
            catch (Exception e)
            {
                Utility.Log(LogLevel.Error, e,"WMS export error");
                string suffix = this.TilingScheme.CacheTileFormat.ToString().Contains("PNG") ? "png" : "jpg";
                Stream stream = this.GetType().Assembly.GetManifestResourceStream("PBS.Assets.badrequest" + this.TilingScheme.TileCols + "." + suffix);
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }

        #region ESRI.ArcGIS.Client.Toolkit.DataSources.WMS
        
        private void ParseCapabilities(XDocument xDoc)
        {
            string ns = xDoc.Root.Name.NamespaceName;
            if (xDoc.Root.Attribute("version") != null)
                Version = xDoc.Root.Attribute("version").Value;
            //Get service info
            var info = (from Service in xDoc.Descendants(XName.Get("Service", ns))
                        select new
                        {
                            Title = Service.Element(XName.Get("Title", ns)) == null ? null : Service.Element(XName.Get("Title", ns)).Value,
                            Abstract = Service.Element(XName.Get("Abstract", ns)) == null ? null : Service.Element(XName.Get("Abstract", ns)).Value
                        }).First();
            //Get a list of layers
            var capabilities = xDoc.Descendants(XName.Get("Capability", ns)).FirstOrDefault();

            var layers = capabilities == null ? null : capabilities.Element(XName.Get("Layer", ns));
            var layerList = CreateLayerInfos(layers, ns);

            if (layerList != null)
            {
                foreach (LayerInfo i in layerList)
                    this.LayerList.Add(i);
            }
            //add by wzf
            foreach (LayerInfo layer in LayerList)
            {
                _layersArray.Add(layer.Name);
            }

            // Give a subLayerID to all layers
            int subLayerID = 0;
            foreach (var layerInfo in Descendants(LayerList))
                layerInfo.SubLayerID = subLayerID++;

            // Init Visibility from the list of Layers
            SetVisibleLayers();

            try
            {
                //Get endpoint for GetMap requests
                var request = (from c in capabilities.Descendants(XName.Get("Request", ns)) select c).First();
                var GetMaps = (from c in request.Descendants(XName.Get("GetMap", ns)) select c);
                var GetMap = (from c in GetMaps
                              where c.Descendants(XName.Get("Format", ns)).Select(t => (t.Value == "image/png" ||
                                  t.Value == "image/jpeg")).Count() > 0
                              select c).First();
                var formats = (from c in GetMaps.Descendants(XName.Get("Format", ns)) select c);
                //Silverlight only supports png and jpg. Prefer PNG, then JPEG
                SupportedImageFormats = new ReadOnlyCollection<string>(new List<string>(from c in formats where c.Value != null select c.Value));
                //OnPropertyChanged("SupportedImageFormats");
                if (ImageFormat == null)
                {
                    foreach (string f in new string[] { "image/png", "image/jpeg", "image/jpg"
#if !SILVERLIGHT
						//for WPF after PNG and JPEG, Prefer GIF, then any image, then whatever is supported
						,"image/gif","image/"
#endif
					})
                    {
                        ImageFormat = (from c in SupportedImageFormats where c.StartsWith(f) select c).FirstOrDefault();
                        if (ImageFormat != null) break;
                    }
#if !SILVERLIGHT
                    if (ImageFormat == null)
                        ImageFormat = (from c in formats where c.Value != null select c.Value).FirstOrDefault();
#endif
                }
                var DCPType = (from c in GetMap.Descendants(XName.Get("DCPType", ns)) select c).First();
                var HTTP = (from c in DCPType.Descendants(XName.Get("HTTP", ns)) select c).First();
                var Get = (from c in HTTP.Descendants(XName.Get("Get", ns)) select c).First();
                var OnlineResource = (from c in Get.Descendants(XName.Get("OnlineResource", ns)) select c).First();
                var href = OnlineResource.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));
                if (this.MapUrl == null)
                    this.MapUrl = href.Value;
            }
            catch
            {   //Default to WMS url
                if (this.MapUrl == null)
                    this.MapUrl = this.Path;
            }

            bool lowerThan13 = LowerThan13Version();
            List<int> supportedIDs = new List<int>();
            string key = lowerThan13 ? "SRS" : "CRS";
            IEnumerable<XElement> SRSs = xDoc.Descendants(XName.Get(key, ns));
            foreach (var element in SRSs)
            {
                if (element.Value != null && element.Value.StartsWith("EPSG:"))
                {
                    try
                    {
                        int srid = int.Parse(element.Value.Replace("EPSG:", ""), CultureInfo.InvariantCulture);
                        if (!supportedIDs.Contains(srid))
                            supportedIDs.Add(srid);
                    }
                    catch { }
                }
            }
            SupportedSpatialReferenceIDs = supportedIDs.ToArray();
        }

        private string GetValidVersionNumber()
        {
            try
            {
                Version providedVersion = new Version(Version);
                if (providedVersion <= highestSupportedVersion)
                    return Version;
            }
            catch { }
            return "1.3.0";
        }

        private bool LowerThan13Version()
        {
            try
            {
                Version providedVersion = new Version(Version);
                return (providedVersion < highestSupportedVersion);
            }
            catch { }
            return true;
        }

        private LayerInfo CreateLayerInfo(XElement layer, string ns)
        {
            LayerInfo layerInfo = new LayerInfo();
            layerInfo.Name = layer.Element(XName.Get("Name", ns)) == null ? null : layer.Element(XName.Get("Name", ns)).Value;
            layerInfo.Title = layer.Element(XName.Get("Title", ns)) == null ? null : layer.Element(XName.Get("Title", ns)).Value;
            layerInfo.Abstract = layer.Element(XName.Get("Abstract", ns)) == null ? null : layer.Element(XName.Get("Abstract", ns)).Value;
            layerInfo.ChildLayers = CreateLayerInfos(layer, ns); // recursive call for sublayers

            var style = layer.Element(XName.Get("Style", ns));
            if (style != null)
            {
                foreach (var legendUrl in style.Elements(XName.Get("LegendURL", ns)))
                {
                    var format = legendUrl.Element(XName.Get("Format", ns));
                    var onlineResource = legendUrl.Element(XName.Get("OnlineResource", ns));
                    if (format != null && onlineResource != null)
                    {
#if SILVERLIGHT
						if (format.Value != "image/png" && format.Value != "image/jpeg" && format.Value != "image/jpeg")
							continue;
#endif
                        var href = onlineResource.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));
                        if (href != null)
                        {
                            layerInfo.LegendUrl = href.Value;
                            break;
                        }
                    }
                }
            }
            // deal with ScaleDenominator from 1.3.0
            var minScaleDenominator = layer.Element(XName.Get("MinScaleDenominator", ns));
            if (minScaleDenominator != null)
            {
                double value;
                double.TryParse(minScaleDenominator.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
                layerInfo.MaximumScale = value;
            }
            var maxScaleDenominator = layer.Element(XName.Get("MaxScaleDenominator", ns));
            if (maxScaleDenominator != null)
            {
                double value;
                double.TryParse(maxScaleDenominator.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
                layerInfo.MinimumScale = value;
            }
            return layerInfo;
        }

        private IList<LayerInfo> CreateLayerInfos(XElement layers, string ns)
        {
            if (layers == null)
                return null;

            return (layers.Elements(XName.Get("Layer", ns)).Select(layer => CreateLayerInfo(layer, ns)).ToList());
        }

        private static bool UseLatLon(int extentWKID)
        {
            int length = LatLongCRSRanges.Length / 2;
            for (int count = 0; count < length; count++)
            {
                if (extentWKID >= LatLongCRSRanges[count, 0] && extentWKID <= LatLongCRSRanges[count, 1])
                    return true;
            }
            return false;
        }

        //private static string[] GetLayersArray(IEnumerable<LayerInfo> layerInfos)
        //{
        //    foreach (var info in layerInfos.Where(info => info.Visible))
        //    {
        //        if (info.ChildLayers == null || info.ChildLayers.Count == 0)
        //            yield return info.Name;
        //        else
        //            foreach (var i in GetVisibleLayers(info.ChildLayers))
        //                yield return i;
        //    }
        //}

        private static IEnumerable<string> GetVisibleLayers(IEnumerable<LayerInfo> layerInfos)
        {
            foreach (var info in layerInfos.Where(info => info.Visible))
            {
                if (info.ChildLayers == null || info.ChildLayers.Count == 0)
                    yield return info.Name;
                else
                    foreach (var i in GetVisibleLayers(info.ChildLayers))
                        yield return i;
            }
        }

        #region SetVisibleLayers

        /// <summary>
        /// Init the visibility of the layers from the visibleLayers array
        /// When a layer is in the array visibleLayers, it is visible (whatever the visibility of its parent) and all its descendants are visible
        /// so in the LayerTree we have to set the visibility for all ascendants and all descendants.
        /// </summary>
        internal void SetVisibleLayers()
        {
            // First pass : set all layers invisible
            foreach (var info in Descendants(LayerList))
                info.Visible = false;

            // Second pass : foreach layer in visibleLayers, set visible flag to all parents and all children
            foreach (var info in LayerList)
            {
                SetVisibleLayers(info);
            }
        }

        private void SetVisibleLayers(LayerInfo layerInfo)
        {
            if (Layers == null)
                return;

            bool visible = false;
            if (Layers.Contains(layerInfo.Name))
            {
                // the layer and all its children is visible
                visible = true;
                foreach (var child in Descendants(layerInfo.ChildLayers))
                    child.Visible = true;
            }
            else if (layerInfo.ChildLayers != null)
            {
                foreach (var child in layerInfo.ChildLayers)
                {
                    SetVisibleLayers(child);
                    visible |= child.Visible; // if a child is visible, all ascendants must be visible
                }
            }
            layerInfo.Visible = visible;
        }

        private static IEnumerable<LayerInfo> Descendants(IEnumerable<LayerInfo> layerInfos)
        {
            if (layerInfos == null)
                yield break;

            foreach (var info in layerInfos)
            {
                yield return info;

                foreach (var child in Descendants(info.ChildLayers))
                    yield return child;
            }
        }

        private static double ScaleHintToScale(double scaleHint)
        {
            const double inchesPerMeter = 10000.0 / 254.0; // = 39.37
            const double sqrt2 = 1.4142; // =Math.Sqrt(2.0)
            const int dpi = 96;
            const double ratio = dpi * inchesPerMeter / sqrt2;

            return scaleHint * ratio;
        }

        #endregion

        public sealed class LayerInfo
        {
            /// <summary>
            /// Gets the name of the layer.
            /// </summary>
            /// <value>The name.</value>
            public string Name { get; internal set; }
            /// <summary>
            /// Gets the title of the layer.
            /// </summary>
            /// <value>The title.</value>
            public string Title { get; internal set; }
            /// <summary>
            /// Gets the abstract for the layer.
            /// </summary>
            /// <value>The abstract.</value>
            public string Abstract { get; internal set; }
            /// <summary>
            /// Gets the extent of the layer.
            /// </summary>
            /// <value>The extent.</value>
            public Envelope Extent { get; internal set; }
            /// <summary>
            /// Gets the child layers.
            /// </summary>
            /// <value>The child layers.</value>
            public IList<LayerInfo> ChildLayers { get; internal set; }

            internal int SubLayerID { get; set; }
            internal bool Visible { get; set; }
            internal double MaximumScale { get; set; }
            internal double MinimumScale { get; set; }
            internal string LegendUrl { get; set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="LayerInfo"/> class.
            /// </summary>
            public LayerInfo() { }

            /// <summary>
            /// Initializes a new instance of the <see cref="LayerInfo"/> class.
            /// </summary>
            /// <param name="name">The name.</param>
            /// <param name="title">The title.</param>
            /// <param name="legendUrl">The legend URL.</param>
            public LayerInfo(string name, string title, string legendUrl)
            {
                Name = name;
                Title = title;
                LegendUrl = legendUrl;
            }
        }
        #endregion

    }
}
