using System;
using System.Globalization;
using System.IO;

namespace DevBoard.Models
{
    public sealed class ThemeScheduleSettings
    {
        public string Mode { get; set; } = "System";
        public TimeSpan LightStart { get; set; } = TimeSpan.FromHours(7);
        public TimeSpan DarkStart { get; set; } = TimeSpan.FromHours(18);
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public static ThemeScheduleSettings Load(string fallbackTheme)
        {
            var settings = new ThemeScheduleSettings
            {
                Mode = NormalizeMode(fallbackTheme),
            };

            var path = GetPath();
            if (!File.Exists(path))
                return settings;

            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var separator = line.IndexOf('=');
                    if (separator <= 0)
                        continue;

                    var key = line[..separator].Trim();
                    var value = line[(separator + 1)..].Trim();
                    switch (key)
                    {
                        case "Mode":
                            settings.Mode = NormalizeMode(value);
                            break;
                        case "LightStart":
                            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var lightStart))
                                settings.LightStart = lightStart;
                            break;
                        case "DarkStart":
                            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var darkStart))
                                settings.DarkStart = darkStart;
                            break;
                        case "Latitude":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
                                settings.Latitude = latitude;
                            break;
                        case "Longitude":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
                                settings.Longitude = longitude;
                            break;
                    }
                }
            }
            catch
            {
                // Keep defaults when the optional schedule file cannot be read.
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                var path = GetPath();
                var temp = path + ".tmp";
                var latitude = Latitude?.ToString("G17", CultureInfo.InvariantCulture) ?? string.Empty;
                var longitude = Longitude?.ToString("G17", CultureInfo.InvariantCulture) ?? string.Empty;
                var content = string.Join(Environment.NewLine,
                [
                    $"Mode={NormalizeMode(Mode)}",
                    $"LightStart={LightStart:hh\\:mm}",
                    $"DarkStart={DarkStart:hh\\:mm}",
                    $"Latitude={latitude}",
                    $"Longitude={longitude}",
                ]);

                File.WriteAllText(temp, content);
                File.Move(temp, path, true);
            }
            catch
            {
                // Scheduling remains usable for the current session even if persistence fails.
            }
        }

        public static string NormalizeMode(string mode)
        {
            if (string.Equals(mode, "Default", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase))
                return "System";
            if (string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase))
                return "Light";
            if (string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase))
                return "Dark";
            if (string.Equals(mode, "Sunset", StringComparison.OrdinalIgnoreCase))
                return "Sunset";
            if (string.Equals(mode, "Custom", StringComparison.OrdinalIgnoreCase))
                return "Custom";

            return "System";
        }

        private static string GetPath() => Path.Combine(Native.OS.DataDir, "theme-schedule.config");
    }
}
