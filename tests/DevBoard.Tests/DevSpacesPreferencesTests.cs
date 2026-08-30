using System.Collections.Specialized;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using Xunit;

namespace DevBoard.Tests;

[Trait("Category", "UIIntegration")]
public sealed class DevSpacesPreferencesTests
{
    [AvaloniaFact]
    public void ReentrantLoadedEventAddsDevSpacesTabOnlyOnce()
    {
        var view = new Views.Preferences();
        var tabs = Assert.IsType<TabControl>(view.FindDescendantOfType<TabControl>());
        var bootstrap = typeof(Views.Preferences).Assembly.GetType("DevBoard.DevSpaces.DevSpacesBootstrap");
        var onLoaded = bootstrap?.GetMethod("OnPreferencesLoaded", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(onLoaded);

        var initialCount = tabs.Items.Count;
        var reentered = false;
        ((INotifyCollectionChanged)tabs.Items).CollectionChanged += (_, _) =>
        {
            if (reentered)
                return;

            reentered = true;
            onLoaded.Invoke(null, [view, new RoutedEventArgs(Control.LoadedEvent)]);
        };

        onLoaded.Invoke(null, [view, new RoutedEventArgs(Control.LoadedEvent)]);

        Assert.True(reentered);
        Assert.Equal(initialCount + 1, tabs.Items.Count);
    }
}
