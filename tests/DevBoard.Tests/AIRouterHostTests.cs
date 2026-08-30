using System.Linq;
using System.Threading.Tasks;

using DevBoard.AI.Hosting;
using DevBoard.AI.Routing;

using Microsoft.AspNetCore.Routing;

using Xunit;

namespace DevBoard.Tests;

public class AIRouterHostTests
{
    [Fact]
    public async Task Build_MapsBrowsableApiRoot()
    {
        await using var app = AIRouterHost.Build(
            new AIRouter([]),
            new AIRouterHostOptions
            {
                Port = 24680,
                ApiKey = "test-key",
            });

        var routes = app.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.Contains("/v1", routes);
    }
}
