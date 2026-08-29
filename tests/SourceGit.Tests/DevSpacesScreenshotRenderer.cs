using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SourceGit.Tests;

public static class DevSpacesScreenshotRenderer
{
    public static string Render(string scenarioId, Control content, int width = 1440, int height = 900)
    {
        var outputRoot = Environment.GetEnvironmentVariable("SOURCEGIT_SCREENSHOT_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputRoot))
            outputRoot = Path.Combine(AppContext.BaseDirectory, "artifacts", "devspaces-screenshots");

        Directory.CreateDirectory(outputRoot);
        var path = Path.Combine(outputRoot, scenarioId + ".png");

        var window = new Window
        {
            Width = width,
            Height = height,
            Content = content,
            Background = Brushes.Transparent,
            SystemDecorations = SystemDecorations.None,
        };

        window.Show();
        window.UpdateLayout();

        var pixelSize = new PixelSize(width, height);
        var dpi = new Vector(96, 96);
        using var bitmap = new RenderTargetBitmap(pixelSize, dpi);
        bitmap.Render(window);
        bitmap.Save(path);
        window.Close();

        return path;
    }
}
