using System;
using System.IO;
using SourceGit.Mcp.Services;
using Xunit;

namespace SourceGit.Tests;

public sealed class McpSkillServiceTests
{
    [Fact]
    public void Builtins_seed_disabled_and_persist_custom_skills()
    {
        var root = Directory.CreateTempSubdirectory("devboard-mcp-skills-");
        try
        {
            var store = new McpSkillStore(root.FullName);
            Assert.Contains(store.List(), x => x.Name == "superpowers" && x.BuiltIn && !x.Enabled);
            store.Create("custom", "---\nname: custom\ndescription: custom coding skill\n---\n# Custom\n");
            Assert.True(new McpSkillStore(root.FullName).Get("custom").Enabled);
            Assert.Throws<InvalidOperationException>(() => store.Delete("superpowers"));
        }
        finally { root.Delete(true); }
    }
}
