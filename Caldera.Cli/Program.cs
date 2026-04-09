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
        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (version is null) {
            throw new InvalidOperationException("Failed to get entry assembly version.");
        }

        var lines = Banner.Split('\n');

        for (var i = 0; i < lines.Length; i++) {
            var ratio = lines.Length > 1 ? (double)i / (lines.Length - 1) : 0;

            var r = (byte)(255 - (255 - 180) * ratio);
            var g = (byte)(165 - (165 - 50) * ratio);

            AnsiConsole.MarkupLine($"[rgb({r},{g},0)]{lines[i]}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[gray70]A C# Vulkan bindings forge.[/]");

        var taskTypes = new Dictionary<int, TaskType>();
        await AnsiConsole.Progress()
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new MetricsColumn(taskTypes), new SpinnerColumn())
            .StartAsync(async ctx => {
                var downloadTask = ctx.AddTask("Downloading [yellow]vk.xml[/]");
                taskTypes[downloadTask.Id] = TaskType.Download;

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

                    if (totalBytes.HasValue) {
                        downloadTask.Increment(bytesRead);
                    }
                }

                var xmlString = Encoding.UTF8.GetString(memoryStream.ToArray());

                var parseMaster = ctx.AddTask("[bold white]Parsing definitions[/]", maxValue: 1, autoStart: false);
                taskTypes[parseMaster.Id] = TaskType.Number;
                parseMaster.StartTask();

                var doc = XDocument.Parse(xmlString);
                var enumNodes = doc.Descendants("enums").Where(x => x.Attribute("name")?.Value != "API Constants").ToList();
                var apiConstantsNode = doc.Descendants("enums").First(x => x.Attribute("name")?.Value == "API Constants");

                var enumTask = ctx.AddTask("Parsing enums", maxValue: enumNodes.Count, autoStart: false);
                taskTypes[enumTask.Id] = TaskType.Number;
                enumTask.StartTask();

                List<VulkanEnum> enums = [];

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

                        string finalValue;

                        if (exactValue != null) {
                            finalValue = exactValue.Replace("ULL", "UL");
                        } else if (bitpos != null) {
                            finalValue = $"(1U << {bitpos})";
                        } else if (alias != null) {
                            finalValue = CleanEnumValue(alias);
                        } else {
                            continue;
                        }

                        values.Add(new VulkanEnumValue(cleanMemberName, finalValue));
                    }

                    enums.Add(new VulkanEnum(cleanEnumName, isBitmask, underlyingType, values));
                    enumTask.Increment(1);
                }

                parseMaster.Increment(1);

                var writeMaster = ctx.AddTask("[bold white]Writing definitions[/]", maxValue: 2, autoStart: false);
                taskTypes[writeMaster.Id] = TaskType.Number;
                writeMaster.StartTask();

                Directory.CreateDirectory("autogen");
                Directory.CreateDirectory("autogen/enums");
                Directory.CreateDirectory("autogen/constants");

                var prologue = Utilities.GetPrologueString(version);

                var constsWriteTask = ctx.AddTask("Writing constants", maxValue: 1, autoStart: false);
                taskTypes[constsWriteTask.Id] = TaskType.Number;
                constsWriteTask.StartTask();

                {
                    await using var file = File.Create(Path.Combine("autogen", "constants", "Constants.cs"));
                    await using var writer = new StreamWriter(file);

                    await writer.WriteLineAsync(prologue);
                    await writer.WriteLineAsync("");
                    await writer.WriteLineAsync($"[GeneratedCode(\"Caldera\", \"{version}\")]");
                    await writer.WriteLineAsync("public static class Constants {");

                    foreach (var def in apiConstantsNode.Elements("enum")) {
                        var memberName = def.GetCheckedAttributeValue("name");
                        var cleanMemberName = CleanEnumValue(memberName);

                        var rawMemberType = def.GetUncheckedAttributeValue("type");
                        var memberType = GetTypeFromXml(rawMemberType);

                        var value = def.GetUncheckedAttributeValue("value");
                        var normalized = NormalizeValue(value);

                        await writer.WriteLineAsync($"    public const {memberType} {cleanMemberName} = {normalized};");
                    }

                    await writer.WriteLineAsync("}");
                }

                constsWriteTask.Increment(1);
                writeMaster.Increment(1);

                var enumWriteTask = ctx.AddTask("Writing enums", maxValue: enums.Count, autoStart: false);
                taskTypes[enumWriteTask.Id] = TaskType.Number;
                enumWriteTask.StartTask();

                foreach (var vulkanEnum in enums) {
                    await using var file = File.Create(Path.Combine("autogen", "enums", $"{vulkanEnum.Name}.cs"));
                    await using var writer = new StreamWriter(file);

                    await writer.WriteLineAsync(prologue);
                    await writer.WriteLineAsync("");
                    await writer.WriteLineAsync($"[GeneratedCode(\"Caldera\", \"{version}\")]");
                    await writer.WriteLineAsync($"public enum {vulkanEnum.Name} : {vulkanEnum.UnderlyingType} {{");

                    foreach (var value in vulkanEnum.Values) {
                        await writer.WriteLineAsync($"    {value.Name} = {value.Value},");
                    }

                    await writer.WriteLineAsync("}");

                    enumWriteTask.Increment(1);
                }

                writeMaster.Increment(1);
            });
    }

    private static string CleanEnumName(string raw) {
        return raw.StartsWith("Vk") ? raw[2..] : raw;
    }

    private static string CleanEnumValue(string raw) {
        return raw.StartsWith("VK_") ? raw[3..] : raw;
    }

    private static string NormalizeValue(string raw) {
        return raw.Replace("LL", "L");
    }

    private static string GetTypeFromXml(string xmlType) {
        return xmlType switch {
            "uint32_t" => "uint",
            "uint64_t" => "ulong",
            "float" => "float",

            _ => throw new ArgumentOutOfRangeException(nameof(xmlType), xmlType, $"Unrecognized type '{xmlType}' provided."),
        };
    }
}