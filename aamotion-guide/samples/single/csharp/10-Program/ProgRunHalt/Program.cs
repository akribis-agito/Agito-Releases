using System;
using System.IO;
using AAMotion;
using System.Threading;
// 运行、暂停与复位线程 / Run, halt and reset a thread
// 顺序：连接 → 本次操作 → 断开。轴不运动。
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        string connectionTarget = args.Length == 0 ? Target : args[0];
        Console.WriteLine("Runs task 1 on thread 1, then halts and resets it.");
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
            // 线程 1 从头运行任务 1（任务号是程序中的任务标签）
            bool run = AAMotionAPI.ProgRun(c, AxisRef.A, 1, 1);
            Console.WriteLine("ProgRun: " + run);
            Thread.Sleep(1000);
            // ProgStat[n]：1 运行中，0 未运行，-1 没有程序
            Console.WriteLine("ProgStat[1]=" + c.ProgStat[1]);
            AAMotionAPI.ProgHalt(c, AxisRef.A, 1);
            Console.WriteLine("halted: ProgStat[1]=" + c.ProgStat[1]);
            AAMotionAPI.ProgReset(c, AxisRef.A, 1);   // 下次从头运行
            Console.WriteLine("reset: ProgError[1]=" + c.ProgError[1]);
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
