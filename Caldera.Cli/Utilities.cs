using System.Text.RegularExpressions;
using System.Xml.Linq;
using Caldera.Cli.Parsing;
using Serilog;

namespace Caldera.Cli;

public static class Utilities {
    public static string GetTypeFromXml(string xmlType) => xmlType switch {
        "char" => "byte",
        "uint8_t" => "byte",
        "int8_t" => "sbyte",
        "uint16_t" => "ushort",
        "int16_t" => "short",
        "uint32_t" => "uint",
        "uint64_t" => "ulong",
        "int32_t" => "int",
        "int64_t" => "long",
        "size_t" => "nuint",
        "float" => "float",
        "void" => "void",
        
        // windows specific
        "HANDLE" => "nint",
        "HWND" => "nint",
        "HINSTANCE" => "nint",
        "HMONITOR" => "nint",
        "SECURITY_ATTRIBUTES" => "nint",
        "DWORD" => "uint",
        "LPCWSTR" => "nint",
        
        // x11 specific
        "xcb_connection_t" => "nint",
        "xcb_window_t" => "uint",
        "Display" => "nint",
        "Window" => "nuint",
        
        // wayland specific
        "wl_display" => "nint",
        "wl_surface" => "nint",

        _ => xmlType,
    };
    
    public static int? ResolveArrayLength(XElement member, ParseContext ctx) {
        var dims = new List<int>();

        foreach (var enumEl in member.Elements("enum")) {
            var constName = NameCleaning.CleanEnumValue(enumEl.Value);
            if (!ctx.Constants.TryGetValue(constName, out var val)) {
                throw new InvalidOperationException($"Array member references unknown constant '{constName}'");
            }

            Log.Debug("Replacing constant {ConstantName} with {ConstantValue}", constName, val.Value);

            dims.Add(Convert.ToInt32(val.Value));
        }

        var tail = string.Concat(
            member.Nodes()
                .SkipWhile(n => !(n is XElement e && e.Name == "name"))
                .Skip(1)
                .OfType<XText>()
                .Select(t => t.Value));

        foreach (Match m in Regex.Matches(tail, @"\[(\d+)\]")) {
            dims.Add(int.Parse(m.Groups[1].Value));
        }

        if (dims.Count == 0) return null;

        return dims.Aggregate(1, (a, b) => a * b);
    }
}