using System.Xml.Linq;
using Caldera.Core.Diagnostics;
using Caldera.Core.Models;
using Caldera.Extensions;

namespace Caldera.Core;

public sealed class BaseTypeReader(DiagnosticBag diagnostics, XDocument doc) : RawReader<RawBaseType>(diagnostics, doc) {
    public override IReadOnlyList<RawBaseType> Read() {
        var nodes = Document.Descendants("type")
            .Where(x => x.Attribute("category")?.Value == "basetype")
            .Where(x => x.HasElement("name"))
            .ToList();

        var result = nodes.Select(CreateBaseType).ToList();

        return result;
    }

    private static RawBaseType CreateBaseType(XElement elem) {
        var name = elem.Element("name")!.Value;
        var typeElem = elem.Element("type");

        var suffix = (typeElem?.NextNode as XText)?.Value.Trim();
        var lead = (typeElem?.LastNode as XText)?.Value.Trim(); // effectively the underlying type

        var type = new RawBaseType(
            Name: name,
            UnderlyingType: lead,
            RawText: elem.Value,
            Span: elem.GetSpan(),
            TypeSuffix: suffix
        );

        return type;
    }
}