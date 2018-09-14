using System.Windows;
using PBS.DataSource;
using System.Diagnostics;
using PBS.Service;

namespace PBS.APP
{
    /// <summary>
    /// Interaction logic for DynamicMapServiceParams.xaml
    /// </summary>
    public partial class ArcGISDynamicMapServiceParams : Window
    {
        DataSourceArcGISDynamicMapService _arcgisDynamicMapService;
        public ArcGISDynamicMapServiceParams(PBS.Service.PBSService service)
        {
            InitializeComponent();
            if (!(service.DataSource is DataSourceArcGISDynamicMapService))
            {
                System.Windows.MessageBox.Show(FindResource("msgDatasourceTypeError").ToString());
                this.Close();
            }
            _arcgisDynamicMapService = service.DataSource as DataSourceArcGISDynamicMapService;

            txtbox_layers.Text = _arcgisDynamicMapService.exportParam_layers;
            txtbox_layerDefs.Text = _arcgisDynamicMapService.exportParam_layerDefs;
            txtbox_time.Text = _arcgisDynamicMapService.exportParam_time;
            txtbox_layerTimeOptions.Text = _arcgisDynamicMapService.exportParam_layerTimeOptions;

            if (double.Parse(_arcgisDynamicMapService.AGSVersion) >= 10)
                sp100.Visibility = Visibility.Visible;
            else
                sp100.Visibility = Visibility.Collapsed;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            //no checking for any params, these params must be ensured by end user.
            _arcgisDynamicMapService.exportParam_layers = txtbox_layers.Text;
            _arcgisDynamicMapService.exportParam_layerDefs = txtbox_layerDefs.Text;
            _arcgisDynamicMapService.exportParam_time = txtbox_time.Text;
            _arcgisDynamicMapService.exportParam_layerTimeOptions = txtbox_layerTimeOptions.Text;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
