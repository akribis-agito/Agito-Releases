using System;
using System.IO;
using AAMotion;
// 读取扫描曲线 / Read the scan curves
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
            OpticalCouplingLightFinding oc =
                AAMotionAPI.CreateOpticalCouplingLightFinding(c);
            int n = oc.LastSampleIndex;   // 上一次扫描的点数
            if (n <= 0)
            {
                Console.WriteLine("no scan data; run SpiralScan first");
                return 1;
            }
            oc.SyncAllGenData();   // 一次读出 GenData，存到本地副本
            // 螺旋数据区：X 1000–3499，Y 3500–5999，通道 1 光功率 6000–8499
            int[] x = oc.SpiralXPosArray(1000, 1000 + n);
            int[] y = oc.SpiralYPosArray(3500, 3500 + n);
            int[] p = oc.Channel1PowerArray(6000, 6000 + n);
            int best = 0;
            for (int i = 1; i < p.Length; i++)
                if (p[i] > p[best]) best = i;
            Console.WriteLine($"{p.Length} samples; peak {p[best]} " +
                $"at ({x[best]}, {y[best]})");
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
