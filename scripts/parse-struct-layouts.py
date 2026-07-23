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

    fields_re = re.compile(
        r"^\s*(\d+)\s*\|\s+(.+?)\s+(\w+)(\[\d+\])?\s*$"
    )  # matches : num | type name

    size_re = re.compile(r"\[sizeof=(\d+),\s*align=(\d+)\]")
    head_re = re.compile(r"struct\s+(\w+)\b")

    with open(path, encoding="utf-8", errors="replace") as fp:
        for line in fp:
            if "Dumping AST Record Layout" in line:
                cur = None
                print(". Starting struct parsing")

                continue

            m = head_re.search(line)
            if m and "|" in line and line.split("|")[0].strip() == "0":  # top of struct
                print(f"+ Got struct {m.group(1)}")

                cur = {"name": m.group(1), "fields": []}
                structs[m.group(1)] = cur

                continue

            if cur is None:
                continue

            s = size_re.search(line)
            if s:
                cur["size"] = int(s.group(1))
                cur["align"] = int(s.group(1))

                print(f"+ With sizeof={cur['size']} and align={cur['align']}")

                cur = None
                continue

            f = fields_re.match(line)
            if f and "|" in line:
                off = int(f.group(1))
                name = f.group(3)
                cur["fields"].append({"name": name, "offset": off})

        return {k: v for k, v in structs.items() if "size" in v}


if __name__ == "__main__":
    main()
