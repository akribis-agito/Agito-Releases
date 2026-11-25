/* Cookbook recipes in C. */
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

/* --8<-- [start:power-up] */
static void on_error(int code, const char* sent, const char* text, void* user)
{
    (void)user;
    fprintf(stderr, "[ERR %d] %s: %s\n", code, sent ? sent : "", text ? text : "");
}

static bool wait_flag(aamotion_controller h, bool (*done)(aamotion_controller, int), int timeout_ms)
{
    for (int t = 0; t < timeout_ms; t += 50)
    {
        if (done(h, AAMOTION_AXIS_REF_A))
            return true;
        sleep_ms(50);
    }
    return false;
}

int power_up_and_move(const char* ip)
{
    if (aamotion_abi_version() != AAMOTION_ABI_VERSION)
        return 1;
    aamotion_controller h = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);
    if (!h)
        return 2;                                           /* no server beside the library */
    aamotion_set_error_occurred_handler(h, on_error, NULL);

    int rc = 0;
    if (!aamotion_connect(h, ip, false, false))            { rc = 3; goto done; }
    if (!aamotion_is_auto_phase(h, AAMOTION_AXIS_REF_A))
    {
        aamotion_auto_phase(h, AAMOTION_AXIS_REF_A);
        if (!wait_flag(h, aamotion_is_auto_phase, 10000))  { rc = 5; goto done; }
    }
    if (!aamotion_motor_on(h, AAMOTION_AXIS_REF_A))         { rc = 4; goto done; }
    aamotion_home(h, AAMOTION_AXIS_REF_A);
    if (!wait_flag(h, aamotion_is_homed, 30000))            { rc = 6; goto done; }
    if (!aamotion_move_abs(h, AAMOTION_AXIS_REF_A, 10000, 20000, 200000, 200000)) { rc = 7; goto done; }

    double pos = 0;
    aamotion_get_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_POS, 0, &pos);
    printf("moving; APos = %g\n", pos);
done:
    aamotion_disconnect(h);                                 /* also frees h */
    return rc;
}
/* --8<-- [end:power-up] */

