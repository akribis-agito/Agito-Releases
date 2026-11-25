"""运行、暂停与复位线程 / Run, halt and reset a thread
顺序：连接 → 本次操作 → 断开。轴不运动。
"""
import math
import os
import sys
import time
import aamotion as am

# 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
TARGET = "172.1.1.101"
STEP = "2/3 ProgRunHalt"
A = am.AXIS_REF_A


def log(step, text):
    print(f"[{step}] {text}")


def on_error(code, message, exception):
    log("ERR", f"{code}: {message} | {exception}")


def read(h, axis, keyword, index=0):
    """读一个数值；失败时返回 NaN，不能当作 0 使用。"""
    value = am.get_value(h, axis, keyword, index)
    return math.nan if value is None else value


def operate(h):
    # --8<-- [start:connect]
    # 1. 连接：TARGET 是 IP 时连接控制器；以 .exe 结尾时连接仿真器
    log("1/3 Connect", TARGET)
    if not TARGET.lower().endswith(".exe"):
        connected = am.connect(h, TARGET, True, False)
    elif os.path.isfile(TARGET):
        am.init(h)   # 仿真器需要先启动 AACommServer
        connected = am.connect_simulation(h, TARGET, None, None, None)
    else:
        log("1/3 Connect", "simulator not found; edit TARGET")
        return 2
    if not connected:
        log("1/3 Connect", "failed; check TARGET and the cable")
        return 2
    # --8<-- [end:connect]

    # --8<-- [start:operation]
    # 2. 本次操作
    # 线程 1 从头运行任务 1（任务号是程序中的任务标签）
    log(STEP, f"prog_run: {am.prog_run(h, A, 1, 1)}")
    time.sleep(1)
    # ProgStat[n]：1 运行中，0 未运行，-1 没有程序
    log(STEP, f"ProgStat[1]={read(h, A, am.KEYWORD_PROG_STAT, 1)}")
    am.prog_halt(h, A, 1)
    stat = read(h, A, am.KEYWORD_PROG_STAT, 1)
    log(STEP, f"halted: ProgStat[1]={stat}")
    am.prog_reset(h, A, 1)   # 下次从头运行
    error = read(h, A, am.KEYWORD_PROG_ERROR, 1)
    log(STEP, f"reset: ProgError[1]={error}")
    # --8<-- [end:operation]
    return 0


def run():
    print("Runs task 1 on thread 1, then halts and resets it.")
    try:
        answer = input("Type RUN to continue:\n")
    except EOFError:
        answer = ""
    if answer != "RUN":
        return 1
    h = am.initialize(am.CONTROLLER_TYPE_AGM800)
    if not h:
        log("1/3 Connect", "initialize failed; AACommServerNative missing?")
        return 2
    am.set_error_occurred_handler(h, on_error)
    try:
        return operate(h)
    except Exception as ex:
        log("Failed", str(ex))
        return 5
    finally:
        # --8<-- [start:cleanup]
        # 3. 断开：同时释放句柄；AACommServer 继续为其他程序服务
        am.disconnect(h)
        log("3/3 Disconnect", "done")
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
