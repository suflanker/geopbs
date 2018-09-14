using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PBS.APP.ViewModels;

namespace PBS.APP
{
    /// <summary>
    /// Interaction logic for OnlineToMBTiles.xaml
    /// </summary>
    public partial class OnlineToMBTiles : Window
    {
        public OnlineToMBTiles(string port)
        {
            InitializeComponent();
            VMConvertOnlineToMBTiles vm;
            try
            {
                vm = new VMConvertOnlineToMBTiles(map1, int.Parse(port));
                this.DataContext = vm;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

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
            this.Closed += (s, a) =>
            {
                vm.Dispose();
                vm = null;
            };
        }
    }
}
