import json
import optparse
import re


def main():
    parser = optparse.OptionParser()
    parser.add_option(
        "-l",
        "--layouts",
        dest="layouts",
        help="path to layouts.txt",
        default="./layouts.txt",
    )

    (options, args) = parser.parse_args()

    data = parse_layouts(options.layouts)
    json.dump(data, open("layouts.json", "w"), indent=2)

    print(f"+ Parsed {len(data)} structs")


def parse_layouts(path):
    structs = {}
    cur = None

    field_re = re.compile(r"^\s*(\d+)\s*\|(\s+)(.+?)\s+(\w+)(\[\d+\])?\s*$")
    size_re = re.compile(r"^\s*\|\s*\[sizeof=(\d+),\s*align=(\d+)\]")
    head_re = re.compile(r"^\s*0\s*\|\s*(?:struct|class|union)\s+(\w+)\b")

    with open(path, encoding="utf-8", errors="replace") as fp:
        for line in fp:
            if "Dumping AST Record Layout" in line:
                cur = None

                continue

            h = head_re.match(line)
            if h and cur is None:
                cur = {"name": h.group(1), "fields": []}
                structs[h.group(1)] = cur

                continue

            if cur is None:
                continue

            s = size_re.match(line)
            if s and "size" not in cur:
                cur["size"] = int(s.group(1))
                cur["align"] = int(s.group(2))
                cur = None

                continue

            m = field_re.match(line)
            if not m:
                continue

            indent = len(m.group(2))
            if "indent" not in cur:
                cur["indent"] = indent

            if indent != cur["indent"]:
                continue

            cur["fields"].append({"name": m.group(4), "offset": int(m.group(1))})

    out = {}
    for k, v in structs.items():
        if "size" in v:
            v.pop("indent", None)
            out[k] = v

    return out


if __name__ == "__main__":
    main()
