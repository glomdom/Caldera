using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class UnionParser {
    public static List<VulkanUnion> ParseFrom(XDocument doc, ParseContext ctx) {
        List<VulkanUnion> unions = [];

        var unionNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "union")
            .ToList();

        foreach (var node in unionNodes) {
            var unionName = node.GetUncheckedAttributeValue("name").CleanName();

            Log.Warning("Parsed {Name}", unionName);

            List<VulkanUnionMember> members = [];
            foreach (var member in node.Elements("member")) {
                var memberName = member.GetElementValue("name").CleanName();
                var memberRawType = Utilities.GetTypeFromXml(member.GetElementValue("type").CleanName());

                var arrCount = Utilities.ResolveArrayLength(member, ctx);
                var memberType = new VulkanType(memberRawType, member.Value, ctx.BaseTypes).WithArray(arrCount);
                if (memberType.IsArray) {
                    var at = ctx.Arrays.Add(memberType, arrCount!.Value);
                        
                    members.Add(new VulkanUnionMember(new VulkanType(at.TypeName), memberName));
                } else {
                    members.Add(new VulkanUnionMember(memberType, memberName));
                }
            }

            var hasPointers = members.Any(x => x.Type.IsPointer);
            unions.Add(new VulkanUnion(unionName, members, hasPointers));
        }

        Log.Information("Parsed {Count} unions of which {ToGenerateCount} will be generated", unionNodes.Count, unions.Count);

        return unions;
    }
}