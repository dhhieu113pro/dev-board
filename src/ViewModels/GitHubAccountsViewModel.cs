using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevBoard.ViewModels
{
    public partial class GitHubAccountsViewModel : ObservableObject
    {
        public GitHubAccountsViewModel()
        {
            Accounts = new AvaloniaList<Models.GitHubAccount>();
            RefreshAccounts();
        }

        public AvaloniaList<Models.GitHubAccount> Accounts { get; }

        [ObservableProperty]
        private Models.GitHubAccount _selectedAccount;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private Models.GitHubAccount _editingAccount;

        [ObservableProperty]
        private string _newToken = string.Empty;

        [ObservableProperty]
        private string _testResult = string.Empty;

        [ObservableProperty]
        private bool _isTesting;

        [ObservableProperty]
        private bool _isImportingGitHubCli;

        [ObservableProperty]
        private string _importResult = string.Empty;

        public bool ShowEmptyState => Accounts.Count == 0 && !IsEditing;

        public string EditingTitle
            => Services.GitHubAccountStore.Instance.Get(EditingAccount?.Id ?? Guid.Empty) == null
                ? "Add GitHub Account"
                : "Edit GitHub Account";

        public int EditingAuthTypeIndex
        {
            get => EditingAccount?.AuthType switch
            {
                Models.GitHubAuthType.GitHubCli => 0,
                Models.GitHubAuthType.PersonalAccessToken => 1,
                Models.GitHubAuthType.SSHKey => 2,
                _ => 0,
            };
            set
            {
                if (EditingAccount == null)
                    return;

                EditingAccount.AuthType = value switch
                {
                    1 => Models.GitHubAuthType.PersonalAccessToken,
                    2 => Models.GitHubAuthType.SSHKey,
                    _ => Models.GitHubAuthType.GitHubCli,
                };
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEditingGitHubCli));
                OnPropertyChanged(nameof(IsEditingPat));
                OnPropertyChanged(nameof(IsEditingSsh));
            }
        }

        public bool IsEditingGitHubCli => EditingAccount?.AuthType == Models.GitHubAuthType.GitHubCli;
        public bool IsEditingPat => EditingAccount?.AuthType == Models.GitHubAuthType.PersonalAccessToken;
        public bool IsEditingSsh => EditingAccount?.AuthType == Models.GitHubAuthType.SSHKey;

        [RelayCommand]
        private void BeginAdd()
        {
            EditingAccount = new Models.GitHubAccount
            {
                AuthType = Models.GitHubAuthType.GitHubCli,
                IsDefault = Accounts.Count == 0,
            };
            NewToken = string.Empty;
            TestResult = string.Empty;
            IsEditing = true;
            NotifyEditorState();
        }

        [RelayCommand]
        private async Task ImportGitHubCliAccountsAsync()
        {
            if (IsImportingGitHubCli)
                return;

            IsImportingGitHubCli = true;
            ImportResult = "Reading GitHub CLI accounts...";
            try
            {
                var discovered = await Services.GitHubCliCredential.DiscoverAccountsAsync().ConfigureAwait(true);
                if (discovered.Count == 0)
                {
                    ImportResult = "No stored GitHub CLI accounts found";
                    return;
                }

                var store = Services.GitHubAccountStore.Instance;
                Models.GitHubAccount activeCandidate = null;
                Guid? firstImportedId = null;
                var imported = 0;

                foreach (var cliAccount in discovered)
                {
                    var existing = store.Accounts.FirstOrDefault(account =>
                        account.AuthType == Models.GitHubAuthType.GitHubCli &&
                        string.Equals(account.Host, cliAccount.Host, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(account.Username, cliAccount.Username, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        existing = new Models.GitHubAccount
                        {
                            Name = cliAccount.Username,
                            Username = cliAccount.Username,
                            Host = cliAccount.Host,
                            AuthType = Models.GitHubAuthType.GitHubCli,
                        };
                        store.AddOrUpdate(existing);
                        imported++;
                        firstImportedId ??= existing.Id;
                    }

                    if (cliAccount.IsActive &&
                        (activeCandidate == null || string.Equals(cliAccount.Host, "github.com", StringComparison.OrdinalIgnoreCase)))
                        activeCandidate = existing;
                }

                if (Accounts.Count == 0 && activeCandidate != null)
                    store.SetDefault(activeCandidate);

                RefreshAccounts(firstImportedId ?? activeCandidate?.Id);
                ImportResult = imported == 0
                    ? $"GitHub CLI accounts already imported ({discovered.Count})"
                    : $"Imported {imported} GitHub CLI account{(imported == 1 ? string.Empty : "s")}";
            }
            catch (Exception ex)
            {
                ImportResult = $"✗ {ex.Message}";
            }
            finally
            {
                IsImportingGitHubCli = false;
            }
        }

        [RelayCommand]
        private void BeginEdit(Models.GitHubAccount account)
        {
            if (account == null)
                return;

            EditingAccount = new Models.GitHubAccount
            {
                Id = account.Id,
                Name = account.Name,
                Username = account.Username,
                Email = account.Email,
                Host = account.Host,
                AuthType = account.AuthType,
                SSHKeyPath = account.SSHKeyPath,
                IsDefault = account.IsDefault,
                AvatarUrl = account.AvatarUrl,
                CreatedAt = account.CreatedAt,
                UpdatedAt = DateTime.Now,
            };
            NewToken = string.Empty;
            TestResult = string.Empty;
            IsEditing = true;
            NotifyEditorState();
        }

        [RelayCommand]
        private void CancelEdit()
        {
            EditingAccount = null;
            IsEditing = false;
            NewToken = string.Empty;
            TestResult = string.Empty;
            NotifyEditorState();
        }

        [RelayCommand]
        private void SaveEdit()
        {
            if (EditingAccount == null)
                return;

            if (string.IsNullOrWhiteSpace(EditingAccount.Name))
            {
                TestResult = "Name is required";
                return;
            }

            var store = Services.GitHubAccountStore.Instance;
            var existing = store.Get(EditingAccount.Id);

            if (EditingAccount.AuthType == Models.GitHubAuthType.GitHubCli)
            {
                if (string.IsNullOrWhiteSpace(EditingAccount.Username))
                {
                    TestResult = "Username is required";
                    return;
                }

                if (string.IsNullOrWhiteSpace(EditingAccount.Host))
                {
                    TestResult = "GitHub host is required";
                    return;
                }
            }
            else if (EditingAccount.AuthType == Models.GitHubAuthType.PersonalAccessToken)
            {
                if (string.IsNullOrWhiteSpace(EditingAccount.Username))
                {
                    TestResult = "Username is required";
                    return;
                }

                var effectiveToken = string.IsNullOrWhiteSpace(NewToken) ? existing?.Token : NewToken;
                if (string.IsNullOrWhiteSpace(effectiveToken))
                {
                    TestResult = "Token is required";
                    return;
                }
            }
            else if (string.IsNullOrWhiteSpace(EditingAccount.SSHKeyPath))
            {
                TestResult = "SSH key path is required";
                return;
            }

            EditingAccount.UpdatedAt = DateTime.Now;
            if (existing != null)
            {
                var previousAuthType = existing.AuthType;
                existing.Name = EditingAccount.Name;
                existing.Username = EditingAccount.Username;
                existing.Email = EditingAccount.Email;
                existing.Host = string.IsNullOrWhiteSpace(EditingAccount.Host) ? "github.com" : EditingAccount.Host.Trim();
                existing.AuthType = EditingAccount.AuthType;
                existing.SSHKeyPath = EditingAccount.SSHKeyPath;
                existing.AvatarUrl = EditingAccount.AvatarUrl;
                existing.UpdatedAt = EditingAccount.UpdatedAt;

                if (existing.AuthType == Models.GitHubAuthType.PersonalAccessToken && !string.IsNullOrWhiteSpace(NewToken))
                    existing.Token = NewToken;
                else if (previousAuthType == Models.GitHubAuthType.PersonalAccessToken && existing.AuthType != Models.GitHubAuthType.PersonalAccessToken)
                    existing.DeleteCredentials();

                store.AddOrUpdate(existing);
                if (EditingAccount.IsDefault)
                    store.SetDefault(existing);
            }
            else
            {
                EditingAccount.Host = string.IsNullOrWhiteSpace(EditingAccount.Host) ? "github.com" : EditingAccount.Host.Trim();
                if (EditingAccount.AuthType == Models.GitHubAuthType.PersonalAccessToken)
                    EditingAccount.Token = NewToken;
                store.AddOrUpdate(EditingAccount);
                if (EditingAccount.IsDefault)
                    store.SetDefault(EditingAccount);
            }

            RefreshAccounts();
            CancelEdit();
        }

        [RelayCommand]
        private void DeleteAccount(Models.GitHubAccount account)
        {
            if (account == null)
                return;

            Services.GitHubAccountStore.Instance.Remove(account);
            if (SelectedAccount?.Id == account.Id)
                SelectedAccount = null;
            RefreshAccounts();
        }

        [RelayCommand]
        private void SetAsDefault(Models.GitHubAccount account)
        {
            if (account == null)
                return;
            Services.GitHubAccountStore.Instance.SetDefault(account);
            RefreshAccounts(account.Id);
        }

        [RelayCommand]
        private async Task TestConnectionAsync()
        {
            if (EditingAccount == null || IsTesting)
                return;

            IsTesting = true;
            TestResult = "Testing...";
            try
            {
                switch (EditingAccount.AuthType)
                {
                    case Models.GitHubAuthType.SSHKey:
                        await TestSshConnectionAsync();
                        break;
                    case Models.GitHubAuthType.GitHubCli:
                        await TestGitHubCliConnectionAsync();
                        break;
                    default:
                        await TestTokenConnectionAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                TestResult = $"✗ Error: {ex.Message}";
            }
            finally
            {
                IsTesting = false;
            }
        }

        public void SetEditingSshKeyPath(string path)
        {
            if (EditingAccount == null)
                return;
            EditingAccount.SSHKeyPath = path ?? string.Empty;
            TestResult = string.Empty;
        }

        private void RefreshAccounts(Guid? selectId = null)
        {
            var selectedId = selectId ?? SelectedAccount?.Id;
            Accounts.Clear();
            Accounts.AddRange(Services.GitHubAccountStore.Instance.Accounts);
            SelectedAccount = selectedId.HasValue
                ? Services.GitHubAccountStore.Instance.Get(selectedId.Value)
                : null;
            OnPropertyChanged(nameof(ShowEmptyState));
        }

        private void NotifyEditorState()
        {
            OnPropertyChanged(nameof(EditingTitle));
            OnPropertyChanged(nameof(EditingAuthTypeIndex));
            OnPropertyChanged(nameof(IsEditingGitHubCli));
            OnPropertyChanged(nameof(IsEditingPat));
            OnPropertyChanged(nameof(IsEditingSsh));
            OnPropertyChanged(nameof(ShowEmptyState));
        }

        private async Task TestGitHubCliConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(EditingAccount.Username) || string.IsNullOrWhiteSpace(EditingAccount.Host))
            {
                TestResult = "Username and GitHub host are required";
                return;
            }

            var token = await Services.GitHubCliCredential.GetTokenAsync(
                EditingAccount.Host,
                EditingAccount.Username);
            await TestTokenAgainstGitHubAsync(token, EditingAccount.Host);
        }

        private async Task TestTokenConnectionAsync()
        {
            // Use the token currently being edited; fall back to the persisted token only
            // when the edit field is intentionally left blank.
            var existing = Services.GitHubAccountStore.Instance.Get(EditingAccount.Id);
            var token = string.IsNullOrWhiteSpace(NewToken) ? existing?.Token : NewToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                TestResult = "No token configured";
                return;
            }

            await TestTokenAgainstGitHubAsync(token, EditingAccount.Host);
        }

        private async Task TestTokenAgainstGitHubAsync(string token, string host)
        {
            var normalizedHost = string.IsNullOrWhiteSpace(host) ? "github.com" : host.Trim();
            var apiUrl = string.Equals(normalizedHost, "github.com", StringComparison.OrdinalIgnoreCase)
                ? "https://api.github.com/user"
                : $"https://{normalizedHost}/api/v3/user";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DevBoard");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                TestResult = $"✗ Failed: {(int)response.StatusCode} {response.ReasonPhrase}";
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            EditingAccount.Username = doc.RootElement.GetProperty("login").GetString() ?? EditingAccount.Username;
            EditingAccount.AvatarUrl = doc.RootElement.TryGetProperty("avatar_url", out var avatar)
                ? avatar.GetString() ?? string.Empty
                : string.Empty;
            TestResult = $"✓ Connected as @{EditingAccount.Username}";
        }

        private async Task TestSshConnectionAsync()
        {
            var keyPath = EditingAccount.SSHKeyPath;
            if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
            {
                TestResult = "SSH key file does not exist";
                return;
            }

            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "ssh",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            proc.StartInfo.ArgumentList.Add("-T");
            proc.StartInfo.ArgumentList.Add("-o");
            proc.StartInfo.ArgumentList.Add("BatchMode=yes");
            proc.StartInfo.ArgumentList.Add("-o");
            proc.StartInfo.ArgumentList.Add("StrictHostKeyChecking=accept-new");
            proc.StartInfo.ArgumentList.Add("-o");
            proc.StartInfo.ArgumentList.Add("IdentitiesOnly=yes");
            proc.StartInfo.ArgumentList.Add("-i");
            proc.StartInfo.ArgumentList.Add(keyPath);
            proc.StartInfo.ArgumentList.Add("git@github.com");

            proc.Start();
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            var exitTask = proc.WaitForExitAsync();
            if (await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(10))) != exitTask)
            {
                try { proc.Kill(true); } catch { }
                TestResult = "✗ SSH test timed out";
                return;
            }

            await exitTask;
            var output = ((await stdoutTask) + "\n" + (await stderrTask)).Trim();
            if (output.Contains("successfully authenticated", StringComparison.OrdinalIgnoreCase))
            {
                var hi = output.IndexOf("Hi ", StringComparison.OrdinalIgnoreCase);
                var bang = hi >= 0 ? output.IndexOf('!', hi) : -1;
                if (hi >= 0 && bang > hi + 3)
                    EditingAccount.Username = output.Substring(hi + 3, bang - hi - 3).Trim();
                TestResult = string.IsNullOrWhiteSpace(EditingAccount.Username)
                    ? "✓ SSH authentication succeeded"
                    : $"✓ Connected as @{EditingAccount.Username}";
            }
            else
            {
                TestResult = string.IsNullOrWhiteSpace(output)
                    ? $"✗ SSH authentication failed (exit {proc.ExitCode})"
                    : $"✗ {output.Replace('\n', ' ').Replace('\r', ' ')}";
            }
        }
    }
}
