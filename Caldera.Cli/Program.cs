using Caldera.Cli.Generation;
using Caldera.Cli.Parsing;
using Caldera.Cli.Writing;
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
        var registry = RegistryParser.ParseFrom(xmlString);

        Log.Information("Writing C# definitions");
        await Writers.WriteDefinitionsAsync(registry, version);

        Log.Information("Generated files can be found in {Location}", Path.Combine(Directory.GetCurrentDirectory(), "autogen"));

        await Log.CloseAndFlushAsync();
    }
}