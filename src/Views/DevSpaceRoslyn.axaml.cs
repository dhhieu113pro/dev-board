using System;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class DevSpaceRoslyn : UserControl, IDisposable
    {
        public DevSpaceRoslyn()
        {
            InitializeComponent();
        }

        public void Dispose()
        {
            DataContext = null;
        }

        private async void OnAnalyze(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.DevSpaceRoslyn roslyn)
                await roslyn.AnalyzeAsync();

            e.Handled = true;
        }
    }
}
