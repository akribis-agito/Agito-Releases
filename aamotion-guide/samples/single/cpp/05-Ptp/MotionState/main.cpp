// 读位置与到位状态 / Position and in-target state
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
static const char* Step = "2/3 MotionState";
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
    Log(Step, "Pos=" + Num(Read(h, A, AAMOTION_KEYWORD_POS)) +
        ", PosRef=" + Num(Read(h, A, AAMOTION_KEYWORD_POS_REF)));
    // MotionStat 为 0：运动已结束
    double stat = Read(h, A, AAMOTION_KEYWORD_MOTION_STAT);
    char text[256] = "";
    if (!std::isnan(stat))
        aamotion_get_motion_stat_des((int)stat, text, sizeof text);
    Log(Step, "MotionStat=" + Num(stat) + ": " + text);
    // 4：整定到位；运动结束不等于整定到位
    double inTarget = Read(h, A, AAMOTION_KEYWORD_IN_TARGET_STAT);
    Log(Step, "InTargetStat=" + Num(inTarget));
    // --8<-- [end:operation]
    return 0;
}

static int Run()
{
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
