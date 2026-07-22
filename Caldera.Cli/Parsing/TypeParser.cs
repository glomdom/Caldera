using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class TypeParser {
    public static void ParseFrom(XDocument doc, ParseContext ctx) {
        var requiresTypeNodes = doc.Descendants("type")
            .Where(x => {
                var value = x.Attribute("requires")?.Value;

                return value != null && ctx.BlockedRequires.Contains(value);
            })
            .ToList();
        
        var namesTypeNodes = doc.Descendants("type")
            .Where(x => {
                var value = x.Attribute("name")?.Value;

                return value != null && ctx.BlockedNames.Contains(value);
            })
            .ToList();

        foreach (var name in requiresTypeNodes.Select(x => x.GetUncheckedAttributeValue("name"))) {
            ctx.BlockedTypes.Add(name);
            
            Log.Debug("Added blocked type {Name}", name);
        }
        
        foreach (var name in namesTypeNodes.Select(x => x.GetUncheckedAttributeValue("name"))) {
            ctx.BlockedTypes.Add(name);
            
            Log.Debug("Added blocked type {Name}", name);
        }
    }
}