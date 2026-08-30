using DevBoard.ViewModels;
using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceFileIconResolverTests
{
    [Theory]
    [InlineData("appsettings.json", DevSpaceFileIconKind.Json)]
    [InlineData("APPSETTINGS.JSON", DevSpaceFileIconKind.Json)]
    [InlineData("Program.cs", DevSpaceFileIconKind.CSharp)]
    [InlineData("Bravo.WebApi.csproj", DevSpaceFileIconKind.CSharpProject)]
    [InlineData("DevBoard.sln", DevSpaceFileIconKind.Solution)]
    [InlineData("web.config", DevSpaceFileIconKind.Config)]
    [InlineData("data.xml", DevSpaceFileIconKind.Xml)]
    [InlineData("README.md", DevSpaceFileIconKind.Markdown)]
    [InlineData("logo.png", DevSpaceFileIconKind.Image)]
    [InlineData("app.js", DevSpaceFileIconKind.JavaScript)]
    [InlineData("app.ts", DevSpaceFileIconKind.TypeScript)]
    [InlineData("site.css", DevSpaceFileIconKind.Css)]
    public void ResolveMapsKnownFileTypesCaseInsensitively(string name, DevSpaceFileIconKind expected)
    {
        Assert.Equal(expected, DevSpaceFileIconResolver.Resolve(name, isDirectory: false, depth: 1));
    }

    [Fact]
    public void ResolveUsesWebIconForWwwroot()
    {
        Assert.Equal(DevSpaceFileIconKind.WebRoot, DevSpaceFileIconResolver.Resolve("wwwroot", isDirectory: true, depth: 1));
    }

    [Fact]
    public void ResolveUsesBlueFolderForRootAndYellowForNestedFolders()
    {
        Assert.Equal(DevSpaceFileIconKind.RootFolder, DevSpaceFileIconResolver.Resolve("Bravo.WebApi", isDirectory: true, depth: 0));
        Assert.Equal(DevSpaceFileIconKind.Folder, DevSpaceFileIconResolver.Resolve("Infrastructure", isDirectory: true, depth: 1));
    }

    [Fact]
    public void ResolveFallsBackToGenericFile()
    {
        Assert.Equal(DevSpaceFileIconKind.File, DevSpaceFileIconResolver.Resolve("notes.unknown", isDirectory: false, depth: 1));
    }
}
