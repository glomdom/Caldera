namespace Caldera.Cli.Models;

public sealed record VulkanStruct(string Name, List<VulkanStructMember> Members, bool HasPointers);

public sealed record VulkanStructMember(VulkanType Type, string Name);