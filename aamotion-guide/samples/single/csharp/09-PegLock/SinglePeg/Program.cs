using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AAMotion;
// 单点位置事件 / Single position event
// 顺序：连接 → 换相 → 使能 → 本次操作 → 确认结果 → 清理与断开
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        Console.WriteLine("Axis A will move. Check travel and units.");
        Console.WriteLine("Type RUN to continue:");
        if (Console.ReadLine() != "RUN") return 1;
        string connectionTarget = args.Length == 0 ? Target : args[0];
        MotionController c = AAMotionAPI.Initialize(ControllerType.AGM800);
        c.ErrorOccurred += (code, command, message) =>
            Console.WriteLine($"{code}: {command} | {message}");
        bool motorOnRequested = false;
        try
        {
            // --8<-- [start:connect]
            // 1. 连接：Target 是 IP 时连接控制器；以 .exe 结尾时连接仿真器
            Console.WriteLine(connectionTarget);
            bool connected;
            bool simulation = connectionTarget.EndsWith(".exe",
                StringComparison.OrdinalIgnoreCase);
            if (!simulation)
                connected = AAMotionAPI.Connect(c, connectionTarget);
            else if (File.Exists(connectionTarget))
            {
                c.Init();   // 仿真器需要先启动 AACommServer
                connected = AAMotionAPI.ConnectSimulation(c, connectionTarget);
            }
            else
            {
                Console.WriteLine("simulator not found; edit Target");
                return 2;
            }
            if (!connected)
            {
                Console.WriteLine("failed; check Target and the cable");
                return 2;
            }
            ISingleAxis a = c.GetAxis(AxisRef.A);
            // --8<-- [end:connect]

            // --8<-- [start:prepare]
            // 仿真用虚拟电机。真实电机才需要换相。
            if (simulation)
                a.MotorType = 5;
            else if (!AAMotionAPI.IsAutoPhase(c, AxisRef.A))
            {
                Console.WriteLine("requested");
                AAMotionAPI.AutoPhase(c, AxisRef.A);
                var phaseTime = Stopwatch.StartNew();
                while (!AAMotionAPI.IsAutoPhase(c, AxisRef.A))
                {
                    if (phaseTime.ElapsedMilliseconds >= 10000)
                    {
                        Console.WriteLine("timed out; motor stays off");
                        return 3;
                    }
                    Thread.Sleep(20);
                }
            }
            if (!simulation) Console.WriteLine("commutated");

            // 3. 使能：清理阶段也会处理结果不确定的使能
            motorOnRequested = true;
            AAMotionAPI.MotorOn(c, AxisRef.A);
            if (a.MotorOn != 1)
            {
                Console.WriteLine("refused; MotorOn=" + a.MotorOn);
                return 4;
            }
            Console.WriteLine("on");
            // --8<-- [end:prepare]

            // --8<-- [start:operation]
            // 4. 本次操作
            double start = a.Pos;
            a.EventCntr = 0;
            // 经过 start + 5000 时在输出 1 上发 50 µs 的脉冲
            a.SetSingleEventPEG(start + 5000, eventSelect: 1,
                eventPulseRes: 0, eventPulseWid: 50);
            Console.WriteLine($"armed at {start + 5000}: EventOn={a.EventOn}");
            a.MoveAbs(start + 10000, speed: 2000, accel: 10000, decel: 10000);
            // --8<-- [end:operation]

            // --8<-- [start:observe]
            // 5. 确认结果：方法返回不等于动作完成
            var motionTime = Stopwatch.StartNew();
            while (a.InTargetStat != 4)
            {
                if (motionTime.ElapsedMilliseconds >= 10000)
                {
                    AAMotionAPI.Stop(c, AxisRef.A);
                    Console.WriteLine("settle timeout; Stop requested");
                    return 6;
                }
                Thread.Sleep(20);
            }
            Console.WriteLine($"settled: Pos={a.Pos}, " +
                $"MotionStat={a.MotionStat}, InTargetStat={a.InTargetStat}");
            // 单点事件触发后 EventOn 自动回到 0
            Console.WriteLine($"EventCntr={a.EventCntr}, EventOn={a.EventOn}");
            // --8<-- [end:observe]
            return 0;
        }
        catch (ControllerCommandException ex)
        {
            Console.WriteLine($"{ex.Command} refused: ERR {ex.ErrorCode}");
            return 5;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return 5;
        }
        finally
        {
            // --8<-- [start:cleanup]
            // 6. 清理：先确认停止再断使能
            //    断开通信不能证明设备已停止
            try
            {
                if (motorOnRequested)
                {
                    AAMotionAPI.Stop(c, AxisRef.A);
                    var stopTime = Stopwatch.StartNew();
                    ISingleAxis a = c.GetAxis(AxisRef.A);
                    while (a.MotionStat != 0 &&
                           stopTime.ElapsedMilliseconds < 5000)
                        Thread.Sleep(20);
                    if (a.MotionStat == 0)
                    {
                        AAMotionAPI.MotorOff(c, AxisRef.A);
                        Console.WriteLine("stopped, motor off");
                    }
                    else
                        Console.WriteLine("stop unconfirmed; check the axis");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("failed, verify axis state: " + ex.Message);
            }
            finally
            {
                AAMotionAPI.Disconnect(c);
            }
            // --8<-- [end:cleanup]
        }
    }
}
