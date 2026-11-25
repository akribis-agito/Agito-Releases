/* Examples for the "Data recording" and "Slow logger" chapters. */
#include "aamotion.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

void example_record(aamotion_controller h)
{
    /* --8<-- [start:record] */
    /* 0.5 s every 0.25 ms; trigger: rising edge of AMotionStat above zero (RecTrigTyp 5), 10 % pre-trigger */
    if (!aamotion_data_rec_record(h, "APos,AVel[1],APosErr", 0.5, 0.25, 10, 5, "AMotionStat", 0, 0, false, 5000))
    {
        fprintf(stderr, "recording failed (see the error hook)\n");
        return;
    }
    int recLength, recGap, vectors, samples;
    aamotion_data_rec_info(h, &recLength, &recGap, &vectors, &samples);

    double* t = malloc(sizeof(double) * samples);
    double* v = malloc(sizeof(double) * samples);
    int copied;
    aamotion_data_rec_time(h, t, samples, &copied);
    for (int k = 0; k < vectors; ++k)
    {
        char name[64];
        int can, count;
        aamotion_data_rec_vector_info(h, k, name, sizeof name, &can, &count);
        if (count <= samples && aamotion_data_rec_vector_values(h, k, v, samples, &copied))
            printf("%s: %d samples, first %g at t=%g s\n", name, copied, v[0], t[0]);
    }
    free(t);
    free(v);
    /* --8<-- [end:record] */
}

void example_capacity(aamotion_controller h)
{
    /* --8<-- [start:capacity] */
    int maxVectors, maxArraySize;
    if (aamotion_data_rec_capacity(h, &maxVectors, &maxArraySize))
        printf("up to %d parameters, %d samples in total\n", maxVectors, maxArraySize);
    /* --8<-- [end:capacity] */
}

void example_slow(aamotion_controller h)
{
    /* --8<-- [start:slow] */
    const char* params[] = { "APos", "AVel[1]", "BPos" };
    aamotion_configure_slow_logger(h, 100, 0, params, 3);
    aamotion_set_logger_on(h, 1);
    /* ... */
    char* reply = malloc(1 << 20);
    if (aamotion_logger_upload(h, reply, 1 << 20))
        printf("%zu bytes of raw logger data\n", strlen(reply));
    printf("lost packets: %d\n", aamotion_get_logger_lost_packets(h));
    aamotion_set_logger_on(h, 0);
    free(reply);
    /* --8<-- [end:slow] */
}
