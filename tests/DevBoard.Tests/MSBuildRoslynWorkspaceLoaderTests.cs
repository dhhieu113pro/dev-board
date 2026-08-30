using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using DevBoard.DevSpaces;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class MSBuildRoslynWorkspaceLoaderTests
    {
        [Fact]
        public async Task LoadAsync_LoadsSdkStyleProject()
        {
            var root = Path.Combine(Path.GetTempPath(), $"devboard-roslyn-loader-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            try
            {
                var projectPath = Path.Combine(root, "LoaderSmoke.csproj");
                File.WriteAllText(
                    projectPath,
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup></Project>");
                File.WriteAllText(Path.Combine(root, "Program.cs"), "Console.WriteLine(\"ok\");");

                using var loaded = await new MSBuildRoslynWorkspaceLoader().LoadAsync(projectPath, CancellationToken.None);

                Assert.Equal(1, loaded.ProjectCount);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public async Task FindUnusedCodeAsync_ReturnsUnusedVariableWithLocation()
        {
            var root = Path.Combine(Path.GetTempPath(), $"devboard-roslyn-unused-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            try
            {
                var projectPath = Path.Combine(root, "UnusedSmoke.csproj");
                File.WriteAllText(
                    projectPath,
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Library</OutputType></PropertyGroup></Project>");
                var sourcePath = Path.Combine(root, "Example.cs");
                File.WriteAllText(sourcePath, "class Example { void Run() { int unused = 42; } }");

                using var loaded = await new MSBuildRoslynWorkspaceLoader().LoadAsync(projectPath, CancellationToken.None);
                var items = await loaded.FindUnusedCodeAsync(CancellationToken.None);

                var item = Assert.Single(items, x => x.Kind == RoslynUnusedCodeKind.Variable && x.Symbol == "unused");
                Assert.Contains(item.DiagnosticId, new[] { "CS0219", "IDE0059" });
                Assert.Equal(sourcePath, item.FilePath);
                Assert.Equal(1, item.Line);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
