//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel.Web;
using System.IO;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using PBS.DataSource;
using System.Collections;
using PBS.Util;
using System.Xml.Linq;
using System.Linq;

namespace PBS.Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false)]
    [ServiceThrottling(10000, 10000, 10000)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class PBSServiceProvider : IPBSServiceProvider
    {
        public Dictionary<string, PBSService> Services { get; set; }        public const string PBSName = "Portable Basemap Server";

        public PBSServiceProvider()
        {
            Services = new Dictionary<string, PBSService>();
                        PortEntity parentPortEntity;
            int requestPort = Utility.GetRequestPortNumber();            
            if (ServiceManager.PortEntities.TryGetValue(requestPort, out parentPortEntity))
            {
                Services = parentPortEntity.ServiceProvider.Services;
            }
                                    if (requestPort != -1 && !ServiceManager.PortEntities.ContainsKey(requestPort))
                throw new WebFaultException<string>("The request port does not exist in PBS. This can be caused by setting a url rewrite/revers proxy incorrectly.", HttpStatusCode.BadRequest);
        }

        ~PBSServiceProvider()
        {

        }

        public Stream ClientAccessPolicyFile()
        {
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/xml";
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
            string str = @"<?xml version=""1.0"" encoding=""utf-8"" ?> 
<access-policy>
<cross-domain-access>
<policy>
<allow-from http-request-headers=""*"">
<domain uri=""*""/>
<domain uri=""http://*""/>
</allow-from>
<grant-to>
<resource path=""/"" include-subpaths=""true""/>
</grant-to>
</policy>
</cross-domain-access>
</access-policy>";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
            return new MemoryStream(bytes);
        }

        public Stream CrossDomainFile()
        {
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/xml";
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
                        string str = @"<?xml version=""1.0"" ?> 
<cross-domain-policy>
 <allow-access-from domain=""*""/>
 <site-control permitted-cross-domain-policies=""all""/>
 <allow-http-request-headers-from domain=""*"" headers=""*""/>
</cross-domain-policy>";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
            return new MemoryStream(bytes);
        }

        public Stream GenerateArcGISServerInfo(string f, string callback)
        {
                        string str = @"{
 ""currentVersion"": 10.11,
 ""fullVersion"": ""10.1.1"",
 ""soapUrl"": ""http://none"",
 ""secureSoapUrl"": ""https://none"",
 ""authInfo"": {
  ""isTokenBasedSecurity"": true,
  ""tokenServicesUrl"": ""https://none"",
  ""shortLivedTokenValidity"": 60
 }
}";
            if (f != null && f.ToLower() == "pjson")
                str = str.Replace("\r\n", "").Replace("\n", "").Replace(" ", "");

            if (callback != null)
            {
                str = callback + "(" + str + ");";
            }
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain;charset=utf-8";
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            return StreamFromPlainText(str, true);
        }

        public Stream GenerateArcGISServerEndpointInfo(string f, string callback)
        {
                        string folders = string.Empty;
            string services = string.Empty;
            foreach (KeyValuePair<string, PBSService> kvp in Services)
            {
                                services += "{\"name\":\"" + kvp.Key + "\",\"type\":\"MapServer\"},\r\n";
            }
            if (!string.IsNullOrEmpty(services))
                services = services.Remove(services.Length - 3);            string str = @"{""currentVersion"" : 10.01, 
  ""folders"" : [
    " + folders + @"
  ], 
  ""services"" : [
    " + services + @"
  ]
}";
            if (f != null && f.ToLower() == "pjson")
                str = str.Replace("\r\n", "").Replace("\n", "").Replace(" ", "");

            if (callback != null)
            {
                str = callback + "(" + str + ");";
            }
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain;charset=utf-8";
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            return StreamFromPlainText(str, true);
        }

        public Stream GenerateArcGISServerEndpointInfo1(string f, string callback)
        {
            return GenerateArcGISServerEndpointInfo(f, callback);
        }

        public Stream GenerateArcGISServiceInfo(string serviceName, string f, string callBack)
        {
            if (Services != null)
            {
                string str = string.Empty;
                if (Services.ContainsKey(serviceName))
                {
                    WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain;charset=utf-8";
                    WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
                    if (f == "json")
                    {
                        str = Services[serviceName].DataSource.TilingScheme.RestResponseArcGISJson;
                    }
                    else if (f == "pjson")
                    {
                        str = Services[serviceName].DataSource.TilingScheme.RestResponseArcGISPJson;
                    }
                    else if (f == "jsapi")                    {
                        WebOperationContext.Current.OutgoingResponse.ContentType = "text/html; charset=utf-8";
                        #region jsapi
                        str = @"<!DOCTYPE html PUBLIC ""-<html>
<head>
<meta http-equiv=""X-UA-Compatible"" content=""IE=7"" />
  <title>ArcGIS JavaScript API: "+Services[serviceName].ServiceName+@"</title>
  <link href='http://services.arcgisonline.com/ArcGIS/rest/ESRI.ArcGIS.Rest.css' rel='stylesheet' type='text/css'>
<style type=""text/css"">
  @import ""http://serverapi.arcgisonline.com/jsapi/arcgis/2.8/js/dojo/dijit/themes/tundra/tundra.css"";
html, body { height: 100%; width: 100%; margin: 0; padding: 0; }
      .tundra .dijitSplitContainer-dijitContentPane, .tundra .dijitBorderContainer-dijitContentPane#navtable { 
        PADDING-BOTTOM: 5px; MARGIN: 0px 0px 3px; PADDING-TOP: 0px; BORDER-BOTTOM: #000 1px solid; BORDER-TOP: #000 1px solid; BACKGROUND-COLOR: #E5EFF7;
      }
      .tundra .dijitSplitContainer-dijitContentPane, .tundra .dijitBorderContainer-dijitContentPane#map {
        overflow:hidden; border:solid 1px black; padding: 0;
      }
      #breadcrumbs {
        PADDING-RIGHT: 0px; PADDING-LEFT: 11px; FONT-SIZE: 0.8em; FONT-WEIGHT: bold; PADDING-BOTTOM: 5px; MARGIN: 0px 0px 3px; PADDING-TOP: 0px;
      }
      #help {
        PADDING-RIGHT: 11px; PADDING-LEFT: 0px; FONT-SIZE: 0.70em; PADDING-BOTTOM: 5px; MARGIN: 0px 0px 3px; PADDING-TOP: 3px; 
</style>
<script type=""text/javascript"" src=""http://serverapi.arcgisonline.com/jsapi/arcgis?v=2.8""></script>
<script type=""text/javascript"">
  dojo.require(""esri.map"");
  dojo.require(""dijit.layout.ContentPane"");
  dojo.require(""dijit.layout.BorderContainer"");
  var map;
  function Init() {
    dojo.style(dojo.byId(""map""), { width: dojo.contentBox(""map"").w + ""px"", height: (esri.documentBox.h - dojo.contentBox(""navTable"").h - 40) + ""px"" });
    map = new esri.Map(""map"");
    var layer = new esri.layers.ArcGISTiledMapServiceLayer(""" + Services[serviceName].UrlArcGIS + @""");
    map.addLayer(layer);
var resizeTimer;
                            dojo.connect(map, 'onLoad', function(theMap) {
                              dojo.connect(dijit.byId('map'), 'resize', function() {
                                clearTimeout(resizeTimer);
                                resizeTimer = setTimeout(function() {
                                  map.resize();
                                  map.reposition();
                                 }, 500);
                               });
                             });
  }
  dojo.addOnLoad(Init);
</script>
</head>
<body class=""tundra"">
<table style=""width:100%"">
<tr>
<td>
<table id=""navTable"" width=""100%"">
<tbody>
<tr valign=""top"">
<td id=""breadcrumbs"">
ArcGIS JavaScript API: "+Services[serviceName].ServiceName+@"
</td>
<td align=""right"" id=""help"">
Built using the  <a href=""http://resources.esri.com/arcgisserver/apis/javascript/arcgis"">ArcGIS JavaScript API</a>
</td>
</tr>
</tbody>
</table>
</td>
</tr>
</table>
<div id=""map"" style=""margin:auto;width:97%;border:1px solid #000;""></div>
</body>
</html>
";
                        #endregion
                    }
                    else
                    {
                        str = "only support json/pjson/jsapi format! for instance: http://hostname:port/PBS/rest/servicename/MapServer?f=json[pjson||jsapi]";
                    }
                                                            if (callBack != null)
                    {
                        str = callBack + "(" + str + ");";
                    }
                }
                else
                {
                    str = serviceName + " service does not exist!";
                }
                return StreamFromPlainText(str);
            }
            return null;
        }
                public Stream GenerateArcGISServiceInfo(string serviceName, string operation, string f, string callBack)
        {
            if (Services != null)
            {
                string str = string.Empty;
                WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain;charset=utf-8";
                WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
                WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
                if (Services.ContainsKey(serviceName))
                {
                                                            str = @"{""error"":{""code"":400,""message"":""Unable to complete  operation."",""details"":[""" + operation + @" operation not supported on this service""]}}";
                    if (callBack != null)
                    {
                        str = callBack + "(" + str + ");";
                    }
                }
                else
                {
                    str = serviceName + " service does not exist!";
                }
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
                return new MemoryStream(bytes);
            }
            return null;
        }

        public Stream GenerateArcGISTile(string serviceName, string level, string row, string col)
        {
            #region loginfo
            string ip = Utility.GetRequestIPAddress();
            if (!Services[serviceName].LogInfo.RequestedIPs.Contains(ip))
            {
                Services[serviceName].LogInfo.RequestedIPs.Add(ip);
            }
            Services[serviceName].LogInfo.RequestedClientCounts = Services[serviceName].LogInfo.RequestedIPs.Count;
            Services[serviceName].LogInfo.LastRequestClientIP = ip;
            #endregion
            if (Services.ContainsKey(serviceName))
            {
                string suffix = Services[serviceName].DataSource.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
                WebOperationContext.Current.OutgoingResponse.ContentType = "image/" + suffix;
                WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
                WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
                if (Services[serviceName].DisableClientCache)
                {
                    WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
                    WebOperationContext.Current.OutgoingResponse.Headers.Add("Pragma", "no-cache");
                }
                else
                {
                                        CheckEtag(level, row, col);
                    SetEtag(level, row, col);
                }
                                                                                                                byte[] bytes = Task.Factory.StartNew<byte[]>(delegate()
                {
                    return GetTileStream(serviceName, level, row, col);
                }).Result;

                if (Services.ContainsKey(serviceName))                {
                    if (bytes != null)
                    {
                        MemoryStream ms = new MemoryStream(bytes);
                        return ms;
                    }
                    else if (Services[serviceName].DisplayNoDataTile)
                    {
                                                                        return this.GetType().Assembly.GetManifestResourceStream("PBS.Assets.missing" + Services[serviceName].DataSource.TilingScheme.TileCols + "." + suffix);
                    }
                }
            }
                        WebOperationContext.Current.OutgoingResponse.SetStatusAsNotFound("tile not exists");
            return null;
        }

        private byte[] GetTileStream(string serviceName, string level, string row, string col)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            if (Services.ContainsKey(serviceName))
            {
                byte[] bytes = null;
                TileLoadEventArgs tileLEA = new TileLoadEventArgs()
                {
                    Level=int.Parse(level),
                    Row=int.Parse(row),
                    Column=int.Parse(col)
                };
                string key = Services[serviceName].Port + serviceName + level + row + col + "_" + Services[serviceName].MemcachedValidKey;
                                if (ServiceManager.Memcache != null && ServiceManager.Memcache.IsActived && Services[serviceName].AllowMemCache && ServiceManager.Memcache.MC.KeyExists(key))
                {
                    bytes = (byte[])ServiceManager.Memcache.MC.Get(key);
                    if (bytes != null)
                    {
#if Debug
                        System.Diagnostics.Debug.WriteLine("from Memcached/" + bytes.Length);
#endif
                        Services[serviceName].LogInfo.OutputTileCountMemcached++;
                        Services[serviceName].LogInfo.OutputTileTotalTime += sw.Elapsed.TotalMilliseconds;
                        tileLEA.GeneratedMethod = TileGeneratedSource.FromMemcached;
                    }
                }
                                if (bytes == null && (Services[serviceName].DataSource.IsOnlineMap ||
                    Services[serviceName].DataSource is DataSourceRasterImage))
                {
                    bytes = Services[serviceName].DataSource.GetTileBytesFromLocalCache(int.Parse(level), int.Parse(row), int.Parse(col));
                    if (bytes != null)
                    {
#if Debug
                        System.Diagnostics.Debug.WriteLine("from FileCache/" + bytes.Length);
#endif
                        Services[serviceName].LogInfo.OutputTileCountFileCache++;
                        Services[serviceName].LogInfo.OutputTileTotalTime += sw.Elapsed.TotalMilliseconds;
                        tileLEA.GeneratedMethod = TileGeneratedSource.FromFileCache;
                    }
                }
                                if (bytes == null)
                {
                    bytes = Services[serviceName].DataSource.GetTileBytes(int.Parse(level), int.Parse(row), int.Parse(col));
                    Services[serviceName].LogInfo.OutputTileCountDynamic++;
                    Services[serviceName].LogInfo.OutputTileTotalTime += sw.Elapsed.TotalMilliseconds;
                    tileLEA.GeneratedMethod = TileGeneratedSource.DynamicOutput;
                }
                                if (Services[serviceName].Style != VisualStyle.None)
                    bytes = PBS.Util.Utility.MakeShaderEffect(bytes, Services[serviceName].Style);
                                if (Services[serviceName].DataSource.TileLoaded != null)
                {
                    tileLEA.TileBytes = bytes;
                    Services[serviceName].DataSource.TileLoaded(Services[serviceName].DataSource, tileLEA);
                }
                                if (ServiceManager.Memcache != null && ServiceManager.Memcache.IsActived && Services[serviceName].AllowMemCache)
                    ServiceManager.Memcache.MC.Set(key, bytes);
                sw.Stop();
                return bytes;
            }
            return null;
        }

        public Stream GenerateWMTSTileRESTful(string serviceName, string version, string layer, string style, string tilematrixset, string tilematrix, string row, string col, string format)
        {
            if(Services==null||!Services.ContainsKey(serviceName))
                return null;
            string suffix = Services[serviceName].DataSource.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
            if ( !string.Equals(version, "1.0.0") || !string.Equals(serviceName, layer)||!string.Equals(suffix,format))
                return null;
            return GenerateArcGISTile(serviceName, tilematrix, row, col);
        }

        public Stream GenerateWMTSTileKVP(string serviceName, string version, string layer, string style, string tilematrixset, string tilematrix, string row, string col, string format)
        {
            if (Services == null || !Services.ContainsKey(serviceName))
                return null;
            string suffix = Services[serviceName].DataSource.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? "png" : "jpg";
            if (!string.Equals(version, "1.0.0") || !string.Equals(serviceName, layer) || !string.Equals(suffix, format.Split(new char[]{'/'})[1]))
                return null;
            return GenerateArcGISTile(serviceName, tilematrix, row, col);
        }

        public Stream GenerateWMTSCapabilitiesRESTful(string serviceName, string version)
        {
            if (Services == null)
                return null;
            if(!Services.ContainsKey(serviceName))
                throw new WebFaultException<string>(string.Format("The '{0}' service does not exist!",serviceName), HttpStatusCode.BadRequest);
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/xml";
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
            string result;
            string key = Services[serviceName].Port + serviceName + "WMTSCapabilities" + "_" + Services[serviceName].MemcachedValidKey;
            string wmtsVersion = "1.0.0";
            if (!string.Equals(version, wmtsVersion))
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                result = @"<?xml version=""1.0"" encoding=""utf-8"" ?> 
<result>
Invalid version!
</result>";
            }
            else
            {
                string tileMatrixSetName = "default028mm";
                string tileFormat=Services[serviceName].DataSource.TilingScheme.CacheTileFormat.ToString().ToLower().Contains("png")?"png":"jpg";
                PBSService service=Services[serviceName];
                                if (ServiceManager.Memcache != null && ServiceManager.Memcache.IsActived && Services[serviceName].AllowMemCache && ServiceManager.Memcache.MC.KeyExists(key))
                    return new MemoryStream((byte[])ServiceManager.Memcache.MC.Get(key));
                                        double INCHES_PER_METER = 39.37;
		double PIXEL_SIZE = 0.00028; 		double DPI_WMTS = 1.0/INCHES_PER_METER/PIXEL_SIZE;         Envelope wgs84boundingbox = null;
                if (Math.Abs(service.DataSource.TilingScheme.TileOrigin.X) < 600)        {
            wgs84boundingbox = service.DataSource.TilingScheme.FullExtent;
        }
        else if (service.DataSource.TilingScheme.WKID == 102100 || service.DataSource.TilingScheme.WKID == 102113 || service.DataSource.TilingScheme.WKID == 3857)        {
            Point geoLowerLeft=Utility.WebMercatorToGeographic(service.DataSource.TilingScheme.FullExtent.LowerLeft);
            Point geoUpperRight=Utility.WebMercatorToGeographic(service.DataSource.TilingScheme.FullExtent.UpperRight);

            wgs84boundingbox = new Envelope(geoLowerLeft.X, geoLowerLeft.Y, geoUpperRight.X, geoUpperRight.Y);
        }
        bool isGoogleMapsCompatible = (service.DataSource.TilingScheme.WKID == 102100 || service.DataSource.TilingScheme.WKID == 102113 || service.DataSource.TilingScheme.WKID == 3857) && service.DataSource.TilingScheme.TileCols == 256 && service.DataSource.TilingScheme.TileRows == 256 && service.DataSource.TilingScheme.DPI == 96;
                XNamespace def = "http://www.opengis.net/wmts/1.0";
                XNamespace ows = "http://www.opengis.net/ows/1.1";
                XNamespace xlink="http://www.w3.org/1999/xlink";
                XNamespace xsi="http://www.w3.org/2001/XMLSchema-instance";
                XNamespace gml="http://www.opengis.net/gml";
                XNamespace schemaLocation = "http://www.opengis.net/wmts/1.0 http://schemas.opengis.net/wmts/1.0/wmtsGetCapabilities_response.xsd";
                XElement root = new XElement(def + "Capabilities",
                        new XAttribute("xmlns", def),
                        new XAttribute(XNamespace.Xmlns + "ows", ows),
                        new XAttribute(XNamespace.Xmlns + "xlink", xlink),
                        new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                        new XAttribute(XNamespace.Xmlns + "gml", gml),
                        new XAttribute(xsi + "schemaLocation", schemaLocation),
                        new XAttribute("version", wmtsVersion),
                        new XComment(" Service Identification "),
                        new XElement(ows + "ServiceIdentification",                            
                            new XElement(ows + "Title", serviceName),
                            new XElement(ows + "ServiceType", "OGC WMTS"),
                            new XElement(ows + "ServiceTypeVersion", wmtsVersion)),
                        new XElement(ows+"ServiceProvider",
                            new XElement(ows+"ProviderName",PBSName),
                            new XElement(ows+"ProviderSite",
                                new XAttribute(xlink+"href","https://geopbs.codeplex.com/")),
                            new XElement(ows+"ServiceContact",
                                new XElement(ows+"IndividualName","diligentpig"))),
                        new XComment(" Operations Metadata "),
                        new XElement(ows+"OperationsMetadata",
                            new XElement(ows+"Operation",
                                new XAttribute("name","GetCapabilities"),
                                new XElement(ows+"DCP",
                                    new XElement(ows+"HTTP",
                                        new XElement(ows+"Get",
                                            new XAttribute(xlink + "href", string.Format("{0}/WMTS/{1}/WMTSCapabilities.xml", service.UrlArcGIS, wmtsVersion)),
                                            new XElement(ows+"Constraint",
                                                new XAttribute("name","GetEncoding"),
                                                new XElement(ows+"AllowedValues",
                                                    new XElement(ows+"Value","RESTful")))),
                                        new XElement(ows + "Get",
                                            new XAttribute(xlink + "href", string.Format("{0}/WMTS?", service.UrlArcGIS)),
                                            new XElement(ows + "Constraint",
                                                new XAttribute("name", "GetEncoding"),
                                                new XElement(ows + "AllowedValues",
                                                    new XElement(ows + "Value", "KVP"))))
                                                    ))),
                            new XElement(ows+"Operation",
                                new XAttribute("name","GetTile"),
                                new XElement(ows+"DCP",
                                    new XElement(ows+"HTTP",
                                        new XElement(ows+"Get",
                                            new XAttribute(xlink + "href", string.Format("{0}/WMTS/tile/{1}/", service.UrlArcGIS, wmtsVersion)),
                                            new XElement(ows+"Constraint",
                                                new XAttribute("name","GetEncoding"),
                                                new XElement(ows+"AllowedValues",
                                                    new XElement(ows+"Value","RESTful")))),
                                       new XElement(ows + "Get",
                                            new XAttribute(xlink + "href", string.Format("{0}/WMTS?", service.UrlArcGIS)),
                                            new XElement(ows + "Constraint",
                                                new XAttribute("name", "GetEncoding"),
                                                new XElement(ows + "AllowedValues",
                                                    new XElement(ows + "Value", "KVP"))))
                                                    )))),
                        new XElement(def+"Contents",
                            new XComment("Layer"),
                            new XElement(def+"Layer",
                                new XElement(ows+"Title",serviceName),
                                new XElement(ows+"Identifier",serviceName),
                                new XElement(ows + "BoundingBox",
                                    new XAttribute("crs",string.Format("urn:ogc:def:crs:EPSG::{0}",service.DataSource.TilingScheme.WKID)),
                                    new XElement(ows + "LowerCorner", service.DataSource.TilingScheme.FullExtent.LowerLeft.X + " " + service.DataSource.TilingScheme.FullExtent.LowerLeft.Y),
                                    new XElement(ows + "UpperCorner", service.DataSource.TilingScheme.FullExtent.UpperRight.X + " " + service.DataSource.TilingScheme.FullExtent.UpperRight.Y)),
                                                                wgs84boundingbox!=null ?
                                new XElement(ows + "WGS84BoundingBox",
                                    new XAttribute("crs", "urn:ogc:def:crs:OGC:2:84"),
                                    new XElement(ows + "LowerCorner", wgs84boundingbox.LowerLeft.X + " " + wgs84boundingbox.LowerLeft.Y),
                                    new XElement(ows + "UpperCorner", wgs84boundingbox.UpperRight.X + " " + wgs84boundingbox.UpperRight.Y)) : null,
                                new XElement(def+"Style",
                                    new XAttribute("isDefault","true"),
                                    new XElement(ows+"Title","Default Style"),
                                    new XElement(ows+"Identifier","default")),
                                new XElement(def+"Format","image/"+tileFormat),
                                new XElement(def+"TileMatrixSetLink",
                                    new XElement(def+"TileMatrixSet",tileMatrixSetName)),
                                new XElement(def + "TileMatrixSetLink",
                                    new XElement(def + "TileMatrixSet", "nativeTileMatrixSet")),
                                                                isGoogleMapsCompatible?
                                new XElement(def + "TileMatrixSetLink",
                                    new XElement(def + "TileMatrixSet", "GoogleMapsCompatible")) : null,
                                new XElement(def+"ResourceURL",
                                    new XAttribute("format","image/"+tileFormat),
                                    new XAttribute("resourceType","tile"),
                                    new XAttribute("template", string.Format("{0}/WMTS/tile/{1}/{2}/{{Style}}/{{TileMatrixSet}}/{{TileMatrix}}/{{TileRow}}/{{TileCol}}.{3}", service.UrlArcGIS, wmtsVersion, serviceName, tileFormat)))),
                            new XComment("TileMatrixSet"),
                                                        new XElement(def+"TileMatrixSet",
                                new XElement(ows + "Title", "Default TileMatrix using 0.28mm"),
                                new XElement(ows + "Abstract", "The tile matrix set that has scale values calculated based on the dpi defined by OGC specification (dpi assumes 0.28mm as the physical distance of a pixel)."),
                                new XElement(ows+"Identifier",tileMatrixSetName),
                                                    new XElement(ows+"SupportedCRS",string.Format("urn:ogc:def:crs:EPSG::{0}",service.DataSource.TilingScheme.WKID)),
                                from lod in service.DataSource.TilingScheme.LODs
                                let coords = getBoundaryTileCoords(service.DataSource.TilingScheme, lod)
                                select new XElement(def+"TileMatrix",
                                    new XElement(ows+"Identifier",lod.LevelID),
                                                                                                                                                new XElement(def + "ScaleDenominator", (lod.Scale * 25.4) / (0.28 * service.DataSource.TilingScheme.DPI)),
                                                                        new XElement(def + "TopLeftCorner", UseLatLon(service.DataSource.TilingScheme.WKID) ? service.DataSource.TilingScheme.TileOrigin.Y + " " + service.DataSource.TilingScheme.TileOrigin.X : service.DataSource.TilingScheme.TileOrigin.X + " " + service.DataSource.TilingScheme.TileOrigin.Y),
                                    new XElement(def + "TileWidth", service.DataSource.TilingScheme.TileCols),
                                    new XElement(def + "TileHeight", service.DataSource.TilingScheme.TileRows),
                                                                        new XElement(def + "MatrixWidth", coords[3]- coords[1]+ 1),
                                    new XElement(def + "MatrixHeight", coords[2] - coords[0] + 1))),
                                                        new XElement(def + "TileMatrixSet",
                                new XElement(ows + "Title", "Native TiledMapService TileMatrixSet"),
                                new XElement(ows + "Abstract", string.Format("the tile matrix set that has scale values calculated based on the dpi defined by ArcGIS Server tiled map service. The current tile dpi is {0}",service.DataSource.TilingScheme.DPI)),
                                new XElement(ows + "Identifier", "nativeTileMatrixSet"),
                                new XElement(ows + "SupportedCRS", string.Format("urn:ogc:def:crs:EPSG::{0}", service.DataSource.TilingScheme.WKID)),
                                from lod in service.DataSource.TilingScheme.LODs
                                let coords = getBoundaryTileCoords(service.DataSource.TilingScheme, lod)
                                select new XElement(def + "TileMatrix",
                                    new XElement(ows + "Identifier", lod.LevelID),
                                    new XElement(def + "ScaleDenominator", lod.Scale),
                                    new XElement(def + "TopLeftCorner", UseLatLon(service.DataSource.TilingScheme.WKID) ? service.DataSource.TilingScheme.TileOrigin.Y + " " + service.DataSource.TilingScheme.TileOrigin.X : service.DataSource.TilingScheme.TileOrigin.X + " " + service.DataSource.TilingScheme.TileOrigin.Y),
                                    new XElement(def + "TileWidth", service.DataSource.TilingScheme.TileCols),
                                    new XElement(def + "TileHeight", service.DataSource.TilingScheme.TileRows),
                                    new XElement(def + "MatrixWidth", coords[3] - coords[1] + 1),
                                    new XElement(def + "MatrixHeight", coords[2] - coords[0] + 1))),
                                                        isGoogleMapsCompatible?
                            new XElement(def + "TileMatrixSet",
                                new XElement(ows + "Title", "GoogleMapsCompatible"),
                                new XElement(ows + "Abstract", "the wellknown 'GoogleMapsCompatible' tile matrix set defined by OGC WMTS specification"),
                                new XElement(ows + "Identifier", "GoogleMapsCompatible"),
                                new XElement(ows + "SupportedCRS", "urn:ogc:def:crs:EPSG:6.18:3:3857"),
                                new XElement(def + "WellKnownScaleSet", "urn:ogc:def:wkss:OGC:1.0:GoogleMapsCompatible"),
                                from level in new int[] {0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18}
                                select new XElement(def + "TileMatrix",
                                    new XElement(ows + "Identifier", level),
                                    new XElement(def + "ScaleDenominator", 559082264.0287178/Math.Pow(2,level)),
                                    new XElement(def + "TopLeftCorner", "-20037508.34278925 20037508.34278925"),
                                    new XElement(def + "TileWidth", "256"),
                                    new XElement(def + "TileHeight","256"),
                                    new XElement(def + "MatrixWidth", Math.Pow(2,level)),
                                    new XElement(def + "MatrixHeight", Math.Pow(2, level)))) : null
                            ),
                        new XElement(def+"ServiceMetadataURL",
                            new XAttribute(xlink + "href",string.Format("{0}/WMTS/{1}/WMTSCapabilities.xml",service.UrlArcGIS,version)))                                
                        );                
                result = root.ToString();
                            }            
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                        if (ServiceManager.Memcache != null && ServiceManager.Memcache.IsActived && Services[serviceName].AllowMemCache)
                ServiceManager.Memcache.MC.Set(key, bytes);
            return new MemoryStream(bytes);
        }

        public Stream GenerateWMTSCapabilitiesRedirect(string serviceName)
        {
            if (Services.ContainsKey(serviceName))
            {
                                        WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.Redirect;
                    WebOperationContext.Current.OutgoingResponse.Location = Services[serviceName].UrlWMTS + "/1.0.0/WMTSCapabilities.xml";
                WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            }
            return null;
        }

        public Stream GenerateWMTSCapabilitiesKVP(string serviceName, string version)
        {
            return GenerateWMTSCapabilitiesRESTful(serviceName, version);
        }

        #region wmts private method
                private static readonly int[,] _latLongCrsRanges = new[,]
		                                                   	{
		                                                   		{4001, 4999},
		                                                   		{2044, 2045}, {2081, 2083}, {2085, 2086}, {2093, 2093},
		                                                   		{2096, 2098}, {2105, 2132}, {2169, 2170}, {2176, 2180},
		                                                   		{2193, 2193}, {2200, 2200}, {2206, 2212}, {2319, 2319},
		                                                   		{2320, 2462}, {2523, 2549}, {2551, 2735}, {2738, 2758},
		                                                   		{2935, 2941}, {2953, 2953}, {3006, 3030}, {3034, 3035},
		                                                   		{3058, 3059}, {3068, 3068}, {3114, 3118}, {3126, 3138},
		                                                   		{3300, 3301}, {3328, 3335}, {3346, 3346}, {3350, 3352},
		                                                   		{3366, 3366}, {3416, 3416}, {20004, 20032}, {20064, 20092},
		                                                   		{21413, 21423}, {21473, 21483}, {21896, 21899}, {22171, 22177},
		                                                   		{22181, 22187}, {22191, 22197}, {25884, 25884}, {27205, 27232},
		                                                   		{27391, 27398}, {27492, 27492}, {28402, 28432}, {28462, 28492},
		                                                   		{30161, 30179}, {30800, 30800}, {31251, 31259}, {31275, 31279},
		                                                   		{31281, 31290}, {31466, 31700}
		                                                   	};        
        private static bool UseLatLon(int wkid)
        {
            int length = _latLongCrsRanges.Length / 2;
            for (int count = 0; count < length; count++)
            {
                if (wkid >= _latLongCrsRanges[count, 0] && wkid <= _latLongCrsRanges[count, 1])
                    return true;
            }
            return false;
        }
        private double GetScale(double resolution, double dpi, TilingScheme ts)
        {
                        if (Math.Abs(ts.TileOrigin.X) > 600)                return resolution * dpi / 2.54 * 100;
            else            {
                double meanY = (ts.FullExtent.YMax + ts.FullExtent.YMin) / 2;
                double R = 6378137 *Math.Cos(meanY / 180 * 3.14);
                double dgreeResolution = 2 * 3.14 * R / 360;
                return dgreeResolution * resolution * dpi / 2.54 * 100;
            }
        }
                public static int[] getBoundaryTileCoords(TilingScheme tilingScheme, LODInfo lod)
        {
            double x = tilingScheme.TileOrigin.X;
            double y = tilingScheme.TileOrigin.Y;
            int rows = tilingScheme.TileRows;
            int cols = tilingScheme.TileCols;
            double resolution = lod.Resolution;
            double tileMapW = cols * resolution;
            double tileMapH = rows * resolution;

            double exmin = tilingScheme.FullExtent.XMin;
            double eymin = tilingScheme.FullExtent.YMin;
            double exmax = tilingScheme.FullExtent.XMax;
            double eymax = tilingScheme.FullExtent.YMax;

            int startRow = (int)Math.Floor((y - eymax) / tileMapH);
            int startCol = (int)Math.Floor((exmin - x) / tileMapW);
            int endRow = (int)Math.Floor((y - eymin) / tileMapH);
            int endCol = (int)Math.Floor((exmax - x) / tileMapW);
            return new int[] { startRow < 0 ? 0 : startRow, startCol < 0 ? 0 : startCol, endRow < 0 ? 0 : endRow, endCol < 0 ? 0 : endCol };
        }
        #endregion

        private void CheckEtag(string level, string row, string col)
        {
                                    if (WebOperationContext.Current.IncomingRequest.Headers[HttpRequestHeader.IfNoneMatch] != null)
            {
                                string etag = WebOperationContext.Current.IncomingRequest.Headers[HttpRequestHeader.IfNoneMatch].Split(new string[] { "\"" }, StringSplitOptions.RemoveEmptyEntries)[0];                try                {
                    string oriEtag = Encoding.UTF8.GetString(Convert.FromBase64String(etag));
                    if (oriEtag == level + row + col)
                    {
                        WebOperationContext.Current.OutgoingResponse.SuppressEntityBody = true;                        WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotModified;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Checking request's etag error.\r\n" + e.Message);
                }
            }
        }
        private void SetEtag(string level, string row, string col)
        {
            WebOperationContext.Current.OutgoingResponse.Headers.Add(HttpResponseHeader.Expires, DateTime.Now.AddHours(24).ToUniversalTime().ToString("r"));                        string oriEtag = level + row + col;
                        string etag = Convert.ToBase64String(Encoding.UTF8.GetBytes(oriEtag));
            WebOperationContext.Current.OutgoingResponse.SetETag(etag);
        }

        private Stream StreamFromPlainText(string content, bool disableCache=false)
        {            
            WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
            if (disableCache)
            {
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Pragma", "no-cache");
            }
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        #region admin api
        private string AuthenticateAndParseParams(Stream requestBody,out Hashtable ht,bool allowEmptyRequestBody=false)
        {
                        ht = null;
            string authResult = string.Empty;
                        if (WebOperationContext.Current.IncomingRequest.Method != "POST")
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.MethodNotAllowed;
                WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
                authResult = @"{
    ""success"": false,
    ""message"": ""This operation is only supported via POST!""
}";
                return authResult;
            }
                        if (WebOperationContext.Current.IncomingRequest.Headers[HttpRequestHeader.Authorization] == null)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Unauthorized;
                                                                authResult = @"{
    ""success"": false,
    ""message"": ""Authorization required!""
}";
                return authResult;
            }
                        byte[] bytes = null;
            try
            {
                bytes = Convert.FromBase64String(WebOperationContext.Current.IncomingRequest.Headers[HttpRequestHeader.Authorization]);
            }
            catch (Exception)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Unauthorized;
                authResult = @"{
    ""success"": false,
    ""message"": ""The autherization header is not base64 encoding""
}";
                return authResult;
            }
            string[] userandpwd = Encoding.UTF8.GetString(bytes).Split(new char[] { ':' });
                        if (userandpwd.Length != 2)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Unauthorized;
                authResult = @"{
    ""success"": false,
    ""message"": ""The autherization header does not match the pattern username:password""
}";
                return authResult;
            }
                        if (!PBS.Util.Utility.IsUserAdmin(userandpwd[0], userandpwd[1], "localhost"))
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Unauthorized;
                authResult = @"{
    ""success"": false,
    ""message"": ""The user is not an administrator on PBS running machine.""
}";
                return authResult;
            }
                                    if (allowEmptyRequestBody && requestBody == null)
                return string.Empty;
                                    if (requestBody == null)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                authResult = @"{
    ""success"": false,
    ""message"": ""Request body is empty.""
}";
                return authResult;
            }
                        string strRequest;
            using (StreamReader sr = new StreamReader(requestBody))
            {
                strRequest = sr.ReadToEnd();
            }
            object o = JSON.JsonDecode(strRequest) as Hashtable;
            if (o == null)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                authResult = @"{
    ""success"": false,
    ""message"": ""Request parameters are not valid json array.""
}";
                return authResult;
            }
            ht = o as Hashtable;
            return string.Empty;
        }

        public Stream AddPBSService(Stream requestBody)
        {            
            string result=string.Empty;
            Hashtable htParams = null;
            WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain;charset=utf-8";
            result = AuthenticateAndParseParams(requestBody,out htParams);
            if (result != string.Empty)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
            else
            {
                string name, datasourcepath, tilingschemepath;
                int port;
                string strDataSourceType;
                bool allowmemorycache, disableclientcache, displaynodatatile;
                VisualStyle visualstyle;
                #region parsing params
                try
                {
                                        if (htParams["name"] == null || htParams["port"] == null || htParams["dataSourceType"] == null || htParams["dataSourcePath"] == null)
                    {
                        WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                        result = @"{
    ""success"": false,
    ""message"": ""name/port/datasourcetype/datasourcepath can not be null.""
}";
                        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
                    }
                    name = htParams["name"].ToString();
                    port = int.Parse(htParams["port"].ToString());
                    strDataSourceType = htParams["dataSourceType"].ToString();
                    datasourcepath = htParams["dataSourcePath"].ToString();
                    allowmemorycache = htParams["allowMemoryCache"] == null ? true : (bool)htParams["allowMemoryCache"];
                    disableclientcache = htParams["disableClientCache"] == null ? false : (bool)htParams["disableClientCache"];
                    displaynodatatile = htParams["displayNodataTile"] == null ? false : (bool)htParams["displayNodataTile"];
                    visualstyle = htParams["visualStyle"] == null ? VisualStyle.None : (VisualStyle)Enum.Parse(typeof(VisualStyle), htParams["visualStyle"].ToString());
                    tilingschemepath = htParams["tilingSchemePath"] == null ? null : htParams["tilingSchemePath"].ToString();
                }
                catch (Exception e)
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                    result = @"{
    ""success"": false,
    ""message"": ""request parameters parsing error! "+e.Message+@"""
}";
                    return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
                }
                #endregion
                                                                                                string str=string.Empty;
                                str = ServiceManager.CreateService(name, port, strDataSourceType, datasourcepath, allowmemorycache, disableclientcache, displaynodatatile, visualstyle, tilingschemepath);
                if (str != string.Empty)
                    result = @"{
                    ""success"": false,
                    ""message"": """ + str + @"""
                }";
                else
                    result = @"{
                    ""success"": true,
                    ""message"": " + ServiceManager.GetService(port, name).DataSource.TilingScheme.RestResponseArcGISJson + @"
                }";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
        }

        public Stream DeletePBSService(Stream requestBody)
        {
            string result = string.Empty;
            Hashtable htParams = null;
            WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain;charset=utf-8";
            result = AuthenticateAndParseParams(requestBody, out htParams);
            if (result != string.Empty)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
            else
            {
                string name;
                int port;
                #region parsing params
                try
                {        
                    if (htParams["name"] == null || htParams["port"] == null)
                    {
                        WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                        result = @"{
    ""success"": false,
    ""message"": ""name/port can not be null.""
}";
                        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
                    }
                    name = htParams["name"].ToString();
                    port = int.Parse(htParams["port"].ToString());
                }
                catch (Exception e)
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                    result = @"{
    ""success"": false,
    ""message"": ""request parameters parsing error! " + e.Message + @"""
}";
                    return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
                }
                #endregion
                string str = string.Empty;
                str = ServiceManager.DeleteService(port, name);
                if (str!=string.Empty)
                    result = @"{
                    ""success"": false,
                    ""message"": """ + str + @"""
                }";
                else
                    result = @"{
                    ""success"": true,
                    ""message"": ""success""
                }";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
        }

        public Stream ClearMemcacheByService(Stream requestBody)
        {
            string result = string.Empty;
            Hashtable htParams = null;
            WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain;charset=utf-8";
            result = AuthenticateAndParseParams(requestBody, out htParams);
            if (result != string.Empty)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
            else
            {
                if (ServiceManager.Memcache == null)
                {
                    result = @"{
                    ""success"": false,
                    ""message"": ""The MemCache capability has not been started yet.""
                }";
                    return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
                }

                string name;
                int port;
                #region parsing params
                try
                {
                    if (htParams["name"] == null || htParams["port"] == null)
                    {
                        WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                        result = @"{
    ""success"": false,
    ""message"": ""name/port can not be null.""
}";
                        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
                    }
                    name = htParams["name"].ToString();
                    port = int.Parse(htParams["port"].ToString());
                }
                catch (Exception e)
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                    result = @"{
    ""success"": false,
    ""message"": ""request parameters parsing error! " + e.Message + @"""
}";
                    return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
                }
                #endregion
                string str = string.Empty;
                str = ServiceManager.Memcache.InvalidateServiceMemcache(port, name);
                if (str != string.Empty)
                    result = @"{
                    ""success"": false,
                    ""message"": """ + str + @"""
                }";
                else
                    result = @"{
                    ""success"": true,
                    ""message"": ""success""
                }";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
        }

        public Stream EnableMemcache(Stream requestBody)
        {
            string result = string.Empty;
            Hashtable htParams = null;
            WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain;charset=utf-8";
            result = AuthenticateAndParseParams(requestBody, out htParams,true);
            if (result != string.Empty)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
            else if (ServiceManager.Memcache != null && ServiceManager.Memcache.IsActived)
            {
                result = @"{
                    ""success"": true,
                    ""message"": ""memory cache is already enabled.""
                }";
                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
            }
            else
            {
                int memSize = -1;
                #region parsing params
                if (requestBody == null || (requestBody.CanSeek && requestBody.Length == 0))
                    memSize = 64;
                else
                {
                    try
                    {
                        memSize = htParams["memSize"] == null ? 64 : int.Parse(htParams["memSize"].ToString());
                    }
                    catch (Exception e)
                    {
                        WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                        result = @"{
    ""success"": false,
    ""message"": ""request parameters parsing error! " + e.Message + @"""
}";
                        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
                    }
                }
                #endregion
                try
                {
                    if (ServiceManager.Memcache == null)
                        ServiceManager.Memcache = new MemCache(memSize);
                    else
                        ServiceManager.Memcache.IsActived = true;
                    result = @"{
                    ""success"": true,
                    ""message"": ""success""
                }";
                }
                catch (Exception ex)
                {
                    ServiceManager.Memcache = null;
                    result = @"{
                    ""success"": false,
                    ""message"": """ + ex.Message + @"""
                }";
                }
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
        }

        public Stream DisableMemcache(Stream requestBody)
        {
            string result = string.Empty;
            Hashtable htParams = null;
            WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain;charset=utf-8";
            result = AuthenticateAndParseParams(requestBody, out htParams, true);
            if (result != string.Empty)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
            else if (ServiceManager.Memcache == null || (ServiceManager.Memcache != null && !ServiceManager.Memcache.IsActived))
            {
                result = @"{
                    ""success"": true,
                    ""message"": ""memory cache is already disabled.""
                }";
                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
            }
            else
            {
                try
                {
                    if (ServiceManager.Memcache != null)
                        ServiceManager.Memcache.IsActived = false;
                    result = @"{
                    ""success"": true,
                    ""message"": ""success""
                }";
                }
                catch (Exception ex)
                {
                    ServiceManager.Memcache = null;
                    result = @"{
                    ""success"": false,
                    ""message"": """ + ex.Message + @"""
                }";
                }
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
        }

        public Stream ChangeArcGISDynamicMapServiceParams(Stream requestBody){
            string result = string.Empty;
            Hashtable htParams = null;
            WebOperationContext.Current.OutgoingResponse.Headers["X-Powered-By"] = PBSName;
            WebOperationContext.Current.OutgoingResponse.Headers["Access-Control-Allow-Origin"] = "*";
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain;charset=utf-8";
            result = AuthenticateAndParseParams(requestBody, out htParams);
            if (result != string.Empty)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
            else
            {
                int port;
                string name;
                string layers, layerDefs, time, layerTimeOptions;
                layers = layerDefs = time = layerTimeOptions = string.Empty;
                #region parsing params
                try
                {
                    if (htParams["name"] == null || htParams["port"] == null)
                    {
                        WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                        result = @"{
    ""success"": false,
    ""message"": ""name/port can not be null.""
}";
                        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
                    }
                    name = htParams["name"].ToString();
                    port = int.Parse(htParams["port"].ToString());

                    if (htParams["layers"] != null)
                        layers = htParams["layers"].ToString();
                    if (htParams["layerDefs"] != null)
                        layerDefs = htParams["layerDefs"].ToString();
                    if (htParams["time"] != null)
                        time = htParams["time"].ToString();
                    if (htParams["layerTimeOptions"] != null)
                        layerTimeOptions = htParams["layerTimeOptions"].ToString();
                }
                catch (Exception e)
                {
                    WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                    result = @"{
    ""success"": false,
    ""message"": ""request parameters parsing error! " + e.Message + @"""
}";
                    return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(result));
                }
                #endregion
                if (ServiceManager.GetService(port, name) == null)
                {
                    result = @"{
                    ""success"": false,
                    ""message"": ""service dose not exist.""
                }";
                }
                else if (ServiceManager.GetService(port, name).DataSource.Type != DataSource.DataSourceTypePredefined.ArcGISDynamicMapService.ToString())
                {
                    result = @"{
                    ""success"": false,
                    ""message"": ""service type is not ArcGISDynamicMapService.""
                }";
                }
                else
                {
                    if (layers != string.Empty)
                        (ServiceManager.GetService(port, name).DataSource as DataSourceArcGISDynamicMapService).exportParam_layers = layers;
                    if (layerDefs != string.Empty)
                        (ServiceManager.GetService(port, name).DataSource as DataSourceArcGISDynamicMapService).exportParam_layerDefs = layerDefs;
                    if (time != string.Empty)
                        (ServiceManager.GetService(port, name).DataSource as DataSourceArcGISDynamicMapService).exportParam_time = time;
                    if (layerTimeOptions != string.Empty)
                        (ServiceManager.GetService(port, name).DataSource as DataSourceArcGISDynamicMapService).exportParam_layerTimeOptions = layerTimeOptions;
                    result = @"{
                    ""success"": true,
                    ""message"": ""success""
                }";
                }                
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(result);
                return new MemoryStream(bytes);
            }
        }
        #endregion        
    }
}