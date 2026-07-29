namespace Caldera.Core.Diagnostics;

public sealed class DiagnosticBag {
    private readonly List<Diagnostic> _diagnostics = [];

    public bool HasErrors => _diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error);

    public List<Diagnostic> GetOrdered() {
        return _diagnostics.OrderByDescending(diagnostic => diagnostic.Span.Line).ToList();
    }

    public List<string> Render() {
        return [.. GetOrdered().Select(x => $"{x.Severity} {x.Span} {x.Message}")];
    }

    public void Error(SourceSpan span, DiagnosticCode code, string message) {
        _diagnostics.Add(Diagnostic.FromSpan(span, DiagnosticSeverity.Error, code, message));
    }

    public void Warning(SourceSpan span, DiagnosticCode code, string message) {
        _diagnostics.Add(Diagnostic.FromSpan(span, DiagnosticSeverity.Warning, code, message));
    }

    public void Info(SourceSpan span, DiagnosticCode code, string message) {
        _diagnostics.Add(Diagnostic.FromSpan(span, DiagnosticSeverity.Info, code, message));
    }

    public void Trace(SourceSpan span, DiagnosticCode code, string message) {
        _diagnostics.Add(Diagnostic.FromSpan(span, DiagnosticSeverity.Trace, code, message));
    }

    public void Trace(string message) {
        Trace(SourceSpan.Unknown, DiagnosticCode.NonFatal, message);
    }
}