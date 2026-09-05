using System.ComponentModel;
using System.Runtime.CompilerServices;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DevBoard.DevSpaces
{
    internal static class DevSpaceRepositorySwitchBootstrap
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            Control.LoadedEvent.AddClassHandler<Views.Launcher>(OnLauncherLoaded);
            Control.UnloadedEvent.AddClassHandler<Views.Launcher>(OnLauncherUnloaded);
        }

        private static void OnLauncherLoaded(Views.Launcher view, RoutedEventArgs e)
        {
            if (_launchers.TryGetValue(view, out _))
                return;

            if (view.DataContext is not ViewModels.Launcher launcher)
                return;

            launcher.PropertyChanging += OnLauncherPropertyChanging;
            _launchers.Add(view, launcher);
        }

        private static void OnLauncherUnloaded(Views.Launcher view, RoutedEventArgs e)
        {
            if (!_launchers.TryGetValue(view, out var launcher))
                return;

            launcher.PropertyChanging -= OnLauncherPropertyChanging;
            _launchers.Remove(view);
        }

        private static void OnLauncherPropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            if (e.PropertyName != nameof(ViewModels.Launcher.ActivePage))
                return;

            if (sender is ViewModels.Launcher { ActivePage.Data: ViewModels.Repository repository })
                DevSpaceRegistry.PrepareForRepositorySwitch(repository);
        }

        private static readonly ConditionalWeakTable<Views.Launcher, ViewModels.Launcher> _launchers = new();
    }
}
