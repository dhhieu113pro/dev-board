using System;
using System.IO;

namespace DevBoard.ViewModels
{
    public static class DevSpaceCodeLanguageResolver
    {
        public static string ResolveGrammarExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var fileName = Path.GetFileName(path);
            if (fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase))
                return ".dockerfile";
            if (fileName.Equals("Makefile", StringComparison.OrdinalIgnoreCase))
                return ".makefile";
            if (fileName.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
                return ".xml";

            return Path.GetExtension(fileName).ToLowerInvariant();
        }
    }
}
