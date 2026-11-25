"""02-first-move: connect, commutate, motor on, move axis A and wait in target, motor off, disconnect.

    python 02_first_move.py "D:\\sim\\AGM800\\firmware_sim.exe"
    python 02_first_move.py 172.1.1.101       (real controller: make sure axis A may move 10000)
"""
import sys
import time

import aamotion as am

# 输出被重定向时，编码不了的字符（如中文路径）转义输出，不报错；
# IDLE、Jupyter 的输出没有 reconfigure
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(errors="backslashreplace")
A = am.AXIS_A
DISTANCE, SPEED, ACCEL = 10000.0, 20000.0, 200000.0
target = sys.argv[1] if len(sys.argv) > 1 else "172.1.1.101"


@am.ERROR_CB
def on_error(code, message, exception, user):
    print(f"[ERR {code}] {(message or b'').decode()} | {(exception or b'').decode()}")


# --8<-- [start:wait]
def wait_in_target(h, timeout_s=10.0):
    """Read live status; 4 means settled. The current SDK cache handles old cached states."""
    end = time.monotonic() + timeout_s
    while time.monotonic() < end:
        stat = am.get_value(h, A, am.KEYWORD_IN_TARGET_STAT)
        if stat is None:
            return False
        if stat == am.IN_TARGET_STAT_TARGET_REACHED:
            return True
        time.sleep(0.05)
    return False
# --8<-- [end:wait]


# --8<-- [start:connect]
h = am.initialize(am.CONTROLLER_AGM800)
if not h:
    sys.exit("initialize failed: is AACommServerNative beside the library?")
am.lib.aamotion_set_error_occurred_handler(h, on_error, None)
if target.lower().endswith(".exe"):
    ok = am.lib.aamotion_connect_simulation(h, target.encode(), b"AGM800", None, None)
else:
    ok = am.lib.aamotion_connect(h, target.encode(), False, False)
print(f"1. connect {target}: {ok}")
if not ok:
    am.lib.aamotion_disconnect(h)
    sys.exit(1)
# --8<-- [end:connect]

rc = 0
try:
    # --8<-- [start:enable]
    if not am.lib.aamotion_is_auto_phase(h, A):
        am.lib.aamotion_auto_phase(h, A)
        end = time.monotonic() + 10
        while not am.lib.aamotion_is_auto_phase(h, A) and time.monotonic() < end:
            time.sleep(0.1)
    commutated = am.lib.aamotion_is_auto_phase(h, A)
    print("2. commutated:", commutated)
    if commutated:
        if not am.lib.aamotion_motor_on(h, A):
            raise RuntimeError("motor-on refused")
        print("3. motor on: MotorOn =", am.get_value(h, A, am.KEYWORD_MOTOR_ON))
    # --8<-- [end:enable]
    if not commutated:
        rc = 3
    else:
        # --8<-- [start:move]
        start = am.get_value(h, A, am.KEYWORD_POS)
        if start is None:
            raise RuntimeError("could not read the starting position")
        if not am.lib.aamotion_move_abs(h, A, start + DISTANCE, SPEED, ACCEL, ACCEL):
            raise RuntimeError("move refused; see the error callback")
        arrived = wait_in_target(h)
        if not arrived:
            am.lib.aamotion_stop(h, A)
        print(f"4. move {start:.0f} -> {start + DISTANCE:.0f}: in target = {arrived}, Pos = {am.get_value(h, A, am.KEYWORD_POS)}")
        rc = 0 if arrived else 4
        # --8<-- [end:move]
finally:
    # --8<-- [start:cleanup]
    am.lib.aamotion_stop(h, A)
    am.lib.aamotion_motor_off(h, A)
    am.lib.aamotion_disconnect(h)
    print("5. motor off, disconnected")
    # --8<-- [end:cleanup]
sys.exit(rc)

