#include "demo.h"

int main(int argc, char** argv)
{
    return run_demo(argc, argv, {A, B}, [](aamotion_controller h) {
        // --8<-- [start:operation]
        char reply[256] = {};
        check(aamotion_send_bulk_command_string(h,
            "AMotionMode=1;ARelTrgt=0;ASpeed=2000;AAccel=10000;ADecel=10000;AAbsTrgt=10000;"
            "BMotionMode=1;BRelTrgt=0;BSpeed=2000;BAccel=10000;BDecel=10000;BAbsTrgt=5000;ABegin;BBegin",
            reply, sizeof reply), "Two-axis batch refused");
        wait_until([&] { return read(h, A, AAMOTION_KEYWORD_IN_TARGET_STAT) == 4 &&
                                read(h, B, AAMOTION_KEYWORD_IN_TARGET_STAT) == 4; }, "Both axes: settle timeout");
        std::printf("A=%g, B=%g\n", read(h, A, AAMOTION_KEYWORD_POS), read(h, B, AAMOTION_KEYWORD_POS));
        // --8<-- [end:operation]
    });
}
