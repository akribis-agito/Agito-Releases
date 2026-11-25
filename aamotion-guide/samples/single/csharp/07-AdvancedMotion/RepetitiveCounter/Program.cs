using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AAMotion;
// 按计数停止往返 / Stop after a count
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
            // 按绝对坐标设定往返端点，两端各停 100 ms。
            double far = a.Pos + 5000;
            a.MoveAbsRepetitiveCycle(far, 100, 8, speed: 5000, accel: 50000,
                decel: 50000);
            int startCount = a.RptCounter;
            var countTime = Stopwatch.StartNew();
            while (a.RptCounter - startCount < 3)
            {
                if (countTime.ElapsedMilliseconds >= 20000)
                {
                    AAMotionAPI.Stop(c, AxisRef.A);
                    Console.WriteLine("count timeout; Stop requested");
                    return 6;
                }
                Thread.Sleep(20);
            }
            a.StopRep();   // 走完正在进行的这一程后停止
            Console.WriteLine($"RptCounter={a.RptCounter}; StopRep requested");
            // --8<-- [end:operation]

            // --8<-- [start:observe]
            // 5. 确认结果：方法返回不等于动作完成
            var motionTime = Stopwatch.StartNew();
            while (a.MotionStat != 0)
            {
                if (motionTime.ElapsedMilliseconds >= 10000)
                {
                    Console.WriteLine(
                        "standstill timeout; verify actual state");
                    return 6;
                }
                Thread.Sleep(20);
            }
            Console.WriteLine($"ended: Pos={a.Pos}, " +
                $"MotionStat={a.MotionStat}, InTargetStat={a.InTargetStat}");
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
