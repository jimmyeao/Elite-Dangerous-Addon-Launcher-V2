using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using Serilog;
using Elite_Dangerous_Addon_Launcher_V2.ViewModels;
using Application = System.Windows.Application;

namespace Elite_Dangerous_Addon_Launcher_V2.Services
{
    /// <summary>
    /// Manages system tray icon and functionality
    /// </summary>
    public class SystemTrayManager : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private readonly MainWindowViewModel _viewModel;
        private Window _mainWindow;

        public SystemTrayManager(MainWindowViewModel viewModel, Window mainWindow)
        {
            _viewModel = viewModel;
            _mainWindow = mainWindow;
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = ExtractIconFromExecutable(),
                Text = "Elite Dangerous Addon Launcher",
                Visible = true
            };

            _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
            _notifyIcon.ContextMenuStrip = CreateContextMenu();

            Log.Information("System tray icon initialized");
        }

        private Icon ExtractIconFromExecutable()
        {
            try
            {
                // Try to load the application icon
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "elite-dangerous-icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load application icon, using default");
            }

            // Fallback to default application icon
            return SystemIcons.Application;
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var contextMenu = new ContextMenuStrip();

            // Show/Hide window
            var showHideItem = new ToolStripMenuItem("Show/Hide Window");
            showHideItem.Click += (s, e) => ToggleWindowVisibility();
            showHideItem.Font = new Font(showHideItem.Font, System.Drawing.FontStyle.Bold);
            contextMenu.Items.Add(showHideItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Quick Launch Profiles section
            var profilesItem = new ToolStripMenuItem("Quick Launch Profile");

            // Subscribe to opening event to refresh profiles dynamically
            profilesItem.DropDownOpening += (s, e) => RefreshProfilesMenu(profilesItem);

            contextMenu.Items.Add(profilesItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            return contextMenu;
        }

        private void RefreshProfilesMenu(ToolStripMenuItem profilesMenuItem)
        {
            profilesMenuItem.DropDownItems.Clear();

            if (_viewModel.Profiles == null || _viewModel.Profiles.Count == 0)
            {
                var noProfilesItem = new ToolStripMenuItem("No profiles available");
                noProfilesItem.Enabled = false;
                profilesMenuItem.DropDownItems.Add(noProfilesItem);
                return;
            }

            foreach (var profile in _viewModel.Profiles)
            {
                var profileItem = new ToolStripMenuItem(profile.Name);
                profileItem.Tag = profile;

                // Mark default profile
                if (profile.IsDefault)
                {
                    profileItem.Text = $"{profile.Name} (Default)";
                }

                profileItem.Click += async (s, e) =>
                {
                    try
                    {
                        // Switch to profile and launch
                        _viewModel.CurrentProfile = profile;
                        await _viewModel.LaunchAllAppsAsync();

                        UpdateTooltip($"Launching: {profile.Name}");
                        Log.Information("Quick launched profile from tray: {ProfileName}", profile.Name);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to quick launch profile: {ProfileName}", profile.Name);
                    }
                };

                profilesMenuItem.DropDownItems.Add(profileItem);
            }
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ToggleWindowVisibility();
        }

        private void ToggleWindowVisibility()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_mainWindow.WindowState == WindowState.Minimized || !_mainWindow.IsVisible)
                {
                    RestoreWindow();
                }
                else
                {
                    MinimizeWindow();
                }
            });
        }

        public void RestoreWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
                Log.Debug("Window restored from tray");
            });
        }

        public void MinimizeWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _mainWindow.WindowState = WindowState.Minimized;
                _mainWindow.Hide();
                Log.Debug("Window minimized to tray");
            });
        }

        public void UpdateTooltip(string message)
        {
            if (_notifyIcon != null)
            {
                // Tooltip max length is 63 characters
                _notifyIcon.Text = message.Length > 63 ? message.Substring(0, 60) + "..." : message;
            }
        }

        public void ShowBalloonTip(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
        {
            _notifyIcon?.ShowBalloonTip(timeout, title, message, icon);
        }

        private void ExitApplication()
        {
            Log.Information("Exiting application from system tray");
            Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
