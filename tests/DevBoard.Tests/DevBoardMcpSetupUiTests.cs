using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using DevBoard.Mcp;
using Xunit;

namespace DevBoard.Tests;

[Trait("Category", "UIIntegration")]
public sealed class DevBoardMcpSetupUiTests
{
    [AvaloniaFact]
    public void Preferences_exposes_token_and_sse_setup_controls()
    {
        var view = new Views.DevSpacesPreferences();

        Assert.NotNull(view.FindControl<TextBox>("McpEndpointTextBox"));
        Assert.NotNull(view.FindControl<TextBox>("MaskedMcpTokenTextBox"));
        Assert.NotNull(view.FindControl<TextBox>("VisibleMcpTokenTextBox"));
        Assert.NotNull(view.FindControl<Button>("ToggleMcpTokenButton"));
        Assert.NotNull(view.FindControl<TextBox>("McpConfigurationTextBox"));
        Assert.NotNull(view.FindControl<TextBlock>("McpConnectionTestResult"));
        Assert.NotNull(view.FindControl<Button>("TestMcpConnectionButton"));

        var settings = Assert.IsType<DevBoardMcpSettings>(
            view.FindControl<Grid>("McpSettingsPanel")?.DataContext);
        Assert.False(string.IsNullOrWhiteSpace(settings.AuthToken));
        Assert.DoesNotContain("Bearer \"", settings.McpClientConfiguration);
    }
}
