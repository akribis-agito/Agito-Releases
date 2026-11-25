// 错误码转成文字 / Error code to text
// 不连接控制器。
#include "aamotion.h"
#include <chrono>
#include <cstdio>
#include <iostream>
#include <string>
static const char* Step = "1/1 DescribeError";

static void Log(const char* step, const std::string& text)
{
    std::printf("[%s] %s\n", step, text.c_str());
}

static int Run()
{
    // --8<-- [start:operation]
    // 与错误回调收到的文字相同（英文）
    char text[512] = "";
    aamotion_get_error_description_by_code(31, text, sizeof text);
    Log(Step, std::string("ERR 31: ") + text);
    // --8<-- [end:operation]
    return 0;
}

int main()
{
    int result = Run();
    std::printf("Exit code %d.\n", result);
    return result;
}
