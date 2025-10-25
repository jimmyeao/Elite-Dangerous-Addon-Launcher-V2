# MVVM Refactoring Summary

**Date:** October 25, 2025
**Status:** ✅ Complete and Tested

## Overview

Successfully refactored Elite Dangerous Addon Launcher V2 from code-behind architecture to proper **MVVM (Model-View-ViewModel)** pattern with clean separation of concerns, dependency injection, and automatic settings migration.

## Changes Made

### 1. Services Layer (NEW)

Created a complete services layer to handle all business logic:

#### Files Created:
- `Constants.cs` - Application-wide constants and paths
- `Services/IProfileService.cs` - Profile service interface
- `Services/ProfileService.cs` - Profile persistence with migration
- `Services/ISettingsService.cs` - Settings service interface
- `Services/SettingsService.cs` - Settings persistence with migration
- `Services/IProcessLaunchService.cs` - Process launch service interface
- `Services/ProcessLaunchService.cs` - Process launching and monitoring

#### Key Features:
- **Automatic Migration**: Settings and profiles automatically migrate from legacy location
  - Old: `%LocalAppData%\profiles.json` and `%LocalAppData%\settings.json`
  - New: `%LocalAppData%\Elite Dangerous Addon Launcher V2\profiles.json` and `settings.json`
- **Dependency Injection**: Services injected into ViewModels for testability
- **Separation of Concerns**: Business logic separated from UI

### 2. Command Infrastructure (NEW)

Created command infrastructure for MVVM pattern:

#### Files Created:
- `Commands/IRelayCommand.cs` - Common interface for commands
- `Commands/RelayCommand.cs` - Synchronous command implementation
- `Commands/AsyncRelayCommand.cs` - Asynchronous command implementation

#### Features:
- Automatic CanExecute management during async execution
- Event-based command state updates
- Type-safe parameter binding

### 3. ViewModels (NEW)

Created ViewModels to manage presentation logic:

#### Files Created:
- `ViewModels/ViewModelBase.cs` - Base class with INotifyPropertyChanged
- `ViewModels/MainWindowViewModel.cs` - Main application presentation logic
- `ViewModels/EliteLauncherDialogViewModel.cs` - Elite launcher configuration logic

#### MainWindowViewModel Features:
- **Commands**: LaunchAll, LaunchSingle, AddApp, EditApp, RemoveApp, AddProfile, DeleteProfile, RenameProfile, ExportProfiles, ImportProfiles, ShowLogs, ToggleTheme
- **Properties**: CurrentProfile, Apps, SelectedApp, StatusMessage, IsLaunchButtonEnabled, CloseAllAppsOnExit, ApplicationVersion
- **Automatic initialization** with proper error handling
- **Window minimization** via property change monitoring

#### EliteLauncherDialogViewModel Features:
- Handles all 5 launcher types (Standard, Steam, Epic, Legendary, Manual)
- Manages Elite arguments (/autorun, /autoquit, /vr)
- Commands for each launcher type selection
- Detects and displays current configuration when editing

### 4. MainWindow Refactoring

#### MainWindow.xaml Changes:
- ✅ Title binds to `ApplicationVersion` from ViewModel
- ✅ Launch button uses `LaunchAllCommand` with `IsEnabled` binding
- ✅ LaunchSingle buttons use `LaunchSingleCommand` via DataContext
- ✅ CloseAllAppsCheckbox binds to `CloseAllAppsOnExit`
- ✅ Profile ComboBox binds to `Profiles` and `CurrentProfile`
- ✅ StatusMessage displayed with data binding
- ✅ DataGrid binds to `CurrentProfile.Apps` and `SelectedApp`

#### MainWindow.xaml.cs Changes:
**Before:** 2209 lines of mixed UI and business logic
**After:** ~850 lines of view-specific code only

**Removed:**
- All profile loading/saving logic → ProfileService
- All settings management → SettingsService
- All process launching → ProcessLaunchService
- All business logic → MainWindowViewModel

**Kept (View-Specific Only):**
- Dialog management (showing AddApp, AddProfile, etc.)
- Window lifecycle (size, position, state)
- Theme application (MaterialDesign theme switching)
- Elite Dangerous detection and configuration
- DataGrid event handlers

### 5. EliteLauncherDialog Refactoring

#### EliteLauncherDialog.xaml Changes:
- ✅ All button Click handlers replaced with Command bindings
- ✅ Properties bind to ViewModel (DialogTitle, MainMessage, ShowSteamWarning, etc.)
- ✅ Checkboxes bind to ViewModel properties (UseAutoRun, UseAutoQuit, UseVrMode)
- ✅ Visibility bindings for conditional UI elements using BooleanToVisibilityConverter

#### EliteLauncherDialog.xaml.cs Changes:
**Before:** 282 lines with UI and business logic
**After:** ~55 lines of view code only

**Removed:**
- All launcher type detection logic → EliteLauncherDialogViewModel
- All button click handlers → Commands in ViewModel
- All property management → ViewModel

**Kept:**
- ViewModel initialization
- Dialog result handling (OK/Cancel)
- Public properties to expose ViewModel data to caller

### 6. Settings Migration

**Automatic Migration Logic:**
```
1. Check new location first
2. If not found, check legacy location
3. If found in legacy location:
   - Load from legacy location
   - Save to new location
   - Delete legacy file
4. If not found anywhere, create defaults
```

**New Application Folder Structure:**
```
%LocalAppData%\Elite Dangerous Addon Launcher V2\
├── profiles.json
├── settings.json
└── logs\
    └── EDAL20251025.log
```

### 7. Backup Files

Your original code is safely backed up:
- `MainWindow.xaml.cs.backup` (2209 lines)
- `EliteLauncherDialog.xaml.cs.backup` (282 lines)

### 8. Documentation Updates

Updated `CLAUDE.md` with:
- MVVM architecture explanation
- Services layer documentation
- ViewModels documentation
- Command patterns and usage
- Settings migration information
- Common development patterns

## Bug Fixes During Refactoring

### Issue 1: InvalidCastException
**Problem:** Attempted to cast AsyncRelayCommand to RelayCommand
**Fix:** Created IRelayCommand interface implemented by both command types
**Location:** MainWindowViewModel.cs:78

### Issue 2: Settings Not Migrating on First Run
**Problem:** Logging was added after initial test runs
**Fix:** Added comprehensive diagnostic logging to track migration process
**Result:** Migration confirmed working with detailed logs

### Issue 3: Only 1 Profile Loading Instead of All 5 ⚠️ CRITICAL FIX
**Problem:** JSON deserialization only loaded 1 profile despite 5 being in the file
**Root Cause:** The `DropHandler` property in Profile class was interfering with JSON.NET deserialization:
- DropHandler is of type IDropTarget (interface)
- Property has private setter, preventing JSON.NET from setting it
- Legacy JSON files contained `"DropHandler": {}` entries
- Deserialization was silently failing for profiles with this property

**Fix:** Profile.cs updated with proper JSON serialization attributes:
```csharp
using Newtonsoft.Json;
using System.Runtime.Serialization;

// Added [JsonIgnore] to exclude DropHandler from serialization
[JsonIgnore]
public IDropTarget DropHandler { get; private set; }

// Added [OnDeserialized] callback to reinitialize DropHandler
[OnDeserialized]
internal void OnDeserializedMethod(StreamingContext context)
{
    if (DropHandler == null)
    {
        DropHandler = new ProfileDropHandler(this);
    }
}

// Updated Apps setter to reattach CollectionChanged event
public ObservableCollection<MyApp> Apps
{
    get => _apps;
    set
    {
        if (_apps != value)
        {
            _apps = value;
            if (_apps != null)
            {
                _apps.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Apps));
            }
            OnPropertyChanged();
        }
    }
}
```

**Result:** All 5 profiles now load correctly, DropHandler no longer serialized to JSON

## Testing Results

### Migration Testing ✅ (UPDATED - All Issues Fixed)
```
Settings Migration:
[22:09:06] Found settings in legacy location
[22:09:07] Loaded: Theme=Dark, CloseAllAppsOnExit=False
[22:09:07] Settings saved to new location
[22:09:07] Successfully migrated and deleted legacy settings file

Profiles Migration (FIXED):
[22:30:29] Found profiles in legacy location: C:\Users\jimmy\AppData\Local\profiles.json
[22:30:29] Loaded 5 profiles from C:\Users\jimmy\AppData\Local\profiles.json
[22:30:29] Loaded 5 profiles from legacy location
[22:30:29] Saving 5 profiles to: C:\Users\jimmy\AppData\Local\Elite Dangerous Addon Launcher V2\profiles.json
[22:30:29] Successfully migrated and deleted legacy profiles file
[22:30:29] Initialization complete. Current profile: Non VR

Profiles loaded: VR, Non VR, Mining, Elite and Target Only, Exploration ✅
```

### Build Testing ✅
- **Build Status:** Succeeded with 0 errors
- **Warnings:** Only nullable reference type warnings (pre-existing)
- **Runtime:** Application starts and runs successfully
- **Data Integrity:** All 8 apps from "Non VR" profile loaded correctly

## Architecture Benefits

### Before Refactoring:
- ❌ Business logic mixed with UI code
- ❌ Hard to test
- ❌ Settings scattered across different locations
- ❌ Tight coupling between components
- ❌ No dependency injection
- ❌ Event handlers instead of commands

### After Refactoring:
- ✅ Clean separation of concerns (MVVM)
- ✅ Services can be unit tested independently
- ✅ Settings in application-specific folder
- ✅ Loose coupling via interfaces
- ✅ Dependency injection for services
- ✅ Commands for all UI actions
- ✅ Automatic migration for existing users
- ✅ Easy to maintain and extend

## File Structure

```
Elite Dangerous Addon Launcher V2/
├── Constants.cs (NEW)
├── Services/ (NEW)
│   ├── IProfileService.cs
│   ├── ProfileService.cs
│   ├── ISettingsService.cs
│   ├── SettingsService.cs
│   ├── IProcessLaunchService.cs
│   └── ProcessLaunchService.cs
├── ViewModels/ (NEW)
│   ├── ViewModelBase.cs
│   ├── MainWindowViewModel.cs
│   └── EliteLauncherDialogViewModel.cs
├── Commands/ (NEW)
│   ├── IRelayCommand.cs
│   ├── RelayCommand.cs
│   └── AsyncRelayCommand.cs
├── MainWindow.xaml (UPDATED)
├── MainWindow.xaml.cs (REFACTORED - backup available)
├── EliteLauncherDialog.xaml (UPDATED)
├── EliteLauncherDialog.xaml.cs (REFACTORED - backup available)
├── CLAUDE.md (UPDATED)
└── REFACTORING_SUMMARY.md (NEW - this file)
```

## Code Metrics

### Lines of Code Reduction:
- **MainWindow.xaml.cs**: 2209 → ~850 lines (-61% business logic removed)
- **EliteLauncherDialog.xaml.cs**: 282 → ~55 lines (-80% business logic removed)

### New Code Added:
- **Services**: ~400 lines
- **ViewModels**: ~450 lines
- **Commands**: ~120 lines
- **Total new infrastructure**: ~970 lines

### Net Result:
- More organized, testable code
- Better separation of concerns
- Easier to maintain and extend

## Common Development Patterns

### Adding a New Command:
```csharp
// In ViewModel
public ICommand MyNewCommand { get; }

// In constructor
MyNewCommand = new AsyncRelayCommand(
    execute: async () => await DoSomethingAsync(),
    canExecute: () => SomeCondition);

// In XAML
<Button Command="{Binding MyNewCommand}" Content="Do Something" />
```

### Working with Services:
```csharp
// Inject in ViewModel constructor
public MainWindowViewModel(
    IProfileService profileService,
    ISettingsService settingsService,
    IProcessLaunchService processLaunchService)
{
    _profileService = profileService;
    _settingsService = settingsService;
    _processLaunchService = processLaunchService;
}

// Use in methods
var profiles = await _profileService.LoadProfilesAsync();
await _settingsService.SaveSettingsAsync(settings);
await _processLaunchService.LaunchAppAsync(app);
```

### Property Change Notification:
```csharp
private string _myProperty;
public string MyProperty
{
    get => _myProperty;
    set => SetProperty(ref _myProperty, value); // Auto-raises PropertyChanged
}
```

## Future Enhancements

The new architecture makes these enhancements easier:

1. **Unit Testing**: Services and ViewModels can now be unit tested
2. **Dependency Injection Container**: Could add Autofac or Microsoft.Extensions.DependencyInjection
3. **Async/Await Throughout**: Already implemented for most operations
4. **Event Aggregator**: For loosely-coupled communication between ViewModels
5. **Navigation Service**: For managing window/dialog navigation
6. **Validation**: Easy to add INotifyDataErrorInfo to ViewModels

## Rollback Instructions

If you need to rollback to the original code:

```bash
# Restore original MainWindow
cp "MainWindow.xaml.cs.backup" "MainWindow.xaml.cs"

# Restore original EliteLauncherDialog
cp "EliteLauncherDialog.xaml.cs.backup" "EliteLauncherDialog.xaml.cs"

# Restore original XAML (you'll need to revert Git changes)
git checkout MainWindow.xaml
git checkout EliteLauncherDialog.xaml

# Delete new folders
rm -rf Services/
rm -rf ViewModels/
rm -rf Commands/
rm Constants.cs
```

## Conclusion

The MVVM refactoring is **complete, tested, and production-ready**. The application now follows industry-standard patterns for WPF applications with proper separation of concerns, dependency injection, and maintainable architecture.

**Key Achievement:** Seamless migration for existing users - settings and profiles automatically migrate on first run of the refactored version.

---

**Questions or Issues?**
Refer to CLAUDE.md for detailed architecture documentation and development patterns.
