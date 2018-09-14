using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using PBS.APP.ViewModels;
using System.IO;
using System.Collections.Generic;
using System;
using System.Text;
using PBS.Service;

namespace PBS.APP
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(VMMainView vm)
        {
            InitializeComponent();
            this.DataContext = vm;
            #region ui stuff
            #region system tray
            //WPF System Tray Application:http://social.msdn.microsoft.com/Forums/en/wpf/thread/21992d0b-a02c-4042-a188-47b0a2b99b0b            
            System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();
            using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(Application.GetResourceStream(new Uri("/PortableBasemapServer;component/Images/Map.png", UriKind.RelativeOrAbsolute)).Stream))
            {
                IntPtr ptr = bmp.GetHicon();
                notifyIcon.Icon = new System.Drawing.Icon(System.Drawing.Icon.FromHandle(ptr), 40, 40);
                PBS.Util.Utility.DeleteObject(ptr);
            }
            notifyIcon.DoubleClick += (s, a) =>
            {
                if (this.WindowState == WindowState.Minimized)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                }
                else
                {
                    this.WindowState = WindowState.Minimized;
                }
            };
            this.StateChanged += (s, a) =>
            {
                if (vm.IsShowInSysTray)
                {
                    if (this.WindowState == WindowState.Minimized)
                    {
                        this.Hide();
                        notifyIcon.Visible = true;
                        //_notifyIcon.BalloonTipTitle = "PBS";
                        //_notifyIcon.BalloonTipText = FindResource("msgSysTrayBalloon").ToString();
                        //_notifyIcon.ShowBalloonTip(1000);
                    }
                }
                else
                    notifyIcon.Visible = false;
            };
            //context menu
            System.Windows.Forms.ContextMenuStrip cms = new System.Windows.Forms.ContextMenuStrip();
            System.Windows.Forms.ToolStripMenuItem cmiExit = new System.Windows.Forms.ToolStripMenuItem();
            cmiExit.Click += (s, a) =>
            {
                Application.Current.Shutdown();
            };
            cms.Items.Add(cmiExit);
            cms.Opened += (s, a) =>
            {
                cmiExit.Text = Application.Current.FindResource("cmiExit").ToString();
            };
            notifyIcon.ContextMenuStrip = cms;
            notifyIcon.MouseMove += (s, a) =>
            {
                string usingPorts = string.Empty;
                foreach (KeyValuePair<int, PortEntity> kv in ServiceManager.PortEntities)
                {
                    usingPorts += kv.Key.ToString() + ", ";
                }
                if (!string.IsNullOrEmpty(usingPorts))
                    usingPorts = usingPorts.Remove(usingPorts.Length - 2, 2);
                notifyIcon.Text = Application.Current.FindResource("msgUsingPorts").ToString() + usingPorts + "\r\n" + Application.Current.FindResource("msgStartedServiceCount").ToString() + ServiceManager.Services.Count;
            };            
            if (vm.IsShowInSysTray)
                notifyIcon.Visible = true;
            vm.IsShowInSysTrayChanged += (s, a) =>
            {
                notifyIcon.Visible = (s as VMMainView).IsShowInSysTray;
            };
            #endregion
            //window closing
            Application.Current.MainWindow.Closing += (s, a) =>
            {
                if (ServiceManager.Services.Count > 0 && !vm.IsLoadLastConfiguration)
                {
                    if (MessageBox.Show(Application.Current.FindResource("msgExitWarning").ToString(), Application.Current.FindResource("msgWarning").ToString(), MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel)
                    {
                        a.Cancel = true;
                        return;
                    }
                }
                vm.DoCleanJobs();
                if (notifyIcon != null)
                {
                    notifyIcon.Dispose();
                    notifyIcon = null;
                }
            };
            #endregion
            this.Show();
        }

        private void lvServices_GridViewColumnHeader_Click(object sender, RoutedEventArgs e)//sort
        {
            //ref:WPF中对ListView排序
            //http://www.cnblogs.com/huihui0630/archive/2008/10/22/1317140.html
            if (e.OriginalSource is GridViewColumnHeader)
            {
                //Get clicked column
                GridViewColumn clickedColumn = (e.OriginalSource as GridViewColumnHeader).Column;
                if (clickedColumn != null)
                {
                    string bindingProperty;
                    //Get binding property of clicked column
                    if (clickedColumn.DisplayMemberBinding is System.Windows.Data.Binding)
                        bindingProperty = (clickedColumn.DisplayMemberBinding as System.Windows.Data.Binding).Path.Path;
                    else//multibinding for Output Tile Count column
                    {
                        System.Windows.Data.Binding binding=(clickedColumn.DisplayMemberBinding as System.Windows.Data.MultiBinding).Bindings[0] as System.Windows.Data.Binding;
                        bindingProperty = binding.Path.Path;
                    }
                    SortDescriptionCollection sdc = lvServices.Items.SortDescriptions;
                    ListSortDirection sortDirection = ListSortDirection.Ascending;
                    if (sdc.Count > 0)
                    {
                        SortDescription sd = sdc[0];
                        sortDirection = (ListSortDirection)((((int)sd.Direction) + 1) % 2);
                        sdc.Clear();
                    }
                    sdc.Add(new SortDescription(bindingProperty, sortDirection));
                }
            }
        }
    }
}
