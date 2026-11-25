"""扫描网段 / Scan the network
不连接控制器。
"""
import sys
import aamotion as am

STEP = "1/1 ScanNetwork"


def log(step, text):
    print(f"[{step}] {text}")


def run():
    try:
        # --8<-- [start:operation]
        # 改成控制器所在网段的前三段；结果是逗号分隔的 IP 列表
        found = am.scan_controller(0, "172.1.1", 1, 254, 50000)
        if found is None:
            log(STEP, "the subnet must be three dotted parts")
            return 6
        log(STEP, "controllers: " + (found or "none"))
        # --8<-- [end:operation]
        return 0
    except Exception as ex:
        log("Failed", str(ex))
        return 5


def main():
    result = run()
    print(f"Exit code {result}.")
    return result


if __name__ == "__main__":
    sys.exit(main())
