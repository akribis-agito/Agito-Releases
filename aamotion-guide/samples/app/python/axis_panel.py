"""轴控制面板 / Axis Panel（tkinter）：连接、实时状态、换相与使能、点位、Jog、停止、回零与日志。

    axis_panel.py       界面：布局，按钮 → 控制器，结果 → 控件
    axis_controller.py  与控制器通信：工作线程与全部 aamotion 调用

先在本目录运行一次  python get_sdk.py  取得 aamotion 库与 AACommServerNative，再运行  python axis_panel.py
"""
import queue
import tkinter as tk
from tkinter import messagebox, ttk

import aamotion as am
from axis_controller import AXES, STATUS_NAMES, AxisController
from axis_operations import OPERATIONS

# 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
DEFAULT_TARGET = "172.1.1.101"


class AxisPanel:
    def __init__(self, root):
        self.root = root
        self.updates = queue.Queue()     # 任何线程 -> 界面线程
        self.controller = AxisController()
        # 控制器的回调在工作线程上调用：只把结果交给界面线程，不在这里碰控件
        self.controller.on_log = lambda line: self.on_ui(lambda: self.add_log(line))
        self.controller.on_connection = lambda ok: self.on_ui(lambda: self.show_connection(ok))
        self.controller.on_status = lambda values: self.on_ui(lambda: self.show_status(values))
        root.title("AAMotion 轴控制面板 / Axis Panel")
        self.build_layout()
        root.after(50, self.apply_updates)
        root.protocol("WM_DELETE_WINDOW", self.close)

    # ---------------- 界面布局 ----------------

    def build_layout(self):
        left = ttk.Frame(self.root, padding=8)
        left.grid(row=0, column=0, sticky="ns")
        right = ttk.LabelFrame(self.root, text="日志 Log", padding=4)
        right.grid(row=0, column=1, sticky="nsew", padx=8, pady=8)
        self.root.columnconfigure(1, weight=1)
        self.root.rowconfigure(0, weight=1)

        box = ttk.LabelFrame(left, text="连接 Connection", padding=6)
        box.pack(fill="x", pady=4)
        ttk.Label(box, text="Target").grid(row=0, column=0, sticky="w")
        self.target = ttk.Entry(box, width=36)
        self.target.insert(0, DEFAULT_TARGET)
        self.target.grid(row=0, column=1, columnspan=3, sticky="we")
        ttk.Button(box, text="连接", command=self.connect).grid(row=1, column=0, pady=4)
        ttk.Button(box, text="断开", command=self.controller.disconnect).grid(row=1, column=1, pady=4)
        self.link = ttk.Label(box, text="未连接")
        self.link.grid(row=1, column=2, columnspan=2, sticky="w")

        box = ttk.LabelFrame(left, text="状态 Status", padding=6)
        box.pack(fill="x", pady=4)
        ttk.Label(box, text="轴").grid(row=0, column=0, sticky="w")
        self.axis = ttk.Combobox(box, values=list(AXES), width=4, state="readonly")
        self.axis.current(0)
        self.axis.grid(row=0, column=1, sticky="w")
        self.status = []
        for i, name in enumerate(STATUS_NAMES):
            ttk.Label(box, text=name, width=14).grid(row=i + 1, column=0, sticky="w")
            value = ttk.Label(box, text="-", width=40)
            value.grid(row=i + 1, column=1, sticky="w")
            self.status.append(value)

        box = ttk.LabelFrame(left, text="电机 Motor", padding=6)
        box.pack(fill="x", pady=4)
        ttk.Button(box, text="换相", command=lambda: self.send("auto_phase", am.auto_phase)).pack(side="left")
        ttk.Button(box, text="使能", command=lambda: self.send("motor_on", am.motor_on)).pack(side="left")
        ttk.Button(box, text="断使能", command=lambda: self.send("motor_off", am.motor_off)).pack(side="left")

        box = ttk.LabelFrame(left, text="运动 Motion", padding=6)
        box.pack(fill="x", pady=4)
        self.speed = self.number(box, "速度", "2000", 0, 0)
        self.accel = self.number(box, "加速度", "10000", 0, 2)
        self.decel = self.number(box, "减速度", "10000", 0, 4)
        self.value = self.number(box, "目标 / 距离", "10000", 1, 0)
        ttk.Button(box, text="绝对运动", command=lambda: self.send_move(True)).grid(row=1, column=2, columnspan=2)
        ttk.Button(box, text="相对运动", command=lambda: self.send_move(False)).grid(row=1, column=4, columnspan=2)
        ttk.Button(box, text="Jog -", command=lambda: self.jog(-1)).grid(row=2, column=0, pady=4)
        ttk.Button(box, text="Jog +", command=lambda: self.jog(1)).grid(row=2, column=1, pady=4)
        ttk.Button(box, text="停止 Stop", command=lambda: self.send("stop", am.stop)).grid(row=2, column=2, columnspan=2)
        ttk.Button(box, text="急停 Abort", command=lambda: self.send("abort", am.abort)).grid(row=2, column=4, columnspan=2)
        ttk.Button(box, text="按已配置流程回零", command=lambda: self.send("home", am.home)).grid(
            row=3, column=0, columnspan=3, sticky="w")

        ttk.Label(left, text="按钮会让真实轴运动：先在仿真器上试，真机先在 PCSuite 中确认行程与急停。",
                  foreground="darkred", wraplength=380).pack(fill="x", pady=4)

        demos = ttk.LabelFrame(right, text="功能示例 / Demo operations", padding=6)
        demos.pack(fill="x", pady=(0, 6))
        self.operation = ttk.Combobox(demos, values=[row[1] for row in OPERATIONS], state="readonly")
        self.operation.pack(fill="x")
        self.operation.bind("<<ComboboxSelected>>", self.select_operation)
        self.operation_hint = ttk.Label(demos, wraplength=480)
        self.operation_hint.pack(anchor="w")
        controls = ttk.Frame(demos)
        controls.pack(fill="x")
        self.arguments = ttk.Entry(controls)
        self.arguments.pack(side="left", fill="x", expand=True)
        ttk.Button(controls, text="执行 Run", command=self.run_operation).pack(side="right")
        self.operation.current(0)
        self.select_operation()

        self.log_box = tk.Listbox(right, width=60)
        self.log_box.pack(fill="both", expand=True)

    def select_operation(self, _event=None):
        _, _, hint, default, _ = OPERATIONS[self.operation.current()]
        self.operation_hint.config(text=hint)
        self.arguments.delete(0, "end")
        self.arguments.insert(0, default)

    def run_operation(self):
        key, label, hint, _, confirm = OPERATIONS[self.operation.current()]
        if not self.controller.connected:
            self.add_log("not connected")
            return
        args = self.arguments.get().strip()
        if confirm == "1" and not messagebox.askyesno(label, f"Axis: {self.axis.get()}\n{hint}\n{args}\n\nRun on the connected controller?"):
            return
        self.controller.run_operation(self.selected_axis(), key, args)

    @staticmethod
    def number(parent, caption, text, row, column):
        ttk.Label(parent, text=caption).grid(row=row, column=column, sticky="w")
        entry = ttk.Entry(parent, width=9)
        entry.insert(0, text)
        entry.grid(row=row, column=column + 1, sticky="w")
        return entry

    # ---------------- 回到界面线程 ----------------

    def on_ui(self, update):
        """在界面线程上执行 update（可从任何线程调用）。"""
        self.updates.put(update)

    def apply_updates(self):
        while not self.updates.empty():
            self.updates.get()()
        self.root.after(50, self.apply_updates)

    def add_log(self, line):
        self.log_box.insert("end", line)
        self.log_box.see("end")

    def show_connection(self, ok):
        self.link.config(text="已连接" if ok else "未连接")
        if ok:
            self.schedule_poll()

    def show_status(self, values):
        for label, value in zip(self.status, values):
            label.config(text=value)

    # ---------------- 按钮 ----------------

    def selected_axis(self):
        return AXES[self.axis.get()]

    def connect(self):
        self.controller.connect(self.target.get().strip())

    def send(self, name, action, *args):
        # 按下按钮时就确定轴，命令执行时用户可能已切换
        self.controller.send(name, self.selected_axis(), action, *args)

    def numbers(self, *entries):
        try:
            return [float(e.get()) for e in entries]
        except ValueError:
            self.controller.log("not a number: " + ", ".join(e.get() for e in entries))
            return None

    def send_move(self, absolute):
        values = self.numbers(self.value, self.speed, self.accel, self.decel)
        if values:
            # 命令被接受不等于到位：到位看状态面板中的 InTargetStat = 4
            if absolute:
                self.send("move_abs", am.move_abs, *values)
            else:
                self.send("move_relative", am.move_relative, *values)

    def jog(self, direction):
        values = self.numbers(self.speed)
        if values:
            self.send("jog", am.jog, direction * values[0])

    def schedule_poll(self):
        """连接期间每 250 ms 读一次状态。"""
        if self.controller.connected:
            self.controller.poll(self.selected_axis())
            self.root.after(250, self.schedule_poll)

    def close(self):
        """关闭窗口：先停止并断使能，再断开。"""
        self.controller.shutdown(self.selected_axis())
        self.root.destroy()


def main():
    root = tk.Tk()
    AxisPanel(root)
    root.mainloop()


if __name__ == "__main__":
    main()
