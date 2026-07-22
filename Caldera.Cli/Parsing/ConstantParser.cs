using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class ConstantParser {
    public static List<VulkanConstant> ParseFrom(XDocument doc) {
        List<VulkanConstant> constants = [];

        var apiConstantsNode = doc.Descendants("enums")
            .First(x => x.Attribute("name")?.Value == "API Constants");

        foreach (var def in apiConstantsNode.Elements("enum")) {
            var memberName = def.GetAttributeValue("name");
            var memberType = Utilities.GetTypeFromXml(def.GetUncheckedAttributeValue("type"));
            var value = NameCleaning.NormalizeValue(def.GetUncheckedAttributeValue("value"));

            constants.Add(new VulkanConstant(NameCleaning.CleanEnumValue(memberName), memberType, value));
        }

        Log.Information("Parsed API constants");

        return constants;
    }
}