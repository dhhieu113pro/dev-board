using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.DevSpaces.Roslyn
{
    public sealed class RoslynMcpClient : IDisposable, IAsyncDisposable
    {
        public const string Package = "RoslynMcp.Dnx@1.0.0";

        public string LastError { get; private set; } = string.Empty;

        public bool IsRunning => _process is { HasExited: false };

        public RoslynMcpClient(string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
                throw new ArgumentException("Roslyn MCP working directory must not be empty.", nameof(workingDirectory));

            _workingDirectory = workingDirectory;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRunning)
                return;

            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "dnx.exe" : "dnx",
                WorkingDirectory = _workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(Package);
            startInfo.ArgumentList.Add("--yes");

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };
            process.ErrorDataReceived += OnErrorDataReceived;

            try
            {
                if (!process.Start())
                    throw new InvalidOperationException("Failed to start Roslyn MCP.");
            }
            catch (Win32Exception ex)
            {
                process.Dispose();
                throw new InvalidOperationException(
                    "Roslyn Analysis requires the .NET 10 SDK with the dnx command available on PATH.",
                    ex);
            }
            catch
            {
                process.Dispose();
                throw;
            }

            _process = process;
            _input = process.StandardInput;
            _output = process.StandardOutput;
            process.BeginErrorReadLine();

            try
            {
                await InitializeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await DisposeProcessAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task<JsonNode> CallToolAsync(string toolName, JsonObject arguments, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentException("Roslyn MCP tool name must not be empty.", nameof(toolName));

            await StartAsync(cancellationToken).ConfigureAwait(false);

            var parameters = new JsonObject
            {
                ["name"] = toolName,
                ["arguments"] = arguments?.DeepClone() ?? new JsonObject(),
            };

            return await SendRequestAsync("tools/call", parameters, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            var process = DetachProcess();
            if (process != null)
            {
                process.ErrorDataReceived -= OnErrorDataReceived;
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch
                {
                    // Best-effort shutdown during synchronous disposal.
                }
                finally
                {
                    process.Dispose();
                }
            }

            _gate.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            await DisposeProcessAsync().ConfigureAwait(false);
            _gate.Dispose();
        }

        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var parameters = new JsonObject
            {
                ["protocolVersion"] = "2026-07-28",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "SourceGit",
                    ["version"] = "1.0",
                },
            };

            await SendRequestAsync("initialize", parameters, cancellationToken).ConfigureAwait(false);
            await SendNotificationAsync("notifications/initialized", new JsonObject(), cancellationToken).ConfigureAwait(false);
        }

        private async Task<JsonNode> SendRequestAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureProcess();
                var id = Interlocked.Increment(ref _nextRequestId);
                var request = new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["method"] = method,
                    ["params"] = parameters,
                };

                await _input!.WriteLineAsync(request.ToJsonString()).ConfigureAwait(false);
                await _input.FlushAsync(cancellationToken).ConfigureAwait(false);

                while (true)
                {
                    var line = await _output!.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line == null)
                        throw CreateExitedException();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    JsonNode? message;
                    try
                    {
                        message = JsonNode.Parse(line);
                    }
                    catch
                    {
                        continue;
                    }

                    if (message?["id"]?.GetValue<long>() != id)
                        continue;

                    if (message["error"] is JsonNode error)
                        throw new InvalidOperationException($"Roslyn MCP request '{method}' failed: {error.ToJsonString()}");

                    return message["result"]?.DeepClone() ?? new JsonObject();
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task SendNotificationAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureProcess();
                var request = new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["method"] = method,
                    ["params"] = parameters,
                };

                await _input!.WriteLineAsync(request.ToJsonString()).ConfigureAwait(false);
                await _input.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private void EnsureProcess()
        {
            if (_process == null || _process.HasExited || _input == null || _output == null)
                throw CreateExitedException();
        }

        private InvalidOperationException CreateExitedException()
        {
            var detail = string.IsNullOrWhiteSpace(LastError) ? string.Empty : $" {LastError}";
            return new InvalidOperationException($"Roslyn MCP is not running.{detail}".TrimEnd());
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                LastError = e.Data;
        }

        private Process? DetachProcess()
        {
            var process = _process;
            _process = null;
            _input = null;
            _output = null;
            return process;
        }

        private async Task DisposeProcessAsync()
        {
            var process = DetachProcess();
            if (process == null)
                return;

            process.ErrorDataReceived -= OnErrorDataReceived;

            try
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.StandardInput.Close();
                    }
                    catch
                    {
                        // Ignore shutdown races.
                    }

                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    try
                    {
                        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        process.Kill(true);
                    }
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        private readonly string _workingDirectory;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private Process? _process;
        private StreamWriter? _input;
        private StreamReader? _output;
        private long _nextRequestId;
        private bool _disposed;
    }
}
