using System;
using System.IO;

using Avalonia.Headless.XUnit;

using DevBoard.DevSpaces;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class DevSpacesTerminalVisibilityTestsRepositorySwitch
    {
        [AvaloniaFact]
        public void PreparingRepositorySwitchReturnsDevSpacesToDashboardWithoutStoppingTerminals()
        {
            var root = Path.Combine(Path.GetTempPath(), $"devboard-repo-switch-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            ViewModels.Repository repository = null;

            try
            {
                repository = new ViewModels.Repository(false, root, root);
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
                if (repository != null)
                    DevSpaceRegistry.Close(repository);

                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
