using System.Xml;
using System.Xml.Linq;

namespace Caldera.Core;

public static class XObjectExtensions {
    public static SourceSpan GetSpan(this XObject node) => node is IXmlLineInfo li
        ? new SourceSpan(li.LineNumber, li.LinePosition)
        : SourceSpan.Unknown;
}