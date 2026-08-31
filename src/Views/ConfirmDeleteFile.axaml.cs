using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DevBoard.Views
{
    public partial class ConfirmDeleteFile : ChromelessWindow
    {
        public ConfirmDeleteFile()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        public Task<bool> ShowAsync(Window owner, string path)
        {
            FilePath.Text = path;
            return ShowDialog<bool>(owner);
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            Close(true);
        }
    }
}
