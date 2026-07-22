using System.Text;

namespace Caldera.Cli;

public static class VkDownloader {
    private static readonly HttpClient Client = new();
    
    public static async Task<string> DownloadVkXmlAsync() {
        using var response = await Client.GetAsync(
            "https://raw.githubusercontent.com/KhronosGroup/Vulkan-Docs/main/xml/vk.xml",
            HttpCompletionOption.ResponseHeadersRead
        );

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();

        return Encoding.UTF8.GetString(bytes);
    }
}