using System;
using System.Windows;
using System.Windows.Navigation;
using System.IO;
using System.Collections.Generic;

namespace Elite_Dangerous_Addon_Launcer_V2
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.Startup += App_Startup;
            this.Exit += App_Exit;
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Equivalent of OnLaunched
            // Create a main window
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            // Equivalent of OnSuspending
            //TODO: Save application state and stop any background activity
        }
    }
}
