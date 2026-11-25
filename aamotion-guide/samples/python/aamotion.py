"""Thin ctypes binding over the AAMotionNative C ABI (aamotion.dll / libaamotion.so).

Only the functions the samples use are declared; every declaration mirrors aamotion.h.
Set AAMOTION_SDK_DIR to the extracted native SDK folder, or put the library beside this file.
"""
import ctypes as C
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from aamotion_enums import *  # noqa: F401,F403  every AAMOTION_* constant without the prefix

ABI_VERSION = 3

AXIS_A = AXIS_REF_A
AXIS_B = AXIS_REF_B
CONTROLLER_AGM800 = CONTROLLER_TYPE_AGM800


def _library_path():
    name = "aamotion.dll" if sys.platform == "win32" else "libaamotion.so"
    sub = "win-x64" if sys.platform == "win32" else "linux-x64"
    candidates = []
    sdk = os.environ.get("AAMOTION_SDK_DIR")
    if sdk:
        candidates.append(os.path.join(sdk, sub, name))
    candidates.append(os.path.join(os.path.dirname(os.path.abspath(__file__)), name))
    for path in candidates:
        if os.path.isfile(path):
            return path
    raise FileNotFoundError(f"{name} not found; set AAMOTION_SDK_DIR. Tried: {candidates}")


if C.sizeof(C.c_void_p) != 8:
    raise RuntimeError("aamotion is 64-bit only: use a 64-bit Python")

_path = _library_path()
if sys.platform == "win32":
    os.add_dll_directory(os.path.dirname(_path))
lib = C.CDLL(_path)

# The last argument is the user pointer passed at registration (None here).
ERROR_CB = C.CFUNCTYPE(None, C.c_int, C.c_char_p, C.c_char_p, C.c_void_p)

lib.aamotion_abi_version.restype = C.c_int32
lib.aamotion_initialize.argtypes = [C.c_int]
lib.aamotion_initialize.restype = C.c_void_p
lib.aamotion_connect.argtypes = [C.c_void_p, C.c_char_p, C.c_bool, C.c_bool]
lib.aamotion_connect.restype = C.c_bool
lib.aamotion_connect_simulation.argtypes = [C.c_void_p, C.c_char_p, C.c_char_p, C.c_char_p, C.c_char_p]
lib.aamotion_connect_simulation.restype = C.c_bool
lib.aamotion_disconnect.argtypes = [C.c_void_p]
lib.aamotion_disconnect.restype = C.c_bool
lib.aamotion_set_error_occurred_handler.argtypes = [C.c_void_p, ERROR_CB, C.c_void_p]
lib.aamotion_set_error_occurred_handler.restype = None
lib.aamotion_get_value.argtypes = [C.c_void_p, C.c_int, C.c_int, C.c_int, C.POINTER(C.c_double)]
lib.aamotion_get_value.restype = C.c_bool
lib.aamotion_motor_on.argtypes = [C.c_void_p, C.c_int]
lib.aamotion_motor_on.restype = C.c_bool
lib.aamotion_motor_off.argtypes = [C.c_void_p, C.c_int]
lib.aamotion_motor_off.restype = C.c_bool
lib.aamotion_move_relative.argtypes = [C.c_void_p, C.c_int, C.c_double, C.c_double, C.c_double, C.c_double]
lib.aamotion_move_relative.restype = C.c_bool
lib.aamotion_stop.argtypes = [C.c_void_p, C.c_int]
lib.aamotion_stop.restype = C.c_bool
lib.aamotion_auto_phase.argtypes = [C.c_void_p, C.c_int]
lib.aamotion_auto_phase.restype = C.c_bool
lib.aamotion_is_auto_phase.argtypes = [C.c_void_p, C.c_int]
lib.aamotion_is_auto_phase.restype = C.c_bool
lib.aamotion_move_abs.argtypes = [C.c_void_p, C.c_int, C.c_double, C.c_double, C.c_double, C.c_double]
lib.aamotion_move_abs.restype = C.c_bool
lib.aamotion_send_command_string.argtypes = [C.c_void_p, C.c_char_p, C.c_char_p, C.c_int]
lib.aamotion_send_command_string.restype = C.c_bool
lib.aamotion_send_bulk_command_string.argtypes = [C.c_void_p, C.c_char_p, C.c_char_p, C.c_int]
lib.aamotion_send_bulk_command_string.restype = C.c_bool
lib.aamotion_get_error_description_by_code.argtypes = [C.c_int, C.c_char_p, C.c_int]
lib.aamotion_get_error_description_by_code.restype = C.c_bool

if lib.aamotion_abi_version() != ABI_VERSION:
    raise RuntimeError("aamotion.py and the aamotion library come from different SDK versions")


# --8<-- [start:initialize]
def initialize(controller_type):
    """aamotion_initialize, run from the library's folder.

    The SDK starts AACommServerNative from the application folder or the current working
    directory. A Python process's application folder is the interpreter's, so the server is
    found through the working directory, switched to the library's folder for this call.
    Returns the handle, or None when the server could not be started or reached.
    """
    here = os.getcwd()
    os.chdir(os.path.dirname(_path))
    try:
        return lib.aamotion_initialize(controller_type)
    finally:
        os.chdir(here)
# --8<-- [end:initialize]


def get_value(h, axis, keyword, index=0):
    """Returns the value, or None when the read failed (the error callback has fired)."""
    v = C.c_double()
    return v.value if lib.aamotion_get_value(h, axis, keyword, index, C.byref(v)) else None


def send(h, command, size=4096):
    """Sends a raw command string; returns (ok, reply)."""
    buf = C.create_string_buffer(size)
    ok = lib.aamotion_send_command_string(h, command.encode(), buf, size)
    return ok, buf.value.decode(errors="replace")


_DES = ("motion_stat", "motion_reason", "stat_reg", "homing_stat", "in_target_stat", "con_flt", "enc_type",
        "motion_mode", "map_type", "operation_mode", "motor_on", "inject_type")
for _kind in _DES:
    _f = getattr(lib, f"aamotion_get_{_kind}_des")
    _f.argtypes = [C.c_int, C.c_char_p, C.c_int]
    _f.restype = C.c_bool


def describe(kind, value, size=512):
    """English text for an enum value, e.g. describe("motion_reason", 1); kind is one of _DES."""
    if kind not in _DES:
        raise ValueError(f"unknown kind {kind!r}; expected one of {', '.join(_DES)}")
    buf = C.create_string_buffer(size)
    ok = getattr(lib, f"aamotion_get_{kind}_des")(int(value), buf, size)
    return buf.value.decode(errors="replace") if ok else None
