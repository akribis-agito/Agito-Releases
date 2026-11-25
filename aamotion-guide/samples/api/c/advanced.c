/* Examples for the "Advanced motion" chapter: PVAJ and electronic gearing. */
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

typedef struct { double p, v, a, j; } pvaj_point;

int example_pvaj(aamotion_controller h, const pvaj_point* points, int count)
{
    /* --8<-- [start:pvaj] */
    /* points in counts, counts/s, counts/s^2, counts/s^3; count 1..AAMOTION_PVAJ_MAX_POINTS */
    const int a = AAMOTION_AXIS_REF_A;
    if (!aamotion_pvaj_supported(h))
        return 1;                      /* needs an AGM800 with firmware 5.3.0 or later */
    if (!aamotion_pvaj_arm(h, a, false) ||
        !aamotion_pvaj_write_header(h, a, count, 1, AAMOTION_PVAJ_POSITION_ABSOLUTE))
        return 2;
    for (int k = 1; k <= count; k++)
    {
        const pvaj_point* p = &points[k - 1];
        if (!aamotion_pvaj_write_point(h, a, k, p->p, p->v, p->a, p->j))
            return 2;
    }
    if (!aamotion_pvaj_validate(h, a, AAMOTION_PVAJ_VALIDATE_FULL) ||
        !aamotion_pvaj_arm(h, a, true) ||
        !aamotion_pvaj_begin(h, a, false))
        return 3;                      /* the error callback names the refusal */

    int state = 0, index = 0, left = 0;
    for (int attempt = 0; attempt < 300; attempt++)
    {
        if (!aamotion_pvaj_get_status(h, a, &state, &index, &left))
            return 4;
        if (state != AAMOTION_PVAJ_STATE_EXECUTING && left == 0)
            return 0;                  /* ended: check the position and MotionReason */
        sleep_ms(100);
    }
    aamotion_stop(h, a);
    return 5;
    /* --8<-- [end:pvaj] */
}

void example_gearing(aamotion_controller h)
{
    /* --8<-- [start:gearing] */
    /* B follows A's position command at 1:2 (32768 / 65536), direct gearing */
    aamotion_gear_config gear = { 0 };
    gear.master = "APosRef";
    gear.ratioNumerator = 32768;
    gear.ratioDenominator = 65536;
    gear.filter = 64;                  /* 64: unfiltered */
    gear.masterModRev = 0;
    gear.motionMode = AAMOTION_MOTION_MODE_GEAR_DIRECT;
    if (!aamotion_gear_begin(h, AAMOTION_AXIS_REF_B, &gear))
        fprintf(stderr, "gearing refused: see the error callback\n");
    /* ... later */
    aamotion_stop(h, AAMOTION_AXIS_REF_B);
    /* --8<-- [end:gearing] */
}
