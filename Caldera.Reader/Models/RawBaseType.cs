namespace Caldera.Core.Models;

/// <summary>
/// Represents an unresolved base type
/// </summary>
/// <param name="Name">The name of the base type</param>
/// <param name="UnderlyingType">The raw text of the type tag</param>
/// <param name="RawText">The verbatim text of the type, without the type tag</param>
/// <param name="Span">The location where this was read from</param>
public sealed record RawBaseType(string Name, string? UnderlyingType, string RawText, string? TypeSuffix, SourceSpan Span) {
    public bool HasUnderlyingType => !string.IsNullOrWhiteSpace(UnderlyingType);
    public bool HasTypeSuffix => !string.IsNullOrWhiteSpace(TypeSuffix);
}