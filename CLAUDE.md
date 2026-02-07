# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Elite Dangerous Addon Launcher V2 is a WPF desktop application that manages the launching of Elite Dangerous alongside companion apps and websites. Users create profiles with different sets of applications (e.g., VR vs Non-VR), and the launcher handles starting all enabled apps in order, monitoring Elite processes, and optionally closing all launched apps when Elite exits.

## Build and Run

**Build the solution:**
```bash
dotnet build "Elite Dangerous Addon Launcher V2.csproj"
```

**Run in Debug mode:**
```bash
dotnet run --project "Elite Dangerous Addon Launcher V2.csproj"
```

**Build for Release:**
```bash
dotnet build "Elite Dangerous Addon Launcher V2.csproj" -c Release
```

**Command-line arguments:**
- `/profile="profilename"` - Load a specific profile
- `/autolaunch` - Automatically launch all enabled apps in the profile

## Technology Stack

- **Framework:** .NET 8 (net8.0-windows10.0.22621.0)
- **UI:** WPF with Material Design Themes 5.1.0
- **Logging:** Serilog with file and console sinks
- **Serialization:** Newtonsoft.Json for profile persistence
- **Drag-Drop:** GongSolutions.Wpf.DragDrop for reordering apps

## Architecture

This application follows the **MVVM (Model-View-ViewModel)** pattern with a clean separation of concerns.

### Application Folders Structure

- **Services/** - Business logic layer (ProfileService, SettingsService, ProcessLaunchService)
- **ViewModels/** - Presentation logic with data binding and commands
- **Commands/** - ICommand implementations (RelayCommand, AsyncRelayCommand)
- **Views/** - XAML files and minimal code-behind
- **Models/** - Data models (Profile, MyApp, Settings)

### Core Data Models

**AppState (AppState.cs)** - Singleton pattern for global application state
- Manages the observable collection of Profiles
- Tracks CurrentProfile and SelectedApp
- Implements INotifyPropertyChanged for UI binding

**Profile (Profile.cs)** - Represents a launch configuration
- Contains an ObservableCollection of MyApp items
- Has Name and IsDefault properties
- Implements custom drag-drop handler (ProfileDropHandler)
- **Important:** Uses `[JsonIgnore]` on DropHandler property to prevent serialization issues
- Uses `[OnDeserialized]` callback to reinitialize DropHandler after JSON deserialization
- Apps property setter reattaches CollectionChanged event handlers after deserialization

**MyApp (MyApp.cs)** - Represents an application or website to launch
- Key properties: Name, Path, ExeName, Args, WebAppURL, IsEnabled, Order
- Supports both local executables and URLs
- InstallationURL field for documentation/download links

**Settings (Settings.cs)** - Stores global settings
- CloseAllAppsOnExit behavior
- Theme preference
- EliteInstallType (Standard, Steam, Epic, Legendary, Manual)

### Services Layer

**IProfileService / ProfileService** - Manages profile persistence
- Location: `%LocalAppData%\Elite Dangerous Addon Launcher V2\profiles.json`
- Automatically migrates from legacy location (`%LocalAppData%\profiles.json`)
- Methods: LoadProfilesAsync(), SaveProfilesAsync(), GetDefaultProfile(), GetProfileByName()

**ISettingsService / SettingsService** - Manages application settings
- Location: `%LocalAppData%\Elite Dangerous Addon Launcher V2\settings.json`
- Automatically migrates from legacy location (`%LocalAppData%\settings.json`)
- Methods: LoadSettingsAsync(), SaveSettingsAsync()

**IProcessLaunchService / ProcessLaunchService** - Handles app launching and process monitoring
- Launches local executables and web URLs
- Monitors Elite Dangerous processes (EDLaunch and EliteDangerous64)
- Raises AllEliteProcessesExited event when all Elite processes terminate
- Closes launched apps on Elite exit if configured
- Methods: LaunchAppAsync(), LaunchAllAppsAsync(), StartMonitoringEliteProcesses(), CloseAllLaunchedApps()

### ViewModels

**MainWindowViewModel** - Main application logic
- Manages profiles, apps, launching, and UI state
- Commands: LaunchAllCommand, LaunchSingleCommand, AddAppCommand, EditAppCommand, etc.
- Properties: CurrentProfile, Apps, SelectedApp, StatusMessage, IsLaunchButtonEnabled
- Uses dependency injection for services

**EliteLauncherDialogViewModel** - Elite Dangerous configuration
- Handles launcher type selection (Standard, Steam, Epic, Legendary, Manual)
- Manages Elite arguments (/autorun, /autoquit, /vr)
- Detects current launcher configuration when editing

### Data Persistence

**Profile Storage:**
- New location: `%LocalAppData%\Elite Dangerous Addon Launcher V2\profiles.json`
- Legacy location: `%LocalAppData%\profiles.json` (automatically migrated)
- Handled by ProfileService

**Application Settings:**
- New location: `%LocalAppData%\Elite Dangerous Addon Launcher V2\settings.json`
- Legacy location: `%LocalAppData%\settings.json` (automatically migrated)
- Handled by SettingsService

**User Preferences (WPF Settings):**
- Uses Properties.Settings for window size, position, column widths
- Accessed via `Properties.Settings.Default.*`

### Elite Dangerous Launcher Detection

The application supports multiple Elite Dangerous installation types through **EliteLauncherDialog**:
- **Standard:** Uses edlaunch.exe from standard Frontier installation
- **Steam:** Uses steam:// URL protocol handler
- **Epic:** Uses Epic Games Store launcher
- **Legendary:** Uses Legendary launcher (open-source Epic alternative)
- **Manual:** User browses to any Elite executable

When editing an existing Elite configuration, the dialog shows the current launcher type and path to help users verify their setup.

### Process Monitoring Architecture

**Launch Flow:**
1. User clicks Launch button → `LaunchAllCommand` in MainWindowViewModel
2. Calls `ProcessLaunchService.LaunchAllAppsAsync()`
3. Service iterates through enabled apps and launches each
4. For Elite apps, starts process monitoring
5. Window minimizes after launch (handled by View)

**Elite Process Monitoring (ProcessLaunchService):**
- `StartMonitoringEliteProcesses()` polls for both "EDLaunch" and "EliteDangerous64" processes
- Maintains `_monitoredEliteProcesses` HashSet with thread-safe `_processLock`
- Each detected process gets event handler: `EliteProcessExitHandler`
- **Critical:** Raises AllEliteProcessesExited event only when ALL Elite processes have exited (handles Steam's multi-process launch)

**Process Cleanup:**
- `CloseAllLaunchedApps()` closes all launched apps if CloseAllAppsOnExit is enabled
- Tracks launched processes in `LaunchedProcesses` list
- Special handling for VoiceAttack (force kill) and Elite Dangerous Odyssey Materials Helper

### Elite Arguments Support

For edlaunch.exe, the app provides special argument checkboxes:
- `/autorun` - Automatically start Elite after launcher loads
- `/autoquit` - Exit launcher when game exits
- `/vr` - Launch in VR mode

These are handled in both AddApp.xaml.cs and EliteLauncherDialog.xaml.cs.

### UI Components (Views)

**MainWindow.xaml/.cs** - Main application window
- DataContext: MainWindowViewModel
- Profile selector (Cb_Profiles ComboBox) bound to Profiles collection
- DataGrid showing apps with drag-drop reordering
- Launch buttons bound to LaunchAllCommand and LaunchSingleCommand
- Minimal code-behind: only view-specific logic (dialogs, window state, theme application)

**EliteLauncherDialog.xaml/.cs** - Specialized dialog for Elite Dangerous setup
- DataContext: EliteLauncherDialogViewModel
- Launcher type buttons with visual selection feedback
- Checkboxes for Elite arguments (/autorun, /autoquit, /vr)
- Displays current configuration when editing
- Code-behind handles dialog result and returns data to caller

**AddApp.xaml/.cs** - Add/Edit application dialog
- Browse for executables or enter web URLs
- Special handling when ExeName is "edlaunch.exe" (shows argument checkboxes)
- Could benefit from ViewModel in future refactoring

**Drag-Drop Implementation:**
- Uses GongSolutions.Wpf.DragDrop library
- ProfileDropHandler.cs implements IDropTarget for app reordering
- Updates Order property on MyApp items after drag-drop

### Elite Dangerous Discovery

**Scan Computer Feature (MainWindow.xaml.cs:162-294):**
- `ScanComputerForEdLaunch()` searches for edlaunch.exe
- Checks common installation paths first (Program Files, Steam, Frontier)
- Shows SearchProgressWindow with cancel support
- Falls back to full drive scan if not found in common locations
- Uses async/await with CancellationToken

### Logging

**Configuration (LoggingConfig.cs):**
- Logs to both console and file
- File location: `%LocalAppData%\Elite Dangerous Addon Launcher V2\logs\`
- Rolling file policy with date-based naming
- LogPath exposed via `LoggingConfig.logFileFullPath`
- Show Logs button opens most recent log file

### Theme Support

- Material Design Themes integrated throughout
- Dark/Light theme switching capability
- Theme state tracked in Settings.Theme
- Primary color brush used for selected launcher buttons

## Common Development Patterns

**Dependency Injection (Services):**
```csharp
// In ViewModel constructor
public MainWindowViewModel(
    IProfileService profileService,
    ISettingsService settingsService,
    IProcessLaunchService processLaunchService)
{
    _profileService = profileService;
    _settingsService = settingsService;
    _processLaunchService = processLaunchService;
}
```

**Working with Profiles (via Service):**
```csharp
// Load profiles
var profiles = await _profileService.LoadProfilesAsync();

// Save profiles
await _profileService.SaveProfilesAsync(Profiles);

// Get default profile
var defaultProfile = _profileService.GetDefaultProfile(profiles);
```

**Launching Apps (via Service):**
```csharp
// Launch single app
await _processLaunchService.LaunchAppAsync(app);

// Launch all enabled apps
await _processLaunchService.LaunchAllAppsAsync(profile.Apps);

// Monitor Elite processes
_processLaunchService.StartMonitoringEliteProcesses();
```

**Creating Commands in ViewModels:**
```csharp
// Synchronous command
LaunchAllCommand = new RelayCommand(
    execute: () => LaunchAllApps(),
    canExecute: () => CurrentProfile != null);

// Asynchronous command
SaveCommand = new AsyncRelayCommand(
    execute: async () => await SaveAsync(),
    canExecute: () => HasChanges);
```

**Property Change Notification:**
```csharp
private string _statusMessage;
public string StatusMessage
{
    get => _statusMessage;
    set => SetProperty(ref _statusMessage, value); // From ViewModelBase
}
```

**UI Updates from Background Threads:**
```csharp
Application.Current.Dispatcher.Invoke(() => {
    // UI updates here
});
```

## Key Files to Understand

**Services:**
- **Services/ProfileService.cs** - Profile persistence with migration from legacy location
- **Services/SettingsService.cs** - Settings persistence with migration from legacy location
- **Services/ProcessLaunchService.cs** - Process launching and Elite monitoring logic

**ViewModels:**
- **ViewModels/MainWindowViewModel.cs** - Main application presentation logic
- **ViewModels/EliteLauncherDialogViewModel.cs** - Elite launcher configuration logic
- **ViewModels/ViewModelBase.cs** - Base class with INotifyPropertyChanged implementation

**Commands:**
- **Commands/RelayCommand.cs** - Synchronous command implementation
- **Commands/AsyncRelayCommand.cs** - Asynchronous command implementation

**Models:**
- **Profile.cs & MyApp.cs** - Core data models with INotifyPropertyChanged
- **Settings.cs** - Application settings model
- **AppState.cs** - Singleton holding global runtime state

**Infrastructure:**
- **Constants.cs** - Application paths and constants
- **ProfileDropHandler.cs** - Custom drag-drop logic for app reordering

## Important Behaviors

1. **Profile Validation:** The app will exit if a command-line profile name is invalid
2. **Elite Process Monitoring:** Only closes apps when ALL Elite-related processes exit (handles Steam multi-process)
3. **Window State:** Saves and restores size/position; validates position is on-screen before restoring
4. **Auto-Resize:** Window adjusts to content when profile changes
5. **Default Profile:** If no default is set, uses first profile in list
6. **Version Checking:** Shows WhatsNew dialog on first run after version update (tracked in Properties.Settings.Default.LastSeenVersion)

## Testing Elite Launcher Types

When testing different Elite installation types:
- Standard: Requires edlaunch.exe in a valid path
- Steam: Uses `steam://rungameid/359320` URL
- Epic: Uses Epic launcher protocol
- Legendary: Uses Legendary CLI launcher
- Manual: Accepts any .exe path (useful for EliteDangerous64.exe direct launch)

The Edit dialog shows current launcher configuration with full path for verification.
