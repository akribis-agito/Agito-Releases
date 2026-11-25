using System;
using System.IO;
using AAMotion;
// GenData 与 UserParam / GenData and UserParam
// 顺序：连接 → 本次操作 → 断开。轴不运动。
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        string connectionTarget = args.Length == 0 ? Target : args[0];
        Console.WriteLine("Writes AGenData[10] and AUserParam[1].");
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
            int savedData = AAMotionAPI.ReadGenData(c, 10);    // 下标从 1 开始
            int savedParam = AAMotionAPI.ReadUserParam(c, 1);
            try
            {
                AAMotionAPI.WriteGenData(c, 10, 1234);
                AAMotionAPI.WriteUserParam(c, 1, 42);
                int data = AAMotionAPI.ReadGenData(c, 10);
                int param = AAMotionAPI.ReadUserParam(c, 1);
                Console.WriteLine($"AGenData[10] {savedData} -> {data}");
                Console.WriteLine($"AUserParam[1] {savedParam} -> {param}");
            }
            finally
            {
                // 改回原值：控制器上的用户程序可能在使用这些下标
                AAMotionAPI.WriteGenData(c, 10, savedData);
                AAMotionAPI.WriteUserParam(c, 1, savedParam);
                Console.WriteLine("originals restored");
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
