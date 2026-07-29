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

    parser.add_option(
        "-s", "--sdk-include", dest="sdk_include", help="vulkan sdk include directory"
    )

    (options, args) = parser.parse_args()

    cmd = f"clang -Xclang -fdump-record-layouts-complete -fsyntax-only -target {options.target} -I{options.sdk_include} {options.sdk_include}/vulkan/vulkan_core.h > layouts.txt 2>&1"

    print(f"+ {cmd}")
    subprocess.run(cmd, shell=True, check=False)


if __name__ == "__main__":
    main()
