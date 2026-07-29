namespace Caldera.Core.Diagnostics;

public sealed record Diagnostic(DiagnosticSeverity Severity, SourceSpan Span, DiagnosticCode Code, string Message) {
    public static Diagnostic FromSpan(SourceSpan span, DiagnosticSeverity severity, DiagnosticCode code, string message) => new(severity, span, code, message);
}