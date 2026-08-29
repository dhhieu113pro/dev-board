using System;
using System.Collections.Generic;
using System.IO;

using SourceGit.Mcp.Services;
using Xunit;

namespace SourceGit.Tests;

public sealed class McpWorkspaceRegistryTests
{
    [Fact]
    public void Open_rejects_unknown_root()
    {
        var known = CreateDirectory();
        var unknown = CreateDirectory();
        try
        {
            var registry = new McpWorkspaceRegistry(() => new[] { known });

            Assert.Throws<UnauthorizedAccessException>(() => registry.Open(unknown));
        }
        finally
        {
            DeleteDirectory(known);
            DeleteDirectory(unknown);
        }
    }

    [Fact]
    public void Open_returns_deterministic_id_and_supports_id_lookup()
    {
        var root = CreateDirectory();
        try
        {
            var registry = new McpWorkspaceRegistry(() => new[] { root });

            var first = registry.Open(root);
            var second = registry.Open(root);
            var byId = registry.Get(first.Id);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(12, first.Id.Length);
            Assert.Equal(first.Root, byId.Root);
            Assert.Equal(Path.GetFullPath(root), first.Root);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void List_and_allowed_roots_follow_live_provider()
    {
        var rootA = CreateDirectory();
        var rootB = CreateDirectory();
        var roots = new List<string> { rootA, rootB };
        try
        {
            var registry = new McpWorkspaceRegistry(() => roots);
            var openedA = registry.Open(rootA);
            registry.Open(rootB);

            Assert.Equal(2, registry.List().Count);
            Assert.Equal(2, registry.GetAllowedRoots().Count);

            roots.Remove(rootA);

            Assert.Single(registry.List());
            Assert.Single(registry.GetAllowedRoots());
            Assert.Throws<KeyNotFoundException>(() => registry.Get(openedA.Id));
        }
        finally
        {
            DeleteDirectory(rootA);
            DeleteDirectory(rootB);
        }
    }

    [Fact]
    public void Open_requires_exact_known_root_not_descendant()
    {
        var root = CreateDirectory();
        var child = Directory.CreateDirectory(Path.Combine(root, "child")).FullName;
        try
        {
            var registry = new McpWorkspaceRegistry(() => new[] { root });

            Assert.Throws<UnauthorizedAccessException>(() => registry.Open(child));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateDirectory()
    {
        return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "devboard-mcp-tests", Guid.NewGuid().ToString("N"))).FullName;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }
}
