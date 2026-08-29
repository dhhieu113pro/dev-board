using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public sealed class DevSpaceDashboard : ObservableObject, IDisposable
    {
        public string WorkspacePath { get; }

        public AvaloniaList<DevSpaceActivityEntry> Activity { get; } = [];

        public IReadOnlyList<DevSpaceDashboardSessionRow> Sessions => _owner.Sessions
            .Select(x => new DevSpaceDashboardSessionRow(x, x.Title, x.State, x.WorkingDirectory))
            .ToArray();

        public DevSpaceDashboard(DevSpaces owner, string workspacePath)
        {
            _owner = owner;
            WorkspacePath = workspacePath;
            _owner.Sessions.CollectionChanged += OnSessionsChanged;
        }

        public void AddActivity(DevSpaceActivityKind kind, string text, DateTimeOffset? at = null)
        {
            Activity.Insert(0, new DevSpaceActivityEntry(kind, text ?? string.Empty, at ?? DateTimeOffset.UtcNow));
            while (Activity.Count > 20)
                Activity.RemoveAt(Activity.Count - 1);
        }

        public void OpenSession(DevSpaceTerminal terminal)
        {
            _owner.ActivateTerminal(terminal);
        }

        public void OpenFiles()
        {
            _owner.ActivateFiles();
        }

        public DevSpaceTerminal StartDefaultTerminal()
        {
            return _owner.CreateTerminal();
        }

        public DevSpaceTerminal StartProfile(SourceGit.DevSpaces.DevSpaceTerminalProfile profile)
        {
            return _owner.CreateProfileTerminalAt(-1, profile);
        }

        public DevSpaceTerminal StartAgent(SourceGit.DevSpaces.DevSpaceAgent agent)
        {
            return _owner.CreateAgentTerminalAt(-1, agent);
        }

        public void CloseAllSessions()
        {
            _owner.StopAll();
        }

        public void Dispose()
        {
            _owner.Sessions.CollectionChanged -= OnSessionsChanged;
        }

        private void OnSessionsChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Sessions));
        }

        private readonly DevSpaces _owner;
    }
}
