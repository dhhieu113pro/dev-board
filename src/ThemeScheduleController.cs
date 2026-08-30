using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace DevBoard
{
    internal static class ThemeScheduleController
    {
        private static readonly HttpClient LocationClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        private static Models.ThemeScheduleSettings _settings;
        private static DispatcherTimer _timer;
        private static bool _started;
        private static string _lastResolvedTheme = string.Empty;
        private static string _lastThemeOverrides = string.Empty;

        public static Models.ThemeScheduleSettings Settings
        {
            get
            {
                EnsureSettings();
                return _settings;
            }
        }

        public static void Start()
        {
            if (_started || Application.Current is not App)
                return;

            _started = true;
            EnsureSettings();

            ViewModels.Preferences.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewModels.Preferences.ThemeOverrides))
                {
                    _lastThemeOverrides = string.Empty;
                    Apply();
                }
            };

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            };
            _timer.Tick += (_, _) =>
            {
                Apply();
                Views.ThemeSchedulePreferencesInjector.EnsureInjected();
            };
            _timer.Start();

            Apply();
            Views.ThemeSchedulePreferencesInjector.EnsureInjected();
        }

        public static void Update(Action<Models.ThemeScheduleSettings> update)
        {
            EnsureSettings();
            update(_settings);
            _settings.Mode = Models.ThemeScheduleSettings.NormalizeMode(_settings.Mode);
            _settings.Save();

            var pref = ViewModels.Preferences.Instance;
            if (_settings.Mode == "System")
                pref.Theme = "Default";
            else if (_settings.Mode is "Light" or "Dark")
                pref.Theme = _settings.Mode;

            _lastResolvedTheme = string.Empty;
            Apply();
        }

        public static async Task<bool> DetectLocationAsync()
        {
            try
            {
                using var response = await LocationClient.GetAsync("https://ipwho.is/");
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                using var document = await JsonDocument.ParseAsync(stream);
                var root = document.RootElement;

                if (root.TryGetProperty("success", out var success) && !success.GetBoolean())
                    return false;
                if (!root.TryGetProperty("latitude", out var latitudeElement) ||
                    !root.TryGetProperty("longitude", out var longitudeElement))
                    return false;

                var latitude = latitudeElement.GetDouble();
                var longitude = longitudeElement.GetDouble();
                if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
                    return false;

                Update(settings =>
                {
                    settings.Latitude = latitude;
                    settings.Longitude = longitude;
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureSettings()
        {
            _settings ??= Models.ThemeScheduleSettings.Load(ViewModels.Preferences.Instance.Theme);
        }

        private static void Apply()
        {
            if (Application.Current is not App)
                return;

            EnsureSettings();
            var pref = ViewModels.Preferences.Instance;
            var resolved = Models.ThemeSchedule.Resolve(
                _settings.Mode,
                DateTime.Now,
                _settings.LightStart,
                _settings.DarkStart,
                _settings.Latitude,
                _settings.Longitude);
            var overrides = pref.ThemeOverrides ?? string.Empty;

            if (resolved == _lastResolvedTheme && overrides == _lastThemeOverrides)
                return;

            _lastResolvedTheme = resolved;
            _lastThemeOverrides = overrides;
            App.SetTheme(resolved == "System" ? "Default" : resolved, overrides);
        }
    }
}
