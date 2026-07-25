using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class ConstantParser {
    public static List<VulkanConstant> ParseFrom(XDocument doc, ParseContext ctx) {
        List<VulkanConstant> constants = [];

        var apiConstantsNode = doc.Descendants("enums")
            .First(x => x.Attribute("name")?.Value == "API Constants");

        constants.AddRange(
            from def in apiConstantsNode.Elements("enum")
            let memberName = def.GetAttributeValue("name")
            let memberType = Utilities.GetTypeFromXml(def.GetUncheckedAttributeValue("type"))
            let value = NameCleaning.NormalizeValue(def.GetUncheckedAttributeValue("value"))
            select new VulkanConstant(NameCleaning.CleanEnumValue(memberName), memberType, value)
        );

        foreach (var x in constants) {
            Log.Debug("Registered constant {ConstantName} = {ConstantValue}", x.Name, x.Value);
            
            ctx.Constants[x.Name] = x;
        }

        Log.Information("Parsed API constants");

        return constants;
    }
}