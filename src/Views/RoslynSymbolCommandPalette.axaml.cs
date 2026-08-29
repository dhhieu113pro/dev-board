using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class RoslynSymbolCommandPalette : UserControl
    {
        public RoslynSymbolCommandPalette()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.RoslynSymbolCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                vm.Launch();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && SymbolListBox.IsKeyboardFocusWithin)
            {
                FilterTextBox.Focus(NavigationMethod.Directional);
                e.Handled = true;
            }
            else if (e.Key == Key.Down || e.Key == Key.Tab)
            {
                if (FilterTextBox.IsKeyboardFocusWithin)
                {
                    if (vm.VisibleSymbols.Count > 0)
                        SymbolListBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
                else if (SymbolListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.RoslynSymbolCommandPalette vm)
            {
                vm.Launch();
                e.Handled = true;
            }
        }
    }
}
