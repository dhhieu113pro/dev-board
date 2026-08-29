using System;
using System.IO;

using SourceGit.Mcp.Services;
using Xunit;

namespace SourceGit.Tests;

public sealed class McpPathSandboxTests
{
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("../../outside.txt")]
    public void Resolve_rejects_traversal(string relativePath)
    {
        var root = CreateDirectory();
        try
        {
            var sandbox = new McpPathSandbox();
            Assert.Throws<UnauthorizedAccessException>(() => sandbox.Resolve(root, relativePath));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Resolve_rejects_rooted_paths()
    {
        var root = CreateDirectory();
        try
        {
            var sandbox = new McpPathSandbox();
            var rooted = Path.GetFullPath(Path.Combine(root, "file.txt"));

            Assert.Throws<UnauthorizedAccessException>(() => sandbox.Resolve(root, rooted));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Resolve_accepts_workspace_relative_path()
    {
        var root = CreateDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            var sandbox = new McpPathSandbox();

            var resolved = sandbox.Resolve(root, "src/file.cs");

            Assert.Equal(Path.GetFullPath(Path.Combine(root, "src", "file.cs")), resolved);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Resolve_rejects_symlink_that_escapes_workspace_when_supported()
    {
        var root = CreateDirectory();
        var outside = CreateDirectory();
        try
        {
            var link = Path.Combine(root, "outside-link");
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                return;
            }

            var sandbox = new McpPathSandbox();
            Assert.Throws<UnauthorizedAccessException>(() => sandbox.Resolve(root, "outside-link/secret.txt"));
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(outside);
        }
    }

    [Theory]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData(".env.production")]
    [InlineData(".env.development")]
    [InlineData("id_rsa")]
    [InlineData("id_rsa.pub")]
    [InlineData("id_ed25519")]
    [InlineData("id_ed25519.pub")]
    [InlineData("id_ecdsa")]
    [InlineData("id_ecdsa.pub")]
    [InlineData("client.pem")]
    [InlineData("client.pfx")]
    [InlineData("client.p12")]
    [InlineData("client.key")]
    [InlineData("credentials.json")]
    [InlineData("secrets.json")]
    [InlineData("appsettings.Production.json")]
    [InlineData(".npmrc")]
    [InlineData(".netrc")]
    [InlineData("authorized_keys")]
    [InlineData("known_hosts")]
    [InlineData("nested/CERT.PEM")]
    public void Sensitive_filter_blocks_secret_bearing_names(string path)
    {
        var filter = new McpSensitiveFileFilter();
        Assert.True(filter.IsBlocked(path));
    }

    [Theory]
    [InlineData("src/Program.cs")]
    [InlineData("README.md")]
    [InlineData("appsettings.json")]
    public void Sensitive_filter_allows_normal_files(string path)
    {
        var filter = new McpSensitiveFileFilter();
        Assert.False(filter.IsBlocked(path));
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
