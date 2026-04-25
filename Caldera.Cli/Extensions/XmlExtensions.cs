using System.Xml.Linq;

namespace Caldera.Cli.Extensions;

public static class XmlExtensions {
    extension(XElement elem) {
        public string GetUncheckedAttributeValue(string attr) {
            return elem.Attribute(attr)!.Value;
        }

        public string GetAttributeValue(string attr) {
            var result = elem.Attribute(attr)?.Value;

            return result ?? throw new InvalidDataException($"Element '{elem.Name}' does not have attribute '{attr}'");
        }

        public string? MaybeGetAttributeValue(string attr) {
            return elem.Attribute(attr)?.Value;
        }

        public XElement GetElement(string name) {
            return elem.Element(name) ?? throw new InvalidDataException($"Element '{elem.Name}' is missing element '{name}'");
        }

        public string GetElementValue(string name) {
            return elem.GetElement(name).Value;
        }
    }
}