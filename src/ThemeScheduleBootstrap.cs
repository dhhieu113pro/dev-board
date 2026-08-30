using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;

namespace DevBoard
{
    internal static class ThemeScheduleBootstrap
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            _ = WaitForApplicationAsync();
        }

        private static async Task WaitForApplicationAsync()
        {
            for (var attempt = 0; attempt < 120; attempt++)
            {
                if (Application.Current is App)
                {
                    Dispatcher.UIThread.Post(ThemeScheduleController.Start);
                    return;
                }

                await Task.Delay(250);
            }
        }
    }
}
