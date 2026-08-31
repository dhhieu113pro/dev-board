using System;

using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace DevBoard.DevSpaces
{
    internal static class DevSpaceAgentIconProvider
    {
        internal static bool HasIcon(string command) => GetIconKey(command) != null;

        internal static IImage Get(string command)
        {
            return GetIconKey(command) switch
            {
                "copilot" => Copilot.Value,
                "codex" => Codex.Value,
                "agy" => Antigravity.Value,
                _ => null,
            };
        }

        private static string GetIconKey(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return null;

            var normalized = command.Trim();
            if (normalized.Equals("copilot", StringComparison.OrdinalIgnoreCase))
                return "copilot";
            if (normalized.Equals("codex", StringComparison.OrdinalIgnoreCase))
                return "codex";
            if (normalized.Equals("agy", StringComparison.OrdinalIgnoreCase))
                return "agy";
            return null;
        }

        private static IImage Load(string fileName)
        {
            var uri = new Uri($"avares://DevBoard/Resources/Images/ExternalToolIcons/{fileName}");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }

        private static readonly Lazy<IImage> Copilot = new(() => Load("githubcopilot.ico"));
        private static readonly Lazy<IImage> Codex = new(() => Load("codex.ico"));
        private static readonly Lazy<IImage> Antigravity = new(() => Load("antigravity.ico"));
    }
}
