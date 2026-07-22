namespace Caldera.Cli;

public static class Utilities {
    public static string GetTypeFromXml(string xmlType) => xmlType switch {
        "int32_t" => "int",
        "uint8_t" => "byte",
        "uint32_t" => "uint",
        "uint64_t" => "ulong",
        "int64_t" => "long",
        "size_t" => "nuint",
        "float" => "float",
        "void" => "void",

        _ => xmlType,
    };
}