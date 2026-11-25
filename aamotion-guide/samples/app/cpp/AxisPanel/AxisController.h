// 与控制器通信的全部代码，不含任何界面代码。
// 所有 aamotion_* 调用都放进一个队列，由一个后台工作线程依次执行，所以通信慢或超时时界面不会卡住，
// 也不会有两个线程同时调用 SDK。结果通过回调报告；回调在工作线程（或 SDK 的线程）上调用，
// 由界面层负责切回界面线程。
#pragma once
#include "aamotion.h"
#include <atomic>
#include <condition_variable>
#include <deque>
#include <functional>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

class AxisController
{
public:
    // 状态面板中各行的名称，顺序与 onStatus 给出的值一致
    static const int StatusCount = 8;
    static const char* const StatusNames[StatusCount];

    std::function<void(const std::string&)> onLog;                   // 一行日志（UTF-8）
    std::function<void(bool)> onConnection;                          // 连接状态变化
    std::function<void(const std::vector<std::string>&)> onStatus;   // 一轮状态值（UTF-8）

    AxisController();
    ~AxisController();

    bool IsConnected() const { return connected_; }

    // 连接控制器（IP）或仿真器（firmware_sim.exe 的完整路径）
    void Connect(const std::string& target);
    void Disconnect();

    // 对一根轴发出一条命令。命令被接受不等于动作完成：完成与否看状态
    void Send(const std::string& name, int axis, std::function<bool(aamotion_controller, int)> action);

    // 读一次状态；上一轮还没读完就跳过，避免排队堆积
    void Poll(int axis);

    // 请求停止并断使能，再断开，最多等待 5 秒。断开通信不能证明设备已停止
    void Shutdown(int axis);

private:
    void Queue(std::function<void()> job);
    void Work();
    void Log(const std::string& text);
    double Read(int axis, int keyword);
    static void OnError(int code, const char* message, const char* exception, void* user);

    aamotion_controller h_ = nullptr;
    std::atomic<bool> connected_{false};
    std::atomic<bool> pollPending_{false};
    std::mutex lock_;
    std::condition_variable ready_;
    std::deque<std::function<void()>> jobs_;
    bool stopping_ = false;
    std::thread worker_;
};
