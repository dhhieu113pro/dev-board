using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

using DevBoard.Mcp;
using Xunit;

namespace DevBoard.Tests;

public class DevBoardMcpPreferencesTests
{
    [Fact]
    public void Mcp_defaults_are_safe()
    {
        var settings = new DevBoardMcpSettings();

        Assert.False(settings.Enabled);
        Assert.Equal(DevBoardMcpOptions.DefaultPort, settings.Port);
        Assert.True(settings.ShareDevSpaceTerminalOutput);
        Assert.Equal(
            $"http://127.0.0.1:{DevBoardMcpOptions.DefaultPort}/sse",
            settings.Endpoint);
        Assert.Equal(string.Empty, settings.AuthToken);
        Assert.Equal("Stopped", settings.RuntimeStatus);
        Assert.Equal(string.Empty, settings.RuntimeError);
    }

    [Fact]
    public void Enabling_mcp_generates_auth_token_when_missing()
    {
        var settings = new DevBoardMcpSettings();

        settings.Enabled = true;

        Assert.True(settings.Enabled);
        Assert.False(string.IsNullOrWhiteSpace(settings.AuthToken));
        Assert.True(settings.AuthToken.Length >= 32);
    }

    [Fact]
    public void EnsureAuthToken_generates_once_without_rotating_existing_token()
    {
        var settings = new DevBoardMcpSettings();

        settings.EnsureAuthToken();
        var generated = settings.AuthToken;
        settings.EnsureAuthToken();

        Assert.False(string.IsNullOrWhiteSpace(generated));
        Assert.Equal(generated, settings.AuthToken);
    }

    [Fact]
    public void RegenerateAuthToken_replaces_existing_token()
    {
        var settings = new DevBoardMcpSettings();
        settings.Enabled = true;
        var first = settings.AuthToken;

        settings.RegenerateAuthToken();

        Assert.NotEqual(first, settings.AuthToken);
        Assert.True(settings.AuthToken.Length >= 32);
    }

    [Fact]
    public void McpClientConfiguration_contains_sse_endpoint_and_bearer_token()
    {
        var settings = new DevBoardMcpSettings
        {
            Port = 54321,
            AuthToken = "devboard-token",
        };

        using var document = JsonDocument.Parse(settings.McpClientConfiguration);
        var server = document.RootElement
            .GetProperty("mcpServers")
            .GetProperty("devboard");

        Assert.Equal("sse", server.GetProperty("type").GetString());
        Assert.Equal("http://127.0.0.1:54321/sse", server.GetProperty("url").GetString());
        Assert.Equal(
            "Bearer devboard-token",
            server.GetProperty("headers").GetProperty("Authorization").GetString());
    }

    [Fact]
    public void McpClientConfiguration_tracks_runtime_endpoint_and_token_changes()
    {
        var settings = new DevBoardMcpSettings
        {
            Port = 0,
            AuthToken = "first-token",
        };
        var changes = 0;
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DevBoardMcpSettings.McpClientConfiguration))
                changes++;
        };

        settings.UpdateRuntimeState(true, "http://127.0.0.1:54321/sse", string.Empty);
        settings.AuthToken = "second-token";

        Assert.Contains("http://127.0.0.1:54321/sse", settings.McpClientConfiguration);
        Assert.Contains("Bearer second-token", settings.McpClientConfiguration);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Enabling_mcp_refreshes_client_configuration_after_generating_token()
    {
        var settings = new DevBoardMcpSettings();
        var configurationChanged = false;
        settings.PropertyChanged += (_, e) =>
            configurationChanged |= e.PropertyName == nameof(DevBoardMcpSettings.McpClientConfiguration);

        settings.Enabled = true;

        Assert.True(configurationChanged);
        Assert.Contains($"Bearer {settings.AuthToken}", settings.McpClientConfiguration);
    }

    [Fact]
    public void Secure_settings_temp_file_is_owner_only_on_unix()
    {
        if (OperatingSystem.IsWindows())
            return;

        var path = Path.Combine(Path.GetTempPath(), $"devboard-mcp-{Guid.NewGuid():N}.tmp");
        try
        {
            var method = typeof(DevBoardMcpSettings).GetMethod(
                "CreateSecureSettingsFile",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            using var stream = Assert.IsType<FileStream>(method.Invoke(null, [path]));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Runtime_state_exposes_running_endpoint_and_startup_errors()
    {
        var settings = new DevBoardMcpSettings();

        settings.UpdateRuntimeState(true, "http://127.0.0.1:54321/sse", string.Empty);

        Assert.Equal("Running", settings.RuntimeStatus);
        Assert.Equal("http://127.0.0.1:54321/sse", settings.RuntimeEndpoint);
        Assert.Equal(string.Empty, settings.RuntimeError);

        settings.UpdateRuntimeState(false, string.Empty, "Address already in use");

        Assert.Equal("Error", settings.RuntimeStatus);
        Assert.Equal(string.Empty, settings.RuntimeEndpoint);
        Assert.Equal("Address already in use", settings.RuntimeError);
    }

    [Fact]
    public void DisplayEndpoint_uses_resolved_runtime_endpoint_while_running()
    {
        var settings = new DevBoardMcpSettings { Port = 0 };

        Assert.Equal("http://127.0.0.1:0/sse", settings.DisplayEndpoint);

        settings.UpdateRuntimeState(true, "http://127.0.0.1:54321/sse", string.Empty);

        Assert.Equal("http://127.0.0.1:54321/sse", settings.DisplayEndpoint);

        settings.UpdateRuntimeState(false, string.Empty, string.Empty);

        Assert.Equal("http://127.0.0.1:0/sse", settings.DisplayEndpoint);
    }

    [Theory]
    [InlineData(nameof(DevBoardMcpSettings.Enabled), true)]
    [InlineData(nameof(DevBoardMcpSettings.Port), true)]
    [InlineData(nameof(DevBoardMcpSettings.ShareDevSpaceTerminalOutput), true)]
    [InlineData(nameof(DevBoardMcpSettings.AuthToken), true)]
    [InlineData(nameof(DevBoardMcpSettings.Endpoint), false)]
    [InlineData("DisplayEndpoint", false)]
    [InlineData(nameof(DevBoardMcpSettings.McpClientConfiguration), false)]
    [InlineData(nameof(DevBoardMcpSettings.RuntimeStatus), false)]
    [InlineData(nameof(DevBoardMcpSettings.RuntimeEndpoint), false)]
    [InlineData(nameof(DevBoardMcpSettings.RuntimeError), false)]
    public void Service_only_reapplies_for_configuration_properties(string propertyName, bool expected)
    {
        Assert.Equal(expected, DevBoardMcpService.IsConfigurationProperty(propertyName));
    }

    [Fact]
    public void Service_maps_settings_to_host_options()
    {
        var settings = new DevBoardMcpSettings
        {
            Port = 54321,
            ShareDevSpaceTerminalOutput = false,
            AuthToken = "token",
        };

        var options = DevBoardMcpService.CreateOptions(settings);

        Assert.Equal(54321, options.Port);
        Assert.False(options.ShareDevSpaceTerminalOutput);
        Assert.Equal("token", options.AuthToken);
        Assert.Equal(DevBoardMcpOptions.DefaultMaxConcurrentToolCalls, options.MaxConcurrentToolCalls);
    }
}
