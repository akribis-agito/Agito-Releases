/* More C examples for the "Initialization and connection" chapter. */
#include "aamotion.h"
#include <stdio.h>
#include <stdlib.h>

void example_open_server(void)
{
    /* --8<-- [start:init] */
    /* start AACommServer once per process; NULL = the default location */
    if (!aamotion_open_aacomm_server(NULL))
        printf("starting the server failed\n");
    /* --8<-- [end:init] */
}

void example_check_connection(aamotion_controller h)
{
    /* --8<-- [start:check-connection] */
    /* ICMP ping, up to about a second; never disconnects or reconnects */
    bool reachable = aamotion_check_connection(h);

    /* one command round trip: is the controller answering, how fast, and if not, why */
    int failure = 0;
    double roundTripMs = 0;
    long long controllerTime = -1;
    bool restarted = false;
    char message[256];
    if (aamotion_probe_connection(h, 0, &failure, &roundTripMs, &controllerTime, &restarted, message, sizeof message))
    {
        if (failure == AAMOTION_CONNECTION_PROBE_FAILURE_NONE)
            printf("answered in %.1f ms%s\n", roundTripMs, restarted ? " (the controller restarted)" : "");
        else
            printf("no answer (%d): %s\n", failure, message);   /* a timeout has dropped the link */
    }
    printf("reachable: %d\n", reachable);
    /* --8<-- [end:check-connection] */
}

void example_timeout(aamotion_controller h)
{
    /* --8<-- [start:timeout] */
    int saved = 2000;
    aamotion_get_default_timeout(h, &saved);
    aamotion_set_default_timeout(h, 20000);      /* 10..60000 ms; only around a slow step */
    char reply[64];
    aamotion_send_command_string(h, "ASave", reply, sizeof reply);
    aamotion_set_default_timeout(h, saved);
    /* --8<-- [end:timeout] */
}

void example_scan(void)
{
    /* --8<-- [start:scan] */
    /* 172.1.1.1 .. 172.1.1.254, port 50000; the result is "ip,ip,..." */
    char small[256];
    int needed = aamotion_scan_controller(0, "172.1.1", 1, 254, 50000, small, sizeof small);
    if (needed < 0)
        return;                                   /* the subnet is not three dotted parts */
    char* list = small;
    if (needed > (int)sizeof small)               /* too long for the buffer: scan again into one that fits */
    {
        list = (char*)malloc((size_t)needed);
        aamotion_scan_controller(0, "172.1.1", 1, 254, 50000, list, needed);
    }
    printf("controllers: %s\n", list);
    if (list != small)
        free(list);
    /* --8<-- [end:scan] */
}
