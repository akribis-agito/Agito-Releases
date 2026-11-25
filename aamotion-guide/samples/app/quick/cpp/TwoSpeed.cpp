#include "demo.h"

int main(int argc, char** argv)
{
    return run_demo(argc, argv, {A}, [](aamotion_controller h) {
        // --8<-- [start:operation]
        check(aamotion_move_rel_two_speed_and_wait(h, A, 10000, 4000, 1000, 8000,
            50000, 50000, -1, 10000), "Two-speed move failed");
        std::printf("In target: position = %g\n", read(h, A, AAMOTION_KEYWORD_POS));
        // --8<-- [end:operation]
    });
}
