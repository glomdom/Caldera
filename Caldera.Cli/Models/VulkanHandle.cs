namespace Caldera.Cli.Models;
//     <type category="handle"                           objtypeenum="VK_OBJECT_TYPE_INSTANCE"><type>VK_DEFINE_HANDLE</type>(<name>VkInstance</name>)</type>
//     <type category="handle" parent="VkInstance"       objtypeenum="VK_OBJECT_TYPE_PHYSICAL_DEVICE"><type>VK_DEFINE_HANDLE</type>(<name>VkPhysicalDevice</name>)</type>
//     <type category="handle" parent="VkPhysicalDevice" objtypeenum="VK_OBJECT_TYPE_DEVICE"><type>VK_DEFINE_HANDLE</type>(<name>VkDevice</name>)</type>

public sealed record VulkanHandle(string Name, bool Dispatchable);