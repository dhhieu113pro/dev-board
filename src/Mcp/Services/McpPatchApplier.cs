using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Mcp.Services
{
    public static class McpPatchApplier
    {
        private static readonly Regex HunkHeader = new("^@@ -(?<old>\\d+)(?:,(?<oldCount>\\d+))? \\+(?<new>\\d+)(?:,(?<newCount>\\d+))? @@", RegexOptions.Compiled);

        public static string Apply(string original, string patch)
        {
            ArgumentNullException.ThrowIfNull(original);
            ArgumentNullException.ThrowIfNull(patch);

            var source = SplitLines(original);
            var output = new List<string>(source.Count);
            var patchLines = patch.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var sourceIndex = 0;
            var i = 0;

            while (i < patchLines.Length)
            {
                var line = patchLines[i];
                if (line.StartsWith("--- ", StringComparison.Ordinal) || line.StartsWith("+++ ", StringComparison.Ordinal) || string.IsNullOrEmpty(line))
                {
                    i++;
                    continue;
                }

                var match = HunkHeader.Match(line);
                if (!match.Success)
                    throw new InvalidOperationException($"Invalid unified diff line: {line}");

                var oldStart = int.Parse(match.Groups["old"].Value) - 1;
                if (oldStart < sourceIndex || oldStart > source.Count)
                    throw new InvalidOperationException("Patch hunk is outside the source file.");

                while (sourceIndex < oldStart)
                    output.Add(source[sourceIndex++]);

                i++;
                while (i < patchLines.Length && !patchLines[i].StartsWith("@@ ", StringComparison.Ordinal))
                {
                    var hunkLine = patchLines[i];
                    if (hunkLine == "\\ No newline at end of file" || (string.IsNullOrEmpty(hunkLine) && i == patchLines.Length - 1))
                    {
                        i++;
                        continue;
                    }
                    if (hunkLine.Length == 0)
                        throw new InvalidOperationException("Malformed unified diff hunk.");

                    var value = hunkLine.Substring(1);
                    switch (hunkLine[0])
                    {
                        case ' ':
                            EnsureSourceLine(source, sourceIndex, value);
                            output.Add(source[sourceIndex++]);
                            break;
                        case '-':
                            EnsureSourceLine(source, sourceIndex, value);
                            sourceIndex++;
                            break;
                        case '+':
                            output.Add(value);
                            break;
                        default:
                            throw new InvalidOperationException($"Invalid unified diff hunk line: {hunkLine}");
                    }
                    i++;
                }
            }

            while (sourceIndex < source.Count)
                output.Add(source[sourceIndex++]);

            var result = string.Join("\n", output);
            if (original.EndsWith("\n", StringComparison.Ordinal) && !result.EndsWith("\n", StringComparison.Ordinal))
                result += "\n";
            return result;
        }

        private static List<string> SplitLines(string value)
        {
            var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal);
            var lines = new List<string>(normalized.Split('\n'));
            if (normalized.EndsWith("\n", StringComparison.Ordinal) && lines.Count > 0)
                lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        private static void EnsureSourceLine(IReadOnlyList<string> source, int index, string expected)
        {
            if (index >= source.Count || !string.Equals(source[index], expected, StringComparison.Ordinal))
                throw new InvalidOperationException("Patch context does not match the source file.");
        }
    }
}
