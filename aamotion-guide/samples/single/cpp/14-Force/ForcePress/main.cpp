// 压装流程 / A press sequence
// 顺序：连接 → 换相 → 使能 → 本次操作 → 确认结果 → 清理与断开
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
static const char* Step = "4/6 ForcePress";
static const char* ResultStep = "5/6 Result";
static const int A = AAMOTION_AXIS_REF_A;
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
    // 2. 换相：仿真用虚拟电机。真实电机才需要换相，等待有上限
    if (simulator)
        aamotion_set_value(h, A, AAMOTION_KEYWORD_MOTOR_TYPE, 0, 5);
    else if (!aamotion_is_auto_phase(h, A))
    {
        Log("2/6 AutoPhase", "requested");
        aamotion_auto_phase(h, A);
        auto phaseTime = std::chrono::steady_clock::now();
        while (!aamotion_is_auto_phase(h, A))
        {
            if (Ms(phaseTime) >= 10000)
            {
                Log("2/6 AutoPhase", "timed out; motor stays off");
                return 3;
            }
            SleepMs(20);
        }
    }
    Log("2/6 AutoPhase", simulator ? "virtual motor" : "commutated");

    // 3. 使能：清理阶段也会处理结果不确定的使能
    //    控制器回复 ERR 时错误回调会打印错误码；超时等失败没有回调，
    //    所以失败时再读一次使能与故障状态
    motorOnRequested = true;
    if (!aamotion_motor_on(h, A))
    {
        Log("3/6 MotorOn", "returned false; " + State(h, A));
        return 4;
    }
    if (Read(h, A, AAMOTION_KEYWORD_MOTOR_ON) != 1)
    {
        Log("3/6 MotorOn", "accepted but not on; " + State(h, A));
        return 4;
    }
    Log("3/6 MotorOn", "on");
    // --8<-- [end:prepare]

    // --8<-- [start:operation]
    // 4. 本次操作
    double start = Read(h, A, AAMOTION_KEYWORD_POS);
    // 1) 接近：向 start + 3000 运动（relTrgt 0：用绝对目标）
    aamotion_force_curr_set_motion(h, A, start + 3000, 0, 4000,
                                   50000, 50000);
    // 2) 到 start + 2400 后换成速度 500 慢速接近
    aamotion_force_curr_set_slow_approach(h, A, 500, start + 2400,
                                          0, 1);
    // 3) 越过 start + 2700 后切换到力控；后两个阈值 0：不用
    aamotion_force_curr_set_curr_mode_sw_trig_src(h, A, start + 2700,
                                                  0, 0, 0);
    // 4) 力指令：一段，斜率 100，目标 200，保持时间 1000
    double slope[] = {100}, value[] = {200};
    int hold[] = {1000};
    aamotion_force_curr_set_force_cmd_source(h, A, 1, slope, hold,
                                             value, 1);
    // 5) 完成后以速度 4000 回退到起点
    aamotion_force_curr_set_retract_motion(h, A, start, 4000, 0);
    if (!aamotion_begin(h, A))
    {
        Log(Step, "begin failed; see any ERR line above");
        return 6;
    }
    Log(Step, "press sequence started");
    // --8<-- [end:operation]

    // --8<-- [start:observe]
    // 5. 确认结果：命令被接受不等于动作完成
    // 回退结束后连续 500 ms 静止，并回到位置控制
    auto pressTime = std::chrono::steady_clock::now();
    auto still = pressTime;
    while (Ms(still) < 500)
    {
        if (Read(h, A, AAMOTION_KEYWORD_MOTION_STAT) != 0)
            still = std::chrono::steady_clock::now();
        if (Ms(pressTime) >= 20000)
        {
            aamotion_go_to_pos_mode(h, A);
            Log(ResultStep, "sequence timeout; back to position mode");
            return 6;
        }
        SleepMs(20);
    }
    double pos = Read(h, A, AAMOTION_KEYWORD_POS);
    double mode = Read(h, A, AAMOTION_KEYWORD_OPERATION_MODE);
    Log(ResultStep, "done: Pos=" + Num(pos) + ", mode=" + Num(mode));
    // --8<-- [end:observe]
    return 0;
}

static int Run()
{
    std::printf("Axis A will move. Check travel and units.\n");
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
        aamotion_stop(h, A);
        auto stopTime = std::chrono::steady_clock::now();
        while (Read(h, A, AAMOTION_KEYWORD_MOTION_STAT) != 0 &&
               Ms(stopTime) < 5000)
            SleepMs(20);
        if (Read(h, A, AAMOTION_KEYWORD_MOTION_STAT) == 0)
        {
            aamotion_motor_off(h, A);
            Log("6/6 Cleanup", "stopped, motor off");
        }
        else
            Log("6/6 Cleanup", "stop unconfirmed; check the axis");
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
