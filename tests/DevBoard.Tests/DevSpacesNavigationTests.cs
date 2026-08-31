using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

using DevBoard.DevSpaces;

using Xunit;

namespace DevBoard.Tests
{
    [Trait("Category", "UIIntegration")]
    public sealed class DevSpacesNavigationTests
    {
        [AvaloniaFact]
        public void RepositoryDevSpacesToolsKeepOriginalNativeNavigationItems()
        {
            var assembly = typeof(Views.Repository).Assembly;
            var integrationType = assembly.GetType("DevBoard.DevSpaces.DevSpacesBootstrap+RepositoryIntegration");
            Assert.NotNull(integrationType);

            var devSpacesFactory = integrationType.GetMethod("CreateNavigationItem", BindingFlags.Static | BindingFlags.NonPublic);
            var toolFactory = integrationType.GetMethod("CreateToolNavigationItem", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(devSpacesFactory);
            Assert.NotNull(toolFactory);

            var devArguments = new object[] { new Views.Repository(), null, null, null };
            var devItem = Assert.IsType<ListBoxItem>(devSpacesFactory.Invoke(null, devArguments));
            var filesItem = Assert.IsType<ListBoxItem>(toolFactory.Invoke(null, new object[]
            {
                new Views.Repository(), "Icons.Folder", App.Text("DevSpaces.Files"), "Files"
            }));
            var aiRouterItem = Assert.IsType<ListBoxItem>(toolFactory.Invoke(null, new object[]
            {
                new Views.Repository(), "Icons.AIAssist", "AI Router", "AIRouter"
            }));
            var roslynItem = Assert.IsType<ListBoxItem>(toolFactory.Invoke(null, new object[]
            {
                new Views.Repository(), "Icons.Code", "Roslyn", "Roslyn"
            }));
            var terminalsItem = Assert.IsType<ListBoxItem>(toolFactory.Invoke(null, new object[]
            {
                new Views.Repository(), "Icons.Terminal", App.Text("DevSpaces.Terminals"), "Terminals"
            }));

            Assert.Contains(App.Text("DevSpaces"), DescendantText(devItem));
            Assert.DoesNotContain("DEV", DescendantText(devItem));
            Assert.DoesNotContain(App.Text("DevSpaces.Dashboard"), DescendantText(devItem));
            Assert.Contains(App.Text("DevSpaces.Files"), DescendantText(filesItem));
            Assert.Contains("AI Router", DescendantText(aiRouterItem));
            Assert.Contains("Roslyn", DescendantText(roslynItem));
            Assert.DoesNotContain("C#", DescendantText(roslynItem));
            Assert.Contains(App.Text("DevSpaces.Terminals"), DescendantText(terminalsItem));

            Assert.Null(integrationType.GetMethod("ApplyNavigationVisualState", BindingFlags.Static | BindingFlags.NonPublic));
            Assert.Null(integrationType.GetMethod("AttachNavigationPointerState", BindingFlags.Static | BindingFlags.NonPublic));
            Assert.Null(integrationType.GetField("NavigationAccentColor", BindingFlags.Static | BindingFlags.NonPublic));
            Assert.Null(integrationType.GetField("NavigationActiveBackgroundColor", BindingFlags.Static | BindingFlags.NonPublic));

            Assert.Equal(3, GetConstant(integrationType, "DevSpacesNavigationIndex"));
            Assert.Equal(4, GetConstant(integrationType, "FilesNavigationIndex"));
            Assert.Equal(5, GetConstant(integrationType, "AIRouterNavigationIndex"));
            Assert.Equal(6, GetConstant(integrationType, "RoslynNavigationIndex"));
            Assert.Equal(7, GetConstant(integrationType, "TerminalsNavigationIndex"));
        }

        [AvaloniaFact]
        public void RepositoryNavigationAddsOnlyBuiltInAgentsAfterTerminals()
        {
            var assembly = typeof(Views.Repository).Assembly;
            var integrationType = assembly.GetType("DevBoard.DevSpaces.DevSpacesBootstrap+RepositoryIntegration");
            Assert.NotNull(integrationType);

            var factory = integrationType.GetMethod("CreateAgentNavigationItem", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(factory);

            var view = new Views.Repository();
            var items = DevBoard.DevSpaces.DevSpaceAgent.BuiltIn
                .Select(agent => Assert.IsType<ListBoxItem>(factory.Invoke(null, new object[] { view, agent })))
                .ToArray();

            Assert.Equal(new[] { "Copilot", "Codex", "Antigravity" },
                items.Select(item => DescendantText(item).Single(x => x is "Copilot" or "Codex" or "Antigravity")).ToArray());
            Assert.Equal(new object[] { "Agent:copilot", "Agent:codex", "Agent:agy" }, items.Select(item => item.Tag).ToArray());
            Assert.All(items, item =>
            {
                var grid = Assert.IsType<Grid>(item.Content);
                Assert.Single(grid.Children.OfType<Image>());
                Assert.DoesNotContain("DEV", DescendantText(item));
                Assert.DoesNotContain("AGENT", DescendantText(item));
            });

            Assert.Equal(8, GetConstant(integrationType, "CopilotNavigationIndex"));
            Assert.Equal(9, GetConstant(integrationType, "CodexNavigationIndex"));
            Assert.Equal(10, GetConstant(integrationType, "AntigravityNavigationIndex"));
        }

        [Fact]
        public void BuiltInAgentShortcutIsSingletonAndExcludedFromTerminalSessions()
        {
            var root = Path.Combine(Path.GetTempPath(), $"devboard-agent-singleton-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var copilot = DevSpaceAgent.BuiltIn.Single(x => x.Command == "copilot");

                var first = spaces.CreateAgentTerminalAt(-1, copilot);
                var second = spaces.CreateAgentTerminalAt(-1, copilot);

                Assert.Same(first, second);
                Assert.Equal("Copilot", first.Title);
                Assert.Equal("Copilot", spaces.ActivePage.ToString());
                Assert.Empty(spaces.Sessions);
                Assert.Equal(0, spaces.TerminalCount);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        [Fact]
        public void RegularTerminalCountIgnoresStandaloneAgents()
        {
            var root = Path.Combine(Path.GetTempPath(), $"devboard-agent-count-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var terminal = spaces.CreateTerminal();
                var codex = DevSpaceAgent.BuiltIn.Single(x => x.Command == "codex");
                var agent = spaces.CreateAgentTerminalAt(-1, codex);

                Assert.Single(spaces.Sessions);
                Assert.Same(terminal, spaces.Sessions[0]);
                Assert.DoesNotContain(agent, spaces.Sessions);
                Assert.Equal(1, spaces.TerminalCount);
                Assert.Equal("Codex", spaces.ActivePage.ToString());
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        [Fact]
        public void TerminalPickerNoLongerOwnsBuiltInAgentMenuItems()
        {
            var viewType = typeof(Views.DevSpaces);
            Assert.Null(viewType.GetMethod("CreateAgentMenuItem", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Fact]
        public void BuiltInAgentsRemainCopilotCodexAndAntigravity()
        {
            Assert.Equal(
                new[]
                {
                    ("Copilot", "copilot"),
                    ("Codex", "codex"),
                    ("Antigravity", "agy"),
                },
                DevBoard.DevSpaces.DevSpaceAgent.BuiltIn.Select(x => (x.Name, x.Command)).ToArray());
        }

        private static int GetConstant(System.Type integrationType, string name)
        {
            var field = integrationType.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return Assert.IsType<int>(field.GetRawConstantValue());
        }

        private static string[] DescendantText(ListBoxItem item)
        {
            var grid = Assert.IsType<Grid>(item.Content);
            return grid.Children
                .OfType<TextBlock>()
                .Concat(grid.GetVisualDescendants().OfType<TextBlock>())
                .Where(x => x != null)
                .Select(x => x.Text ?? string.Empty)
                .Distinct()
                .ToArray();
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
