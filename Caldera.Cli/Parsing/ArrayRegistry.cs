using Caldera.Cli.Models;

namespace Caldera.Cli.Parsing;

public sealed record ArrayType(VulkanType ElementType, int Count) {
    public string TypeName => $"{Sanitize(ElementType.Type)}Array{Count}";

    private static string Sanitize(string t) => t
        .Replace("*", "Ptr")
        .Replace(" ", "");
}

public sealed class ArrayRegistry {
    private readonly HashSet<ArrayType> _types = [];

    public ArrayType Add(VulkanType elementType, int count) {
        var at = new ArrayType(elementType, count);
        _types.Add(at);

        return at;
    }

    public IReadOnlyCollection<ArrayType> All => _types;
}