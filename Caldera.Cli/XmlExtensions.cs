using System.Xml.Linq;

namespace Caldera.Cli;

public static class XmlExtensions {
    extension(XElement elem) {
        public string GetUncheckedAttributeValue(string attr) {
            return elem.Attribute(attr)!.Value;
        }

        public string GetCheckedAttributeValue(string attr) {
            var result = elem.Attribute(attr)?.Value;

            return result ?? throw new InvalidDataException($"Element '{elem.Name}' does not have attribute '{attr}'");
        }

        public string? MaybeGetAttributeValue(string attr) {
            return elem.Attribute(attr)?.Value;
        }
    }
}