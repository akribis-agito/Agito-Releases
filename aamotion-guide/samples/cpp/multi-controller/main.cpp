#include "aamotion.h"
#include <cstdio>
#include <cstring>
#include <future>

static bool ReadPosition(const char* ip)
{
    auto controller = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);
    if (!controller)
    {
        std::fprintf(stderr, "%s: initialization failed\n", ip);
        return false;
    }

    double position = 0;
    bool ok = aamotion_connect(controller, ip, false, false) &&
        aamotion_get_value(controller, AAMOTION_AXIS_REF_A,
                          AAMOTION_KEYWORD_POS, 0, &position);
    if (ok)
        std::printf("%s: APos=%g\n", ip, position);
    else
        std::fprintf(stderr, "%s: connect or read failed\n", ip);

    aamotion_disconnect(controller);
    return ok;
}

int main(int argc, char** argv)
{
    if (argc != 3 || std::strcmp(argv[1], argv[2]) == 0)
    {
        std::fprintf(stderr, "Usage: multi_controller <first-ip> <second-ip>\n");
        return 1;
    }

    auto first = std::async(std::launch::async, ReadPosition, argv[1]);
    auto second = std::async(std::launch::async, ReadPosition, argv[2]);
    const bool firstOk = first.get();
    const bool secondOk = second.get();
    return firstOk && secondOk ? 0 : 2;
}
