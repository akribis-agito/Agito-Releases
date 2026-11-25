// 02-first-move: connect, commutate, motor on, move axis A by a relative distance and wait in target,
// then motor off and disconnect. Every step prints what it did, so the output shows how far it got.
//   sample02_first_move "D:\sim\AGM800\firmware_sim.exe"   firmware simulator (absolute path)
//   sample02_first_move 172.1.1.101                        real controller: make sure axis A may move 10000
#include "aamotion.h"
#include <chrono>
#include <cmath>
#include <cstdio>
#include <string>
#include <thread>

static const int A = AAMOTION_AXIS_REF_A;
static const double Distance = 10000, Speed = 20000, Accel = 200000;

static void on_error(int code, const char* message, const char* exception, void* user)
{
    std::fprintf(stderr, "[ERR %d] %s | %s\n", code, message ? message : "", exception ? exception : "");
}

static double read(aamotion_controller h, int keyword, int index = 0)
{
    double v = NAN;
    aamotion_get_value(h, A, keyword, index, &v);
    return v;
}

static void sleep_ms(int ms) { std::this_thread::sleep_for(std::chrono::milliseconds(ms)); }

// --8<-- [start:wait]
// Read live status: 4 means settled; 0 MotionStat means only motion ended.
static bool wait_in_target(aamotion_controller h, int timeout_ms)
{
    const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeout_ms);
    while (std::chrono::steady_clock::now() < deadline)
    {
        double stat = 0;
        if (!aamotion_get_value(h, A, AAMOTION_KEYWORD_IN_TARGET_STAT, 0, &stat))
            return false;
        if (stat == AAMOTION_IN_TARGET_STAT_TARGET_REACHED)
            return true;
        sleep_ms(50);
    }
    return false;
}
// --8<-- [end:wait]

int main(int argc, char** argv)
{
    std::string target = argc > 1 ? argv[1] : "172.1.1.101";
    if (aamotion_abi_version() != AAMOTION_ABI_VERSION)
        return 1;

    // --8<-- [start:connect]
    aamotion_controller h = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);
    if (!h)
    {
        std::fprintf(stderr, "initialize failed: is AACommServerNative beside the library?\n");
        return 1;
    }
    aamotion_set_error_occurred_handler(h, on_error, nullptr);
    bool is_sim = target.size() > 4 && (target.compare(target.size() - 4, 4, ".exe") == 0 || target.compare(target.size() - 4, 4, ".EXE") == 0);
    bool ok = is_sim ? aamotion_connect_simulation(h, target.c_str(), "AGM800", nullptr, nullptr)
                     : aamotion_connect(h, target.c_str(), false, false);
    std::printf("1. connect %s: %s\n", target.c_str(), ok ? "true" : "false");
    if (!ok)
    {
        aamotion_disconnect(h);
        return 1;
    }
    // --8<-- [end:connect]

    int rc = 0;
    // --8<-- [start:enable]
    if (!aamotion_is_auto_phase(h, A))
    {
        aamotion_auto_phase(h, A);
        int waited = 0;
        while (!aamotion_is_auto_phase(h, A) && waited < 10000)
        {
            sleep_ms(100);
            waited += 100;
        }
    }
    bool commutated = aamotion_is_auto_phase(h, A);
    std::printf("2. commutated: %s\n", commutated ? "true" : "false");
    bool enabled = commutated && aamotion_motor_on(h, A);
    if (enabled)
        std::printf("3. motor on: MotorOn = %.0f\n", read(h, AAMOTION_KEYWORD_MOTOR_ON));
    // --8<-- [end:enable]

    if (!enabled)
        rc = 3;
    else
    {
        // --8<-- [start:move]
        double start = read(h, AAMOTION_KEYWORD_POS);
        bool accepted = std::isfinite(start) && aamotion_move_abs(h, A, start + Distance, Speed, Accel, Accel);
        bool arrived = accepted && wait_in_target(h, 10000);
        std::printf("4. move %.0f -> %.0f: in target = %s, Pos = %.0f\n", start, start + Distance,
                    arrived ? "true" : "false", read(h, AAMOTION_KEYWORD_POS));
        if (!arrived)
            rc = 4;
        // --8<-- [end:move]
    }

    // --8<-- [start:cleanup]
    aamotion_stop(h, A);          // harmless when already still
    aamotion_motor_off(h, A);
    aamotion_disconnect(h);       // also frees h
    std::printf("5. motor off, disconnected\n");
    // --8<-- [end:cleanup]
    return rc;
}

