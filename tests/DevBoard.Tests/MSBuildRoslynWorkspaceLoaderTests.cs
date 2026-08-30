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
    }
}
