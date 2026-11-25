using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AAMotion;
// 切换控制模式 / Switch the control mode
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
            // OperationMode：1 电流、2 速度、3 位置、4 力控
            int mode = (int)c.GetValue(Axis, eKeyword.OperationMode);
            string text = EnumPropertyHelper.GetOperationModeDes(mode);
            Console.WriteLine($"OperationMode={mode}: {text}");
            a.GoToCurrMode();   // 只有电流环；电流指令按控制器中的设置
            Thread.Sleep(500);
            mode = (int)c.GetValue(Axis, eKeyword.OperationMode);
            Console.WriteLine("after GoToCurrMode: OperationMode=" + mode);
            a.GoToPosMode();    // 回到位置控制
            Thread.Sleep(200);
            // --8<-- [end:operation]

            // --8<-- [start:observe]
            // 5. 确认结果：方法返回不等于动作完成
            int now = (int)c.GetValue(Axis, eKeyword.OperationMode);
            Console.WriteLine($"OperationMode={now} (3 = position control)");
            if (now != 3) return 6;
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
