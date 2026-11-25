// Examples for the "Homing" and "Advanced motion" chapters.
using System;
using System.Threading;
using AAMotion;

namespace ApiSamples
{
    public static class Homing
    {
        public static bool WaitHomed(MotionController c, AxisRef axis, int timeoutMs)
        {
            // --8<-- [start:wait-homed]
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (AAMotionAPI.IsHomed(c, axis))
                    return true;                                   // HomingStat == 100
                int stat = (int)c.GetValue(axis, eKeyword.HomingStat);
                if (stat < 0)
                {
                    Console.WriteLine("homing failed: " + EnumPropertyHelper.GetHomingStatDes(stat));
                    return false;
                }
                Thread.Sleep(100);
            }
            return false;
            // --8<-- [end:wait-homed]
        }

        public static void Home(MotionController c)
        {
            // --8<-- [start:home]
            // the homing sequence already configured in the controller (PCSuite homing tool)
            AAMotionAPI.Home(c, AxisRef.A);
            bool ok = WaitHomed(c, AxisRef.A, 30000);
            // --8<-- [end:home]
        }

        public static void HomeOptions(MotionController c)
        {
            // --8<-- [start:home-options]
            // limit switch first, then the index; offset 1000 from the index becomes 0
            AAMotionAPI.Home(c, AxisRef.A,
                isHomeToLimit: true, isHomeToIndex: true,
                velocity: 5000, acceleration: 100000, emergencydeceleration: 500000,
                homeoffset: 1000, timeOut: 20000);
            bool ok = WaitHomed(c, AxisRef.A, 25000);
            // --8<-- [end:home-options]
        }

        public static void HardStop(MotionController c)
        {
            // --8<-- [start:hard-stop]
            // move slowly into the mechanical end; a position error above 2000 marks the stop
            AAMotionAPI.HomeToHardStop(c, AxisRef.A, threshold: 2000, velocity: 2000,
                acceleration: 50000, emergencydeceleration: 500000, homeoffset: 500, timeOut: 20000);
            bool ok = WaitHomed(c, AxisRef.A, 25000);
            // --8<-- [end:hard-stop]
        }

        public static void Index(MotionController c)
        {
            // --8<-- [start:index]
            if (AAMotionAPI.LockSrcZeroSupported(c, AxisRef.A))
            {
                // the main encoder index, searching forward at 2000 per second
                AAMotionAPI.HomeToIndex(c, AxisRef.A, lockSource: 0, direction: 1, slowSpeed: 2000, accel: 50000);
            }
            AAMotionAPI.HomeToLimitThenIndex(c, AxisRef.B, lockSource: 0, direction: -1,
                fastSpeed: 10000, slowSpeed: 1000, accel: 100000, timeoutMs: 30000);
            // --8<-- [end:index]
        }

        public static void StuckThenIndex(MotionController c)
        {
            // --8<-- [start:stuck]
            // stuck: below 500 counts/s with more than 800 mA for 50 ms; controller units, never User Units
            AAMotionAPI.HomeToHardStopByStuckThenIndex(c, AxisRef.A, lockSource: 1, direction: -1,
                stuckVel: 500, stuckCurr: 800, stuckTimeMs: 50,
                fastSpeed: 5000, slowSpeed: 1000, accel: 50000, timeoutMs: 20000);
            bool ok = WaitHomed(c, AxisRef.A, 60000);
            // --8<-- [end:stuck]
        }

        public static void FromFile(MotionController c)
        {
            // --8<-- [start:from-file]
            // homing.txt: one HomingDef element per line ("HomingDef[1]=1", "HomingDef[2]=5000", ...); "//" starts a comment
            AAMotionAPI.Home(c, AxisRef.A, @"C:\MyApp\homing.txt");
            // --8<-- [end:from-file]
        }
    }

    public static class AdvancedMotion
    {
        public static void Repetitive(MotionController c)
        {
            // --8<-- [start:repetitive]
            // between the current position and 50000, waiting 200 ms at each end, forever
            AAMotionAPI.MoveAbsRepetitive(c, AxisRef.A, 50000, 200, speed: 20000);
            Thread.Sleep(10000);
            AAMotionAPI.StopRep(c, AxisRef.A);                  // finishes the stroke in progress

            // 100 round trips, then stop by itself
            AAMotionAPI.MoveAbsRepetitiveCycle(c, AxisRef.A, 50000, 200, 100, speed: 20000);
            // --8<-- [end:repetitive]
        }

        public static void TwoSpeed(MotionController c)
        {
            // --8<-- [start:two-speed]
            // fast to 80000, then slow for the last part of the way to 100000
            AAMotionAPI.MoveAbsTwoSpeed(c, AxisRef.A, 100000, speed1: 50000, speed2: 5000, speedChangePosition: 80000);

            // relative form that waits for in-target
            bool ok = AAMotionAPI.MoveRelTwoSpeedAndWait(c, AxisRef.A, -100000, 50000, 5000, 80000, timeout: 30000);
            // --8<-- [end:two-speed]
        }

        public static void SinePtp(MotionController c)
        {
            // --8<-- [start:sine-ptp]
            var move = new AAMotionAPI.SinePtpMotionConfig
            {
                MotionMode = 20,          // Sine PTP
                RelTrgt = 0,
                AbsTrgt = 100000,
                Speed = 50000,
                Accel = 500000,
                Decel = 500000,
                JerkInAcc = 5000000,
                JerkInDec = 5000000,
            };
            if (!AAMotionAPI.SinePtpBegin(c, AxisRef.A, move))
                Console.WriteLine("refused: see the error hook");

            // change the target while it moves
            move.AbsTrgt = 120000;
            AAMotionAPI.SinePtpCommit(c, AxisRef.A, move);
            // --8<-- [end:sine-ptp]
        }

        public static void Commit(MotionController c)
        {
            // --8<-- [start:commit]
            // write new Sine PTP targets to A and B first, then apply both in the same control cycle
            c.GetAxis(AxisRef.A).AbsTrgt = 150000;
            c.GetAxis(AxisRef.B).AbsTrgt = 80000;
            bool ok = AAMotionAPI.CommitMotion(c, AxisRef.A | AxisRef.B);
            // --8<-- [end:commit]
        }

        public static void Pvaj(MotionController c, double[][] points)
        {
            // --8<-- [start:pvaj]
            // points[k - 1] = { position, velocity, acceleration, jerk } in counts, counts/s, /s², /s³
            if (!AAMotionAPI.PvajSupported(c))
                return;                         // needs an AGM800 with firmware 5.3.0 or later
            AAMotionAPI.PvajArm(c, AxisRef.A, false);
            AAMotionAPI.PvajWriteHeader(c, AxisRef.A, points.Length, 1, PvajPositionMode.Absolute);
            for (int k = 1; k <= points.Length; k++)
            {
                double[] p = points[k - 1];
                AAMotionAPI.PvajWritePoint(c, AxisRef.A, k, p[0], p[1], p[2], p[3]);
            }
            if (!AAMotionAPI.PvajValidate(c, AxisRef.A, PvajValidationMode.Full)
                || !AAMotionAPI.PvajArm(c, AxisRef.A)
                || !AAMotionAPI.PvajBegin(c, AxisRef.A))
                return;                         // the reason is on ErrorOccurred

            for (int attempt = 0; attempt < 300; attempt++)
            {
                PvajState state = AAMotionAPI.PvajGetStatus(c, AxisRef.A, out int index, out int left);
                if (state != PvajState.Executing && left == 0)
                    break;                      // ended: check the position and MotionReason
                Thread.Sleep(100);
            }
            // --8<-- [end:pvaj]
        }

        public static void Gearing(MotionController c)
        {
            // --8<-- [start:gearing]
            // B follows A's position command at 1:2 (32768 / 65536), direct gearing
            var gear = new AAMotionAPI.GearMotionConfig
            {
                Master = "APosRef",
                RatioNumerator = 32768,
                RatioDenominator = 65536,
                MotionMode = (int)MotionMode.GearDirect,
            };
            try
            {
                if (!AAMotionAPI.GearBegin(c, AxisRef.B, gear))
                    Console.WriteLine("Begin refused: see the error hook");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("not written: " + ex.Message);   // the controller cannot take it
            }
            catch (ControllerCommandException ex)
            {
                Console.WriteLine("write refused, earlier writes stay: " + ex.Command);
            }
            // ... later
            AAMotionAPI.Stop(c, AxisRef.B);
            // --8<-- [end:gearing]
        }
    }
}
