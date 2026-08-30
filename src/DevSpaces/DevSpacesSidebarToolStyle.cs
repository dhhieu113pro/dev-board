using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DevBoard.DevSpaces
{
    internal static class DevSpacesSidebarToolStyle
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            Control.LoadedEvent.AddClassHandler<Button>(OnButtonLoaded);
        }

        internal static void Apply(Button button)
        {
            button.Classes.Remove("flat");
            button.Height = 28;
            button.Margin = new Thickness(0);
            button.Padding = new Thickness(0);
            button.BorderThickness = new Thickness(0);
            button.CornerRadius = new CornerRadius(0);
            button.Background = Brushes.Transparent;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.FontWeight = FontWeight.Normal;
        }

        private static void OnButtonLoaded(Button button, RoutedEventArgs e)
        {
            if (button.Tag is not string tag || (tag != "Files" && tag != "AIRouter"))
                return;

            if (button.FindAncestorOfType<Views.Repository>() == null)
                return;

            Apply(button);
        }
    }
}
