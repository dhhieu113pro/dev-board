using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Mcp.Services
{
    public sealed record McpRemoteSkillDocument(string SourceUrl, string ResolvedUrl, string Content, string Sha256);

    public sealed class McpRemoteSkillFetcher
    {
        public McpRemoteSkillFetcher(HttpClient client) => _client = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<McpRemoteSkillDocument> FetchAsync(string source, CancellationToken cancellationToken = default)
        {
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) throw new ArgumentException("A valid absolute HTTPS URL is required.", nameof(source));
            ValidateUri(uri);
            var original = uri.ToString();
            for (var redirects = 0; redirects <= 3; redirects++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    if (redirects == 3 || response.Headers.Location == null) throw new HttpRequestException("Too many or invalid redirects.");
                    uri = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(uri, response.Headers.Location);
                    ValidateUri(uri);
                    continue;
                }
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > 1_048_576) throw new InvalidDataException("Remote skill exceeds the maximum size.");
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var memory = new MemoryStream();
                var buffer = new byte[81920];
                while (true)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    if (memory.Length + read > 1_048_576) throw new InvalidDataException("Remote skill exceeds the maximum size.");
                    memory.Write(buffer, 0, read);
                }
                var bytes = memory.ToArray();
                var content = Encoding.UTF8.GetString(bytes);
                McpSkillStore.ReadFrontMatter(content);
                return new McpRemoteSkillDocument(original, uri.ToString(), content, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            }
            throw new HttpRequestException("Unable to fetch remote skill.");
        }

        public static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        }

        private static void ValidateUri(Uri uri)
        {
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(uri.UserInfo))
                throw new UnauthorizedAccessException("Remote skill URLs must use HTTPS and must not contain user-info.");
        }

        private readonly HttpClient _client;
    }
}
