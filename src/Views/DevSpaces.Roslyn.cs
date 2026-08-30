using Avalonia.Input;

namespace DevBoard.Views
{
    public partial class DevSpaces
    {
        private void OnRoslynTabPressed(object sender, PointerPressedEventArgs e)
        {
            _owner?.ActivateRoslyn();
            e.Handled = true;
        }
    }
}
