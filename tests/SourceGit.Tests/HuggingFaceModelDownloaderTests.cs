using System;
using SourceGit.AI;
using Xunit;

namespace SourceGit.Tests;

public class HuggingFaceModelDownloaderTests
{
    [Theory]
    [InlineData("bartowski/Qwen2.5-Coder-7B-Instruct-GGUF", "bartowski", "Qwen2.5-Coder-7B-Instruct-GGUF")]
    [InlineData("https://huggingface.co/bartowski/Qwen2.5-Coder-7B-Instruct-GGUF", "bartowski", "Qwen2.5-Coder-7B-Instruct-GGUF")]
    [InlineData("https://huggingface.co/bartowski/Qwen2.5-Coder-7B-Instruct-GGUF/tree/main", "bartowski", "Qwen2.5-Coder-7B-Instruct-GGUF")]
    public void ParseSource_Repository_ReturnsRepository(string source, string owner, string repo)
    {
        var parsed = HuggingFaceModelDownloader.ParseSource(source);

        Assert.False(parsed.IsDirectFile);
        Assert.Equal(owner, parsed.Owner);
        Assert.Equal(repo, parsed.Repository);
    }

    [Fact]
    public void ParseSource_DirectResolveGguf_ReturnsFile()
    {
        var parsed = HuggingFaceModelDownloader.ParseSource(
            "https://huggingface.co/bartowski/Qwen2.5-Coder-7B-Instruct-GGUF/resolve/main/Qwen2.5-Coder-7B-Instruct-Q4_K_M.gguf?download=true");

        Assert.True(parsed.IsDirectFile);
        Assert.Equal("Qwen2.5-Coder-7B-Instruct-Q4_K_M.gguf", parsed.FileName);
        Assert.Equal("bartowski", parsed.Owner);
        Assert.Equal("Qwen2.5-Coder-7B-Instruct-GGUF", parsed.Repository);
    }

    [Theory]
    [InlineData("https://example.com/user/model")]
    [InlineData("http://huggingface.co/user/model")]
    [InlineData("https://huggingface.co/user/model/resolve/main/readme.md")]
    [InlineData("not-a-repo")]
    public void ParseSource_Invalid_Throws(string source)
    {
        Assert.Throws<ArgumentException>(() => HuggingFaceModelDownloader.ParseSource(source));
    }

    [Fact]
    public void BuildDestinationPaths_UsesPartSuffix()
    {
        var paths = HuggingFaceModelDownloader.BuildDestinationPaths(
            "/tmp/models",
            new HuggingFaceModelFile("model.gguf", "https://huggingface.co/owner/repo-a/resolve/main/model.gguf", null));

        Assert.True(paths.FinalPath.EndsWith("model.gguf", StringComparison.Ordinal));
        Assert.True(paths.PartPath.EndsWith("model.gguf.part", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildDestinationPaths_SameBasenameDifferentRepositories_AreIsolated()
    {
        var first = HuggingFaceModelDownloader.BuildDestinationPaths(
            "/tmp/models",
            new HuggingFaceModelFile("model.gguf", "https://huggingface.co/owner/repo-a/resolve/main/model.gguf", null));
        var second = HuggingFaceModelDownloader.BuildDestinationPaths(
            "/tmp/models",
            new HuggingFaceModelFile("model.gguf", "https://huggingface.co/owner/repo-b/resolve/main/model.gguf", null));

        Assert.NotEqual(first.FinalPath, second.FinalPath);
        Assert.NotEqual(first.PartPath, second.PartPath);
    }

    [Fact]
    public void BuildDestinationPaths_SameSource_ProducesStableResumePath()
    {
        var file = new HuggingFaceModelFile(
            "subdir/model.gguf",
            "https://huggingface.co/owner/repo-a/resolve/main/subdir/model.gguf?download=true",
            null);

        var first = HuggingFaceModelDownloader.BuildDestinationPaths("/tmp/models", file);
        var second = HuggingFaceModelDownloader.BuildDestinationPaths("/tmp/models", file);

        Assert.Equal(first.FinalPath, second.FinalPath);
        Assert.Equal(first.PartPath, second.PartPath);
    }

    [Fact]
    public void FormatBytes_ProducesReadableValues()
    {
        Assert.Equal("0 B", HuggingFaceModelDownloader.FormatBytes(0));
        Assert.Equal("1.0 KB", HuggingFaceModelDownloader.FormatBytes(1024));
        Assert.Equal("1.0 MB", HuggingFaceModelDownloader.FormatBytes(1024 * 1024));
    }
}