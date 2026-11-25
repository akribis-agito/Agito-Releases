using System;
using System.IO;
using AAMotion;
using System.Diagnostics;
using System.Threading;
// 快速读缓存 / Fast-read cache
// 顺序：连接 → 本次操作 → 断开。轴不运动。
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        string connectionTarget = args.Length == 0 ? Target : args[0];
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
            AAMotionConfig.FastReadEnabled = true;   // 属性读取可以走缓存
            try
            {
                AAMotionAPI.RegisterCacheKeywords(c, AxisRef.A,
                    eKeyword.Pos, eKeyword.MotionStat);
                AAMotionAPI.EnableFastReadCache(c, 5);   // 后台每 5 ms 刷新
                Thread.Sleep(100);
                var readTime = Stopwatch.StartNew();
                double pos = 0;
                for (int i = 0; i < 1000; i++)
                    pos = a.Pos;                         // 从缓存读，不通信
                long ms = readTime.ElapsedMilliseconds;
                Console.WriteLine($"1000 reads in {ms} ms, Pos={pos}");
                double age = AAMotionAPI.GetCacheAgeMs(c, AxisRef.A,
                    eKeyword.Pos);
                Console.WriteLine($"value age {age:F1} ms");
            }
            finally
            {
                AAMotionAPI.DisableFastReadCache(c);
                AAMotionConfig.FastReadEnabled = false;
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
