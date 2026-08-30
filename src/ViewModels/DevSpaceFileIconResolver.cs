using System;
using System.IO;

namespace DevBoard.ViewModels
{
    public enum DevSpaceFileIconKind
    {
        File,
        Folder,
        RootFolder,
        WebRoot,
        Json,
        CSharp,
        CSharpProject,
        Solution,
        Config,
        Xml,
        Markdown,
        Image,
        JavaScript,
        TypeScript,
        Css,
    }

    public static class DevSpaceFileIconResolver
    {
        public static DevSpaceFileIconKind Resolve(string name, bool isDirectory, int depth)
        {
            if (isDirectory)
            {
                if (string.Equals(name, "wwwroot", StringComparison.OrdinalIgnoreCase))
                    return DevSpaceFileIconKind.WebRoot;

                return depth == 0
                    ? DevSpaceFileIconKind.RootFolder
                    : DevSpaceFileIconKind.Folder;
            }

            var extension = Path.GetExtension(name);
            if (string.IsNullOrEmpty(extension))
                return DevSpaceFileIconKind.File;

            return extension.ToLowerInvariant() switch
            {
                ".json" => DevSpaceFileIconKind.Json,
                ".cs" => DevSpaceFileIconKind.CSharp,
                ".csproj" => DevSpaceFileIconKind.CSharpProject,
                ".sln" or ".slnx" => DevSpaceFileIconKind.Solution,
                ".config" => DevSpaceFileIconKind.Config,
                ".xml" => DevSpaceFileIconKind.Xml,
                ".md" or ".markdown" => DevSpaceFileIconKind.Markdown,
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" => DevSpaceFileIconKind.Image,
                ".js" or ".jsx" or ".mjs" or ".cjs" => DevSpaceFileIconKind.JavaScript,
                ".ts" or ".tsx" or ".mts" or ".cts" => DevSpaceFileIconKind.TypeScript,
                ".css" or ".scss" or ".sass" or ".less" => DevSpaceFileIconKind.Css,
                _ => DevSpaceFileIconKind.File,
            };
        }
    }
}
