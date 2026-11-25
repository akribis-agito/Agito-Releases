// 02-first-move: connect, motor on, commutate if needed, move axis A by a relative distance and wait in target,
// then motor off and disconnect. Every step prints what it did, so the output shows how far it got.
//   02-FirstMove.exe "D:\sim\AGM800\firmware_sim.exe"      firmware simulator (absolute path)
//   02-FirstMove.exe 172.1.1.101                           real controller: make sure axis A may move 10000
using System;
using System.Threading;
using AAComm.Services;
using AAMotion;

namespace Sample02
{
    internal static class Program
    {
        private const double Distance = 10000, Speed = 20000, Accel = 200000;

        private static int Main(string[] args)
        {
            string target = args.Length > 0 ? args[0] : "172.1.1.101";
            LocaleProvider.Instance.Locale = "zh-CN";

            // --8<-- [start:connect]
            MotionController c = AAMotionAPI.Initialize(ControllerType.AGM800);
            c.ErrorOccurred += (code, sent, text) => Console.WriteLine($"[ERR {code}] {sent} | {text.Replace("\n", " | ")}");
            bool ok;
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                c.Init();
                ok = AAMotionAPI.ConnectSimulation(c, target);
            }
            else
            {
                ok = AAMotionAPI.Connect(c, target);
            }
            Console.WriteLine($"1. connect {target}: {ok}");
            if (!ok)
            {
                AAMotionAPI.Disconnect(c);
                return 1;
            }
            // --8<-- [end:connect]

            ISingleAxis a = c.GetAxis(AxisRef.A);
            try
            {
                // --8<-- [start:enable]
                if (!AAMotionAPI.IsAutoPhase(c, AxisRef.A))
                {
                    AAMotionAPI.AutoPhase(c, AxisRef.A);
                    DateTime end = DateTime.UtcNow.AddSeconds(10);
                    while (!AAMotionAPI.IsAutoPhase(c, AxisRef.A))
                    {
                        if (DateTime.UtcNow > end)
                        {
                            Console.WriteLine($"2. commutation timed out: ComtStatus[1] = {a.ComtStatus[1]}");
                            return 3;
                        }
                        Thread.Sleep(100);
                    }
                }
                Console.WriteLine("2. commutated");
                AAMotionAPI.MotorOn(c, AxisRef.A);
                Console.WriteLine($"3. motor on: MotorOn = {a.MotorOn}");
                // --8<-- [end:enable]

                // --8<-- [start:move]
                double start = a.Pos;
                bool arrived = AAMotionAPI.MoveAbsAndWait(c, AxisRef.A, start + Distance, Speed, Accel, Accel, timeout: 10000);
                Console.WriteLine($"4. move {start} -> {start + Distance}: in target = {arrived}, Pos = {a.Pos}");
                if (!arrived)
                {
                    AAMotionAPI.Stop(c, AxisRef.A);
                    int reason = (int)c.GetValue(AxisRef.A, eKeyword.MotionReason);
                    Console.WriteLine($"   stopped; MotionReason = {reason} {EnumPropertyHelper.GetMotionReasonDes(reason)}");
                    return 4;
                }
                // --8<-- [end:move]
                return 0;
            }
            catch (ControllerCommandException ex)
            {
                Console.WriteLine($"refused: {ex.Command} ERR {ex.ErrorCode}");
                return 5;
            }
            finally
            {
                // --8<-- [start:cleanup]
                AAMotionAPI.Stop(c, AxisRef.A);       // harmless when already still
                AAMotionAPI.MotorOff(c, AxisRef.A);
                AAMotionAPI.Disconnect(c);            // also releases the controller object
                Console.WriteLine("5. motor off, disconnected");
                // --8<-- [end:cleanup]
            }
        }
    }
}

