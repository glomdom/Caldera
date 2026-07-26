using System.Xml.Linq;
using Caldera.Core;
using Caldera.Core.Diagnostics;

var reader = new StructReader(new DiagnosticBag());
var v = reader.Read(new XDocument());

foreach (var x in v) {
    Console.WriteLine(x);
}