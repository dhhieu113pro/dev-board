using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.ViewModels
{
    public sealed class PullRequestCherryPickPage : ObservableObject
    {
        public List<Models.Remote> SupportedRemotes
        {
            get => _supportedRemotes;
            private set => SetProperty(ref _supportedRemotes, value);
        }

        public Models.Remote SelectedRemote
        {
            get => _selectedRemote;
            set => SetProperty(ref _selectedRemote, value);
        }

        public string PullRequestNumber
        {
            get => _pullRequestNumber;
            set => SetProperty(ref _pullRequestNumber, value);
        }

        public List<Models.Commit> Commits
        {
            get => _commits;
            private set
            {
                if (SetProperty(ref _commits, value))
                    OnPropertyChanged(nameof(HasCommits));
            }
        }

        public bool HasCommits => Commits is { Count: > 0 };

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public PullRequestCherryPickPage(Repository repo)
        {
            _repo = repo;
            RefreshRemotes();
        }

        public void RefreshRemotes()
        {
            var supported = new List<Models.Remote>();
            foreach (var remote in _repo.Remotes)
            {
                if (Models.PullRequestRemote.TryCreate(remote, 1, out _))
                    supported.Add(remote);
            }

            var selectedName = SelectedRemote?.Name;
            SupportedRemotes = supported;
            SelectedRemote = supported.Find(x => x.Name.Equals(selectedName, StringComparison.Ordinal)) ??
                supported.Find(x => x.Name.Equals("origin", StringComparison.Ordinal)) ??
                (supported.Count > 0 ? supported[0] : null);
        }

        public async Task LoadAsync()
        {
            if (IsLoading)
                return;

            Commits = [];
            StatusMessage = string.Empty;

            if (SelectedRemote == null)
            {
                StatusMessage = "No GitHub or Azure DevOps remote is available.";
                return;
            }

            if (!int.TryParse(PullRequestNumber, out var number) || number <= 0)
            {
                StatusMessage = "Enter a valid pull request number.";
                return;
            }

            if (!Models.PullRequestRemote.TryCreate(SelectedRemote, number, out var descriptor))
            {
                StatusMessage = "The selected remote does not support pull-request refs.";
                return;
            }

            IsLoading = true;
            var log = _repo.CreateLog($"Load Pull Request #{number}");
            try
            {
                var mergeFetch = new Commands.FetchPullRequest(
                    _repo.FullPath,
                    descriptor.RemoteName,
                    descriptor.MergeRemoteRef,
                    descriptor.MergeLocalRef)
                {
                    Log = log,
                };

                var fetchedMerge = await mergeFetch.RunAsync();
                string limits = null;

                if (fetchedMerge)
                {
                    limits = Models.PullRequestCommitRange.FromMergeRef(descriptor.MergeLocalRef);
                }
                else if (descriptor.Provider == Models.PullRequestProvider.GitHub &&
                         descriptor.HeadRemoteRef != null && descriptor.HeadLocalRef != null)
                {
                    var headFetch = new Commands.FetchPullRequest(
                        _repo.FullPath,
                        descriptor.RemoteName,
                        descriptor.HeadRemoteRef,
                        descriptor.HeadLocalRef)
                    {
                        Log = log,
                    };

                    if (!await headFetch.RunAsync())
                    {
                        StatusMessage = $"Unable to fetch pull request #{number} from '{descriptor.RemoteName}'.";
                        return;
                    }

                    var mergeBase = await new Commands.MergeBase(_repo.FullPath, "HEAD", descriptor.HeadLocalRef)
                        .GetResultAsync();
                    if (string.IsNullOrEmpty(mergeBase))
                    {
                        StatusMessage = "Unable to resolve the pull request base commit.";
                        return;
                    }

                    limits = Models.PullRequestCommitRange.FromHeadFallback(mergeBase, descriptor.HeadLocalRef);
                }
                else
                {
                    StatusMessage = $"Unable to fetch pull request #{number} from '{descriptor.RemoteName}'.";
                    return;
                }

                var commits = await new Commands.QueryCommits(_repo.FullPath, limits, false)
                    .GetResultAsync();

                if (commits.Count == 0)
                {
                    StatusMessage = "No commits were found for this pull request.";
                    return;
                }

                if (Models.PullRequestCommitRange.ContainsMergeCommit(commits))
                {
                    StatusMessage = "This pull request contains merge commits. Cherry Pick PR v1 only supports linear pull requests.";
                    return;
                }

                Commits = commits;
                StatusMessage = $"Loaded {commits.Count} commit(s) from PR #{number}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load pull request: {ex.Message}";
            }
            finally
            {
                log.Complete();
                IsLoading = false;
            }
        }

        public async Task CherryPickAsync(Models.Commit commit)
        {
            if (commit == null || !await CanStartCherryPickAsync())
                return;

            _repo.ShowPopup(new CherryPick(_repo, [commit]));
        }

        public async Task CherryPickAllAsync()
        {
            if (!HasCommits || !await CanStartCherryPickAsync())
                return;

            _repo.ShowPopup(new CherryPick(_repo, [.. Commits]));
        }

        private async Task<bool> CanStartCherryPickAsync()
        {
            if (_repo.CurrentBranch == null || _repo.CurrentBranch.IsDetachedHead)
            {
                _repo.SendNotification("Check out a local branch before cherry-picking a pull request.", true);
                return false;
            }

            if (_repo.InProgressContext != null)
            {
                _repo.SendNotification("Finish or abort the current Git operation before cherry-picking a pull request.", true);
                return false;
            }

            var changes = await new Commands.QueryLocalChanges(_repo.FullPath, true).GetResultAsync();
            if (changes.Count > 0)
            {
                _repo.SendNotification("The working tree must be clean before cherry-picking a pull request.", true);
                return false;
            }

            if (!_repo.CanCreatePopup())
                return false;

            return true;
        }

        private readonly Repository _repo;
        private List<Models.Remote> _supportedRemotes = [];
        private Models.Remote _selectedRemote = null;
        private string _pullRequestNumber = string.Empty;
        private List<Models.Commit> _commits = [];
        private bool _isLoading;
        private string _statusMessage = string.Empty;
    }
}
