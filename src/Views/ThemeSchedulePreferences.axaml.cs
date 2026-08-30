using System;
using Avalonia.Controls;

namespace DevBoard.Views
{
    public partial class ThemeSchedulePreferences : UserControl
    {
        private bool _loading;

        public ThemeSchedulePreferences()
        {
            InitializeComponent();

            ModePicker.SelectionChanged += (_, _) => OnModeChanged();
            LightTimePicker.SelectedTimeChanged += (_, _) => OnCustomTimeChanged();
            DarkTimePicker.SelectedTimeChanged += (_, _) => OnCustomTimeChanged();
            LatitudeInput.ValueChanged += (_, _) => OnCoordinatesChanged();
            LongitudeInput.ValueChanged += (_, _) => OnCoordinatesChanged();
            DetectLocationButton.Click += async (_, _) => await DetectLocationAsync();

            Reload();
        }

        private void Reload()
        {
            _loading = true;
            var settings = ThemeScheduleController.Settings;
            ModePicker.SelectedIndex = settings.Mode switch
            {
                "Light" => 1,
                "Dark" => 2,
                "Sunset" => 3,
                "Custom" => 4,
                _ => 0,
            };
            LightTimePicker.SelectedTime = settings.LightStart;
            DarkTimePicker.SelectedTime = settings.DarkStart;
            LatitudeInput.Value = settings.Latitude is double latitude ? (decimal)latitude : null;
            LongitudeInput.Value = settings.Longitude is double longitude ? (decimal)longitude : null;
            UpdateVisibility(settings.Mode);
            _loading = false;
        }

        private void OnModeChanged()
        {
            if (_loading)
                return;

            var mode = ModePicker.SelectedIndex switch
            {
                1 => "Light",
                2 => "Dark",
                3 => "Sunset",
                4 => "Custom",
                _ => "System",
            };

            ThemeScheduleController.Update(settings => settings.Mode = mode);
            UpdateVisibility(mode);
        }

        private void OnCustomTimeChanged()
        {
            if (_loading || LightTimePicker.SelectedTime is not TimeSpan light || DarkTimePicker.SelectedTime is not TimeSpan dark)
                return;

            ThemeScheduleController.Update(settings =>
            {
                settings.LightStart = light;
                settings.DarkStart = dark;
            });
        }

        private void OnCoordinatesChanged()
        {
            if (_loading)
                return;

            ThemeScheduleController.Update(settings =>
            {
                settings.Latitude = LatitudeInput.Value is decimal latitude ? (double)latitude : null;
                settings.Longitude = LongitudeInput.Value is decimal longitude ? (double)longitude : null;
            });
        }

        private async System.Threading.Tasks.Task DetectLocationAsync()
        {
            DetectLocationButton.IsEnabled = false;
            LocationStatus.Text = "Detecting approximate IP location…";

            var success = await ThemeScheduleController.DetectLocationAsync();
            if (success)
            {
                var settings = ThemeScheduleController.Settings;
                _loading = true;
                LatitudeInput.Value = settings.Latitude is double latitude ? (decimal)latitude : null;
                LongitudeInput.Value = settings.Longitude is double longitude ? (decimal)longitude : null;
                _loading = false;
                LocationStatus.Text = "Approximate IP location detected. Sunrise and sunset are calculated locally from the saved coordinates.";
            }
            else
            {
                LocationStatus.Text = "Approximate location detection failed. Enter coordinates manually, or DevBoard will follow the system theme.";
            }

            DetectLocationButton.IsEnabled = true;
        }

        private void UpdateVisibility(string mode)
        {
            CustomPanel.IsVisible = mode == "Custom";
            SunsetPanel.IsVisible = mode == "Sunset";
        }
    }
}
