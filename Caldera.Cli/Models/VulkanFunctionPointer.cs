namespace Caldera.Cli.Models;

public sealed record VulkanFunctionPointer(VulkanType ReturnType, string Name, List<VulkanFunctionParameter> Parameters);