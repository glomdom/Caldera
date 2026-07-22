using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli.Parsing;

public static class FunctionPointerParser {
    public static void ParseFrom(XDocument doc, ParseContext ctx) {
        var funcPointerNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "funcpointer")
            .ToList();

        foreach (var funcPointer in funcPointerNodes) {
            var proto = funcPointer.GetElement("proto");
            var returnTypeStr = proto.GetElementValue("type").CleanName();
            var name = proto.GetElementValue("name").CleanFunctionPointerName();

            var type = new VulkanType(returnTypeStr, proto.Value, ctx.BaseTypes);
            var paramNodes = funcPointer.Elements("param");

            List<VulkanFunctionParameter> paramTypes = [];
            foreach (var param in paramNodes) {
                var paramName = param.GetElementValue("name").CleanName();
                var paramType = Utilities.GetTypeFromXml(param.GetElementValue("type").CleanName());

                paramTypes.Add(new VulkanFunctionParameter(new VulkanType(paramType, param.Value, ctx.BaseTypes), paramName));
            }

            ctx.FunctionPointers[name] = new VulkanFunctionPointer(type, name, paramTypes);

            Log.Debug("Parsed function pointer {Name} with return type {ReturnType} and parameters ({Parameters})", name, type, string.Join(", ", paramTypes));
        }

        Log.Information("Parsed {Count} function pointers", funcPointerNodes.Count);
    }
}