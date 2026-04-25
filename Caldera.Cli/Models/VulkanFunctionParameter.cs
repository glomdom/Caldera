namespace Caldera.Cli.Models;

public sealed record VulkanFunctionParameter(VulkanType Type, string Name) {
    public override string ToString() => $"{Type} {Name}";
}