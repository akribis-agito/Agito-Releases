"""螺旋找光 / Spiral light finding
顺序：连接 → 本次操作 → 断开。轴不运动。
"""
import os
import sys
import time
import aamotion as am

# 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
TARGET = "172.1.1.101"
STEP = "2/3 SpiralScan"
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
    oc = am.optical_coupling_create(h)
    if not oc:
        log(STEP, "optical_coupling_create failed")
        return 6
    try:
        # 模拟输入 1；找光轴 0、1；螺距 50、最大半径 2000、角度系数 10；
        # 扫描速度 2000、返回速度 5000、光功率阈值 1000
        am.optical_coupling_apply_spiral_config(oc, 1, 0, 1, 50, 2000,
                                                10, 2000, 5000, 1000)
        am.write_gen_data(h, 602, 0)   # 清除上次结果
        am.optical_coupling_start_spiral_scan(oc)
        scan_time = time.monotonic()
        while True:
            # 0：还在扫描；正数成功，负数失败
            result = am.optical_coupling_get_coupling_result(oc)
            if result != 0:
                break
            if time.monotonic() - scan_time >= 60:
                am.optical_coupling_emergency_stop(oc)
                log(STEP, "scan timeout; emergency stop requested")
                return 6
            time.sleep(0.1)
        if not am.optical_coupling_is_success(oc):
            error = am.optical_coupling_get_error_code(oc)
            log(STEP, f"failed: result {result}, error {error}")
            return 6
        power = am.optical_coupling_get_max_power(oc)
        x = am.optical_coupling_get_max_power_x_pos(oc)
        y = am.optical_coupling_get_max_power_y_pos(oc)
        log(STEP, f"max power {power} at ({x}, {y})")
    finally:
        am.optical_coupling_release(oc)
    # --8<-- [end:operation]
    return 0


def run():
    print("Starts a spiral scan; the coupling axes move.")
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
