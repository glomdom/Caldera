using System.Text;
using System.Xml.Linq;
using Caldera.Cli.Columns;
using Caldera.Cli.Models;
using Serilog;
using Spectre.Console;

namespace Caldera.Cli;

public static class Program {
    private static readonly Dictionary<string, string> BaseTypeLookup = [];

    public static async Task Main() {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Async(x => x.File("logs/caldera.log", rollingInterval: RollingInterval.Day))
            .CreateLogger();

        var version = Utilities.GetAssemblyVersion();
        Utilities.PrintBanner();

        var taskTypes = new Dictionary<int, TaskType>();

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new MetricsColumn(taskTypes),
                new SpinnerColumn())
            .StartAsync(async ctx => {
                var downloadTask = ctx.AddTask("Downloading [yellow]vk.xml[/]");
                taskTypes[downloadTask.Id] = TaskType.Download;

                var xmlString = await DownloadVkXmlAsync(downloadTask);

                var parseMaster = ctx.AddTask("[bold white]Parsing definitions[/]", maxValue: 4, autoStart: false);
                var enumParseTask = ctx.AddTask("Parsing enums", autoStart: false, maxValue: 0);
                var bitmaskParseTask = ctx.AddTask("Parsing bitmasks", autoStart: false, maxValue: 0);
                var baseTypeParseTask = ctx.AddTask("Parsing base types", autoStart: false, maxValue: 0);
                var handlesParseTask = ctx.AddTask("Parsing handles", autoStart: false, maxValue: 0);

                taskTypes[parseMaster.Id] = TaskType.Number;
                taskTypes[enumParseTask.Id] = TaskType.Number;
                taskTypes[bitmaskParseTask.Id] = TaskType.Number;
                taskTypes[baseTypeParseTask.Id] = TaskType.Number;
                taskTypes[handlesParseTask.Id] = TaskType.Number;

                parseMaster.StartTask();
                var registry = ParseRegistry(xmlString, parseMaster, enumParseTask, bitmaskParseTask, baseTypeParseTask, handlesParseTask);

                var writeMaster = ctx.AddTask("[bold white]Writing definitions[/]", maxValue: 5, autoStart: false);
                var constsWriteTask = ctx.AddTask("Writing constants", maxValue: 1, autoStart: false);
                var enumWriteTask = ctx.AddTask("Writing enums", maxValue: registry.Enums.Count, autoStart: false);
                var bitmasksWriteTask = ctx.AddTask("Writing bitmasks", maxValue: registry.Bitmasks.Count, autoStart: false);
                var baseTypesWriteTask = ctx.AddTask("Writing base types", maxValue: registry.BaseTypes.Count, autoStart: false);
                var handlesWriteTask = ctx.AddTask("Writing handles", maxValue: registry.Handles.Count, autoStart: false);

                taskTypes[writeMaster.Id] = TaskType.Number;
                taskTypes[constsWriteTask.Id] = TaskType.Number;
                taskTypes[enumWriteTask.Id] = TaskType.Number;
                taskTypes[bitmasksWriteTask.Id] = TaskType.Number;
                taskTypes[baseTypesWriteTask.Id] = TaskType.Number;
                taskTypes[handlesWriteTask.Id] = TaskType.Number;

                writeMaster.StartTask();

                await WriteDefinitionsAsync(registry, version, writeMaster, constsWriteTask, enumWriteTask, bitmasksWriteTask, baseTypesWriteTask, handlesWriteTask);
            });

        await Log.CloseAndFlushAsync();
    }

    private static async Task<string> DownloadVkXmlAsync(ProgressTask downloadTask) {
        using var client = new HttpClient();
        using var response = await client.GetAsync(
            "https://raw.githubusercontent.com/KhronosGroup/Vulkan-Docs/main/xml/vk.xml",
            HttpCompletionOption.ResponseHeadersRead
        );

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        if (totalBytes.HasValue) {
            downloadTask.MaxValue = totalBytes.Value;
        } else {
            downloadTask.IsIndeterminate = true;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var memoryStream = new MemoryStream();
        var buffer = new byte[8192];

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer)) > 0) {
            await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            if (totalBytes.HasValue) downloadTask.Increment(bytesRead);
        }

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    private static VulkanRegistry ParseRegistry(
        string xmlString,
        ProgressTask parseMaster,
        ProgressTask enumParseTask,
        ProgressTask bitmaskParseTask,
        ProgressTask baseTypesParseTask,
        ProgressTask handlesParseTask
    ) {
        var doc = XDocument.Parse(xmlString);

        var enumNodes = doc.Descendants("enums")
            .Where(x => x.Attribute("name")?.Value != "API Constants" && x.Attribute("type")?.Value != "bitmask")
            .ToList();

        var bitmaskNodes = doc.Descendants("enums")
            .Where(x => x.Attribute("name")?.Value != "API Constants" && x.Attribute("type")?.Value == "bitmask")
            .ToList();

        var apiConstantsNode = doc.Descendants("enums")
            .First(x => x.Attribute("name")?.Value == "API Constants");

        var baseTypeNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "basetype")
            .ToList();

        var handleNodes = doc.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "handle")
            .Where(x => x.Attribute("alias")?.Value == null)
            .ToList();

        enumParseTask.MaxValue = enumNodes.Count;
        enumParseTask.StartTask();

        List<VulkanEnum> enums = [];
        List<VulkanEnum> bitmasks = [];
        List<VulkanConstant> constants = [];
        List<VulkanBaseType> baseTypes = [];
        List<VulkanHandle> handles = [];

        foreach (var enumNode in enumNodes) {
            var rawEnumName = enumNode.GetUncheckedAttributeValue("name");
            var cleanEnumName = Utilities.CleanEnumName(rawEnumName);
            var bitwidth = enumNode.MaybeGetAttributeValue("bitwidth");

            var underlyingType = bitwidth == "64" ? "long" : "int";

            List<VulkanEnumValue> values = [];

            foreach (var enumDef in enumNode.Elements("enum")) {
                var memberName = enumDef.GetCheckedAttributeValue("name");
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
            enumParseTask.Increment(1);
        }

        bitmaskParseTask.MaxValue = bitmaskNodes.Count;
        bitmaskParseTask.StartTask();

        foreach (var bitmaskNode in bitmaskNodes) {
            var rawEnumName = bitmaskNode.GetUncheckedAttributeValue("name");
            var cleanEnumName = Utilities.CleanEnumName(rawEnumName);
            var bitwidth = bitmaskNode.MaybeGetAttributeValue("bitwidth");

            var underlyingType = bitwidth == "64" ? "ulong" : "uint";

            List<VulkanEnumValue> values = [];

            foreach (var enumDef in bitmaskNode.Elements("enum")) {
                var memberName = enumDef.GetCheckedAttributeValue("name");
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
            bitmaskParseTask.Increment(1);
        }

        parseMaster.Increment(1);

        foreach (var def in apiConstantsNode.Elements("enum")) {
            var memberName = def.GetCheckedAttributeValue("name");
            var memberType = Utilities.GetTypeFromXml(def.GetUncheckedAttributeValue("type"));
            var value = Utilities.NormalizeValue(def.GetUncheckedAttributeValue("value"));

            constants.Add(new VulkanConstant(Utilities.CleanEnumValue(memberName), memberType, value));
        }

        parseMaster.Increment(1);

        baseTypesParseTask.MaxValue = baseTypeNodes.Count;
        baseTypesParseTask.StartTask();

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

            baseTypes.Add(new VulkanBaseType(type, memberName, opaque, primitive));
            baseTypesParseTask.Increment(1);
        }

        parseMaster.Increment(1);

        handlesParseTask.MaxValue = handleNodes.Count;
        handlesParseTask.StartTask();

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
            handlesParseTask.Increment(1);
        }

        parseMaster.Increment(1);

        return new VulkanRegistry(enums, bitmasks, constants, baseTypes, handles);
    }

    private static async Task WriteDefinitionsAsync(
        VulkanRegistry registry,
        string version,
        ProgressTask writeMaster,
        ProgressTask constsTask,
        ProgressTask enumTask,
        ProgressTask bitmasksTask,
        ProgressTask baseTypesTask,
        ProgressTask handlesWriteTask
    ) {
        Directory.CreateDirectory("autogen/bitmasks");

        var prologue = Utilities.GetPrologueString(version);
        var genCodeAttribute = $"[GeneratedCode(\"Caldera\", \"{version}\")]";

        await WriteConstantsAsync(registry.Constants, prologue, genCodeAttribute, constsTask, writeMaster);
        await WriteEnumsAsync(registry.Enums, prologue, genCodeAttribute, enumTask, writeMaster);
        await WriteBitmasksAsync(registry.Bitmasks, prologue, genCodeAttribute, bitmasksTask, writeMaster);
        await WriteBaseTypesAsync(registry.BaseTypes, prologue, genCodeAttribute, baseTypesTask, writeMaster);
        await WriteHandlesAsync(registry.Handles, prologue, genCodeAttribute, handlesWriteTask, writeMaster);
    }

    private static async Task WriteConstantsAsync(List<VulkanConstant> constants, string prologue, string genCodeAttribute, ProgressTask constsTask, ProgressTask writeMaster) {
        Directory.CreateDirectory("autogen/constants");
        constsTask.StartTask();

        await using (var file = File.Create(Path.Combine("autogen", "constants", "Constants.cs")))
        await using (var writer = new StreamWriter(file)) {
            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\npublic static class Constants {{");

            foreach (var constant in constants) {
                await writer.WriteLineAsync($"    public const {constant.Type} {constant.Name} = {constant.Value};");
            }

            await writer.WriteLineAsync("}");
        }

        constsTask.Increment(1);
        writeMaster.Increment(1);
    }

    private static async Task WriteEnumsAsync(List<VulkanEnum> enums, string prologue, string genCodeAttribute, ProgressTask enumTask, ProgressTask writeMaster) {
        Directory.CreateDirectory("autogen/enums");
        enumTask.StartTask();

        foreach (var def in enums) {
            await using var file = File.Create(Path.Combine("autogen", "enums", $"{def.Name}.cs"));
            await using var writer = new StreamWriter(file);

            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\npublic enum {def.Name} : {def.UnderlyingType} {{");
            foreach (var value in def.Values) {
                await writer.WriteLineAsync($"    {value.Name} = {value.Value},");
            }

            await writer.WriteLineAsync("}");
            enumTask.Increment(1);
        }

        writeMaster.Increment(1);
    }

    private static async Task WriteBitmasksAsync(List<VulkanEnum> bitmasks, string prologue, string genCodeAttribute, ProgressTask bitmasksTask, ProgressTask writeMaster) {
        Directory.CreateDirectory("autogen/bitmasks");
        bitmasksTask.StartTask();

        foreach (var vulkanBitmask in bitmasks) {
            await using var file = File.Create(Path.Combine("autogen", "bitmasks", $"{vulkanBitmask.Name}.cs"));
            await using var writer = new StreamWriter(file);

            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\n[Flags]\npublic enum {vulkanBitmask.Name} : {vulkanBitmask.UnderlyingType} {{");
            foreach (var value in vulkanBitmask.Values) {
                await writer.WriteLineAsync($"    {value.Name} = {value.Value},");
            }

            await writer.WriteLineAsync("}");
            bitmasksTask.Increment(1);
        }

        writeMaster.Increment(1);
    }

    private static async Task WriteBaseTypesAsync(
        List<VulkanBaseType> baseTypes,
        string prologue,
        string genCodeAttribute,
        ProgressTask baseTypesTask,
        ProgressTask writeMaster
    ) {
        Directory.CreateDirectory("autogen/basetypes");
        baseTypesTask.StartTask();

        foreach (var type in baseTypes) {
            if (type.IsOpaque) {
                await using var file = File.Create(Path.Combine("autogen", "basetypes", $"{type.Name}.cs"));
                await using var writer = new StreamWriter(file);

                await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\npublic readonly record struct {type.Name}(nint Handle) {{");
                await writer.WriteLineAsync($"    public static readonly {type.Name} Null = new(0);");
                await writer.WriteLineAsync("}");
            } else if (type.IsPrimitive) {
                BaseTypeLookup[type.Name] = type.Type;

                Log.Debug("Populated lookup table with {Normal} -> {Lookup}", type.Name, type.Type);
            }

            baseTypesTask.Increment(1);
        }

        writeMaster.Increment(1);
    }

    private static async Task WriteHandlesAsync(
        List<VulkanHandle> handles,
        string prologue,
        string genCodeAttribute,
        ProgressTask handlesWriteTask,
        ProgressTask writeMaster
    ) {
        Directory.CreateDirectory("autogen/handles");
        handlesWriteTask.StartTask();

        foreach (var handle in handles) {
            await using var file = File.Create(Path.Combine("autogen", "handles", $"{handle.Name}.cs"));
            await using var writer = new StreamWriter(file);

            var handleType = handle.Dispatchable ? "nint" : "ulong";

            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\npublic readonly record struct {handle.Name}({handleType} Handle) {{");
            await writer.WriteLineAsync($"    public static readonly {handle.Name} Null = new(0);");
            await writer.WriteLineAsync("}");

            handlesWriteTask.Increment(1);
        }

        writeMaster.Increment(1);
    }
}