/* Examples for the "Central-i" and "Optical coupling light finding" chapters. */
#ifndef _WIN32
#define _POSIX_C_SOURCE 199309L
#endif
#include "aamotion.h"
#include <stdio.h>
#include <errno.h>
#include <ctype.h>
#include <limits.h>
#include <stdlib.h>
#ifdef _WIN32
#include <windows.h>
static void sleep_ms(int ms) { Sleep(ms); }
#else
#include <time.h>
static void sleep_ms(int ms)
{
    struct timespec t = {ms / 1000, (long)(ms % 1000) * 1000000L};
    nanosleep(&t, NULL);
}
#endif

/* --8<-- [start:firmware-data] */
/* Indices and the axis A prefix must be confirmed against the installed firmware. */
bool optical_read_firmware_data(aamotion_controller h, int index, int *value)
{
    char command[64], reply[256], *end;
    if (index < 0 || !value) return false;
    snprintf(command, sizeof command, "AGenData[%d]", index);
    if (!aamotion_send_command_string(h, command, reply, sizeof reply)) return false;
    errno = 0;
    long parsed = strtol(reply, &end, 10);
    if (end == reply || errno == ERANGE || parsed < INT_MIN || parsed > INT_MAX) return false;
    while (isspace((unsigned char)*end)) ++end;
    if (*end == '>') ++end;
    while (isspace((unsigned char)*end)) ++end;
    if (*end != '\0') return false;
    *value = (int)parsed;  /* output is valid only when this function returns true */
    return true;
}

bool optical_write_firmware_data(aamotion_controller h, int index, int value)
{
    char command[80], reply[256];
    if (index < 0) return false;
    snprintf(command, sizeof command, "AGenData[%d]=%d", index, value);
    return aamotion_send_command_string(h, command, reply, sizeof reply);
}
/* --8<-- [end:firmware-data] */

void example_firmware_data(aamotion_controller h)
{
    char input[64], *end;
    printf("Axis A GenData index from your firmware table (blank cancels): ");
    if (!fgets(input, sizeof input, stdin)) return;
    errno = 0;
    long index = strtol(input, &end, 10);
    if (end == input || errno == ERANGE || index < 0 || index > INT_MAX) return;
    while (isspace((unsigned char)*end)) ++end;
    if (*end) return;
    int value;
    if (optical_read_firmware_data(h, (int)index, &value))
        printf("AGenData[%ld] = %d\n", index, value);
    else
        fprintf(stderr, "read/parse failed; inspect errors and the firmware table\n");
}

void example_ci(aamotion_controller h)
{
    /* --8<-- [start:ci] */
    aamotion_ci_connect(h, AAMOTION_AXIS_REF_E);
    aamotion_linear_absolute_xyt(h, AAMOTION_AXIS_REF_A, 100000, 50000, 9000, 40000, 0);
    aamotion_ci_group_d_out_set_bit(h, AAMOTION_AXIS_REF_A, 2);
    aamotion_cnc_begin(h);
    /* --8<-- [end:ci] */
}

void example_spiral(aamotion_controller h)
{
    /* --8<-- [start:spiral] */
    aamotion_optical_coupling oc = aamotion_optical_coupling_create(h);
    if (!oc)
        return;
    aamotion_optical_coupling_apply_spiral_config(oc, 1, 0, 1, 50, 2000, 10, 2000, 5000, 1000);
    aamotion_write_gen_data(h, 602, 0);                      /* clear the previous result */
    if (!aamotion_optical_coupling_start_spiral_scan(oc))
    {
        aamotion_optical_coupling_release(oc);
        return;
    }
    /* Only for firmware matching this SDK mapping: result 602, stop 501. */
    int result = 0;
    bool finished = false;
    for (int i = 0; i < 600; ++i)
    {
        if (!optical_read_firmware_data(h, 602, &result)) break;
        if (result != 0) { finished = true; break; }
        sleep_ms(100);
    }
    if (!finished)
    {
        aamotion_optical_coupling_emergency_stop(oc);
        fprintf(stderr, "scan timeout/read failure; stop requested, verify actual state\n");
        aamotion_optical_coupling_release(oc);
        return;
    }
    if (aamotion_optical_coupling_is_success(oc))
        printf("max power %d at (%d, %d)\n", aamotion_optical_coupling_get_max_power(oc),
               aamotion_optical_coupling_get_max_power_x_pos(oc), aamotion_optical_coupling_get_max_power_y_pos(oc));
    else
        printf("failed, error %d\n", aamotion_optical_coupling_get_error_code(oc));
    aamotion_optical_coupling_release(oc);
    /* --8<-- [end:spiral] */
}

