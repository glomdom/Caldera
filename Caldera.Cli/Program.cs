using System.Xml.Linq;
using Caldera.Cli.Generation;
using Caldera.Cli.Models;
using Caldera.Cli.Parsing;
using Serilog;

namespace Caldera.Cli;

public static class Program {
    public static async Task Main() {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .CreateLogger();

        var version = Metadata.GetAssemblyVersion();

        Banner.PrintBanner();

        Log.Information("Downloading vk.xml");
        var xmlString = await VkDownloader.DownloadVkXmlAsync();

        Log.Information("Parsing definitions from vk.xml");
        var registry = ParseRegistry(xmlString);

        Log.Information("Writing C# definitions");
        await WriteDefinitionsAsync(registry, version);

        Log.Information("Generated files can be found in {Location}", Path.Combine(Directory.GetCurrentDirectory(), "autogen"));

        await Log.CloseAndFlushAsync();
    }

    private static VulkanRegistry ParseRegistry(string xmlString) {
        var doc = XDocument.Parse(xmlString);

        var ctx = new ParseContext();
        var baseTypes = BaseTypeParser.ParseFrom(doc, ctx);

        FunctionPointerParser.ParseFrom(doc, ctx);

        var enums = EnumParser.ParseFrom(doc);
        var bitmasks = BitmaskParser.ParseFrom(doc);
        var constants = ConstantParser.ParseFrom(doc);
        var handles = HandleParser.ParseHandles(doc);
        var structs = StructParser.ParseFrom(doc, ctx);
        var unions = UnionParser.ParseFrom(doc, ctx);

        return new VulkanRegistry(enums, bitmasks, constants, baseTypes, handles, structs, unions);
    }

    private static async Task WriteDefinitionsAsync(VulkanRegistry registry, string version) {
        Directory.CreateDirectory("autogen/bitmasks");

        var prologue = Metadata.GetPrologueString(version);
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
                await writer.WriteLineAsync($"    [FieldOffset(0)]\n    public {value.Type} {value.Name};");
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