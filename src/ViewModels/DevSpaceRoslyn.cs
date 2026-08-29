using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Collections;

namespace SourceGit.ViewModels
{
    public enum DevSpaceRoslynState
    {
        Ready,
        Starting,
        Analyzing,
        Completed,
        Failed,
        Stopping,
    }

    public sealed class DevSpaceRoslyn : DevSpaceSession
    {
        public override DevSpaceSessionKind Kind => DevSpaceSessionKind.Roslyn;

        public AvaloniaList<string> WorkspaceCandidates { get; } = [];

        public string SelectedWorkspace
        {
            get => _selectedWorkspace;
            set => SetProperty(ref _selectedWorkspace, value);
        }

        public DevSpaceRoslynState State
        {
            get => _state;
            private set
            {
                if (SetProperty(ref _state, value))
                {
                    OnPropertyChanged(nameof(IsBusy));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public bool IsBusy => State is DevSpaceRoslynState.Starting or DevSpaceRoslynState.Analyzing;

        public string StatusText => State switch
        {
            DevSpaceRoslynState.Ready => "Ready",
            DevSpaceRoslynState.Starting => "Starting Roslyn MCP...",
            DevSpaceRoslynState.Analyzing => "Analyzing project...",
            DevSpaceRoslynState.Completed => "Analysis completed",
            DevSpaceRoslynState.Failed => "Analysis failed",
            DevSpaceRoslynState.Stopping => "Stopping",
            _ => State.ToString(),
        };

        public string Output
        {
            get => _output;
            private set => SetProperty(ref _output, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public DevSpaceRoslyn(string workingDirectory)
            : base("Roslyn Analysis")
        {
            _workingDirectory = workingDirectory;
            foreach (var candidate in SourceGit.DevSpaces.Roslyn.RoslynWorkspaceDiscovery.FindCandidates(workingDirectory))
                WorkspaceCandidates.Add(candidate);

            SelectedWorkspace = WorkspaceCandidates.FirstOrDefault() ?? string.Empty;
        }

        public async Task AnalyzeAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(SelectedWorkspace))
            {
                ErrorMessage = "No .slnx, .sln, or .csproj workspace was found.";
                State = DevSpaceRoslynState.Failed;
                return;
            }

            ErrorMessage = string.Empty;
            Output = string.Empty;

            try
            {
                _client ??= new SourceGit.DevSpaces.Roslyn.RoslynMcpClient(_workingDirectory);
                if (!_client.IsRunning)
                {
                    State = DevSpaceRoslynState.Starting;
                    await _client.StartAsync(cancellationToken).ConfigureAwait(false);
                }

                State = DevSpaceRoslynState.Analyzing;

                var diagnose = await _client.CallToolAsync(
                    "diagnose",
                    new JsonObject
                    {
                        ["path"] = SelectedWorkspace,
                    },
                    cancellationToken).ConfigureAwait(false);

                var diagnostics = await _client.CallToolAsync(
                    "get-diagnostics",
                    new JsonObject
                    {
                        ["path"] = SelectedWorkspace,
                        ["parameters"] = new JsonObject(),
                    },
                    cancellationToken).ConfigureAwait(false);

                Output = $"Environment\n{FormatToolResult(diagnose)}\n\nDiagnostics\n{FormatToolResult(diagnostics)}";
                State = DevSpaceRoslynState.Completed;
            }
            catch (OperationCanceledException)
            {
                State = DevSpaceRoslynState.Ready;
                throw;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                State = DevSpaceRoslynState.Failed;
            }
        }

        public override void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            State = DevSpaceRoslynState.Stopping;
            var client = _client;
            _client = null;
            if (client != null)
                _ = client.DisposeAsync();
        }

        private static string FormatToolResult(JsonNode result)
        {
            if (result["structuredContent"] is JsonNode structured)
                return structured.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            if (result["content"] is JsonArray content)
            {
                var texts = content
                    .OfType<JsonObject>()
                    .Select(x => x["text"]?.GetValue<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x));
                var text = string.Join(Environment.NewLine, texts!);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return result.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        private readonly string _workingDirectory;
        private SourceGit.DevSpaces.Roslyn.RoslynMcpClient _client;
        private string _selectedWorkspace = string.Empty;
        private DevSpaceRoslynState _state = DevSpaceRoslynState.Ready;
        private string _output = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _disposed;
    }
}
