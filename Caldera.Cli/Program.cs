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

    public static async Task Main(string[] args) {
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

                taskTypes[parseMaster.Id] = TaskType.Number;
                taskTypes[enumParseTask.Id] = TaskType.Number;

                parseMaster.StartTask();
                var registry = ParseRegistry(xmlString, parseMaster, enumParseTask);

                var writeMaster = ctx.AddTask("[bold white]Writing definitions[/]", maxValue: 2, autoStart: false);
                var constsWriteTask = ctx.AddTask("Writing constants", maxValue: 1, autoStart: false);
                var enumWriteTask = ctx.AddTask("Writing enums", maxValue: registry.Enums.Count, autoStart: false);

                taskTypes[writeMaster.Id] = TaskType.Number;
                taskTypes[constsWriteTask.Id] = TaskType.Number;
                taskTypes[enumWriteTask.Id] = TaskType.Number;

                writeMaster.StartTask();
                await WriteDefinitionsAsync(registry, version, writeMaster, constsWriteTask, enumWriteTask);
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

    private static VkRegistry ParseRegistry(string xmlString, ProgressTask parseMaster, ProgressTask enumParseTask) {
        var doc = XDocument.Parse(xmlString);
        var enumNodes = doc.Descendants("enums").Where(x => x.Attribute("name")?.Value != "API Constants").ToList();
        var apiConstantsNode = doc.Descendants("enums").First(x => x.Attribute("name")?.Value == "API Constants");

        enumParseTask.MaxValue = enumNodes.Count;
        enumParseTask.StartTask();

        List<VulkanEnum> enums = [];
        List<VulkanConstant> constants = [];

        foreach (var enumNode in enumNodes) {
            var rawEnumName = enumNode.GetUncheckedAttributeValue("name");
            var cleanEnumName = CleanEnumName(rawEnumName);
            var isBitmask = enumNode.GetUncheckedAttributeValue("type") == "bitmask";
            var underlyingType = "int";
            var bitwidth = enumNode.MaybeGetAttributeValue("bitwidth");

            if (isBitmask) {
                underlyingType = bitwidth == "64" ? "ulong" : "uint";
            } else if (bitwidth == "64") {
                underlyingType = "long";
            }

            List<VulkanEnumValue> values = [];

            foreach (var enumDef in enumNode.Elements("enum")) {
                var memberName = enumDef.GetCheckedAttributeValue("name");
                var cleanMemberName = CleanEnumValue(memberName);

                var exactValue = enumDef.Attribute("value")?.Value;
                var bitpos = enumDef.Attribute("bitpos")?.Value;
                var alias = enumDef.Attribute("alias")?.Value;

                var finalValue = exactValue != null ? exactValue.Replace("ULL", "UL")
                    : bitpos != null ? $"(1U << {bitpos})"
                    : alias != null ? CleanEnumValue(alias)
                    : string.Empty;

                if (!string.IsNullOrEmpty(finalValue)) {
                    values.Add(new VulkanEnumValue(cleanMemberName, finalValue));
                }
            }

            enums.Add(new VulkanEnum(cleanEnumName, isBitmask, underlyingType, values));
            enumParseTask.Increment(1);
        }

        parseMaster.Increment(1);

        foreach (var def in apiConstantsNode.Elements("enum")) {
            var memberName = def.GetCheckedAttributeValue("name");
            var memberType = GetTypeFromXml(def.GetUncheckedAttributeValue("type"));
            var value = NormalizeValue(def.GetUncheckedAttributeValue("value"));

            constants.Add(new VulkanConstant(CleanEnumValue(memberName), memberType, value));
        }

        parseMaster.Increment(1);

        return new VkRegistry(enums, constants);
    }

    private static async Task WriteDefinitionsAsync(VkRegistry registry, string version, ProgressTask writeMaster, ProgressTask constsTask, ProgressTask enumTask) {
        Directory.CreateDirectory("autogen/enums");
        Directory.CreateDirectory("autogen/constants");

        var prologue = Utilities.GetPrologueString(version);
        var genCodeAttribute = $"[GeneratedCode(\"Caldera\", \"{version}\")]";

        constsTask.StartTask();
        await using (var file = File.Create(Path.Combine("autogen", "constants", "Constants.cs")))
        await using (var writer = new StreamWriter(file)) {
            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\npublic static class Constants {{");

            foreach (var constant in registry.Constants) {
                await writer.WriteLineAsync($"    public const {constant.Type} {constant.Name} = {constant.Value};");
            }

            await writer.WriteLineAsync("}");
        }

        constsTask.Increment(1);
        writeMaster.Increment(1);

        enumTask.StartTask();
        foreach (var vulkanEnum in registry.Enums) {
            await using var file = File.Create(Path.Combine("autogen", "enums", $"{vulkanEnum.Name}.cs"));
            await using var writer = new StreamWriter(file);

            await writer.WriteLineAsync($"{prologue}\n\n{genCodeAttribute}\npublic enum {vulkanEnum.Name} : {vulkanEnum.UnderlyingType} {{");
            foreach (var value in vulkanEnum.Values) {
                await writer.WriteLineAsync($"    {value.Name} = {value.Value},");
            }

            await writer.WriteLineAsync("}");
            enumTask.Increment(1);
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

    private static string CleanEnumName(string raw) => raw.StartsWith("Vk") ? raw[2..] : raw;
    private static string CleanEnumValue(string raw) => raw.StartsWith("VK_") ? raw[3..] : raw;
    private static string NormalizeValue(string raw) => raw.Replace("LL", "L");

    private static string GetTypeFromXml(string xmlType) => xmlType switch {
        "uint32_t" => "uint",
        "uint64_t" => "ulong",
        "float" => "float",

        _ => throw new ArgumentOutOfRangeException(nameof(xmlType), xmlType, $"Unrecognized type '{xmlType}'."),
    };
}