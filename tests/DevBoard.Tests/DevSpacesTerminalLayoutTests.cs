using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using DevBoard.DevSpaces;
using DevBoard.Models;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class DevSpacesTerminalLayoutTests
    {
        [Fact]
        public void AutoLayoutPlacesSessionsInGrid()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                spaces.Layout = DevSpaceLayout.Auto;
                spaces.CreateTerminal();
                spaces.CreateTerminal();
                spaces.CreateTerminal();

                Assert.Equal(2, spaces.GridRows);
                Assert.Equal(2, spaces.GridColumns);
                Assert.Equal(3, spaces.Sessions.Count);
                Assert.Equal(4, spaces.VisibleSlots.Count);
                Assert.NotNull(spaces.VisibleSlots[0].Terminal);
                Assert.NotNull(spaces.VisibleSlots[1].Terminal);
                Assert.NotNull(spaces.VisibleSlots[2].Terminal);
                Assert.Null(spaces.VisibleSlots[3].Terminal);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void SelectedGridLayoutKeepsSessions()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                spaces.Layout = DevSpaceLayout.TwoByTwo;
                var first = spaces.CreateTerminal();
                var second = spaces.CreateTerminal();

                Assert.Equal(DevSpaceLayout.TwoByTwo, spaces.Layout);
                Assert.Equal(2, spaces.GridRows);
                Assert.Equal(2, spaces.GridColumns);
                Assert.Equal(2, spaces.Sessions.Count);
                Assert.Same(first, spaces.Sessions[0]);
                Assert.Same(second, spaces.Sessions[1]);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void TabLayoutShowsOnlyActiveTerminal()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var first = spaces.CreateTerminal();
                var second = spaces.CreateTerminal();
                var third = spaces.CreateTerminal();

                spaces.Layout = DevSpaceLayout.Tab;
                spaces.ActivateTerminal(second);

                Assert.Equal(3, spaces.Sessions.Count);
                Assert.Equal(1, spaces.GridRows);
                Assert.Equal(1, spaces.GridColumns);
                Assert.Single(spaces.VisibleSlots);
                Assert.Same(second, spaces.VisibleSlots[0].Terminal);
                Assert.Contains(first, spaces.Sessions);
                Assert.Contains(third, spaces.Sessions);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void LayoutIndexPlacesTabBesideAuto()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());

                spaces.LayoutIndex = 0;
                Assert.Equal(DevSpaceLayout.Auto, spaces.Layout);
                spaces.LayoutIndex = 1;
                Assert.Equal(DevSpaceLayout.Tab, spaces.Layout);
                spaces.LayoutIndex = 2;
                Assert.Equal(DevSpaceLayout.OneByTwo, spaces.Layout);
                spaces.LayoutIndex = 3;
                Assert.Equal(DevSpaceLayout.TwoByTwo, spaces.Layout);
                spaces.LayoutIndex = 4;
                Assert.Equal(DevSpaceLayout.ThreeByThree, spaces.Layout);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void TabLayoutPreservesExistingSerializedLayoutValues()
        {
            Assert.Equal(0, (int)DevSpaceLayout.Auto);
            Assert.Equal(1, (int)DevSpaceLayout.OneByTwo);
            Assert.Equal(2, (int)DevSpaceLayout.TwoByTwo);
            Assert.Equal(3, (int)DevSpaceLayout.ThreeByThree);
            Assert.Equal(4, (int)DevSpaceLayout.FourByFour);
            Assert.Equal(5, (int)DevSpaceLayout.Tab);
        }

        [Fact]
        public void TerminalLayoutSelectorPlacesTabBesideAuto()
        {
            var root = LoadXaml("src/Views/DevSpaces.axaml");
            var comboBox = root
                .Descendants()
                .Single(node =>
                    node.Name.LocalName == "ComboBox"
                    && (string)node.Attribute("SelectedIndex") == "{Binding LayoutIndex, Mode=TwoWay}");
            var items = comboBox
                .Elements()
                .Where(node => node.Name.LocalName == "ComboBoxItem")
                .Select(node => (string)node.Attribute("Content"))
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "{DynamicResource Text.DevSpaces.Layout.Auto}",
                    "{DynamicResource Text.DevSpaces.Layout.Tab}",
                    "{DynamicResource Text.DevSpaces.Layout.1x2}",
                    "{DynamicResource Text.DevSpaces.Layout.2x2}",
                    "{DynamicResource Text.DevSpaces.Layout.3x3}",
                },
                items);
        }

        private static XElement LoadXaml(string relativePath)
        {
            using var stream = File.OpenRead(ResolveRepositoryFile(relativePath));
            return XDocument.Load(stream).Root
                ?? throw new InvalidOperationException($"Could not parse '{relativePath}'.");
        }

        private static string ResolveRepositoryFile(string relativePath)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                var candidate = Path.Combine(
                    current.FullName,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                    return candidate;

                current = current.Parent;
            }

            throw new FileNotFoundException(
                $"Could not locate repository file '{relativePath}' from '{AppContext.BaseDirectory}'.");
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"devboard-terminal-layout-{System.Guid.NewGuid():N}");
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
