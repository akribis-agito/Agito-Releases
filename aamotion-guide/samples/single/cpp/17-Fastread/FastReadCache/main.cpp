// 快速读缓存 / Fast-read cache
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
static const char* Step = "2/3 FastReadCache";
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

static std::string Num(double v)
{
    char text[32];
    std::snprintf(text, sizeof text, "%.10g", v);
    return text;
}

static void SleepMs(int ms)
{
    std::this_thread::sleep_for(std::chrono::milliseconds(ms));
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
    // 登记 A 轴的 Pos 与 MotionStat，后台每 5 ms 刷新
    const int keywords[] = {AAMOTION_KEYWORD_POS,
                            AAMOTION_KEYWORD_MOTION_STAT};
    if (!aamotion_register_cache_keywords(h, A, keywords, 2) ||
        !aamotion_enable_fast_read_cache(h, 5))
    {
        Log(Step, "enabling the cache failed; see the ERR line");
        return 6;
    }
    for (int i = 0; i < 5; i++)
    {
        SleepMs(100);
        double age = -1;   // -1：还没有缓存值
        aamotion_get_cache_age_ms(h, A, AAMOTION_KEYWORD_POS, &age);
        Log(Step, "Pos value age " + Num(age) + " ms");
    }
    aamotion_disable_fast_read_cache(h);
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
