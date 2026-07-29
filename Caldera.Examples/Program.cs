using System.Xml.Linq;
using Caldera.Core;
using Caldera.Core.Diagnostics;

var rawRegistryText = File.ReadAllText(@"C:\VulkanSDK\1.4.350.0\share\vulkan\registry\vk.xml");
var doc = XDocument.Parse(rawRegistryText, LoadOptions.SetLineInfo | LoadOptions.SetBaseUri | LoadOptions.PreserveWhitespace);
var diagnostics = new DiagnosticBag();
var reader = new BaseTypeReader(diagnostics, doc);

var v = reader.Read();

foreach (var diag in diagnostics.Render()) {
    Console.Write(diag);
}

foreach (var type in v) {
    Console.WriteLine(type);
}