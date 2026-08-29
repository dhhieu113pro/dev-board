using System.Text.Json.Nodes;
using SourceGit.DevSpaces.Roslyn;
using Xunit;

namespace SourceGit.Tests;

public sealed class RoslynSymbolSearchTests
{
    [Fact]
    public void Parse_extracts_symbol_location_from_mcp_text_content()
    {
        var payload = JsonNode.Parse("""
        {
          "content": [
            {
              "type": "text",
              "text": "[{\"name\":\"Launcher\",\"kind\":\"Class\",\"containerName\":\"SourceGit.Views\",\"filePath\":\"src/Views/Launcher.axaml.cs\",\"line\":121,\"column\":9}]"
            }
          ]
        }
        """)!;

        var result = Assert.Single(RoslynSymbolSearch.Parse(payload));

        Assert.Equal("Launcher", result.Name);
        Assert.Equal("Class", result.Kind);
        Assert.Equal("SourceGit.Views", result.ContainerName);
        Assert.Equal("src/Views/Launcher.axaml.cs", result.FilePath);
        Assert.Equal(121, result.Line);
        Assert.Equal(9, result.Column);
    }

    [Fact]
    public void Parse_accepts_direct_structured_content()
    {
        var payload = JsonNode.Parse("""
        {
          "structuredContent": {
            "results": [
              {
                "name": "OnKeyDown",
                "kind": "Method",
                "containingType": "Launcher",
                "file": "src/Views/Launcher.axaml.cs",
                "lineNumber": 120,
                "columnNumber": 33
              }
            ]
          }
        }
        """)!;

        var result = Assert.Single(RoslynSymbolSearch.Parse(payload));

        Assert.Equal("OnKeyDown", result.Name);
        Assert.Equal("Method", result.Kind);
        Assert.Equal("Launcher", result.ContainerName);
        Assert.Equal("src/Views/Launcher.axaml.cs", result.FilePath);
        Assert.Equal(120, result.Line);
        Assert.Equal(33, result.Column);
    }
}
