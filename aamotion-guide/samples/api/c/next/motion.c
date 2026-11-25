/* More C examples for the motor, point-to-point, homing and advanced-motion chapters. */
#include "aamotion.h"
#include <stdio.h>

void example_commutated(aamotion_controller h)
{
    /* --8<-- [start:commutated] */
    /* starts commutation unless already done, then waits up to 5000 ms */
    aamotion_commutated(h, AAMOTION_AXIS_REF_A, 5000);
    if (!aamotion_is_commutated(h, AAMOTION_AXIS_REF_A))
        printf("not commutated: read AComtStatus[1]\n");
    /* --8<-- [end:commutated] */
}

void example_move_abs_wait(aamotion_controller h)
{
    /* --8<-- [start:move-abs-wait] */
    /* move and wait in target, at most 10000 ms; false on timeout: the motion may still be running */
    if (!aamotion_move_abs_and_wait(h, AAMOTION_AXIS_REF_A, 100000, 20000, 200000, 200000, 10000))
        aamotion_stop(h, AAMOTION_AXIS_REF_A);
    /* --8<-- [end:move-abs-wait] */
}

void example_move_rel_wait(aamotion_controller h)
{
    /* --8<-- [start:move-rel] */
    aamotion_move_rel_and_wait(h, AAMOTION_AXIS_REF_A, 5000, 20000, 200000, 200000, 5000);
    /* --8<-- [end:move-rel] */
}

void example_multi_axis(aamotion_controller h)
{
    /* --8<-- [start:multi-axis] */
    const int axes[] = { AAMOTION_AXIS_REF_A, AAMOTION_AXIS_REF_B };
    const double targets[] = { 10000, 5000 };
    /* Arguments after count: speed, acceleration, deceleration. */
    if (!aamotion_move_abs_multi(h, axes, targets, 2, 2000, 10000, 10000)) {
        printf("Batch failed. Check the state of both axes.\n");
        return;
    }
    /* Check both axes for settling before commanding the next move. */
    /* --8<-- [end:multi-axis] */
}

void example_index_homing(aamotion_controller h)
{
    /* --8<-- [start:index] */
    if (aamotion_lock_src_zero_supported(h, AAMOTION_AXIS_REF_A))
        /* the main encoder index, forward at 2000 counts/s, 30 s per motion step */
        aamotion_home_to_index(h, AAMOTION_AXIS_REF_A, 0, 1, 2000, 50000, 30000);

    /* the reverse limit fast, then the index slowly */
    aamotion_home_to_limit_then_index(h, AAMOTION_AXIS_REF_B, 0, -1, 10000, 1000, 100000, 30000);
    /* --8<-- [end:index] */
}

void example_repetitive_cycle(aamotion_controller h)
{
    /* --8<-- [start:repetitive] */
    /* 100 round trips between here and 50000, 200 ms at each end, then stop by itself */
    aamotion_move_abs_repetitive_cycle(h, AAMOTION_AXIS_REF_A, 50000, 200, 100, 20000, 200000, 200000);
    /* --8<-- [end:repetitive] */
}

void example_sine_ptp(aamotion_controller h)
{
    /* --8<-- [start:sine-ptp] */
    aamotion_sine_ptp_config move = { 0 };
    move.motionMode = 20;            /* Sine PTP, single */
    move.absTrgt = 100000;
    move.speed = 50000;
    move.accel = 500000;
    move.decel = 500000;
    move.jerkInAcc = 5000000;
    move.jerkInDec = 5000000;        /* preCruiseSpd 0: no pre-cruise phase */
    if (!aamotion_sine_ptp_begin(h, AAMOTION_AXIS_REF_A, &move))
        printf("refused: see the error callback\n");

    move.absTrgt = 120000;           /* change the target while it moves */
    aamotion_sine_ptp_commit(h, AAMOTION_AXIS_REF_A, &move);
    /* --8<-- [end:sine-ptp] */
}

void example_commit(aamotion_controller h)
{
    /* --8<-- [start:commit] */
    /* targets written to A and B first, then applied in the same control cycle */
    aamotion_set_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_ABS_TRGT, 0, 150000);
    aamotion_set_value(h, AAMOTION_AXIS_REF_B, AAMOTION_KEYWORD_ABS_TRGT, 0, 80000);
    aamotion_commit_motion(h, AAMOTION_AXIS_REF_A | AAMOTION_AXIS_REF_B);
    /* --8<-- [end:commit] */
}
