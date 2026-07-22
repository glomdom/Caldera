using System.Xml.Linq;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class HandleParser {
    public static List<VulkanHandle> ParseHandles(XDocument doc) {
        List<VulkanHandle> handles = [];

        var handleNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "handle")
            .Where(x => x.Attribute("alias")?.Value == null)
            .ToList();

        foreach (var def in handleNodes) {
            var rawName = def.Element("name")?.Value;
            if (rawName is null || string.IsNullOrEmpty(def.Value)) {
                Log.Warning("Unable to get 'name' from handle, is it an alias?");
                continue;
            }

            var name = NameCleaning.CleanEnumName(rawName);
            var type = def.Element("type")?.Value;
            if (type is null) {
                Log.Warning("Unable to get 'type' element inside handle '{Name}'", name);
            }

            var dispatchable = type != "VK_DEFINE_NON_DISPATCHABLE_HANDLE";

            handles.Add(new VulkanHandle(name, dispatchable));

            Log.Debug("Parsed handle {HandleName} of type {Type}, dispatchable={Dispatchable}", name, type, dispatchable);
        }

        Log.Information("Parsed {Count} handles, of which {ToGenerateCount} will be generated", handleNodes.Count, handles.Count);

        return handles;
    }
}