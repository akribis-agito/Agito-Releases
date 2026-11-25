import time
from pathlib import Path

from demo import check, connect


if __name__ == "__main__":
    with connect() as (am, h):
        # --8<-- [start:operation]
        check(am.configure_slow_logger(h, 10, 0, ["APos"]), "Configure SlowLogger")
        check(am.set_logger_on(h, 1), "Start SlowLogger")
        try:
            time.sleep(2)
            text = am.logger_upload(h)
            check(text, "No logger data")
            Path("slow-logger.txt").write_text(text, encoding="utf-8")
        finally:
            check(am.set_logger_on(h, 0), "Stop SlowLogger")
        print("Saved slow-logger.txt")
        # --8<-- [end:operation]
