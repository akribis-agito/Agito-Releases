"""与控制器通信的全部代码，不含任何界面代码。

所有 aamotion 调用都放进一个队列，由一个后台工作线程依次执行，所以通信慢或超时时界面不会卡住，
也不会有两个线程同时调用 SDK。结果通过回调报告；回调在工作线程（或 SDK 的线程）上调用，
由界面层负责切回界面线程。
"""
import math
import os
import queue
import threading
from datetime import datetime

import aamotion as am

# 状态面板中各行的名称，顺序与 on_status 给出的值一致
STATUS_NAMES = ["Pos", "PosRef", "MotorOn", "MotionStat", "InTargetStat", "ConFlt", "换相", "HomingStat"]
AXES = {"A": am.AXIS_REF_A, "B": am.AXIS_REF_B, "C": am.AXIS_REF_C, "D": am.AXIS_REF_D}


class AxisController:
    def __init__(self):
        self.on_log = lambda line: None            # 一行日志
        self.on_connection = lambda ok: None       # 连接状态变化
        self.on_status = lambda values: None       # 一轮状态值，顺序同 STATUS_NAMES
        self.h = None
        self.connected = False
        self._poll_pending = False
        self._jobs = queue.Queue()
        threading.Thread(target=self._work, name="AAMotion worker", daemon=True).start()

    def connect(self, target):
        """连接控制器（IP）或仿真器（firmware_sim.exe 的完整路径）。"""
        if self.connected:
            return

        def job():
            self.h = am.initialize(am.CONTROLLER_TYPE_AGM800)
            if not self.h:
                self._log("initialize failed; AACommServerNative missing?")
                return
            am.set_error_occurred_handler(self.h, self._on_error)
            if not target.lower().endswith(".exe"):
                ok = am.connect(self.h, target, True, False)
            elif os.path.isfile(target):
                am.init(self.h)   # 仿真器需要先启动 AACommServer
                ok = am.connect_simulation(self.h, target, None, None, None)
            else:
                self._log("simulator not found: " + target)
                am.release(self.h)
                return
            if not ok:
                am.disconnect(self.h)   # 同时释放句柄
            self.connected = ok
            self._log(f"connected to {target}" if ok else "connect failed; check Target and the cable")
            self.on_connection(ok)
        self._jobs.put(job)

    def disconnect(self):
        if not self.connected:
            return

        def job():
            self.connected = False
            am.disconnect(self.h)
            self._log("disconnected")
            self.on_connection(False)
        self._jobs.put(job)

    def send(self, name, axis, action, *args):
        """对一根轴发出一条命令。命令被接受不等于动作完成：完成与否看状态。"""
        if not self.connected:
            self._log("not connected")
            return

        def job():
            ok = action(self.h, axis, *args)
            self._log(f"{name} " + ("accepted" if ok else "failed; see any ERR line above"))
        self._jobs.put(job)

    def run_operation(self, axis, operation, arguments):
        from axis_operations import run

        def job():
            if self.connected:
                self._log(run(self.h, axis, operation, arguments))
            else:
                self._log("not connected")
        self._jobs.put(job)

    def poll(self, axis):
        """读一次状态；上一轮还没读完就跳过，避免排队堆积。"""
        if not self.connected or self._poll_pending:
            return
        self._poll_pending = True
        self._jobs.put(lambda: self._read_status(axis))

    def shutdown(self, axis):
        """请求停止并断使能，再断开，最多等待 5 秒。断开通信不能证明设备已停止。"""
        if self.connected:
            done = threading.Event()

            def job():
                try:
                    am.stop(self.h, axis)
                    am.motor_off(self.h, axis)
                finally:
                    am.disconnect(self.h)
                    self.connected = False
                    done.set()
            self._jobs.put(job)
            done.wait(5)
        self._jobs.put(None)

    def log(self, text):
        self._log(text)

    # ---------------- 工作线程 ----------------

    def _work(self):
        while True:
            job = self._jobs.get()
            if job is None:
                return
            try:
                job()
            except Exception as ex:
                self._log(f"Failed: {ex}")

    def _log(self, text):
        self.on_log(f"[{datetime.now():%H:%M:%S}] {text}")

    def _on_error(self, code, message, exception):
        # 错误回调可能在 SDK 的任何线程上调用
        self._log(f"ERR {code}: {message} | {exception}")

    def _read(self, axis, keyword):
        value = am.get_value(self.h, axis, keyword, 0)
        return math.nan if value is None else value

    def _read_status(self, axis):
        try:
            stat = self._read(axis, am.KEYWORD_MOTION_STAT)
            fault = self._read(axis, am.KEYWORD_CON_FLT)
            homing = self._read(axis, am.KEYWORD_HOMING_STAT)
            in_target = self._read(axis, am.KEYWORD_IN_TARGET_STAT)

            def describe(f, v):
                return "" if math.isnan(v) else f(int(v))
            self.on_status([
                f"{self._read(axis, am.KEYWORD_POS):g}",
                f"{self._read(axis, am.KEYWORD_POS_REF):g}",
                f"{self._read(axis, am.KEYWORD_MOTOR_ON):g}",
                f"{stat:g}  {describe(am.get_motion_stat_des, stat)}",
                f"{in_target:g}" + ("  到位" if in_target == 4 else ""),
                f"{fault:g}  {describe(am.get_con_flt_des, fault)}",
                "已换相" if am.is_auto_phase(self.h, axis) else "未换相",
                f"{homing:g}  {describe(am.get_homing_stat_des, homing)}",
            ])
        finally:
            self._poll_pending = False
