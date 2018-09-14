using System;
using System.Collections.Generic;
using System.Linq;
using PBS.Service;
using System.ComponentModel;
using System.Linq.Expressions;
using PBS.Util;
using PBS.DataSource;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using System.Globalization;
using System.Configuration;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using PBS.APP.Classes;
using System.ServiceProcess;

namespace PBS.APP.ViewModels
{
    public class VMMainView:INotifyPropertyChanged
    {
        private ConfigManager _configManager = ConfigManager.Instance;
        private string _currentLanguage;//avoid to use multibinding in xaml to determine current ui language
        string _processName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;//"PortableBasemapServer";
        /// <summary>
        /// record the user input name
        /// </summary>
        private string _inputServiceName;

        #region binding properties
        private PBSService _selectedPBSService;
        /// <summary>
        /// binding to selected item of services listview 
        /// </summary>
        public PBSService SelectedPBSService
        {
            get { return _selectedPBSService; }
            set
            {
                _selectedPBSService = value;
                if (value != null)
                {                    
                    SelectedServiceChanged(_selectedPBSService);
                }
                
                NotifyPropertyChanged(p => p.SelectedPBSService);
            }
        }
        /// <summary>
        /// ServiceManager.Services
        /// </summary>
        public MTObservableCollection<PBSService> PBSServices
        {
            get
            {
                //initialize ServiceManager.Services if necessary
                if (ServiceManager.Services == null)
                {
                    ServiceManager.Services = new MTObservableCollection<PBSService>();
                }
                return ServiceManager.Services;
            }
        }
        private ObservableCollection<string> _dataSourceTypes = new ObservableCollection<string>();
        /// <summary>
        /// binding to datasourcetypes combobox
        /// </summary>
        public ObservableCollection<string> DataSourceTypes
        {
            get
            {
                return _dataSourceTypes;
            }
            private set
            {
                _dataSourceTypes = value;
                NotifyPropertyChanged(p => p.DataSourceTypes);
            }
        }
        private string _valueDataSourceType;
        /// <summary>
        /// string value of the currently selected datasourcetype
        /// </summary>
        public string ValueDataSourceType
        {
            get
            {
                return _valueDataSourceType;
            }
            set
            {
                _valueDataSourceType = value;
                DataSourceTypeSelectionChanged();
                NotifyPropertyChanged(p => p.ValueDataSourceType);
            }
        }
        private ObservableCollection<string> _visualStyles = new ObservableCollection<string>();
        /// <summary>
        /// binding to visualstyles combobox
        /// </summary>
        public ObservableCollection<string> VisualStyles
        {
            get
            {
                return _visualStyles;
            }
            private set
            {
                _visualStyles = value;
                NotifyPropertyChanged(p => p.VisualStyles);
            }
        }
        private string _valueVisualStyle;
        public string ValueVisualStyle
        {
            get
            {
                return _valueVisualStyle;
            }
            set
            {
                _valueVisualStyle = value;
                NotifyPropertyChanged(p => p.ValueVisualStyle);
                SelectedVisualStyleChanged();
            }
        }     
        private string _valueDataSourcePath;
        /// <summary>
        /// binding to textboxDataSourcePath's text
        /// </summary>
        public string ValueDataSourcePath
        {
            get { return _valueDataSourcePath; }
            set
            {
                _valueDataSourcePath = value;
                NotifyPropertyChanged(p => p.ValueDataSourcePath);
            }
        }
        private string _valueServicePort;
        /// <summary>
        /// binding to ServicePort textbox's text
        /// </summary>
        public string ValueServicePort
        {
            get { return _valueServicePort; }
            set
            {
                _valueServicePort = value;
                NotifyPropertyChanged(p => p.ValueServicePort);
            }
        }

        private string _valueServiceName;
        /// <summary>
        /// binding to ServiceName textbox's text
        /// </summary>
        public string ValueServiceName
        {
            get { return _valueServiceName; }
            set
            {
                _valueServiceName = value;
                NotifyPropertyChanged(p => p.ValueServiceName);                
            }
        }

        private bool _isUsingGoogleTilingSchemeChecked;
        /// <summary>
        /// bingding to UsingGoogleTilingScheme checkbox checking status
        /// </summary>
        public bool IsUsingGoogleTilingSchemeChecked
        {
            get { return _isUsingGoogleTilingSchemeChecked; }
            set
            {
                _isUsingGoogleTilingSchemeChecked = value;
                NotifyPropertyChanged(p => p.IsUsingGoogleTilingSchemeChecked);
            }
        }
        private string _valueTilingSchemeFilePath;
        /// <summary>
        /// binding to text of TilingScheme stack panel
        /// </summary>
        public string ValueTilingSchemeFilePath
        {
            get { return _valueTilingSchemeFilePath; }
            set
            {
                _valueTilingSchemeFilePath = value;
                NotifyPropertyChanged(p => p.ValueTilingSchemeFilePath);
            }
        }
        private bool _isAllowMemoryCache;
        public bool IsAllowMemoryCache
        {
            get { return _isAllowMemoryCache; }
            set { _isAllowMemoryCache = value;
            NotifyPropertyChanged(p => p.IsAllowMemoryCache);
            }
        }
        private bool _isDisableClientCache;
        public bool IsDisableClientCache
        {
            get { return _isDisableClientCache; }
            set { _isDisableClientCache = value;
            NotifyPropertyChanged(p => p.IsDisableClientCache);
            }
        }
        private bool _isDisplayNoDataTile;
        public bool IsDisplayNoDataTile
        {
            get { return _isDisplayNoDataTile; }
            set { _isDisplayNoDataTile = value;
            NotifyPropertyChanged(p => p.IsDisplayNoDataTile);
            }
        }
        private MTObservableCollection<string> _ipAddresses = new MTObservableCollection<string>();
        /// <summary>
        /// binding to datasourcetypes combobox
        /// </summary>
        public MTObservableCollection<string> IPAddresses
        {
            get
            {
                return _ipAddresses;
            }
            private set
            {
                _ipAddresses = value;
                NotifyPropertyChanged(p => p.IPAddresses);
            }
        }
        private string _valueIPAddress;
        public string ValueIPAddress
        {
            get
            {
                return _valueIPAddress;
            }
            set
            {
                _valueIPAddress = value;
                ServiceManager.IPAddress = _valueIPAddress;
                NotifyPropertyChanged(p => p.ValueIPAddress);
            }
        }        
	    #endregion
        #region UI related
        private bool _isMemCacheEnabled;
        /// <summary>
        /// binding to memcache menu item check status and menu item header text.
        /// </summary>
        public bool IsMemCacheEnabled
        {
            get { return _isMemCacheEnabled; }
            set
            {
                _isMemCacheEnabled = value;
                NotifyPropertyChanged(p => p.IsMemCacheEnabled);
            }
        }
        private string _strMemCacheMenuHeader;
        /// <summary>
        /// binding to memcache menu header text
        /// </summary>
        public string StrMemCacheMenuHeader
        {
            get { return _strMemCacheMenuHeader; }
            private set
            {
                _strMemCacheMenuHeader = value;
                NotifyPropertyChanged(p => p.StrMemCacheMenuHeader);
            }
        }
        private string _strURLArcGIS;
        /// <summary>
        /// binding to text of textboxServiceArcGIS
        /// </summary>
        public string StrURLArcGIS
        {
            get { return _strURLArcGIS; }
            set
            {
                _strURLArcGIS = value;
                NotifyPropertyChanged(p => p.StrURLArcGIS);
                (CMDClickCopyUrlButton as DelegateCommand).RaiseCanExecuteChanged();
            }
        }
        private string _strURLWMTS;
        /// <summary>
        /// binding to text of textboxServiceWMTS
        /// </summary>
        public string StrURLWMTS
        {
            get { return _strURLWMTS; }
            set
            {
                _strURLWMTS = value;
                NotifyPropertyChanged(p => p.StrURLWMTS);
                (CMDClickCopyUrlButton as DelegateCommand).RaiseCanExecuteChanged();
            }
        }
        private bool _isShowInSysTray;
        /// <summary>
        /// binding to showinsystray menu item check status
        /// </summary>
        public bool IsShowInSysTray
        {
            get { return _isShowInSysTray; }
            set
            {
                _isShowInSysTray = value;
                NotifyPropertyChanged(p => p.IsShowInSysTray);
            }
        }
        public EventHandler IsShowInSysTrayChanged;
        private bool _isLoadLastConfiguration;
        /// <summary>
        /// binding to load last configuration menu item check status
        /// </summary>
        public bool IsLoadLastConfiguration
        {
            get { return _isLoadLastConfiguration; }
            set
            {
                _isLoadLastConfiguration = value;
                NotifyPropertyChanged(p => p.IsLoadLastConfiguration);
            }
        }
        private bool _isRunAsWindowsService;
        /// <summary>
        /// binding to run as windows service menu item check status
        /// </summary>
        public bool IsRunAsWindowsService
        {
            get { return _isRunAsWindowsService; }
            set
            {
                _isRunAsWindowsService = value;
                NotifyPropertyChanged(p => p.IsRunAsWindowsService);
            }
        }
        private Visibility _isTilingSchemePanelVisible;
        /// <summary>
        /// binding to TilingScheme stack panel visibility
        /// </summary>
        public Visibility IsTilingSchemePanelVisible
        {
            get { return _isTilingSchemePanelVisible; }
            private set
            {
                _isTilingSchemePanelVisible = value;
                NotifyPropertyChanged(p => p.IsTilingSchemePanelVisible);
            }
        }
        private double _dataSourcePathTBHeight;
        /// <summary>
        /// binding to data source path textbox's height
        /// </summary>
        public double DataSourcePathTBHeight
        {
            get { return _dataSourcePathTBHeight; }
            private set
            {
                _dataSourcePathTBHeight = value;
                NotifyPropertyChanged(p => p.DataSourcePathTBHeight);
            }
        }
        private bool _isDataSourcePathTBEnabled;
        /// <summary>
        /// binding to data source path's availability
        /// </summary>
        public bool IsDataSourcePathTBEnabled
        {
            get { return _isDataSourcePathTBEnabled; }
            private set { _isDataSourcePathTBEnabled = value;
            NotifyPropertyChanged(p => p.IsDataSourcePathTBEnabled);
            }
        }
        private Visibility _isBrowseButtonVisible;
        /// <summary>
        /// binding to data source path Browse button's visibility
        /// </summary>
        public Visibility IsBrowseButtonVisible
        {
            get { return _isBrowseButtonVisible; }
            private set { _isBrowseButtonVisible = value;
            NotifyPropertyChanged(p => p.IsBrowseButtonVisible);
            }
        }
        private bool _isBrowseButtonEnabled;
        /// <summary>
        /// binding data source path browse button's availability
        /// </summary>
        public bool IsBrowseButtonEnabled
        {
            get { return _isBrowseButtonEnabled; }
            private set { _isBrowseButtonEnabled = value;
            NotifyPropertyChanged(p => p.IsBrowseButtonEnabled);
            }
        }
        private bool _isServiceNameTBEnabled;
        /// <summary>
        /// binding to ServiceName textbox's availability
        /// </summary>
        public bool IsServiceNameTBEnabled
        {
            get { return _isServiceNameTBEnabled; }
            private set { _isServiceNameTBEnabled = value;
            NotifyPropertyChanged(p => p.IsServiceNameTBEnabled);
            }
        }
        private Visibility _isAGSDMSParamsButtonVisible;
        /// <summary>
        /// bingding to ArcGISDynamicMapServiceParams button's visibility
        /// </summary>
        public Visibility IsAGSDMSParamsButtonVisible
        {
            get { return _isAGSDMSParamsButtonVisible; }
            private set { _isAGSDMSParamsButtonVisible = value;
            NotifyPropertyChanged(p => p.IsAGSDMSParamsButtonVisible);
            }
        }
        #endregion
        #region Command
        public ICommand CMDClickMenuItem { get; private set; }
        public ICommand CMDGotAndLostFocus { get; private set; }
        public ICommand CMDArcGISDynamicMapServiceParams { get; private set; }
        public ICommand CMDClickIsUsingGoogleTilingScheme { get; private set; }
        public ICommand CMDClickBrowseButton { get; private set; }
        public ICommand CMDClickCopyUrlButton { get; private set; }
        public ICommand CMDClickServiceButton { get; private set; }
        public ICommand CMDDoubleClickService { get; private set; }
        #endregion
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bindToUI">if true, this instance is intended to bind to MainWindow, and do ui related initialization jobs. otherwise, instance started without ui related initialization.</param>
        public VMMainView()
        {            
            #region app init
            //data source type of service
            foreach (DataSourceTypePredefined t in Enum.GetValues(typeof(DataSourceTypePredefined)))
            {
                DataSourceTypes.Add(t.ToString());
            }
            try
            {
                foreach (var map in DataSourceCustomOnlineMaps.CustomOnlineMaps)
                {
                    DataSourceTypes.Add(map.Name);
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            ValueDataSourceType = DataSourceTypes[3];
            //in case of returned rest response Stream not be released
            DispatcherTimer dt = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            dt.Tick += (s, a) =>
            {
                GC.Collect();
            };
            dt.Start();
            //When memory cache enabled/disabled, change UI. Especially changed by REST admin API.
            IsMemCacheEnabled = false;
            MemCache.IsActivedChanged += (s, a) =>
            {
                if (a.NewValue)
                {
                    IsMemCacheEnabled = true;
                    StrMemCacheMenuHeader = Application.Current.FindResource("menuMemoryCacheOn").ToString();
                }
                else
                {
                    IsMemCacheEnabled = false;
                    StrMemCacheMenuHeader = Application.Current.FindResource("menuMemoryCacheOff").ToString();
                }
            };
            //UI language
            SelectCulture(AppUtility.ReadConfig("Language", "en-US"));            
            //bing api key
            ConfigManager.App_BingMapsAppKey = AppUtility.ReadConfig("BingApiKey", string.Empty);
            //show in system tray?
            IsShowInSysTray = bool.Parse(AppUtility.ReadConfig("ShowInSystemTray", "True"));
            //if onlinemaps and rasterimage datasource should be cached in local .cache file.
            ConfigManager.App_AllowFileCacheOfOnlineMaps = bool.Parse(AppUtility.ReadConfig("AllowFileCacheOfOnlineMaps", "True"));
            ConfigManager.App_AllowFileCacheOfRasterImage = bool.Parse(AppUtility.ReadConfig("AllowFileCacheOfRasterImage", "True"));
            ConfigManager.App_FileCachePath = AppUtility.ReadConfig("FileCachePath", "filecache").ToString();
            if (!Directory.Exists(ConfigManager.App_FileCachePath))
                Directory.CreateDirectory(ConfigManager.App_FileCachePath);
            //load last configuration if necessary
            System.Threading.Tasks.Task.Factory.StartNew(delegate()
            {
                //find machine's ipv4 address first, starting service follow need this address.
                System.Net.IPHostEntry IpEntry = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                for (int i = 0; i != IpEntry.AddressList.Length; i++)
                {
                    if (!IpEntry.AddressList[i].IsIPv6LinkLocal && !IpEntry.AddressList[i].IsIPv6Multicast && !IpEntry.AddressList[i].IsIPv6SiteLocal && !IpEntry.AddressList[i].IsIPv6Teredo)
                    {
                        IPAddresses.Add(IpEntry.AddressList[i].ToString());
                    }
                }
                ValueIPAddress = IPAddresses[0];
                //check if need to load last time services from config.db.
                bool isLoadLastConfig = false;
                isLoadLastConfig = IsLoadLastConfiguration = bool.Parse(AppUtility.ReadConfig("LoadLastConfiguration", "True"));
                if (isLoadLastConfig && _configManager.IsConfigurationExists(ConfigManager.CONST_strLastConfigName))
                {
                    string result = _configManager.LoadConfigurationAndStartServices(ConfigManager.CONST_strLastConfigName);
                    if (result != string.Empty)
                        MessageBox.Show(result, Application.Current.FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                if (ServiceManager.Services == null || (ServiceManager.Services != null && ServiceManager.Services.Count == 0))
                {
                    //if last config did not have any service, no ServiceHost is started, then start WebServiceHost at default port.
                    int port = int.Parse(AppUtility.ReadConfig("DefaultPort", "7080"));
                    string str = ServiceManager.StartServiceHost(port);
                    if (str != string.Empty)
                        MessageBox.Show(Application.Current.FindResource("msgOpenPortError").ToString() + port + ".\r\n" + str, Application.Current.FindResource("msgWarning").ToString(), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
            //checking run as windows service status
            IsRunAsWindowsService = Utility.IsWindowsServiceExisted(_processName) ? true : false;
            if (IsRunAsWindowsService)
            {
                string str1;
                Utility.Cmd("sc qc " + _processName, false, out str1);
                //if (!str1.ToUpper().Contains(AppDomain.CurrentDomain.BaseDirectory.ToUpper()))
                //    Utility.Log(LogLevel.Error,null, "The bin path of PortableBasemapServer windows service is different than the current exe path. You may have moved the PortableBasemapServer folder after the windows service has been created.");
            }
            //visual style of service
            foreach (VisualStyle vs in Enum.GetValues(typeof(VisualStyle)))
                VisualStyles.Add(vs.ToString());
            ValueVisualStyle = VisualStyles[0];
            #endregion
            #region ui text init
            ValueDataSourcePath = @"D:\arcgisserver\directories\arcgiscache\CharlotteRaster.tpk";
            StrMemCacheMenuHeader = Application.Current.FindResource("menuMemoryCacheOff").ToString();
            ValueTilingSchemeFilePath = Application.Current.FindResource("tbGoogleBingAGOLTilingScheme").ToString();
            ValueServicePort = AppUtility.ReadConfig("DefaultPort", "7080");
            ValueServiceName = _inputServiceName = "MyPBSService1";
            #endregion
            #region Command
            CMDClickMenuItem = new DelegateCommand(MenuItemClicked);
            CMDGotAndLostFocus = new DelegateCommand(GotAndLostFocus);
            CMDArcGISDynamicMapServiceParams = new DelegateCommand(AGSDynamicMapServiceParamsClicked);
            CMDClickIsUsingGoogleTilingScheme = new DelegateCommand(IsUsingGoogleTilingSchemeClicked);
            CMDClickBrowseButton = new DelegateCommand(BrowseButtonClicked);
            CMDClickCopyUrlButton = new DelegateCommand(CopyUrlButtonClicked, (p) => { return !string.IsNullOrEmpty(StrURLArcGIS); });
            CMDClickServiceButton = new DelegateCommand(ServiceButtonClicked);
            CMDDoubleClickService = new DelegateCommand(ListViewDoubleClicked);
            #endregion
        }

        #region ui events
        private void MenuItemClicked(object parameters)
        {
            string miHeader = parameters.ToString().Split(new char[] { ',' })[0];
            if (miHeader == "miSetBingAPIKey")
            {
                BingApiKeyDialog keyDialog = new BingApiKeyDialog();
                keyDialog.tbKey.Text = ConfigManager.App_BingMapsAppKey;
                keyDialog.Owner = Application.Current.MainWindow;
                keyDialog.ShowDialog();
            }
            else if (miHeader == "miAbout")
            {
                AboutDialog aboutDialog = new AboutDialog();
                aboutDialog.Owner = Application.Current.MainWindow;
                aboutDialog.ShowDialog();
            }
            #region Configuration
            else if (miHeader == "miConfigurationFile")
            {
                ConfigWindow cw = new ConfigWindow();
                cw.Owner = Application.Current.MainWindow;
                cw.ShowDialog();
            }
            else if (miHeader == "miLoadLastConfiguration")
            {
                AppUtility.WriteConfig("LoadLastConfiguration", IsLoadLastConfiguration.ToString());
            }
            else if (miHeader == "miRunAsWindowsService")
            {
                //stop, delete window service first
                string str;
                Utility.Cmd("sc stop " + _processName, false, out str, 500);
                Utility.Cmd("sc delete " + _processName, false, out str);
                if (!IsRunAsWindowsService)
                {    
                    if (str.Contains("成功") || str.Contains("SUCCESS"))
                    {
                        MessageBox.Show(_processName + " windows service deleted successfully!", "", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(_processName + " windows service deleted failed!\r\nSee log.", "", MessageBoxButton.OK, MessageBoxImage.Error);
                        Utility.Log(LogLevel.Error, null, "windows service deleted failed:" + str);
                    }
                }
                else//stop, delete, install windows service
                {
                    Utility.Cmd("sc create " + _processName + @" binPath= """ + System.Reflection.Assembly.GetExecutingAssembly().Location + @" /runasservice"" start= auto type= own DisplayName= " + _processName, false, out str, 500);
                    if (str.Contains("成功") || str.Contains("SUCCESS"))
                    {
                        MessageBox.Show(_processName + " windows service created successfully!\r\n\r\n"+Application.Current.FindResource("msgWindowsServiceCreated").ToString(), "", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(_processName + " windows service created failed!\r\nSee log.", "", MessageBoxButton.OK, MessageBoxImage.Error);
                        Utility.Log(LogLevel.Error, null, "windows service created failed:" + str);
                        IsRunAsWindowsService = false;
                        return;
                    }
                }
            }
            #endregion
            #region Memcached
            else if (miHeader == "miEnableMemCache")//memcached
            {
                if (IsMemCacheEnabled)
                {
                    int memSize = int.Parse(AppUtility.ReadConfig("MemcachedSize", "64"));
                    try
                    {
                        if (ServiceManager.Memcache == null)
                            ServiceManager.Memcache = new MemCache(memSize);
                        else
                            ServiceManager.Memcache.IsActived = true;
                    }
                    catch (Exception ex)
                    {
                        ServiceManager.Memcache = null;
                        MessageBox.Show(Application.Current.FindResource("msgMemcacheUnavailable").ToString() + "\r\n" + ex.Message, Application.Current.FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    if (ServiceManager.Memcache != null)
                        ServiceManager.Memcache.IsActived = false;
                }
            }
            else if (miHeader == "miClearMemoryCache")
            {
                try
                {
                    ClearMemcacheByServiceNameWindow clearMemcacheWindow = new ClearMemcacheByServiceNameWindow();
                    clearMemcacheWindow.Owner = Application.Current.MainWindow;
                    clearMemcacheWindow.ShowDialog();
                }
                catch (Exception ee)
                {
                    MessageBox.Show(ee.Message);
                }
            }
            #endregion
            #region Format Convert
            else if (miHeader == "miArcGISToMBTiles")
            {
                ArcGISCacheToMBTiles convertWindow = new ArcGISCacheToMBTiles();
                convertWindow.Owner = Application.Current.MainWindow;
                convertWindow.Show();
            }
            else if (miHeader == "miOnlineToMBTiles")
            {
                try
                {
                    OnlineToMBTiles convertWindow = new OnlineToMBTiles(ValueServicePort);
                    convertWindow.Owner = Application.Current.MainWindow;
                    convertWindow.Show();
                }
                catch (Exception)
                {
                }
            }
            #endregion
            #region Appearance
            else if (miHeader == "miLanguage")
            {
                if (_currentLanguage == "zh-CN")
                {
                    SelectCulture("en-US");
                    AppUtility.WriteConfig("Language", "en-US");
                }
                else
                {
                    SelectCulture("zh-CN");
                    AppUtility.WriteConfig("Language", "zh-CN");
                }
            }
            else if (miHeader == "miShowInSysTray")
            {
                if (IsShowInSysTrayChanged != null)
                    IsShowInSysTrayChanged(this, new EventArgs());
                AppUtility.WriteConfig("ShowInSystemTray", IsShowInSysTray.ToString());
            }
            #endregion
        }

        private void GotAndLostFocus(object parameters)
        {
            //ref: Invoking commands from events using the InvokeCommandAction behavior
            //http://blogs.u2u.be/peter/post/2010/10/19/Invoking-commands-from-events-using-the-InvokeCommandAction-behavior.aspx
            string[] commandParams = parameters.ToString().Split(new char[] { '_' });
            //datasource format info for ArcGISDynamicMapService datasource
            if (commandParams[0] == "textboxDataSourcePath"&&
                (ValueDataSourceType==DataSourceTypePredefined.ArcGISDynamicMapService.ToString()||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISTiledMapService.ToString()||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISImageService.ToString()))
            {
                if (commandParams[1].ToLower() == "got")//got focus
                {
                    if (ValueDataSourcePath == Application.Current.FindResource("tbArcGISDynamicMapServiceDataSourceInfo").ToString())
                        ValueDataSourcePath = "";                    
                }
                //else if (commandParams[1].ToLower() == "lost")
                //{
                //    if (StrTextBoxDataSourcePath == "")
                //        MessageBox.Show(Application.Current.FindResource("msgLeavingDataSourcePathTextBoxError").ToString(), Application.Current.FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                //}
            }
            //when textboxServicePort lost focus
            if (commandParams[0] == "textboxServicePort" && commandParams[1] == "lost")
            {
                if (!IsNumeric(ValueServicePort))
                {
                    System.Windows.MessageBox.Show(Application.Current.FindResource("msgServicePortInvalid").ToString());
                    ValueServicePort = "7080";
                }
            }
            //when textboxServiceName lost focus
            if (commandParams[0] == "textboxServiceName" && commandParams[1] == "lost")
            {
                _inputServiceName = ValueServiceName;
            }
        }

        private void AGSDynamicMapServiceParamsClicked(object parameters)
        {
            if (SelectedPBSService != null)
            {
                try
                {
                    ArcGISDynamicMapServiceParams paramsWindow = new ArcGISDynamicMapServiceParams(SelectedPBSService);
                    paramsWindow.Owner = Application.Current.MainWindow;
                    paramsWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    //if using WrapAround=true in map control, here will throw a "Cannot read from the stream" exception.
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void IsUsingGoogleTilingSchemeClicked(object parameters)
        {
            if (IsUsingGoogleTilingSchemeChecked)
            {
                ValueTilingSchemeFilePath = Application.Current.FindResource("tbGoogleBingAGOLTilingScheme").ToString();
                //textboxTilingSchemeFile.IsEnabled = false;
            }
            else
            {
                System.Windows.Forms.OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
                openFileDialog1.Title = Application.Current.FindResource("tbSelectConfxmlFile").ToString();
                openFileDialog1.RestoreDirectory = true;
                openFileDialog1.Multiselect = false;
                openFileDialog1.Filter = "xml file (*.xml)|*.xml";
                if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (openFileDialog1.CheckFileExists)
                    {
                        //textboxTilingSchemeFile.IsEnabled = true;
                        ValueTilingSchemeFilePath = openFileDialog1.FileName;
                    }
                }
                else
                {
                    IsUsingGoogleTilingSchemeChecked = true;
                }
            }
        }

        private void BrowseButtonClicked(object parameters)
        {
            DataSourceTypePredefined type = (DataSourceTypePredefined)Enum.Parse(typeof(DataSourceTypePredefined), ValueDataSourceType);
            switch (type)
            {
                case DataSourceTypePredefined.MobileAtlasCreator:
                case DataSourceTypePredefined.MBTiles:
                    System.Windows.Forms.OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
                    openFileDialog1.Title = Application.Current.FindResource("tbSelectSqliteFile").ToString();
                    openFileDialog1.RestoreDirectory = true;
                    openFileDialog1.Multiselect = false;
                    openFileDialog1.Filter = "All files (*.*)|*.*";
                    if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        if (openFileDialog1.CheckFileExists)
                        {
                            ValueDataSourcePath = openFileDialog1.FileName;
                        }
                    }
                    break;
                case DataSourceTypePredefined.ArcGISTilePackage:
                    openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
                    openFileDialog1.Title = Application.Current.FindResource("tbSelectTPKFile").ToString();
                    openFileDialog1.RestoreDirectory = true;
                    openFileDialog1.Multiselect = false;
                    openFileDialog1.Filter = "All files (*.tpk)|*.tpk";
                    if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        if (openFileDialog1.CheckFileExists)
                        {
                            ValueDataSourcePath = openFileDialog1.FileName;
                        }
                    }
                    break;
                case DataSourceTypePredefined.RasterImage:
                    openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
                    openFileDialog1.Title = Application.Current.FindResource("tbSelectRasterFile").ToString();
                    openFileDialog1.RestoreDirectory = true;
                    openFileDialog1.Multiselect = false;
                    openFileDialog1.Filter = "All files (*.*)|*.*";
                    if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        if (openFileDialog1.CheckFileExists)
                        {
                            ValueDataSourcePath = openFileDialog1.FileName;
                        }
                    }
                    break;                 
                case DataSourceTypePredefined.ArcGISCache:
                    System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
                    if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        if (!Directory.Exists(folderBrowserDialog1.SelectedPath + @"\_alllayers"))
                        {
                            System.Windows.MessageBox.Show(Application.Current.FindResource("msg_alllayersNotExist").ToString());
                            return;
                        }
                        ValueDataSourcePath = folderBrowserDialog1.SelectedPath;
                    }
                    break;
                case DataSourceTypePredefined.AutoNaviCache:
                    System.Windows.Forms.FolderBrowserDialog folderBrowserDialog2 = new System.Windows.Forms.FolderBrowserDialog();
                    if (folderBrowserDialog2.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        ValueDataSourcePath = folderBrowserDialog2.SelectedPath;
                    }
                    break;
                default:
                    break;
            }
        }

        private void CopyUrlButtonClicked(object parameters)
        {
            try
            {
                switch (parameters.ToString().ToLower())
                {
                    case "arcgis":
                        if (StrURLArcGIS != string.Empty)
                        {
                            System.Windows.Clipboard.SetData(System.Windows.DataFormats.Text, StrURLArcGIS);
                            System.Windows.MessageBox.Show(Application.Current.FindResource("msgCopyed").ToString() + "\r\n" + StrURLArcGIS);
                        }
                        break;
                    case "wmts":
                        if (StrURLWMTS != string.Empty)
                        {
                            System.Windows.Clipboard.SetData(System.Windows.DataFormats.Text, StrURLWMTS);
                            System.Windows.MessageBox.Show(Application.Current.FindResource("msgCopyed").ToString() + "\r\n" + StrURLWMTS);
                        }
                        break;
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ServiceButtonClicked(object parameters)
        {
            string param = parameters.ToString();
            if (param=="START")
            {
                //if datasourcetype is file, check file existence
                if (ValueDataSourceType == DataSourceTypePredefined.MobileAtlasCreator.ToString() || ValueDataSourceType == DataSourceTypePredefined.MBTiles.ToString() || ValueDataSourceType == DataSourceTypePredefined.RasterImage.ToString() || ValueDataSourceType == DataSourceTypePredefined.ArcGISTilePackage.ToString())
                {
                    if (!File.Exists(ValueDataSourcePath))
                    {
                        MessageBox.Show(Application.Current.FindResource("msgDataSourcePathNotExist").ToString(), "", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                string result = null;
                if ((ValueDataSourceType == DataSourceTypePredefined.ArcGISDynamicMapService.ToString() || ValueDataSourceType == DataSourceTypePredefined.ArcGISImageService.ToString() || ValueDataSourceType == DataSourceTypePredefined.RasterImage.ToString() || ValueDataSourceType == DataSourceTypePredefined.OGCWMSService.ToString()) && !IsUsingGoogleTilingSchemeChecked)//_dataSourceType == DataSourceType.WMSService 
                {
                    result = ServiceManager.CreateService(
                        ValueServiceName,
                        int.Parse(ValueServicePort),
                        ValueDataSourceType,
                        ValueDataSourcePath,
                        IsAllowMemoryCache,
                        IsDisableClientCache,
                        IsDisplayNoDataTile,
                        (VisualStyle)Enum.Parse(typeof(VisualStyle), ValueVisualStyle, true),
                        ValueTilingSchemeFilePath);
                }
                else
                {
                    result = ServiceManager.CreateService(
                        ValueServiceName,
                        int.Parse(ValueServicePort),
                        ValueDataSourceType,
                        ValueDataSourcePath,
                        IsAllowMemoryCache,
                        IsDisableClientCache,
                        IsDisplayNoDataTile,
                        (VisualStyle)Enum.Parse(typeof(VisualStyle), ValueVisualStyle, true));
                }
                if (result != string.Empty)
                {
                    System.Windows.MessageBox.Show(result, Application.Current.FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                PortEntity portEntity;
                if (ServiceManager.PortEntities.TryGetValue(int.Parse(ValueServicePort), out portEntity))
                {
                    PBSService service = portEntity.ServiceProvider.Services[ValueServiceName];
                    //check if MAC source has initial extent
                    if (service.DataSource.Type == DataSourceTypePredefined.MobileAtlasCreator.ToString() && Math.Abs(service.DataSource.TilingScheme.InitialExtent.XMin + 20037508.3427892) < 0.1)
                    {
                        if (System.Windows.MessageBox.Show(Application.Current.FindResource("msgMACExtentWarning").ToString(), Application.Current.FindResource("msgWarning").ToString(), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(@"https://github.com/mapbox/mbtiles-spec/blob/master/1.1/spec.md"));
                        }
                    }
                    //check if RasterDataset can retrieve projection info from gdal
                    if (service.DataSource.Type == DataSourceTypePredefined.RasterImage.ToString() && (service.DataSource as DataSourceRasterImage).HasProjectionFromGDAL == false)
                    {
                        MessageBox.Show(Application.Current.FindResource("msgGDALSRWarning").ToString(), Application.Current.FindResource("msgWarning").ToString(), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    //copyright annoucement for online maps
                    if (ValueDataSourceType != DataSourceTypePredefined.ArcGISCache.ToString() &&
                        ValueDataSourceType != DataSourceTypePredefined.AutoNaviCache.ToString() &&
                        ValueDataSourceType != DataSourceTypePredefined.ArcGISDynamicMapService.ToString() &&
                        ValueDataSourceType != DataSourceTypePredefined.ArcGISImageService.ToString() &&
                        ValueDataSourceType != DataSourceTypePredefined.ArcGISTiledMapService.ToString() &&
                        ValueDataSourceType != DataSourceTypePredefined.ArcGISTilePackage.ToString() &&
                        ValueDataSourceType != DataSourceTypePredefined.MBTiles.ToString() &&
                        ValueDataSourceType != DataSourceTypePredefined.MobileAtlasCreator.ToString() &&
                        ValueDataSourceType != DataSourceTypePredefined.RasterImage.ToString()&&
                        ValueDataSourceType != DataSourceTypePredefined.OGCWMSService.ToString())
                    {
                        MessageBox.Show(Application.Current.FindResource("msgOnlineMapsWarning").ToString(), Application.Current.FindResource("msgWarning").ToString(), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    StrURLArcGIS = service.UrlArcGIS;
                    StrURLWMTS = service.UrlWMTS;
                }
            }
            if (param == "DELETE")
            {
                if (SelectedPBSService !=null)
                {
                    PBSService service = SelectedPBSService;
                    if (MessageBox.Show(Application.Current.FindResource("msgDeleteServiceWarning").ToString() + "\r\n" + service.ServiceName + ":" + service.Port.ToString(), Application.Current.FindResource("msgWarning").ToString(), MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                    {
                        ServiceManager.DeleteService(service.Port, service.ServiceName);
                        if(PBSServices.Count>0)
                            SelectedPBSService = PBSServices[0];
                    }
                }
                else
                {
                    MessageBox.Show(Application.Current.FindResource("msgSelectServiceFirst").ToString());
                }
            }
        }

        private void ListViewDoubleClicked(object parameters)
        {
            if (SelectedPBSService != null)
            {
                try
                {
                    PBSService service = SelectedPBSService;
                    PreviewWindow previewWindow = new PreviewWindow(service);
                    previewWindow.Owner = Application.Current.MainWindow;
                    previewWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    //if using WrapAround=true in map control, here will throw a "Cannot read from the stream" exception.
                    MessageBox.Show(ex.Message);
                }
            }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged<TValue>(Expression<Func<VMMainView, TValue>> propertySelector)
        {
            if (PropertyChanged == null)
                return;

            var memberExpression = propertySelector.Body as MemberExpression;
            if (memberExpression == null)
                return;

            PropertyChanged(this, new PropertyChangedEventArgs(memberExpression.Member.Name));
        }

        /// <summary>
        /// equal to cmbboxSource_SelectionChanged event handler
        /// </summary>
        private void DataSourceTypeSelectionChanged()
        {
            IsAllowMemoryCache = true;
            IsDisableClientCache = IsDisplayNoDataTile = false;
            IsAGSDMSParamsButtonVisible = Visibility.Collapsed;
            //tilingscheme stack panel's visibility & usinggoogletilingscheme checkbox check status
            if (ValueDataSourceType == DataSourceTypePredefined.RasterImage.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISDynamicMapService.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISImageService.ToString()||
                ValueDataSourceType == DataSourceTypePredefined.OGCWMSService.ToString())
            {
                IsTilingSchemePanelVisible = Visibility.Visible;
                IsUsingGoogleTilingSchemeChecked = true;
            }
            else
            {
                IsTilingSchemePanelVisible = Visibility.Collapsed;
                IsUsingGoogleTilingSchemeChecked = false;
            }
            //data source path textbox's height & data source path browse button's visibility
            if (ValueDataSourceType == DataSourceTypePredefined.ArcGISDynamicMapService.ToString() || ValueDataSourceType == DataSourceTypePredefined.OGCWMSService.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISTiledMapService.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISImageService.ToString())
            {
                DataSourcePathTBHeight = 100;
                IsBrowseButtonVisible = Visibility.Collapsed;
            }
            else
            {
                DataSourcePathTBHeight = double.NaN;
                IsBrowseButtonVisible = Visibility.Visible;
            }
            //data source path textbox's text
            if (ValueDataSourceType == DataSourceTypePredefined.ArcGISDynamicMapService.ToString())
                ValueDataSourcePath = Application.Current.FindResource("tbArcGISDynamicMapServiceDataSourceInfo").ToString() + "\r\n" + "http://sampleserver1.arcgisonline.com/ArcGIS/rest/services/Specialty/ESRI_StateCityHighway_USA/MapServer";
            else if (ValueDataSourceType == DataSourceTypePredefined.ArcGISTiledMapService.ToString())
                ValueDataSourcePath = "http://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer";
            else if (ValueDataSourceType == DataSourceTypePredefined.ArcGISImageService.ToString())
                ValueDataSourcePath = "http://imagery.arcgisonline.com/ArcGIS/rest/services/LandsatGLS/FalseColor/ImageServer";
            else if (ValueDataSourceType == DataSourceTypePredefined.OGCWMSService.ToString())
                ValueDataSourcePath = "http://sampleserver1.arcgisonline.com/ArcGIS/services/Specialty/ESRI_StatesCitiesRivers_USA/MapServer/WMSServer";
            else
                ValueDataSourcePath = "";
            //data source path textbox's availability & ServiceName textbox's text & ServiceName textbox's availability
            if (ValueDataSourceType == DataSourceTypePredefined.MobileAtlasCreator.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.MBTiles.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISCache.ToString() || ValueDataSourceType == DataSourceTypePredefined.AutoNaviCache.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISTilePackage.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISDynamicMapService.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISTiledMapService.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISImageService.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.RasterImage.ToString()||
                ValueDataSourceType == DataSourceTypePredefined.OGCWMSService.ToString())
            {
                IsDataSourcePathTBEnabled = true;                
                IsServiceNameTBEnabled = true;
            }
            else
            {
                IsDataSourcePathTBEnabled = false;
                ValueServiceName = ValueDataSourceType;
                IsServiceNameTBEnabled = false;
            }
            //data source path browse button's availability
            if (ValueDataSourceType == DataSourceTypePredefined.MobileAtlasCreator.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.MBTiles.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISCache.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.AutoNaviCache.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.ArcGISTilePackage.ToString() ||
                ValueDataSourceType == DataSourceTypePredefined.RasterImage.ToString())
                IsBrowseButtonEnabled = true;
            else
                IsBrowseButtonEnabled = false;
        }

        /// <summary>
        /// equal to lvServices_SelectionChanged event handler
        /// </summary>
        /// <param name="service"></param>
        private void SelectedServiceChanged(PBSService service)
        {
            ValueDataSourceType = service.DataSource.Type;
            if (service.DataSource.Type == DataSourceTypePredefined.ArcGISDynamicMapService.ToString() || service.DataSource.Type == DataSourceTypePredefined.ArcGISImageService.ToString() || service.DataSource.Type == DataSourceTypePredefined.RasterImage.ToString() || service.DataSource.Type == DataSourceTypePredefined.OGCWMSService.ToString())//service.DataSource.Type==DataSourceType.WMSService||
            {
                IsTilingSchemePanelVisible = Visibility.Visible;
                if (service.DataSource.TilingScheme.WKID == 102100 || service.DataSource.TilingScheme.WKID == 3857)
                {
                    ValueTilingSchemeFilePath = Application.Current.FindResource("tbGoogleBingAGOLTilingScheme").ToString();
                    IsUsingGoogleTilingSchemeChecked = true;
                }
                else
                {
                    ValueTilingSchemeFilePath = service.DataSource.TilingScheme.Path;
                    IsUsingGoogleTilingSchemeChecked = false;
                }
            }
            else
            {
                IsTilingSchemePanelVisible = Visibility.Collapsed;
            }

            if (service.DataSource.Type == DataSourceTypePredefined.ArcGISDynamicMapService.ToString())
                IsAGSDMSParamsButtonVisible = Visibility.Visible;
            else
                IsAGSDMSParamsButtonVisible = Visibility.Collapsed;

            ValueDataSourcePath = service.DataSource.Path;
            ValueServicePort = service.Port.ToString();            
            IsAllowMemoryCache = service.AllowMemCache;
            IsDisableClientCache = service.DisableClientCache;
            IsDisplayNoDataTile = service.DisplayNoDataTile;
            ValueVisualStyle = service.Style.ToString();//will change the ValueServiceName value
            StrURLArcGIS = service.UrlArcGIS;
            StrURLWMTS = service.UrlWMTS;
            ValueServiceName = service.ServiceName;
        }

        /// <summary>
        /// equal to cmbboxVisualStyle_SelectionChanged event handler
        /// </summary>
        private void SelectedVisualStyleChanged()
        {
            if (ValueVisualStyle != VisualStyle.None.ToString())
                ValueServiceName = _inputServiceName + ValueVisualStyle;
            else
                ValueServiceName = _inputServiceName;
        }

        private void SelectCulture(string culture)
        {
            _currentLanguage = culture;
            // List all our resources      
            List<ResourceDictionary> dictionaryList = new List<ResourceDictionary>();
            foreach (ResourceDictionary dictionary in Application.Current.Resources.MergedDictionaries)
            {
                dictionaryList.Add(dictionary);
            }
            // We want our specific culture      
            string requestedCulture = string.Format("Languages/StringResources.{0}.xaml", culture);
            ResourceDictionary resourceDictionary = dictionaryList.FirstOrDefault(d => d.Source.OriginalString == requestedCulture);
            if (resourceDictionary == null)
            {
                // If not found, we select our default language        
                //        
                requestedCulture = "Languages/StringResources.en-US.xaml";
                resourceDictionary = dictionaryList.FirstOrDefault(d => d.Source.OriginalString == requestedCulture);
            }

            // If we have the requested resource, remove it from the list and place at the end.\      
            // Then this language will be our string table to use.      
            if (resourceDictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(resourceDictionary);
                Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
            }
            // Inform the threads of the new culture      
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture(culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);

            //app related
            if (IsMemCacheEnabled)
                StrMemCacheMenuHeader = Application.Current.FindResource("menuMemoryCacheOn").ToString();
            else
                StrMemCacheMenuHeader = Application.Current.FindResource("menuMemoryCacheOff").ToString();
            ValueTilingSchemeFilePath = Application.Current.FindResource("tbGoogleBingAGOLTilingScheme").ToString();
        }
        /// <summary>
        /// Auto save started services, delete servicehost, shutdown memcache.
        /// </summary>
        public void DoCleanJobs()
        {
            //auto save services
            string result = ConfigManager.Instance.SaveConfigurationWithOverwrite(ConfigManager.CONST_strLastConfigName);
            if (result != string.Empty)
                MessageBox.Show(result, Application.Current.FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
            ServiceManager.DeleteAllServiceHost();
            if (ServiceManager.Memcache != null)
                ServiceManager.Memcache.Shutdown();//very important. otherwise, the app will not be termitated after closing the window
        }

        private bool IsNumeric(string str)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(str, @"^[0-9]*[1-9][0-9]*$");
        }
    }
}
