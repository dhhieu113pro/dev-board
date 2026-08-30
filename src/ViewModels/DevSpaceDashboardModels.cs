using System;

namespace DevBoard.ViewModels
{
    public enum DevSpaceCapabilityState
    {
        Checking,
        Available,
        Unavailable,
        Failed,
    }

    public enum DevSpaceActivityKind
    {
        SessionStarted,
        SessionClosed,
        SessionExited,
        SessionFailed,
        FileOpened,
        AnalysisCompleted,
        DiagnosticsChanged,
    }

    public sealed record DevSpaceDashboardSessionRow(
        DevSpaceTerminal Terminal,
        string Title,
        DevSpaceTerminalState State,
        string WorkingDirectory)
    {
        public string Icon => ResolveProfileIcon(Title);
        public string DisplayTitle => StripProfileIcon(Title, Icon);
        public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);

        private static string ResolveProfileIcon(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            foreach (var profile in DevBoard.DevSpaces.DevSpaceProfileSettings.Instance.Profiles)
            {
                var icon = DevBoard.DevSpaces.DevSpaceProfileSettings.NormalizeProfileIcon(profile.Icon);
                var prefix = $"{icon} {profile.Name} ";
                if (title.StartsWith(prefix, StringComparison.Ordinal))
                    return icon;
            }

            return string.Empty;
        }

        private static string StripProfileIcon(string title, string icon)
        {
            if (string.IsNullOrWhiteSpace(icon))
                return title ?? string.Empty;

            var prefix = $"{icon} ";
            return title != null && title.StartsWith(prefix, StringComparison.Ordinal)
                ? title[prefix.Length..]
                : title ?? string.Empty;
        }
    }

    public sealed record DevSpaceGitSummary(
        int Total,
        int Added,
        int Modified,
        int Deleted,
        int Renamed,
        int Staged,
        int Unstaged)
    {
        public static DevSpaceGitSummary Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
    }

    public sealed record DevSpaceActivityEntry(
        DevSpaceActivityKind Kind,
        string Text,
        DateTimeOffset At);
}
