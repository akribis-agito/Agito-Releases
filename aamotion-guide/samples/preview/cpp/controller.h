#pragma once
#include "aamotion.h"
#include <iostream>

inline void report_error(int code, const char* command, const char* message, void*)
{
    std::cerr << code << " " << command << ": " << message << '\n';
}

struct Controller
{
    explicit Controller(const char* address)
        : handle(aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800))
    {
        if (handle) {
            aamotion_set_error_occurred_handler(handle, report_error, nullptr);
            connected = aamotion_connect(handle, address, true, false);
        }
    }

    ~Controller()
    {
        if (handle)
            aamotion_release(handle);
    }

    Controller(const Controller&) = delete;
    Controller& operator=(const Controller&) = delete;

    aamotion_controller handle;
    bool connected = false;
};

inline bool ready_axis(aamotion_controller controller, int axis)
{
    double enabled = 0, moving = 0;
    return aamotion_get_value(controller, axis, AAMOTION_KEYWORD_MOTOR_ON, 0, &enabled) &&
           aamotion_get_value(controller, axis, AAMOTION_KEYWORD_MOTION_STAT, 0, &moving) &&
           enabled == 1 && moving == 0;
}
