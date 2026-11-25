"""双速运动 / Two-speed move
顺序：连接 → 换相 → 使能 → 本次操作 → 确认结果 → 清理与断开
"""
import math
import os
import sys
import time
import aamotion as am

# 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
TARGET = "172.1.1.101"
STEP = "4/6 TwoSpeed"
RESULT_STEP = "5/6 Result"
A = am.AXIS_REF_A


def log(step, text):
    print(f"[{step}] {text}")


def on_error(code, message, exception):
    log("ERR", f"{code}: {message} | {exception}")


def read(h, axis, keyword, index=0):
    """读一个数值；失败时返回 NaN，不能当作 0 使用。"""
    value = am.get_value(h, axis, keyword, index)
    return math.nan if value is None else value


def axis_state(h, axis):
    """使能与控制器故障状态，用于说明失败原因。"""
    fault = am.get_value(h, axis, am.KEYWORD_CON_FLT, 0)
    text = am.get_con_flt_des(int(fault)) if fault is not None else ""
    return f"MotorOn={read(h, axis, am.KEYWORD_MOTOR_ON)}, ConFlt={fault} {text}"


def operate(h, state):
    # --8<-- [start:connect]
    # 1. 连接：TARGET 是 IP 时连接控制器；以 .exe 结尾时连接仿真器
    log("1/6 Connect", TARGET)
    simulator = TARGET.lower().endswith(".exe")
    if not simulator:
        connected = am.connect(h, TARGET, True, False)
    elif os.path.isfile(TARGET):
        am.init(h)   # 仿真器需要先启动 AACommServer
        connected = am.connect_simulation(h, TARGET, None, None, None)
    else:
        log("1/6 Connect", "simulator not found; edit TARGET")
        return 2
    if not connected:
        log("1/6 Connect", "failed; check TARGET and the cable")
        return 2
    # --8<-- [end:connect]

    # --8<-- [start:prepare]
    # 2. 换相：仿真用虚拟电机。真实电机才需要换相，等待有上限
    if simulator:
        am.set_value(h, A, am.KEYWORD_MOTOR_TYPE, 0, 5)
    elif not am.is_auto_phase(h, A):
        log("2/6 AutoPhase", "requested")
        am.auto_phase(h, A)
        phase_time = time.monotonic()
        while not am.is_auto_phase(h, A):
            if time.monotonic() - phase_time >= 10:
                log("2/6 AutoPhase", "timed out; motor stays off")
                return 3
            time.sleep(0.02)
    log("2/6 AutoPhase", "virtual motor" if simulator else "commutated")

    # 3. 使能：清理阶段也会处理结果不确定的使能
    #    控制器回复 ERR 时错误回调会打印错误码；超时等失败没有回调，
    #    所以失败时再读一次使能与故障状态
    state["motor_on_requested"] = True
    if not am.motor_on(h, A):
        log("3/6 MotorOn", "returned False; " + axis_state(h, A))
        return 4
    if read(h, A, am.KEYWORD_MOTOR_ON) != 1:
        log("3/6 MotorOn", "accepted but not on; " + axis_state(h, A))
        return 4
    log("3/6 MotorOn", "on")
    # --8<-- [end:prepare]

    # --8<-- [start:operation]
    # 4. 本次操作
    # 前 8000 用速度 4000，之后用 1000 慢速走完 10000
    start = read(h, A, am.KEYWORD_POS)
    # 最后一个参数 -1：换速方向用默认值（正向越过换速点）
    am.move_abs_two_speed(h, A, start + 10000, 4000, 1000,
                          start + 8000, 50000, 50000, -1)
    log(STEP, f"two-speed move to {start + 10000} requested")
    # --8<-- [end:operation]

    # --8<-- [start:observe]
    # 5. 确认结果：命令被接受不等于动作完成
    motion_time = time.monotonic()
    reached = am.IN_TARGET_STAT_TARGET_REACHED   # 4：整定到位
    while read(h, A, am.KEYWORD_IN_TARGET_STAT) != reached:
        if time.monotonic() - motion_time >= 10:
            am.stop(h, A)
            log(RESULT_STEP, "settle timeout; stop requested")
            return 6
        time.sleep(0.02)
    log(RESULT_STEP, f"settled: Pos={read(h, A, am.KEYWORD_POS)}")
    # --8<-- [end:observe]
    return 0


def run():
    print("Axis A will move. Check travel and units.")
    try:
        answer = input("Type RUN to continue:\n")
    except EOFError:
        answer = ""
    if answer != "RUN":
        return 1
    h = am.initialize(am.CONTROLLER_TYPE_AGM800)
    if not h:
        log("1/6 Connect", "initialize failed; AACommServerNative missing?")
        return 2
    am.set_error_occurred_handler(h, on_error)
    state = {"motor_on_requested": False}
    try:
        return operate(h, state)
    except Exception as ex:
        log("Failed", str(ex))
        return 5
    finally:
        # --8<-- [start:cleanup]
        # 6. 清理：先确认停止再断使能；断开通信不能证明设备已停止
        if state["motor_on_requested"]:
            am.stop(h, A)
            stop_time = time.monotonic()
            while (read(h, A, am.KEYWORD_MOTION_STAT) != 0
                   and time.monotonic() - stop_time < 5):
                time.sleep(0.02)
            if read(h, A, am.KEYWORD_MOTION_STAT) == 0:
                am.motor_off(h, A)
                log("6/6 Cleanup", "stopped, motor off")
            else:
                log("6/6 Cleanup", "stop unconfirmed; check the axis")
        am.disconnect(h)   # 同时释放句柄
        # --8<-- [end:cleanup]


def main():
    # 输出被重定向时，编码不了的字符（如中文路径）转义输出，不报错；
    # IDLE、Jupyter 的输出没有 reconfigure
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="backslashreplace")
    result = run()
    print(f"Exit code {result}.")
    return result


if __name__ == "__main__":
    sys.exit(main())
