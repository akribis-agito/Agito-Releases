from demo import A, check, connect, read


if __name__ == "__main__":
    with connect(A) as (am, h):
        # --8<-- [start:operation]
        check(am.move_rel_two_speed_and_wait(h, A, 10000, 4000, 1000, 8000,
                                             50000, 50000, -1, 10000), "Two-speed move failed")
        print("In target: position =", read(am, h, A, am.KEYWORD_POS))
        # --8<-- [end:operation]
