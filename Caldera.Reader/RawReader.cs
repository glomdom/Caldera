using System.Xml.Linq;
using Caldera.Core.Diagnostics;

namespace Caldera.Core;

public abstract class RawReader<T>(DiagnosticBag diagnostics, XDocument doc) {
    protected DiagnosticBag Diagnostics { get; } = diagnostics;
    protected XDocument Document { get; } = doc;

    public abstract IReadOnlyList<T> Read();

    protected string? RequiredAttr(XElement el, string name) {
        var attr = el.Attribute(name);
        if (attr is null) {
            Diagnostics.Error(el.GetSpan(), DiagnosticCode.RequiredAttributeNotSet, $"<{el.Name}> is missing required attribute '{name}'");
        }

        return attr?.Value;
    }
}