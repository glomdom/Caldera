using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class AliasParser {
    public static void ParseFrom(XDocument doc, ParseContext ctx) {
        var enumAliases = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "enum")
            .Where(x => x.Attribute("name")?.Value is not null)
            .Where(x => x.Attribute("alias")?.Value is not null)
            .ToDictionary(
                x => x.Attribute("name")!.Value.CleanName(),
                x => x.Attribute("alias")!.Value.CleanName()
            );
        
        foreach (var (name, alias) in enumAliases) {
            ctx.Aliases[name] = alias;
            
            Log.Debug("Mapped {Name} to {Aliased}", name, alias);
        }
        
        Log.Information("Parsed {AliasCount} enum aliases", enumAliases.Count);
    }
}