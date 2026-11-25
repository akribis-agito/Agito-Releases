/* Examples for the "Digital I/O" and "PEG and position lock" chapters. */
#include "aamotion.h"
#include <stdio.h>

void example_outputs(aamotion_controller h)
{
    /* --8<-- [start:outputs] */
    aamotion_d_out_set_bit(h, AAMOTION_AXIS_REF_A, 1);      /* output 1 on (1-based) */
    aamotion_d_out_toggle_bit(h, AAMOTION_AXIS_REF_A, 2);
    aamotion_d_out_clear_bit(h, AAMOTION_AXIS_REF_A, 1);
    int out1 = aamotion_get_d_out_port(h, AAMOTION_AXIS_REF_A, 0);   /* output 1 is bit 0; 0 also on failure */
    /* --8<-- [end:outputs] */
    (void)out1;
}

void example_inputs(aamotion_controller h)
{
    /* --8<-- [start:inputs] */
    int in3 = aamotion_get_d_in_port(h, AAMOTION_AXIS_REF_A, 2);     /* input 3 is register bit 2 */

    /* when a failed read must not look like 0, read the register through a raw command */
    char reply[64];
    if (aamotion_send_command_string(h, "ADInPort", reply, sizeof reply))
        printf("inputs: %s\n", reply);
    /* --8<-- [end:inputs] */
    (void)in3;
}

void example_gap_peg(aamotion_controller h)
{
    /* --8<-- [start:gap-peg] */
    /* every argument is written: pass the pulse resolution and width you want */
    aamotion_set_event_fixed_gap_peg(h, AAMOTION_AXIS_REF_A, 10000, 1000, 90000, 1, 0, 10);
    aamotion_move_abs(h, AAMOTION_AXIS_REF_A, 100000, 20000, 200000, 200000);
    /* --8<-- [end:gap-peg] */
}

void example_table_peg(aamotion_controller h)
{
    /* --8<-- [start:table-peg] */
    char reply[128];
    const char* setup[] = { "AEventOn=0", "AEventType=2", "AEventPulseRes=0",
                            "AEventPulseWid=10", "AEventTableWid[1]=-1" };
    for (size_t i = 0; i < sizeof setup / sizeof setup[0]; ++i)
    {
        if (!aamotion_send_command_string(h, setup[i], reply, sizeof reply))
        {
            puts(reply);
            return;
        }
    }
    const double positions[] = { 12000, 15500, 23000, 41000 };
    for (int i = 0; i < 4; ++i)
    {
        if (!aamotion_set_event_table_value(h, AAMOTION_AXIS_REF_A, i + 1, positions[i]) ||
            !aamotion_set_event_table_select(h, AAMOTION_AXIS_REF_A, i + 1, 1))
            return;
    }
    if (aamotion_set_event_table_begin_index(h, AAMOTION_AXIS_REF_A, 1) &&
        aamotion_set_event_table_end_index(h, AAMOTION_AXIS_REF_A, 4) &&
        !aamotion_send_command_string(h, "AEventOn=1", reply, sizeof reply))
        puts(reply);
    /* --8<-- [end:table-peg] */
}

void example_lock(aamotion_controller h)
{
    /* --8<-- [start:lock] */
    aamotion_lock_dis_enable(h, AAMOTION_AXIS_REF_A);
    aamotion_set_lock(h, AAMOTION_AXIS_REF_A, false, 1, 1);
    aamotion_lock_enable(h, AAMOTION_AXIS_REF_A);
    double before, after, latched;
    if (aamotion_get_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_LOCK_CNTR, 0, &before))
    {
        puts("Apply one edge to the selected input, then press Enter.");
        getchar();
        if (aamotion_get_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_LOCK_CNTR, 0, &after))
        {
            if (after == before)
                puts("No new capture.");
            else if (aamotion_get_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_LOCK_VAL, 0, &latched))
                printf("Captured position: %g\n", latched);
        }
    }
    aamotion_lock_dis_enable(h, AAMOTION_AXIS_REF_A);
    /* --8<-- [end:lock] */
}
