using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.System;
using System.Diagnostics;

namespace Project_FluentAI.Services
{
    public class WindowsAgentService
    {
        // Windows Command Registry: Maps logical names to known executables or URIs
        private readonly Dictionary<string, string> _commandRegistry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
                // Settings
                { "settings", "ms-settings:" },
    { "windows settings", "ms-settings:" },

    // File Explorer
    { "explorer", "explorer.exe" },
    { "file explorer", "explorer.exe" },
    { "windows explorer", "explorer.exe" },

    // Calculator
    { "calculator", "calc.exe" },
    { "calc", "calc.exe" },

    // Notepad
    { "notepad", "notepad.exe" },

    // Paint
    { "paint", "mspaint.exe" },
    { "mspaint", "mspaint.exe" },

    // Command Line
    { "cmd", "cmd.exe" },
    { "command prompt", "cmd.exe" },
    { "terminal", "wt.exe" },
    { "windows terminal", "wt.exe" },
    { "powershell", "powershell.exe" },

    // Task Manager
    { "task manager", "taskmgr.exe" },
    { "taskmgr", "taskmgr.exe" },

    // Control Panel
    { "control panel", "control.exe" },
    { "control", "control.exe" },

    // Device Manager
    { "device manager", "devmgmt.msc" },
    { "devmgmt", "devmgmt.msc" },

    // Registry
    { "registry editor", "regedit.exe" },
    { "registry", "regedit.exe" },
    { "regedit", "regedit.exe" },

    // Services
    { "services", "services.msc" },

    // Disk Management
    { "disk management", "diskmgmt.msc" },

    // Computer Management
    { "computer management", "compmgmt.msc" },

    // Event Viewer
    { "event viewer", "eventvwr.msc" },

    // Local Group Policy
    { "group policy", "gpedit.msc" },
    { "gpedit", "gpedit.msc" },

    // Local Security Policy
    { "security policy", "secpol.msc" },
    { "secpol", "secpol.msc" },

    // Resource Monitor
    { "resource monitor", "resmon.exe" },

    // Performance Monitor
    { "performance monitor", "perfmon.msc" },

    // System Configuration
    { "system configuration", "msconfig.exe" },
    { "msconfig", "msconfig.exe" },

    // System Information
    { "system information", "msinfo32.exe" },
    { "msinfo", "msinfo32.exe" },

    // Character Map
    { "character map", "charmap.exe" },

    // Snipping Tool
    { "snipping tool", "snippingtool.exe" },

    // On Screen Keyboard
    { "on screen keyboard", "osk.exe" },
    { "osk", "osk.exe" },

    // Magnifier
    { "magnifier", "magnify.exe" },

    // Narrator
    { "narrator", "narrator.exe" },

    // Sticky Notes
    { "sticky notes", "ms-stickynotes:" },

    // Clock
    { "clock", "ms-clock:" },
    { "alarms", "ms-clock:" },

    // Photos
    { "photos", "ms-photos:" },

    // Camera
    { "camera", "microsoft.windows.camera:" },

    // Maps
    { "maps", "bingmaps:" },

    // Microsoft Store
    { "store", "ms-windows-store:" },
    { "microsoft store", "ms-windows-store:" },

    // Edge
    { "edge", "Microsoft-edge:" },
    { "microsoft edge", "Microsoft-edge:" },
    { "browser", "Microsoft-edge:" },

    // Default Apps
    { "mail", "outlook.exe" },
    { "outlook", "outlook.exe" },

    // Media
    { "media player", "wmplayer.exe" },

    // Remote Desktop
    { "remote desktop", "mstsc.exe" },

    // Run dialog
    { "run", "explorer.exe shell:::{2559a1f3-21d7-11d4-bdaf-00c04f60b9f0}" },

    // Bluetooth
    { "bluetooth", "ms-settings:bluetooth" },

    // Wi-Fi
    { "wifi", "ms-settings:network-wifi" },
    { "wi-fi", "ms-settings:network-wifi" },

    // Network
    { "network", "ms-settings:network" },

    // Display
    { "display", "ms-settings:display" },

    // Personalization
    { "personalization", "ms-settings:personalization" },

    // Apps
    { "apps", "ms-settings:appsfeatures" },

    // Windows Update
    { "windows update", "ms-settings:windowsupdate" },
    { "update", "ms-settings:windowsupdate" },

    // Sound
    { "sound", "ms-settings:sound" },

    // Privacy
    { "privacy", "ms-settings:privacy" },

    // Time & Language
    { "time", "ms-settings:dateandtime" },
    { "language", "ms-settings:regionlanguage" },

    // Default Browser Settings
    { "default apps", "ms-settings:defaultapps" }
        };

        public Dictionary<string, string> CommandRegistry => _commandRegistry;

        public async Task<bool> TryExecuteFromResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return false;

            Debug.WriteLine($"[WindowsAgent] Processing response: \"{response}\"");

            // 1. Detect logical application names from the registry
            foreach (var entry in _commandRegistry)
            {
                // Matches "open settings", "launch notepad", etc., or just the command itself if it's explicit
                string pattern = $@"\b(open|launch|start|run)?\s*{Regex.Escape(entry.Key)}\b";
                if (Regex.IsMatch(response, pattern, RegexOptions.IgnoreCase))
                {
                    Debug.WriteLine($"[WindowsAgent] MATCHED Registry Entry: {entry.Key} -> {entry.Value}");
                    return await ExecuteCommand(entry.Value);
                }
            }

            // 2. Detect explicit URIs or EXEs that might not be in the registry but are supported
            string explicitPattern = @"\b(ms-[a-z-]+:|[\w-]+\.exe|[\w-]+\.msc)\b";
            var match = Regex.Match(response, explicitPattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string explicitCommand = match.Value;
                Debug.WriteLine($"[WindowsAgent] MATCHED Explicit Command: {explicitCommand}");
                return await ExecuteCommand(explicitCommand);
            }

            Debug.WriteLine("[WindowsAgent] No commands detected in response.");
            return false;
        }

        private async Task<bool> ExecuteCommand(string command)
        {
            try
            {
                Debug.WriteLine($"[WindowsAgent] ATTEMPTING TO EXECUTE: {command}");

                // In UWP, we should prioritize Launcher.LaunchUriAsync for URIs
                if (command.EndsWith(":") || command.StartsWith("http"))
                {
                    Debug.WriteLine($"[WindowsAgent] Launching via URI: {command}");
                    bool success = await Launcher.LaunchUriAsync(new Uri(command));
                    Debug.WriteLine($"[WindowsAgent] URI Launch Result: {success}");
                    return success;
                }

                // For EXEs and MSCs, we use Process.Start with UseShellExecute = true
                // Note: UWP apps have significant restrictions on Process.Start.
                // In a standard UWP sandbox, this may fail unless the app has specific capabilities.
                // We provide the implementation as requested.
                
                Debug.WriteLine($"[WindowsAgent] Launching via Process.Start: {command}");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                using (var process = Process.Start(startInfo))
                {
                    bool success = process != null;
                    Debug.WriteLine($"[WindowsAgent] Process.Start Result: {success}");
                    return success;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsAgent] EXECUTION ERROR for '{command}': {ex.Message}");
                
                // Fallback: If Process.Start fails (common in UWP), try mapping to a URI if possible
                return await TryUriFallback(command);
            }
        }

        private async Task<bool> TryUriFallback(string command)
        {
            string uri = null;
            if (command.Equals("notepad.exe", StringComparison.OrdinalIgnoreCase)) uri = "ms-editor:";
            else if (command.Equals("calc.exe", StringComparison.OrdinalIgnoreCase)) uri = "ms-calculator:";
            else if (command.Equals("mspaint.exe", StringComparison.OrdinalIgnoreCase)) uri = "ms-paint:";
            else if (command.Equals("control.exe", StringComparison.OrdinalIgnoreCase)) uri = "ms-settings:";
            else if (command.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)) uri = "file:";

            if (uri != null)
            {
                Debug.WriteLine($"[WindowsAgent] Retrying via URI Fallback: {uri}");
                return await Launcher.LaunchUriAsync(new Uri(uri));
            }

            return false;
        }
    }
}
