using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.AI
{
    public sealed record HuggingFaceSource(
        string Owner,
        string Repository,
        bool IsDirectFile,
        string FileName,
        string DirectUrl);

    public sealed record HuggingFaceModelFile(string FileName, string DownloadUrl, long? Size);

    public sealed record HuggingFaceDestinationPaths(string FinalPath, string PartPath);

    public sealed class HuggingFaceDownloadState : ObservableObject
    {
        public HuggingFaceModelFile File
        {
            get => _file;
            internal set => SetProperty(ref _file, value);
        }

        public long BytesDownloaded
        {
            get => _bytesDownloaded;
            internal set
            {
                if (SetProperty(ref _bytesDownloaded, value))
                {
                    OnPropertyChanged(nameof(ProgressText));
                    OnPropertyChanged(nameof(Percent));
                }
            }
        }

        public long? TotalBytes
        {
            get => _totalBytes;
            internal set
            {
                if (SetProperty(ref _totalBytes, value))
                {
                    OnPropertyChanged(nameof(ProgressText));
                    OnPropertyChanged(nameof(Percent));
                }
            }
        }

        public double BytesPerSecond
        {
            get => _bytesPerSecond;
            internal set
            {
                if (SetProperty(ref _bytesPerSecond, value))
                {
                    OnPropertyChanged(nameof(SpeedText));
                    OnPropertyChanged(nameof(EtaText));
                }
            }
        }

        public string Status
        {
            get => _status;
            internal set => SetProperty(ref _status, value);
        }

        public string Error
        {
            get => _error;
            internal set => SetProperty(ref _error, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            internal set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    OnPropertyChanged(nameof(CanRetry));
                    OnPropertyChanged(nameof(CanCancel));
                }
            }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            internal set
            {
                if (SetProperty(ref _isCompleted, value))
                    OnPropertyChanged(nameof(CanRetry));
            }
        }

        public bool IsCanceled
        {
            get => _isCanceled;
            internal set
            {
                if (SetProperty(ref _isCanceled, value))
                    OnPropertyChanged(nameof(CanRetry));
            }
        }

        public double Percent => TotalBytes is > 0 ? Math.Clamp(BytesDownloaded * 100d / TotalBytes.Value, 0d, 100d) : 0d;
        public string ProgressText => TotalBytes is > 0
            ? $"{HuggingFaceModelDownloader.FormatBytes(BytesDownloaded)} / {HuggingFaceModelDownloader.FormatBytes(TotalBytes.Value)}"
            : HuggingFaceModelDownloader.FormatBytes(BytesDownloaded);
        public string SpeedText => BytesPerSecond > 0 ? $"{HuggingFaceModelDownloader.FormatBytes((long)BytesPerSecond)}/s" : string.Empty;
        public string EtaText
        {
            get
            {
                if (TotalBytes is not > 0 || BytesPerSecond <= 1 || BytesDownloaded >= TotalBytes.Value)
                    return string.Empty;

                var seconds = Math.Max(0, (TotalBytes.Value - BytesDownloaded) / BytesPerSecond);
                var eta = TimeSpan.FromSeconds(seconds);
                return eta.TotalHours >= 1 ? $"ETA {eta:hh\\:mm\\:ss}" : $"ETA {eta:mm\\:ss}";
            }
        }

        public bool CanCancel => IsRunning;
        public bool CanRetry => !IsRunning && !IsCompleted && File != null;

        internal CancellationTokenSource Cancellation { get; set; }

        private HuggingFaceModelFile _file;
        private long _bytesDownloaded;
        private long? _totalBytes;
        private double _bytesPerSecond;
        private string _status = string.Empty;
        private string _error = string.Empty;
        private bool _isRunning;
        private bool _isCompleted;
        private bool _isCanceled;
    }

    public sealed class HuggingFaceModelDownloader
    {
        public static HuggingFaceModelDownloader Instance { get; } = new(new HttpClient());

        internal HuggingFaceModelDownloader(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public static HuggingFaceSource ParseSource(string source)
        {
            source = source?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("Enter a Hugging Face repository or GGUF URL.", nameof(source));

            if (!source.Contains("://", StringComparison.Ordinal))
            {
                var simple = source.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (simple.Length != 2)
                    throw new ArgumentException("Use owner/repository or a huggingface.co URL.", nameof(source));
                return new HuggingFaceSource(simple[0], simple[1], false, string.Empty, string.Empty);
            }

            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !uri.Host.Equals("huggingface.co", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Only HTTPS huggingface.co URLs are supported.", nameof(source));

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString)
                .ToArray();
            if (segments.Length < 2)
                throw new ArgumentException("The Hugging Face URL must include owner and repository.", nameof(source));

            var owner = segments[0];
            var repository = segments[1];
            var resolveIndex = Array.FindIndex(segments, x => x.Equals("resolve", StringComparison.OrdinalIgnoreCase));
            if (resolveIndex >= 0)
            {
                if (segments.Length <= resolveIndex + 2)
                    throw new ArgumentException("The direct model URL is incomplete.", nameof(source));

                var fileSegments = segments.Skip(resolveIndex + 2).ToArray();
                var fileName = fileSegments[^1];
                if (!fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Direct Hugging Face URLs must point to a .gguf file.", nameof(source));

                return new HuggingFaceSource(owner, repository, true, fileName, uri.ToString());
            }

            return new HuggingFaceSource(owner, repository, false, string.Empty, string.Empty);
        }

        public async Task<IReadOnlyList<HuggingFaceModelFile>> ResolveFilesAsync(string source, CancellationToken cancellationToken = default)
        {
            var parsed = ParseSource(source);
            if (parsed.IsDirectFile)
                return [new HuggingFaceModelFile(parsed.FileName, parsed.DirectUrl, null)];

            var apiUrl = $"https://huggingface.co/api/models/{Uri.EscapeDataString(parsed.Owner)}/{Uri.EscapeDataString(parsed.Repository)}";
            using var response = await _httpClient.GetAsync(apiUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new InvalidOperationException("This Hugging Face model is gated or private. v1 supports public models only.");
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("siblings", out var siblings) || siblings.ValueKind != JsonValueKind.Array)
                return [];

            var files = new List<HuggingFaceModelFile>();
            foreach (var sibling in siblings.EnumerateArray())
            {
                if (!sibling.TryGetProperty("rfilename", out var nameProperty))
                    continue;

                var relativeName = nameProperty.GetString();
                if (string.IsNullOrWhiteSpace(relativeName) || !relativeName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                    continue;

                long? size = null;
                if (sibling.TryGetProperty("size", out var sizeProperty) && sizeProperty.TryGetInt64(out var parsedSize))
                    size = parsedSize;

                var escapedPath = string.Join('/', relativeName.Split('/').Select(Uri.EscapeDataString));
                var downloadUrl = $"https://huggingface.co/{Uri.EscapeDataString(parsed.Owner)}/{Uri.EscapeDataString(parsed.Repository)}/resolve/main/{escapedPath}?download=true";
                files.Add(new HuggingFaceModelFile(relativeName, downloadUrl, size));
            }

            return files.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public HuggingFaceDownloadState GetState(Service service)
        {
            if (service == null)
                return null;
            return _states.TryGetValue(service, out var state) ? state : null;
        }

        public HuggingFaceDownloadState StartDownload(Service service, HuggingFaceModelFile file)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(file);

            if (!service.IsLocalLlm)
                throw new InvalidOperationException("Hugging Face downloads can only be attached to a LocalLLM service.");

            if (!Uri.TryCreate(file.DownloadUrl, UriKind.Absolute, out var uri) ||
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !uri.Host.Equals("huggingface.co", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only HTTPS huggingface.co downloads are allowed.");

            var state = _states.GetOrAdd(service, _ => new HuggingFaceDownloadState());
            if (state.IsRunning)
                return state;

            state.File = file;
            state.Error = string.Empty;
            state.IsCompleted = false;
            state.IsCanceled = false;
            state.Status = "Starting download...";
            state.Cancellation?.Dispose();
            state.Cancellation = new CancellationTokenSource();
            _ = DownloadCoreAsync(service, state, state.Cancellation.Token);
            return state;
        }

        public HuggingFaceDownloadState Retry(Service service)
        {
            var state = GetState(service);
            if (state?.File == null || state.IsRunning)
                return state;
            return StartDownload(service, state.File);
        }

        public void Cancel(Service service)
        {
            GetState(service)?.Cancellation?.Cancel();
        }

        public static HuggingFaceDestinationPaths BuildDestinationPaths(string directory, string fileName)
        {
            var safeName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeName) || !safeName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A .gguf file name is required.", nameof(fileName));

            var finalPath = Path.Combine(directory, safeName);
            return new HuggingFaceDestinationPaths(finalPath, finalPath + ".part");
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024L * 1024L)
                return $"{bytes / 1024d:0.0} KB";
            if (bytes < 1024L * 1024L * 1024L)
                return $"{bytes / (1024d * 1024d):0.0} MB";
            return $"{bytes / (1024d * 1024d * 1024d):0.0} GB";
        }

        private async Task DownloadCoreAsync(Service service, HuggingFaceDownloadState state, CancellationToken cancellationToken)
        {
            var modelDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DevBoard",
                "models");
            Directory.CreateDirectory(modelDirectory);
            var paths = BuildDestinationPaths(modelDirectory, state.File.FileName);

            state.IsRunning = true;
            state.Status = "Downloading...";
            state.Error = string.Empty;

            try
            {
                var existingLength = File.Exists(paths.PartPath) ? new FileInfo(paths.PartPath).Length : 0L;
                using var request = new HttpRequestMessage(HttpMethod.Get, state.File.DownloadUrl);
                if (existingLength > 0)
                    request.Headers.Range = new RangeHeaderValue(existingLength, null);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new InvalidOperationException("This Hugging Face model is gated or private. v1 supports public models only.");
                response.EnsureSuccessStatusCode();

                var append = existingLength > 0 && response.StatusCode == HttpStatusCode.PartialContent;
                var startingLength = append ? existingLength : 0L;
                state.BytesDownloaded = startingLength;
                state.TotalBytes = response.Content.Headers.ContentLength is { } contentLength
                    ? startingLength + contentLength
                    : state.File.Size;

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(
                    paths.PartPath,
                    append ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    1024 * 128,
                    useAsync: true);

                var buffer = new byte[1024 * 128];
                var stopwatch = Stopwatch.StartNew();
                var sampleStarted = stopwatch.Elapsed;
                var sampleBytes = state.BytesDownloaded;

                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                        break;

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    state.BytesDownloaded += read;

                    var elapsed = stopwatch.Elapsed - sampleStarted;
                    if (elapsed.TotalMilliseconds >= 500)
                    {
                        state.BytesPerSecond = (state.BytesDownloaded - sampleBytes) / Math.Max(elapsed.TotalSeconds, 0.001);
                        sampleStarted = stopwatch.Elapsed;
                        sampleBytes = state.BytesDownloaded;
                    }
                }

                await output.FlushAsync(cancellationToken);
                if (File.Exists(paths.FinalPath))
                    File.Delete(paths.FinalPath);
                File.Move(paths.PartPath, paths.FinalPath);

                state.BytesPerSecond = 0;
                state.IsCompleted = true;
                state.Status = "Download complete";
                service.LocalModelPath = paths.FinalPath;
            }
            catch (OperationCanceledException)
            {
                state.IsCanceled = true;
                state.Status = "Download canceled — partial file kept for retry";
            }
            catch (Exception ex)
            {
                state.Error = ex.Message;
                state.Status = "Download failed";
            }
            finally
            {
                state.IsRunning = false;
            }
        }

        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<Service, HuggingFaceDownloadState> _states = new();
    }
}
