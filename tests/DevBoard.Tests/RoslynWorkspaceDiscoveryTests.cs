using System;
using System.IO;

using DevBoard.DevSpaces;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class RoslynWorkspaceDiscoveryTests
    {
        [Fact]
        public void FindWorkspace_PrefersRootSlnx()
        {
            using var dir = new TempDirectory();
            File.WriteAllText(Path.Combine(dir.Path, "z.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(dir.Path, "a.sln"), string.Empty);
            var slnx = Path.Combine(dir.Path, "workspace.slnx");
            File.WriteAllText(slnx, "<Solution />");

            Assert.Equal(slnx, RoslynWorkspaceDiscovery.FindWorkspace(dir.Path));
        }

        [Fact]
        public void FindWorkspace_PrefersAnyRootCandidateOverNestedCandidate()
        {
            using var dir = new TempDirectory();
            var rootProject = Path.Combine(dir.Path, "workspace.csproj");
            File.WriteAllText(rootProject, "<Project />");
            var nested = Directory.CreateDirectory(Path.Combine(dir.Path, "src")).FullName;
            File.WriteAllText(Path.Combine(nested, "workspace.slnx"), "<Solution />");

            Assert.Equal(rootProject, RoslynWorkspaceDiscovery.FindWorkspace(dir.Path));
        }

        [Fact]
        public void FindWorkspace_PrefersNestedSlnxWhenNoRootCandidateExists()
        {
            using var dir = new TempDirectory();
            var first = Directory.CreateDirectory(Path.Combine(dir.Path, "a")).FullName;
            var second = Directory.CreateDirectory(Path.Combine(dir.Path, "b")).FullName;
            File.WriteAllText(Path.Combine(first, "workspace.csproj"), "<Project />");
            var slnx = Path.Combine(second, "workspace.slnx");
            File.WriteAllText(slnx, "<Solution />");

            Assert.Equal(slnx, RoslynWorkspaceDiscovery.FindWorkspace(dir.Path));
        }

        [Theory]
        [InlineData(".git")]
        [InlineData("bin")]
        [InlineData("obj")]
        [InlineData("node_modules")]
        [InlineData(".vs")]
        [InlineData(".idea")]
        [InlineData(".vscode")]
        public void FindWorkspace_SkipsIgnoredDirectories(string ignoredDirectory)
        {
            using var dir = new TempDirectory();
            var ignored = Directory.CreateDirectory(Path.Combine(dir.Path, ignoredDirectory)).FullName;
            File.WriteAllText(Path.Combine(ignored, "ignored.slnx"), "<Solution />");
            var src = Directory.CreateDirectory(Path.Combine(dir.Path, "src")).FullName;
            var project = Path.Combine(src, "workspace.csproj");
            File.WriteAllText(project, "<Project />");

            Assert.Equal(project, RoslynWorkspaceDiscovery.FindWorkspace(dir.Path));
        }

        [Fact]
        public void FindWorkspace_UsesOrdinalPathOrderingForSameExtension()
        {
            using var dir = new TempDirectory();
            var firstDir = Directory.CreateDirectory(Path.Combine(dir.Path, "A")).FullName;
            var secondDir = Directory.CreateDirectory(Path.Combine(dir.Path, "b")).FullName;
            var first = Path.Combine(firstDir, "workspace.csproj");
            var second = Path.Combine(secondDir, "workspace.csproj");
            File.WriteAllText(second, "<Project />");
            File.WriteAllText(first, "<Project />");

            var expected = string.Compare(first, second, StringComparison.Ordinal) <= 0 ? first : second;
            Assert.Equal(expected, RoslynWorkspaceDiscovery.FindWorkspace(dir.Path));
        }

        [Fact]
        public void FindWorkspace_ReturnsNullWhenNoDotNetWorkspaceExists()
        {
            using var dir = new TempDirectory();
            File.WriteAllText(Path.Combine(dir.Path, "README.md"), "hello");

            Assert.Null(RoslynWorkspaceDiscovery.FindWorkspace(dir.Path));
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; }

            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"devboard-roslyn-discovery-{Guid.NewGuid():N}");
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
        }
    }
}
