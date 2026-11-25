"""GenData 与 UserParam / GenData and UserParam
顺序：连接 → 本次操作 → 断开。轴不运动。
"""
import os
import sys
import aamotion as am

# 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
TARGET = "172.1.1.101"
STEP = "2/3 GenDataUserParam"
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
    # 先读出原值并检查：读不到就不写，避免把用户数据改成 0。
    # read_gen_data 失败时也返回 0，所以原值用 get_value 读
    data = am.get_value(h, A, am.KEYWORD_GEN_DATA, 10)   # 下标从 1 开始
    param = am.read_user_param(h, 1)
    if data is None or param is None:
        log(STEP, "reading the originals failed; nothing written")
        return 6
    try:
        am.write_gen_data(h, 10, 1234)
        am.write_user_param(h, 1, 42)
        now = am.read_gen_data(h, 10)
        log(STEP, f"AGenData[10] {data:g} -> {now}")
        now = am.read_user_param(h, 1)
        log(STEP, f"AUserParam[1] {param} -> {now}")
    finally:
        # 改回原值：控制器上的用户程序可能在使用这些下标
        am.write_gen_data(h, 10, int(data))
        am.write_user_param(h, 1, param)
        log(STEP, "originals restored")
    # --8<-- [end:operation]
    return 0


def run():
    print("Writes AGenData[10] and AUserParam[1].")
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
