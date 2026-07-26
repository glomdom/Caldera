namespace Caldera.Core;

public readonly record struct SourceSpan(int Line, int Column) {
    public static readonly SourceSpan Unknown = new(-1, -1);

    public override string ToString() => Line == -1 ? "vk.xml" : $"vk.xml:{Line}:{Column}";
}