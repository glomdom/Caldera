using System.Xml.Linq;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class BaseTypeParser {
    public static List<VulkanBaseType> ParseFrom(XDocument doc, ParseContext ctx) {
        List<VulkanBaseType> baseTypes = [];

        var baseTypeNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "basetype")
            .Where(x => {
                var value = x.Element("name")?.Value;
                if (value is null) return false;

                return !ctx.BlockedTypes.Contains(value);
            })
            .ToList();

        foreach (var def in baseTypeNodes) {
            var rawMemberName = def.Element("name")?.Value;
            if (rawMemberName is null) continue;

            var memberName = NameCleaning.CleanEnumName(rawMemberName);

            var innerType = def.Element("type")?.Value;
            var opaque = false;
            var primitive = false;
            string type;

            var source = def.Value;
            if (innerType == "void" && source.Contains('*')) {
                type = "nint";
                opaque = true;
            } else if (innerType is not null) {
                type = Utilities.GetTypeFromXml(innerType);
                primitive = true;
            } else if (source.Contains("struct") || source.Contains("@class") || source.Contains("void*")) {
                type = "nint";
                opaque = true;
            } else {
                type = "nint";
                opaque = true;

                Log.Warning("Unable to parse type for {Name}, falling back to nint", memberName);
            }

            if (primitive) {
                ctx.BaseTypes[memberName] = type;
                Log.Debug("Populated lookup table with {Normal} -> {Lookup}", memberName, type);
            }

            baseTypes.Add(new VulkanBaseType(type, memberName, opaque, primitive));

            Log.Debug("Parsed base type {BaseTypeName} of type {Type}, primitive={Primitive} | opaque={Opaque}", memberName, type, primitive, opaque);
        }

        Log.Information("Parsed {Count} base types, of which {ToGenerateCount} will be generated", baseTypeNodes.Count, baseTypes.Count);

        return baseTypes;
    }
}