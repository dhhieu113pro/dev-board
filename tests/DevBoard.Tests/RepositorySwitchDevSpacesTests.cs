using System;
using System.IO;

using DevBoard.DevSpaces;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class RepositorySwitchDevSpacesTests
    {
        [Fact]
        public void PreparingRepositorySwitchReturnsDevSpacesToDashboardWithoutStoppingTerminals()
        {
            var root = Path.Combine(Path.GetTempPath(), $"devboard-repo-switch-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            try
            {
                var repository = new ViewModels.Repository(false, root, root);
                var spaces = DevSpaceRegistry.GetOrCreate(repository);
                var terminal = spaces.CreateTerminal();

                Assert.True(spaces.IsTerminalsActive);
                Assert.Single(spaces.Sessions);

                DevSpaceRegistry.PrepareForRepositorySwitch(repository);

                Assert.True(spaces.IsDashboardActive);
                Assert.Single(spaces.Sessions);
                Assert.Same(terminal, spaces.Sessions[0]);
            }
            finally
            {
                try
                {
                    var repository = new ViewModels.Repository(false, root, root);
                    DevSpaceRegistry.Close(repository);
                }
                catch
                {
                }

                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
