using System;
using System.IO;

namespace SourceGit.Mcp.Services
{
    public sealed class McpSensitiveFileFilter
    {
        public bool IsBlocked(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(name))
                return false;

            foreach (var blocked in _exactNames)
            {
                if (name.Equals(blocked, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            foreach (var extension in _blockedExtensions)
            {
                if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static readonly string[] _exactNames =
        [
            ".env",
            ".env.local",
            ".env.production",
            ".env.development",
            "id_rsa",
            "id_rsa.pub",
            "id_ed25519",
            "id_ed25519.pub",
            "id_ecdsa",
            "id_ecdsa.pub",
            "credentials.json",
            "secrets.json",
            "appsettings.Production.json",
            ".npmrc",
            ".netrc",
            "authorized_keys",
            "known_hosts",
        ];

        private static readonly string[] _blockedExtensions =
        [
            ".pem",
            ".pfx",
            ".p12",
            ".key",
        ];
    }
}
