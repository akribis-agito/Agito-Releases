"""Cookbook recipes in Python (ctypes over the native SDK).

    python cookbook.py 172.1.1.101
"""
import sys
import time

import aamotion as am


# --8<-- [start:session]
class Controller:
    """One controller, with the error callback kept alive as long as the handle."""

    def __init__(self, controller_type=am.CONTROLLER_TYPE_AGM800):
        self.errors = []
        self._cb = am.ERROR_CB(self._on_error)      # keep a reference: ctypes must not free it
        self.h = am.initialize(controller_type)
        if not self.h:
            raise RuntimeError("initialize failed: is AACommServerNative beside the library?")
        am.lib.aamotion_set_error_occurred_handler(self.h, self._cb, None)

    def _on_error(self, code, sent, text, user):
        self.errors.append((code, (sent or b"").decode(), (text or b"").decode()))

    def connect(self, ip, immediate_ack=True):
        # the C argument is "fastArk": False turns PC-side immediate ACK on
        return am.lib.aamotion_connect(self.h, ip.encode(), not immediate_ack, False)

    def close(self):
        if self.h:
            am.lib.aamotion_disconnect(self.h)       # also frees the handle
            self.h = None

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.close()
# --8<-- [end:session]


# --8<-- [start:wait]
def move_and_wait(ctl, axis, target, speed, accel, decel, timeout_s=10.0):
    """aamotion_move_abs, then poll InTargetStat until 4 (in target)."""
    if not am.lib.aamotion_move_abs(ctl.h, axis, float(target), float(speed), float(accel), float(decel)):
        return False
    end = time.monotonic() + timeout_s
    while time.monotonic() < end:
        if am.get_value(ctl.h, axis, am.KEYWORD_IN_TARGET_STAT) == am.IN_TARGET_STAT_TARGET_REACHED:
            return True
        time.sleep(0.02)
    return False
# --8<-- [end:wait]


# --8<-- [start:snapshot]
def snapshot(ctl, names):
    """Several values in one round trip, with a bulk command."""
    import ctypes as C
    buf = C.create_string_buffer(8192)
    ok = am.lib.aamotion_send_bulk_command_string(ctl.h, ";".join(names).encode(), buf, len(buf))
    parts = buf.value.decode(errors="replace").split(">")
    return dict(zip(names, parts)) if ok else None
# --8<-- [end:snapshot]



if __name__ == "__main__":
    ip = sys.argv[1] if len(sys.argv) > 1 else "172.1.1.101"
    with Controller() as ctl:
        print("connect", ip, ctl.connect(ip))
        print("errors so far:", ctl.errors)
