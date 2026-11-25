// 螺旋找光 / Spiral light finding
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
static const char* Step = "2/3 SpiralScan";
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

static long long Ms(std::chrono::steady_clock::time_point since)
{
    using namespace std::chrono;
    return duration_cast<milliseconds>(steady_clock::now() - since).count();
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
    aamotion_optical_coupling oc = aamotion_optical_coupling_create(h);
    if (!oc)
    {
        Log(Step, "optical_coupling_create failed");
        return 6;
    }
    // 模拟输入 1；找光轴 0、1；螺距 50、最大半径 2000、角度系数 10；
    // 扫描速度 2000、返回速度 5000、光功率阈值 1000
    aamotion_optical_coupling_apply_spiral_config(oc, 1, 0, 1, 50, 2000,
                                                  10, 2000, 5000, 1000);
    aamotion_write_gen_data(h, 602, 0);   // 清除上次结果
    aamotion_optical_coupling_start_spiral_scan(oc);
    auto scanTime = std::chrono::steady_clock::now();
    int result = 0;   // 0：还在扫描；正数成功，负数失败
    for (;;)
    {
        result = aamotion_optical_coupling_get_coupling_result(oc);
        if (result != 0) break;
        if (Ms(scanTime) >= 60000)
        {
            aamotion_optical_coupling_emergency_stop(oc);
            aamotion_optical_coupling_release(oc);
            Log(Step, "scan timeout; emergency stop requested");
            return 6;
        }
        SleepMs(100);
    }
    bool success = aamotion_optical_coupling_is_success(oc);
    int power = aamotion_optical_coupling_get_max_power(oc);
    int x = aamotion_optical_coupling_get_max_power_x_pos(oc);
    int y = aamotion_optical_coupling_get_max_power_y_pos(oc);
    int error = aamotion_optical_coupling_get_error_code(oc);
    aamotion_optical_coupling_release(oc);
    if (!success)
    {
        Log(Step, "failed: result " + std::to_string(result) +
            ", error " + std::to_string(error));
        return 6;
    }
    Log(Step, "max power " + std::to_string(power) + " at (" +
        std::to_string(x) + ", " + std::to_string(y) + ")");
    // --8<-- [end:operation]
    return 0;
}

static int Run()
{
    std::printf("Starts a spiral scan; the coupling axes move.\n");
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
