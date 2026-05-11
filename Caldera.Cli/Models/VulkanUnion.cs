namespace Caldera.Cli.Models;

public sealed record VulkanUnion(string Name, List<VulkanUnionMember> Members, bool HasPointers);
public sealed record VulkanUnionMember(VulkanType Type, string Name);