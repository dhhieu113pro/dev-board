using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DevBoard.Services
{
    public sealed class GitHubAccountStore
    {
        public static GitHubAccountStore Instance { get; } = Load();

        public IReadOnlyList<Models.GitHubAccount> Accounts => _accounts;

        public Models.GitHubAccount Get(Guid id) => _accounts.FirstOrDefault(x => x.Id == id);

        public Models.GitHubAccount GetDefault()
            => _accounts.FirstOrDefault(x => x.IsDefault) ?? _accounts.FirstOrDefault();

        public void AddOrUpdate(Models.GitHubAccount account)
        {
            var existing = Get(account.Id);
            if (existing == null)
                _accounts.Add(account);
            else if (!ReferenceEquals(existing, account))
            {
                var index = _accounts.IndexOf(existing);
                _accounts[index] = account;
            }

            if (account.IsDefault || _accounts.Count == 1)
                SetDefault(account);
            else
                Save();
        }

        public void Remove(Models.GitHubAccount account)
        {
            if (account == null)
                return;
            account.DeleteCredentials();
            _accounts.RemoveAll(x => x.Id == account.Id);
            if (!_accounts.Any(x => x.IsDefault) && _accounts.Count > 0)
                _accounts[0].IsDefault = true;
            Save();
        }

        public void SetDefault(Models.GitHubAccount account)
        {
            foreach (var item in _accounts)
                item.IsDefault = item.Id == account.Id;
            Save();
        }

        public void Save()
        {
            try
            {
                var file = GetFilePath();
                var temp = file + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(_accounts, JsonCodeGen.Default.ListGitHubAccount));
                File.Move(temp, file, true);
            }
            catch
            {
            }
        }

        private static GitHubAccountStore Load()
        {
            var store = new GitHubAccountStore();
            try
            {
                var file = GetFilePath();
                if (!File.Exists(file))
                    return store;
                var accounts = JsonSerializer.Deserialize(File.ReadAllText(file), JsonCodeGen.Default.ListGitHubAccount);
                if (accounts != null)
                    store._accounts.AddRange(accounts);
            }
            catch
            {
            }
            return store;
        }

        private static string GetFilePath()
        {
            var root = Native.OS.DataDir;
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(Path.GetTempPath(), "DevBoard");
            Directory.CreateDirectory(root);
            return Path.Combine(root, "github-accounts.json");
        }

        private readonly List<Models.GitHubAccount> _accounts = [];
    }
}
