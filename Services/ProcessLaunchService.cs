using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcher_V2.Services
{
    /// <summary>
    /// Service for launching and monitoring processes
    /// </summary>
    public class ProcessLaunchService : IProcessLaunchService
    {
        private readonly HashSet<Process> _monitoredEliteProcesses = new HashSet<Process>();
        private readonly object _processLock = new object();
        private readonly List<string> _launchedProcesses = new List<string>();

        public event EventHandler AllEliteProcessesExited;

        public List<string> LaunchedProcesses => _launchedProcesses;

        public async Task<bool> LaunchAppAsync(MyApp app)
        {
            if (app == null)
            {
                Log.Warning("Attempted to launch null app");
                return false;
            }

            Log.Information("Launching app: {AppName}", app.Name);

            // Handle web launches (Steam, Epic, Legendary, websites)
            if (!string.IsNullOrEmpty(app.WebAppURL))
            {
                return LaunchWebApp(app);
            }

            // Handle local executable launches
            return LaunchLocalApp(app);
        }

        public async Task LaunchAllAppsAsync(IEnumerable<MyApp> apps)
        {
            if (apps == null)
                return;

            foreach (var app in apps.Where(a => a.IsEnabled))
            {
                await LaunchAppAsync(app);
                // Small delay between launches
                await Task.Delay(100);
            }

            Log.Information("Finished launching all enabled apps");
        }

        public void StartMonitoringEliteProcesses()
        {
            Log.Information("Starting Elite process monitoring...");

            Task.Run(() =>
            {
                // Wait for Elite processes to start (Steam can take a few seconds)
                for (int i = 0; i < 20; i++) // Try for up to 10 seconds
                {
                    var edLaunchProcesses = Process.GetProcessesByName("EDLaunch");
                    var eliteProcesses = Process.GetProcessesByName("EliteDangerous64");

                    lock (_processLock)
                    {
                        // Monitor EDLaunch processes
                        foreach (var proc in edLaunchProcesses)
                        {
                            if (!_monitoredEliteProcesses.Any(p => p.Id == proc.Id))
                            {
                                proc.EnableRaisingEvents = true;
                                proc.Exited += EliteProcessExitHandler;
                                _monitoredEliteProcesses.Add(proc);
                                _launchedProcesses.Add(proc.ProcessName);
                                Log.Information("Now monitoring EDLaunch process (ID: {ProcessId})", proc.Id);
                            }
                        }

                        // Monitor EliteDangerous64 processes
                        foreach (var proc in eliteProcesses)
                        {
                            if (!_monitoredEliteProcesses.Any(p => p.Id == proc.Id))
                            {
                                proc.EnableRaisingEvents = true;
                                proc.Exited += EliteProcessExitHandler;
                                _monitoredEliteProcesses.Add(proc);
                                _launchedProcesses.Add(proc.ProcessName);
                                Log.Information("Now monitoring EliteDangerous64 process (ID: {ProcessId})", proc.Id);
                            }
                        }

                        // If we found processes, we're done
                        if (_monitoredEliteProcesses.Count > 0)
                        {
                            Log.Information("Found and monitoring {Count} Elite processes", _monitoredEliteProcesses.Count);
                            break;
                        }
                    }

                    Thread.Sleep(500);
                }
            });
        }

        public void StopMonitoringEliteProcesses()
        {
            lock (_processLock)
            {
                Log.Information("Stopping Elite process monitoring for {Count} processes", _monitoredEliteProcesses.Count);

                foreach (var proc in _monitoredEliteProcesses.ToList())
                {
                    try
                    {
                        proc.Exited -= EliteProcessExitHandler;
                        proc.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error while cleaning up process monitoring");
                    }
                }

                _monitoredEliteProcesses.Clear();
            }
        }

        public void CloseAllLaunchedApps()
        {
            Log.Information("CloseAllAppsOnExit is enabled, closing all apps..");

            try
            {
                foreach (string processName in _launchedProcesses.Distinct())
                {
                    Log.Information("Closing {ProcessName}", processName);

                    foreach (Process process in Process.GetProcessesByName(processName))
                    {
                        try
                        {
                            process.CloseMainWindow();
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to close {ProcessName}", processName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred trying to close all apps");
            }

            // Special handling for VoiceAttack - needs to be killed forcefully
            try
            {
                Process[] procs = Process.GetProcessesByName("VoiceAttack");
                foreach (var proc in procs)
                {
                    proc.Kill();
                    Log.Information("Force killed VoiceAttack");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to kill VoiceAttack");
            }

            // Special handling for Elite Dangerous Odyssey Materials Helper
            try
            {
                Process[] procs = Process.GetProcessesByName("Elite Dangerous Odyssey Materials Helper");
                foreach (var proc in procs)
                {
                    proc.CloseMainWindow();
                    Log.Information("Closed Elite Dangerous Odyssey Materials Helper");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to close Elite Dangerous Odyssey Materials Helper");
            }
        }

        private bool LaunchWebApp(MyApp app)
        {
            try
            {
                Process.Start(new ProcessStartInfo(app.WebAppURL) { UseShellExecute = true });
                Log.Information("Launched {AppName} via {WebAppURL}", app.Name, app.WebAppURL);

                // For Elite Dangerous launches, set up process monitoring
                if (IsEliteApp(app))
                {
                    Log.Information("Elite launched via web URL, setting up process monitoring");
                    StartMonitoringEliteProcesses();
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error launching {AppName}", app.Name);
                return false;
            }
        }

        private bool LaunchLocalApp(MyApp app)
        {
            string path = Path.Combine(app.Path, app.ExeName);
            string args = PrepareArguments(app);

            if (!File.Exists(path))
            {
                Log.Warning("File not found: {Path}", path);
                return false;
            }

            try
            {
                var info = new ProcessStartInfo(path)
                {
                    Arguments = args,
                    UseShellExecute = true,
                    WorkingDirectory = app.Path
                };

                Process proc = Process.Start(info);
                proc.EnableRaisingEvents = true;
                _launchedProcesses.Add(proc.ProcessName);

                // For Elite Dangerous, use monitoring system
                if (IsEliteApp(app))
                {
                    lock (_processLock)
                    {
                        proc.Exited += EliteProcessExitHandler;
                        _monitoredEliteProcesses.Add(proc);
                        Log.Information("Added direct Elite process to monitoring (ID: {ProcessId})", proc.Id);
                    }
                }

                Thread.Sleep(50);
                proc.Refresh();

                Log.Information("Successfully launched {AppName}", app.Name);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error launching {AppName}", app.Name);
                return false;
            }
        }

        private string PrepareArguments(MyApp app)
        {
            // Special handling for TARGET
            if (string.Equals(app.ExeName, "targetgui.exe", StringComparison.OrdinalIgnoreCase))
            {
                return $"-r \"{app.Args}\"";
            }

            return app.Args ?? string.Empty;
        }

        private bool IsEliteApp(MyApp app)
        {
            if (app == null)
                return false;

            // Check if it's a local Elite exe
            if (!string.IsNullOrEmpty(app.ExeName) &&
                (app.ExeName.Equals("edlaunch.exe", StringComparison.OrdinalIgnoreCase) ||
                 app.ExeName.Equals("EliteDangerous64.exe", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Check if it's a web launcher for Elite
            if (!string.IsNullOrEmpty(app.WebAppURL) &&
                (app.WebAppURL.Contains("rungameid/359320") ||
                 app.WebAppURL.Contains("epic://launch") ||
                 app.WebAppURL.Contains("legendary://launch")))
            {
                return true;
            }

            // Check if the name is exactly "Elite Dangerous" or a launcher variant
            if (!string.IsNullOrEmpty(app.Name))
            {
                string name = app.Name.Trim();

                if (name.Equals("Elite Dangerous", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (name.StartsWith("Elite Dangerous (", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(")", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void EliteProcessExitHandler(object sender, EventArgs e)
        {
            var exitedProcess = sender as Process;
            if (exitedProcess != null)
            {
                string processName = "Unknown";
                int processId = -1;

                try
                {
                    processName = exitedProcess.ProcessName;
                    processId = exitedProcess.Id;
                }
                catch (InvalidOperationException)
                {
                    processName = "Elite Process";
                    processId = -1;
                }

                Log.Information("Elite process exited: {ProcessName} (ID: {ProcessId})", processName, processId);

                lock (_processLock)
                {
                    _monitoredEliteProcesses.Remove(exitedProcess);
                    Log.Information("Remaining Elite processes being monitored: {Count}", _monitoredEliteProcesses.Count);

                    // Only trigger event when ALL Elite processes have exited
                    if (_monitoredEliteProcesses.Count == 0)
                    {
                        Log.Information("All Elite processes have exited, triggering event");
                        AllEliteProcessesExited?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }
    }
}
