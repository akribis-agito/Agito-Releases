// 扫描网段 / Scan the network
// 不连接控制器。
#include "aamotion.h"
#include <chrono>
#include <cstdio>
#include <iostream>
#include <string>
static const char* Step = "1/1 ScanNetwork";

static void Log(const char* step, const std::string& text)
{
    std::printf("[%s] %s\n", step, text.c_str());
}

static int Run()
{
    // --8<-- [start:operation]
    // 改成控制器所在网段的前三段；结果是逗号分隔的 IP 列表
    char list[4096] = "";
    int needed = aamotion_scan_controller(0, "172.1.1", 1, 254, 50000,
                                          list, sizeof list);
    if (needed < 0)
    {
        Log(Step, "the subnet must be three dotted parts");
        return 6;
    }
    Log(Step, std::string("controllers: ") + (list[0] ? list : "none"));
    // --8<-- [end:operation]
    return 0;
}

int main()
{
    int result = Run();
    std::printf("Exit code %d.\n", result);
    return result;
}
