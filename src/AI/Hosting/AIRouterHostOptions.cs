using System;

namespace DevBoard.AI.Hosting;

public sealed class AIRouterHostOptions
{
    public const int DefaultPort = 11435;

    public bool Enabled { get; set; } = true;
    public int Port { get; set; } = DefaultPort;
    public string ApiKey { get; set; } = "devboard-local";

    public string ListenUrl => $"http://127.0.0.1:{Port}";
    public string EndpointUrl => $"{ListenUrl}/v1";

    public void Validate()
    {
        if (Port is < 1 or > 65535)
            throw new InvalidOperationException("AI Router port must be between 1 and 65535.");

        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("AI Router API key must not be empty.");
    }
}
