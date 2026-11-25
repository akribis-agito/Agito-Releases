"""错误码转成文字 / Error code to text
不连接控制器。
"""
import sys
import aamotion as am

STEP = "1/1 DescribeError"


def log(step, text):
    print(f"[{step}] {text}")


def run():
    try:
        # --8<-- [start:operation]
        # 与错误回调收到的文字相同（英文）
        text = am.get_error_description_by_code(31)
        log(STEP, f"ERR 31: {text}")
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
