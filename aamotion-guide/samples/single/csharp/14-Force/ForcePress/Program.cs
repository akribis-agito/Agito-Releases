using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AAMotion;
// 压装流程 / A press sequence
// 顺序：连接 → 换相 → 使能 → 本次操作 → 确认结果 → 清理与断开
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const AxisRef Axis = AxisRef.A;
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        Console.WriteLine($"Axis {Axis} will move. Check travel and units.");
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
            ISingleAxis a = c.GetAxis(Axis);
            // --8<-- [end:connect]

            // --8<-- [start:prepare]
            // 仿真用虚拟电机。真实电机才需要换相。
            if (simulation)
                a.MotorType = 5;
            else if (!AAMotionAPI.IsAutoPhase(c, Axis))
            {
                Console.WriteLine("requested");
                AAMotionAPI.AutoPhase(c, Axis);
                var phaseTime = Stopwatch.StartNew();
                while (!AAMotionAPI.IsAutoPhase(c, Axis))
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
            AAMotionAPI.MotorOn(c, Axis);
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
            // 1) 接近：向 start + 3000 运动
            a.ForceCurr_SetMotion(absTrgt: start + 3000, relTrgt: 0,
                speed: 4000, accel: 50000, decel: 50000);
            // 2) 到 start + 2400 后换成速度 500 慢速接近
            a.ForceCurr_SetSlowApproach(speedChgNew: 500,
                speedChgPos: start + 2400, speedChgDir: 0, speedChgOn: 1);
            // 3) 越过 start + 2700 后切换到力控
            // 位置误差与模拟输入阈值 0：不用这两个条件
            a.ForceCurr_SetCurrModeSWTrigSrc(currPosTh: start + 2700,
                currPosThDir: 0, forcePosErrTh: 0, forceAInTh: 0);
            // 4) 力指令：一段，斜率 100，目标 200，保持时间 1000
            a.ForceCurr_SetForceCmdSource(forceCmdSrc: 1,
                forceCurrSlope: new[] { 100.0 },
                forceCmdHTime: new[] { 1000 },
                forceCmdVal: new[] { 200.0 });
            // 5) 完成后以速度 4000 回退到起点
            a.ForceCurr_SetRetractMotion(fRetractTarget: start,
                fRetractSpeed: 4000, fBeginOnToPos: 0);
            a.Begin();
            Console.WriteLine("press sequence started");
            // --8<-- [end:operation]

            // --8<-- [start:observe]
            // 5. 确认结果：方法返回不等于动作完成
            // 回退结束后连续 500 ms 静止，并回到位置控制
            var pressTime = Stopwatch.StartNew();
            var still = Stopwatch.StartNew();
            while (still.ElapsedMilliseconds < 500)
            {
                if (a.MotionStat != 0) still.Restart();
                if (pressTime.ElapsedMilliseconds >= 20000)
                {
                    a.GoToPosMode();
                    Console.WriteLine(
                        "sequence timeout; back to position mode");
                    return 6;
                }
                Thread.Sleep(20);
            }
            int mode = (int)c.GetValue(Axis, eKeyword.OperationMode);
            Console.WriteLine($"done: Pos={a.Pos}, OperationMode={mode}");
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
                    AAMotionAPI.Stop(c, Axis);
                    var stopTime = Stopwatch.StartNew();
                    ISingleAxis a = c.GetAxis(Axis);
                    while (a.MotionStat != 0 &&
                           stopTime.ElapsedMilliseconds < 5000)
                        Thread.Sleep(20);
                    if (a.MotionStat == 0)
                    {
                        AAMotionAPI.MotorOff(c, Axis);
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
