using System;
using System.IO;
using AAMotion;
using System.Diagnostics;
using System.Threading;
// 螺旋找光 / Spiral light finding
// 顺序：连接 → 本次操作 → 断开。轴不运动。
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        string connectionTarget = args.Length == 0 ? Target : args[0];
        Console.WriteLine("Starts a spiral scan; the coupling axes move.");
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
            OpticalCouplingLightFinding oc =
                AAMotionAPI.CreateOpticalCouplingLightFinding(c);
            oc.ApplySpiralConfig(new SpiralConfig
            {
                NumOfAInPort = 1,   // 光功率接在模拟输入 1
                NumOfXAxis = 0,     // 找光轴，按算法的编号（0、1、2）
                NumOfYAxis = 1,
                SpiralPitch = 50, SpiralMaxRadius = 2000,
                SpiralAngleCoeff = 10, SpiralScanSpeed = 2000,
                SpiralRtnSpeed = 5000, PowerTh = 1000,
            });
            c.WriteGenData(602, 0);   // 清除上次结果
            oc.StartSpiralScan();
            var scanTime = Stopwatch.StartNew();
            while (oc.CouplingResultCode == 0)   // 0：还在扫描
            {
                if (scanTime.ElapsedMilliseconds >= 60000)
                {
                    oc.EmergencyStop();
                    Console.WriteLine("scan timeout; emergency stop requested");
                    return 6;
                }
                Thread.Sleep(100);
            }
            SpiralScanResult r = oc.GetSpiralScanResult();
            if (!r.IsSuccess)
            {
                Console.WriteLine(
                    $"failed: {r.CouplingResult}, error {r.ErrCode}");
                return 6;
            }
            Console.WriteLine($"max power {r.MaxPower} at " +
                $"({r.MaxPowerXPos}, {r.MaxPowerYPos})");
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
