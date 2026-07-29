using System.Xml.Linq;

namespace Caldera.Extensions;

public static class XElementExtensions {
    public static bool HasElement(this XElement elem, string name) => elem.Element(name) is not null;
}