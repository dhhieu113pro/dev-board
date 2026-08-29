using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Mcp.Services
{
    public sealed record McpHistoryEntry(DateTimeOffset Timestamp, string Tool, bool Success, long DurationMs, JsonNode Arguments, string Error);

    public sealed class McpExecutionHistory
    {
        public McpExecutionHistory(string filePath, int maxArgumentLength, long maxFileBytes)
        {
            _filePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
            _maxArgumentLength = Math.Max(1, maxArgumentLength);
            _maxFileBytes = Math.Max(1024, maxFileBytes);
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        }

        public async Task RecordAsync(string tool, object arguments, bool success, long durationMs, string error = null)
        {
            var node = arguments == null ? null : JsonSerializer.SerializeToNode(arguments);
            var entry = new McpHistoryEntry(DateTimeOffset.UtcNow, tool ?? string.Empty, success, durationMs, Redact(node), Truncate(error));
            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                RotateIfNeeded(System.Text.Encoding.UTF8.GetByteCount(line));
                await File.AppendAllTextAsync(_filePath, line).ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }

        public object Query(int count = 100, string tool = null, bool? success = null)
        {
            var limit = Math.Clamp(count, 1, 500);
            if (!File.Exists(_filePath)) return new { count = 0, history_file = _filePath, entries = Array.Empty<McpHistoryEntry>() };
            var entries = new List<McpHistoryEntry>();
            foreach (var line in File.ReadLines(_filePath).Reverse())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                McpHistoryEntry entry;
                try { entry = JsonSerializer.Deserialize<McpHistoryEntry>(line); } catch { continue; }
                if (entry == null) continue;
                if (!string.IsNullOrWhiteSpace(tool) && !entry.Tool.Equals(tool, StringComparison.OrdinalIgnoreCase)) continue;
                if (success.HasValue && entry.Success != success.Value) continue;
                entries.Add(entry);
                if (entries.Count >= limit) break;
            }
            return new { count = entries.Count, history_file = _filePath, entries };
        }

        private JsonNode Redact(JsonNode node, string key = null)
        {
            if (node == null) return null;
            if (IsSensitiveKey(key)) return JsonValue.Create("[REDACTED]");
            if (node is JsonObject obj)
            {
                var clone = new JsonObject();
                foreach (var pair in obj) clone[pair.Key] = Redact(pair.Value, pair.Key);
                return clone;
            }
            if (node is JsonArray array)
            {
                var clone = new JsonArray();
                foreach (var item in array) clone.Add(Redact(item));
                return clone;
            }
            if (node is JsonValue value && value.TryGetValue<string>(out var text)) return JsonValue.Create(Truncate(text));
            return node.DeepClone();
        }

        private string Truncate(string value) => value == null || value.Length <= _maxArgumentLength ? value : value[.._maxArgumentLength] + "…";

        private static bool IsSensitiveKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            var normalized = key.Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
            return normalized.Contains("token", StringComparison.Ordinal) || normalized.Contains("password", StringComparison.Ordinal) || normalized.Contains("secret", StringComparison.Ordinal) || normalized.Contains("authorization", StringComparison.Ordinal) || normalized.Contains("api_key", StringComparison.Ordinal) || normalized.Contains("apikey", StringComparison.Ordinal) || normalized.Contains("base64_content", StringComparison.Ordinal) || normalized == "content";
        }

        private void RotateIfNeeded(int additionalBytes)
        {
            if (!File.Exists(_filePath) || new FileInfo(_filePath).Length + additionalBytes <= _maxFileBytes) return;
            var rotated = _filePath + ".1";
            if (File.Exists(rotated)) File.Delete(rotated);
            File.Move(_filePath, rotated);
        }

        private readonly string _filePath;
        private readonly int _maxArgumentLength;
        private readonly long _maxFileBytes;
        private readonly SemaphoreSlim _gate = new(1, 1);
    }
}
