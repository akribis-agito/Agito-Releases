using System;
using System.IO;
using AAMotion;
using System.Diagnostics;
using System.Threading;
// 和用户程序交换数据 / Exchange data with the program
// 顺序：连接 → 本次操作 → 断开。轴不运动。
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        string connectionTarget = args.Length == 0 ? Target : args[0];
        Console.WriteLine("Writes AGenData[1] and [2] for the handshake.");
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
            // 约定：GenData[1] 参数，GenData[2] 开始标志，GenData[3] 结果
            AAMotionAPI.WriteGenData(c, 1, 7);
            AAMotionAPI.WriteGenData(c, 2, 1);   // 程序看到 1 后开始执行
            Console.WriteLine("input 7 written, start flag set");
            var waitTime = Stopwatch.StartNew();
            while (AAMotionAPI.ReadGenData(c, 2) != 0)   // 程序完成后清零
            {
                if (waitTime.ElapsedMilliseconds >= 5000)
                {
                    AAMotionAPI.WriteGenData(c, 2, 0);   // 撤回请求
                    Console.WriteLine(
                        "no answer in 5 s; is the program running?");
                    return 6;
                }
                Thread.Sleep(50);
            }
            Console.WriteLine(
                "result GenData[3]=" + AAMotionAPI.ReadGenData(c, 3));
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
