using System.Windows;

namespace Elite_Dangerous_Addon_Launcher_V2
{
    public partial class App : Application
    {
        public static string ProfileName { get; set; }
        public static bool AutoLaunch { get; set; }
        public App()
        {
            this.Exit += App_Exit;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Reset ProfileName to null at the start of each session
            App.ProfileName = null;

            // e.Args is a string array that contains the command-line arguments
            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                // process arguments
                if (arg.StartsWith("/profile="))
                {
                    App.ProfileName = arg.Substring("/profile=".Length);
                    // Now App.ProfileName contains the profile name passed as argument
                }
                else if (arg.Equals("/autolaunch"))
                {
                    // The /autolaunch argument was passed
                    App.AutoLaunch = true;
                }
            }

            // Create main window and open it
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
