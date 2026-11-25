import sys
import time
from contextlib import contextmanager
from pathlib import Path

# Use the same versioned ctypes binding as the single-function examples.
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "single" / "python"))

A, B = 1, 2


def check(ok, operation):
    if not ok:
        raise RuntimeError(operation)


def wait_until(finished, operation):
    deadline = time.monotonic() + 30
    while not finished():
        check(time.monotonic() < deadline, operation)
        time.sleep(0.05)


def read(am, h, axis, keyword):
    value = am.get_value(h, axis, keyword, 0)
    check(value is not None, "Read failed")
    return value


@contextmanager
def connect(*axes):
    if len(sys.argv) != 2:
        sys.exit(f"Usage: python {Path(sys.argv[0]).name} <controller IP or absolute firmware_sim.exe path>")

    import aamotion as am

    target = sys.argv[1]
    h = am.initialize(am.CONTROLLER_TYPE_AGM800)
    check(h, "Initialize failed")
    try:
        connected = (am.init(h) and am.connect_simulation(h, target, None, None, None)
                     if target.lower().endswith(".exe") else am.connect(h, target, False, False))
        check(connected, "Connect failed")
        for axis in axes:
            check(read(am, h, axis, am.KEYWORD_MOTOR_ON) == 1
                  and read(am, h, axis, am.KEYWORD_MOTION_STAT) == 0, "Enable each idle axis first")
        try:
            yield am, h
        except BaseException:
            for axis in axes:
                if not am.stop(h, axis):
                    print(f"Stop refused on axis {axis}", file=sys.stderr)
            raise
    finally:
        am.disconnect(h)
