// 01-connect: connect to a controller or to the firmware simulator, read the position, disconnect.
//   sample01_connect 172.1.1.101
//   sample01_connect "D:\...\firmware_sim.exe"
#include "aamotion.h"
#include <cstdio>
#include <cstring>
#include <string>

// --8<-- [start:hook]
static void on_error(int code, const char* message, const char* exception, void* user)
{
    std::fprintf(stderr, "[ERR %d] %s | %s\n", code, message ? message : "", exception ? exception : "");
}
// --8<-- [end:hook]

static bool ends_with_exe(const std::string& s)
{
    return s.size() > 4 && (s.compare(s.size() - 4, 4, ".exe") == 0 || s.compare(s.size() - 4, 4, ".EXE") == 0);
}

int main(int argc, char** argv)
{
    std::string target = argc > 1 ? argv[1] : "172.1.1.101";

    // --8<-- [start:init]
    if (aamotion_abi_version() != AAMOTION_ABI_VERSION)
    {
        std::fprintf(stderr, "aamotion.h and the aamotion library come from different SDK versions\n");
        return 1;
    }
    aamotion_controller h = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);
    if (!h)
    {
        std::fprintf(stderr, "initialize failed: is AACommServerNative beside the library?\n");
        return 1;
    }
    aamotion_set_error_occurred_handler(h, on_error, nullptr);
    // --8<-- [end:init]

    // --8<-- [start:connect]
    bool ok = ends_with_exe(target)
        ? aamotion_connect_simulation(h, target.c_str(), "AGM800", nullptr, nullptr)
        : aamotion_connect(h, target.c_str(), false, false);
    std::printf("Connect %s: %s\n", target.c_str(), ok ? "true" : "false");
    // --8<-- [end:connect]

    if (ok)
    {
        // --8<-- [start:read]
        double pos = 0;
        if (aamotion_get_value(h, AAMOTION_AXIS_REF_A, AAMOTION_KEYWORD_POS, 0, &pos))
            std::printf("APos = %.3f\n", pos);
        // --8<-- [end:read]
    }

    // --8<-- [start:disconnect]
    aamotion_disconnect(h);
    // --8<-- [end:disconnect]
    return ok ? 0 : 2;
}
