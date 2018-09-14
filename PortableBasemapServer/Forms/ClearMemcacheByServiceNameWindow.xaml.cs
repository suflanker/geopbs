using System.Windows;
using PBS.Service;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.ServiceModel.Web;
using System.ServiceModel.Description;
using System.ServiceModel;
using PBS.DataSource;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.ComponentModel;

namespace PBS.APP
{
    /// <summary>
    /// Interaction logic for ClearMemcacheWindow.xaml
    /// </summary>
    public partial class ClearMemcacheByServiceNameWindow : Window
    {
        private ObservableCollection<MemcachedService> _listMemcachedService;
        public ClearMemcacheByServiceNameWindow()
        {
            InitializeComponent();

            _listMemcachedService = new ObservableCollection<MemcachedService>();
            foreach (PBSService service in ServiceManager.Services)
            {
                if (service.AllowMemCache)
                {
                    _listMemcachedService.Add(new MemcachedService(false, service));
                }
            }
            this.DataContext = _listMemcachedService;
            if (_listMemcachedService.Count == 0)
                btnClear.IsEnabled = btnSelectAll.IsEnabled = btnUnSelectAll.IsEnabled = false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == btnSelectAll)
            {
                foreach (MemcachedService service in _listMemcachedService)
                {
                    service.IsChecked = true;
                }
            }
            if (btn==btnUnSelectAll)
            {
                foreach (MemcachedService service in _listMemcachedService)
                {
                    service.IsChecked = false;
                }
            }
            if (btn==btnClear)
            {
                if (ServiceManager.Memcache != null)
                {
                    foreach (MemcachedService service in _listMemcachedService)
                    {
                        if (service.IsChecked)
                        {
                            ServiceManager.Memcache.InvalidateServiceMemcache(service.PBSService.Port, service.PBSService.ServiceName);
                        }
                    }
                    MessageBox.Show(FindResource("msgSelectedServiceMemoryCacheCleared").ToString());
                    this.Close();
                }
            }
        }

        private class MemcachedService : INotifyPropertyChanged
        {
            private bool _isChecked;
            public bool IsChecked { get { return _isChecked; } set { _isChecked = value; NotifyPropertyChanged("IsChecked"); } }
            public PBSService PBSService { get; set; }

            public MemcachedService(bool isChecked, PBSService service)
            {
                IsChecked = isChecked;
                PBSService = service;
            }

            #region INotifyPropertyChanged
            public event PropertyChangedEventHandler PropertyChanged;

            public void NotifyPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }
            #endregion
        }        
    }
}
