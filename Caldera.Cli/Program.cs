using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Caldera.Cli.Models;
using Spectre.Console;

namespace Caldera.Cli;

public static class Program {
    private const string Banner = """
                                                  (    (           (              
                                     (     (      )\ ) )\ )        )\ )    (      
                                     )\    )\    (()/((()/(   (   (()/(    )\     
                                   (((_)((((_)(   /(_))/(_))  )\   /(_))((((_)(   
                                   )\___ )\ _ )\ (_)) (_))_  ((_) (_))   )\ _ )\  
                                  ((/ __|(_)_\(_)| |   |   \ | __|| _ \  (_)_\(_) 
                                   | (__  / _ \  | |__ | |) || _| |   /   / _ \   
                                    \___|/_/ \_\ |____||___/ |___||_|_\  /_/ \_\  
                                  """;

    public static async Task Main() {
        var version = GetAssemblyVersion();
        PrintBanner();

        var taskTypes = new Dictionary<int, TaskType>();

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new MetricsColumn(taskTypes),
                new SpinnerColumn())
            .StartAsync(async ctx => {
                var downloadTask = ctx.AddTask("Downloading [yellow]vk.xml[/]");
                taskTypes[downloadTask.Id] = TaskType.Download;
                var xmlString = await DownloadVkXmlAsync(downloadTask);

                var parseMaster = ctx.AddTask("[bold white]Parsing definitions[/]", maxValue: 2, autoStart: false);
                var enumParseTask = ctx.AddTask("Parsing enums", autoStart: false);
                var bitmaskParseTask = ctx.AddTask("Parsing bitmasks", autoStart: false);

                taskTypes[parseMaster.Id] = TaskType.Number;
                taskTypes[enumParseTask.Id] = TaskType.Number;

                parseMaster.StartTask();
                var registry = ParseRegistry(xmlString, parseMaster, enumParseTask, bitmaskParseTask);

                var writeMaster = ctx.AddTask("[bold white]Writing definitions[/]", maxValue: 3, autoStart: false);
                var constsWriteTask = ctx.AddTask("Writing constants", maxValue: 1, autoStart: false);
                var enumWriteTask = ctx.AddTask("Writing enums", maxValue: registry.Enums.Count, autoStart: false);
                var bitmasksWriteTask = ctx.AddTask("Writing bitmasks", maxValue: registry.Bitmasks.Count, autoStart: false);

                taskTypes[writeMaster.Id] = TaskType.Number;
                taskTypes[constsWriteTask.Id] = TaskType.Number;
                taskTypes[enumWriteTask.Id] = TaskType.Number;

                writeMaster.StartTask();

                await WriteDefinitionsAsync(registry, version, writeMaster, constsWriteTask, enumWriteTask, bitmasksWriteTask);
            });
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

    private static VulkanRegistry ParseRegistry(string xmlString, ProgressTask parseMaster, ProgressTask enumParseTask, ProgressTask bitmaskParseTask) {
        var doc = XDocument.Parse(xmlString);

        var enumNodes = doc.Descendants("enums")
            .Where(x => x.Attribute("name")?.Value != "API Constants" && x.Attribute("type")?.Value != "bitmask")
            .ToList();

        var bitmaskNodes = doc.Descendants("enums")
            .Where(x => x.Attribute("name")?.Value != "API Constants" && x.Attribute("type")?.Value == "bitmask")
            .ToList();

        var apiConstantsNode = doc.Descendants("enums")
            .First(x => x.Attribute("name")?.Value == "API Constants");

        enumParseTask.MaxValue = enumNodes.Count;
        enumParseTask.StartTask();

        List<VulkanEnum> enums = [];
        List<VulkanEnum> bitmasks = [];
        List<VulkanConstant> constants = [];

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

        return new VulkanRegistry(enums, bitmasks, constants);
    }

    private static async Task WriteDefinitionsAsync(
        VulkanRegistry registry,
        string version,
        ProgressTask writeMaster,
        ProgressTask constsTask,
        ProgressTask enumTask,
        ProgressTask bitmasksTask
    ) {
        Directory.CreateDirectory("autogen/bitmasks");

        var prologue = Utilities.GetPrologueString(version);
        var genCodeAttribute = $"[GeneratedCode(\"Caldera\", \"{version}\")]";

        await WriteConstantsAsync(registry.Constants, prologue, genCodeAttribute, constsTask, writeMaster);
        await WriteEnumsAsync(registry.Enums, prologue, genCodeAttribute, enumTask, writeMaster);
        await WriteBitmasksAsync(registry.Bitmasks, prologue, genCodeAttribute, bitmasksTask, writeMaster);
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

    private static string GetAssemblyVersion() {
        return Assembly.GetEntryAssembly()
                   ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion ??
               throw new InvalidOperationException("Failed to get entry assembly version.");
    }

    private static void PrintBanner() {
        var lines = Banner.Split('\n');
        for (var i = 0; i < lines.Length; i++) {
            var ratio = lines.Length > 1 ? (double)i / (lines.Length - 1) : 0;
            var r = (byte)(255 - (255 - 180) * ratio);
            var g = (byte)(165 - (165 - 50) * ratio);

            AnsiConsole.MarkupLine($"[rgb({r},{g},0)]{lines[i]}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[gray70]A C# Vulkan bindings forge.[/]");
    }
}