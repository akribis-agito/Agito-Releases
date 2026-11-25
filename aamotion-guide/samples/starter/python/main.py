"""Validate locally, or execute an explicit AGM800 target using the sibling aamotion wrapper."""
import argparse
import json
import math
from pathlib import Path
import sys
import time


def load(path):
    s = json.loads(Path(path).read_text(encoding="utf-8"))
    if s.get("goal") not in ("inputs", "move") or s.get("axis") not in ("A", "B"):
        raise ValueError("Unknown goal or axis")
    for key, lo, hi in [("target", -100, 100), ("speed", 0, 100), ("accel", 0, 500), ("decel", 0, 500), ("timeoutMs", 100, 60000)]:
        value = s.get(key)
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or not lo <= value <= hi or (key in ("speed", "accel", "decel") and value == 0):
            raise ValueError("Invalid scenario: " + key)
    return s


def run():
    ap = argparse.ArgumentParser()
    ap.add_argument("address", nargs="?")
    ap.add_argument("--validate", action="store_true")
    ap.add_argument("--execute", action="store_true")
    args = ap.parse_args()
    s = load(Path(__file__).with_name("scenario.json"))
    if args.validate and not args.execute and not args.address:
        print("Valid scenario; no SDK initialization or controller connection")
        return 0
    if not args.execute or not args.address or args.validate:
        ap.error("use --validate, or <controller-IP|absolute-firmware_sim.exe> --execute")
    simulation = args.address.lower().endswith(".exe")
    if simulation and (not Path(args.address).is_absolute() or not Path(args.address).is_file()):
        raise ValueError("Simulator requires an existing absolute path")
    import aamotion as am
    axis = am.AXIS_A if s["axis"] == "A" else am.AXIS_B

    @am.ERROR_CB
    def on_error(code, sent, text, user):
        print(f"[ERR {code}] {(sent or b'').decode(errors='replace')} | {(text or b'').decode(errors='replace')}", file=sys.stderr)

    h = am.initialize(am.CONTROLLER_AGM800)
    if not h:
        raise RuntimeError("Initialize failed; check AACommServerNative installation")
    touched_motion = False
    result = 0
    try:
        am.lib.aamotion_set_error_occurred_handler(h, on_error, None)
        connected = am.lib.aamotion_connect_simulation(h, args.address.encode(), b"AGM800", None, None) if simulation else am.lib.aamotion_connect(h, args.address.encode(), False, False)
        if not connected:
            raise RuntimeError("Connection failed")
        if s["goal"] == "inputs":
            value = am.get_value(h, axis, am.KEYWORD_DIN_PORT)
            if value is None or not math.isfinite(value):
                raise RuntimeError("DInPort read failed")
            print(f"{s['axis']} DInPort = {int(value)}; input 1 = {bool(int(value) & 1)}")
        else:
            if not am.lib.aamotion_is_auto_phase(h, axis):
                raise RuntimeError("Commutation not confirmed; inspect errors and verify machine preconditions with your setup tool first")
            touched_motion = True
            if not am.lib.aamotion_motor_on(h, axis):
                raise RuntimeError("MotorOn refused")
            if not am.lib.aamotion_move_abs(h, axis, s["target"], s["speed"], s["accel"], s["decel"]):
                raise RuntimeError("Move refused")
            seen_moving = False
            deadline = time.monotonic() + s["timeoutMs"] / 1000
            while time.monotonic() < deadline:
                status = am.get_value(h, axis, am.KEYWORD_IN_TARGET_STAT)
                pos = am.get_value(h, axis, am.KEYWORD_POS)
                if status is None or pos is None or not math.isfinite(status) or not math.isfinite(pos):
                    raise RuntimeError("Status read failed; actual motion state is unknown")
                if status == am.IN_TARGET_STAT_MOVING:
                    seen_moving = True
                elif status == am.IN_TARGET_STAT_TARGET_REACHED and (seen_moving or abs(pos - s["target"]) <= max(1e-9, abs(s["target"]) * 1e-12)):
                    print("In target:", pos)
                    break
                time.sleep(0.05)
            else:
                raise RuntimeError("Wait timeout; not proof of a stop")
    except Exception as ex:
        print(ex, file=sys.stderr)
        result = 1
    finally:
        def cleanup(name, *call_args):
            try:
                if getattr(am.lib, name)(*call_args):
                    return True
            except Exception as ex:
                print(name + ": " + str(ex), file=sys.stderr)
            print(name + " not confirmed", file=sys.stderr)
            return False

        if touched_motion:
            for name in ("aamotion_stop", "aamotion_motor_off"):
                if not cleanup(name, h, axis):
                    result = 1
        if not cleanup("aamotion_disconnect", h):
            result = 1
    return result


if __name__ == "__main__":
    try:
        sys.exit(run())
    except (OSError, ValueError, RuntimeError) as ex:
        sys.exit(str(ex))
