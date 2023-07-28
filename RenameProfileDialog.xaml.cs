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

namespace Elite_Dangerous_Addon_Launcer_V2
{
    /// <summary>
    /// Interaction logic for RenameProfileDialog.xaml
    /// </summary>
    public partial class RenameProfileDialog : Window
    {
        public string NewName { get; private set; }

        public RenameProfileDialog(string currentName)
        {
            InitializeComponent();

            ProfileNameTextBox.Text = currentName;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            NewName = ProfileNameTextBox.Text;
            this.DialogResult = true;
        }
    }
}
