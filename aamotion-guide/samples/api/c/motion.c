/* Examples for the "Motor, commutation and stop" and "Point-to-point motion" chapters. */
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

void example_motor_on(aamotion_controller h)
{
    /* --8<-- [start:motor-on] */
    if (!aamotion_is_auto_phase(h, AAMOTION_AXIS_REF_A))
    {
        fprintf(stderr, "commutate successfully before motor-on\n");
        return;
    }
    if (!aamotion_motor_on(h, AAMOTION_AXIS_REF_A))
        fprintf(stderr, "motor on refused (see the error hook)\n");
    /* ... */
    aamotion_motor_off(h, AAMOTION_AXIS_REF_A);
    /* --8<-- [end:motor-on] */
}

bool example_auto_phase(aamotion_controller h)
{
    /* --8<-- [start:auto-phase] */
    aamotion_auto_phase(h, AAMOTION_AXIS_REF_A);            /* returns at once */
    for (int i = 0; i < 100; ++i)                            /* up to 10 s */
    {
        if (aamotion_is_auto_phase(h, AAMOTION_AXIS_REF_A))
            return true;
        sleep_ms(100);
    }
    return false;
    /* --8<-- [end:auto-phase] */
}

void example_stop(aamotion_controller h)
{
    /* --8<-- [start:stop] */
    aamotion_stop(h, AAMOTION_AXIS_REF_A);       /* decelerate to a stop with ADecel */
    /* --8<-- [end:stop] */
}

void example_stop_rep(aamotion_controller h)
{
    /* --8<-- [start:stop-rep] */
    aamotion_stop_rep(h, AAMOTION_AXIS_REF_A);   /* end a repetitive move after the repetition in progress */
    /* --8<-- [end:stop-rep] */
}

void example_abort(aamotion_controller h)
{
    /* --8<-- [start:abort] */
    aamotion_abort(h, AAMOTION_AXIS_REF_A);      /* emergency deceleration with AEmrgDec */
    /* --8<-- [end:abort] */
}

void example_move_abs(aamotion_controller h)
{
    /* --8<-- [start:move-abs] */
    /* C has no "keep current": always pass the speed, accel and decel you want */
    if (!aamotion_move_abs(h, AAMOTION_AXIS_REF_A, 100000, 20000, 200000, 200000))
        fprintf(stderr, "move refused\n");
    /* --8<-- [end:move-abs] */
}

void example_move_abs_wait(aamotion_controller h)
{
    /* --8<-- [start:move-abs-wait] */
    if (!aamotion_move_abs(h, AAMOTION_AXIS_REF_A, 100000, 20000, 200000, 200000))
        return;
    /* poll the settling status live; aamotion_move_abs_and_wait does the same */
    for (int i = 0; i < 200; ++i)
    {
        double stat = 0;
        if (!aamotion_get_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_IN_TARGET_STAT, 0, &stat))
            break;
        if (stat == AAMOTION_IN_TARGET_STAT_TARGET_REACHED)
            return;
        sleep_ms(50);
    }
    aamotion_stop(h, AAMOTION_AXIS_REF_A);
    fprintf(stderr, "not in target: timeout or status read failed; stop requested\n");
    /* --8<-- [end:move-abs-wait] */
}

void example_move_rel(aamotion_controller h)
{
    /* --8<-- [start:move-rel] */
    aamotion_move_relative(h, AAMOTION_AXIS_REF_A, -5000, 20000, 200000, 200000);
    /* --8<-- [end:move-rel] */
}

void example_jog(aamotion_controller h)
{
    /* --8<-- [start:jog] */
    aamotion_jog(h, AAMOTION_AXIS_REF_A, 10000);    /* +10000 per second until stopped */
    sleep_ms(2000);
    aamotion_stop(h, AAMOTION_AXIS_REF_A);
    /* --8<-- [end:jog] */
}

void example_begin(aamotion_controller h)
{
    /* --8<-- [start:begin] */
    char reply[64];
    aamotion_send_command_string(h, "AMotionMode=1", reply, sizeof reply);
    aamotion_send_command_string(h, "ASpeed=20000", reply, sizeof reply);
    aamotion_send_command_string(h, "ARelTrgt=0", reply, sizeof reply);
    aamotion_send_command_string(h, "AAbsTrgt=100000", reply, sizeof reply);
    aamotion_begin(h, AAMOTION_AXIS_REF_A);
    /* --8<-- [end:begin] */
}

