using System;
using System.Windows;

namespace Elite_Dangerous_Addon_Launcher_V2.Views
{
    /// <summary>
    /// Custom dialog for maintaining consistent look and feel throughout the application
    /// </summary>
    public partial class CustomDialog : Window
    {
        public MessageBoxResult Result { get; set; }
        public string Title { get; set; }

        public CustomDialog(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK)
        {
            InitializeComponent();
            DataContext = this;

            Title = title;
            MessageTextBlock.Text = message;

            // Configure buttons based on MessageBoxButton type
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    YesOkButton.Content = "OK";
                    NoCancelButton.Visibility = Visibility.Collapsed;
                    break;

                case MessageBoxButton.OKCancel:
                    YesOkButton.Content = "OK";
                    NoCancelButton.Content = "Cancel";
                    break;

                case MessageBoxButton.YesNo:
                    YesOkButton.Content = "Yes";
                    NoCancelButton.Content = "No";
                    break;

                case MessageBoxButton.YesNoCancel:
                    YesOkButton.Content = "Yes";
                    NoCancelButton.Content = "No";
                    // Would need a third button for Cancel, but this is rarely used
                    break;
            }
        }

        private void YesOkButton_Click(object sender, RoutedEventArgs e)
        {
            if (YesOkButton.Content.ToString() == "OK")
            {
                Result = MessageBoxResult.OK;
            }
            else
            {
                Result = MessageBoxResult.Yes;
            }
            this.DialogResult = true;
            this.Close();
        }

        private void NoCancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (NoCancelButton.Content.ToString() == "Cancel")
            {
                Result = MessageBoxResult.Cancel;
            }
            else
            {
                Result = MessageBoxResult.No;
            }
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Static helper method to show a custom dialog (similar to MessageBox.Show)
        /// </summary>
        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, Window owner = null)
        {
            var dialog = new CustomDialog(message, title, buttons);
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}
