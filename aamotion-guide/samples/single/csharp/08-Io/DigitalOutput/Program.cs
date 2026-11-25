using System;
using System.IO;
using AAMotion;
using System.Threading;
// 置位与清零输出 / Set and clear an output
// 顺序：连接 → 本次操作 → 断开。轴不运动。
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        string connectionTarget = args.Length == 0 ? Target : args[0];
        Console.WriteLine(
            "Sets and clears output 1 of axis A, then restores it.");
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
            IIOModule io = c.GetIO(AxisRef.A);
            // 写输出的编号从 1 开始；读位的编号从 0 开始：输出 1 是位 0
            int before = io.GetDOutPort(0);
            io.DOutSetBit(1);
            Console.WriteLine($"output 1: {before} -> {io.GetDOutPort(0)}");
            Thread.Sleep(500);
            io.DOutClearBit(1);
            Console.WriteLine("output 1 cleared: " + io.GetDOutPort(0));
            if (before == 1) io.DOutSetBit(1);   // 改回原状态
            Console.WriteLine("restored: " + io.GetDOutPort(0));
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
