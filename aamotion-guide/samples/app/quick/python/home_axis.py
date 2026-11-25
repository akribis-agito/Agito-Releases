from demo import A, check, connect, read, wait_until


if __name__ == "__main__":
    with connect(A) as (am, h):
        # --8<-- [start:operation]
        check(am.home(h, A), "Home refused")

        def homed():
            status = read(am, h, A, am.KEYWORD_HOMING_STAT)
            check(status >= 0, f"Homing failed: {status}")
            return status == 100

        wait_until(homed, "Homing timeout")
        print("Home complete: position =", read(am, h, A, am.KEYWORD_POS))
        # --8<-- [end:operation]
