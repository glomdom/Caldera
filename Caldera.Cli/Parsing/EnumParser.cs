using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class EnumParser {
    public static List<VulkanEnum> ParseFrom(XDocument doc) {
        List<VulkanEnum> enums = [];

        var enumNodes = doc.Descendants("enums")
            .Where(x => x.Attribute("name")?.Value != "API Constants" && x.Attribute("type")?.Value != "bitmask")
            .ToList();

        foreach (var enumNode in enumNodes) {
            var rawEnumName = enumNode.GetUncheckedAttributeValue("name");
            var cleanEnumName = NameCleaning.CleanEnumName(rawEnumName);
            var bitwidth = enumNode.MaybeGetAttributeValue("bitwidth");

            var underlyingType = bitwidth == "64" ? "long" : "int";

            List<VulkanEnumValue> values = [];

            foreach (var enumDef in enumNode.Elements("enum")) {
                var memberName = enumDef.GetAttributeValue("name");
                var cleanMemberName = NameCleaning.CleanEnumValue(memberName);

                var exactValue = enumDef.Attribute("value")?.Value;
                var alias = enumDef.Attribute("alias")?.Value;

                var finalValue = exactValue != null ? exactValue.Replace("ULL", "UL")
                    : alias != null ? NameCleaning.CleanEnumValue(alias)
                    : string.Empty;

                if (!string.IsNullOrEmpty(finalValue)) {
                    values.Add(new VulkanEnumValue(cleanMemberName, finalValue));
                }
            }

            enums.Add(new VulkanEnum(cleanEnumName, false, underlyingType, values));
            
            Log.Debug("Parsed enum {EnumName} with {ValueCount} values", cleanEnumName, values.Count);
        }

        Log.Information("Parsed {Count} enums, of which {ToGenerateCount} will be generated", enumNodes.Count, enums.Count);

        return enums;
    }
}