/* Examples for the "Error handling" chapter. */
#include "aamotion.h"
#include <stdio.h>
#include <string.h>

/* --8<-- [start:hook] */
static char g_last_error[512];

/* called on the SDK's thread; the strings are valid only during the call */
static void on_error(int code, const char* message, const char* exception,
                     void* user)
{
    (void)user;   /* the pointer passed to aamotion_set_error_occurred_handler */
    snprintf(g_last_error, sizeof g_last_error, "ERR %d | %s | %s",
             code, message ? message : "", exception ? exception : "");
    fprintf(stderr, "%s\n", g_last_error);
}

void example_hook(aamotion_controller h)
{
    aamotion_set_error_occurred_handler(h, on_error, NULL);
    /* ... */
    aamotion_clear_error_occurred_handler(h);
}
/* --8<-- [end:hook] */

void example_check(aamotion_controller h)
{
    /* --8<-- [start:check] */
    char reply[256];
    if (!aamotion_send_command_string(h, "AMotorOn=1", reply, sizeof reply))
    {
        /* the hook has already been called with the code and the text */
        fprintf(stderr, "motor on refused: %s\n", g_last_error);
    }
    /* --8<-- [end:check] */
}

void example_describe(void)
{
    /* --8<-- [start:describe] */
    char text[512];
    if (aamotion_get_error_description_by_code(31, text, sizeof text))
        printf("ERR 31: %s\n", text);   /* English */
    /* --8<-- [end:describe] */
}
