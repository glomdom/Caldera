using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class StructParser {
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

                var memberRawType = Utilities.GetTypeFromXml(member.GetElementValue("type"));
                if (ctx.BlockedTypes.Contains(memberRawType)) {
                    drop = true;
                    blockedTypeName = memberRawType;
                }
                
                var cleanedMemberType = memberRawType.CleanName().CleanFunctionPointerName();

                if (ctx.FunctionPointers.TryGetValue(cleanedMemberType, out var fp)) {
                    var memberType = new VulkanType(fp.Name, member.Value, ctx.FunctionPointers);
                    members.Add(new VulkanStructMember(memberType, memberName));
                } else {
                    if (ctx.Aliases.TryGetValue(cleanedMemberType, out var alias)) {
                        Log.Debug("Hit alias {Alias} for {Name}", alias, cleanedMemberType);
                        
                        cleanedMemberType = alias;
                    }
                    
                    var memberType = new VulkanType(cleanedMemberType, member.Value, ctx.BaseTypes);
                    members.Add(new VulkanStructMember(memberType, memberName));
                }
            }

            if (drop) {
                Log.Information("Dropping struct {Name} because it references blocked type {Type}", name, blockedTypeName);
                
                continue;
            }

            var hasPointers = members.Any(x => x.Type.IsPointer);
            structs.Add(new VulkanStruct(name, members, hasPointers));
        }

        Log.Information("Parsed {Count} structs of which {ToGenerateCount} will be generated", structNodes.Count, structs.Count);

        return structs;
    }
}