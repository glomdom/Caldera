using Spectre.Console;
using Spectre.Console.Rendering;

namespace Caldera.Cli.Columns;

public enum TaskType {
    Download,
    Number,
}

public sealed class MetricsColumn(Dictionary<int, TaskType> taskTypes) : ProgressColumn {
    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime) {
        if (!task.IsStarted) {
            return new Markup("[grey]???[/]");
        }
        
        if (taskTypes.TryGetValue(task.Id, out var type) && type == TaskType.Download) {
            var downloaded = task.Value / 1024.0 / 1024.0;
            var total = task.MaxValue / 1024.0 / 1024.0;

            return new Markup($"[cyan]{downloaded:0.00} MB[/] / [green]{total:0.00} MB[/]");
        }

        return new Markup($"[cyan]{task.Value}[/] / [green]{task.MaxValue}[/]");
    }
}