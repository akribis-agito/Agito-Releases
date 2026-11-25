#include "demo.h"

int main(int argc, char** argv)
{
    return run_demo(argc, argv, {A}, [](aamotion_controller h) {
        // --8<-- [start:operation]
        check(aamotion_home(h, A), "Home refused");
        wait_until([&] {
            const double status = read(h, A, AAMOTION_KEYWORD_HOMING_STAT);
            check(status >= 0, "Homing failed");
            return status == 100;
        }, "Homing timeout");
        std::printf("Home complete: position = %g\n", read(h, A, AAMOTION_KEYWORD_POS));
        // --8<-- [end:operation]
    });
}
