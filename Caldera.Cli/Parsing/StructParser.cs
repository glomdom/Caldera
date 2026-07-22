using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class StructParser {
    // <type category="struct" name="VkBaseOutStructure">
    // <member><type>VkStructureType</type> <name>sType</name></member>
    // <member optional="true">struct <type>VkBaseOutStructure</type>* <name>pNext</name></member>
    // </type>
    // <type category="struct" name="VkBaseInStructure">
    // <member><type>VkStructureType</type> <name>sType</name></member>
    // <member optional="true">const struct <type>VkBaseInStructure</type>* <name>pNext</name></member>
    // </type>
    // <type category="struct" name="VkOffset2D"> OK
    // <member><type>int32_t</type>        <name>x</name></member>
    // <member><type>int32_t</type>        <name>y</name></member>
    // </type>
    // <type category="struct" name="VkOffset3D">
    // <member><type>int32_t</type>        <name>x</name></member>
    // <member><type>int32_t</type>        <name>y</name></member>
    // <member><type>int32_t</type>        <name>z</name></member>
    // </type>

    // TODO: parse optional properly, true/false does not change abi.
    //       optional="true,false" - pointer must be provided, elements can be null
    //       optional="false,true" - pointer can be null, if provided all elements must be valid
    public static List<VulkanStruct> ParseFrom(XDocument doc, ParseContext ctx) {
        List<VulkanStruct> structs = [];

        var structNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "struct")
            .ToList();

        foreach (var structNode in structNodes) {
            var name = structNode.GetUncheckedAttributeValue("name").CleanName();

            List<VulkanStructMember> members = [];
            foreach (var member in structNode.Elements("member")) {
                var memberName = member.GetElementValue("name").CleanName();

                var memberApi = member.MaybeGetAttributeValue("api");
                if (memberApi is not null && !memberApi.Split(',').Contains("vulkan")) {
                    Log.Information("Skipping {MemberName} because it does not have vulkan api constraint", memberName);

                    continue;
                }

                var memberRawType = Utilities.GetTypeFromXml(member.GetElementValue("type").CleanName().CleanFunctionPointerName());
                if (memberRawType.EndsWith("FlagBits")) {
                    memberRawType = memberRawType[..^4] + "s";
                }

                if (ctx.FunctionPointers.TryGetValue(memberRawType, out var fp)) {
                    var memberType = new VulkanType(fp.Name, member.Value, ctx.FunctionPointers);
                    members.Add(new VulkanStructMember(memberType, memberName));
                } else {
                    var memberType = new VulkanType(memberRawType.CleanName(), member.Value, ctx.BaseTypes);
                    members.Add(new VulkanStructMember(memberType, memberName));
                }
            }

            var hasPointers = members.Any(x => x.Type.IsPointer);
            structs.Add(new VulkanStruct(name, members, hasPointers));
        }

        Log.Information("Parsed {Count} structs of which {ToGenerateCount} will be generated", structNodes.Count, structs.Count);

        return structs;
    }
}