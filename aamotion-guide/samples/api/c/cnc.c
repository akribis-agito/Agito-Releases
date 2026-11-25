/* Examples for the "User programs" and "CNC interpolation" chapters. */
#ifndef _WIN32
#define _POSIX_C_SOURCE 199309L
#endif
#include "aamotion.h"
#ifdef _WIN32
#include <windows.h>
static void sleep_ms(int ms) { Sleep(ms); }
#else
#include <time.h>
static void sleep_ms(int ms) { struct timespec t = {ms / 1000, (long)(ms % 1000) * 1000000L}; nanosleep(&t, NULL); }
#endif
#include <stdio.h>

void example_prog(aamotion_controller h)
{
    /* --8<-- [start:run] */
    aamotion_prog_run(h, AAMOTION_AXIS_REF_A, 1, 1);    /* thread 1 runs task 1 */
    aamotion_prog_halt(h, AAMOTION_AXIS_REF_A, 1);
    aamotion_prog_reset(h, AAMOTION_AXIS_REF_A, 1);
    aamotion_prog_halt_all(h, AAMOTION_AXIS_REF_A);
    aamotion_auto_exec(h, AAMOTION_AXIS_REF_A, true);
    /* --8<-- [end:run] */
}

void example_square(aamotion_controller h)
{
    /* --8<-- [start:square] */
    /* CNC group A: X = axis A, Y = axis B; integer counts */
    if (!aamotion_clear_buffer(h) ||
        !aamotion_set_start_positions(h, AAMOTION_AXIS_REF_A | AAMOTION_AXIS_REF_B, 0, 0, 0) ||
        !aamotion_set_motion_profile(h, 100, 2000000, 2000000, 0) ||
        !aamotion_linear_absolute_xy(h, 100000, 0, 50000, 0) ||
        !aamotion_linear_absolute_xy(h, 100000, 100000, 50000, 0) ||
        !aamotion_linear_absolute_xy(h, 0, 100000, 50000, 0) ||
        !aamotion_linear_absolute_xy(h, 0, 0, 50000, 0))
    {
        fprintf(stderr, "CNC download failed; Begin was not sent\n");
        return;
    }
    if (!aamotion_cnc_begin(h))
        fprintf(stderr, "CNC Begin refused; inspect error callback\n");
    /* --8<-- [end:square] */
}

void example_arc(aamotion_controller h)
{
    /* --8<-- [start:arc] */
    /* Assumes X and Y are already at (0, 0). */
    const int xy = AAMOTION_AXIS_REF_A | AAMOTION_AXIS_REF_B;
    if (!aamotion_clear_buffer(h) ||
        !aamotion_set_start_positions(h, xy, 0, 0, 0) ||
        !aamotion_linear_absolute_xy(h, 50000, 0, 40000, 0) ||
        !aamotion_arc_xy(h, 100000, 50000, 50000, 50000, 0, 40000, 0) ||
        !aamotion_delay(h, 200))
    {
        fprintf(stderr, "CNC download failed. Check the controller error.\n");
        return;
    }
    if (!aamotion_cnc_begin(h))
        fprintf(stderr, "CNC Begin failed.\n");
    /* --8<-- [end:arc] */
}

void example_pause(aamotion_controller h)
{
    /* --8<-- [start:pause] */
    aamotion_pause(h);
    aamotion_resume(h);
    aamotion_group_stop(h);
    aamotion_clear_buffer(h);
    /* --8<-- [end:pause] */
}

void example_wait_done(aamotion_controller h)
{
    /* --8<-- [start:wait] */
    for (int i = 0; i < 600; ++i)
    {
        if (aamotion_is_cnc_completed(h, AAMOTION_AXIS_REF_A))
            return;
        sleep_ms(50);
    }
    aamotion_group_stop(h);
    fprintf(stderr, "CNC wait failed/timed out; group Stop requested, verify actual state\n");
    /* --8<-- [end:wait] */
}

