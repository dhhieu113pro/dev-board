using Avalonia.Controls;
using Avalonia.Threading;

namespace DevBoard.Views
{
    public partial class GoToFileSearch : UserControl
    {
        public GoToFileSearch()
        {
            InitializeComponent();
            Loaded += (_, _) => Dispatcher.UIThread.Post(() => SearchBox.Focus());
        }
    }
}
