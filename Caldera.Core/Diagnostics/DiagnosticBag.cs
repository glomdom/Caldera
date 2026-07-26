namespace Caldera.Core.Diagnostics;

public sealed class DiagnosticBag {
    private readonly List<Diagnostic> _diagnostics = [];

    public bool HasErrors => _diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error);

    public List<Diagnostic> GetOrdered() {
        return _diagnostics.OrderByDescending(diagnostic => diagnostic.Span.Line).ToList();
    }

    public void Error(SourceSpan span, DiagnosticCode code, string message) {
        _diagnostics.Add(Diagnostic.FromSpan(span, DiagnosticSeverity.Error, code));
    }
}