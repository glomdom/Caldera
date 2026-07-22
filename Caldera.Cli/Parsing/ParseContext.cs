using Caldera.Cli.Models;

namespace Caldera.Cli.Parsing;

public sealed class ParseContext {
    public Dictionary<string, string> BaseTypes { get; } = [];
    public Dictionary<string, VulkanFunctionPointer> FunctionPointers { get; } = [];
}