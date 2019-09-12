//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.Xml.Linq;
using System.ComponentModel;
using OSGeo.GDAL;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PBS.DataSource;
using System.Net;
using System.Linq.Expressions;

namespace PBS.Service
{
    public class PBSService
    {
        public string ServiceName { get; set; }
        public int Port { get; set; }
        public DataSourceBase DataSource { get; set; }
        public bool AllowMemCache { get; set; }//is server memory cache allowable
        public bool DisableClientCache { get; set; }
        public bool DisplayNoDataTile { get; set; }
        /// <summary>
        /// provide option to change service/tile image's visual style, such as to grayscale
        /// </summary>
        public VisualStyle Style { get; set; }
        /// <summary>
        /// arcgis service url
        /// </summary>
        public string UrlArcGIS { get; private set; }
        /// <summary>
        /// ogc wmts url
        /// </summary>
        public string UrlWMTS { get { return UrlArcGIS+"/WMTS"; } }
        public Log LogInfo { get; set; }
        //using for clear memory cache of individual service
        //memcached批量删除方案探讨:http://it.dianping.com/memcached_item_batch_del.htm Key flag 方案
        public string MemcachedValidKey = string.Empty;
        //public List<??> PendingTiles { get; set; }//arcgis silverlight api using this for only calculate progress
        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="dataSourcePath"></param>
        /// <param name="port"></param>
        /// <param name="strType">DataSourceType enum + custom online maps</param>
        /// <param name="disableClientCache"></param>
        /// <param name="displayNodataTile"></param>
        /// <param name="tilingSchemePath">Set this parameter only when type is ArcGISDynamicMapService||RasterDataset and do not use Google Maps's tiling scheme</param>
        public PBSService(string serviceName, string dataSourcePath, int port, string strType, bool allowmemcache, bool disableClientCache, bool displayNodataTile, VisualStyle style, string tilingSchemePath)
        {
            ServiceName = serviceName;
            if (!DataSourceBase.IsOnlineMaps(strType))
            {
                DataSourceTypePredefined type = (DataSourceTypePredefined)Enum.Parse(typeof(DataSourceTypePredefined), strType);
                switch (type)
                {
                    case DataSourceTypePredefined.MobileAtlasCreator:
                        DataSource = new DataSourceMAC(dataSourcePath);
                        break;
                    case DataSourceTypePredefined.MBTiles:
                        DataSource = new DataSourceMBTiles(dataSourcePath);
                        break;
                    case DataSourceTypePredefined.ArcGISCacheV2:
                        DataSource = new DataSourceArcGISCacheV2(dataSourcePath);
                        break;
                    case DataSourceTypePredefined.ArcGISCache:
                        DataSource = new DataSourceArcGISCache(dataSourcePath);
                        break;
                    case DataSourceTypePredefined.ArcGISTilePackage:
                        DataSource = new DataSourceArcGISTilePackage(dataSourcePath);
                        break;
                    case DataSourceTypePredefined.RasterImage:
                        DataSource = new DataSourceRasterImage(dataSourcePath, tilingSchemePath, ServiceName);
                        break;
                    case DataSourceTypePredefined.ArcGISDynamicMapService:
                        DataSource = new DataSourceArcGISDynamicMapService(dataSourcePath, tilingSchemePath);
                        break;
                    case DataSourceTypePredefined.ArcGISTiledMapService:
                        DataSource = new DataSourceArcGISTiledMapService(dataSourcePath, tilingSchemePath);
                        break;
                    case DataSourceTypePredefined.ArcGISImageService:
                        DataSource = new DataSourceArcGISImageService(dataSourcePath, tilingSchemePath);
                        break;
                    case DataSourceTypePredefined.AutoNaviCache:
                        DataSource = new DataSourceAutoNaviCache(dataSourcePath);
                        break;
                    case DataSourceTypePredefined.OGCWMSService:
                        DataSource = new DataSourceWMSService(dataSourcePath, tilingSchemePath);
                        break;
                    case DataSourceTypePredefined.TianDiTuAnnotation:
                        DataSource = new DataSourceTianDiTuAnno();
                        break;
                    case DataSourceTypePredefined.TianDiTuMap:
                        DataSource = new DataSourceTianDiTuMap();
                        break;
                    default:
                        throw new Exception();
                }
                DataSource.IsOnlineMap = false;
            }
            else
            {
                bool known = false;
                foreach (var map in DataSourceCustomOnlineMaps.CustomOnlineMaps)
                {
                    if (map.Name == strType)
                    {
                        known = true;
                        break;
                    }
                }
                if (!known)
                    throw new Exception(strType + " is not a known data source type.");
                DataSource = new DataSourceCustomOnlineMaps(strType)
                {
                    IsOnlineMap=true
                };
            }
            
            
            Port = port;
            AllowMemCache = allowmemcache;
            DisableClientCache = disableClientCache;
            DisplayNoDataTile = displayNodataTile;            
            Style = style;
            LogInfo = new Log();
            if(string.IsNullOrEmpty(ServiceManager.IPAddress))
                throw new Exception("IPAddress is null or empty when creating PBS service!");
            UrlArcGIS = "http://" + ServiceManager.IPAddress + ":" + Port + "/PBS/rest/services/" + ServiceName + "/MapServer";
        }

        public void Dispose()
        {
            //if (PendingRequests.Count > 0)
            //{
            //    foreach (WebClient request in PendingRequests)
            //    {
            //        if (request.IsBusy)
            //        {
            //            request.CancelAsync();
            //            request.Dispose();
            //        }
            //    }
            //}
            if (DataSource is IDisposable)
                ((IDisposable)DataSource).Dispose();
        }

        ~PBSService()
        {
            
        }
    }

    public class Log : INotifyPropertyChanged
    {
        /// <summary>
        /// how many tiles this service have been output totally
        /// </summary>
        public long OutputTileCountTotal
        {
            get { return _tileCountDynamic+_tileCountMemcached+_tileCountFileCached; }
        }
        private long _tileCountDynamic;
        /// <summary>
        /// how many tiles this service have been output dynamically
        /// </summary>
        public long OutputTileCountDynamic
        {
            get { return _tileCountDynamic; }
            set
            {
                _tileCountDynamic = value;
                NotifyPropertyChanged(p => p.OutputTileCountDynamic);
                NotifyPropertyChanged(p => p.OutputTileCountTotal);
            }
        }
        private long _tileCountMemcached;
        /// <summary>
        /// how many tiles this service have been output from Memcached
        /// </summary>
        public long OutputTileCountMemcached
        {
            get { return _tileCountMemcached; }
            set
            {
                _tileCountMemcached = value;
                NotifyPropertyChanged(p => p.OutputTileCountMemcached);
                NotifyPropertyChanged(p => p.OutputTileCountTotal);
            }
        }
        private long _tileCountFileCached;
        /// <summary>
        /// how many tiles this service have been output from cached file.
        /// </summary>
        public long OutputTileCountFileCache
        {
            get { return _tileCountFileCached; }
            set
            {
                _tileCountFileCached = value;
                NotifyPropertyChanged(p => p.OutputTileCountFileCache);
                NotifyPropertyChanged(p => p.OutputTileCountTotal);
            }
        }
        private double _tileTotalTime;
        /// <summary>
        /// total time count of all the output tiles. in Milliseconds.
        /// </summary>
        public double OutputTileTotalTime
        {
            get { return _tileTotalTime; }
            set
            {
                _tileTotalTime = value;
                NotifyPropertyChanged(p => p.OutputTileTotalTime);
                NotifyPropertyChanged(p => p.SPT);
            }
        }
        /// <summary>
        /// average one tile output time. senconds per tile.
        /// </summary>
        public double SPT
        {
            get
            {
                return _tileTotalTime / OutputTileCountTotal / 1000;
            }
        }
        private List<string> _requestedIPs;
        /// <summary>
        /// all requested client ip
        /// </summary>
        public List<string> RequestedIPs
        {
            get { return _requestedIPs; }
            set
            {
                _requestedIPs = value;
                NotifyPropertyChanged(p => p.RequestedIPs);
            }
        }
        private string _lastRequestClientIP;
        /// <summary>
        /// LastRequestClientIP address
        /// </summary>
        public string LastRequestClientIP
        {
            get
            {
                return _lastRequestClientIP;
            }
            set
            {
                _lastRequestClientIP = value;
                NotifyPropertyChanged(p => p.LastRequestClientIP);
            }
        }
        private int _requestedClientCounts;
        /// <summary>
        /// how many clients does this service has served
        /// </summary>
        public int RequestedClientCounts
        {
            get { return _requestedClientCounts; }
            set
            {
                _requestedClientCounts = value;
                NotifyPropertyChanged(p => p.RequestedClientCounts);
            }
        }

        public Log()
        {
            RequestedIPs = new List<string>();
        }


        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged<TValue>(Expression<Func<Log, TValue>> propertySelector)
        {
            if (PropertyChanged == null)
                return;

            var memberExpression = propertySelector.Body as MemberExpression;
            if (memberExpression == null)
                return;

            PropertyChanged(this, new PropertyChangedEventArgs(memberExpression.Member.Name));
        }
        #endregion
    }

    public enum VisualStyle
    {
        None,
        /// <summary>
        /// 灰度
        /// </summary>
        Gray,
        /// <summary>
        /// 反色
        /// </summary>
        Invert,
        /// <summary>
        /// 怀旧
        /// </summary>
        Tint,
        /// <summary>
        /// 饱和
        /// </summary>
        //Saturation,
        /// <summary>
        /// 浮雕
        /// </summary>
        Embossed
    }
}
