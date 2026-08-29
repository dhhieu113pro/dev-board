using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Mcp.Services
{
    public sealed record McpCommandResult(int ExitCode, string Stdout, string Stderr, long DurationMs, bool TimedOut, bool Truncated);

    public sealed class McpCommandService
    {
        public McpCommandService(int timeoutSeconds, int maxOutputBytes)
        {
            _timeoutSeconds = Math.Max(1, timeoutSeconds);
            _maxOutputBytes = Math.Max(1024, maxOutputBytes);
        }

        public async Task<McpCommandResult> RunAsync(string command, string workingDirectory, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command is required.", nameof(command));

            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (OperatingSystem.IsWindows())
            {
                startInfo.ArgumentList.Add("/d");
                startInfo.ArgumentList.Add("/s");
                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add(command);
            }
            else
            {
                startInfo.ArgumentList.Add("-lc");
                startInfo.ArgumentList.Add(command);
            }

            using var process = new Process { StartInfo = startInfo };
            var stopwatch = Stopwatch.StartNew();
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            var timedOut = false;
            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                TryKill(process);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            if (timedOut)
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            stopwatch.Stop();
            var truncated = false;
            stdout = TruncateUtf8(stdout, ref truncated);
            stderr = TruncateUtf8(stderr, ref truncated);
            return new McpCommandResult(timedOut ? -1 : process.ExitCode, stdout, stderr, stopwatch.ElapsedMilliseconds, timedOut, truncated);
        }

        private string TruncateUtf8(string value, ref bool truncated)
        {
            if (Encoding.UTF8.GetByteCount(value) <= _maxOutputBytes)
                return value;
            truncated = true;
            var bytes = Encoding.UTF8.GetBytes(value);
            return Encoding.UTF8.GetString(bytes, 0, _maxOutputBytes);
        }

        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
        }

        private readonly int _timeoutSeconds;
        private readonly int _maxOutputBytes;
    }
}
