using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Linq;
using PBS.Util;

namespace PBS.APP
{
    /// <summary>
    /// Interaction logic for ConfigWindow.xaml
    /// </summary>
    public partial class ConfigWindow : Window
    {
        ConfigManager _cm;
        public ConfigWindow()
        {
            InitializeComponent();
            _cm = ConfigManager.Instance;
            lvServices.DataContext = _cm.Configurations;
        }

        private void lvServices_GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader)
            {
                //Get clicked column
                GridViewColumn clickedColumn = (e.OriginalSource as GridViewColumnHeader).Column;
                if (clickedColumn != null)
                {
                    //Get binding property of clicked column
                    string bindingProperty = (clickedColumn.DisplayMemberBinding as System.Windows.Data.Binding).Path.Path;
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

        private void lvServices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvServices.SelectedItem != null)
            {
                Configuration config = lvServices.SelectedItem as Configuration;
                txtboxConfigName.Text = config.Name;
            }
        }

        private void btn_Click(object sender, RoutedEventArgs e)
        {
            string result = string.Empty;
            Button btn = sender as Button;
            if (btn==btnSave)
            {
                if (txtboxConfigName.Text.Trim()=="")
                {
                    MessageBox.Show(FindResource("msgConfigNameEmpty").ToString(), FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (_cm.IsConfigurationExists(txtboxConfigName.Text))
                {
                    if (MessageBox.Show(FindResource("msgOverwriteConfigWarning").ToString(), FindResource("msgWarning").ToString(), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        return;
                }
                result = _cm.SaveConfigurationWithOverwrite(txtboxConfigName.Text);
                if(result!=string.Empty)
                    MessageBox.Show(result, FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if (btn==btnLoad)
            {
                if (!_cm.IsConfigurationExists(txtboxConfigName.Text))
                {
                    MessageBox.Show(txtboxConfigName.Text + FindResource("msgConfigNotExist").ToString(), FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (MessageBox.Show(FindResource("msgLoadConfigWarning").ToString(),FindResource("msgWarning").ToString(),MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes)
                {
                    result = _cm.LoadConfigurationAndStartServices(txtboxConfigName.Text);
                    if (result != string.Empty)
                        MessageBox.Show(result, FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                    else
                        this.Close();
                }
            }
            if (btn==btnDelete)
            {
                if (!_cm.IsConfigurationExists(txtboxConfigName.Text))
                {
                    MessageBox.Show(txtboxConfigName.Text + FindResource("msgConfigNotExist").ToString(), FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (MessageBox.Show(FindResource("msgConfigDeleteWarning").ToString(), FindResource("msgWarning").ToString(), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    result = _cm.DeleteConfiguration(txtboxConfigName.Text);
                    if (result != string.Empty)
                        MessageBox.Show(result, FindResource("msgError").ToString(), MessageBoxButton.OK, MessageBoxImage.Error);                    
                }
            }
        }
    }
}
