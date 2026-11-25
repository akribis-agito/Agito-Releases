/* More C examples for the "Error handling", "Reading and writing parameters" and "User programs" chapters. */
#include "aamotion.h"
#include <stdio.h>

/* --8<-- [start:error-reported] */
/* where each error came from; the text arrives through the error-occurred callback */
static void on_error_reported(int code, int source, int axis, int keyword,
                              void* user)
{
    (void)user;
    if (source == AAMOTION_CONTROLLER_ERROR_SOURCE_FAST_READ_REFRESH)
        printf("ERR %d in a cache refresh (axis %d, keyword %d)\n",
               code, axis, keyword);
}

void example_error_reported(aamotion_controller h)
{
    aamotion_set_error_reported_handler(h, on_error_reported, NULL);
    /* ... */
    aamotion_clear_error_reported_handler(h);
}
/* --8<-- [end:error-reported] */

void example_set_value(aamotion_controller h)
{
    /* --8<-- [start:set-value] */
    /* the write twin of aamotion_get_value: User Units applied, index 0 for a scalar */
    if (!aamotion_set_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_SPEED, 0, 20000))
        printf("refused (see the error callback)\n");
    double speed = 0;
    aamotion_get_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_SPEED, 0, &speed);
    /* --8<-- [end:set-value] */
}

void example_gen_data(aamotion_controller h)
{
    /* --8<-- [start:gen-data] */
    /* a read returns true and writes the value only when it succeeded: a failed read is never a 0 */
    float f = 0;
    double d = 0;
    int p = 0;
    aamotion_write_gen_data_f(h, 1, 3.5f);            /* AGenDataF[1] */
    if (aamotion_read_gen_data_f(h, 1, &f))
        printf("GenDataF[1] = %g\n", f);
    aamotion_write_gen_data_d(h, 1, 0.125);           /* AGenDataD[1] */
    aamotion_read_gen_data_d(h, 1, &d);
    aamotion_write_user_param(h, 1, 42);              /* AUserParam[1], index from 1 */
    aamotion_read_user_param(h, 1, &p);
    /* --8<-- [end:gen-data] */
}

void example_par_files(aamotion_controller h)
{
    /* --8<-- [start:par-files] */
    char message[512];
    /* blocks until done; false with the reason in message */
    if (!aamotion_upload_parameters(h, "C:\\backup\\axis.par", message, sizeof message))
        printf("upload failed: %s\n", message);

    /* write the file back; true = recalculate filters and save to flash */
    if (!aamotion_download_parameters(h, "C:\\backup\\axis.par", true, message, sizeof message))
        printf("download failed: %s\n", message);
    /* --8<-- [end:par-files] */
}

/* --8<-- [start:print] */
/* one printf line of the user program; called on a background thread */
static void on_print(unsigned int timeSec, unsigned int timeTick, int thread,
                     const char* message, void* user)
{
    (void)user;
    printf("[%u.%u T%d] %s\n", timeSec, timeTick, thread, message);
}

void example_print(aamotion_controller h)
{
    aamotion_set_user_program_print_handler(h, on_print, NULL);
    /* ... the user program's printf lines arrive in on_print ... */
    aamotion_clear_user_program_print_handler(h);
}
/* --8<-- [end:print] */
