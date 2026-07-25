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
    bitfield_re = re.compile(r"^\s*(\d+):(\d+)-(\d+)\s*\|(\s+)(.+?)\s+(\w+)\s*$")
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

            b = bitfield_re.match(line)
            if b:
                if not accept(cur, len(b.group(4))):
                    continue

                first, last = int(b.group(2)), int(b.group(3))
                cur["fields"].append(
                    {
                        "name": b.group(6),
                        "offset": int(b.group(1)),
                        "bitfield": True,
                        "bit_offset": first,
                        "bit_width": last - first + 1,
                    }
                )

                continue

            m = field_re.match(line)
            if m:
                if not accept(cur, len(m.group(2))):
                    continue

                cur["fields"].append({"name": m.group(4), "offset": int(m.group(1))})

    out = {}
    for k, v in structs.items():
        if "size" in v:
            v.pop("indent", None)
            out[k] = v

    return out


def accept(cur, indent):
    if "indent" not in cur:
        cur["indent"] = indent

    return indent == cur["indent"]


if __name__ == "__main__":
    main()
