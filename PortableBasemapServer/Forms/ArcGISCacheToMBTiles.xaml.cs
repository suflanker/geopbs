using System.Windows;
using PBS.APP.ViewModels;
using System;

namespace PBS.APP
{
    /// <summary>
    /// Interaction logic for ArcGISCacheToMBTiles.xaml
    /// </summary>
    public partial class ArcGISCacheToMBTiles : Window
    {
        public ArcGISCacheToMBTiles()
        {
            InitializeComponent();
            VMConvertAGSToMBTiles vm = new VMConvertAGSToMBTiles();
            this.DataContext = vm;
            //vm.Input = @"d:\arcgisserver\directories\arcgiscache\tianjin\天津\";

            this.Closing += (s, a) =>
            {
                if (!vm.IsIdle)
                {
                    if (MessageBox.Show(Application.Current.FindResource("msgCancelConvert").ToString(), Application.Current.FindResource("msgWarning").ToString(), MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel)
                    {
                        a.Cancel = true;
                        return;
                    }
                    else
                    {
                        vm.Datasource.CancelConverting();
                    }
                }
            };

            //vm.Datasource.ConvertCompleted += (s, a) =>
            //{
            //    if (a.Successful)
            //    {
            //        System.Diagnostics.Process.Start("Explorer", "/select," + vm.Output);
            //        this.Close();
            //    }
            //};
        }
    }
}
