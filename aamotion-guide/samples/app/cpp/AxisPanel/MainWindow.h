// 主窗口：布局在 AxisPanel.rc，这里只做“按钮 → AxisController 调用”和“结果 → 控件”。
// 不含任何 aamotion_* 调用。
#pragma once
#include <windows.h>
#include "AxisController.h"
#include <deque>
#include <functional>
#include <mutex>
#include <string>

class MainWindow
{
public:
    // 从 AxisPanel.rc 的 IDD_MAIN 创建无模式对话框
    HWND Create(HINSTANCE instance);

private:
    static INT_PTR CALLBACK DialogProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp);
    std::vector<std::vector<std::string>> operations_;
    void SelectOperation();
    void OnInit();
    void OnCommand(int id);
    void OnClose();

    void OnUi(std::function<void()> update);   // 可从任何线程调用，在界面线程上执行
    void RunUiUpdates();
    void AddLog(const std::string& line);

    int SelectedAxis() const;
    std::wstring TextOf(int id) const;
    bool NumberOf(int id, double* value);
    void Send(const char* name, std::function<bool(aamotion_controller, int)> action);
    void SendMove(bool absolute);
    void Jog(int direction);

    HWND hwnd_ = nullptr;
    AxisController controller_;
    std::mutex uiLock_;
    std::deque<std::function<void()>> uiUpdates_;
};
