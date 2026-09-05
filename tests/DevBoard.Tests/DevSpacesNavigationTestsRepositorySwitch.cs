using System;
using System.IO;

using Avalonia.Headless.XUnit;

using DevBoard.DevSpaces;

using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpacesNavigationTestsRepositorySwitch
{
    [AvaloniaFact]
    public void RepositorySwitchReturnsOutgoingDevSpaceToDashboardWithoutStoppingTerminal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devboard-repository-switch-{Guid.NewGuid():N}");
        var gitDir = Path.Combine(root, ".git");
        Directory.CreateDirectory(gitDir);

        var repository = new ViewModels.Repository(false, root, gitDir);
        try
        {
            var spaces = DevSpaceRegistry.GetOrCreate(repository);
            var terminal = spaces.CreateTerminal();
            spaces.ActivateTerminals();
            repository.SelectedViewIndex = 7;

            DevSpaceRegistry.PrepareForRepositorySwitch(repository);

            Assert.Equal(3, repository.SelectedViewIndex);
            Assert.Equal(Models.DevSpacePage.Dashboard, spaces.ActivePage);
            Assert.Single(spaces.Sessions);
            Assert.Same(terminal, spaces.Sessions[0]);
        }
        finally
        {
            DevSpaceRegistry.Close(repository);
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
