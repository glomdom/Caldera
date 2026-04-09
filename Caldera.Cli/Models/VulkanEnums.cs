namespace Caldera.Cli.Models;

public sealed record VulkanEnumValue(string Name, string Value);
public sealed record VulkanEnum(string Name, bool IsBitmask, string UnderlyingType, List<VulkanEnumValue> Values);