using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.VisualTree;

namespace DevBoard.Views
{
    public partial class Preferences
    {
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var tabs = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
            if (tabs == null || tabs.Items.OfType<TabItem>().Any(x => x.Tag as string == DevSpacesTabTag))
                return;

            var item = new TabItem
            {
                Tag = DevSpacesTabTag,
                Header = App.Text("DevSpaces"),
                Content = new DevSpacesPreferences
                {
                    DataContext = ViewModels.Preferences.Instance,
                },
            };

            tabs.Items.Add(item);
        }

        private const string DevSpacesTabTag = "devboard.devspaces";
    }
}
