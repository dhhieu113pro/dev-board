using DevBoard.ViewModels;
using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceCodeLanguageResolverTests
{
    [Theory]
    [InlineData("Program.cs", ".cs")]
    [InlineData("Views/DevSpaceFiles.axaml", ".xml")]
    [InlineData("Views/DevSpaceFiles.axaml.cs", ".cs")]
    [InlineData("appsettings.json", ".json")]
    [InlineData("frontend/app.ts", ".ts")]
    [InlineData("README.md", ".md")]
    [InlineData("Dockerfile", ".dockerfile")]
    [InlineData("Makefile", ".makefile")]
    [InlineData("SCRIPT.PS1", ".ps1")]
    public void ResolveGrammarExtensionMapsKnownFiles(string path, string expected)
    {
        Assert.Equal(expected, DevSpaceCodeLanguageResolver.ResolveGrammarExtension(path));
    }

    [Fact]
    public void ResolveGrammarExtensionReturnsEmptyForExtensionlessUnknownFile()
    {
        Assert.Equal(string.Empty, DevSpaceCodeLanguageResolver.ResolveGrammarExtension("LICENSE"));
    }
}
