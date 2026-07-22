using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class BitmaskParser {
    public static List<VulkanEnum> ParseFrom(XDocument doc) {
        List<VulkanEnum> bitmasks = [];
        HashSet<string> generatedFlags = [];

        var bitmaskNodes = doc.Descendants("enums")
            .Where(x => x.Attribute("name")?.Value != "API Constants" && x.Attribute("type")?.Value == "bitmask")
            .ToList();

        var mapping = ParseBitmaskTypeMapping(doc);

        foreach (var bitmaskNode in bitmaskNodes) {
            var rawEnumName = bitmaskNode.GetUncheckedAttributeValue("name");
            var rawFlagsName = mapping.GetValueOrDefault(rawEnumName, rawEnumName);

            var cleanEnumName = NameCleaning.CleanEnumName(rawFlagsName);
            var bitwidth = bitmaskNode.MaybeGetAttributeValue("bitwidth");

            var underlyingType = bitwidth == "64" ? "ulong" : "uint";

            List<VulkanEnumValue> values = [];

            foreach (var enumDef in bitmaskNode.Elements("enum")) {
                var memberName = enumDef.GetAttributeValue("name");
                var cleanMemberName = NameCleaning.CleanEnumValue(memberName);

                var exactValue = enumDef.Attribute("value")?.Value;
                var bitpos = enumDef.Attribute("bitpos")?.Value;
                var alias = enumDef.Attribute("alias")?.Value;

                var finalValue = exactValue != null ? exactValue.Replace("ULL", "UL")
                    : bitpos != null ? $"(1U << {bitpos})"
                    : alias != null ? NameCleaning.CleanEnumValue(alias)
                    : string.Empty;

                if (!string.IsNullOrEmpty(finalValue)) {
                    values.Add(new VulkanEnumValue(cleanMemberName, finalValue));
                }
            }

            bitmasks.Add(new VulkanEnum(cleanEnumName, true, underlyingType, values));
            generatedFlags.Add(rawFlagsName);

            Log.Debug("Parsed bitmask {BitmaskName} of type {UnderlyingType} with {MemberCount} members", cleanEnumName, underlyingType, values.Count);
        }

        var typeNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "bitmask");

        foreach (var typeNode in typeNodes) {
            var rawFlagsName = typeNode.Element("name")?.Value;
            if (string.IsNullOrEmpty(rawFlagsName)) continue;
            if (generatedFlags.Contains(rawFlagsName)) continue;

            var cleanEnumName = NameCleaning.CleanEnumName(rawFlagsName);
            var innerType = typeNode.Element("type")?.Value;
            var underlyingType = innerType == "VkFlags64" ? "ulong" : "uint";

            List<VulkanEnumValue> emptyValues = [new("None", "0")];

            bitmasks.Add(new VulkanEnum(cleanEnumName, true, underlyingType, emptyValues));

            Log.Debug("Parsed empty bitmask {BitmaskName} of type {UnderlyingType}", cleanEnumName, underlyingType);
        }

        Log.Information("Parsed {Count} bitmasks total", bitmasks.Count);

        return bitmasks;
    }

    private static Dictionary<string, string> ParseBitmaskTypeMapping(XDocument doc) {
        return doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "bitmask")
            .Where(x => x.Attribute("requires") != null)
            .ToDictionary(
                x => x.Attribute("requires")!.Value,
                x => x.Element("name")!.Value
            );
    }
}