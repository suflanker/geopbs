//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel.Web;
using System.ServiceModel.Description;
using System.ServiceModel;
using PBS.DataSource;
using System.Collections.ObjectModel;
using System.Net;
using PBS.Util;

namespace PBS.Service
{
    public static class ServiceManager
    {
        private static Dictionary<int, PortEntity> _portEntities;
        public static Dictionary<int, PortEntity> PortEntities
        {
            get
            {
                if (_portEntities == null)
                {
                    _portEntities = new Dictionary<int, PortEntity>();
                }
                return _portEntities;
            }
            set
            {
                _portEntities = value;
            }
        }
        /// <summary>
        /// all services at all ports
        /// </summary>
        public static MTObservableCollection<PBSService> Services { get; set; }        
        /// <summary>
        /// used for service urls
        /// </summary>
        public static string IPAddress { get; set; }
        /// <summary>
        /// used to grab the Memcached object so can be used in external exe
        /// </summary>
        public static MemCache Memcache { get; set; }

        public static string StartServiceHost(int port)
        {
            if (Services == null)
            {
                Services = new MTObservableCollection<PBSService>();
            }
            //create one webservicehost to corresponding port number first
            if (!PortEntities.ContainsKey(port))
            {
                //Self Hosted WCF REST Service or Hosting WCF REST Service in Console Application
                //http://www.c-sharpcorner.com/UploadFile/dhananjaycoder/1407/

                try
                {
                    //WebServiceHost host = new WebServiceHost(serviceProvider, new Uri("http://localhost:" + port));
                    WebServiceHost host = new WebServiceHost(typeof(PBSServiceProvider), new Uri("http://localhost:" + port));
                    host.AddServiceEndpoint(typeof(IPBSServiceProvider), new WebHttpBinding(), "").Behaviors.Add(new WebHttpBehavior());
                    ServiceDebugBehavior stp = host.Description.Behaviors.Find<ServiceDebugBehavior>();
                    stp.HttpHelpPageEnabled = false;
                    host.Open();

                    PortEntities.Add(port, new PortEntity(host, new PBSServiceProvider()));
                }
                catch (Exception e)
                {
                    //HTTP 无法注册 URL http://+:7777/CalulaterServic/。进程不具有此命名空间的访问权限(有关详细信息，请参阅 http://go.microsoft.com/fwlink/?LinkId=70353)
                    if (e.Message.Contains("http://go.microsoft.com/fwlink/?LinkId=70353"))
                    {
                        return "Your Windows has enabled UAC, which restrict of Http.sys Namespace. Please reopen PortableBasemapServer by right clicking and select 'Run as Administrator'. \r\nAdd WebServiceHost Error!\r\n" + e.Message;
                    }
                    return "Add WebServiceHost Error!\r\n" + e.Message;
                }
            }
            else
                return "WebServiceHost@" + port + " already exist!";
            return string.Empty;
        }

        public static PBSService GetService(int port, string name)
        {
            PortEntity portEntity;
            if (ServiceManager.PortEntities.TryGetValue(port, out portEntity) && portEntity.ServiceProvider.Services.ContainsKey(name))
            {
                return portEntity.ServiceProvider.Services[name];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="port"></param>
        /// <param name="strType">DataSourceType enum + custom online maps</param>
        /// <param name="dataSorucePath"></param>
        /// <param name="disableClientCache"></param>
        /// <param name="displayNoDataTile"></param>
        /// <param name="style"></param>
        /// <param name="tilingSchemePath">Set this parameter only when type is ArcGISDynamicMapService and do not use Google Maps's tiling scheme</param>
        /// <returns>errors or warnings. string.empty if nothing wrong.</returns>
        public static string CreateService(string name, int port, string strType, string dataSorucePath,bool allowMemoryCache, bool disableClientCache, bool displayNoDataTile,VisualStyle style,string tilingSchemePath=null)
        {
            PBSServiceProvider serviceProvider = null;
            string str;
            if (!PortEntities.ContainsKey(port))
            {
                str =StartServiceHost(port);
                if (str != string.Empty)
                    return str;
            }
            serviceProvider = PortEntities[port].ServiceProvider;

            if (serviceProvider.Services.ContainsKey(name))
            {
                return "Servicename already exists!";
            }

            PBSService service;
            try
            {
                service = new PBSService(name, dataSorucePath, port, strType,allowMemoryCache,disableClientCache,displayNoDataTile,style,tilingSchemePath);
            }
            catch (Exception e)//in case of reading conf.xml or conf.cdi file error|| reading a sqlite db error
            {
                Utility.Log(LogLevel.Error, null,"Creating New Service(" + name + ") Error!\r\nData Source: " + dataSorucePath + "\r\n\r\n" + e.Message);
                return "Creating New Service(" + name + ") Error!\r\nData Source: " + dataSorucePath + "\r\n\r\n" + e.Message;
            }
            serviceProvider.Services.Add(name, service);//for process http request
            Services.Add(service);//for ui binding
            return string.Empty;
        }

        public static string DeleteService(int port, string name)
        {
            if (!PortEntities.ContainsKey(port))
                return "Port " + port + " is not started yet.";
            if (!PortEntities[port].ServiceProvider.Services.ContainsKey(name))
                return "Service " + name + "does not exists on port " + port + ".";
            Services.Remove(PortEntities[port].ServiceProvider.Services[name]);
            PortEntities[port].ServiceProvider.Services[name].Dispose();
            PortEntities[port].ServiceProvider.Services.Remove(name);
            return string.Empty;
        }
        /// <summary>
        /// explicitly delete all services first, then delete all service host on all portentities. 
        /// </summary>
        public static void DeleteAllServiceHost()
        {
            //delete all services
            while (Services != null && Services.Count > 0)
            {
                DeleteService(Services[0].Port, Services[0].ServiceName);
            }
            if (PortEntities != null)
            {
                List<int> ports = PortEntities.Keys.ToList();
                for (int i = 0; i < ports.Count; i++)
                {
                    PortEntities[ports[i]].ServiceProvider.Services.Clear();
                    PortEntities[ports[i]].ServiceHost.Close();
                    PortEntities.Remove(ports[i]);
                }
            }
        }

        public static string CreateServiceByHTTP(string name, int port, string dataSourceType, string dataSorucePath, bool allowMemoryCache, bool disableClientCache, bool displayNoDataTile, VisualStyle style, string tilingSchemePath = null)
        {
            System.Collections.Hashtable ht = new System.Collections.Hashtable();
            ht.Add("name", name);
            ht.Add("port", port);
            ht.Add("dataSourceType", dataSourceType.ToString());
            ht.Add("dataSourcePath", dataSorucePath);
            ht.Add("allowMemoryCache", allowMemoryCache);
            ht.Add("disableClientCache", disableClientCache);
            ht.Add("displayNodataTile", displayNoDataTile);
            ht.Add("visualStyle", style.ToString());
            ht.Add("tilingSchemePath", tilingSchemePath);
            byte[] postData = Encoding.UTF8.GetBytes(JSON.JsonEncode(ht));

            HttpWebRequest myReq = WebRequest.Create("http://192.168.26.128:7080/PBS/rest/admin/addService") as HttpWebRequest;
            myReq.Method = "POST";
            string username = "esrichina";
            string password = "esrichina";
            string usernamePassword = username + ":" + password;
            //注意格式 “用户名:密码”，之后Base64编码
            myReq.Headers.Add("Authorization", Convert.ToBase64String(Encoding.UTF8.GetBytes(usernamePassword)));
            myReq.ContentLength = postData.Length;
            using (System.IO.Stream requestStream = myReq.GetRequestStream())
            {
                requestStream.Write(postData, 0, postData.Length);
            }            
            WebResponse wr = myReq.GetResponse();
            System.IO.Stream receiveStream = wr.GetResponseStream();
            System.IO.StreamReader reader = new System.IO.StreamReader(receiveStream, Encoding.UTF8);
            string content = reader.ReadToEnd();
            receiveStream.Close();
            reader.Close();
            return string.Empty;
        }
    }

    /// <summary>
    /// There are always one WebServiceHost and one PBSServiceProvider on one port.
    /// </summary>
    public class PortEntity
    {
        public WebServiceHost ServiceHost { get; set; }
        public PBSServiceProvider ServiceProvider { get; set; }//services at the same port
        public PortEntity(WebServiceHost host,PBSServiceProvider provider)
        {
            ServiceHost = host;
            ServiceProvider = provider;
        }
    }    
}
