// 路径上的输出动作 / An output on the path
// 顺序：连接 → 换相 → 使能（A、B 两轴）→ 本次操作 → 确认结果 → 清理与断开
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
static const char* Step = "4/6 CncOutputOnPath";
static const char* ResultStep = "5/6 Result";
static const int A = AAMOTION_AXIS_REF_A;
static const int B = AAMOTION_AXIS_REF_B;
static const int Axes[] = {A, B};
static bool motorOnRequested = false;

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

// 使能与控制器故障状态，用于说明失败原因
static std::string State(aamotion_controller h, int axis)
{
    double fault = Read(h, axis, AAMOTION_KEYWORD_CON_FLT);
    char text[256] = "";
    if (!std::isnan(fault))
        aamotion_get_con_flt_des((int)fault, text, sizeof text);
    return "MotorOn=" + Num(Read(h, axis, AAMOTION_KEYWORD_MOTOR_ON)) +
           ", ConFlt=" + Num(fault) + " " + text;
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
    Log("1/6 Connect", Target);
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
        Log("1/6 Connect", "simulator not found; edit Target");
        return 2;
    }
    if (!connected)
    {
        Log("1/6 Connect", "failed; check Target and the cable");
        return 2;
    }
    // --8<-- [end:connect]

    // --8<-- [start:prepare]
    // 2. 换相：仿真用虚拟电机。真实电机 A、B 都要先换相，等待有上限
    for (int axis : Axes)
    {
        if (simulator)
        {
            aamotion_set_value(h, axis, AAMOTION_KEYWORD_MOTOR_TYPE,
                               0, 5);
            continue;
        }
        if (aamotion_is_auto_phase(h, axis)) continue;
        Log("2/6 AutoPhase", axis == A ? "A requested" : "B requested");
        aamotion_auto_phase(h, axis);
        auto phaseTime = std::chrono::steady_clock::now();
        while (!aamotion_is_auto_phase(h, axis))
        {
            if (Ms(phaseTime) >= 10000)
            {
                Log("2/6 AutoPhase", "timed out; motors stay off");
                return 3;
            }
            SleepMs(20);
        }
    }
    Log("2/6 AutoPhase",
        simulator ? "virtual motors" : "A and B commutated");

    // 3. 使能：清理阶段也会处理结果不确定的使能
    //    控制器回复 ERR 时错误回调会打印错误码；超时等失败没有回调，
    //    所以失败时再读一次使能与故障状态
    motorOnRequested = true;
    for (int axis : Axes)
    {
        std::string name = axis == A ? "A: " : "B: ";
        if (!aamotion_motor_on(h, axis))
        {
            Log("3/6 MotorOn", name + "failed; " + State(h, axis));
            return 4;
        }
        if (Read(h, axis, AAMOTION_KEYWORD_MOTOR_ON) != 1)
        {
            Log("3/6 MotorOn", name + "not on; " + State(h, axis));
            return 4;
        }
    }
    Log("3/6 MotorOn", "A and B on");
    // --8<-- [end:prepare]

    // --8<-- [start:operation]
    // 4. 本次操作
    const int x0 = (int)Read(h, A, AAMOTION_KEYWORD_POS);
    const int y0 = (int)Read(h, B, AAMOTION_KEYWORD_POS);
    if (!aamotion_clear_buffer(h) ||
        !aamotion_set_start_positions(h, A | B, x0, y0, 0) ||
        !aamotion_linear_absolute_xy(h, x0 + 10000, y0, 8000, 0))
    {
        Log(Step, "CNC segment refused; path not started");
        return 6;
    }
    aamotion_group_d_out_set_bit(h, A, 1);
    if (!aamotion_delay(h, 200))
    {
        Log(Step, "CNC segment refused; path not started");
        return 6;
    }
    aamotion_group_d_out_clear_bit(h, A, 1);
    if (!aamotion_linear_absolute_xy(h, x0, y0, 8000, 0))
    {
        Log(Step, "CNC segment refused; path not started");
        return 6;
    }
    // 组 A 的两根成员轴都用 MotionMode 11。
    char modeReply[128];
    if (!aamotion_send_command_string(h, "AMotionMode=11",
                                      modeReply, sizeof modeReply) ||
        !aamotion_send_command_string(h, "BMotionMode=11",
                                      modeReply, sizeof modeReply) ||
        !aamotion_cnc_begin(h))
    {
        Log(Step, "cnc_begin refused; see the ERR line");
        return 6;
    }
    Log(Step, "path with an output action started");
    // --8<-- [end:operation]

    // --8<-- [start:observe]
    // 5. 确认结果：命令被接受不等于动作完成
    auto pathTime = std::chrono::steady_clock::now();
    while (!aamotion_is_cnc_completed(h, A))
    {
        if (Ms(pathTime) >= 30000)
        {
            aamotion_group_stop(h);
            Log(ResultStep, "path timeout; group_stop requested");
            return 6;
        }
        SleepMs(50);
    }
    double posA = Read(h, A, AAMOTION_KEYWORD_POS);
    double posB = Read(h, B, AAMOTION_KEYWORD_POS);
    Log(ResultStep, "path done: A=" + Num(posA) + ", B=" + Num(posB));
    // --8<-- [end:observe]
    return 0;
}

static int Run()
{
    std::printf("Axes A and B will move. Check travel and units.\n");
    std::printf("Type RUN to continue:\n");
    std::string answer;
    if (!std::getline(std::cin, answer) || answer != "RUN") return 1;
    aamotion_controller h = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);
    if (!h)
    {
        Log("1/6 Connect", "initialize failed; AACommServerNative missing?");
        return 2;
    }
    aamotion_set_error_occurred_handler(h, OnError, nullptr);
    int result = Operate(h);
    // --8<-- [start:cleanup]
    // 6. 清理：先确认停止再断使能；断开通信不能证明设备已停止
    if (motorOnRequested)
    {
        for (int axis : Axes)
            aamotion_stop(h, axis);
        auto moving = [h] {
            return Read(h, A, AAMOTION_KEYWORD_MOTION_STAT) != 0 ||
                   Read(h, B, AAMOTION_KEYWORD_MOTION_STAT) != 0;
        };
        auto stopTime = std::chrono::steady_clock::now();
        while (moving() && Ms(stopTime) < 5000)
            SleepMs(20);
        if (!moving())
        {
            for (int axis : Axes)
                aamotion_motor_off(h, axis);
            Log("6/6 Cleanup", "stopped, motors off");
        }
        else
            Log("6/6 Cleanup", "stop unconfirmed; check both axes");
    }
    aamotion_disconnect(h);   // 同时释放句柄
    // --8<-- [end:cleanup]
    return result;
}

int main()
{
    int result = Run();
    std::printf("Exit code %d.\n", result);
    return result;
}
