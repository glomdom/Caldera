using Caldera.Cli.Generation;
using Caldera.Cli.Parsing;
using Caldera.Cli.Writing;
using Serilog;
using Spectre.Console;

namespace Caldera.Cli;

public static class Program {
    public static async Task Main(string[] args) {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .CreateLogger();

        var version = Metadata.GetAssemblyVersion();

        Banner.PrintBanner();

        if (args.Length < 1) {
            Console.MarkupLine("[red]error:[/] missing vk.xml path argument");
            
            Environment.Exit(-1);
        }

        var xmlString = await File.ReadAllTextAsync(args[0]);

        Log.Information("Parsing definitions from vk.xml");
        var registry = RegistryParser.ParseFrom(xmlString);

        Log.Information("Writing C# definitions");
        await Writers.WriteDefinitionsAsync(registry, version);

        Log.Information("Generated files can be found in {Location}", Path.Combine(Directory.GetCurrentDirectory(), "autogen"));

        await Log.CloseAndFlushAsync();
    }
}