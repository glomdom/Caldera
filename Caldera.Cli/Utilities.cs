namespace Caldera.Cli;

public static class Utilities {
    public static string GetTypeFromXml(string xmlType) => xmlType switch {
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
}