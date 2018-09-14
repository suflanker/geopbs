using System;
using PBS.DataSource;
using System.Windows.Input;
using PBS.APP.Classes;
using System.Windows.Forms;
using System.IO;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace PBS.APP.ViewModels
{
    public class VMConvertAGSToMBTiles:INotifyPropertyChanged
    {
        private string _input;
        public string Input
        {
            get { return _input; }
            set
            {
                _input = value;
                NotifyPropertyChanged(p => p.Input);
                (CMDClickStartButton as DelegateCommand).RaiseCanExecuteChanged();
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
        public string Name { get; set; }
        public string Description { get; set; }
        public string Attribution { get; set; }
        public bool DoCompact { get; set; }
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

        private DataSourceArcGISCache _datasource;
        public DataSourceArcGISCache Datasource
        {
            get { return _datasource; }
            set
            {
                _datasource = value;
                NotifyPropertyChanged(p => p.Datasource);
            }
        }


        public ICommand CMDClickBrowseButton { get; private set; }
        public ICommand CMDClickStartButton { get; private set; }

        public VMConvertAGSToMBTiles()
        {
            IsIdle = true;
            CMDClickBrowseButton = new DelegateCommand(BrowseButtonClicked);
            CMDClickStartButton = new DelegateCommand(StartButtonClicked, (p) => { return !string.IsNullOrEmpty(Input) && !string.IsNullOrEmpty(Output); });
        }

        private void BrowseButtonClicked(object parameters)
        {
            string param = parameters.ToString();
            switch (param)
            {
                case "input":
                    FolderBrowserDialog fbd = new FolderBrowserDialog();
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        if (!Directory.Exists(fbd.SelectedPath + @"\_alllayers"))
                        {
                            MessageBox.Show(App.Current.FindResource("msg_alllayersNotExist").ToString());
                            return;
                        }
                        Input = fbd.SelectedPath;
                    }
                    break;
                case "output":
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
                    break;
                default:
                    break;
            }
        }

        private void StartButtonClicked(object parameters)
        {
            if (!IsValidFilename(Output))
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
                Datasource = new DataSourceArcGISCache(Input);
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
                    try
                    {
                        Datasource.ConvertToMBTiles(Output, Name, Description, Attribution,DoCompact);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, App.Current.FindResource("msgError").ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }                    
                    IsIdle = true;
                };
                bw.RunWorkerAsync();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged<TValue>(Expression<Func<VMConvertAGSToMBTiles, TValue>> propertySelector)
        {
            if (PropertyChanged == null)
                return;

            var memberExpression = propertySelector.Body as MemberExpression;
            if (memberExpression == null)
                return;

            PropertyChanged(this, new PropertyChangedEventArgs(memberExpression.Member.Name));
        }

        /// <summary>
        /// full file name including path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsValidFilename(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            Regex regEx = new Regex("[\\*\\\\/:?<>|\"]");

            return !regEx.IsMatch(Path.GetFileName(path));
        }
    }
}
