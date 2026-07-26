using System.Xml.Linq;
using Caldera.Core.Diagnostics;

namespace Caldera.Core;

public sealed class StructReader(DiagnosticBag diagnostics) : RawReader<string>(diagnostics) {
    public override IReadOnlyList<string> Read(XDocument doc) {
        return ["balls"];
    }
}