// 设定当前位置 / Set the current position
// 顺序：连接 → 本次操作 → 断开。轴不运动。
#include "aamotion.h"
#include <chrono>
#include <cmath>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <thread>
// 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
static const std::string Target = "172.1.1.101";
static const char* Step = "2/3 SetPosition";
static const int A = AAMOTION_AXIS_REF_A;

static void Log(const char* step, const std::string& text)
{
    std::printf("[%s] %s\n", step, text.c_str());
}

static void OnError(int code, const char* message, const char* exception,
                    void* user)
{
    Log("ERR", std::to_string(code) + ": " + (message ? message : "") + " | " +
        (exception ? exception : ""));
}

// 读一个数值；失败时返回 NAN，不能当作 0 使用
static double Read(aamotion_controller h, int axis, int keyword, int index = 0)
{
    double value = NAN;
    aamotion_get_value(h, axis, keyword, index, &value);
    return value;
}

static std::string Num(double v)
{
    char text[32];
    std::snprintf(text, sizeof text, "%.10g", v);
    return text;
}

static bool IsSimulator(const std::string& target)
{
    return target.size() > 4 && target.compare(target.size() - 4, 4, ".exe") == 0;
}

static int Operate(aamotion_controller h)
{
    // --8<-- [start:connect]
    // 1. 连接：Target 是 IP 时连接控制器；以 .exe 结尾时连接仿真器
    Log("1/3 Connect", Target);
    bool simulator = IsSimulator(Target);
    bool connected;
    if (!simulator)
        connected = aamotion_connect(h, Target.c_str(), true, false);
    else if (std::filesystem::exists(std::filesystem::u8path(Target)))
    {
        aamotion_init(h);   // 仿真器需要先启动 AACommServer
        connected = aamotion_connect_simulation(h, Target.c_str(),
            nullptr, nullptr, nullptr);
    }
    else
    {
        Log("1/3 Connect", "simulator not found; edit Target");
        return 2;
    }
    if (!connected)
    {
        Log("1/3 Connect", "failed; check Target and the cable");
        return 2;
    }
    // --8<-- [end:connect]

    // --8<-- [start:operation]
    // 2. 本次操作
    Log(Step, "before: Pos=" + Num(Read(h, A, AAMOTION_KEYWORD_POS)));
    // 值按计数，不按用户单位；轴不运动，只改坐标
    if (!aamotion_set_position(h, A, 0))
    {
        Log(Step, "set_position refused; see the ERR line");
        return 6;
    }
    Log(Step, "after: Pos=" + Num(Read(h, A, AAMOTION_KEYWORD_POS)));
    // --8<-- [end:operation]
    return 0;
}

static int Run()
{
    std::printf("Redefines the current position of axis A as 0.\n");
    std::printf("Type RUN to continue:\n");
    std::string answer;
    if (!std::getline(std::cin, answer) || answer != "RUN") return 1;
    aamotion_controller h = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);
    if (!h)
    {
        Log("1/3 Connect", "initialize failed; AACommServerNative missing?");
        return 2;
    }
    aamotion_set_error_occurred_handler(h, OnError, nullptr);
    int result = Operate(h);
    // --8<-- [start:cleanup]
    // 3. 断开：同时释放句柄；AACommServer 继续为其他程序服务
    aamotion_disconnect(h);
    Log("3/3 Disconnect", "done");
    // --8<-- [end:cleanup]
    return result;
}

int main()
{
    int result = Run();
    std::printf("Exit code %d.\n", result);
    return result;
}
