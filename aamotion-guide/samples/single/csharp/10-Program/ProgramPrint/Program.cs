using System;
using System.IO;
using AAMotion;
using System.Threading;
// 接收程序 printf 输出 / Receive program printf lines
// 顺序：连接 → 本次操作 → 断开。轴不运动。
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        string connectionTarget = args.Length == 0 ? Target : args[0];
        Console.WriteLine("Runs task 1 on thread 1 for 5 seconds.");
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
            // 回调在后台线程上执行；Console 输出是线程安全的
            Action<UserProgramPrintMessage> onPrint = m =>
                Console.WriteLine(
                    $"[PRINT] thread {m.Thread}: {m.DisplayText}");
            AAMotionAPI.SetUserProgramPrintHandler(c, onPrint);
            try
            {
                AAMotionAPI.ProgRun(c, AxisRef.A, 1, 1);
                Console.WriteLine("thread 1 runs for 5 s; printf lines follow");
                Thread.Sleep(5000);
                AAMotionAPI.ProgHalt(c, AxisRef.A, 1);
            }
            finally
            {
                // 取消时传同一个委托实例
                AAMotionAPI.ClearUserProgramPrintHandler(c, onPrint);
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
