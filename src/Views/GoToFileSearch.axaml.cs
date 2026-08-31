using Avalonia.Controls;
using Avalonia.Threading;

namespace DevBoard.Views
{
    public partial class GoToFileSearch : UserControl
    {
        public GoToFileSearch()
        {
            InitializeComponent();
            DataContextChanged += (_, _) =>
            {
                if (DataContext != null)
                    Dispatcher.UIThread.Post(() => SearchBox.Focus());
            };
        }
    }
}
