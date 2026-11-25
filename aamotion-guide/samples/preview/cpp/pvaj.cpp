#include "controller.h"
#include <chrono>
#include <cmath>
#include <fstream>
#include <thread>
#include <vector>

struct Point { double p, v, a, j; };

int main(int argc, char* argv[])
{
    if (argc != 3) {
        std::cerr << "Usage: pvaj <AGM800 IP> <trajectory.txt>\n";
        return 1;
    }
    // File: gap and position mode (0 absolute, 1 relative), then P V A J per point.
    std::ifstream file(argv[2]);
    int gap, mode;
    if (!(file >> gap >> mode) || gap < 1 || gap > AAMOTION_PVAJ_MAX_GAP ||
        (mode != 0 && mode != 1))
        return 2;
    std::vector<Point> points;
    Point point{};
    while (file >> std::ws && file.peek() != std::char_traits<char>::eof()) {
        if (!(file >> point.p >> point.v >> point.a >> point.j) ||
            !std::isfinite(point.p) || !std::isfinite(point.v) ||
            !std::isfinite(point.a) || !std::isfinite(point.j) ||
            points.size() == AAMOTION_PVAJ_MAX_POINTS)
            return 2;
        points.push_back(point);
    }
    if (points.empty())
        return 2;

    Controller session(argv[1]);
    auto h = session.handle;
    const int axis = AAMOTION_AXIS_REF_A;
    if (!session.connected || !ready_axis(h, axis))
        return 3;

    // --8<-- [start:pvaj]
    if (!aamotion_pvaj_arm(h, axis, false) ||
        !aamotion_pvaj_write_header(h, axis, static_cast<int>(points.size()), gap, mode))
        return 4;
    for (std::size_t i = 0; i < points.size(); ++i) {
        const Point& p = points[i];
        if (!aamotion_pvaj_write_point(h, axis, static_cast<int>(i + 1), p.p, p.v, p.a, p.j))
            return 4;
    }
    if (!aamotion_pvaj_validate(h, axis, 1) || !aamotion_pvaj_is_validated(h, axis) ||
        !aamotion_pvaj_arm(h, axis, true))
        return 4;
    if (!aamotion_pvaj_begin(h, axis, false)) {
        aamotion_pvaj_arm(h, axis, false);
        return 5;
    }

    int state, index, remaining;
    for (int attempt = 0; attempt < 300; ++attempt) {
        if (!aamotion_pvaj_get_status(h, axis, &state, &index, &remaining))
            break;
        if (state != 3 && remaining == 0) {
            std::cout << "PVAJ ended. Check the final position and MotionReason.\n";
            return aamotion_pvaj_arm(h, axis, false) ? 0 : 6;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
    // --8<-- [end:pvaj]
    aamotion_stop(h, axis);
    std::cerr << "Status unavailable or timeout. Verify the actual axis state.\n";
    return 6;
}
