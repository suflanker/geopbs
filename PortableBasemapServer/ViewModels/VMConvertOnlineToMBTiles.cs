using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Collections.ObjectModel;
using PBS.DataSource;
using System.Windows.Input;
using PBS.APP.Classes;
using System.Windows.Forms;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client;
using PBS.Service;
using System.ServiceModel.Web;
using System.ServiceModel;
using System.ServiceModel.Description;
using ESRI.ArcGIS.Client.Symbols;
using System.Windows.Media;
using ESRI.ArcGIS.Client.Projection;
using System.IO;
using Vishcious.ArcGIS.SLContrib;
using System.Linq;

namespace PBS.APP.ViewModels
{
    public class VMConvertOnlineToMBTiles:INotifyPropertyChanged,IDisposable
    {
        private string _hiddenServiceName = "INTERNALDOWNLOAD";
        /// <summary>
        /// record the download union polygon if download by shapefile
        /// PBS.Util.Polygon
        /// </summary>
        private PBS.Util.Polygon _downloadPolygon;
        private string _selectedDatasourceType;
        public string SelectedDatasourceType
        {
            get { return _selectedDatasourceType; }
            set
            {
                _selectedDatasourceType = value;
                ChangeMap();
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

        private string _output;
        public string Output
        {
            get { return _output; }
            set
            {
                _output = value;
                NotifyPropertyChanged(p => p.Output);
                (CMDClickStartButton as DelegateCommand).RaiseCanExecuteChanged();
            }
        }
        private int _selectedIndexOfDrawExtentMethod;
        /// <summary>
        /// 0 == Draw by Mouse
        /// 1 == Import ShapeFile
        /// </summary>
        public int SelectedIndexOfDrawExtentMethod
        {
            get { return _selectedIndexOfDrawExtentMethod; }
            set
            {                
                _selectedIndexOfDrawExtentMethod = value;
                bool openSuccessful = false;
                if (value == 1)//import shapefile
                    openSuccessful = OpenShapeFile();
                //if (!openSuccessful)
                //{
                //    _selectedIndexOfDrawExtentMethod = 0;
                //    NotifyPropertyChanged(p => p.SelectedIndexOfDrawExtentMethod);
                //    NotifyPropertyChanged(p => p.IsDrawExtentTipVisible);
                //}
                NotifyPropertyChanged(p => p.SelectedIndexOfDrawExtentMethod);
                NotifyPropertyChanged(p => p.IsDrawExtentTipVisible);
            }
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Attribution { get; set; }
        public bool DoCompact { get; set; }
        public string CurrentLevel { get; set; }
        ConfigManager _configManager = ConfigManager.Instance;
        /// <summary>
        /// esri Envelope,sr=4326, for ui binding
        /// </summary>
        public ESRI.ArcGIS.Client.Geometry.Envelope DownloadExtent { get; set; }
        private int[] _levels;
        /// <summary>
        /// binding to download levels on UI.
        /// </summary>
        public int[] Levels
        {
            get { return _levels; }
            set
            {
                _levels = value;
                if (DownloadExtent != null)
                    TilesCount = AppUtility.CalculateTileCount(Levels, (Envelope)_webMercator.FromGeographic(DownloadExtent));
                NotifyPropertyChanged(p => p.Levels);
                (CMDClickStartButton as DelegateCommand).RaiseCanExecuteChanged();
            }
        }
        private ObservableCollection<string> _profiles;
        /// <summary>
        /// combobox content, profiles of download extent and download levels.
        /// </summary>
        public ObservableCollection<string> Profiles
        {
            get { return _profiles; }
            set { _profiles = value;
            NotifyPropertyChanged(p => p.Profiles);
            }
        }
        private string _selectedProfile;
        public string SelectedProfile
        {
            get { return _selectedProfile; }
            set { _selectedProfile = value;
            NotifyPropertyChanged(p => p.SelectedProfile);
            (CMDClickProfileButton as DelegateCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// binding to Draw tip
        /// </summary>
        public System.Windows.Visibility IsDrawExtentTipVisible
        {
            get { return (DownloadExtent == null&&SelectedIndexOfDrawExtentMethod==0) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; }
        }
        private long _tilesCount;
        /// <summary>
        /// tiles count to download
        /// </summary>
        public long TilesCount
        {
            get { return _tilesCount; }
            set { _tilesCount = value;
            NotifyPropertyChanged(p => p.TilesCount);
            }
        }
        private bool _isIdle;
        public bool IsIdle
        {
            get { return _isIdle; }
            private set
            {
                _isIdle = value;
                NotifyPropertyChanged(p => p.IsIdle);
            }
        }
        private DataSourceCustomOnlineMaps _datasource;
        public DataSourceCustomOnlineMaps Datasource
        {
            get { return _datasource; }
            set { _datasource = value;
            NotifyPropertyChanged(p => p.Datasource);
            }
        }

        #region Map related
        private Map _map;
        private bool _isRightMouseButtonDown = false;
        private MapPoint _startPoint;
        private WebMercator _webMercator = new WebMercator();
        private GraphicsLayer _graphicsLayer;
        private PBSService _pbsService;
        private int _port;
        #endregion

        public ICommand CMDClickBrowseButton { get; private set; }
        public ICommand CMDClickStartButton { get; private set; }
        public ICommand CMDClickProfileButton { get; private set; }

        public VMConvertOnlineToMBTiles(Map esriMap,int port)
        {
            //check internet connectivity
            if (!PBS.Util.Utility.IsConnectedToInternet())
            {
                throw new Exception("No internet connectivity!");
            }
            _port = port;
            //check the availability of the port using for this map
            if (!ServiceManager.PortEntities.ContainsKey(_port))
            {
                try
                {
                    //WebServiceHost host = new WebServiceHost(serviceProvider, new Uri("http://localhost:" + port));
                    WebServiceHost host = new WebServiceHost(typeof(PBSServiceProvider), new Uri("http://localhost:" + _port));
                    host.AddServiceEndpoint(typeof(IPBSServiceProvider), new WebHttpBinding(), "").Behaviors.Add(new WebHttpBehavior());
                    ServiceDebugBehavior stp = host.Description.Behaviors.Find<ServiceDebugBehavior>();
                    stp.HttpHelpPageEnabled = false;
                    host.Open();
                    ServiceManager.PortEntities.Add(_port, new PortEntity(host, new PBSServiceProvider()));
                }
                catch (Exception e)
                {
                    string m = "The port using for this map is not available.\r\n";
                    //HTTP 无法注册 URL http://+:7777/CalulaterServic/。进程不具有此命名空间的访问权限(有关详细信息，请参阅 http://go.microsoft.com/fwlink/?LinkId=70353)
                    if (e.Message.Contains("http://go.microsoft.com/fwlink/?LinkId=70353"))
                    {
                        throw new Exception(m+"Your Windows has enabled UAC, which restrict of Http.sys Namespace. Please reopen PortableBasemapServer by right clicking and select 'Run as Administrator'. \r\nAdd WebServiceHost Error!\r\n" + e.Message);
                    }
                    throw new Exception(m+e.Message);
                }
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
            SelectedDatasourceType = DataSourceTypes[0];
            CMDClickBrowseButton = new DelegateCommand(BrowseButtonClicked);
            CMDClickStartButton = new DelegateCommand(StartButtonClicked, (p) => { return !string.IsNullOrEmpty(Output)&&DownloadExtent!=null&&(Levels!=null&&Levels.Length>0)&&IsIdle; });
            CMDClickProfileButton = new DelegateCommand(ProfileButtonClicked, (p) => { return !string.IsNullOrEmpty(SelectedProfile); });
            IsIdle = true;
            SelectedIndexOfDrawExtentMethod = 0;
            Profiles = new ObservableCollection<string>(_configManager.GetAllDownloadProfileNames());
            #region map related
            _map = esriMap;
            //current level
            _map.ExtentChanged += (s, a) =>
            {
                if ((_map.Layers[0] as ArcGISTiledMapServiceLayer).TileInfo == null)
                    return;
                int i;
                for (i = 0; i < (_map.Layers[0] as ArcGISTiledMapServiceLayer).TileInfo.Lods.Length; i++)
                {
                    if (Math.Abs(_map.Resolution - (_map.Layers[0] as ArcGISTiledMapServiceLayer).TileInfo.Lods[i].Resolution) < 0.000001)
                    {
                        break;
                    }
                }
                CurrentLevel = i.ToString();
                NotifyPropertyChanged(p => p.CurrentLevel);
            };
            _graphicsLayer = new GraphicsLayer();
            _map.Layers.Add(_graphicsLayer);
            _map.MouseRightButtonDown += new MouseButtonEventHandler(MouseRightButtonDown);
            _map.MouseRightButtonUp += new MouseButtonEventHandler(MouseRightButtonUp);
            _map.MouseMove += new System.Windows.Input.MouseEventHandler(MouseMove);
            //load first map
            ChangeMap();
            #endregion
        }

        void MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isRightMouseButtonDown&&IsIdle)
            {
                //draw download extent
                Graphic g = _graphicsLayer.Graphics[0];
                MapPoint endPoint = _map.ScreenToMap(e.GetPosition(_map));
                g.Geometry = new Envelope(_startPoint.X, endPoint.Y, endPoint.X, _startPoint.Y);
                //tiles count
                if (Levels != null && Levels.Length > 0)
                {
                    TilesCount = AppUtility.CalculateTileCount(Levels, (Envelope)g.Geometry);
                }
                DownloadExtent = (Envelope)_webMercator.ToGeographic(g.Geometry);                
                NotifyPropertyChanged(p => p.DownloadExtent);
                NotifyPropertyChanged(p => p.IsDrawExtentTipVisible);                
            }
        }

        void MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isRightMouseButtonDown = false;
            (CMDClickStartButton as DelegateCommand).RaiseCanExecuteChanged();
            if (_graphicsLayer.Graphics.Count > 0 && _graphicsLayer.Graphics[0].Geometry == null)
                _map.Zoom(2);//just right click mouse, without moving.
        }

        void MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsIdle||SelectedIndexOfDrawExtentMethod!=0)
                return;
            _startPoint = _map.ScreenToMap(e.GetPosition(_map));
            _isRightMouseButtonDown = true;
            _graphicsLayer.ClearGraphics();
            _graphicsLayer.Graphics.Add(new Graphic()
            {
                Symbol = new SimpleFillSymbol()
                {
                    BorderBrush = new SolidColorBrush(Colors.Red),
                    Fill = new SolidColorBrush(Color.FromArgb(100, 255, 0, 0))
                }
            });
            _downloadPolygon = null;
        }

        private void BrowseButtonClicked(object parameters)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = App.Current.FindResource("titleOutputPath").ToString();
            sfd.RestoreDirectory = true;
            sfd.DefaultExt = ".mbtiles";
            sfd.OverwritePrompt = false;
            sfd.AddExtension = true;
            sfd.Filter = "All files (*.*)|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Output = sfd.FileName;
            }
        }

        private void StartButtonClicked(object parameters)
        {
            if (!PBS.Util.Utility.IsValidFilename(Output))
            {
                MessageBox.Show(App.Current.FindResource("msgOutputPathError").ToString(), App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (File.Exists(Output))
            {
                if (MessageBox.Show(App.Current.FindResource("msgOverwrite").ToString(), App.Current.FindResource("msgWarning").ToString(), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                    return;
                else
                {
                    try
                    {
                        File.Delete(Output);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }
            try
            {
                Datasource = new DataSourceCustomOnlineMaps(SelectedDatasourceType);
                Datasource.ConvertCompleted += (s, a) =>
                {
                    if (a.Successful)
                    {
                        string str = App.Current.FindResource("msgConvertComplete").ToString();
                        if (DoCompact)
                            str += "\r\n" + App.Current.FindResource("msgCompactResult").ToString() + (Datasource.ConvertingStatus.SizeBeforeCompact / 1024).ToString("N0") + "KB --> " + (Datasource.ConvertingStatus.SizeAfterCompact / 1024).ToString("N0") + "KB";
                        MessageBox.Show(str, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };
                Datasource.ConvertCancelled += (s, a) =>
                {
                    //try
                    //{
                    //    //aa.mbtiles and aa.mbtiles-journal
                    //    if (File.Exists(Output))
                    //    {
                    //        File.Delete(Output);
                    //    }
                    //    if (File.Exists(Output + "-journal"))
                    //    {
                    //        File.Delete(Output + "-journal");
                    //    }
                    //}
                    //catch (Exception e)
                    //{
                    //    throw new Exception(".mbtiles and .mbtiles-journal files could not be deleted.\r\n" + e.Message);
                    //}
                };
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += (s, a) =>
                {
                    IsIdle = false;
                    App.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        (CMDClickStartButton as DelegateCommand).RaiseCanExecuteChanged();
                    }));
                    Envelope extent = (Envelope)_webMercator.FromGeographic(DownloadExtent);
                    try
                    {
                        PBS.Util.Geometry g = _downloadPolygon == null ? (PBS.Util.Geometry)new PBS.Util.Envelope(extent.XMin, extent.YMin, extent.XMax, extent.YMax) : _downloadPolygon;
                        if(_downloadPolygon!=null)
                            MessageBox.Show(App.Current.FindResource("msgDownloadByPolygonIntro").ToString(), "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Datasource.ConvertToMBTiles(Output, Name, Description, Attribution, Levels, g, DoCompact); 
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }                    
                    IsIdle = true;
                    App.Current.Dispatcher.Invoke(new Action(() =>
                {
                    (CMDClickStartButton as DelegateCommand).RaiseCanExecuteChanged();
                }));
                };
                bw.RunWorkerAsync();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void ProfileButtonClicked(object parameters)
        {
            string str = parameters.ToString();            
            switch (str)
            {
                case "SAVE":
                    if (DownloadExtent == null || Levels==null||Levels.Length == 0)
                    {
                        MessageBox.Show(App.Current.FindResource("msgDownloadProfileInvalid").ToString(), App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    Util.Envelope env = new Util.Envelope(DownloadExtent.XMin, DownloadExtent.YMin, DownloadExtent.XMax, DownloadExtent.YMax);
                    string result=_configManager.SaveDownloadProfileWithOverwrite(new PBS.DownloadProfile(SelectedProfile, Levels, env, _downloadPolygon));
                    if(result!=string.Empty)
                        MessageBox.Show(result, App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "LOAD":
                    DownloadProfile profile = _configManager.LoadDownloadProfile(SelectedProfile);
                    if (profile == null)
                    {
                        MessageBox.Show(App.Current.FindResource("msgLoadFailed").ToString(), App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    _downloadPolygon = profile.Polygon;
                    Levels = profile.Levels;
                    DownloadExtent = new Envelope(profile.Envelope.XMin, profile.Envelope.YMin, profile.Envelope.XMax, profile.Envelope.YMax);
                    _graphicsLayer.ClearGraphics();
                    _graphicsLayer.Graphics.Add(new Graphic()
                    {
                        Symbol = new SimpleFillSymbol()
                        {
                            BorderBrush = new SolidColorBrush(Colors.Red),
                            Fill = new SolidColorBrush(Color.FromArgb(100, 255, 0, 0))
                        },
                        Geometry = profile.Polygon == null ? _webMercator.FromGeographic(DownloadExtent) : AppUtility.ConvertPBSPolygonToEsriPolygon(profile.Polygon)
                    });
                    (CMDClickStartButton as DelegateCommand).RaiseCanExecuteChanged();
                    TilesCount = AppUtility.CalculateTileCount(Levels, (Envelope)_webMercator.FromGeographic(DownloadExtent));
                    break;
                case "DELETE":
                    _configManager.DeleteDownloadProfile(SelectedProfile);
                    break;
                default:
                    break;
            }
            Profiles = new ObservableCollection<string>(_configManager.GetAllDownloadProfileNames());
        }

        private void ChangeMap()
        {
            if (_map != null)
            {
                int i;
                if (int.TryParse(_hiddenServiceName.ToCharArray()[_hiddenServiceName.Length - 1].ToString(), out i))//if last char of _hiddenServiceName is int
                    ServiceManager.DeleteService(_port, _hiddenServiceName);
                else
                    _hiddenServiceName = _hiddenServiceName + ServiceManager.PortEntities[_port].ServiceProvider.Services.Count(service => service.Key.Contains(_hiddenServiceName)).ToString();//INTERNALDOWNLOAD0,INTERNALDOWNLOAD1...
                _pbsService = new PBSService(_hiddenServiceName, "", _port, SelectedDatasourceType, false, true, true, VisualStyle.None, null);
                ServiceManager.PortEntities[_port].ServiceProvider.Services.Add(_pbsService.ServiceName, _pbsService);
                _map.Layers.RemoveAt(0);
                ArcGISTiledMapServiceLayer l = new ArcGISTiledMapServiceLayer() { Url = _pbsService.UrlArcGIS };
                _map.Layers.Insert(0, l);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged<TValue>(Expression<Func<VMConvertOnlineToMBTiles, TValue>> propertySelector)
        {
            if (PropertyChanged == null)
                return;

            var memberExpression = propertySelector.Body as MemberExpression;
            if (memberExpression == null)
                return;

            PropertyChanged(this, new PropertyChangedEventArgs(memberExpression.Member.Name));
        }

        private bool OpenShapeFile()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Title = App.Current.FindResource("tbSelectSHPFile").ToString();
            ofd.RestoreDirectory = true;
            ofd.Multiselect = false;
            ofd.Filter = "shape file (*.shp)|*.shp";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string shpFileName = ofd.FileName;
                string dbfFilename = Path.ChangeExtension(shpFileName,".dbf");
                if (!ofd.CheckFileExists || !File.Exists(dbfFilename))
                    MessageBox.Show(App.Current.FindResource("msgSHPError").ToString(), App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                {
                    try
                    {
                        FileInfo fiSHP = new FileInfo(shpFileName);
                        FileInfo fiDBF = new FileInfo(dbfFilename);
                        ShapeFile shapeFile = new ShapeFile();
                        shapeFile.Read(fiSHP, fiDBF);
                        if (shapeFile.FileHeader.ShapeType != 5)
                            throw new Exception(App.Current.FindResource("msgSHPTypeError").ToString());
                        _graphicsLayer.ClearGraphics();
                        Envelope env = new Envelope();
                        foreach (ShapeFileRecord record in shapeFile.Records)
                        {
                            Graphic graphic = record.ToGraphic();
                            //if the coordinates is wgs 84,try to convert to 3857
                            if (Math.Abs(record.Points[0].X) < 300 || Math.Abs(record.Points[0].Y) < 300)
                                graphic.Geometry = _webMercator.FromGeographic(graphic.Geometry);
                            if (graphic != null)
                                graphic.Symbol = new SimpleFillSymbol()
                                {
                                    BorderBrush = new SolidColorBrush(Colors.Red),
                                    Fill = new SolidColorBrush(Color.FromArgb(100, 255, 0, 0))
                                };
                            _graphicsLayer.Graphics.Add(graphic);
                            env = env.Union(graphic.Geometry.Extent);
                        }
                        DownloadExtent = _webMercator.ToGeographic(env).Extent;
                        _downloadPolygon = AppUtility.ConvertEsriPolygonToPBSPolygon(AppUtility.UnionEsriPolygon(_graphicsLayer.Graphics));
                        return true;
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            return false;
        }

        public void Dispose()
        {
            string r=ServiceManager.DeleteService(_port, _hiddenServiceName);
            if (r != string.Empty)
                throw new Exception("Dispose internal PBSService error when closing download tiles window!\r\n" + r);
        }
    }
}
