namespace Caldera.Cli.Models;

public sealed record VulkanFunctionPointer(VulkanType ReturnType, string Name, List<VulkanFunctionParameter> Parameters) {
    public override string ToString() => $"delegate* unmanaged[Cdecl]<{string.Join(", ", Parameters.Select(x => x.Type))}, {ReturnType}>";
}