using Caldera.Cli.Models;

namespace Caldera.Cli.Parsing;

public sealed class ParseContext {
    public Dictionary<string, string> BaseTypes { get; } = [];
    public Dictionary<string, VulkanFunctionPointer> FunctionPointers { get; } = [];
    public Dictionary<string, string> Aliases { get; } = [];
    public List<string> BlockedTypes { get; } = [
        // video related
        "VkVideoEncodeH264SessionParametersAddInfoKHR",
        "VkVideoEncodeH265SessionParametersAddInfoKHR",
        "VkVideoDecodeH264SessionParametersAddInfoKHR",
        "VkVideoDecodeH265SessionParametersAddInfoKHR",
    ];

    public List<string> BlockedRequires { get; init; } = [
        "zircon/types.h",
        "ggp_c/vulkan_types.h",
        "screen/screen.h",
        "nvscisync.h", "nvscibuf.h",
        "directfb.h",
        "ubm.h",
        "vk_video/vulkan_video_codec_av1std.h", // todo: parse video.xml
        "vk_video/vulkan_video_codec_av1std_encode.h",
        "vk_video/vulkan_video_codec_av1std_decode.h",
        "vk_video/vulkan_video_codec_vp9std.h",
        "vk_video/vulkan_video_codec_vp9std_encode.h",
        "vk_video/vulkan_video_codec_vp9std_decode.h",
        "vk_video/vulkan_video_codec_h264std.h",
        "vk_video/vulkan_video_codec_h264std_encode.h",
        "vk_video/vulkan_video_codec_h264std_decode.h",
        "vk_video/vulkan_video_codec_h265std.h",
        "vk_video/vulkan_video_codec_h265std_encode.h",
        "vk_video/vulkan_video_codec_h265std_decode.h",
    ];

    public List<string> BlockedNames { get; init; } = [
        "ANativeWindow", "AHardwareBuffer", "OHNativeWindow", "OHBufferHandle", "OH_NativeBuffer",
    ];
}