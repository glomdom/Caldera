namespace Caldera.Cli.Models;

public record VulkanRegistry(List<VulkanEnum> Enums, List<VulkanEnum> Bitmasks, List<VulkanConstant> Constants, List<VulkanBaseType> BaseTypes, List<VulkanHandle> Handles);