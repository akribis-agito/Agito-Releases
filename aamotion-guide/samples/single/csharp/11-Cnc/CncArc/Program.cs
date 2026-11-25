using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AAMotion;
// 直线接圆弧 / A line, then an arc
// 顺序：连接 → 换相 → 使能（A、B 两轴）→ 本次操作 → 确认结果 → 清理与断开
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";
    private static readonly AxisRef[] Axes = { AxisRef.A, AxisRef.B };

    private static int Main(string[] args)
    {
        Console.WriteLine("Axes A and B will move. Check travel and units.");
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
            ISingleAxis b = c.GetAxis(AxisRef.B);
            // --8<-- [end:connect]

            // --8<-- [start:prepare]
            // 2. 换相：A、B 两轴都要先换相，等待有上限
            foreach (AxisRef axis in Axes)
            {
                if (simulation)
                {
                    c.GetAxis(axis).MotorType = 5;
                    continue;
                }
                if (AAMotionAPI.IsAutoPhase(c, axis)) continue;
                Console.WriteLine(axis + " requested");
                AAMotionAPI.AutoPhase(c, axis);
                var phaseTime = Stopwatch.StartNew();
                while (!AAMotionAPI.IsAutoPhase(c, axis))
                {
                    if (phaseTime.ElapsedMilliseconds >= 10000)
                    {
                        Console.WriteLine(axis + " timed out");
                        return 3;
                    }
                    Thread.Sleep(20);
                }
            }
            if (!simulation) Console.WriteLine("A and B commutated");

            // 3. 使能：清理阶段也会处理结果不确定的使能
            motorOnRequested = true;
            foreach (AxisRef axis in Axes)
            {
                AAMotionAPI.MotorOn(c, axis);
                int on = c.GetAxis(axis).MotorOn;
                if (on != 1)
                {
                    Console.WriteLine($"{axis} refused; MotorOn={on}");
                    return 4;
                }
            }
            Console.WriteLine("A and B on");
            // --8<-- [end:prepare]

            // --8<-- [start:operation]
            // 4. 本次操作
            IGroupAxis g = c.GetGroup(AxisRef.A);
            int x0 = (int)a.Pos, y0 = (int)b.Pos;
            if (!g.ClearBuffer() ||
                !g.SetStartPositions(x0, y0, null) ||
                !g.LinearAbsoluteXY(x0 + 10000, y0, 8000, 0) ||
            // 逆时针（dir 0）四分之一圆，终点 (x0+20000, y0+10000)；
            // 圆心的绝对坐标是 (x0+10000, y0+10000)
                !g.ArcXY(x0 + 20000, y0 + 10000,
                         x0 + 10000, y0 + 10000, 0, 8000, 0) ||
                !g.Delay(200) ||
                !g.LinearAbsoluteXY(x0, y0, 8000, 0))
            {
                Console.WriteLine("CNC segment refused; path not started");
                return 6;
            }
            if (!c.SendCommandString("AMotionMode=11", out _) ||
                !c.SendCommandString("BMotionMode=11", out _) ||
                !g.Begin())
            {
                Console.WriteLine("Begin refused; see the ERR line");
                return 6;
            }
            Console.WriteLine("line + arc path started");
            // --8<-- [end:operation]

            // --8<-- [start:observe]
            // 5. 确认结果：方法返回不等于动作完成
            var pathTime = Stopwatch.StartNew();
            while (!g.IsCNCCompleted(AxisRef.A))
            {
                if (pathTime.ElapsedMilliseconds >= 30000)
                {
                    g.Stop();
                    Console.WriteLine("path timeout; group Stop requested");
                    return 6;
                }
                Thread.Sleep(50);
            }
            Console.WriteLine($"path done: A={a.Pos}, B={b.Pos}");
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
                    foreach (AxisRef axis in Axes)
                        AAMotionAPI.Stop(c, axis);
                    var stopTime = Stopwatch.StartNew();
                    ISingleAxis sa = c.GetAxis(AxisRef.A);
                    ISingleAxis sb = c.GetAxis(AxisRef.B);
                    Func<bool> moving = () =>
                        sa.MotionStat != 0 || sb.MotionStat != 0;
                    while (moving() && stopTime.ElapsedMilliseconds < 5000)
                        Thread.Sleep(20);
                    if (!moving())
                    {
                        foreach (AxisRef axis in Axes)
                            AAMotionAPI.MotorOff(c, axis);
                        Console.WriteLine("stopped, motors off");
                    }
                    else
                        Console.WriteLine("stop unconfirmed; check both axes");
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
