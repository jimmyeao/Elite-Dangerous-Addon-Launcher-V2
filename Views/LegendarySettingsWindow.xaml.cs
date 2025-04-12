using Elite_Dangerous_Addon_Launcher_V2.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Elite_Dangerous_Addon_Launcher_V2.Views
{
    /// <summary>
    /// Interaction logic for LegendarySettingsWindow.xaml
    /// </summary>
    public partial class LegendarySettingsWindow : Window
    {
        public LegendarySettingsWindow()
        {
            InitializeComponent();
            ParamsTextBox.Text = LegendaryConfigManager.CurrentStartParams;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            LegendaryConfigManager.UpdateStartParams(ParamsTextBox.Text.Trim());
            MessageBox.Show("Launch parameters saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
