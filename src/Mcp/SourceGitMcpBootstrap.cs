using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace SourceGit.Mcp
{
    internal static class SourceGitMcpBootstrap
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            Control.LoadedEvent.AddClassHandler<Views.Launcher>(OnLauncherLoaded);
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        private static void OnLauncherLoaded(Views.Launcher view, RoutedEventArgs e)
        {
            if (view.DataContext is not ViewModels.Launcher launcher)
                return;

            SourceGitMcpService.Initialize(SourceGitMcpSettings.Instance, CreateKnownRootsProvider(launcher));
        }

        internal static Func<IReadOnlyCollection<string>> CreateKnownRootsProvider(ViewModels.Launcher launcher)
        {
            ArgumentNullException.ThrowIfNull(launcher);

            return () =>
            {
                if (Dispatcher.UIThread.CheckAccess())
                    return CollectKnownRoots(launcher);

                return Dispatcher.UIThread.Invoke(() => CollectKnownRoots(launcher));
            };
        }

        private static IReadOnlyCollection<string> CollectKnownRoots(ViewModels.Launcher launcher)
        {
            var comparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var roots = new HashSet<string>(comparer);

            foreach (var page in launcher.Pages)
            {
                if (page.Data is not ViewModels.Repository repo)
                    continue;

                if (!string.IsNullOrWhiteSpace(repo.FullPath))
                    roots.Add(repo.FullPath);

                if (repo.Worktrees == null)
                    continue;

                foreach (var worktree in repo.Worktrees)
                {
                    if (!string.IsNullOrWhiteSpace(worktree.FullPath))
                        roots.Add(worktree.FullPath);
                }
            }

            return new List<string>(roots);
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            try
            {
                SourceGitMcpService.ShutdownAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // MCP is optional and must never block SourceGit process shutdown.
            }
        }
    }
}
