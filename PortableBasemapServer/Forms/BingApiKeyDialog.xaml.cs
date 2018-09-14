using System.Diagnostics;
using System.Windows;
using System.Configuration;
using PBS.Service;
using System;
using System.IO;

namespace PBS.APP
{
    /// <summary>
    /// Interaction logic for BingApiKeyDialog.xaml
    /// </summary>
    public partial class BingApiKeyDialog : Window
    {
        public BingApiKeyDialog()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            System.Configuration.Configuration config=null;
            //do not use ConfigurationUserLevel.None, otherwise saving settings will be failed.
            try
            {
                //do not use ConfigurationUserLevel.None, otherwise saving settings will be failed.
                config = ConfigurationManager.OpenExeConfiguration("PortableBasemapServer.exe");
                if (!config.HasFile)
                    throw new FileNotFoundException(FindResource("msgConfigFileNotExist").ToString());
            }
            catch (Exception ee)
            {
                MessageBox.Show(FindResource("msgConfigFileBroken").ToString() + "\r\n" + ee.Message);
            }
            config.AppSettings.Settings["BingApiKey"].Value = tbKey.Text;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
            ConfigManager.App_BingMapsAppKey = tbKey.Text;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
