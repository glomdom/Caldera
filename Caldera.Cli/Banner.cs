using Spectre.Console;

namespace Caldera.Cli;

public static class Banner {
    public static void PrintBanner() {
        var lines = Constants.Banner.Split('\n');
        for (var i = 0; i < lines.Length; i++) {
            var ratio = lines.Length > 1 ? (double)i / (lines.Length - 1) : 0;
            var r = (byte)(255 - (255 - 180) * ratio);
            var g = (byte)(165 - (165 - 50) * ratio);

            AnsiConsole.MarkupLine($"[rgb({r},{g},0)]{lines[i]}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[gray70]A C# Vulkan bindings forge.[/]");
        AnsiConsole.WriteLine();
    }
}