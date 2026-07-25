using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

if (args.Length < 2) {
    Console.WriteLine("Usage: layoutdump <dll> <out dir> [pointer-size]");
    Environment.Exit(-1);
}

var pointerSize = args.Length > 2 ? int.Parse(args[2]) : 8; // x86_64 default
var layout = new LayoutComputer(pointerSize);

var asm = Assembly.LoadFrom(args[0]);
var result = new Dictionary<string, object>();
var skipped = new List<string>();

foreach (var t in asm.GetTypes()) {
    if (!t.IsValueType || t.IsEnum || t.IsGenericType) continue;
    if (LayoutComputer.IsInlineArray(t, out _, out _)) continue;

    try {
        var (fields, size, align) = layout.Layout(t);
        result[t.Name] = new { size, align, fields };

        if (fields.Count == 0)
            Console.WriteLine($"! {t.Name} has NO fields - handle type missing its backing field?");
        else
            Console.WriteLine($"+ {t.Name} has size {size} align {align}");
    } catch (Exception ex) {
        skipped.Add($"{t.Name}: {ex.Message}");
    }
}

File.WriteAllText(Path.Combine(args[1], "cs_layouts.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"+ Saved {result.Count} structs to cs_layouts.json");

if (skipped.Count > 0) {
    Console.WriteLine($"! Skipped {skipped.Count}:");

    foreach (var s in skipped) Console.WriteLine($"  - {s}");
}

sealed class LayoutComputer {
    private readonly int _pointerSize;
    private readonly Dictionary<Type, (int Size, int Align)> _cache = new();

    public LayoutComputer(int pointerSize) => _pointerSize = pointerSize;

    public sealed record FieldLayout(string name, int offset, int size, int align);

    public (List<FieldLayout> Fields, int Size, int Align) Layout(Type t) {
        var fields = new List<FieldLayout>();
        int offset = 0, structAlign = 1;

        foreach (var f in DeclarationOrder(t)) {
            var (fs, fa) = SizeAndAlign(f.FieldType);
            offset = Align(offset, fa);
            fields.Add(new FieldLayout(CleanFieldName(f.Name), offset, fs, fa));
            offset += fs;
            structAlign = Math.Max(structAlign, fa);
        }

        return fields.Count == 0 ? (fields, 1, 1) : (fields, Align(offset, structAlign), structAlign);
    }

    public (int Size, int Align) SizeAndAlign(Type t) {
        if (_cache.TryGetValue(t, out var hit)) return hit;

        var r = Compute(t);
        _cache[t] = r;

        return r;
    }

    private (int, int) Compute(Type t) {
        if (t.IsPointer || t.IsByRef || t == typeof(IntPtr) || t == typeof(UIntPtr))
            return (_pointerSize, _pointerSize);

        if (t.IsEnum)
            return SizeAndAlign(Enum.GetUnderlyingType(t));

        // [InlineArray(N)] over element E -> size N*sizeof(E), align alignof(E)
        if (IsInlineArray(t, out var length, out var element)) {
            var (es, ea) = SizeAndAlign(element!);

            return (checked(es * length), ea);
        }

        var prim = Primitive(t);
        if (prim is not null) return prim.Value;

        if (t.IsValueType) {
            var (_, size, align) = Layout(t);

            return (size, align);
        }

        throw new NotSupportedException($"cannot size reference type {t.FullName}");
    }

    private (int, int)? Primitive(Type t) => Type.GetTypeCode(t) switch {
        TypeCode.Boolean or TypeCode.SByte or TypeCode.Byte => (1, 1),
        TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Char => (2, 2),
        TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => (4, 4),
        TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double => (8, 8),
        _ => null,
    };

    public static bool IsInlineArray(Type t, out int length, out Type? element) {
        length = 0;
        element = null;

        var attr = t.GetCustomAttribute<InlineArrayAttribute>();
        if (attr is null) return false;

        var backing = t.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (backing.Length != 1) return false;

        length = attr.Length;
        element = backing[0].FieldType;

        return true;
    }

    private static FieldInfo[] DeclarationOrder(Type t) =>
        t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(f => f.MetadataToken)
            .ToArray();

    private static string CleanFieldName(string name) {
        var close = name.IndexOf('>');

        return name.StartsWith('<') && close > 1 ? name[1..close] : name;
    }

    private static int Align(int value, int align) => (value + align - 1) / align * align;
}