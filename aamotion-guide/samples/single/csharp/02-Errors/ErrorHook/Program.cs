using System;
using System.IO;
using AAMotion;
using AAComm.Services;
// 错误钩子 / Error hook
// 顺序：连接 → 本次操作 → 断开。轴不运动。
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";

    private static int Main(string[] args)
    {
        string connectionTarget = args.Length == 0 ? Target : args[0];
        MotionController c = AAMotionAPI.Initialize(ControllerType.AGM800);
        // --8<-- [start:hook]
        // 连接前注册：连接过程中的错误也会报告
        LocaleProvider.Instance.Locale = "en-US";   // "zh-CN" 输出中文说明
        Action<int, string, string> onError = (code, sent, text) =>
            Console.WriteLine(
                $"[ERR] {code} {sent} | {text.Replace("\n", " | ")}");
        AAMotionAPI.SetErrorOccurredHandler(c, onError);
        // --8<-- [end:hook]
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
            if (a.MotorOn != 0)
            {
                Console.WriteLine("axis A is enabled; nothing sent");
                return 1;
            }
            // 断使能时发送绝对运动：控制器拒绝，钩子打印错误
            try
            {
                a.MoveAbs(1000, speed: 2000, accel: 10000, decel: 10000);
                Console.WriteLine("MoveAbs returned; see the ERR line above");
            }
            catch (ControllerCommandException ex)
            {
                Console.WriteLine($"MoveAbs refused: ERR {ex.ErrorCode}");
            }
            // 取消时传同一个委托实例
            AAMotionAPI.ClearErrorOccurredHandler(c, onError);
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
