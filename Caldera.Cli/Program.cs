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

    private static async Task WriteEachAsync<T>(
        IEnumerable<T> defs,
        string subdir,
        string prologue,
        string genCodeAttr,
        Func<T, string> fileName,
        Func<T, string> header,
        Func<T, IEnumerable<string>> body
    ) {
        Directory.CreateDirectory(Path.Combine("autogen", subdir));

        foreach (var def in defs) {
            await using var file = File.Create(Path.Combine("autogen", subdir, fileName(def)));
            await using var writer = new StreamWriter(file);

            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttr}\n{header(def)}");

            foreach (var line in body(def)) {
                await writer.WriteLineAsync(line);
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

    private static Task WriteEnumsAsync(List<VulkanEnum> enums, string prologue, string genCodeAttribute) =>
        WriteEachAsync(
            enums, "enums", prologue, genCodeAttribute,
            def => $"{def.Name}.cs",
            def => $"public enum {def.Name} : {def.UnderlyingType} {{",
            def => def.Values.Select(v => $"    {v.Name} = {v.Value},")
        );

    private static Task WriteBitmasksAsync(List<VulkanEnum> bitmasks, string prologue, string genCodeAttribute) =>
        WriteEachAsync(
            bitmasks, "bitmasks", prologue, genCodeAttribute,
            def => $"{def.Name}.cs",
            def => $"[Flags]\npublic enum {def.Name} : {def.UnderlyingType} {{",
            def => def.Values.Select(v => $"    {v.Name} = {v.Value},")
        );

    private static Task WriteBaseTypesAsync(List<VulkanBaseType> baseTypes, string prologue, string genCodeAttribute) =>
        WriteEachAsync(
            baseTypes.Where(t => t.IsOpaque), "basetypes", prologue, genCodeAttribute,
            def => $"{def.Name}.cs",
            def => $"public readonly record struct {def.Name}(nint Handle) {{",
            def => [$"    public static readonly {def.Name} Null = new(0);"]
        );

    private static Task WriteHandlesAsync(List<VulkanHandle> handles, string prologue, string genCodeAttribute) =>
        WriteEachAsync(
            handles, "handles", prologue, genCodeAttribute,
            def => $"{def.Name}.cs",
            def => $"public readonly record struct {def.Name}({(def.Dispatchable ? "nint" : "ulong")} Handle) {{",
            def => [$"    public static readonly {def.Name} Null = new(0);"]
        );

    private static Task WriteStructsAsync(List<VulkanStruct> structs, string prologue, string genCodeAttribute) =>
        WriteEachAsync(
            structs, "structs", prologue, genCodeAttribute,
            def => $"{def.Name}.cs",
            def => $"[StructLayout(LayoutKind.Sequential)]\npublic {(def.HasPointers ? "unsafe struct" : "struct")} {def.Name} {{",
            def => def.Members.Select(m => $"    public {m.Type} {m.Name};")
        );

    private static Task WriteUnionsAsync(List<VulkanUnion> unions, string prologue, string genCodeAttribute) =>
        WriteEachAsync(
            unions, "unions", prologue, genCodeAttribute,
            def => $"{def.Name}.cs",
            def => $"[StructLayout(LayoutKind.Explicit)]\npublic {(def.HasPointers ? "unsafe struct" : "struct")} {def.Name} {{",
            def => def.Members.Select(m => $"    [FieldOffset(0)]\n    public {m.Type} {m.Name};")
        );
}