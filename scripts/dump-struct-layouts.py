import optparse
import subprocess


def main():
    parser = optparse.OptionParser()
    parser.add_option(
        "-t",
        "--target",
        dest="target",
        help="target triplet to use for clang",
        default="x86_64-pc-windows-msvc",
    )

    (options, args) = parser.parse_args()

    cmd = f"clang -Xclang -fdump-record-layouts-complete -fsyntax-only -target {options.target} -IC:/VulkanSDK/1.4.350.0/Include C:/VulkanSDK/1.4.350.0/Include/vulkan/vulkan_core.h > layouts.txt 2>&1"

    print(f"+ {cmd}")
    subprocess.run(cmd, shell=True)


if __name__ == "__main__":
    main()
