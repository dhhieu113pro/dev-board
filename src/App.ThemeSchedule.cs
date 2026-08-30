using Avalonia.Threading;

namespace DevBoard
{
    public partial class App
    {
        public App()
        {
            Dispatcher.UIThread.Post(ThemeScheduleController.Start, DispatcherPriority.Loaded);
        }
    }
}
