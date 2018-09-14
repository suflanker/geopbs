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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PBS.APP.Controls
{
    /// <summary>
    /// Interaction logic for DownloadLevels.xaml
    /// </summary>
    public partial class DownloadLevels : UserControl
    {
        private bool _setByBinding = true;
        public static readonly DependencyProperty LevelsProperty =
            DependencyProperty.Register("Levels", typeof(int[]), typeof(DownloadLevels), new PropertyMetadata(null, OnLevelsPropertyChanged));
        /// <summary>
        /// 
        /// </summary>
        public int[] Levels
        {
            get
            {
                return (int[])GetValue(LevelsProperty);
            }
            set 
            { 
                SetValue(LevelsProperty, value); 
            }
        }
        private static void OnLevelsPropertyChanged(DependencyObject dependencyObject,
               DependencyPropertyChangedEventArgs e)
        {
            DownloadLevels instance = (DownloadLevels)dependencyObject;
            if (!instance._setByBinding)//avoid infinite loop
                return;
            foreach (UIElement element in instance.root.Children)
            {
                foreach (UIElement uie in (element as StackPanel).Children)
                {
                    CheckBox cb = uie as CheckBox;
                    cb.IsChecked = false;
                }
            }
            foreach (int  i in (int[])e.NewValue)
            {
                if (i <= 9)
                    ((instance.root.Children[0] as StackPanel).Children[i] as CheckBox).IsChecked = true;
                else
                    ((instance.root.Children[1] as StackPanel).Children[i-10] as CheckBox).IsChecked = true;
            }            
        }

        private int[] _levels
        {
            get
            {
                List<int> levels = new List<int>();
                foreach (UIElement element in root.Children)
                {
                    foreach (UIElement e in (element as StackPanel).Children)
                    {
                        CheckBox cb = e as CheckBox;
                        if (cb.IsChecked == true)
                            levels.Add(int.Parse(cb.Content.ToString()));
                    }
                }
                return levels.ToArray();
            }
        }

        public DownloadLevels()
        {
            InitializeComponent();
            foreach (UIElement element in root.Children)
            {
                foreach (UIElement e1 in (element as StackPanel).Children)
                {
                    (e1 as CheckBox).Checked += (s, a) =>
                    {
                        _setByBinding = false;
                        SetValue(LevelsProperty, _levels);
                        _setByBinding = true;
                    };
                    (e1 as CheckBox).Unchecked += (s, a) =>
                        {
                            _setByBinding = false;
                            SetValue(LevelsProperty, _levels);
                            _setByBinding = true;
                        };
                }
            }
        }

        private void btnAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (UIElement element in root.Children)
            {
                foreach (UIElement e1 in (element as StackPanel).Children)
                {
                    (e1 as CheckBox).IsChecked = true;
                }
            }
        }

        private void btnInverse_Click(object sender, RoutedEventArgs e)
        {
            foreach (UIElement element in root.Children)
            {
                foreach (UIElement e1 in (element as StackPanel).Children)
                {
                    (e1 as CheckBox).IsChecked = !(e1 as CheckBox).IsChecked;
                }                
            }
        }
    }
}
