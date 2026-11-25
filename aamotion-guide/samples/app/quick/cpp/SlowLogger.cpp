#include "demo.h"
#include <fstream>

int main(int argc, char** argv)
{
    return run_demo(argc, argv, {}, [](aamotion_controller h) {
        // --8<-- [start:operation]
        const char* signals[] = {"APos"};
        check(aamotion_configure_slow_logger(h, 10, 0, signals, 1), "Configure SlowLogger");
        check(aamotion_set_logger_on(h, 1), "Start SlowLogger");
        std::this_thread::sleep_for(std::chrono::seconds(2));
        char text[8192] = {};
        const bool uploaded = aamotion_logger_upload(h, text, sizeof text);
        check(aamotion_set_logger_on(h, 0), "Stop SlowLogger");
        check(uploaded && text[0], "No logger data");
        std::ofstream output("slow-logger.txt");
        output << text;
        output.close();
        check(static_cast<bool>(output), "Write slow-logger.txt");
        std::puts("Saved slow-logger.txt");
        // --8<-- [end:operation]
    });
}
