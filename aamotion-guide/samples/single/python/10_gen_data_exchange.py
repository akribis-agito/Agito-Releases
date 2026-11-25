"""和用户程序交换数据 / Exchange data with the program
顺序：连接 → 本次操作 → 断开。轴不运动。
"""
import os
import sys
import time
import aamotion as am

# 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
TARGET = "172.1.1.101"
STEP = "2/3 GenDataExchange"
A = am.AXIS_REF_A


def log(step, text):
    print(f"[{step}] {text}")


def on_error(code, message, exception):
    log("ERR", f"{code}: {message} | {exception}")


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
    # 约定：GenData[1] 参数，GenData[2] 开始标志，GenData[3] 结果
    am.write_gen_data(h, 1, 7)
    am.write_gen_data(h, 2, 1)   # 程序看到 1 后开始执行
    log(STEP, "input 7 written, start flag set")
    wait_time = time.monotonic()
    while True:
        flag = am.send_command_string(h, "AGenData[2]")
        if flag is None:
            log(STEP, "reading the flag failed; see the ERR line")
            return 6
        if float(flag) == 0:   # 程序完成后清零
            break
        if time.monotonic() - wait_time >= 5:
            am.write_gen_data(h, 2, 0)   # 撤回请求
            log(STEP, "no answer in 5 s; is the program running?")
            return 6
        time.sleep(0.05)
    log(STEP, f"result GenData[3]={am.read_gen_data(h, 3)}")
    # --8<-- [end:operation]
    return 0


def run():
    print("Writes AGenData[1] and [2] for the handshake.")
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
