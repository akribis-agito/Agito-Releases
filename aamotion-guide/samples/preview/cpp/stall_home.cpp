#include "controller.h"
#include <chrono>
#include <cmath>
#include <thread>

int main(int argc, char* argv[])
{
    if (argc != 2) {
        std::cerr << "Usage: stall_home <AGM800 IP>\n";
        return 1;
    }
    int direction, timeout_ms;
    double stuck_vel, stuck_curr, stuck_ms, fast, slow, accel;
    std::cout << "Enter commissioned direction, velocity threshold, current threshold, "
                 "hold ms, fast speed, slow speed, acceleration, and timeout ms: ";
    if (!(std::cin >> direction >> stuck_vel >> stuck_curr >> stuck_ms >> fast >> slow >> accel >> timeout_ms) ||
        (direction != -1 && direction != 1) || timeout_ms <= 0 ||
        !std::isfinite(stuck_vel) || !std::isfinite(stuck_curr) || !std::isfinite(stuck_ms) ||
        !std::isfinite(fast) || !std::isfinite(slow) || !std::isfinite(accel) ||
        stuck_vel <= 0 || stuck_curr <= 0 || stuck_ms <= 0 || fast <= 0 || slow <= 0 || accel <= 0)
        return 2;

    Controller session(argv[1]);
    auto h = session.handle;
    const int axis = AAMOTION_AXIS_REF_A;
    if (!session.connected || !ready_axis(h, axis))
        return 3;

    // --8<-- [start:stall-home]
    if (!aamotion_lock_src_zero_supported(h, axis) ||
        !aamotion_home_to_hard_stop_by_stuck_then_index(h, axis, 0, direction,
            stuck_vel, stuck_curr, stuck_ms, fast, slow, accel, timeout_ms))
        return 4;

    const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeout_ms);
    while (std::chrono::steady_clock::now() < deadline) {
        double status;
        if (!aamotion_get_value(h, axis, AAMOTION_KEYWORD_HOMING_STAT, 0, &status))
            break;
        if (status == 100) {
            std::cout << "Homing completed.\n";
            return 0;
        }
        if (status < 0) {
            std::cerr << "Homing failed, HomingStat=" << status << '\n';
            return 5;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
    }
    aamotion_stop(h, axis);
    std::cerr << "Homing could not be confirmed. Check the actual axis state.\n";
    return 6;
    // --8<-- [end:stall-home]
}
