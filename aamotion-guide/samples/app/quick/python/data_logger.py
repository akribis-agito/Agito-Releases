from demo import check, connect


if __name__ == "__main__":
    with connect() as (am, h):
        # --8<-- [start:operation]
        check(am.data_rec_record(h, "APos", 1, 1, 0, 0, None, 0, 0, False, 5000),
              "Record and upload APos")
        info = am.data_rec_info(h)
        check(info is not None, "Read recording info")
        _, _, vectors, samples = info
        check(vectors == 1 and samples > 0, "No APos samples")
        print(f"APos: {samples} samples")
        # --8<-- [end:operation]
