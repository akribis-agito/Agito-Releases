// Cookbook recipes. Each region is one complete recipe shown in the manual.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AAComm.Services;
using AAMotion;

namespace ApiSamples
{
    public static class Cookbook
    {
        // --8<-- [start:power-up]
        /// <summary>From power-up to the first move, with a check at every step.</summary>
        public static bool PowerUpAndMove(string ip)
        {
            LocaleProvider.Instance.Locale = "zh-CN";
            MotionController c = AAMotionAPI.Initialize(ControllerType.AGM800);
            c.ErrorOccurred += (code, sent, text) => Console.WriteLine($"[ERR {code}] {sent}: {text.Replace("\n", " | ")}");

            if (!AAMotionAPI.Connect(c, ip))
                return Fail("connect", c);
            try
            {
                ISingleAxis a = c.GetAxis(AxisRef.A);

                // 1. a fault left from before? ConFlt says which, MotorReason why the motor went off
                if (a.ConFlt != 0)
                    Console.WriteLine("last fault: " + EnumPropertyHelper.GetConFltDes(a.ConFlt));

                // 2. commutation before motor-on, including the simulator
                if (!AAMotionAPI.IsAutoPhase(c, AxisRef.A))
                {
                    AAMotionAPI.AutoPhase(c, AxisRef.A);
                    if (!WaitUntil(() => AAMotionAPI.IsAutoPhase(c, AxisRef.A), 10000))
                        return Fail("commutation", c);
                }

                // 3. enable only after successful commutation
                AAMotionAPI.MotorOn(c, AxisRef.A);

                // 4. homing with the sequence configured in PCSuite
                AAMotionAPI.Home(c, AxisRef.A);
                if (!WaitUntil(() => AAMotionAPI.IsHomed(c, AxisRef.A) || c.GetValue(AxisRef.A, eKeyword.HomingStat) < 0, 30000)
                    || !AAMotionAPI.IsHomed(c, AxisRef.A))
                    return Fail("homing", c);

                // 5. the first move, waiting for in-target
                if (!AAMotionAPI.MoveAbsAndWait(c, AxisRef.A, 10000, speed: 20000, accel: 200000, decel: 200000, timeout: 5000))
                    return Fail("move", c);

                Console.WriteLine($"done: APos = {a.Pos}");
                return true;
            }
            catch (ControllerCommandException ex)
            {
                Console.WriteLine($"{ex.Command} refused with ERR {ex.ErrorCode}");
                return false;
            }
            finally
            {
                AAMotionAPI.Disconnect(c);
            }
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (condition()) return true;
                Thread.Sleep(50);
            }
            return false;
        }

        private static bool Fail(string step, MotionController c)
        {
            Console.WriteLine($"step '{step}' failed; MotionReason = {c.GetValue(AxisRef.A, eKeyword.MotionReason)}");
            return false;
        }
        // --8<-- [end:power-up]

        // --8<-- [start:burn-in]
        /// <summary>Reciprocating motion for a set time, stopping at the first fault.</summary>
        public static void BurnIn(MotionController c, TimeSpan duration)
        {
            ISingleAxis a = c.GetAxis(AxisRef.A);
            AAMotionAPI.MoveAbsRepetitive(c, AxisRef.A, 200000, 100, speed: 100000, accel: 2000000, decel: 2000000);
            DateTime end = DateTime.UtcNow + duration;
            double worstPosErr = 0;
            using (var log = new StreamWriter("burn-in.csv"))
            {
                log.WriteLine("time,cycles,pos_err,motion_reason");
                while (DateTime.UtcNow < end)
                {
                    Thread.Sleep(1000);
                    double err = Math.Abs(a.PosErr);
                    worstPosErr = Math.Max(worstPosErr, err);
                    int reason = (int)c.GetValue(AxisRef.A, eKeyword.MotionReason);
                    log.WriteLine($"{DateTime.Now:O},{a.RptCounter},{err},{reason}");
                    if (a.MotorOn == 0 || a.MotionStat == 0)       // the motion ended by itself: a fault
                    {
                        Console.WriteLine("stopped: " + EnumPropertyHelper.GetMotionReasonDes(reason));
                        return;
                    }
                }
            }
            AAMotionAPI.StopRep(c, AxisRef.A);
            Console.WriteLine($"finished {a.RptCounter} cycles, worst position error {worstPosErr}");
        }
        // --8<-- [end:burn-in]

        // --8<-- [start:gantry]
        /// <summary>Start two independent axes together, then wait until both have settled.</summary>
        public static bool MovePair(MotionController c, double x, double y)
        {
            ISingleAxis ax = c.GetAxis(AxisRef.A), ay = c.GetAxis(AxisRef.B);
            if (!AAMotionAPI.MoveAbs(c, 500000, 50000, 500000, ax, x, ay, y))
                return false;                                    // inspect error reports and actual state of both axes
            DateTime end = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < end)
            {
                if (ax.InTargetStat == 4 && ay.InTargetStat == 4)
                    return true;
                Thread.Sleep(10);
            }
            return false;
        }
        // --8<-- [end:gantry]

        // --8<-- [start:capture]
        /// <summary>Record a move with a trigger on its start and save it as CSV.</summary>
        public static string CaptureMove(MotionController c, double target)
        {
            var cfg = new MotionController.DataLoggerConfiguration
            {
                DurationSec = 1.0,
                GapMs = 0.5,
                TriggerPosition = 5,
                Trigger = new MotionController.DataLoggerTrigger { Type = 5, Source = "AMotionStat", Value = 0.5 },
            };
            foreach (string p in new[] { "APosRef", "APos", "APosErr", "AVel[1]" })
                cfg.ParamsToRecord.Add(p);

            Task<string> recording = Task.Run(() => c.RecordData(cfg, out MotionController.DataLoggerResult r, false, 5000) is string e && e.Length == 0
                ? MotionController.GenerateCsvFile(r, Path.GetTempPath())
                : null);
            Thread.Sleep(100);                                         // let the recorder arm
            AAMotionAPI.MoveAbs(c, AxisRef.A, target, 50000, 500000, 500000);
            return recording.Result;                                   // the CSV path, or null
        }
        // --8<-- [end:capture]



        // --8<-- [start:multi]
        /// <summary>Two controllers, one program: one object per controller, both through the same server.</summary>
        public static void TwoControllers()
        {
            var left = AAMotionAPI.Initialize(ControllerType.AGM800);
            var right = AAMotionAPI.Initialize(ControllerType.AGD301);
            var connected = new[] { Task.Run(() => AAMotionAPI.Connect(left, "172.1.1.101")),
                                    Task.Run(() => AAMotionAPI.Connect(right, "172.1.1.102")) };
            Task.WaitAll(connected);
            if (!connected.All(t => t.Result))
                return;
            try
            {
                Task.WaitAll(
                    Task.Run(() => AAMotionAPI.MoveAbsAndWait(left, AxisRef.A, 50000, 20000, timeout: 10000)),
                    Task.Run(() => AAMotionAPI.MoveAbsAndWait(right, AxisRef.A, 30000, 20000, timeout: 10000)));
            }
            finally
            {
                AAMotionAPI.Disconnect(left);
                AAMotionAPI.Disconnect(right);
            }
        }
        // --8<-- [end:multi]

        // --8<-- [start:fast]
        /// <summary>Read a status snapshot of four axes in one round trip instead of sixteen.</summary>
        public static Dictionary<string, string> Snapshot(MotionController c)
        {
            string[] names = { "APos", "AVel[1]", "AMotionStat", "AStatReg",
                               "BPos", "BVel[1]", "BMotionStat", "BStatReg",
                               "CPos", "CVel[1]", "CMotionStat", "CStatReg",
                               "DPos", "DVel[1]", "DMotionStat", "DStatReg" };
            var values = new Dictionary<string, string>();
            if (c.SendBulkCommandString(string.Join(";", names), out string reply))
                for (int i = 0; i < names.Length; i++)
                    values[names[i]] = c.BulkCommandParser(i + 1, reply);
            return values;
        }
        // --8<-- [end:fast]
    }
}

