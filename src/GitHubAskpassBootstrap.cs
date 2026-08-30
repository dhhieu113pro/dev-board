using System;
using System.Runtime.CompilerServices;

namespace DevBoard
{
    internal static class GitHubAskpassBootstrap
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            var token = Environment.GetEnvironmentVariable("SOURCEGIT_GITHUB_ASKPASS_TOKEN");
            if (string.IsNullOrEmpty(token))
                return;

            var args = Environment.GetCommandLineArgs();
            var prompt = args.Length > 1 ? args[1] : string.Empty;
            Console.Out.WriteLine(ResolveResponse(
                prompt,
                Environment.GetEnvironmentVariable("SOURCEGIT_GITHUB_ASKPASS_USERNAME"),
                token));
            Environment.Exit(0);
        }

        internal static string ResolveResponse(string prompt, string username, string token)
        {
            if (!string.IsNullOrEmpty(prompt) &&
                prompt.Contains("username", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrEmpty(username) ? "x-access-token" : username;

            return token ?? string.Empty;
        }
    }
}
