using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DevBoard.Services
{
    public sealed record GitHubCliAccount(string Host, string Username, bool IsActive);

    public static class GitHubCliCredential
    {
        public static ProcessStartInfo CreateTokenStartInfo(string host, string username)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("GitHub host is required.", nameof(host));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("GitHub username is required.", nameof(username));

            var start = CreateBaseStartInfo();
            start.ArgumentList.Add("auth");
            start.ArgumentList.Add("token");
            start.ArgumentList.Add("--hostname");
            start.ArgumentList.Add(host.Trim());
            start.ArgumentList.Add("--user");
            start.ArgumentList.Add(username.Trim());
            return start;
        }

        public static ProcessStartInfo CreateStatusStartInfo()
        {
            var start = CreateBaseStartInfo();
            start.ArgumentList.Add("auth");
            start.ArgumentList.Add("status");
            start.ArgumentList.Add("--json");
            start.ArgumentList.Add("hosts");
            return start;
        }

        public static IReadOnlyList<GitHubCliAccount> ParseAccounts(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("hosts", out var hosts) ||
                hosts.ValueKind != JsonValueKind.Object)
                return [];

            var accounts = new List<GitHubCliAccount>();
            foreach (var host in hosts.EnumerateObject())
            {
                if (host.Value.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var account in host.Value.EnumerateArray())
                {
                    if (!account.TryGetProperty("login", out var loginElement))
                        continue;

                    var login = loginElement.GetString();
                    if (string.IsNullOrWhiteSpace(login))
                        continue;

                    var active = account.TryGetProperty("active", out var activeElement) &&
                                 activeElement.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                                 activeElement.GetBoolean();
                    accounts.Add(new GitHubCliAccount(host.Name, login, active));
                }
            }

            return accounts;
        }

        public static async Task<IReadOnlyList<GitHubCliAccount>> DiscoverAccountsAsync(
            CancellationToken cancellationToken = default)
        {
            var result = await RunAsync(CreateStatusStartInfo(), cancellationToken).ConfigureAwait(false);
            return ParseAccounts(result);
        }

        public static Task<string> GetTokenAsync(
            string host,
            string username,
            CancellationToken cancellationToken = default)
            => RunAsync(CreateTokenStartInfo(host, username), cancellationToken);

        private static ProcessStartInfo CreateBaseStartInfo()
        {
            var start = new ProcessStartInfo
            {
                FileName = "gh",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // Environment tokens take precedence over the account store used by gh.
            // Remove them so the explicitly requested --user account is authoritative.
            start.Environment.Remove("GH_TOKEN");
            start.Environment.Remove("GITHUB_TOKEN");
            start.Environment.Remove("GH_ENTERPRISE_TOKEN");
            start.Environment.Remove("GITHUB_ENTERPRISE_TOKEN");
            return start;
        }

        private static async Task<string> RunAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken)
        {
            using var process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Unable to start GitHub CLI. Install gh and authenticate the selected account first.", ex);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stdout = (await stdoutTask.ConfigureAwait(false)).Trim();
            var stderr = (await stderrTask.ConfigureAwait(false)).Trim();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                var reason = string.IsNullOrWhiteSpace(stderr)
                    ? "GitHub CLI did not return a credential."
                    : stderr;
                throw new InvalidOperationException(reason);
            }

            return stdout;
        }
    }
}
