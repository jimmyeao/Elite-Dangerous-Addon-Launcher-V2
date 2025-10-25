using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Elite_Dangerous_Addon_Launcher_V2.Services
{
    /// <summary>
    /// Service for launching and monitoring processes
    /// </summary>
    public interface IProcessLaunchService
    {
        /// <summary>
        /// Event raised when all Elite processes have exited
        /// </summary>
        event EventHandler AllEliteProcessesExited;

        /// <summary>
        /// Gets the list of launched process names
        /// </summary>
        List<string> LaunchedProcesses { get; }

        /// <summary>
        /// Launches an application
        /// </summary>
        Task<bool> LaunchAppAsync(MyApp app);

        /// <summary>
        /// Launches all enabled apps in a profile
        /// </summary>
        Task LaunchAllAppsAsync(IEnumerable<MyApp> apps);

        /// <summary>
        /// Starts monitoring Elite Dangerous processes
        /// </summary>
        void StartMonitoringEliteProcesses();

        /// <summary>
        /// Stops monitoring Elite Dangerous processes
        /// </summary>
        void StopMonitoringEliteProcesses();

        /// <summary>
        /// Closes all launched applications
        /// </summary>
        void CloseAllLaunchedApps();
    }
}
