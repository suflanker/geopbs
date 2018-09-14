using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Symbols;
using System.Windows.Media;
using PBS.Service;
using PBS.DataSource;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace PBS.APP
{
    /// <summary>
    /// Interaction logic for PreviewWindow.xaml
    /// </summary>
    public partial class PreviewWindow : Window
    {
        PBSService _service;
        GraphicsLayer _gLayer;
        double _oldResolution = -1;
        /// <summary>
        /// void to draw duplicate grid graphics
        /// </summary>
        ConcurrentDictionary<string, TileWatermark> _dictDrawedGrids = new ConcurrentDictionary<string, TileWatermark>();
        public PreviewWindow(PBSService service)
        {
            InitializeComponent();

            _service = service;
            //for MAC and mbtiles and those whose wkid=102100 and which doesn't has initialextent
            if ((service.DataSource.Type == DataSourceTypePredefined.MobileAtlasCreator.ToString()&&Math.Abs(service.DataSource.TilingScheme.InitialExtent.XMin + 20037508.3427892) < 0.1)  ||
                (service.DataSource.Type == DataSourceTypePredefined.MBTiles.ToString()&&Math.Abs(service.DataSource.TilingScheme.InitialExtent.XMin + 20037508.3427892) < 0.1) ||
                (service.DataSource.Type == DataSourceTypePredefined.RasterImage.ToString() && (service.DataSource.TilingScheme.WKID == 102100 || service.DataSource.TilingScheme.WKID == 3857)) ||
                (service.DataSource.Type == DataSourceTypePredefined.ArcGISDynamicMapService.ToString() && (service.DataSource.TilingScheme.WKID == 102100 || service.DataSource.TilingScheme.WKID == 3857)))
            {
                ArcGISTiledMapServiceLayer streetLayer = new ArcGISTiledMapServiceLayer() { Url = "http://www.arcgisonline.cn/ArcGIS/rest/services/ChinaOnlineStreetGray/MapServer" };
                streetLayer.InitializationFailed += (s,a) =>
                    {
                        string innerMessage = (s as ArcGISTiledMapServiceLayer).InitializationFailure.InnerException == null ? string.Empty : (s as ArcGISTiledMapServiceLayer).InitializationFailure.InnerException.Message;
                        string message = "ArcGIS Online World Street Map" + "\r\n" + (s as ArcGISTiledMapServiceLayer).InitializationFailure.Message;
                        if (innerMessage != string.Empty)
                            message += "\r\n" + innerMessage;
                        MessageBox.Show(message);
                    };
                map1.Layers.Add(streetLayer);

                CheckBox chkbox = new CheckBox()
                {
                    Content = FindResource("tbDisplayBasemap").ToString(),
                    FontSize = 20,
                    FontFamily = new FontFamily("Bold"),
                    Foreground = new SolidColorBrush(Colors.White)
                };
                Binding b = new Binding("Visible");
                b.Source = map1.Layers[0];
                chkbox.SetBinding(CheckBox.IsCheckedProperty, b);
                StackPanel sp = new StackPanel()
                {
                    Background = new SolidColorBrush(Colors.DodgerBlue),
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };
                sp.Children.Add(chkbox);
                GridLeft.Children.Add(sp);
            }
            //add PBS Service layer
            ArcGISTiledMapServiceLayer pbsLayer=new ArcGISTiledMapServiceLayer() { Url = service.UrlArcGIS,ID="PBSLayer" };
            pbsLayer.InitializationFailed+=(s,a)=>{
                MessageBox.Show("PBSLayer" + "\r\n" + (s as ArcGISTiledMapServiceLayer).InitializationFailure.Message + "\r\n" + (s as ArcGISTiledMapServiceLayer).InitializationFailure.InnerException.Message);
            };
            //pbsLayer.Initialized+=(s,a)=>{
            //    map1.ZoomTo(pbsLayer.InitialExtent);
            //};
            //add grid
            _service.DataSource.TileLoaded += new EventHandler<TileLoadEventArgs>(pbsLayer_TileLoaded);
            map1.Layers.Add(pbsLayer);
            _gLayer = new GraphicsLayer();
            map1.Layers.Add(_gLayer);
            _gLayer.Visible = false;

            serviceDetail1.DataContext = service;
            //set LODs's text
            StringBuilder sbLODs = new StringBuilder();
            foreach (LODInfo lod in service.DataSource.TilingScheme.LODs)
            {
                sbLODs.Append("Level:" + lod.LevelID +", Scale:" + lod.Scale + ", Resolution:" + lod.Resolution + "\r\n");
            }
            string str = sbLODs.ToString();
            str = str.Remove(str.Length - 2);//remove last \r \n
            serviceDetail1.tbTSLODs.Text = str;
        }

        void pbsLayer_TileLoaded(object sender, TileLoadEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string key = e.Level + "/" + e.Row + "/" + e.Column;
                //avoid to draw a grid duplicately
                if (_dictDrawedGrids.ContainsKey(key))
                {
                    TileWatermark tw = null;
                    if (_dictDrawedGrids.TryGetValue(key, out tw))
                    {
                        _gLayer.Graphics.Remove(tw.GraphicBoundingBox);
                        _gLayer.Graphics.Remove(tw.GraphicTextLabel);
                        _dictDrawedGrids.TryRemove(key, out tw);
                    }
                }
                double xmin, ymin, xmax, ymax;
                CalculateTileBBox(e.Level, e.Row, e.Column, out xmin, out ymin, out xmax, out ymax);
                //polygon as bounding box
                Polygon boundingLine = new Polygon();
                ESRI.ArcGIS.Client.Geometry.PointCollection pc = new ESRI.ArcGIS.Client.Geometry.PointCollection();
                pc.Add(new MapPoint(xmin, ymax, map1.SpatialReference));
                pc.Add(new MapPoint(xmax, ymax, map1.SpatialReference));
                pc.Add(new MapPoint(xmax, ymin, map1.SpatialReference));
                pc.Add(new MapPoint(xmin, ymin, map1.SpatialReference));
                pc.Add(new MapPoint(xmin, ymax, map1.SpatialReference));
                boundingLine.Rings.Add(pc);
                SimpleFillSymbol sfs = new SimpleFillSymbol()
                {
                    //BorderThickness = 2.0,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255,255)),
                };
                //dynamic-red, filecache-yellow, memcached-green
                if (e.GeneratedMethod == TileGeneratedSource.DynamicOutput)
                    sfs.Fill = new SolidColorBrush(Color.FromArgb(80, 200, 0, 0));
                else if (e.GeneratedMethod == TileGeneratedSource.FromFileCache)
                    sfs.Fill = new SolidColorBrush(Color.FromArgb(80, 200, 200, 0));
                else if (e.GeneratedMethod == TileGeneratedSource.FromMemcached)
                    sfs.Fill = new SolidColorBrush(Color.FromArgb(80, 0, 200, 0));
                Graphic gBoundingBox = new Graphic()
                {
                    Geometry = boundingLine,
                    Symbol = sfs,
                };
                _gLayer.Graphics.Add(gBoundingBox);
                //textsymol
                MapPoint center = new MapPoint((xmin + xmax) / 2, (ymin + ymax) / 2, map1.SpatialReference);
                TextSymbol tSymbol = new TextSymbol()
                {
                    FontSize = 16,
                    Text = "Level/Row/Column\r\n" + e.Level + "/" + e.Row + "/" + e.Column,
                    Foreground = new SolidColorBrush(Colors.White),
                };
                if (_service.DataSource.Type == DataSourceTypePredefined.MBTiles.ToString())
                {
                    int tmsRow, tmsCol;
                    ConvertTMSTileToGoogleTile(e.Level, e.Row, e.Column, out tmsRow, out tmsCol);
                    tSymbol.Text = "Level/Row/Column(TMS)\r\n" + e.Level + "/" + tmsRow + "/" + tmsCol;
                }
                //where the tile comes from
                string generatedSource = FindResource("tbDynamic").ToString();
                //if tile comes from memcached
                if (e.GeneratedMethod == TileGeneratedSource.FromMemcached)
                    generatedSource = FindResource("tbMemCached").ToString();
                //if tile comes from file cache
                else if (e.GeneratedMethod == TileGeneratedSource.FromFileCache)
                    generatedSource = FindResource("tbFileCache").ToString();
                //if tile bytes count
                string bytesCount = e.TileBytes == null ? "null" : Math.Round(e.TileBytes.Length / 1024.0, 3).ToString() + "KB";
                tSymbol.Text += "\r\noutput from: " + generatedSource + "\r\nsize: " + bytesCount;

                tSymbol.OffsetY = 16;
                tSymbol.OffsetX = MeasureTextWidth(tSymbol.Text, 16, "Verdana").Width / 2;
                Graphic gText = new Graphic()
                {
                    Geometry = center,
                    Symbol = tSymbol
                };
                _gLayer.Graphics.Add(gText);

                _dictDrawedGrids.TryAdd(key, new TileWatermark()
                {
                    GraphicBoundingBox = gBoundingBox,
                    GraphicTextLabel = gText
                });
            }));
        }

        private void map1_Progress(object sender, ProgressEventArgs e)
        {
            if (e.Progress == 100)
                progressBar1.Visibility = tbProgress.Visibility = Visibility.Collapsed;
            else
                progressBar1.Visibility = tbProgress.Visibility = Visibility.Visible;
            progressBar1.Value = e.Progress;
        }

        private void map1_ExtentChanged(object sender, ExtentEventArgs e)
        {
            try
            {
                ArcGISTiledMapServiceLayer layer=map1.Layers["PBSLayer"] as ArcGISTiledMapServiceLayer;
            //find current lod
            int i;
            for (i=0;i<layer.TileInfo.Lods.Length;i++)
            {
                if (Math.Abs(map1.Resolution -layer.TileInfo.Lods[i].Resolution) < 0.000001)
                {
                    break;
                }
            }
            tbZoomLevel.Text = FindResource("tbLevel").ToString()+":" + _service.DataSource.TilingScheme.LODs[i].LevelID + " ";
            tbScale.Text = FindResource("tbScale").ToString() + ":" + string.Format("{0:N0}", _service.DataSource.TilingScheme.LODs[i].Scale) + " ";
            tbResolution.Text = FindResource("tbResolution").ToString() + ":" + _service.DataSource.TilingScheme.LODs[i].Resolution;
            //if zoom level changed, then cleargraphics
            if (Math.Abs(_oldResolution - _service.DataSource.TilingScheme.LODs[i].Resolution) > 0.0000001)
            {
                _gLayer.ClearGraphics();
                _dictDrawedGrids.Clear();
            }
             _oldResolution = _service.DataSource.TilingScheme.LODs[i].Resolution;
            }
            catch (Exception)
            {
               
            }            
        }

        private void CalculateTileBBox(int level, int row, int col, out double xmin, out double ymin, out double xmax, out double ymax)
        {
            //calculate the bbox
            double resolution = _service.DataSource.TilingScheme.LODs[level].Resolution;
            PBS.Util.Point origin = _service.DataSource.TilingScheme.TileOrigin;
            int tileRows = _service.DataSource.TilingScheme.TileRows;
            int tileCols = _service.DataSource.TilingScheme.TileCols;
            xmin = origin.X + resolution * tileCols * col;
            ymin = origin.Y - resolution * tileRows * (row + 1);
            xmax = origin.X + resolution * tileCols * (col + 1);
            ymax = origin.Y - resolution * tileRows * row;
        }

        public Size MeasureTextWidth(string text, double fontSize, string typeFace)
        {
            FormattedText ft = new FormattedText(text,
              CultureInfo.CurrentCulture,
              FlowDirection.LeftToRight,
              new Typeface(typeFace),
              fontSize,
              Brushes.Black);
            return new Size(ft.Width, ft.Height);
        }

        private void chkboxDispalyGrid_Click(object sender, RoutedEventArgs e)
        {
            _gLayer.Visible = (bool)(sender as CheckBox).IsChecked;
        }

        private static void ConvertTMSTileToGoogleTile(int level, int row, int col, out int outRow, out int outCol)
        {
            outCol = col;
            outRow = ((int)(Math.Pow(2.0, (double)level) - 1.0)) - row;
        }

        private class TileWatermark
        {
            public Graphic GraphicBoundingBox { get; set; }
            public Graphic GraphicTextLabel { get; set; }
        }
    }
}
