using System;
using System.IO;
using System.Linq;

using DevBoard.DevSpaces;
using Xunit;

namespace DevBoard.Tests;

public sealed class DevSpaceProjectDiscoveryTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), $"devboard-project-discovery-{Guid.NewGuid():N}");

    public DevSpaceProjectDiscoveryTests()
    {
        Directory.CreateDirectory(_workspace);
    }

    [Fact]
    public void DiscoverFindsAspNetCoreAndWorkerProjectsAndSkipsIgnoredDirectories()
    {
        WriteFile("src/Time.Web/Time.Web.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        WriteFile("src/Time.Job/Time.Job.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Worker\"></Project>");
        WriteFile("src/Time.Core/Time.Core.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        WriteFile("node_modules/Ignored/Ignored.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        WriteFile("src/Time.Web/bin/Ignored.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");

        var profiles = DevSpaceProjectDiscovery.Discover(_workspace);

        Assert.Equal(new[] { "Time.Job", "Time.Web" }, profiles.Select(x => x.Name).OrderBy(x => x).ToArray());

        var web = profiles.Single(x => x.Name == "Time.Web");
        Assert.Equal(Path.Combine("src", "Time.Web"), web.Path);
        Assert.Equal("dotnet run --project \"Time.Web.csproj\"", web.Command);

        var worker = profiles.Single(x => x.Name == "Time.Job");
        Assert.Equal(Path.Combine("src", "Time.Job"), worker.Path);
        Assert.Equal("dotnet run --project \"Time.Job.csproj\"", worker.Command);
    }

    [Fact]
    public void DiscoverFindsAngularApplicationsAndUsesWorkspacePackageManager()
    {
        WriteFile("frontend/angular.json", """
            {
              "projects": {
                "uptime-ui": { "projectType": "application", "root": "projects/uptime-ui" },
                "shared": { "projectType": "library", "root": "projects/shared" },
                "customer-web": { "projectType": "application", "root": "projects/customer-web" }
              }
            }
            """);
        WriteFile("frontend/pnpm-lock.yaml", "lockfileVersion: '9.0'");

        var profiles = DevSpaceProjectDiscovery.Discover(_workspace);

        Assert.Equal(new[] { "customer-web", "uptime-ui" }, profiles.Select(x => x.Name).OrderBy(x => x).ToArray());
        Assert.All(profiles, profile => Assert.Equal("frontend", profile.Path));
        Assert.Equal("pnpm exec ng serve uptime-ui", profiles.Single(x => x.Name == "uptime-ui").Command);
        Assert.Equal("pnpm exec ng serve customer-web", profiles.Single(x => x.Name == "customer-web").Command);
    }

    [Fact]
    public void DiscoverFallsBackToNpmForAngularWorkspace()
    {
        WriteFile("ui/angular.json", """
            {
              "projects": {
                "portal": { "projectType": "application" }
              }
            }
            """);
        WriteFile("ui/package-lock.json", "{}");

        var profile = Assert.Single(DevSpaceProjectDiscovery.Discover(_workspace));

        Assert.Equal("portal", profile.Name);
        Assert.Equal("ui", profile.Path);
        Assert.Equal("npm exec -- ng serve portal", profile.Command);
    }

    [Fact]
    public void ManualProfilesOverrideDiscoveredProfilesWithTheSameName()
    {
        WriteFile("src/Time.Web/Time.Web.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        WriteFile("src/Other.Api/Other.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");

        var manual = new DevSpaceTerminalProfile
        {
            Id = "manual-time-web",
            Name = "Time.Web",
            Icon = "🦊",
            Path = "custom",
            Command = "custom-start",
        };

        var profiles = DevSpaceProjectDiscovery.Discover(_workspace, new[] { manual });

        Assert.Equal(2, profiles.Count);
        Assert.Same(manual, profiles.Single(x => x.Name == "Time.Web"));
        Assert.Contains(profiles, x => x.Name == "Other.Api");
    }

    [Fact]
    public void DiscoverIgnoresMalformedProjectFilesInsteadOfFailingTheWorkspaceScan()
    {
        WriteFile("broken/Broken.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"");
        WriteFile("good/Good.Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        WriteFile("bad-ui/angular.json", "{ not-json }");

        var profile = Assert.Single(DevSpaceProjectDiscovery.Discover(_workspace));

        Assert.Equal("Good.Api", profile.Name);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_workspace, recursive: true);
        }
        catch
        {
        }
    }

    private void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_workspace, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
