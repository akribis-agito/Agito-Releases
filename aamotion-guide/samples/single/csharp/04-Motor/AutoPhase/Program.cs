using System;
using System.IO;
using AAMotion;
using System.Diagnostics;
using System.Threading;
// 自动换相 / Auto-phasing
// 顺序：连接 → 本次操作 → 断开。轴不运动。
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        string connectionTarget = args.Length == 0 ? Target : args[0];
        Console.WriteLine("Auto-phasing may move axis A slightly.");
        Console.WriteLine("Type RUN to continue:");
        if (Console.ReadLine() != "RUN") return 1;
        MotionController c = AAMotionAPI.Initialize(ControllerType.AGM800);
        c.ErrorOccurred += (code, command, message) =>
            Console.WriteLine($"[ERR] {code}: {command} | {message}");
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
            // --8<-- [end:connect]

            // --8<-- [start:operation]
            // 2. 本次操作
            ISingleAxis a = c.GetAxis(AxisRef.A);
            if (AAMotionAPI.IsAutoPhase(c, AxisRef.A))
            {
                Console.WriteLine("already commutated");
                return 0;
            }
            // 立即返回，换相在控制器中进行
            AAMotionAPI.AutoPhase(c, AxisRef.A);
            var phaseTime = Stopwatch.StartNew();
            while (!AAMotionAPI.IsAutoPhase(c, AxisRef.A))
            {
                if (phaseTime.ElapsedMilliseconds >= 10000)
                {
                    // ComtStatus[1] 说明换相为什么没有完成
                    Console.WriteLine(
                        "timed out; ComtStatus[1]=" + a.ComtStatus[1]);
                    return 6;
                }
                Thread.Sleep(50);
            }
            Console.WriteLine(
                $"commutated in {phaseTime.ElapsedMilliseconds} ms");
            if (a.MotorOn == 1)
            {
                // 换相后可能保持使能，示例结束前断使能
                AAMotionAPI.MotorOff(c, AxisRef.A);
                Console.WriteLine("motor off");
            }
            // --8<-- [end:operation]
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
            // 3. 断开：AACommServer 继续为其他程序服务
            AAMotionAPI.Disconnect(c);
            Console.WriteLine("done");
            // --8<-- [end:cleanup]
        }
    }
}
