using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using AAMotion;

namespace AxisPanel
{
    /// <summary>
    /// 与控制器通信的全部代码，不含任何界面类型。所有 SDK 调用都放进一个队列，
    /// 由一个后台工作线程依次执行，所以通信慢或超时时界面不会卡住，也不会有两个线程同时调用 SDK。
    /// 结果通过事件报告；事件在工作线程（或 SDK 的线程）上触发，由界面层负责切回界面线程。
    /// </summary>
    public sealed class AxisController : IDisposable
    {
        /// <summary>状态面板中各行的名称，顺序与 <see cref="StatusRead"/> 给出的值一致。</summary>
        public static readonly string[] StatusNames =
            { "Pos", "PosRef", "MotorOn", "MotionStat", "InTargetStat", "ConFlt", "换相", "HomingStat" };

        private readonly BlockingCollection<Action> _jobs = new BlockingCollection<Action>();
        private readonly Thread _worker;
        private MotionController _c;
        private volatile bool _connected;
        private int _pollPending;

        public event Action<string> Logged;
        public event Action<bool> ConnectionChanged;
        public event Action<string[]> StatusRead;

        public AxisController()
        {
            _worker = new Thread(Work) { IsBackground = true, Name = "AAMotion worker" };
            _worker.Start();
        }

        public bool IsConnected => _connected;

        /// <summary>连接控制器（IP）或仿真器（firmware_sim.exe 的完整路径）。</summary>
        public void Connect(string target)
        {
            if (_connected) return;
            _jobs.Add(() =>
            {
                _c = AAMotionAPI.Initialize(ControllerType.AGM800);
                _c.ErrorOccurred += (code, command, message) => Log($"ERR {code}: {command} | {message}");
                bool ok;
                if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    ok = AAMotionAPI.Connect(_c, target);
                else if (File.Exists(target))
                {
                    _c.Init();   // 仿真器需要先启动 AACommServer
                    ok = AAMotionAPI.ConnectSimulation(_c, target);
                }
                else
                {
                    Log("simulator not found: " + target);
                    return;
                }
                _connected = ok;
                Log(ok ? "connected to " + target : "connect failed; check Target and the cable");
                ConnectionChanged?.Invoke(ok);
            });
        }

        public void Disconnect()
        {
            if (!_connected) return;
            _jobs.Add(() =>
            {
                _connected = false;
                AAMotionAPI.Disconnect(_c);
                Log("disconnected");
                ConnectionChanged?.Invoke(false);
            });
        }

        /// <summary>对一根轴发出一条命令。命令被接受不等于动作完成：完成与否看状态。</summary>
        public void Send(string name, AxisRef axis, Action<ISingleAxis> action)
        {
            if (!_connected)
            {
                Log("not connected");
                return;
            }
            _jobs.Add(() =>
            {
                action(_c.GetAxis(axis));
                Log($"{name} sent to axis {axis}");
            });
        }

        public void RunOperation(AxisRef axis, string id, string input)
        {
            _jobs.Add(() => Log(_connected ? AxisOperations.Run(_c, axis, id, input) : "not connected"));
        }

        /// <summary>读一次状态；上一轮还没读完就跳过，避免排队堆积。</summary>
        public void Poll(AxisRef axis)
        {
            if (!_connected || Interlocked.Exchange(ref _pollPending, 1) == 1) return;
            _jobs.Add(() =>
            {
                try
                {
                    ISingleAxis a = _c.GetAxis(axis);
                    int stat = a.MotionStat, fault = a.ConFlt, homing = a.HomingStat, inTarget = a.InTargetStat;
                    StatusRead?.Invoke(new[]
                    {
                        a.Pos.ToString(), a.PosRef.ToString(), a.MotorOn.ToString(),
                        $"{stat}  {EnumPropertyHelper.GetMotionStatDes(stat)}",
                        inTarget + (inTarget == 4 ? "  到位" : ""),
                        $"{fault}  {EnumPropertyHelper.GetConFltDes(fault)}",
                        AAMotionAPI.IsAutoPhase(_c, axis) ? "已换相" : "未换相",
                        $"{homing}  {EnumPropertyHelper.GetHomingStatDes(homing)}"
                    });
                }
                finally
                {
                    Interlocked.Exchange(ref _pollPending, 0);
                }
            });
        }

        /// <summary>
        /// 请求停止并断使能，再断开，最多等待 5 秒。断开通信不能证明设备已停止。
        /// </summary>
        public void Shutdown(AxisRef axis)
        {
            if (_connected)
            {
                var done = new ManualResetEventSlim(false);
                _jobs.Add(() =>
                {
                    try
                    {
                        AAMotionAPI.Stop(_c, axis);
                        AAMotionAPI.MotorOff(_c, axis);
                    }
                    finally
                    {
                        _connected = false;
                        AAMotionAPI.Disconnect(_c);
                        done.Set();
                    }
                });
                done.Wait(5000);
            }
            _jobs.CompleteAdding();
        }

        public void Dispose() => _jobs.Dispose();

        private void Work()
        {
            foreach (Action job in _jobs.GetConsumingEnumerable())
            {
                try
                {
                    job();
                }
                catch (ControllerCommandException ex)
                {
                    Log($"{ex.Command} refused: ERR {ex.ErrorCode} {ex.Message}");
                }
                catch (Exception ex)
                {
                    Log("Failed: " + ex.Message);
                }
            }
        }

        private void Log(string text) => Logged?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] {text}");
    }
}
