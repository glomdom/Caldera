import json


def norm(name):
    return name[2:] if name.startswith("Vk") else name


c = {norm(k): v for k, v in json.load(open("layouts.json")).items()}
n = json.load(open("cs_layouts.json"))

bad = 0
for name, ref in c.items():
    if name not in n:
        continue

    got = n[name]
    if got["size"] != ref["size"]:
        print(f"FAIL SIZE  {name}: C#={got['size']} clang={ref['size']}")

        bad += 1

    ro = {f["name"]: f["offset"] for f in ref["fields"]}
    go = {f["name"]: f["offset"] for f in got["fields"]}
    for fld, off in ro.items():
        if fld in go and go[fld] != off:
            print(f"FAIL OFFS  {name}.{fld}: C#={go[fld]} clang={off}")

            bad += 1

print("PASS" if bad == 0 else f"{bad} mismatches")
