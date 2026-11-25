using System;
using System.IO;
using AAMotion;
using System.Collections.Generic;
// 立即记录并上传 / Record now and upload
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
            string err = c.GetDataRecordingCapacity(out int maxVectors,
                out int maxArraySize);
            Console.WriteLine($"capacity: {maxVectors} parameters, " +
                $"{maxArraySize} samples in total");
            var cfg = new MotionController.DataLoggerConfiguration
            {
                DurationSec = 1, GapMs = 1,   // 没有 Trigger：立即开始
            };
            cfg.ParamsToRecord.Add("APos");
            cfg.ParamsToRecord.Add("AVel[1]");   // 数组关键字要带下标
            err = c.ConfigureDataLogger(cfg);    // 只配置，不开始
            if (string.IsNullOrEmpty(err)) err = c.StartDataRecording();
            if (string.IsNullOrEmpty(err))
                err = c.WaitForDataRecordingToEnd(3000);
            MotionController.DataLoggerResult r = null;
            if (string.IsNullOrEmpty(err)) err = c.UploadDataLoggerData(out r);
            if (!string.IsNullOrEmpty(err))
            {
                Console.WriteLine("recording failed: " + err);
                return 6;
            }
            foreach (MotionController.DataLoggerVector v in r.Vectors)
            {
                List<double> x = v.Values;
                string range = x.Count > 0 ?
                    $"{x[0]} .. {x[x.Count - 1]}" : "-";
                Console.WriteLine($"{v.Parameter}: {x.Count} samples, {range}");
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
