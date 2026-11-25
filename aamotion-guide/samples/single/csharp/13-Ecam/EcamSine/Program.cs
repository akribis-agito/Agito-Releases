using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AAMotion;
// 正弦凸轮跟随 / Follow a sine cam
// 顺序：连接 → 换相 → 使能（A、B 两轴）→ 本次操作 → 确认结果 → 清理与断开
internal static class Program
{
    // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
    private const string Target = "172.1.1.101";
    private static readonly AxisRef[] Axes = { AxisRef.A, AxisRef.B };

    private static int Main(string[] args)
    {
        Console.WriteLine("Axes A and B will move. Check travel and units.");
        Console.WriteLine("Type RUN to continue:");
        if (Console.ReadLine() != "RUN") return 1;
        string connectionTarget = args.Length == 0 ? Target : args[0];
        MotionController c = AAMotionAPI.Initialize(ControllerType.AGM800);
        c.ErrorOccurred += (code, command, message) =>
            Console.WriteLine($"{code}: {command} | {message}");
        bool motorOnRequested = false;
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
            ISingleAxis a = c.GetAxis(AxisRef.A);
            ISingleAxis b = c.GetAxis(AxisRef.B);
            // --8<-- [end:connect]

            // --8<-- [start:prepare]
            // 2. 换相：A、B 两轴都要先换相，等待有上限
            foreach (AxisRef axis in Axes)
            {
                if (simulation)
                {
                    c.GetAxis(axis).MotorType = 5;
                    continue;
                }
                if (AAMotionAPI.IsAutoPhase(c, axis)) continue;
                Console.WriteLine(axis + " requested");
                AAMotionAPI.AutoPhase(c, axis);
                var phaseTime = Stopwatch.StartNew();
                while (!AAMotionAPI.IsAutoPhase(c, axis))
                {
                    if (phaseTime.ElapsedMilliseconds >= 10000)
                    {
                        Console.WriteLine(axis + " timed out");
                        return 3;
                    }
                    Thread.Sleep(20);
                }
            }
            if (!simulation) Console.WriteLine("A and B commutated");

            // 3. 使能：清理阶段也会处理结果不确定的使能
            motorOnRequested = true;
            foreach (AxisRef axis in Axes)
            {
                AAMotionAPI.MotorOn(c, axis);
                int on = c.GetAxis(axis).MotorOn;
                if (on != 1)
                {
                    Console.WriteLine($"{axis} refused; MotorOn={on}");
                    return 4;
                }
            }
            Console.WriteLine("A and B on");
            // --8<-- [end:prepare]

            // --8<-- [start:operation]
            // 4. 本次操作
            // 主轴：A 轴的参考位置 PosRef；编号随产品和固件不同，运行时读取
            int posRef = c.GetKeywordCANCode(eKeyword.PosRef);
            // 360 点正弦表写入 GenData[100..459]，幅值 2000 计数，从 0 开始
            AAMotionAPI.ECAM_CreateSinusoidalTable(c, AxisRef.B, 2000, 0,
                360, 100);
            // 主轴每走 10 计数对应表中一个点；一直循环
            AAMotionAPI.ECAM_ConfigureTable(c, AxisRef.B, 100, 459, 100, 459,
                int.MaxValue, posRef, 10);
            if (!AAMotionAPI.ECAM_Start(c, AxisRef.B))
            {
                Console.WriteLine("ECAM_Start refused; see the ERR line");
                return 6;
            }
            AAMotionAPI.Jog(c, AxisRef.A, 2000);   // 主轴运动，从轴跟随
            Thread.Sleep(3000);
            AAMotionAPI.Stop(c, AxisRef.A);
            int cycles = AAMotionAPI.ECAM_GetCycleCount(c, AxisRef.B);
            Console.WriteLine($"master stopped; cycle count {cycles}");
            AAMotionAPI.ECAM_Stop(c, AxisRef.B);
            // --8<-- [end:operation]

            // --8<-- [start:observe]
            // 5. 确认结果：方法返回不等于动作完成
            bool active = AAMotionAPI.ECAM_IsActive(c, AxisRef.B);
            Console.WriteLine($"ECAM active: {active}, A={a.Pos}, B={b.Pos}");
            // --8<-- [end:observe]
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
            // 6. 清理：先确认停止再断使能
            //    断开通信不能证明设备已停止
            try
            {
                if (motorOnRequested)
                {
                    foreach (AxisRef axis in Axes)
                        AAMotionAPI.Stop(c, axis);
                    var stopTime = Stopwatch.StartNew();
                    ISingleAxis sa = c.GetAxis(AxisRef.A);
                    ISingleAxis sb = c.GetAxis(AxisRef.B);
                    Func<bool> moving = () =>
                        sa.MotionStat != 0 || sb.MotionStat != 0;
                    while (moving() && stopTime.ElapsedMilliseconds < 5000)
                        Thread.Sleep(20);
                    if (!moving())
                    {
                        foreach (AxisRef axis in Axes)
                            AAMotionAPI.MotorOff(c, axis);
                        Console.WriteLine("stopped, motors off");
                    }
                    else
                        Console.WriteLine("stop unconfirmed; check both axes");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("failed, verify axis state: " + ex.Message);
            }
            finally
            {
                AAMotionAPI.Disconnect(c);
            }
            // --8<-- [end:cleanup]
        }
    }
}
