using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.DevSpaces.Roslyn
{
    public sealed record RoslynSymbolSearchResult(
        string Name,
        string Kind,
        string ContainerName,
        string FilePath,
        int Line,
        int Column);

    public static class RoslynSymbolSearch
    {
        public static IReadOnlyList<RoslynSymbolSearchResult> Parse(JsonNode result)
        {
            if (result == null)
                return [];

            foreach (var candidate in EnumeratePayloads(result))
            {
                var parsed = ParsePayload(candidate);
                if (parsed.Count > 0)
                    return parsed;
            }

            return [];
        }

        private static IEnumerable<JsonNode> EnumeratePayloads(JsonNode result)
        {
            if (result["structuredContent"] is JsonNode structured)
                yield return structured;

            if (result["content"] is JsonArray content)
            {
                foreach (var item in content.OfType<JsonObject>())
                {
                    var text = item["text"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    JsonNode parsed = null;
                    try
                    {
                        parsed = JsonNode.Parse(text);
                    }
                    catch
                    {
                        // Some MCP servers return human-readable text for empty/error results.
                    }

                    if (parsed != null)
                        yield return parsed;
                }
            }

            yield return result;
        }

        private static List<RoslynSymbolSearchResult> ParsePayload(JsonNode payload)
        {
            var array = payload as JsonArray
                ?? payload["results"] as JsonArray
                ?? payload["symbols"] as JsonArray
                ?? payload["items"] as JsonArray;

            if (array == null)
                return [];

            var results = new List<RoslynSymbolSearchResult>();
            foreach (var item in array.OfType<JsonObject>())
            {
                var name = ReadString(item, "name", "symbolName", "displayName");
                var file = ReadString(item, "filePath", "file", "path", "documentPath");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                    continue;

                results.Add(new RoslynSymbolSearchResult(
                    name,
                    ReadString(item, "kind", "symbolKind"),
                    ReadString(item, "containerName", "containingType", "container", "namespace"),
                    file,
                    ReadInt(item, "line", "lineNumber", "startLine"),
                    ReadInt(item, "column", "columnNumber", "startColumn")));
            }

            return results;
        }

        private static string ReadString(JsonObject item, params string[] names)
        {
            foreach (var name in names)
            {
                if (item[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return string.Empty;
        }

        private static int ReadInt(JsonObject item, params string[] names)
        {
            foreach (var name in names)
            {
                if (item[name] is JsonValue value && value.TryGetValue<int>(out var number))
                    return number;
            }

            return 0;
        }
    }

    public static class RoslynSymbolSearchSessions
    {
        public static async Task<IReadOnlyList<RoslynSymbolSearchResult>> SearchAsync(
            string workspaceRoot,
            string query,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot) || string.IsNullOrWhiteSpace(query))
                return [];

            var candidates = RoslynWorkspaceDiscovery.FindCandidates(workspaceRoot);
            if (candidates.Count == 0)
                throw new InvalidOperationException("No Roslyn workspace was found.");

            var session = _sessions.GetOrAdd(
                workspaceRoot,
                static root => new Session(root));

            return await session.SearchAsync(candidates[0], query, cancellationToken).ConfigureAwait(false);
        }

        public static void ShutdownAll()
        {
            foreach (var pair in _sessions)
                pair.Value.Dispose();
            _sessions.Clear();
        }

        private sealed class Session : IDisposable
        {
            public Session(string root)
            {
                _client = new RoslynMcpClient(root);
            }

            public async Task<IReadOnlyList<RoslynSymbolSearchResult>> SearchAsync(
                string workspace,
                string query,
                CancellationToken cancellationToken)
            {
                await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (!_diagnosed || !string.Equals(_workspace, workspace, StringComparison.Ordinal))
                    {
                        await _client.CallToolAsync(
                            "diagnose",
                            new JsonObject { ["path"] = workspace },
                            cancellationToken).ConfigureAwait(false);

                        _workspace = workspace;
                        _diagnosed = true;
                        _searchMode = SearchMode.Unknown;
                    }

                    var result = await CallSearchAsync(workspace, query, cancellationToken).ConfigureAwait(false);
                    return RoslynSymbolSearch.Parse(result);
                }
                finally
                {
                    _gate.Release();
                }
            }

            public void Dispose()
            {
                _client.Dispose();
                _gate.Dispose();
            }

            private async Task<JsonNode> CallSearchAsync(string workspace, string query, CancellationToken cancellationToken)
            {
                if (_searchMode == SearchMode.PathParameters)
                    return await CallPathParametersAsync("search-symbols", workspace, query, cancellationToken).ConfigureAwait(false);
                if (_searchMode == SearchMode.PublicSchema)
                    return await CallPublicSchemaAsync("search_symbols", workspace, query, cancellationToken).ConfigureAwait(false);

                try
                {
                    var result = await CallPathParametersAsync("search-symbols", workspace, query, cancellationToken).ConfigureAwait(false);
                    _searchMode = SearchMode.PathParameters;
                    return result;
                }
                catch (InvalidOperationException)
                {
                    var result = await CallPublicSchemaAsync("search_symbols", workspace, query, cancellationToken).ConfigureAwait(false);
                    _searchMode = SearchMode.PublicSchema;
                    return result;
                }
            }

            private Task<JsonNode> CallPathParametersAsync(string tool, string workspace, string query, CancellationToken cancellationToken)
            {
                return _client.CallToolAsync(
                    tool,
                    new JsonObject
                    {
                        ["path"] = workspace,
                        ["parameters"] = new JsonObject
                        {
                            ["query"] = query,
                            ["maxResults"] = 100,
                        },
                    },
                    cancellationToken);
            }

            private Task<JsonNode> CallPublicSchemaAsync(string tool, string workspace, string query, CancellationToken cancellationToken)
            {
                return _client.CallToolAsync(
                    tool,
                    new JsonObject
                    {
                        ["solutionPath"] = workspace,
                        ["query"] = query,
                        ["maxResults"] = 100,
                    },
                    cancellationToken);
            }

            private enum SearchMode
            {
                Unknown,
                PathParameters,
                PublicSchema,
            }

            private readonly RoslynMcpClient _client;
            private readonly SemaphoreSlim _gate = new(1, 1);
            private string _workspace = string.Empty;
            private bool _diagnosed;
            private SearchMode _searchMode;
        }

        private static readonly ConcurrentDictionary<string, Session> _sessions =
            new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }
}
