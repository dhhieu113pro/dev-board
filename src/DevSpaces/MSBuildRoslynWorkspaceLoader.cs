using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

namespace DevBoard.DevSpaces
{
    public sealed class MSBuildRoslynWorkspaceLoader : IRoslynWorkspaceLoader
    {
        public async Task<IRoslynLoadedWorkspace> LoadAsync(string workspacePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
                throw new ArgumentException("A Roslyn workspace path is required.", nameof(workspacePath));
            if (!File.Exists(workspacePath))
                throw new FileNotFoundException("The selected Roslyn workspace does not exist.", workspacePath);

            EnsureMSBuildRegistered();

            var workspace = MSBuildWorkspace.Create();
            try
            {
                Solution solution;
                var extension = Path.GetExtension(workspacePath);
                if (string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    var project = await workspace.OpenProjectAsync(workspacePath, cancellationToken: cancellationToken);
                    solution = project.Solution;
                }
                else if (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
                {
                    solution = await workspace.OpenSolutionAsync(workspacePath, cancellationToken: cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported Roslyn workspace type '{extension}'.");
                }

                return new LoadedWorkspace(workspace, solution);
            }
            catch (OperationCanceledException)
            {
                workspace.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                workspace.Dispose();
                throw new InvalidOperationException($"Roslyn could not load '{Path.GetFileName(workspacePath)}': {ex.Message}", ex);
            }
        }

        private static void EnsureMSBuildRegistered()
        {
            lock (RegistrationGate)
            {
                if (MSBuildLocator.IsRegistered)
                    return;

                var instance = MSBuildLocator.QueryVisualStudioInstances()
                    .OrderByDescending(x => x.Version)
                    .FirstOrDefault();
                if (instance == null)
                    throw new InvalidOperationException("No compatible .NET SDK/MSBuild installation was found.");

                MSBuildLocator.RegisterInstance(instance);
            }
        }

        public sealed class LoadedWorkspace : IRoslynLoadedWorkspace
        {
            public MSBuildWorkspace Workspace { get; }
            public Solution Solution { get; }
            public int ProjectCount => Solution.ProjectIds.Count;

            internal LoadedWorkspace(MSBuildWorkspace workspace, Solution solution)
            {
                Workspace = workspace;
                Solution = solution;
            }

            public async Task<IReadOnlyList<RoslynUnusedCodeItem>> FindUnusedCodeAsync(CancellationToken cancellationToken)
            {
                var results = new List<RoslynUnusedCodeItem>();
                foreach (var project in Solution.Projects.Where(x => x.Language == LanguageNames.CSharp))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var compilation = await project.GetCompilationAsync(cancellationToken);
                    if (compilation == null)
                        continue;

                    if (compilation is CSharpCompilation csharpCompilation)
                    {
                        var compilerOptions = csharpCompilation.Options.SpecificDiagnosticOptions
                            .SetItems(UnusedCompilerDiagnostics.Select(id =>
                                new KeyValuePair<string, ReportDiagnostic>(id, ReportDiagnostic.Warn)));
                        compilation = csharpCompilation.WithOptions(
                            csharpCompilation.Options
                                .WithGeneralDiagnosticOption(ReportDiagnostic.Default)
                                .WithWarningLevel(4)
                                .WithSpecificDiagnosticOptions(compilerOptions));
                    }

                    var diagnostics = new List<Diagnostic>(compilation.GetDiagnostics(cancellationToken));
                    var analyzers = project.AnalyzerReferences
                        .SelectMany(x => x.GetAnalyzers(project.Language))
                        .ToImmutableArray();
                    if (!analyzers.IsDefaultOrEmpty)
                    {
                        var analyzerDiagnostics = await compilation
                            .WithAnalyzers(analyzers, project.AnalyzerOptions)
                            .GetAnalyzerDiagnosticsAsync(cancellationToken);
                        diagnostics.AddRange(analyzerDiagnostics);
                    }

                    foreach (var diagnostic in diagnostics)
                    {
                        if (!TryClassify(diagnostic.Id, out var kind) || !diagnostic.Location.IsInSource)
                            continue;

                        var lineSpan = diagnostic.Location.GetLineSpan();
                        var sourceTree = diagnostic.Location.SourceTree;
                        var symbol = string.Empty;
                        if (sourceTree != null)
                        {
                            var root = await sourceTree.GetRootAsync(cancellationToken);
                            symbol = root.FindToken(diagnostic.Location.SourceSpan.Start).ValueText;
                        }

                        results.Add(new RoslynUnusedCodeItem(
                            project.Name,
                            kind,
                            symbol,
                            diagnostic.Id,
                            diagnostic.GetMessage(),
                            lineSpan.Path,
                            lineSpan.StartLinePosition.Line + 1,
                            lineSpan.StartLinePosition.Character + 1));
                    }

                    await AddUnusedPrivateFieldsAsync(project, compilation, results, cancellationToken);
                }

                return results
                    .OrderBy(x => x.ProjectName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Line)
                    .ThenBy(x => x.Column)
                    .ToArray();
            }

            public void Dispose() => Workspace.Dispose();

            private static async Task AddUnusedPrivateFieldsAsync(
                Project project,
                Compilation compilation,
                ICollection<RoslynUnusedCodeItem> results,
                CancellationToken cancellationToken)
            {
                var trees = compilation.SyntaxTrees.ToArray();
                foreach (var tree in trees)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var root = await tree.GetRootAsync(cancellationToken);
                    var semanticModel = compilation.GetSemanticModel(tree);

                    foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                    {
                        if (declarator.Parent?.Parent is not FieldDeclarationSyntax)
                            continue;
                        if (semanticModel.GetDeclaredSymbol(declarator, cancellationToken) is not IFieldSymbol field ||
                            field.DeclaredAccessibility != Accessibility.Private)
                            continue;

                        var isReferenced = false;
                        foreach (var candidateTree in trees)
                        {
                            var candidateRoot = await candidateTree.GetRootAsync(cancellationToken);
                            var candidateModel = compilation.GetSemanticModel(candidateTree);
                            foreach (var identifier in candidateRoot.DescendantNodes().OfType<IdentifierNameSyntax>())
                            {
                                if (!string.Equals(identifier.Identifier.ValueText, field.Name, StringComparison.Ordinal))
                                    continue;

                                var referencedSymbol = candidateModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
                                if (SymbolEqualityComparer.Default.Equals(referencedSymbol, field))
                                {
                                    isReferenced = true;
                                    break;
                                }
                            }

                            if (isReferenced)
                                break;
                        }

                        if (isReferenced)
                            continue;

                        var lineSpan = declarator.Identifier.GetLocation().GetLineSpan();
                        var filePath = lineSpan.Path;
                        if (results.Any(x =>
                                x.Kind == RoslynUnusedCodeKind.Member &&
                                string.Equals(x.Symbol, field.Name, StringComparison.Ordinal) &&
                                string.Equals(x.FilePath, filePath, StringComparison.OrdinalIgnoreCase) &&
                                x.Line == lineSpan.StartLinePosition.Line + 1))
                            continue;

                        var hasInitializer = declarator.Initializer != null;
                        results.Add(new RoslynUnusedCodeItem(
                            project.Name,
                            RoslynUnusedCodeKind.Member,
                            field.Name,
                            hasInitializer ? "CS0414" : "CS0169",
                            hasInitializer
                                ? $"The field '{field.Name}' is assigned but its value is never used"
                                : $"The field '{field.Name}' is never used",
                            filePath,
                            lineSpan.StartLinePosition.Line + 1,
                            lineSpan.StartLinePosition.Character + 1));
                    }
                }
            }

            private static bool TryClassify(string diagnosticId, out RoslynUnusedCodeKind kind)
            {
                switch (diagnosticId)
                {
                    case "IDE0005":
                        kind = RoslynUnusedCodeKind.Using;
                        return true;
                    case "IDE0051":
                    case "IDE0052":
                    case "CS0169":
                    case "CS0414":
                        kind = RoslynUnusedCodeKind.Member;
                        return true;
                    case "CS0168":
                    case "CS0219":
                    case "IDE0059":
                    case "IDE0060":
                        kind = RoslynUnusedCodeKind.Variable;
                        return true;
                    default:
                        kind = default;
                        return false;
                }
            }

            private static readonly ImmutableArray<string> UnusedCompilerDiagnostics =
                ImmutableArray.Create("CS0168", "CS0169", "CS0219", "CS0414");
        }

        private static readonly object RegistrationGate = new();
    }
}
