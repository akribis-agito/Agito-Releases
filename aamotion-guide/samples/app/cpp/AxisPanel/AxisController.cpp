#include "AxisController.h"
#include <chrono>
#include <cmath>
#include <cstdio>
#include <ctime>
#include <filesystem>
#include <fstream>
#include <future>
#include <memory>

const char* const AxisController::StatusNames[StatusCount] = {
    "Pos", "PosRef", "MotorOn", "MotionStat", "InTargetStat", "ConFlt", "换相", "HomingStat"};

// 错误回调是 C 函数指针，没有用户参数：用它找到唯一的控制器对象
static AxisController* current = nullptr;

AxisController::AxisController()
{
    current = this;
    worker_ = std::thread([this] { Work(); });
}

AxisController::~AxisController()
{
    {
        std::lock_guard<std::mutex> guard(lock_);
        stopping_ = true;
    }
    ready_.notify_one();
    worker_.join();
    current = nullptr;
}

void AxisController::Queue(std::function<void()> job)
{
    {
        std::lock_guard<std::mutex> guard(lock_);
        jobs_.push_back(std::move(job));
    }
    ready_.notify_one();
}

void AxisController::Work()
{
    for (;;)
    {
        std::function<void()> job;
        {
            std::unique_lock<std::mutex> lock(lock_);
            ready_.wait(lock, [this] { return stopping_ || !jobs_.empty(); });
            if (jobs_.empty()) return;
            job = std::move(jobs_.front());
            jobs_.pop_front();
        }
        job();
    }
}

void AxisController::Log(const std::string& text)
{
    char stamp[16];
    std::time_t t = std::time(nullptr);
    std::strftime(stamp, sizeof stamp, "%H:%M:%S", std::localtime(&t));
    if (onLog) onLog(std::string("[") + stamp + "] " + text);
}

// 错误回调可能在 SDK 的任何线程上调用；onLog 由界面层切回界面线程
void AxisController::OnError(int code, const char* message, const char* exception, void*)
{
    if (current)
        current->Log("ERR " + std::to_string(code) + ": " + (message ? message : "") +
                     " | " + (exception ? exception : ""));
}

double AxisController::Read(int axis, int keyword)
{
    double value = NAN;
    aamotion_get_value(h_, axis, keyword, 0, &value);
    return value;
}

static std::string Num(double v)
{
    char text[32];
    std::snprintf(text, sizeof text, "%.10g", v);
    return text;
}

void AxisController::Connect(const std::string& target)
{
    if (connected_) return;
    Queue([this, target] {
        if (connected_) return;
        h_ = aamotion_initialize(AAMOTION_CONTROLLER_TYPE_AGM800);
        if (!h_)
        {
            Log("initialize failed; AACommServerNative missing?");
            return;
        }
        aamotion_set_error_occurred_handler(h_, OnError, nullptr);
        bool simulator = target.size() > 4 && target.compare(target.size() - 4, 4, ".exe") == 0;
        bool ok;
        if (!simulator)
            ok = aamotion_connect(h_, target.c_str(), true, false);
        else if (std::filesystem::exists(std::filesystem::u8path(target)))
        {
            aamotion_init(h_);   // 仿真器需要先启动 AACommServer
            ok = aamotion_connect_simulation(h_, target.c_str(), nullptr, nullptr, nullptr);
        }
        else
        {
            Log("simulator not found: " + target);
            aamotion_release(h_);
            return;
        }
        if (!ok) aamotion_disconnect(h_);   // 同时释放句柄
        connected_ = ok;
        Log(ok ? "connected to " + target : "connect failed; check Target and the cable");
        if (onConnection) onConnection(ok);
    });
}

void AxisController::Disconnect()
{
    if (!connected_) return;
    Queue([this] {
        if (!connected_) return;
        connected_ = false;
        aamotion_disconnect(h_);
        Log("disconnected");
        if (onConnection) onConnection(false);
    });
}

void AxisController::Send(const std::string& name, int axis,
                          std::function<bool(aamotion_controller, int)> action)
{
    if (!connected_)
    {
        Log("not connected");
        return;
    }
    Queue([this, name, axis, action] {
        if (!connected_)
        {
            Log("not connected");
            return;
        }
        bool ok = action(h_, axis);
        Log(name + (ok ? " accepted" : " failed; see any ERR line above"));
    });
}

void AxisController::Poll(int axis)
{
    if (!connected_ || pollPending_.exchange(true)) return;
    Queue([this, axis] {
        if (!connected_)
        {
            pollPending_ = false;
            return;
        }
        char text[256];
        double stat = Read(axis, AAMOTION_KEYWORD_MOTION_STAT);
        double fault = Read(axis, AAMOTION_KEYWORD_CON_FLT);
        double homing = Read(axis, AAMOTION_KEYWORD_HOMING_STAT);
        double inTarget = Read(axis, AAMOTION_KEYWORD_IN_TARGET_STAT);
        std::vector<std::string> values;
        values.push_back(Num(Read(axis, AAMOTION_KEYWORD_POS)));
        values.push_back(Num(Read(axis, AAMOTION_KEYWORD_POS_REF)));
        values.push_back(Num(Read(axis, AAMOTION_KEYWORD_MOTOR_ON)));
        text[0] = '\0';
        if (!std::isnan(stat)) aamotion_get_motion_stat_des((int)stat, text, sizeof text);
        values.push_back(Num(stat) + "  " + text);
        values.push_back(Num(inTarget) + (inTarget == 4 ? "  到位" : ""));
        text[0] = '\0';
        if (!std::isnan(fault)) aamotion_get_con_flt_des((int)fault, text, sizeof text);
        values.push_back(Num(fault) + "  " + text);
        values.push_back(aamotion_is_auto_phase(h_, axis) ? "已换相" : "未换相");
        text[0] = '\0';
        if (!std::isnan(homing)) aamotion_get_homing_stat_des((int)homing, text, sizeof text);
        values.push_back(Num(homing) + "  " + text);
        pollPending_ = false;
        if (onStatus) onStatus(values);
    });
}

void AxisController::Shutdown(int axis)
{
    if (!connected_) return;
    auto done = std::make_shared<std::promise<void>>();
    std::future<void> finished = done->get_future();
    Queue([this, axis, done] {
        if (!connected_)
        {
            done->set_value();
            return;
        }
        aamotion_stop(h_, axis);
        aamotion_motor_off(h_, axis);
        aamotion_disconnect(h_);
        connected_ = false;
        done->set_value();
    });
    finished.wait_for(std::chrono::seconds(5));
}
