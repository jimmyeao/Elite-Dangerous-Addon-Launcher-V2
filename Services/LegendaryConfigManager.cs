// LegendaryConfigManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Elite_Dangerous_Addon_Launcher_V2.Services
{
    public static class LegendaryConfigManager
    {
        private const string AliasSection = "[Legendary.aliases]";
        private const string GameSection = "[9c203b6ed35846e8a4a9ff1e314f6593]";
        private const string GameAlias = "elite";
        private const string GameId = "9c203b6ed35846e8a4a9ff1e314f6593";
        private const string DefaultParams = "/edh /autorun /autoquit";

        public static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "legendary",
            "config.ini");

        public static string CurrentParams { get; private set; } = DefaultParams;

        public static void EnsureLegendaryConfig()
        {
            var configDir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            List<string> lines = File.Exists(ConfigPath)
                ? File.ReadAllLines(ConfigPath).ToList()
                : new List<string>();

            bool aliasSectionExists = lines.Any(l => l.Trim().Equals(AliasSection, StringComparison.OrdinalIgnoreCase));
            bool aliasExists = lines.Any(l => l.Trim().Equals($"{GameAlias} = {GameId}", StringComparison.OrdinalIgnoreCase));
            bool gameSectionExists = lines.Any(l => l.Trim().Equals(GameSection, StringComparison.OrdinalIgnoreCase));
            bool hasStartParams = lines.Any(l => l.Trim().StartsWith("start_params"));

            if (!aliasSectionExists)
            {
                lines.Add("");
                lines.Add(AliasSection);
            }

            if (!aliasExists)
            {
                int aliasIndex = lines.FindIndex(l => l.Trim().Equals(AliasSection));
                lines.Insert(aliasIndex + 1, $"{GameAlias} = {GameId}");
            }

            if (!gameSectionExists)
            {
                lines.Add("");
                lines.Add(GameSection);
            }

            if (!hasStartParams)
            {
                int sectionIndex = lines.FindIndex(l => l.Trim().Equals(GameSection));
                lines.Insert(sectionIndex + 1, $"start_params = {DefaultParams}");
                CurrentParams = DefaultParams;
            }
            else
            {
                var paramLine = lines.FirstOrDefault(l => l.Trim().StartsWith("start_params"));
                if (paramLine != null)
                    CurrentParams = paramLine.Split('=')[1].Trim();
            }

            File.WriteAllLines(ConfigPath, lines);
        }
        public static string CurrentStartParams
        {
            get
            {
                if (!File.Exists(ConfigPath)) return DefaultParams;
                var lines = File.ReadAllLines(ConfigPath).ToList();
                int sectionIndex = lines.FindIndex(l => l.Trim().Equals(GameSection));
                if (sectionIndex < 0) return DefaultParams;

                int paramLineIndex = lines.FindIndex(sectionIndex + 1, l => l.Trim().StartsWith("start_params"));
                if (paramLineIndex >= 0)
                {
                    var line = lines[paramLineIndex];
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                        return parts[1].Trim();
                }
                return DefaultParams;
            }
        }

        public static void UpdateStartParams(string newParams)
        {
            if (!File.Exists(ConfigPath)) return;

            var lines = File.ReadAllLines(ConfigPath).ToList();
            int sectionIndex = lines.FindIndex(l => l.Trim().Equals(GameSection));
            if (sectionIndex < 0) return;

            int paramLineIndex = lines.FindIndex(sectionIndex + 1, l => l.Trim().StartsWith("start_params"));
            if (paramLineIndex >= 0)
            {
                lines[paramLineIndex] = $"start_params = {newParams}";
            }
            else
            {
                lines.Insert(sectionIndex + 1, $"start_params = {newParams}");
            }

            CurrentParams = newParams;
            File.WriteAllLines(ConfigPath, lines);
        }
    }
}
