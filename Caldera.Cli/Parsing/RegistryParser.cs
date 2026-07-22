using System.Xml.Linq;
using Caldera.Cli.Models;

namespace Caldera.Cli.Parsing;

public static class RegistryParser {
    public static VulkanRegistry ParseFrom(string xmlString) {
        var doc = XDocument.Parse(xmlString);

        var ctx = new ParseContext();
        TypeParser.ParseFrom(doc, ctx);
        
        var baseTypes = BaseTypeParser.ParseFrom(doc, ctx);

        FunctionPointerParser.ParseFrom(doc, ctx);
        AliasParser.ParseFrom(doc, ctx);

        var enums = EnumParser.ParseFrom(doc);
        var bitmasks = BitmaskParser.ParseFrom(doc);
        var constants = ConstantParser.ParseFrom(doc);
        var handles = HandleParser.ParseHandles(doc);
        var structs = StructParser.ParseFrom(doc, ctx);
        var unions = UnionParser.ParseFrom(doc, ctx);

        return new VulkanRegistry(enums, bitmasks, constants, baseTypes, handles, structs, unions);
    }
}