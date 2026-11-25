// 轴控制面板 / Axis Panel（Win32）：连接、实时状态、换相与使能、点位、Jog、停止、回零与日志。
//   AxisPanel.rc / resource.h      界面布局（对话框模板）
//   MainWindow.h / MainWindow.cpp  界面逻辑：按钮 → 控制器，结果 → 控件
//   AxisController.h / .cpp        与控制器通信：工作线程与全部 aamotion_* 调用
#include <windows.h>
#include <commctrl.h>
#include "MainWindow.h"

#ifdef _MSC_VER
#pragma comment(linker, "/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' " \
    "version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")
#endif

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int show)
{
    InitCommonControls();
    MainWindow window;
    HWND hwnd = window.Create(instance);
    if (!hwnd) return 1;
    ShowWindow(hwnd, show);
    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        if (!IsDialogMessageW(hwnd, &msg))
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }
    return 0;
}
