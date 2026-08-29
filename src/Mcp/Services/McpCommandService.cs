using System;
using System.Diagnostics;
using System.IO;
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
            var stdoutTask = ReadBoundedAsync(process.StandardOutput, cancellationToken);
            var stderrTask = ReadBoundedAsync(process.StandardError, cancellationToken);
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
            return new McpCommandResult(
                timedOut ? -1 : process.ExitCode,
                stdout.Text,
                stderr.Text,
                stopwatch.ElapsedMilliseconds,
                timedOut,
                stdout.Truncated || stderr.Truncated);
        }

        private async Task<BoundedText> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();
            var buffer = new char[4096];
            var retainedBytes = 0;
            var truncated = false;

            while (true)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                if (truncated)
                    continue;

                var span = buffer.AsSpan(0, read);
                var chunkBytes = Encoding.UTF8.GetByteCount(span);
                if (retainedBytes + chunkBytes <= _maxOutputBytes)
                {
                    builder.Append(span);
                    retainedBytes += chunkBytes;
                    continue;
                }

                var remainingBytes = _maxOutputBytes - retainedBytes;
                if (remainingBytes > 0)
                {
                    var low = 0;
                    var high = span.Length;
                    while (low < high)
                    {
                        var mid = low + ((high - low + 1) / 2);
                        if (Encoding.UTF8.GetByteCount(span[..mid]) <= remainingBytes)
                            low = mid;
                        else
                            high = mid - 1;
                    }

                    if (low > 0)
                        builder.Append(span[..low]);
                }

                truncated = true;
            }

            return new BoundedText(builder.ToString(), truncated);
        }

        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
        }

        private sealed record BoundedText(string Text, bool Truncated);
        private readonly int _timeoutSeconds;
        private readonly int _maxOutputBytes;
    }
}
