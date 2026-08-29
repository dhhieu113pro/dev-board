using System;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public sealed class DevSpaceGridSlot
    {
        public int Index { get; }

        public DevSpaceSession Session { get; }

        public DevSpaceGridSlot(int index, DevSpaceSession session)
        {
            Index = index;
            Session = session;
        }
    }

    public sealed class DevSpaces : ObservableObject, IDisposable
    {
        public SourceGit.DevSpaces.IDevSpaceSessionLauncher Launcher { get; }

        public AvaloniaList<DevSpaceSession> Sessions { get; } = [];

        public AvaloniaList<DevSpaceGridSlot> VisibleSlots { get; } = [];

        public DevSpaceSession ActiveSession
        {
            get => _activeSession;
            private set => SetProperty(ref _activeSession, value);
        }

        public Models.DevSpaceLayout Layout
        {
            get => _layout;
            set
            {
                if (value == Models.DevSpaceLayout.FourByFour)
                    value = Models.DevSpaceLayout.ThreeByThree;

                if (SetProperty(ref _layout, value))
                {
                    OnPropertyChanged(nameof(LayoutIndex));
                    RebuildSlots();
                }
            }
        }

        public int LayoutIndex
        {
            get => (int)_layout;
            set
            {
                if (value >= 0 && value <= 3)
                    Layout = (Models.DevSpaceLayout)value;
            }
        }

        public int GridRows => Models.DevSpaceLayoutExtensions.GetRows(_layout, Sessions.Count);

        public int GridColumns => Models.DevSpaceLayoutExtensions.GetColumns(_layout, Sessions.Count);

        public int GridCapacity => GridRows * GridColumns;

        public DevSpaces(
            string workingDirectory,
            SourceGit.DevSpaces.IDevSpaceSessionLauncher launcher = null)
        {
            _workingDirectory = workingDirectory;
            Launcher = launcher ?? new SourceGit.DevSpaces.LocalDevSpaceSessionLauncher();

            var savedLayout = Preferences.Instance.DevSpacesDefaultLayout;
            if (savedLayout == Models.DevSpaceLayout.FourByFour)
            {
                savedLayout = Models.DevSpaceLayout.ThreeByThree;
                Preferences.Instance.DevSpacesDefaultLayout = savedLayout;
            }

            _layout = savedLayout;
            RebuildSlots();
        }

        public void EnsureFirstSession()
        {
            if (Sessions.Count == 0)
                CreateTerminal();
        }

        public DevSpaceTerminal CreateTerminal()
        {
            return CreateTerminalAt(-1);
        }

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot)
        {
            var command = Preferences.Instance.DevSpacesDefaultCommand;
            return CreateTerminalAt(preferredSlot, command, GetTerminalDisplayName(command));
        }

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot, string command, string displayName)
        {
            if (string.IsNullOrWhiteSpace(command))
                command = Preferences.Instance.DevSpacesDefaultCommand;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = GetTerminalDisplayName(command);

            var number = _nextSessionNumber++;
            var terminal = new DevSpaceTerminal($"{displayName} {number}", command, _workingDirectory);
            AddSession(terminal, preferredSlot);
            return terminal;
        }

        public DevSpaceRoslyn CreateRoslynAt(int preferredSlot)
        {
            var roslyn = new DevSpaceRoslyn(_workingDirectory);
            AddSession(roslyn, preferredSlot);
            return roslyn;
        }

        public void ActivateSession(DevSpaceSession session)
        {
            if (session == null || !Sessions.Contains(session))
                return;

            ActiveSession = session;
            RebuildSlots();
        }

        public void CloseSession(DevSpaceSession session)
        {
            if (session == null || !Sessions.Remove(session))
                return;

            session.Dispose();
            if (ActiveSession == session)
                ActiveSession = Sessions.Count > 0 ? Sessions[Sessions.Count - 1] : null;

            RebuildSlots();
        }

        public void ActivateTerminal(DevSpaceTerminal terminal)
        {
            ActivateSession(terminal);
        }

        public void CloseTerminal(DevSpaceTerminal terminal)
        {
            CloseSession(terminal);
        }

        public void StopAll()
        {
            for (var i = Sessions.Count - 1; i >= 0; i--)
                Sessions[i].Dispose();

            Sessions.Clear();
            VisibleSlots.Clear();
            ActiveSession = null;
            _preferredSlot = -1;
            OnPropertyChanged(nameof(GridRows));
            OnPropertyChanged(nameof(GridColumns));
            OnPropertyChanged(nameof(GridCapacity));
            OnPropertyChanged(nameof(VisibleSlots));
        }

        public void Dispose()
        {
            StopAll();
        }

        private void AddSession(DevSpaceSession session, int preferredSlot)
        {
            Sessions.Add(session);
            ActiveSession = session;
            _preferredSlot = preferredSlot;
            RebuildSlots();
        }

        private static string GetTerminalDisplayName(string command)
        {
            var normalized = command?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "copilot" => "Copilot",
                "pwsh" or "__devspaces_pwsh__" => "PowerShell 7",
                "powershell" or "powershell.exe" or "__devspaces_powershell__" => "Windows PowerShell",
                "cmd" or "cmd.exe" or "__devspaces_cmd__" => "Command Prompt",
                "__devspaces_git_bash__" => "Git Bash",
                "__devspaces_shell__" => "Shell",
                _ => "Terminal",
            };
        }

        private void RebuildSlots()
        {
            var capacity = GridCapacity;
            var slots = new DevSpaceSession[capacity];

            if (capacity == 1)
            {
                slots[0] = ActiveSession ?? (Sessions.Count > 0 ? Sessions[0] : null);
            }
            else
            {
                var placeActiveInPreferredSlot =
                    ActiveSession != null &&
                    _preferredSlot >= 0 &&
                    _preferredSlot < capacity &&
                    Sessions.Contains(ActiveSession);

                if (placeActiveInPreferredSlot)
                    slots[_preferredSlot] = ActiveSession;

                var slotIndex = 0;
                foreach (var session in Sessions)
                {
                    if (placeActiveInPreferredSlot && session == ActiveSession)
                        continue;

                    while (slotIndex < capacity && slots[slotIndex] != null)
                        slotIndex++;

                    if (slotIndex >= capacity)
                        break;

                    slots[slotIndex] = session;
                    slotIndex++;
                }

                if (ActiveSession != null && Array.IndexOf(slots, ActiveSession) < 0)
                    slots[capacity - 1] = ActiveSession;
            }

            VisibleSlots.Clear();
            for (var i = 0; i < capacity; i++)
                VisibleSlots.Add(new DevSpaceGridSlot(i, slots[i]));

            _preferredSlot = -1;
            OnPropertyChanged(nameof(GridRows));
            OnPropertyChanged(nameof(GridColumns));
            OnPropertyChanged(nameof(GridCapacity));
            OnPropertyChanged(nameof(VisibleSlots));
        }

        private readonly string _workingDirectory;
        private DevSpaceSession _activeSession;
        private Models.DevSpaceLayout _layout;
        private int _nextSessionNumber = 1;
        private int _preferredSlot = -1;
    }
}
