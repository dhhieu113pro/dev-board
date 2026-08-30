using DevBoard.AI.Hosting;
using Xunit;

namespace DevBoard.Tests;

public class AIRouterHostOptionsTests
{
    [Fact]
    public void Port_DefaultsTo11435()
    {
        var options = new AIRouterHostOptions();

        Assert.Equal(11435, options.Port);
    }

    [Fact]
    public void ListenUrl_UsesConfiguredPort()
    {
        var options = new AIRouterHostOptions { Port = 24680 };

        Assert.Equal("http://127.0.0.1:24680", options.ListenUrl);
    }

    [Fact]
    public void EndpointUrl_UsesConfiguredPort()
    {
        var options = new AIRouterHostOptions { Port = 24680 };

        Assert.Equal("http://127.0.0.1:24680/v1", options.EndpointUrl);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_RejectsPortOutsideTcpRange(int port)
    {
        var options = new AIRouterHostOptions { Port = port };

        Assert.Throws<System.InvalidOperationException>(() => options.Validate());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void Validate_AcceptsTcpPortRangeBoundaries(int port)
    {
        var options = new AIRouterHostOptions { Port = port };

        options.Validate();
    }

    [Fact]
    public void Validate_RejectsEmptyApiKey()
    {
        var options = new AIRouterHostOptions { ApiKey = "" };

        Assert.Throws<System.InvalidOperationException>(() => options.Validate());
    }
}
