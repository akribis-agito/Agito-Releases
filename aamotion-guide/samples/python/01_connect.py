"""01-connect: connect to a controller or to the firmware simulator, read the position, disconnect.

    python 01_connect.py 172.1.1.101
    python 01_connect.py "D:\\...\\firmware_sim.exe"
"""
import sys

import aamotion as am

# 输出被重定向时，编码不了的字符（如中文路径）转义输出，不报错；
# IDLE、Jupyter 的输出没有 reconfigure
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(errors="backslashreplace")
target = sys.argv[1] if len(sys.argv) > 1 else "172.1.1.101"

# --8<-- [start:init]
@am.ERROR_CB
def on_error(code, message, exception, user):
    print(f"[ERR {code}] {(message or b'').decode()} | {(exception or b'').decode()}")

h = am.initialize(am.CONTROLLER_AGM800)
if not h:
    sys.exit("initialize failed: is AACommServerNative beside the library?")
am.lib.aamotion_set_error_occurred_handler(h, on_error, None)  # keep on_error referenced
# --8<-- [end:init]

# --8<-- [start:connect]
if target.lower().endswith(".exe"):
    ok = am.lib.aamotion_connect_simulation(h, target.encode(), b"AGM800", None, None)
else:
    ok = am.lib.aamotion_connect(h, target.encode(), False, False)
print(f"Connect {target}: {ok}")
# --8<-- [end:connect]

if ok:
    # --8<-- [start:read]
    print("APos =", am.get_value(h, am.AXIS_A, am.KEYWORD_POS))
    # --8<-- [end:read]

# --8<-- [start:disconnect]
am.lib.aamotion_disconnect(h)
# --8<-- [end:disconnect]
