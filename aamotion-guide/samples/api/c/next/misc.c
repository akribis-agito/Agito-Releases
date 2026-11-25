/* More C examples for the CNC, fast-read cache and optical coupling chapters. */
#include "aamotion.h"
#include <stdio.h>

void example_group_arc(aamotion_controller h)
{
    /* --8<-- [start:arc] */
    /* Assumes axes A and B are already at (0, 0). */
    const int xy = AAMOTION_AXIS_REF_A | AAMOTION_AXIS_REF_B;
    if (!aamotion_clear_buffer(h) ||
        !aamotion_set_start_positions(h, xy, 0, 0, 0) ||
        !aamotion_linear_absolute_xy(h, 50000, 0, 40000, 0) ||
        !aamotion_arc(h, xy, 100000, 50000, 0, 50000, 1, 40000, 0, 0))
    {
        fprintf(stderr, "CNC download failed. Check the controller error.\n");
        return;
    }
    if (!aamotion_cnc_begin(h))
        fprintf(stderr, "CNC Begin failed.\n");
    /* --8<-- [end:arc] */
}

void example_cache(aamotion_controller h)
{
    /* --8<-- [start:cache] */
    const int keywords[] = { AAMOTION_KEYWORD_POS, AAMOTION_KEYWORD_MOTION_STAT };
    aamotion_register_cache_keywords(h, AAMOTION_AXIS_REF_A, keywords, 2);
    aamotion_enable_fast_read_cache(h, 5);          /* refresh every 5 ms in the background */

    /* the C interface has no cached-value read: aamotion_get_value always asks the controller */
    double age = -1;
    aamotion_get_cache_age_ms(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_POS, &age);   /* -1: not cached */
    printf("cached APos is %.0f ms old\n", age);

    aamotion_refresh_cache_now(h);                    /* one refresh at once, outside the interval */
    aamotion_disable_fast_read_cache(h);
    /* --8<-- [end:cache] */
}

void example_scan_results(aamotion_optical_coupling oc)
{
    /* --8<-- [start:oc-result] */
    /* the whole result of the last scan in one read */
    aamotion_spiral_scan_result r;
    if (aamotion_optical_coupling_get_spiral_scan_result(oc, &r) && aamotion_optical_coupling_is_success(oc))
        printf("max power %d at (%d, %d)\n", r.maxPower, r.maxPowerXPos, r.maxPowerYPos);

    aamotion_single_axis_scan_result s;
    if (aamotion_optical_coupling_get_single_axis_scan_result(oc, &s))
        printf("single axis: max power %d at %d\n", s.maxPower, s.maxPowerPos);
    /* --8<-- [end:oc-result] */
}
