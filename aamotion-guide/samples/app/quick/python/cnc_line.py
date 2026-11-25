from demo import A, B, check, connect, read, wait_until


if __name__ == "__main__":
    with connect(A, B) as (am, h):
        # --8<-- [start:operation]
        x = int(read(am, h, A, am.KEYWORD_POS))
        y = int(read(am, h, B, am.KEYWORD_POS))
        check(am.clear_buffer(h), "ClearBuffer")
        check(am.set_start_positions(h, A | B, x, y, 0), "SetStartPositions")
        check(am.set_motion_profile(h, 100, 10000, 10000, 0), "SetMotionProfile")
        check(am.linear_absolute_xy(h, x + 1000, y + 1000, 1000, 0), "LinearAbsoluteXY")
        check(am.send_command_string(h, "AMotionMode=11") is not None
              and am.send_command_string(h, "BMotionMode=11") is not None, "Set CNC mode")
        check(am.cnc_begin(h), "Begin")
        try:
            wait_until(lambda: am.is_cnc_completed(h, A), "CNC completion timeout")
        except BaseException:
            am.group_stop(h)
            raise
        print("CNC complete: A =", read(am, h, A, am.KEYWORD_POS), "B =", read(am, h, B, am.KEYWORD_POS))
        # --8<-- [end:operation]
