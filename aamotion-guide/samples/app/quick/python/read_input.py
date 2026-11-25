from demo import A, connect


if __name__ == "__main__":
    with connect() as (am, h):
        # --8<-- [start:operation]
        print("Input 1 =", am.get_d_in_port(h, A, 0))
        # --8<-- [end:operation]
