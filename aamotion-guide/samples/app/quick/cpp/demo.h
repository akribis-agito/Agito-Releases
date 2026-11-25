#pragma once

#include "aamotion.h"
#include <chrono>
#include <cstdio>
#include <initializer_list>
#include <stdexcept>
#include <string>
#include <thread>

inline constexpr int A = AAMOTION_AXIS_REF_A;
inline constexpr int B = AAMOTION_AXIS_REF_B;

inline void check(bool ok, const char* operation)
{
    if (!ok)
        throw std::runtime_error(operation);
}

inline double read(aamotion_controller h, int axis, int keyword)
{
    double value = 0;
    check(aamotion_get_value(h, axis, keyword, 0, &value), "Read failed");
    return value;
}

template<class Finished>
void wait_until(Finished finished, const char* operation)
{
    const auto end = std::chrono::steady_clock::now() + std::chrono::seconds(30);
    while (!finished())
    {
        check(std::chrono::steady_clock::now() < end, operation);
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
    }
}

template<class Run>
int run_demo(int argc, char** argv, std::initializer_list<int> axes, Run run)
{
    if (argc != 2)
    {
        std::fprintf(stderr, "Usage: %s <controller IP or absolute firmware_sim.exe path>\n", argv[0]);
        return 2;
    }
    if (aamotion_abi_version() != AAMOTION_ABI_VERSION)
    {
        std::fprintf(stderr, "SDK header/library mismatch\n");
        return 1;
    }
    auto h = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);
    if (!h)
    {
        std::fprintf(stderr, "Initialize failed\n");
        return 1;
    }

    bool ready = false;
    int status = 0;
    try
    {
        const std::string target = argv[1];
        const bool simulator = target.size() >= 4 && target.substr(target.size() - 4) == ".exe";
        const bool connected = simulator
            ? aamotion_init(h) && aamotion_connect_simulation(h, argv[1], nullptr, nullptr, nullptr)
            : aamotion_connect(h, argv[1], false, false);
        check(connected, "Connect failed");
        for (int axis : axes)
            check(read(h, axis, AAMOTION_KEYWORD_MOTOR_ON) == 1 &&
                  read(h, axis, AAMOTION_KEYWORD_MOTION_STAT) == 0, "Enable each idle axis first");
        ready = true;
        run(h);
    }
    catch (const std::exception& ex)
    {
        std::fprintf(stderr, "%s\n", ex.what());
        if (ready)
            for (int axis : axes)
                if (!aamotion_stop(h, axis))
                    std::fprintf(stderr, "Stop refused on axis %d\n", axis);
        status = 1;
    }
    aamotion_disconnect(h);
    return status;
}
