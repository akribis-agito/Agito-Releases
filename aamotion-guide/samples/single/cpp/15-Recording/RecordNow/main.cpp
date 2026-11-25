// 立即记录并上传 / Record now and upload
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
#include <vector>
// 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
static const std::string Target = "172.1.1.101";
static const char* Step = "2/3 RecordNow";
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
    int maxVectors = 0, maxSize = 0;
    aamotion_data_rec_capacity(h, &maxVectors, &maxSize);
    Log(Step, "capacity: " + std::to_string(maxVectors) +
        " parameters, " + std::to_string(maxSize) + " samples");
    // 触发源 nullptr：立即开始；记录 1 s，每 1 ms 一点
    if (!aamotion_data_rec_record(h, "APos,AVel[1]", 1.0, 1.0, 0, 0,
                                  nullptr, 0, 0, false, 5000))
    {
        Log(Step, "recording failed; see the ERR line");
        return 6;
    }
    int length = 0, gap = 0, vectors = 0, samples = 0;
    aamotion_data_rec_info(h, &length, &gap, &vectors, &samples);
    for (int i = 0; i < vectors; i++)
    {
        char name[64] = "";
        int canCode = 0, count = 0, copied = 0;
        aamotion_data_rec_vector_info(h, i, name, sizeof name,
                                      &canCode, &count);
        std::vector<double> x(count > 0 ? count : 1);
        aamotion_data_rec_vector_values(h, i, x.data(), (int)x.size(),
                                        &copied);
        std::string range = copied > 0 ?
            Num(x[0]) + " .. " + Num(x[copied - 1]) : "-";
        Log(Step, std::string(name) + ": " + std::to_string(copied) +
            " samples, " + range);
    }
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
