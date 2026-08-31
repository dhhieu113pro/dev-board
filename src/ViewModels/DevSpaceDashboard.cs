using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.ViewModels
{
    public sealed class DevSpaceDashboard : ObservableObject, IDisposable
    {
        public string WorkspacePath { get; }
        public string WorkspaceName { get; }

        public Statistics RepositoryStatistics { get; }
        public ViewLogs RepositoryLogs { get; }
        public bool HasRepositoryInsights => _repository != null;

        public int StatisticsModeIndex
        {
            get => RepositoryStatistics == null ? 0 : GetStatisticsModeIndex(RepositoryStatistics.ViewMode);
            set
            {
                if (RepositoryStatistics == null)
                    return;

                var mode = GetStatisticsMode(value);
                if (RepositoryStatistics.ViewMode == mode)
                    return;

                RepositoryStatistics.ViewMode = mode;
                OnPropertyChanged();
            }
        }

        public DevSpaceCapabilityState CopilotCapability { get; }
        public DevSpaceCapabilityState CodexCapability { get; }
        public DevSpaceCapabilityState AntigravityCapability { get; }

        public DevBoard.DevSpaces.RoslynDevSpaceState RoslynState => _roslynService.State;
        public string RoslynFailureReason => _roslynService.FailureReason;
        public bool IsRoslynInitializing => RoslynState == DevBoard.DevSpaces.RoslynDevSpaceState.Initializing;
        public bool CanInitializeRoslyn => RoslynState == DevBoard.DevSpaces.RoslynDevSpaceState.Unavailable || RoslynState == DevBoard.DevSpaces.RoslynDevSpaceState.Failed;
        public string RoslynActionText => RoslynState == DevBoard.DevSpaces.RoslynDevSpaceState.Failed ? "Retry" : "Initialize";
        public string RoslynStatusText => RoslynState switch
        {
            DevBoard.DevSpaces.RoslynDevSpaceState.Initializing => "Initializing…",
            DevBoard.DevSpaces.RoslynDevSpaceState.Available => "Available",
            DevBoard.DevSpaces.RoslynDevSpaceState.Failed => "Failed",
            _ => "Unavailable",
        };
        public string RoslynHealthStatusText => RoslynState == DevBoard.DevSpaces.RoslynDevSpaceState.Available
            ? $"Available · {UnusedCodeCount} unused"
            : RoslynStatusText;
        public string RoslynSummaryText => RoslynState switch
        {
            DevBoard.DevSpaces.RoslynDevSpaceState.Initializing => "Roslyn is loading this workspace…",
            DevBoard.DevSpaces.RoslynDevSpaceState.Available => "Roslyn is available for this DevSpace.",
            DevBoard.DevSpaces.RoslynDevSpaceState.Failed when !string.IsNullOrWhiteSpace(RoslynFailureReason) => RoslynFailureReason,
            DevBoard.DevSpaces.RoslynDevSpaceState.Failed => "Roslyn initialization failed. Use Retry in Workspace Health.",
            _ => "Roslyn is not running. Click Initialize to analyze this workspace.",
        };

        public IReadOnlyList<DevBoard.DevSpaces.RoslynUnusedCodeItem> UnusedCode => _roslynService.UnusedCode;
        public int UnusedCodeCount => _roslynService.UnusedCodeCount;
        public bool IsUnusedCodeVisible => RoslynState == DevBoard.DevSpaces.RoslynDevSpaceState.Available;
        public bool IsAnalyzingUnusedCode => _roslynService.IsAnalyzingUnusedCode;
        public string UnusedCodeFailureReason => _roslynService.UnusedCodeFailureReason;
        public string UnusedCodeFilter
        {
            get => _unusedCodeFilter;
            private set
            {
                if (SetProperty(ref _unusedCodeFilter, value))
                    OnPropertyChanged(nameof(FilteredUnusedCode));
            }
        }
        public IReadOnlyList<DevBoard.DevSpaces.RoslynUnusedCodeItem> FilteredUnusedCode => UnusedCode
            .Where(x => UnusedCodeFilter switch
            {
                "Members" => x.Kind == DevBoard.DevSpaces.RoslynUnusedCodeKind.Member,
                "Variables" => x.Kind == DevBoard.DevSpaces.RoslynUnusedCodeKind.Variable,
                "Usings" => x.Kind == DevBoard.DevSpaces.RoslynUnusedCodeKind.Using,
                _ => true,
            })
            .ToArray();

        public IReadOnlyList<DevBoard.DevSpaces.DevSpaceTerminalProfile> Profiles =>
            DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.Profiles;

        public string CurrentBranch
        {
            get => _currentBranch;
            private set => SetProperty(ref _currentBranch, value ?? string.Empty);
        }

        public string BaseBranch
        {
            get => _baseBranch;
            private set => SetProperty(ref _baseBranch, value ?? string.Empty);
        }

        public int AheadCount
        {
            get => _aheadCount;
            private set => SetProperty(ref _aheadCount, value);
        }

        public int BehindCount
        {
            get => _behindCount;
            private set => SetProperty(ref _behindCount, value);
        }

        public DevSpaceGitSummary GitSummary
        {
            get => _gitSummary;
            private set => SetProperty(ref _gitSummary, value);
        }

        public AvaloniaList<DevSpaceActivityEntry> Activity { get; } = [];

        public IReadOnlyList<DevSpaceDashboardSessionRow> Sessions => _owner.Sessions
            .Select(x => new DevSpaceDashboardSessionRow(x, x.Title, x.State, x.WorkingDirectory))
            .ToArray();

        public DevSpaceDashboard(
            DevSpaces owner,
            string workspacePath,
            Repository repository = null,
            DevBoard.DevSpaces.IRoslynWorkspaceLoader roslynLoader = null)
        {
            _owner = owner;
            _repository = repository;
            WorkspacePath = workspacePath;
            WorkspaceName = GetWorkspaceName(workspacePath);
            CopilotCapability = DevBoard.DevSpaces.DevSpaceToolHealth.CheckCommand("copilot");
            CodexCapability = DevBoard.DevSpaces.DevSpaceToolHealth.CheckCommand("codex");
            AntigravityCapability = DevBoard.DevSpaces.DevSpaceToolHealth.CheckCommand("agy");
            _roslynService = new DevBoard.DevSpaces.RoslynDevSpaceService(
                workspacePath,
                roslynLoader ?? new DevBoard.DevSpaces.MSBuildRoslynWorkspaceLoader());
            _roslynService.PropertyChanged += OnRoslynPropertyChanged;
            _owner.Sessions.CollectionChanged += OnSessionsChanged;

            if (_repository != null)
            {
                RepositoryStatistics = new Statistics(_repository.FullPath)
                {
                    ViewMode = Models.StatisticsMode.ThisWeek,
                };
                RepositoryLogs = new ViewLogs(_repository);

                _repository.PropertyChanged += OnRepositoryPropertyChanged;
                if (_repository.WorkingCopy != null)
                    _repository.WorkingCopy.PropertyChanged += OnWorkingCopyPropertyChanged;
                RefreshRepositorySummary();
            }
        }

        public static Models.StatisticsMode GetStatisticsMode(int index) => index switch
        {
            0 => Models.StatisticsMode.ThisWeek,
            1 => Models.StatisticsMode.ThisMonth,
            _ => Models.StatisticsMode.All,
        };

        public static int GetStatisticsModeIndex(Models.StatisticsMode mode) => mode switch
        {
            Models.StatisticsMode.ThisWeek => 0,
            Models.StatisticsMode.ThisMonth => 1,
            _ => 2,
        };

        public static DevSpaceGitSummary BuildGitSummary(
            IEnumerable<Models.Change> staged,
            IEnumerable<Models.Change> unstaged)
        {
            var states = new Dictionary<string, Models.ChangeState>(StringComparer.Ordinal);
            var stagedPaths = new HashSet<string>(StringComparer.Ordinal);
            var unstagedPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var change in staged ?? [])
            {
                if (string.IsNullOrEmpty(change.Path))
                    continue;
                stagedPaths.Add(change.Path);
                var state = change.Index != Models.ChangeState.None ? change.Index : change.WorkTree;
                states[change.Path] = state;
            }

            foreach (var change in unstaged ?? [])
            {
                if (string.IsNullOrEmpty(change.Path))
                    continue;
                unstagedPaths.Add(change.Path);
                var state = change.WorkTree != Models.ChangeState.None ? change.WorkTree : change.Index;
                if (state != Models.ChangeState.None || !states.ContainsKey(change.Path))
                    states[change.Path] = state;
            }

            var added = 0;
            var modified = 0;
            var deleted = 0;
            var renamed = 0;
            foreach (var state in states.Values)
            {
                switch (state)
                {
                    case Models.ChangeState.Added:
                    case Models.ChangeState.Untracked:
                        added++;
                        break;
                    case Models.ChangeState.Deleted:
                        deleted++;
                        break;
                    case Models.ChangeState.Renamed:
                        renamed++;
                        break;
                    case Models.ChangeState.None:
                        break;
                    default:
                        modified++;
                        break;
                }
            }

            return new DevSpaceGitSummary(
                states.Count,
                added,
                modified,
                deleted,
                renamed,
                stagedPaths.Count,
                unstagedPaths.Count);
        }

        public void AddActivity(DevSpaceActivityKind kind, string text, DateTimeOffset? at = null)
        {
            Activity.Insert(0, new DevSpaceActivityEntry(kind, text ?? string.Empty, at ?? DateTimeOffset.UtcNow));
            while (Activity.Count > 20)
                Activity.RemoveAt(Activity.Count - 1);
        }

        public Task InitializeRoslynAsync() => _roslynService.InitializeAsync();
        public Task RefreshUnusedCodeAsync() => _roslynService.RefreshUnusedCodeAsync();

        public void SetUnusedCodeFilter(string filter)
        {
            UnusedCodeFilter = filter is "Members" or "Variables" or "Usings" ? filter : "All";
        }

        public bool OpenUnusedCode(DevBoard.DevSpaces.RoslynUnusedCodeItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath))
                return false;

            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(WorkspacePath, item.FilePath);
            }
            catch
            {
                return false;
            }

            return !relativePath.StartsWith("..", StringComparison.Ordinal) && _owner.OpenFile(relativePath);
        }

        public void OpenSession(DevSpaceTerminal terminal) => _owner.ActivateTerminal(terminal);
        public void CloseSession(DevSpaceTerminal terminal) => _owner.CloseTerminal(terminal);
        public void OpenFiles() => _owner.ActivateFiles();
        public void OpenWorkspaceFolder()
        {
            if (!string.IsNullOrWhiteSpace(WorkspacePath))
                Native.OS.OpenInFileManager(WorkspacePath);
        }

        public void OpenWorkingCopy()
        {
            if (_repository != null)
                _repository.SelectedViewIndex = 1;
        }

        public DevSpaceTerminal StartDefaultTerminal() => _owner.CreateTerminal();
        public DevSpaceTerminal StartProfile(DevBoard.DevSpaces.DevSpaceTerminalProfile profile) => _owner.CreateProfileTerminalAt(-1, profile);
        public DevSpaceTerminal StartAgent(DevBoard.DevSpaces.DevSpaceAgent agent) => _owner.CreateAgentTerminalAt(-1, agent);
        public void CloseAllSessions() => _owner.StopAll();

        public void Dispose()
        {
            _roslynService.PropertyChanged -= OnRoslynPropertyChanged;
            _roslynService.Dispose();
            _owner.Sessions.CollectionChanged -= OnSessionsChanged;
            if (_repository != null)
            {
                _repository.PropertyChanged -= OnRepositoryPropertyChanged;
                if (_repository.WorkingCopy != null)
                    _repository.WorkingCopy.PropertyChanged -= OnWorkingCopyPropertyChanged;
            }
        }

        private void OnSessionsChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
            OnPropertyChanged(nameof(Sessions));

        private void OnRoslynPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DevBoard.DevSpaces.RoslynDevSpaceService.State))
            {
                OnPropertyChanged(nameof(RoslynState));
                OnPropertyChanged(nameof(RoslynStatusText));
                OnPropertyChanged(nameof(RoslynHealthStatusText));
                OnPropertyChanged(nameof(IsRoslynInitializing));
                OnPropertyChanged(nameof(CanInitializeRoslyn));
                OnPropertyChanged(nameof(RoslynActionText));
                OnPropertyChanged(nameof(RoslynSummaryText));
                OnPropertyChanged(nameof(IsUnusedCodeVisible));
            }
            else if (e.PropertyName == nameof(DevBoard.DevSpaces.RoslynDevSpaceService.FailureReason))
            {
                OnPropertyChanged(nameof(RoslynFailureReason));
                OnPropertyChanged(nameof(RoslynSummaryText));
            }
            else if (e.PropertyName == nameof(DevBoard.DevSpaces.RoslynDevSpaceService.UnusedCode) ||
                     e.PropertyName == nameof(DevBoard.DevSpaces.RoslynDevSpaceService.UnusedCodeCount))
            {
                OnPropertyChanged(nameof(UnusedCode));
                OnPropertyChanged(nameof(UnusedCodeCount));
                OnPropertyChanged(nameof(FilteredUnusedCode));
                OnPropertyChanged(nameof(RoslynHealthStatusText));
            }
            else if (e.PropertyName == nameof(DevBoard.DevSpaces.RoslynDevSpaceService.IsAnalyzingUnusedCode))
            {
                OnPropertyChanged(nameof(IsAnalyzingUnusedCode));
            }
            else if (e.PropertyName == nameof(DevBoard.DevSpaces.RoslynDevSpaceService.UnusedCodeFailureReason))
            {
                OnPropertyChanged(nameof(UnusedCodeFailureReason));
            }
        }

        private void OnRepositoryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Repository.CurrentBranch) ||
                e.PropertyName == nameof(Repository.LocalChangesCount))
                RefreshRepositorySummary();
        }

        private void OnWorkingCopyPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WorkingCopy.Staged) || e.PropertyName == nameof(WorkingCopy.Unstaged))
                RefreshRepositorySummary();
        }

        private void RefreshRepositorySummary()
        {
            if (_repository == null)
                return;

            var branch = _repository.CurrentBranch;
            CurrentBranch = branch?.Name ?? string.Empty;
            AheadCount = branch?.Ahead?.Count ?? 0;
            BehindCount = branch?.Behind?.Count ?? 0;
            BaseBranch = Models.WorktreeBaseBranch.ReadPersisted(_repository.GitDir, CurrentBranch);
            GitSummary = BuildGitSummary(_repository.WorkingCopy?.Staged, _repository.WorkingCopy?.Unstaged);
        }

        private static string GetWorkspaceName(string workspacePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                return string.Empty;
            var trimmed = workspacePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(trimmed);
        }

        private readonly DevSpaces _owner;
        private readonly Repository _repository;
        private readonly DevBoard.DevSpaces.RoslynDevSpaceService _roslynService;
        private string _currentBranch = string.Empty;
        private string _baseBranch = string.Empty;
        private int _aheadCount;
        private int _behindCount;
        private DevSpaceGitSummary _gitSummary = DevSpaceGitSummary.Empty;
        private string _unusedCodeFilter = "All";
    }
}
