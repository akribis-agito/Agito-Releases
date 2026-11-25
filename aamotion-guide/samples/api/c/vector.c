/* Examples for the "Vector motion", "Electronic cam" and "Force and current control" chapters. */
#include "aamotion.h"
#include <stdio.h>

void example_vector_linear(aamotion_controller h)
{
    /* --8<-- [start:linear] */
    double targets[] = { 80000, 40000 };     /* B, then C: the order of the mask */
    if (!aamotion_vector_linear_with_mask(h, AAMOTION_AXIS_REF_B, AAMOTION_AXIS_REF_B | AAMOTION_AXIS_REF_C,
                                          30000, 300000, 300000, 1000000, 4, targets, 2))
        fprintf(stderr, "vector move refused\n");
    /* --8<-- [end:linear] */
}

void example_vector_arc(aamotion_controller h)
{
    /* --8<-- [start:arc] */
    aamotion_vector_arc(h, AAMOTION_AXIS_REF_A, AAMOTION_AXIS_REF_A, AAMOTION_AXIS_REF_B,
                        50000, 50000, 100000, 50000, 0, 0, 20000, 200000, 200000, 1000000, 4);
    /* dir 0 = counter-clockwise (AVecArcDir), circles 0 = no extra turns */
    /* --8<-- [end:arc] */
}

void example_vector_pause(aamotion_controller h)
{
    /* --8<-- [start:pause] */
    aamotion_vector_pause(h, AAMOTION_AXIS_REF_A);
    aamotion_vector_continue(h, AAMOTION_AXIS_REF_A);
    aamotion_stop_vec(h, AAMOTION_AXIS_REF_A);
    /* --8<-- [end:pause] */
}

void example_force(aamotion_controller h)
{
    /* --8<-- [start:press] */
    aamotion_force_curr_set_motion(h, AAMOTION_AXIS_REF_A, 50000, 0, 40000, 400000, 400000);
    aamotion_force_curr_set_slow_approach(h, AAMOTION_AXIS_REF_A, 2000, 45000, 0, 1);
    aamotion_begin(h, AAMOTION_AXIS_REF_A);
    /* --8<-- [end:press] */
}

void example_modes(aamotion_controller h)
{
    /* --8<-- [start:modes] */
    aamotion_go_to_force_mode(h, AAMOTION_AXIS_REF_A);
    aamotion_go_to_curr_mode(h, AAMOTION_AXIS_REF_A);
    aamotion_go_to_pos_mode(h, AAMOTION_AXIS_REF_A);
    aamotion_set_position(h, AAMOTION_AXIS_REF_A, 0);      /* counts */
    /* --8<-- [end:modes] */
}
