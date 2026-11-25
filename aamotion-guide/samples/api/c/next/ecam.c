/* More C examples for the "Electronic cam" chapter. Slave B follows master A. */
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

#define POSREF_CAN_CODE 24       /* the master keyword is a CAN code: on an AGM800 24 = PosRef, 2 = Pos */

void example_ecam_table(aamotion_controller h)
{
    /* --8<-- [start:ecam-table] */
    /* table 1: 360 points of a sine cam in GenData from index 100 */
    aamotion_ecam_create_sinusoidal_table(h, AAMOTION_AXIS_REF_B, 20000, 0, 360, 100, 1);
    /* one cam point every 100 master counts, master = A's PosRef, many cycles, master offset 0 */
    aamotion_ecam_configure_table(h, AAMOTION_AXIS_REF_B, 100, 459, 100, 459, 2147483647, POSREF_CAN_CODE, 100, 1, 0);

    /* table 2: your own profile, a smooth rise of 10000 over 100 points */
    int profile[100];
    for (int i = 0; i < 100; ++i)
    {
        double t = i / 99.0;
        profile[i] = (int)(10000 * t * t * (3 - 2 * t));      /* smoothstep: zero slope at both ends */
    }
    aamotion_ecam_set_table_range(h, AAMOTION_AXIS_REF_B, 600, 699, 2);
    aamotion_ecam_set_table_data(h, AAMOTION_AXIS_REF_B, profile, 100, 2);    /* GenData[600..699] */
    aamotion_ecam_set_master(h, AAMOTION_AXIS_REF_B, POSREF_CAN_CODE, 2);
    aamotion_ecam_set_master_ini(h, AAMOTION_AXIS_REF_B, 0, 2);
    aamotion_ecam_set_gap(h, AAMOTION_AXIS_REF_B, 200, 2);
    aamotion_ecam_set_cycle_params(h, AAMOTION_AXIS_REF_B, 600, 699, 1, 2);    /* run it once */
    /* --8<-- [end:ecam-table] */
}

void example_ecam_run(aamotion_controller h)
{
    /* --8<-- [start:ecam-run] */
    aamotion_ecam_start(h, AAMOTION_AXIS_REF_B, 1);
    aamotion_jog(h, AAMOTION_AXIS_REF_A, 10000);          /* move the master for 5 s */
    sleep_ms(5000);
    int cycles = 0;
    if (aamotion_ecam_is_active(h, AAMOTION_AXIS_REF_B) && aamotion_ecam_get_cycle_count(h, AAMOTION_AXIS_REF_B, 1, &cycles))
        printf("cycles: %d\n", cycles);
    aamotion_stop(h, AAMOTION_AXIS_REF_A);
    aamotion_ecam_stop(h, AAMOTION_AXIS_REF_B);
    /* --8<-- [end:ecam-run] */
}

void example_ecam_linear(aamotion_controller h)
{
    /* --8<-- [start:ecam-linear] */
    /* table 3: a straight line from 0 to 50000 in 50 points, GenData from 800 */
    aamotion_ecam_create_linear_table(h, AAMOTION_AXIS_REF_B, 0, 50000, 50, 800, 3);
    /* --8<-- [end:ecam-linear] */
}
