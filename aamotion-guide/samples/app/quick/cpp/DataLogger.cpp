#include "demo.h"

int main(int argc, char** argv)
{
    return run_demo(argc, argv, {}, [](aamotion_controller h) {
        // --8<-- [start:operation]
        check(aamotion_data_rec_record(h, "APos", 1, 1, 0, 0, nullptr, 0, 0, false, 5000),
              "Record and upload APos");
        int length = 0, gap = 0, vectors = 0, samples = 0;
        check(aamotion_data_rec_info(h, &length, &gap, &vectors, &samples), "Read recording info");
        check(vectors == 1 && samples > 0, "No APos samples");
        std::printf("APos: %d samples\n", samples);
        // --8<-- [end:operation]
    });
}
