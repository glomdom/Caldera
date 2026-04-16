namespace Caldera.Cli.Extensions;

public static class StringExtensions {
    extension(string str) {
        public string CleanName() {
            return Utilities.CleanEnumName(str);
        }

        public string CleanFunctionPointerName() {
            return str.StartsWith("PFN_vk") ? str[6..] : str;
        }
    }
}