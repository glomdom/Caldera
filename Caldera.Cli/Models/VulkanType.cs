namespace Caldera.Cli.Models;

public sealed record VulkanType(string Type, bool IsPointer) {
    /// <summary>
    /// Uses the <c>NameSource</c> and <c>ParentSource</c> params
    /// to check for pointers, and sets it.
    /// </summary>
    /// <param name="nameSource">The string containing the name of the type.</param>
    /// <param name="parentSource">The string of the entire element source containing the child name.</param>
    public VulkanType(string nameSource, string parentSource) : this(nameSource, HasStar(parentSource)) { }

    public VulkanType(string nameSource, string parentSource, Dictionary<string, string> lookupTable) : this(GetName(nameSource, lookupTable), HasStar(parentSource)) { }
    public VulkanType(string nameSource, string parentSource, Dictionary<string, VulkanFunctionPointer> lookupTable) : this(LookupFunction(nameSource, lookupTable), HasStar(parentSource)) { }

    private static bool HasStar(string parent) => parent.Contains('*');

    private static string LookupFunction(string name, Dictionary<string, VulkanFunctionPointer> lookupTable) {
        if (lookupTable.TryGetValue(name, out var pfn)) {
            return pfn.ToString();
        }

        throw new ArgumentOutOfRangeException(nameof(name), name, "Provided function pointer name was not found in lookup table.");
    }
    
    private static string GetName(string name, Dictionary<string, string> lookupTable) {
        return name == "PFN_vkVoidFunction" ? "delegate* unmanaged[Cdecl]<void>" : lookupTable.GetValueOrDefault(name, name);
    }

    public override string ToString() => $"{Type}{(IsPointer ? "*" : "")}";
}