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
        var paths = HuggingFaceModelDownloader.BuildDestinationPaths("/tmp/models", "model.gguf");

        Assert.EndsWith("model.gguf", paths.FinalPath, StringComparison.Ordinal);
        Assert.EndsWith("model.gguf.part", paths.PartPath, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatBytes_ProducesReadableValues()
    {
        Assert.Equal("0 B", HuggingFaceModelDownloader.FormatBytes(0));
        Assert.Equal("1.0 KB", HuggingFaceModelDownloader.FormatBytes(1024));
        Assert.Equal("1.0 MB", HuggingFaceModelDownloader.FormatBytes(1024 * 1024));
    }
}
