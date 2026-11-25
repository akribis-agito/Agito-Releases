// 读取扫描曲线 / Read the scan curves
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
static const char* Step = "2/3 ScanCurves";
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
    aamotion_optical_coupling oc = aamotion_optical_coupling_create(h);
    if (!oc)
    {
        Log(Step, "optical_coupling_create failed");
        return 6;
    }
    int n = aamotion_optical_coupling_get_last_sample_index(oc);
    if (n <= 0)
    {
        aamotion_optical_coupling_release(oc);
        Log(Step, "no scan data; run SpiralScan first");
        return 1;
    }
    // 一次读出 GenData，存到本地副本
    aamotion_optical_coupling_sync_all_gen_data(oc, 1, 16000);
    // 螺旋数据区：X 1000–3499，Y 3500–5999，通道 1 光功率 6000–8499
    std::vector<int> x(n + 1), y(n + 1), p(n + 1);
    int size = n + 1;
    aamotion_optical_coupling_get_spiral_x_pos_array(oc, 1000, 1000 + n,
                                                     x.data(), size);
    aamotion_optical_coupling_get_spiral_y_pos_array(oc, 3500, 3500 + n,
                                                     y.data(), size);
    int got = aamotion_optical_coupling_get_channel1_power_array(
        oc, 6000, 6000 + n, p.data(), size);
    aamotion_optical_coupling_release(oc);
    int best = 0;
    for (int i = 1; i < got; i++)
        if (p[i] > p[best]) best = i;
    Log(Step, std::to_string(got) + " samples; peak " +
        std::to_string(p[best]) + " at (" + std::to_string(x[best]) +
        ", " + std::to_string(y[best]) + ")");
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
