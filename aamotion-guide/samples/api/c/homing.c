/* Examples for the "Homing" and "Advanced motion" chapters. */
#ifndef _WIN32
#define _POSIX_C_SOURCE 199309L
#endif
#include "aamotion.h"
#include <stdio.h>
#ifdef _WIN32
#include <windows.h>
static void sleep_ms(int ms) { Sleep(ms); }
#else
#include <time.h>
static void sleep_ms(int ms)
{
    struct timespec t = { ms / 1000, (long)(ms % 1000) * 1000000L };
    nanosleep(&t, NULL);
}
#endif

/* --8<-- [start:wait-homed] */
static bool wait_homed(aamotion_controller h, int axis, int timeout_ms)
{
    for (int waited = 0; waited < timeout_ms; waited += 100)
    {
        if (aamotion_is_homed(h, axis))
            return true;                                   /* HomingStat == 100 */
        double stat;
        if (aamotion_get_value(h, axis, AAMOTION_KEYWORD_HOMING_STAT, 0, &stat) && stat < 0)
        {
            char text[256];
            aamotion_get_homing_stat_des((int)stat, text, sizeof text);
            fprintf(stderr, "homing failed: %s\n", text);
            return false;
        }
        sleep_ms(100);
    }
    return false;
}
/* --8<-- [end:wait-homed] */

void example_home(aamotion_controller h)
{
    /* --8<-- [start:home] */
    aamotion_home(h, AAMOTION_AXIS_REF_A);                 /* the sequence configured in the controller */
    bool ok = wait_homed(h, AAMOTION_AXIS_REF_A, 30000);
    /* --8<-- [end:home] */
    (void)ok;
}

void example_home_options(aamotion_controller h)
{
    /* --8<-- [start:home-options] */
    /* limit switch first, then the index; offset 1000; timeout 20 s */
    aamotion_home_with_options(h, AAMOTION_AXIS_REF_A, true, true, 5000, 100000, 500000, 1000, 20000);
    bool ok = wait_homed(h, AAMOTION_AXIS_REF_A, 25000);
    /* --8<-- [end:home-options] */
    (void)ok;
}

void example_hard_stop(aamotion_controller h)
{
    /* --8<-- [start:hard-stop] */
    aamotion_home_to_hard_stop(h, AAMOTION_AXIS_REF_A, 2000, 2000, 50000, 500000, 500, 20000);
    bool ok = wait_homed(h, AAMOTION_AXIS_REF_A, 25000);
    /* --8<-- [end:hard-stop] */
    (void)ok;
}

void example_stuck_then_index(aamotion_controller h)
{
    /* --8<-- [start:stuck] */
    /* stuck: below 500 counts/s with more than 800 mA for 50 ms (controller units) */
    bool ok = aamotion_home_to_hard_stop_by_stuck_then_index(h, AAMOTION_AXIS_REF_A,
        1, -1, 500, 800, 50, 5000, 1000, 50000, 20000)
        && wait_homed(h, AAMOTION_AXIS_REF_A, 60000);
    /* --8<-- [end:stuck] */
    (void)ok;
}

void example_from_file(aamotion_controller h)
{
    /* --8<-- [start:from-file] */
    if (!aamotion_home_from_file(h, AAMOTION_AXIS_REF_A, "C:\\MyApp\\homing.txt"))
        fprintf(stderr, "could not read the homing file or start homing\n");
    /* --8<-- [end:from-file] */
}

void example_repetitive(aamotion_controller h)
{
    /* --8<-- [start:repetitive] */
    aamotion_move_abs_repetitive(h, AAMOTION_AXIS_REF_A, 50000, 200, 20000, 200000, 200000);
    sleep_ms(10000);
    aamotion_stop_rep(h, AAMOTION_AXIS_REF_A);
    /* --8<-- [end:repetitive] */
}

void example_two_speed(aamotion_controller h)
{
    /* --8<-- [start:two-speed] */
    /* -1 for speedChgDir: the API default */
    aamotion_move_abs_two_speed(h, AAMOTION_AXIS_REF_A, 100000, 50000, 5000, 80000, 200000, 200000, -1);
    bool ok = aamotion_move_rel_two_speed_and_wait(h, AAMOTION_AXIS_REF_A, -100000, 50000, 5000, 80000,
                                                   200000, 200000, -1, 30000);
    /* --8<-- [end:two-speed] */
    (void)ok;
}
