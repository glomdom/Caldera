namespace Caldera.Core.Diagnostics;

public sealed record Diagnostic(DiagnosticSeverity Severity, SourceSpan Span, DiagnosticCode Code) {
    public static Diagnostic FromSpan(SourceSpan span, DiagnosticSeverity severity, DiagnosticCode code) => new(severity, span, code);
}