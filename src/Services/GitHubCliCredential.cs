using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DevBoard.Services
{
    public static class GitHubCliCredential
    {
        public static ProcessStartInfo CreateTokenStartInfo(string host, string username)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("GitHub host is required.", nameof(host));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("GitHub username is required.", nameof(username));

            return new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = $"auth token --hostname {host.Trim()} --user {username.Trim()}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
        }

        public static async Task<string> GetTokenAsync(
            string host,
            string username,
            CancellationToken cancellationToken = default)
        {
            using var process = new Process
            {
                StartInfo = CreateTokenStartInfo(host, username),
            };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Unable to start GitHub CLI. Install gh and authenticate the selected GitHub account first.", ex);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stdout = (await stdoutTask.ConfigureAwait(false)).Trim();
            var stderr = (await stderrTask.ConfigureAwait(false)).Trim();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                var reason = string.IsNullOrWhiteSpace(stderr)
                    ? $"GitHub CLI has no credential for '{username}' on '{host}'."
                    : stderr;
                throw new InvalidOperationException(reason);
            }

            return stdout;
        }
    }
}
