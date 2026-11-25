// 按固件地址表读 GenData / Read GenData by the firmware table
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
#include <cstdlib>
// 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
static const std::string Target = "172.1.1.101";
static const char* Step = "2/3 FirmwareGenData";
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
    // 下标必须来自当前固件的地址表；-1 表示还没有填写
    int index = -1;
    if (index < 0)
    {
        Log(Step, "set index from your firmware address table");
        return 1;
    }
    std::string command = "AGenData[" + std::to_string(index) + "]";
    char reply[64] = "";
    if (!aamotion_send_command_string(h, command.c_str(), reply,
                                      sizeof reply))
    {
        Log(Step, "read failed; see the ERR line");
        return 6;
    }
    char* end = nullptr;
    long value = std::strtol(reply, &end, 10);
    if (end == reply)
    {
        Log(Step, std::string("not a number: ") + reply);
        return 6;
    }
    Log(Step, command + " = " + std::to_string(value));
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
