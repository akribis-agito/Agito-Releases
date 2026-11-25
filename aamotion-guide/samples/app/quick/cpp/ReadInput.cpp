#include "demo.h"

int main(int argc, char** argv)
{
    return run_demo(argc, argv, {}, [](aamotion_controller h) {
        // --8<-- [start:operation]
        std::printf("Input 1 = %d\n", aamotion_get_d_in_port(h, A, 0));
        // --8<-- [end:operation]
    });
}
