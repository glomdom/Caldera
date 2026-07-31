using System.Xml.Linq;

namespace Caldera.Core;

public static class XElementExtensions {
    public static T GetNextNode<T>(this XElement elem) where T : XNode =>
        elem.NextNode as T ?? throw new InvalidOperationException($"Could not cast next node of '{elem.Name}' to '{typeof(T).Name}'");
    
    public static T GetFirstNode<T>(this XElement elem) where T : XNode =>
        elem.FirstNode as T ?? throw new InvalidOperationException($"Could not cast first node of '{elem.Name}' to '{typeof(T).Name}'");
}