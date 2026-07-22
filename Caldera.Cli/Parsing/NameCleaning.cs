namespace Caldera.Cli.Parsing;

public static class NameCleaning {
    public static string CleanEnumName(string raw) {
        return raw switch {
            "object" => "@object",
            "event" => "@event",

            _ => raw.StartsWith("Vk") ? raw[2..] : raw,
        };
    }

    public static string CleanEnumValue(string raw) => raw.StartsWith("VK_") ? raw[3..] : raw;
    public static string NormalizeValue(string raw) => raw.Replace("LL", "L");
}