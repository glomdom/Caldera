using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (args.Length < 2) {
    Console.WriteLine("Usage: layoutdump <dll> <out dir>");

    Environment.Exit(-1);
}

var asm = Assembly.LoadFrom(args[0]);
var result = new Dictionary<string, object>();

foreach (var t in asm.GetTypes()) {
    if (!t.IsValueType || t.IsEnum || t.IsGenericType) continue;

    try {
        var size = Marshal.SizeOf(t);
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => new {
            name = f.Name,
            offset = (int)Marshal.OffsetOf(t, f.Name),
        }).ToList();

        result[t.Name] = new { size, fields };

        Console.WriteLine($"+ {t.Name} has size {size}");
    } catch {
        // ignore
    }
}

File.WriteAllText(Path.Combine(args[1], "cs_layouts.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions() { WriteIndented = true }));
Console.WriteLine($"+ Saved {result.Count} structs to cs_layouts.json");