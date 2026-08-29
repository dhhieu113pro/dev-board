using System.Collections.Generic;

namespace SourceGit.Mcp.Services
{
    public sealed record McpBuiltInSkill(string Name, string SourceUrl, string License, string Content);

    public static class McpBuiltInSkillCatalog
    {
        public static IReadOnlyList<McpBuiltInSkill> All { get; } =
        [
            new("caveman", "https://github.com/JuliusBrussee/caveman", "MIT", "---\nname: caveman\ndescription: Ultra-compressed communication mode for coding work.\nlicense: MIT\n---\n\n# Caveman\n\nRespond tersely while preserving technical accuracy.\n"),
            new("hallmark", "https://github.com/Nutlope/hallmark", "MIT", "---\nname: hallmark\ndescription: Anti-AI-slop UI design discipline for pages and components.\nlicense: MIT\n---\n\n# Hallmark\n\nBuild deliberate, accessible, responsive interfaces that follow the existing product language.\n"),
            new("superpowers", "https://github.com/tpffounder/superpowers", "MIT", "---\nname: superpowers\ndescription: Disciplined software development workflow for planning, TDD, debugging, review, and verification.\nlicense: MIT\n---\n\n# Superpowers\n\nUnderstand, plan, test, implement, review, and verify before claiming completion.\n"),
            new("ponytail", "https://github.com/DietrichGebert/ponytail", "MIT", "---\nname: ponytail\ndescription: Anti-over-engineering coding discipline focused on the smallest correct solution.\nlicense: MIT\n---\n\n# Ponytail\n\nPrefer reuse, platform capabilities, and minimal code while preserving safety and correctness.\n"),
        ];
    }
}
