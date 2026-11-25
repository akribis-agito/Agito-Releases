#include "demo.h"

int main(int argc, char** argv)
{
    return run_demo(argc, argv, {A, B}, [](aamotion_controller h) {
        // --8<-- [start:operation]
        const int x = static_cast<int>(read(h, A, AAMOTION_KEYWORD_POS));
        const int y = static_cast<int>(read(h, B, AAMOTION_KEYWORD_POS));
        check(aamotion_clear_buffer(h), "ClearBuffer");
        check(aamotion_set_start_positions(h, A | B, x, y, 0), "SetStartPositions");
        check(aamotion_set_motion_profile(h, 100, 10000, 10000, 0), "SetMotionProfile");
        check(aamotion_linear_absolute_xy(h, x + 1000, y + 1000, 1000, 0), "LinearAbsoluteXY");
        char reply[128] = {};
        check(aamotion_send_command_string(h, "AMotionMode=11", reply, sizeof reply) &&
              aamotion_send_command_string(h, "BMotionMode=11", reply, sizeof reply), "Set CNC mode");
        check(aamotion_cnc_begin(h), "Begin");
        try { wait_until([&] { return aamotion_is_cnc_completed(h, A); }, "CNC completion timeout"); }
        catch (...) { aamotion_group_stop(h); throw; }
        std::printf("CNC complete: A=%g, B=%g\n", read(h, A, AAMOTION_KEYWORD_POS), read(h, B, AAMOTION_KEYWORD_POS));
        // --8<-- [end:operation]
    });
}
