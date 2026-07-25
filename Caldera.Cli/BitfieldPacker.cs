using Caldera.Cli.Models;
using Serilog;

namespace Caldera.Cli;

public abstract record StructLayoutItem;
public sealed record PlainItem(VulkanStructMember Member) : StructLayoutItem;
public sealed record BitfieldSlot(string Name, string Type, int BitOffset, int BitWidth);

public sealed record BitfieldUnit(int Index, string StorageType, int StorageBits, List<BitfieldSlot> Slots) : StructLayoutItem {
    public string FieldName => $"_bits{Index}";
}

public static class BitfieldPacker {
    public static List<StructLayoutItem> Pack(IReadOnlyList<VulkanStructMember> members, Func<string, (string Storage, int Bits)>? resolve = null) {
        resolve ??= DefaultStorage;

        var items = new List<StructLayoutItem>();
        BitfieldUnit? open = null;
        int cursor = 0, nextIndex = 0;

        foreach (var m in members) {
            if (m.BitWidth is not { } width) {
                open = null;
                cursor = 0;
                items.Add(new PlainItem(m));

                continue;
            }

            var (storage, bits) = resolve($"{m.Type}");

            if (open is null || open.StorageBits != bits || cursor + width > bits) {
                open = new BitfieldUnit(nextIndex++, storage, bits, []);
                cursor = 0;
                items.Add(open);
            }

            open.Slots.Add(new BitfieldSlot(m.Name, $"{m.Type}", cursor, width));
            cursor += width;
        }

        return items;
    }

    public static IEnumerable<string> EmitBody(IReadOnlyList<StructLayoutItem> items, Func<VulkanStructMember, string> renderPlain) {
        foreach (var item in items) {
            switch (item) {
                case PlainItem p: {
                    yield return renderPlain(p.Member);

                    break;
                }

                case BitfieldUnit u: {
                    var packed = string.Join(" | ", u.Slots.Select(s => $"{s.Name}:{s.BitWidth}"));
                    yield return $"    private {u.StorageType} {u.FieldName};   // {packed}";

                    break;
                }
            }
        }

        foreach (var unit in items.OfType<BitfieldUnit>()) {
            foreach (var slot in unit.Slots) {
                yield return "";

                foreach (var line in Property(unit, slot)) {
                    yield return line;
                }
            }
        }
    }

    private static IEnumerable<string> Property(BitfieldUnit unit, BitfieldSlot slot) {
        var field = unit.FieldName;
        var mask = MaskLiteral(unit.StorageBits, slot.BitWidth);
        var shift = slot.BitOffset == 0 ? "" : $" << {slot.BitOffset}";
        var cast = slot.Type != unit.StorageType;

        var read = slot.BitOffset == 0
            ? $"{field} & {mask}"
            : $"({field} >> {slot.BitOffset}) & {mask}";

        if (cast) {
            read = $"({slot.Type})({read})";
        }

        var value = cast ? $"({unit.StorageType})value" : "value";

        yield return $"    public {slot.Type} {slot.Name} {{";
        yield return $"        readonly get => {read};";
        yield return $"        set => {field} = ({field} & ~({mask}{shift})) | (({value} & {mask}){shift});";
        yield return "    }";
    }

    private static (string Storage, int Bits) DefaultStorage(string type) {
        if (type is not ("uint" or "int") && !type.Contains("Flags", StringComparison.Ordinal)) {
            Log.Warning("Bitfield member of unexpected type {Type}, assuming 32-bit storage", type);
        }

        return ("uint", 32);
    }

    private static string MaskLiteral(int storageBits, int width) {
        if (storageBits <= 32) {
            var m = width >= 32 ? uint.MaxValue : (1u << width) - 1;

            return $"0x{m:X}u";
        }

        var m64 = width >= 64 ? ulong.MaxValue : (1UL << width) - 1;

        return $"0x{m64:X}UL";
    }
}