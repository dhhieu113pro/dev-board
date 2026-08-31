using System;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.ViewModels
{
    public sealed class DevSpaceGridSlot
    {
        public int Index { get; }
        public DevSpaceTerminal Terminal { get; }

        public DevSpaceGridSlot(int index, DevSpaceTerminal terminal)
        {
            Index = index;
            Terminal = terminal;
        }
    }

    public sealed class DevSpaces : ObservableObject, IDisposable
    {
        public DevBoard.DevSpaces.IDevSpaceSessionLauncher Launcher { get; }
        public DevSpaceFiles Files { get; }
        public DevSpaceDashboard Dashboard { get; }
        public DevSpaceRoslynAnalytics RoslynAnalytics { get; }
        public AvaloniaList<DevSpaceTerminal> Sessions { get; } = [];
        public AvaloniaList<DevSpaceGridSlot> VisibleSlots { get; } = [];
        public int TerminalCount => Sessions.Count;

        public DevSpaceTerminal CopilotSession => _copilotSession;
        public DevSpaceTerminal CodexSession => _codexSession;
        public DevSpaceTerminal AntigravitySession => _antigravitySession;

        public Models.DevSpacePage ActivePage
        {
            get => _activePage;
            private set
            {
                if (!SetProperty(ref _activePage, value))
                    return;
                OnPropertyChanged(nameof(IsDashboardActive));
                OnPropertyChanged(nameof(IsFilesActive));
                OnPropertyChanged(nameof(IsAIRouterActive));
                OnPropertyChanged(nameof(IsTerminalsActive));
                OnPropertyChanged(nameof(IsRoslynActive));
                OnPropertyChanged(nameof(IsAgentActive));
                OnPropertyChanged(nameof(ActiveAgentTerminal));
            }
        }

        public bool IsDashboardActive => ActivePage == Models.DevSpacePage.Dashboard;
        public bool IsFilesActive => ActivePage == Models.DevSpacePage.Files;
        public bool IsAIRouterActive => ActivePage == Models.DevSpacePage.AIRouter;
        public bool IsTerminalsActive => ActivePage == Models.DevSpacePage.Terminals;
        public bool IsRoslynActive => ActivePage == Models.DevSpacePage.Roslyn;
        public bool IsAgentActive => ActivePage is Models.DevSpacePage.Copilot or Models.DevSpacePage.Codex or Models.DevSpacePage.Antigravity;

        public DevSpaceTerminal ActiveAgentTerminal => ActivePage switch
        {
            Models.DevSpacePage.Copilot => _copilotSession,
            Models.DevSpacePage.Codex => _codexSession,
            Models.DevSpacePage.Antigravity => _antigravitySession,
            _ => null,
        };

        public DevSpaceTerminal ActiveTerminal
        {
            get => _activeTerminal;
            private set => SetProperty(ref _activeTerminal, value);
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
            get => Array.IndexOf(LayoutOptions, _layout);
            set
            {
                if (value >= 0 && value < LayoutOptions.Length)
                    Layout = LayoutOptions[value];
            }
        }

        public int GridRows => Models.DevSpaceLayoutExtensions.GetRows(_layout, Sessions.Count);
        public int GridColumns => Models.DevSpaceLayoutExtensions.GetColumns(_layout, Sessions.Count);
        public int GridCapacity => GridRows * GridColumns;

        public DevSpaces(
            string workingDirectory,
            DevBoard.DevSpaces.IDevSpaceSessionLauncher launcher = null,
            DevBoard.DevSpaces.Terminal.DevSpaceTerminalRegistry terminalRegistry = null)
            : this(null, workingDirectory, launcher, terminalRegistry)
        {
        }

        public DevSpaces(
            Repository repository,
            string workingDirectory,
            DevBoard.DevSpaces.IDevSpaceSessionLauncher launcher = null,
            DevBoard.DevSpaces.Terminal.DevSpaceTerminalRegistry terminalRegistry = null)
        {
            _workingDirectory = workingDirectory;
            _terminalRegistry = terminalRegistry ?? DevBoard.DevSpaces.Terminal.DevSpaceTerminalRegistry.Instance;
            Launcher = launcher ?? new DevBoard.DevSpaces.LocalDevSpaceSessionLauncher();
            Files = new DevSpaceFiles(workingDirectory);
            Dashboard = new DevSpaceDashboard(this, workingDirectory, repository);
            RoslynAnalytics = new DevSpaceRoslynAnalytics(Dashboard);

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
            // Kept for bootstrap compatibility. Terminal creation is now always explicit.
        }

        public void ActivateDashboard() => ActivePage = Models.DevSpacePage.Dashboard;
        public void ActivateFiles() => ActivePage = Models.DevSpacePage.Files;
        public void ActivateAIRouter() => ActivePage = Models.DevSpacePage.AIRouter;
        public void ActivateTerminals() => ActivePage = Models.DevSpacePage.Terminals;
        public void ActivateRoslyn() => ActivePage = Models.DevSpacePage.Roslyn;

        public bool OpenFile(string relativePath)
        {
            ActivateFiles();
            var opened = Files.OpenFile(relativePath);
            if (opened)
                Dashboard.AddActivity(DevSpaceActivityKind.FileOpened, relativePath);
            return opened;
        }

        public DevSpaceTerminal CreateTerminal() => CreateTerminalAt(-1);

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot)
        {
            var settings = DevBoard.DevSpaces.DevSpaceProfileSettings.Instance;
            return CreateTerminalAt(preferredSlot, settings.DefaultTerminal,
                DevBoard.DevSpaces.DevSpaceProfileSettings.GetTerminalDisplayName(settings.DefaultTerminal));
        }

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot, string terminal, string displayName) =>
            CreateTerminalAt(preferredSlot, terminal, displayName, _workingDirectory, null);

        public DevSpaceTerminal CreateTerminalAt(int preferredSlot, string terminal, string displayName, string workingDirectory, string startupCommand)
        {
            var settings = DevBoard.DevSpaces.DevSpaceProfileSettings.Instance;
            if (string.IsNullOrWhiteSpace(terminal))
                terminal = settings.DefaultTerminal;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = DevBoard.DevSpaces.DevSpaceProfileSettings.GetTerminalDisplayName(terminal);
            if (string.IsNullOrWhiteSpace(workingDirectory))
                workingDirectory = _workingDirectory;

            var number = _nextSessionNumber++;
            var created = new DevSpaceTerminal(
                $"{displayName} {number}",
                terminal,
                workingDirectory,
                startupCommand,
                _workingDirectory);
            _terminalRegistry.Register(created);
            Sessions.Add(created);
            OnPropertyChanged(nameof(TerminalCount));
            ActiveTerminal = created;
            ActivateTerminals();
            Dashboard.AddActivity(DevSpaceActivityKind.SessionStarted, $"{created.Title} started");
            _preferredSlot = preferredSlot;
            RebuildSlots();
            return created;
        }

        public DevSpaceTerminal CreateProfileTerminalAt(
            int preferredSlot,
            DevBoard.DevSpaces.DevSpaceTerminalProfile profile,
            bool showProfileIcon = true)
        {
            DevBoard.DevSpaces.DevSpaceProfileSettings.ValidateProfile(profile);
            var settings = DevBoard.DevSpaces.DevSpaceProfileSettings.Instance;
            var workingDirectory = DevBoard.DevSpaces.DevSpaceProfileSettings.ResolveWorkingDirectory(_workingDirectory, profile.Path);

            if (string.Equals(profile.Command, "codex", StringComparison.OrdinalIgnoreCase))
                DevBoard.DevSpaces.CodexWorkspaceTrust.EnsureTrusted(workingDirectory);
            else if (string.Equals(profile.Command, "agy", StringComparison.OrdinalIgnoreCase))
                DevBoard.DevSpaces.AntigravityWorkspaceTrust.EnsureTrusted(workingDirectory);

            return CreateTerminalAt(
                preferredSlot,
                settings.DefaultTerminal,
                showProfileIcon ? profile.DisplayName : profile.Name,
                workingDirectory,
                profile.Command);
        }

        public DevSpaceTerminal CreateCopilotTerminalAt(int preferredSlot) =>
            CreateAgentTerminalAt(preferredSlot, DevBoard.DevSpaces.DevSpaceAgent.BuiltIn[0]);

        public DevSpaceTerminal CreateAgentTerminalAt(int preferredSlot, DevBoard.DevSpaces.DevSpaceAgent agent)
        {
            ArgumentNullException.ThrowIfNull(agent);

            var page = GetBuiltInAgentPage(agent.Command);
            if (page == null)
            {
                var fallbackSettings = DevBoard.DevSpaces.DevSpaceProfileSettings.Instance;
                return CreateTerminalAt(preferredSlot, fallbackSettings.DefaultTerminal, agent.Name, _workingDirectory, agent.Command);
            }

            var existing = GetAgentSession(page.Value);
            if (existing != null)
            {
                ActivePage = page.Value;
                return existing;
            }

            EnsureAgentWorkspaceTrusted(agent.Command);
            var settings = DevBoard.DevSpaces.DevSpaceProfileSettings.Instance;
            var created = new DevSpaceTerminal(
                agent.Name,
                settings.DefaultTerminal,
                _workingDirectory,
                agent.Command,
                _workingDirectory);
            _terminalRegistry.Register(created);
            SetAgentSession(page.Value, created);
            ActivePage = page.Value;
            Dashboard.AddActivity(DevSpaceActivityKind.SessionStarted, $"{agent.Name} started");
            return created;
        }

        public void ActivateTerminal(DevSpaceTerminal terminal)
        {
            if (terminal == null || !Sessions.Contains(terminal))
                return;
            ActiveTerminal = terminal;
            ActivateTerminals();
            RebuildSlots();
        }

        public void CloseTerminal(DevSpaceTerminal terminal)
        {
            if (terminal == null || !Sessions.Remove(terminal))
                return;
            OnPropertyChanged(nameof(TerminalCount));
            _terminalRegistry.Unregister(terminal.Id);
            Dashboard.AddActivity(DevSpaceActivityKind.SessionClosed, $"{terminal.Title} closed");
            terminal.Dispose();
            if (ActiveTerminal == terminal)
                ActiveTerminal = Sessions.Count > 0 ? Sessions[Sessions.Count - 1] : null;
            RebuildSlots();
        }

        public void StopAll()
        {
            for (var i = Sessions.Count - 1; i >= 0; i--)
            {
                _terminalRegistry.Unregister(Sessions[i].Id);
                Sessions[i].Dispose();
            }
            Sessions.Clear();
            OnPropertyChanged(nameof(TerminalCount));
            VisibleSlots.Clear();
            ActiveTerminal = null;
            _preferredSlot = -1;
            OnPropertyChanged(nameof(GridRows));
            OnPropertyChanged(nameof(GridColumns));
            OnPropertyChanged(nameof(GridCapacity));
            OnPropertyChanged(nameof(VisibleSlots));
        }

        public void Dispose()
        {
            Dashboard.Dispose();
            StopAll();
            StopAgents();
        }

        private void StopAgents()
        {
            StopAgent(_copilotSession);
            StopAgent(_codexSession);
            StopAgent(_antigravitySession);
            _copilotSession = null;
            _codexSession = null;
            _antigravitySession = null;
        }

        private void StopAgent(DevSpaceTerminal terminal)
        {
            if (terminal == null)
                return;
            _terminalRegistry.Unregister(terminal.Id);
            terminal.Dispose();
        }

        private static Models.DevSpacePage? GetBuiltInAgentPage(string command)
        {
            if (string.Equals(command, "copilot", StringComparison.OrdinalIgnoreCase))
                return Models.DevSpacePage.Copilot;
            if (string.Equals(command, "codex", StringComparison.OrdinalIgnoreCase))
                return Models.DevSpacePage.Codex;
            if (string.Equals(command, "agy", StringComparison.OrdinalIgnoreCase))
                return Models.DevSpacePage.Antigravity;
            return null;
        }

        private DevSpaceTerminal GetAgentSession(Models.DevSpacePage page) => page switch
        {
            Models.DevSpacePage.Copilot => _copilotSession,
            Models.DevSpacePage.Codex => _codexSession,
            Models.DevSpacePage.Antigravity => _antigravitySession,
            _ => null,
        };

        private void SetAgentSession(Models.DevSpacePage page, DevSpaceTerminal terminal)
        {
            switch (page)
            {
                case Models.DevSpacePage.Copilot:
                    _copilotSession = terminal;
                    OnPropertyChanged(nameof(CopilotSession));
                    break;
                case Models.DevSpacePage.Codex:
                    _codexSession = terminal;
                    OnPropertyChanged(nameof(CodexSession));
                    break;
                case Models.DevSpacePage.Antigravity:
                    _antigravitySession = terminal;
                    OnPropertyChanged(nameof(AntigravitySession));
                    break;
            }
            OnPropertyChanged(nameof(ActiveAgentTerminal));
        }

        private void EnsureAgentWorkspaceTrusted(string command)
        {
            if (string.Equals(command, "copilot", StringComparison.OrdinalIgnoreCase))
                DevBoard.DevSpaces.CopilotWorkspaceTrust.EnsureTrusted(_workingDirectory);
            else if (string.Equals(command, "codex", StringComparison.OrdinalIgnoreCase))
                DevBoard.DevSpaces.CodexWorkspaceTrust.EnsureTrusted(_workingDirectory);
            else if (string.Equals(command, "agy", StringComparison.OrdinalIgnoreCase))
                DevBoard.DevSpaces.AntigravityWorkspaceTrust.EnsureTrusted(_workingDirectory);
        }

        private void RebuildSlots()
        {
            var capacity = GridCapacity;
            var slots = new DevSpaceTerminal[capacity];
            if (capacity == 1)
            {
                slots[0] = ActiveTerminal ?? (Sessions.Count > 0 ? Sessions[0] : null);
            }
            else
            {
                var placeActiveInPreferredSlot = ActiveTerminal != null && _preferredSlot >= 0 && _preferredSlot < capacity && Sessions.Contains(ActiveTerminal);
                if (placeActiveInPreferredSlot)
                    slots[_preferredSlot] = ActiveTerminal;
                var slotIndex = 0;
                foreach (var session in Sessions)
                {
                    if (placeActiveInPreferredSlot && session == ActiveTerminal)
                        continue;
                    while (slotIndex < capacity && slots[slotIndex] != null)
                        slotIndex++;
                    if (slotIndex >= capacity)
                        break;
                    slots[slotIndex++] = session;
                }
                if (ActiveTerminal != null && Array.IndexOf(slots, ActiveTerminal) < 0)
                    slots[capacity - 1] = ActiveTerminal;
            }
            VisibleSlots.Clear();
            for (var i = 0; i < capacity; i++)
                VisibleSlots.Add(new DevSpaceGridSlot(i, slots[i]));
            _preferredSlot = -1;
            NotifyLayoutChanged();
        }

        private void NotifyLayoutChanged()
        {
            OnPropertyChanged(nameof(GridRows));
            OnPropertyChanged(nameof(GridColumns));
            OnPropertyChanged(nameof(GridCapacity));
            OnPropertyChanged(nameof(VisibleSlots));
        }

        private static readonly Models.DevSpaceLayout[] LayoutOptions =
        [
            Models.DevSpaceLayout.Auto,
            Models.DevSpaceLayout.Tab,
            Models.DevSpaceLayout.OneByTwo,
            Models.DevSpaceLayout.TwoByTwo,
            Models.DevSpaceLayout.ThreeByThree,
        ];
        private readonly string _workingDirectory;
        private readonly DevBoard.DevSpaces.Terminal.DevSpaceTerminalRegistry _terminalRegistry;
        private DevSpaceTerminal _activeTerminal;
        private DevSpaceTerminal _copilotSession;
        private DevSpaceTerminal _codexSession;
        private DevSpaceTerminal _antigravitySession;
        private Models.DevSpaceLayout _layout;
        private Models.DevSpacePage _activePage = Models.DevSpacePage.Dashboard;
        private int _nextSessionNumber = 1;
        private int _preferredSlot = -1;
    }
}
