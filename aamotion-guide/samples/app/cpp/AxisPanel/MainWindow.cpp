#include "MainWindow.h"
#include "resource.h"
#include <cwchar>
#include <filesystem>
#include <fstream>
#include <sstream>
#include "AxisOperations.h"

// 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
static const wchar_t* DefaultTarget = L"172.1.1.101";
static const UINT WM_UI = WM_APP + 1;
static const UINT_PTR PollTimer = 1;

static std::wstring Wide(const std::string& text)
{
    int n = MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, nullptr, 0);
    std::wstring out(n > 0 ? n - 1 : 0, L'\0');
    if (n > 1) MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, &out[0], n);
    return out;
}

static std::string Narrow(const std::wstring& text)
{
    int n = WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, nullptr, 0, nullptr, nullptr);
    std::string out(n > 0 ? n - 1 : 0, '\0');
    if (n > 1) WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, &out[0], n, nullptr, nullptr);
    return out;
}

HWND MainWindow::Create(HINSTANCE instance)
{
    return CreateDialogParamW(instance, MAKEINTRESOURCEW(IDD_MAIN), nullptr, DialogProc,
                              (LPARAM)this);
}

INT_PTR CALLBACK MainWindow::DialogProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    if (msg == WM_INITDIALOG)
    {
        SetWindowLongPtrW(hwnd, DWLP_USER, lp);
        MainWindow* self = (MainWindow*)lp;
        self->hwnd_ = hwnd;
        self->OnInit();
        return TRUE;
    }
    MainWindow* self = (MainWindow*)GetWindowLongPtrW(hwnd, DWLP_USER);
    if (!self) return FALSE;
    switch (msg)
    {
    case WM_COMMAND:
        if (LOWORD(wp) == IDC_OPERATION && HIWORD(wp) == CBN_SELCHANGE) self->SelectOperation();
        if (HIWORD(wp) == BN_CLICKED) self->OnCommand(LOWORD(wp));
        return TRUE;
    case WM_TIMER:
        self->controller_.Poll(self->SelectedAxis());
        return TRUE;
    case WM_UI:
        self->RunUiUpdates();
        return TRUE;
    case WM_CLOSE:
        self->OnClose();
        return TRUE;
    case WM_DESTROY:
        PostQuitMessage(0);
        return TRUE;
    }
    return FALSE;
}

void MainWindow::OnInit()
{
    wchar_t executable[32768];
    GetModuleFileNameW(nullptr, executable, 32768);
    std::ifstream menu(std::filesystem::path(executable).parent_path() / "operations.tsv");
    std::string line;
    while (std::getline(menu, line))
    {
        std::vector<std::string> row;
        std::istringstream fields(line);
        for (std::string field; std::getline(fields, field, '\t');) row.push_back(field);
        if (row.size() != 5) continue;
        SendDlgItemMessageW(hwnd_, IDC_OPERATION, CB_ADDSTRING, 0, (LPARAM)Wide(row[1]).c_str());
        operations_.push_back(std::move(row));
    }
    SendDlgItemMessageW(hwnd_, IDC_OPERATION, CB_SETCURSEL, 0, 0);
    SelectOperation();
    if (operations_.empty()) AddLog("Missing operations.tsv beside AxisPanel.exe");
    SetDlgItemTextW(hwnd_, IDC_TARGET, DefaultTarget);
    SetDlgItemTextW(hwnd_, IDC_SPEED, L"2000");
    SetDlgItemTextW(hwnd_, IDC_ACCEL, L"10000");
    SetDlgItemTextW(hwnd_, IDC_DECEL, L"10000");
    SetDlgItemTextW(hwnd_, IDC_VALUE, L"10000");
    for (const wchar_t* axis : {L"A", L"B", L"C", L"D"})
        SendDlgItemMessageW(hwnd_, IDC_AXIS, CB_ADDSTRING, 0, (LPARAM)axis);
    SendDlgItemMessageW(hwnd_, IDC_AXIS, CB_SETCURSEL, 0, 0);

    // 控制器的回调在工作线程上调用：只把结果交给界面线程，不在这里碰控件
    controller_.onLog = [this](const std::string& line) {
        OnUi([this, line] { AddLog(line); });
    };
    controller_.onConnection = [this](bool ok) {
        OnUi([this, ok] {
            SetDlgItemTextW(hwnd_, IDC_LINK, ok ? L"已连接" : L"未连接");
            if (ok) SetTimer(hwnd_, PollTimer, 250, nullptr);
            else KillTimer(hwnd_, PollTimer);
        });
    };
    controller_.onStatus = [this](const std::vector<std::string>& values) {
        OnUi([this, values] {
            for (size_t i = 0; i < values.size(); i++)
                SetDlgItemTextW(hwnd_, IDC_STATUS0 + (int)i, Wide(values[i]).c_str());
        });
    };
}

void MainWindow::SelectOperation()
{
    auto index = SendDlgItemMessageW(hwnd_, IDC_OPERATION, CB_GETCURSEL, 0, 0);
    if (index < 0) return;
    SetDlgItemTextW(hwnd_, IDC_OPERATION_HINT, Wide(operations_[index][2]).c_str());
    SetDlgItemTextW(hwnd_, IDC_ARGUMENTS, Wide(operations_[index][3]).c_str());
}

void MainWindow::OnCommand(int id)
{
    switch (id)
    {
    case IDC_RUN_OPERATION:
    {
        auto index = SendDlgItemMessageW(hwnd_, IDC_OPERATION, CB_GETCURSEL, 0, 0);
        if (index < 0) break;
        const auto& row = operations_[index];
        if (row[4] == "1" && MessageBoxW(hwnd_, Wide(row[1]).c_str(), L"AAMotion", MB_OKCANCEL) != IDOK) break;
        std::string operation = row[0], input = Narrow(TextOf(IDC_ARGUMENTS));
        Send(operation.c_str(), [this, operation, input](aamotion_controller h, int axis) {
            try
            {
                auto result = RunOperation(h, axis, operation, input);
                OnUi([this, result] { AddLog(result); });
                return true;
            }
            catch (const std::exception& error)
            {
                std::string result = error.what();
                OnUi([this, result] { AddLog(result); });
                return false;
            }
        });
        break;
    }
    case IDC_CONNECT: controller_.Connect(Narrow(TextOf(IDC_TARGET))); break;
    case IDC_DISCONNECT: controller_.Disconnect(); break;
    case IDC_AUTOPHASE: Send("auto_phase", aamotion_auto_phase); break;
    case IDC_MOTORON: Send("motor_on", aamotion_motor_on); break;
    case IDC_MOTOROFF: Send("motor_off", aamotion_motor_off); break;
    case IDC_MOVEABS: SendMove(true); break;
    case IDC_MOVEREL: SendMove(false); break;
    case IDC_JOGMINUS: Jog(-1); break;
    case IDC_JOGPLUS: Jog(1); break;
    case IDC_STOP: Send("stop", aamotion_stop); break;
    case IDC_ABORT: Send("abort", aamotion_abort); break;
    case IDC_HOME: Send("home", aamotion_home); break;
    }
}

// 关闭窗口：停止定时读状态，请求停止并断使能，再断开
void MainWindow::OnClose()
{
    KillTimer(hwnd_, PollTimer);
    controller_.Shutdown(SelectedAxis());
    DestroyWindow(hwnd_);
}

void MainWindow::Send(const char* name, std::function<bool(aamotion_controller, int)> action)
{
    // 按下按钮时就确定轴，命令执行时用户可能已切换
    controller_.Send(name, SelectedAxis(), action);
}

void MainWindow::SendMove(bool absolute)
{
    double value, speed, accel, decel;
    if (!NumberOf(IDC_VALUE, &value) || !NumberOf(IDC_SPEED, &speed) ||
        !NumberOf(IDC_ACCEL, &accel) || !NumberOf(IDC_DECEL, &decel))
        return;
    // 命令被接受不等于到位：到位看状态面板中的 InTargetStat = 4
    if (absolute)
        Send("move_abs", [=](aamotion_controller h, int a) {
            return aamotion_move_abs(h, a, value, speed, accel, decel);
        });
    else
        Send("move_relative", [=](aamotion_controller h, int a) {
            return aamotion_move_relative(h, a, value, speed, accel, decel);
        });
}

void MainWindow::Jog(int direction)
{
    double speed;
    if (NumberOf(IDC_SPEED, &speed))
        Send("jog", [=](aamotion_controller h, int a) {
            return aamotion_jog(h, a, direction * speed);
        });
}

int MainWindow::SelectedAxis() const
{
    LRESULT i = SendDlgItemMessageW(hwnd_, IDC_AXIS, CB_GETCURSEL, 0, 0);
    return 1 << (i < 0 ? 0 : (int)i);   // AAMOTION_AXIS_REF_A = 1, B = 2, C = 4 ...
}

std::wstring MainWindow::TextOf(int id) const
{
    wchar_t text[512];
    GetDlgItemTextW(hwnd_, id, text, 512);
    return text;
}

bool MainWindow::NumberOf(int id, double* value)
{
    std::wstring text = TextOf(id);
    wchar_t* end = nullptr;
    *value = wcstod(text.c_str(), &end);
    if (end != text.c_str() && *end == L'\0') return true;
    AddLog("not a number: " + Narrow(text));
    return false;
}

void MainWindow::AddLog(const std::string& line)
{
    HWND log = GetDlgItem(hwnd_, IDC_LOG);
    LRESULT index = SendMessageW(log, LB_ADDSTRING, 0, (LPARAM)Wide(line).c_str());
    SendMessageW(log, LB_SETTOPINDEX, index, 0);
}

void MainWindow::OnUi(std::function<void()> update)
{
    {
        std::lock_guard<std::mutex> guard(uiLock_);
        uiUpdates_.push_back(std::move(update));
    }
    PostMessageW(hwnd_, WM_UI, 0, 0);
}

void MainWindow::RunUiUpdates()
{
    std::deque<std::function<void()>> updates;
    {
        std::lock_guard<std::mutex> guard(uiLock_);
        updates.swap(uiUpdates_);
    }
    for (auto& update : updates) update();
}
