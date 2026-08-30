namespace DevBoard.DevSpaces
{
    public enum RoslynUnusedCodeKind
    {
        Member,
        Variable,
        Using,
    }

    public sealed class RoslynUnusedCodeItem
    {
        public string ProjectName { get; }
        public RoslynUnusedCodeKind Kind { get; }
        public string Symbol { get; }
        public string DiagnosticId { get; }
        public string Message { get; }
        public string FilePath { get; }
        public int Line { get; }
        public int Column { get; }

        public RoslynUnusedCodeItem(
            string projectName,
            RoslynUnusedCodeKind kind,
            string symbol,
            string diagnosticId,
            string message,
            string filePath,
            int line,
            int column)
        {
            ProjectName = projectName ?? string.Empty;
            Kind = kind;
            Symbol = symbol ?? string.Empty;
            DiagnosticId = diagnosticId ?? string.Empty;
            Message = message ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            Line = line;
            Column = column;
        }
    }
}
