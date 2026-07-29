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
        var isOpaque = !elem.HasElement("type");
        string? underlying = null;
        string? suffix = null;

        if (!isOpaque) {
            var typeElem = elem.Element("type")!;
            underlying = typeElem.Value;

            suffix = (typeElem.NextNode as XText)?.Value.Trim();
        }

        var type = new RawBaseType(
            Name: name,
            UnderlyingType: underlying,
            RawText: elem.Value,
            Span: elem.GetSpan(),
            TypeSuffix: suffix
        );

        return type;
    }
}