namespace SourceGit.Mcp
{
    public sealed class SourceGitMcpOptions
    {
        public const int DefaultPort = 53921;
        public const int DefaultMaxConcurrentToolCalls = 6;
        public const int DefaultCommandTimeoutSeconds = 30;
        public const int DefaultMaxCommandOutputBytes = 1_048_576;
        public const int DefaultMaxFileReadBytes = 10 * 1024 * 1024;
        public const int DefaultMaxSearchFileBytes = 2 * 1024 * 1024;
        public const int DefaultMaxSearchResults = 50;

        public int Port { get; set; } = DefaultPort;
        public bool ShareDevSpaceTerminalOutput { get; set; } = true;
        public string AuthToken { get; set; } = string.Empty;
        public int MaxConcurrentToolCalls { get; set; } = DefaultMaxConcurrentToolCalls;
    }
}
