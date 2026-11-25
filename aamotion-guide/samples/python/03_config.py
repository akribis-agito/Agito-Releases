"""03-config: the controller address, model, axis and motion values come from machine.ini, not from the code.

    python 03_config.py                 reads ../machine.ini (samples/machine.ini)
    python 03_config.py D:\\cell2.ini
"""
import configparser
import os
import sys

import aamotion as am

# 输出被重定向时，编码不了的字符（如中文路径）转义输出，不报错；
# IDLE、Jupyter 的输出没有 reconfigure
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(errors="backslashreplace")

# --8<-- [start:settings]
def load_settings(path):
    """Reads and checks machine.ini; raises ValueError naming the bad key, so a typo fails at start-up."""
    ini = configparser.ConfigParser(inline_comment_prefixes=(";", "#"))
    if not ini.read(path, encoding="utf-8"):
        raise ValueError(f"{path}: not found")

    def number(section, key, lo, hi):
        try:
            v = ini.getfloat(section, key)
        except (configparser.Error, ValueError) as e:
            raise ValueError(f"machine.ini: {section}.{key}: {e}") from None
        if not lo <= v <= hi:
            raise ValueError(f"machine.ini: {section}.{key} = {v} must be from {lo} to {hi}")
        return v

    def constant(prefix, section, key):
        # "AGD155EC" and "AGD155_EC" both name CONTROLLER_TYPE_AGD155_EC
        name = ini.get(section, key, fallback="").strip().upper().replace("_", "")
        for attr in dir(am):
            if attr.startswith(prefix) and attr[len(prefix):].replace("_", "") == name and name:
                return getattr(am, attr)
        raise ValueError(f"machine.ini: {section}.{key} = {name!r} is not a known {prefix.rstrip('_').lower()}")

    return {
        "target": ini.get("controller", "target"),
        "model_name": ini.get("controller", "model").strip().upper(),
        "model": constant("CONTROLLER_TYPE_", "controller", "model"),
        "axis": constant("AXIS_REF_", "motion", "axis"),
        "distance": number("motion", "distance", -1e9, 1e9),
        "speed": number("motion", "speed", 1, 1e9),
        "accel": number("motion", "accel", 1, 1e12),
    }
# --8<-- [end:settings]


path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "machine.ini")
try:
    s = load_settings(path)
except (ValueError, configparser.Error) as e:
    sys.exit(str(e))

# --8<-- [start:use]
h = am.initialize(s["model"])
if not h:
    sys.exit("initialize failed: is AACommServerNative beside the library?")
if s["target"].lower().endswith(".exe"):
    ok = am.lib.aamotion_connect_simulation(h, s["target"].encode(), s["model_name"].encode(), None, None)
else:
    ok = am.lib.aamotion_connect(h, s["target"].encode(), False, False)
if ok:
    pos = am.get_value(h, s["axis"], am.KEYWORD_POS)
    print(f"Pos = {pos}; would move {s['distance']} at {s['speed']} (accel {s['accel']})")
else:
    print(f"connect {s['target']} failed")
am.lib.aamotion_disconnect(h)
# --8<-- [end:use]
