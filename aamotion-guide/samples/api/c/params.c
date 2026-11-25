/* Examples for the "Reading and writing parameters" chapter. */
#include "aamotion.h"
#include <stdio.h>
#include <stdlib.h>

void example_raw(aamotion_controller h)
{
    /* --8<-- [start:raw] */
    char reply[256];
    if (aamotion_send_command_string(h, "ASpeed=20000", reply, sizeof reply))
        printf("write: %s\n", reply);            /* "OK" */

    if (aamotion_send_command_string(h, "APos", reply, sizeof reply))
        printf("APos = %s\n", reply);            /* the value, as text */
    /* --8<-- [end:raw] */
}

void example_bulk(aamotion_controller h)
{
    /* --8<-- [start:bulk] */
    char reply[1024];
    if (aamotion_send_bulk_command_string(h, "APos;BPos;AVel[1];BVel[1]", reply, sizeof reply))
        printf("%s\n", reply);                   /* e.g. "1200>-300>15>0>" */
    /* --8<-- [end:bulk] */
}

void example_get_value(aamotion_controller h)
{
    /* --8<-- [start:get-value] */
    double pos, vel, factor;
    char unit[32];
    if (aamotion_get_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_POS, 0, &pos) &&
        aamotion_get_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_VEL, 1, &vel) &&
        aamotion_get_units(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_POS, unit, sizeof unit, &factor))
    {
        printf("APos = %g %s (factor %g), AVel[1] = %g\n", pos, unit, factor, vel);
    }
    /* --8<-- [end:get-value] */
}

void example_gen_data(aamotion_controller h)
{
    /* --8<-- [start:gen-data] */
    aamotion_write_gen_data(h, 10, 1234);       /* AGenData[10] = 1234 */
    int v = aamotion_read_gen_data(h, 10);       /* 0 on any failure: see the note */

    /* when 0 is a legitimate value, read through a raw command and check the bool */
    char reply[64];
    if (aamotion_send_command_string(h, "AGenData[10]", reply, sizeof reply))
        v = atoi(reply);
    /* --8<-- [end:gen-data] */
}
