from demo import A, B, check, connect, read, wait_until


if __name__ == "__main__":
    with connect(A, B) as (am, h):
        # --8<-- [start:operation]
        commands = (
            "AMotionMode=1;ARelTrgt=0;ASpeed=2000;AAccel=10000;ADecel=10000;AAbsTrgt=10000;"
            "BMotionMode=1;BRelTrgt=0;BSpeed=2000;BAccel=10000;BDecel=10000;BAbsTrgt=5000;ABegin;BBegin"
        )
        check(am.send_bulk_command_string(h, commands) is not None, "Two-axis batch refused")
        wait_until(lambda: all(read(am, h, axis, am.KEYWORD_IN_TARGET_STAT) == 4 for axis in (A, B)),
                   "Both axes: settle timeout")
        print("A =", read(am, h, A, am.KEYWORD_POS), "B =", read(am, h, B, am.KEYWORD_POS))
        # --8<-- [end:operation]
