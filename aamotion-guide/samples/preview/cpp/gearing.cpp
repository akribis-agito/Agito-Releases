#include "controller.h"

int main(int argc, char* argv[])
{
    if (argc != 2) {
        std::cerr << "Usage: gearing <AGM800 IP>\n";
        return 1;
    }
    int mode, numerator;
    std::cout << "Enter mode (5 direct, 6 indirect) and ratio numerator (denominator 65536): ";
    if (!(std::cin >> mode >> numerator) || (mode != 5 && mode != 6) || numerator == 0)
        return 2;

    Controller session(argv[1]);
    auto h = session.handle;
    const int axis = AAMOTION_AXIS_REF_B;
    if (!session.connected || !ready_axis(h, axis))
        return 3;

    // --8<-- [start:gearing]
    const aamotion_gear_config config{"APosRef", numerator, 65536, 64, 0, mode};
    if (!aamotion_gear_begin(h, axis, &config)) {
        std::cerr << "Start failed. Check the follower configuration and state.\n";
        return 4;
    }
    double motion_mode, motion_status;
    if (aamotion_get_value(h, axis, AAMOTION_KEYWORD_MOTION_MODE, 0, &motion_mode) &&
        aamotion_get_value(h, axis, AAMOTION_KEYWORD_MOTION_STAT, 0, &motion_status))
        std::cout << "Follower mode " << motion_mode << ", motion status " << motion_status << '\n';
    std::cin.ignore(1024, '\n');
    std::cout << "Press Enter to stop the follower. The demo does not command the master.\n";
    std::cin.get();
    if (!aamotion_stop(h, axis)) {
        std::cerr << "Stop was not confirmed. Check the follower at the machine.\n";
        return 5;
    }
    // --8<-- [end:gearing]
    return 0;
}
