using System;
using System.IO;
using System.Threading.Tasks;

using DevBoard.DevSpaces;
using DevBoard.ViewModels;

using Xunit;

namespace DevBoard.Tests
{
    [Trait("Category", "UIIntegration")]
    public sealed class GoToFileSearchTests
    {
        [Fact]
        public async Task QuerySearchIsDebouncedForEightHundredMilliseconds()
        {
            var root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "alpha-target.txt"), "content");
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                await spaces.Files.InitialRefreshTask;
                using var search = new GoToFileSearch(root, spaces);

                search.Query = "alpha";
                await Task.Delay(400);

                Assert.Empty(search.Results);

                await Task.Delay(550);

                Assert.Contains(search.Results, result => result.RelativePath == "alpha-target.txt");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public async Task NewQueryRestartsDebounceWindow()
        {
            var root = CreateTempDirectory();
            try
            {
                File.WriteAllText(Path.Combine(root, "alpha-target.txt"), "content");
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                await spaces.Files.InitialRefreshTask;
                using var search = new GoToFileSearch(root, spaces);

                search.Query = "alp";
                await Task.Delay(500);
                search.Query = "alpha";
                await Task.Delay(400);

                Assert.Empty(search.Results);

                await Task.Delay(500);

                Assert.Contains(search.Results, result => result.RelativePath == "alpha-target.txt");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"devboard-file-search-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class FakeLauncher : IDevSpaceSessionLauncher
        {
            public DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null)
            {
                return new DevSpaceLaunchSpec(terminal ?? string.Empty, [], workingDirectory);
            }
        }
    }
}
