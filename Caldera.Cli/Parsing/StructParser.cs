using System.Text.RegularExpressions;
using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class StructParser {
    // TODO: parse optional properly, true/false does not change abi.
    //       optional="true,false" - pointer must be provided, elements can be null
    //       optional="false,true" - pointer can be null, if provided all elements must be valid

    /// <summary>vk.xml writes bitfields as "&lt;name&gt;mask&lt;/name&gt;:8".</summary>
    private static readonly Regex BitWidthRe = new(@"^\s*:\s*(\d+)", RegexOptions.Compiled);

    public static List<VulkanStruct> ParseFrom(XDocument doc, ParseContext ctx) {
        List<VulkanStruct> structs = [];

        var structNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "struct")
            .Where(x => x.Attribute("alias") is null) // alias nodes carry no members
            .ToList();

        foreach (var structNode in structNodes) {
            var name = structNode.GetUncheckedAttributeValue("name").CleanName();
            var drop = false;
            var blockedTypeName = string.Empty;

            List<VulkanStructMember> members = [];
            foreach (var member in structNode.Elements("member")) {
                var memberName = member.GetElementValue("name").CleanName();

                var memberApi = member.MaybeGetAttributeValue("api");
                if (memberApi is not null && !memberApi.Split(',').Contains("vulkan")) {
                    Log.Information("Skipping {MemberName} because it does not have vulkan api constraint", memberName);

                    continue;
                }

                var bitWidth = ParseBitWidth(member);

                var memberRawType = Utilities.GetTypeFromXml(member.GetElementValue("type"));
                if (ctx.BlockedTypes.Contains(memberRawType)) {
                    drop = true;
                    blockedTypeName = memberRawType;
                }

                var cleanedMemberType = memberRawType.CleanName().CleanFunctionPointerName();

                if (ctx.FunctionPointers.TryGetValue(cleanedMemberType, out var fp)) {
                    var memberType = new VulkanType(fp.Name, member.Value, ctx.FunctionPointers);
                    members.Add(new VulkanStructMember(memberType, memberName, bitWidth));
                } else {
                    if (ctx.Aliases.TryGetValue(cleanedMemberType, out var alias)) {
                        Log.Debug("Hit alias {Alias} for {Name}", alias, cleanedMemberType);

                        cleanedMemberType = alias;
                    }

                    var arrCount = Utilities.ResolveArrayLength(member, ctx);
                    var memberType = new VulkanType(cleanedMemberType, member.Value, ctx.BaseTypes).WithArray(arrCount);
                    if (memberType.IsArray) {
                        var at = ctx.Arrays.Add(memberType, arrCount!.Value);

                        members.Add(new VulkanStructMember(new VulkanType(at.TypeName), memberName, bitWidth));
                    } else {
                        members.Add(new VulkanStructMember(memberType, memberName, bitWidth));
                    }
                }
            }

            if (drop) {
                Log.Information("Dropping struct {Name} because it references blocked type {Type}", name, blockedTypeName);

                continue;
            }

            var bitfieldCount = members.Count(x => x.BitWidth is not null);
            if (bitfieldCount > 0) {
                Log.Debug("Struct {Name} has {Count} bitfield members", name, bitfieldCount);
            }

            var hasPointers = members.Any(x => x.Type.IsPointer);
            structs.Add(new VulkanStruct(name, members, hasPointers));
        }

        Log.Information("Parsed {Count} structs of which {ToGenerateCount} will be generated", structNodes.Count, structs.Count);

        Log.Debug("Have to generate {ArrayCount} inline arrays", ctx.Arrays.All.Count);

        foreach (var x in ctx.Arrays.All) {
            Log.Debug("Array of type {ElementType} and element count {Count} will be generated as {SanitizedName}", x.ElementType, x.Count, x.TypeName);
        }

        return structs;
    }

    /// <summary>
    /// The width lives in the text node following the name element, not in an attribute,
    /// so it has to be read off the sibling node rather than via GetElementValue.
    /// </summary>
    private static int? ParseBitWidth(XElement member) {
        if (member.Element("name")?.NextNode is not XText text) {
            return null;
        }

        var m = BitWidthRe.Match(text.Value);

        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }
}