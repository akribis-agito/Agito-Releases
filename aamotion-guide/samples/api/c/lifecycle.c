/* Examples for the "Initialization and connection" chapter. Each function is one example; the manual shows the
   part between its snippet markers. */
#include "aamotion.h"
#include <stdio.h>

aamotion_controller example_initialize(void)
{
    /* --8<-- [start:initialize] */
    if (aamotion_abi_version() != AAMOTION_ABI_VERSION)
    {
        fprintf(stderr, "aamotion.h and the aamotion library are from different SDK versions\n");
        return NULL;
    }
    aamotion_controller h = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);
    if (h == NULL)
    {
        /* unknown controller type, or AACommServerNative could not be started (is it beside the library?) */
        fprintf(stderr, "initialize failed\n");
        return NULL;
    }
    /* --8<-- [end:initialize] */
    return h;
}

void example_init(aamotion_controller h)
{
    /* --8<-- [start:init] */
    if (!aamotion_init(h))
        fprintf(stderr, "could not start AACommServer\n");
    /* --8<-- [end:init] */
}

bool example_connect(aamotion_controller h)
{
    /* --8<-- [start:connect] */
    /* third argument false: PC-side TCP immediate ACK ON */
    bool ok = aamotion_connect(h, "172.1.1.101", false, false);
    if (!ok)
        fprintf(stderr, "connect failed: check IP, cable, subnet and the AACommServer log\n");
    /* --8<-- [end:connect] */
    return ok;
}

bool example_connect_simulation(aamotion_controller h)
{
    /* --8<-- [start:connect-simulation] */
    bool ok = aamotion_connect_simulation(h, "D:\\sim\\AGM800\\firmware_sim.exe", "AGM800", NULL, NULL);
    /* --8<-- [end:connect-simulation] */
    return ok;
}

void example_disconnect(aamotion_controller h)
{
    /* --8<-- [start:disconnect] */
    aamotion_disconnect(h);   /* also frees h: do not use it again */
    h = NULL;
    /* --8<-- [end:disconnect] */
}

void example_is_connected(aamotion_controller h)
{
    /* --8<-- [start:is-connected] */
    if (!aamotion_is_connected(h))
        printf("not connected\n");
    /* --8<-- [end:is-connected] */
}

void example_try_reconnect(aamotion_controller h)
{
    /* --8<-- [start:try-reconnect] */
    /* NULL address: the last address that connected successfully */
    if (!aamotion_try_reconnect(h, NULL, false))
        fprintf(stderr, "controller still unreachable\n");
    /* --8<-- [end:try-reconnect] */
}

aamotion_controller example_reopen_simulation(aamotion_controller h)
{
    /* --8<-- [start:reopen-simulation] */
    aamotion_disconnect(h);                                   /* frees h */
    h = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);   /* a new handle */
    if (h && !aamotion_connect_simulation(h, "D:\\sim\\AGM800\\firmware_sim.exe", "AGM800", NULL, NULL))
        fprintf(stderr, "simulator did not reopen\n");
    /* --8<-- [end:reopen-simulation] */
    return h;
}

void example_shutdown(aamotion_controller h)
{
    /* --8<-- [start:shutdown] */
    aamotion_shutdown(h);     /* the server exits only if this is its last client */
    aamotion_disconnect(h);   /* frees h */
    /* --8<-- [end:shutdown] */
}

void example_release(aamotion_controller h)
{
    /* --8<-- [start:release] */
    aamotion_release(h);      /* frees h without touching the server; safe after aamotion_disconnect */
    /* --8<-- [end:release] */
}
