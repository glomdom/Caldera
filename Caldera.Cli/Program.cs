using System.Text;
using System.Xml.Linq;
using Caldera.Cli.Extensions;
using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli;

public static class Program {
    private static readonly Dictionary<string, string> BaseTypeLookup = [];
    private static readonly Dictionary<string, VulkanFunctionPointer> FunctionPointerLookup = [];
    private static readonly HttpClient Client = new();

    public static async Task Main() {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .CreateLogger();

        var version = Utilities.GetAssemblyVersion();

        Utilities.PrintBanner();

        Log.Information("Downloading vk.xml");
        var xmlString = await DownloadVkXmlAsync();

        Log.Information("Parsing definitions from vk.xml");
        var registry = ParseRegistry(xmlString);

        Log.Information("Writing C# definitions");
        await WriteDefinitionsAsync(registry, version);

        Log.Information("Generated files can be found in {Location}", Path.Combine(Directory.GetCurrentDirectory(), "autogen"));

        await Log.CloseAndFlushAsync();
    }

    private static async Task<string> DownloadVkXmlAsync() {
        using var response = await Client.GetAsync(
            "https://raw.githubusercontent.com/KhronosGroup/Vulkan-Docs/main/xml/vk.xml",
            HttpCompletionOption.ResponseHeadersRead
        );

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();

        return Encoding.UTF8.GetString(bytes);
    }

    private static VulkanRegistry ParseRegistry(string xmlString) {
        var doc = XDocument.Parse(xmlString);

        var enums = ParseEnums(doc);
        var bitmasks = ParseBitmasks(doc);
        var constants = ParseConstants(doc);
        var baseTypes = ParseBaseTypes(doc);
        var handles = ParseHandles(doc);
        ParseFunctionPointers(doc);
        var structs = ParseStructs(doc);
        var unions = ParseUnions(doc);

        return new VulkanRegistry(enums, bitmasks, constants, baseTypes, handles, structs, unions);
    }

    private static List<VulkanEnum> ParseEnums(XDocument doc) {
        List<VulkanEnum> enums = [];

        var enumNodes = doc.Descendants("enums")
            .Where(x => x.Attribute("name")?.Value != "API Constants" && x.Attribute("type")?.Value != "bitmask")
            .ToList();

        foreach (var enumNode in enumNodes) {
            var rawEnumName = enumNode.GetUncheckedAttributeValue("name");
            var cleanEnumName = Utilities.CleanEnumName(rawEnumName);
            var bitwidth = enumNode.MaybeGetAttributeValue("bitwidth");

            var underlyingType = bitwidth == "64" ? "long" : "int";

            List<VulkanEnumValue> values = [];

            foreach (var enumDef in enumNode.Elements("enum")) {
                var memberName = enumDef.GetAttributeValue("name");
                var cleanMemberName = Utilities.CleanEnumValue(memberName);

                var exactValue = enumDef.Attribute("value")?.Value;
                var alias = enumDef.Attribute("alias")?.Value;

                var finalValue = exactValue != null ? exactValue.Replace("ULL", "UL")
                    : alias != null ? Utilities.CleanEnumValue(alias)
                    : string.Empty;

                if (!string.IsNullOrEmpty(finalValue)) {
                    values.Add(new VulkanEnumValue(cleanMemberName, finalValue));
                }
            }

            enums.Add(new VulkanEnum(cleanEnumName, false, underlyingType, values));
        }

        Log.Information("Parsed {Count} enums, of which {ToGenerateCount} will be generated", enumNodes.Count, enums.Count);

        return enums;
    }

    private static List<VulkanEnum> ParseBitmasks(XDocument doc) {
        List<VulkanEnum> bitmasks = [];
        HashSet<string> generatedFlags = [];

        var bitmaskNodes = doc.Descendants("enums")
            .Where(x => x.Attribute("name")?.Value != "API Constants" && x.Attribute("type")?.Value == "bitmask")
            .ToList();

        var mapping = ParseBitmaskTypeMapping(doc);

        foreach (var bitmaskNode in bitmaskNodes) {
            var rawEnumName = bitmaskNode.GetUncheckedAttributeValue("name");
            var rawFlagsName = mapping.GetValueOrDefault(rawEnumName, rawEnumName);

            var cleanEnumName = Utilities.CleanEnumName(rawFlagsName);
            var bitwidth = bitmaskNode.MaybeGetAttributeValue("bitwidth");

            var underlyingType = bitwidth == "64" ? "ulong" : "uint";

            List<VulkanEnumValue> values = [];

            foreach (var enumDef in bitmaskNode.Elements("enum")) {
                var memberName = enumDef.GetAttributeValue("name");
                var cleanMemberName = Utilities.CleanEnumValue(memberName);

                var exactValue = enumDef.Attribute("value")?.Value;
                var bitpos = enumDef.Attribute("bitpos")?.Value;
                var alias = enumDef.Attribute("alias")?.Value;

                var finalValue = exactValue != null ? exactValue.Replace("ULL", "UL")
                    : bitpos != null ? $"(1U << {bitpos})"
                    : alias != null ? Utilities.CleanEnumValue(alias)
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

            var cleanEnumName = Utilities.CleanEnumName(rawFlagsName);
            var innerType = typeNode.Element("type")?.Value;
            var underlyingType = innerType == "VkFlags64" ? "ulong" : "uint";

            List<VulkanEnumValue> emptyValues = [new VulkanEnumValue("None", "0")];

            bitmasks.Add(new VulkanEnum(cleanEnumName, true, underlyingType, emptyValues));

            Log.Debug("Parsed empty bitmask {BitmaskName} of type {UnderlyingType}", cleanEnumName, underlyingType);
        }

        Log.Information("Parsed {Count} bitmasks total", bitmasks.Count);

        return bitmasks;
    }

    private static List<VulkanConstant> ParseConstants(XDocument doc) {
        List<VulkanConstant> constants = [];

        var apiConstantsNode = doc.Descendants("enums")
            .First(x => x.Attribute("name")?.Value == "API Constants");

        foreach (var def in apiConstantsNode.Elements("enum")) {
            var memberName = def.GetAttributeValue("name");
            var memberType = Utilities.GetTypeFromXml(def.GetUncheckedAttributeValue("type"));
            var value = Utilities.NormalizeValue(def.GetUncheckedAttributeValue("value"));

            constants.Add(new VulkanConstant(Utilities.CleanEnumValue(memberName), memberType, value));
        }

        Log.Information("Parsed API constants");

        return constants;
    }

    private static List<VulkanBaseType> ParseBaseTypes(XDocument doc) {
        List<VulkanBaseType> baseTypes = [];

        var baseTypeNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "basetype")
            .ToList();

        foreach (var def in baseTypeNodes) {
            var rawMemberName = def.Element("name")?.Value;
            if (rawMemberName is null) continue;

            var memberName = Utilities.CleanEnumName(rawMemberName);

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
                BaseTypeLookup[memberName] = type;
                Log.Debug("Populated lookup table with {Normal} -> {Lookup}", memberName, type);
            }

            baseTypes.Add(new VulkanBaseType(type, memberName, opaque, primitive));

            Log.Debug("Parsed base type {BaseTypeName} of type {Type}, primitive={Primitive} | opaque={Opaque}", memberName, type, primitive, opaque);
        }

        Log.Information("Parsed {Count} base types, of which {ToGenerateCount} will be generated", baseTypeNodes.Count, baseTypes.Count);

        return baseTypes;
    }

    private static List<VulkanHandle> ParseHandles(XDocument doc) {
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

            var name = Utilities.CleanEnumName(rawName);
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

    private static void ParseFunctionPointers(XDocument doc) {
        var funcPointerNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "funcpointer")
            .ToList();

        foreach (var funcPointer in funcPointerNodes) {
            var proto = funcPointer.GetElement("proto");
            var returnTypeStr = proto.GetElementValue("type").CleanName();
            var name = proto.GetElementValue("name").CleanFunctionPointerName();

            var type = new VulkanType(returnTypeStr, proto.Value, BaseTypeLookup);
            var paramNodes = funcPointer.Elements("param");

            List<VulkanFunctionParameter> paramTypes = [];
            foreach (var param in paramNodes) {
                var paramName = param.GetElementValue("name").CleanName();
                var paramType = Utilities.GetTypeFromXml(param.GetElementValue("type").CleanName());

                paramTypes.Add(new VulkanFunctionParameter(new VulkanType(paramType, param.Value, BaseTypeLookup), paramName));
            }

            FunctionPointerLookup[name] = new VulkanFunctionPointer(type, name, paramTypes);

            Log.Debug("Parsed function pointer {Name} with return type {ReturnType} and parameters ({Parameters})", name, type, string.Join(", ", paramTypes));
        }

        Log.Information("Parsed {Count} function pointers", funcPointerNodes.Count);
    }

    // <type category="struct" name="VkBaseOutStructure">
    // <member><type>VkStructureType</type> <name>sType</name></member>
    // <member optional="true">struct <type>VkBaseOutStructure</type>* <name>pNext</name></member>
    // </type>
    // <type category="struct" name="VkBaseInStructure">
    // <member><type>VkStructureType</type> <name>sType</name></member>
    // <member optional="true">const struct <type>VkBaseInStructure</type>* <name>pNext</name></member>
    // </type>
    // <type category="struct" name="VkOffset2D"> OK
    // <member><type>int32_t</type>        <name>x</name></member>
    // <member><type>int32_t</type>        <name>y</name></member>
    // </type>
    // <type category="struct" name="VkOffset3D">
    // <member><type>int32_t</type>        <name>x</name></member>
    // <member><type>int32_t</type>        <name>y</name></member>
    // <member><type>int32_t</type>        <name>z</name></member>
    // </type>

    // TODO: parse optional properly, true/false does not change abi.
    //       optional="true,false" - pointer must be provided, elements can be null
    //       optional="false,true" - pointer can be null, if provided all elements must be valid
    private static List<VulkanStruct> ParseStructs(XDocument doc) {
        List<VulkanStruct> structs = [];

        var structNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "struct")
            .ToList();

        foreach (var structNode in structNodes) {
            var name = structNode.GetUncheckedAttributeValue("name").CleanName();

            List<VulkanStructMember> members = [];
            foreach (var member in structNode.Elements("member")) {
                var memberName = member.GetElementValue("name").CleanName();
                var memberApi = member.MaybeGetAttributeValue("api");
                if (memberApi is not null && !memberApi.Split(',').Contains("vulkan")) {
                    Log.Information("Skipping {MemberName} because it does not have vulkan api constraint", memberName);

                    continue;
                }

                var memberRawType = Utilities.GetTypeFromXml(member.GetElementValue("type").CleanName().CleanFunctionPointerName());
                if (FunctionPointerLookup.TryGetValue(memberRawType, out var fp)) {
                    var memberType = new VulkanType(fp.Name, member.Value, FunctionPointerLookup);
                    members.Add(new VulkanStructMember(memberType, memberName));
                } else {
                    var memberType = new VulkanType(memberRawType.CleanName(), member.Value, BaseTypeLookup);
                    members.Add(new VulkanStructMember(memberType, memberName));
                }
            }

            var hasPointers = members.Any(x => x.Type.IsPointer);
            structs.Add(new VulkanStruct(name, members, hasPointers));
        }

        Log.Information("Parsed {Count} structs of which {ToGenerateCount} will be generated", structNodes.Count, structs.Count);

        return structs;
    }

    private static List<VulkanUnion> ParseUnions(XDocument doc) {
        List<VulkanUnion> unions = [];

        var unionNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "union")
            .ToList();

        foreach (var node in unionNodes) {
            var unionName = node.GetUncheckedAttributeValue("name").CleanName();

            Log.Warning("Parsed {Name}", unionName);

            List<VulkanUnionMember> members = [];
            foreach (var member in node.Elements("member")) {
                var memberName = member.GetElementValue("name").CleanName();
                var memberRawType = Utilities.GetTypeFromXml(member.GetElementValue("type").CleanName());

                members.Add(new VulkanUnionMember(new VulkanType(memberRawType, member.Value, BaseTypeLookup), memberName));
            }

            var hasPointers = members.Any(x => x.Type.IsPointer);
            unions.Add(new VulkanUnion(unionName, members, hasPointers));
        }

        Log.Information("Parsed {Count} unions of which {ToGenerateCount} will be generated", unionNodes.Count, unions.Count);

        return unions;
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

    private static async Task WriteDefinitionsAsync(VulkanRegistry registry, string version) {
        Directory.CreateDirectory("autogen/bitmasks");

        var prologue = Utilities.GetPrologueString(version);
        var genCodeAttribute = $"[GeneratedCode(\"Caldera\", \"{version}\")]";

        await WriteConstantsAsync(registry.Constants, prologue, genCodeAttribute);
        Log.Information("Wrote API Constants");

        await WriteEnumsAsync(registry.Enums, prologue, genCodeAttribute);
        Log.Information("Wrote {Count} enums", registry.Enums.Count);

        await WriteBitmasksAsync(registry.Bitmasks, prologue, genCodeAttribute);
        Log.Information("Wrote {Count} bitmasks", registry.Bitmasks.Count);

        await WriteBaseTypesAsync(registry.BaseTypes, prologue, genCodeAttribute);
        Log.Information("Wrote {Count} base types", registry.BaseTypes.Count);

        await WriteHandlesAsync(registry.Handles, prologue, genCodeAttribute);
        Log.Information("Wrote {Count} handles", registry.Handles.Count);

        await WriteStructsAsync(registry.Structs, prologue, genCodeAttribute);
        Log.Information("Wrote {Count} structs", registry.Structs.Count);

        await WriteUnionsAsync(registry.Unions, prologue, genCodeAttribute);
        Log.Information("Wrote {Count} unions", registry.Unions.Count);
    }

    private static async Task WriteUnionsAsync(List<VulkanUnion> unions, string prologue, string genCodeAttribute) {
        Directory.CreateDirectory("autogen/unions");

        foreach (var def in unions) {
            await using var file = File.Create(Path.Combine("autogen", "unions", $"{def.Name}.cs"));
            await using var writer = new StreamWriter(file);
            
            var structDefinition = "struct";
            if (def.HasPointers) {
                structDefinition = "unsafe struct";
            }
            
            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\n[StructLayout(LayoutKind.Explicit)]\npublic {structDefinition} {def.Name} {{");
            foreach (var value in def.Members) {
                await writer.WriteLineAsync($"    [FieldOffset(0)]\npublic {value.Type} {value.Name};");
            }

            await writer.WriteLineAsync("}");
        }
    }

    private static async Task WriteStructsAsync(List<VulkanStruct> structs, string prologue, string genCodeAttribute) {
        Directory.CreateDirectory("autogen/structs");

        foreach (var def in structs) {
            await using var file = File.Create(Path.Combine("autogen", "structs", $"{def.Name}.cs"));
            await using var writer = new StreamWriter(file);

            var structDefinition = "struct";
            if (def.HasPointers) {
                structDefinition = "unsafe struct";
            }

            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\n[StructLayout(LayoutKind.Sequential)]\npublic {structDefinition} {def.Name} {{");
            foreach (var value in def.Members) {
                await writer.WriteLineAsync($"    public {value.Type} {value.Name};");
            }

            await writer.WriteLineAsync("}");
        }
    }

    private static async Task WriteConstantsAsync(List<VulkanConstant> constants, string prologue, string genCodeAttribute) {
        Directory.CreateDirectory("autogen/constants");

        await using var file = File.Create(Path.Combine("autogen", "constants", "Constants.cs"));
        await using var writer = new StreamWriter(file);

        await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\npublic static class Constants {{");

        foreach (var constant in constants) {
            await writer.WriteLineAsync($"    public const {constant.Type} {constant.Name} = {constant.Value};");
        }

        await writer.WriteLineAsync("}");
    }

    private static async Task WriteEnumsAsync(List<VulkanEnum> enums, string prologue, string genCodeAttribute) {
        Directory.CreateDirectory("autogen/enums");

        foreach (var def in enums) {
            await using var file = File.Create(Path.Combine("autogen", "enums", $"{def.Name}.cs"));
            await using var writer = new StreamWriter(file);

            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\npublic enum {def.Name} : {def.UnderlyingType} {{");
            foreach (var value in def.Values) {
                await writer.WriteLineAsync($"    {value.Name} = {value.Value},");
            }

            await writer.WriteLineAsync("}");
        }
    }

    private static async Task WriteBitmasksAsync(List<VulkanEnum> bitmasks, string prologue, string genCodeAttribute) {
        Directory.CreateDirectory("autogen/bitmasks");

        foreach (var vulkanBitmask in bitmasks) {
            await using var file = File.Create(Path.Combine("autogen", "bitmasks", $"{vulkanBitmask.Name}.cs"));
            await using var writer = new StreamWriter(file);

            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\n[Flags]\npublic enum {vulkanBitmask.Name} : {vulkanBitmask.UnderlyingType} {{");
            foreach (var value in vulkanBitmask.Values) {
                await writer.WriteLineAsync($"    {value.Name} = {value.Value},");
            }

            await writer.WriteLineAsync("}");
        }
    }

    private static async Task WriteBaseTypesAsync(List<VulkanBaseType> baseTypes, string prologue, string genCodeAttribute) {
        Directory.CreateDirectory("autogen/basetypes");

        foreach (var type in baseTypes.Where(type => type.IsOpaque)) {
            await using var file = File.Create(Path.Combine("autogen", "basetypes", $"{type.Name}.cs"));
            await using var writer = new StreamWriter(file);

            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\npublic readonly record struct {type.Name}(nint Handle) {{");
            await writer.WriteLineAsync($"    public static readonly {type.Name} Null = new(0);");
            await writer.WriteLineAsync("}");
        }
    }

    private static async Task WriteHandlesAsync(List<VulkanHandle> handles, string prologue, string genCodeAttribute) {
        Directory.CreateDirectory("autogen/handles");

        foreach (var handle in handles) {
            await using var file = File.Create(Path.Combine("autogen", "handles", $"{handle.Name}.cs"));
            await using var writer = new StreamWriter(file);

            var handleType = handle.Dispatchable ? "nint" : "ulong";

            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\npublic readonly record struct {handle.Name}({handleType} Handle) {{");
            await writer.WriteLineAsync($"    public static readonly {handle.Name} Null = new(0);");
            await writer.WriteLineAsync("}");
        }
    }
}