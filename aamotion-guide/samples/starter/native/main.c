#ifndef _WIN32
#define _POSIX_C_SOURCE 200809L
#endif
#include "scenario.h"
#include <math.h>
#include <ctype.h>
#include <stdio.h>
#include <string.h>

static int valid(void)
{
    return (WB_GOAL == 0 || WB_GOAL == 1) && (WB_AXIS == 'A' || WB_AXIS == 'B') &&
        isfinite((double)WB_TARGET) && fabs(WB_TARGET) <= 100 && isfinite((double)WB_SPEED) && WB_SPEED > 0 && WB_SPEED <= 100 &&
        isfinite((double)WB_ACCEL) && WB_ACCEL > 0 && WB_ACCEL <= 500 && isfinite((double)WB_DECEL) && WB_DECEL > 0 && WB_DECEL <= 500 &&
        WB_TIMEOUT_MS >= 100 && WB_TIMEOUT_MS <= 60000;
}
#ifndef WB_VALIDATE_ONLY
#include "aamotion.h"
#ifdef _WIN32
#include <windows.h>
static double now_ms(void) { return (double)GetTickCount64(); }
static void pause_ms(void) { Sleep(50); }
#else
#include <time.h>
static double now_ms(void) { struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t); return t.tv_sec * 1000.0 + t.tv_nsec / 1000000.0; }
static void pause_ms(void) { struct timespec t = {0, 50000000}; nanosleep(&t, NULL); }
#endif
static void on_error(int code, const char* sent, const char* text, void* user)
{
    fprintf(stderr, "[ERR %d] %s | %s\n", code, sent ? sent : "", text ? text : "");
}
static int execute(const char* address)
{
    const int axis = WB_AXIS == 'A' ? AAMOTION_AXIS_REF_A : AAMOTION_AXIS_REF_B;
    size_t address_length = strlen(address);
    int simulator = address_length > 4 && address[address_length - 4] == '.' &&
        tolower((unsigned char)address[address_length - 3]) == 'e' &&
        tolower((unsigned char)address[address_length - 2]) == 'x' &&
        tolower((unsigned char)address[address_length - 1]) == 'e';
    if (simulator)
    {
        FILE* file = NULL;
#ifdef _WIN32
        int absolute = (address_length >= 3 && isalpha((unsigned char)address[0]) && address[1] == ':' && (address[2] == '/' || address[2] == '\\')) ||
            (address_length >= 2 && address[0] == '\\' && address[1] == '\\');
#else
        int absolute = address[0] == '/';
#endif
        if (!absolute || !(file = fopen(address, "rb"))) { fprintf(stderr, "Simulator requires an existing absolute path\n"); return 2; }
        fclose(file);
    }
    if (aamotion_abi_version() != AAMOTION_ABI_VERSION) { fprintf(stderr, "SDK ABI mismatch\n"); return 1; }
    aamotion_controller h = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);
    if (!h) { fprintf(stderr, "Initialize failed; check AACommServerNative installation\n"); return 1; }
    int rc = 1, touched = 0, seen_moving = 0, simulation = 0;
    double bits = 0, deadline = 0;
    aamotion_set_error_occurred_handler(h, on_error, NULL);
    simulation = simulator;
    if (!(simulation ? aamotion_connect_simulation(h, address, "AGM800", NULL, NULL) : aamotion_connect(h, address, false, false)))
    { fprintf(stderr, "Connection failed\n"); goto cleanup; }
    if (WB_GOAL == 0)
    {
        if (!aamotion_get_value(h, axis, AAMOTION_KEYWORD_DIN_PORT, 0, &bits) || !isfinite(bits)) goto cleanup;
        printf("%c DInPort = %.0f; input 1 = %d\n", WB_AXIS, bits, fmod(fabs(bits), 2) != 0);
        rc = 0; goto cleanup;
    }
    if (!aamotion_is_auto_phase(h, axis)) { fprintf(stderr, "Commutation not confirmed; inspect errors and verify machine preconditions with your setup tool first\n"); goto cleanup; }
    touched = 1;
    if (!aamotion_motor_on(h, axis) || !aamotion_move_abs(h, axis, WB_TARGET, WB_SPEED, WB_ACCEL, WB_DECEL)) goto cleanup;
    deadline = now_ms() + WB_TIMEOUT_MS;
    while (now_ms() < deadline)
    {
        double status = 0, pos = 0;
        if (!aamotion_get_value(h, axis, AAMOTION_KEYWORD_IN_TARGET_STAT, 0, &status) ||
            !aamotion_get_value(h, axis, AAMOTION_KEYWORD_POS, 0, &pos) || !isfinite(status) || !isfinite(pos)) goto cleanup;
        if ((int)status == AAMOTION_IN_TARGET_STAT_MOVING) seen_moving = 1;
        else if ((int)status == AAMOTION_IN_TARGET_STAT_TARGET_REACHED && (seen_moving || fabs(pos - WB_TARGET) <= fmax(1e-9, fabs(WB_TARGET) * 1e-12)))
        { printf("In target: %g\n", pos); rc = 0; goto cleanup; }
        pause_ms();
    }
    fprintf(stderr, "Wait timeout; not proof of a stop\n");
cleanup:
    if (touched)
    {
        if (!aamotion_stop(h, axis)) { fprintf(stderr, "Stop not confirmed\n"); rc = 1; }
        if (!aamotion_motor_off(h, axis)) { fprintf(stderr, "MotorOff not confirmed\n"); rc = 1; }
    }
    if (!aamotion_disconnect(h)) { fprintf(stderr, "Disconnect failed\n"); rc = 1; }
    return rc;
}
#endif
int main(int argc, char** argv)
{
    if (!valid()) { fprintf(stderr, "Invalid scenario.h\n"); return 2; }
    if (argc == 2 && strcmp(argv[1], "--validate") == 0)
    { puts("Valid scenario; no SDK initialization or controller connection"); return 0; }
#ifndef WB_VALIDATE_ONLY
    if (argc == 3 && argv[1][0] && strcmp(argv[2], "--execute") == 0) return execute(argv[1]);
#endif
    fprintf(stderr, "Usage: starter --validate | starter <controller-IP|absolute-firmware_sim.exe> --execute\n");
    return 2;
}
