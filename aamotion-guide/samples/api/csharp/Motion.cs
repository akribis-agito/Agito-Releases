// Examples for the "Motor, commutation and stop" and "Point-to-point motion" chapters.
using System;
using System.Threading;
using AAMotion;

namespace ApiSamples
{
    public static class Motion
    {
        public static void MotorOnOff(MotionController c)
        {
            // --8<-- [start:motor-on]
            if (!AAMotionAPI.IsAutoPhase(c, AxisRef.A))
            {
                Console.WriteLine("commutate successfully before motor-on");
                return;
            }
            AAMotionAPI.MotorOn(c, AxisRef.A);          // same as c.GetAxis(AxisRef.A).MotorOn = 1
            bool on = c.GetAxis(AxisRef.A).MotorOn == 1;
            // ...
            AAMotionAPI.MotorOff(c, AxisRef.A);
            // --8<-- [end:motor-on]
        }

        public static bool AutoPhase(MotionController c)
        {
            // --8<-- [start:auto-phase]
            AAMotionAPI.AutoPhase(c, AxisRef.A);        // starts commutation; returns at once

            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!AAMotionAPI.IsAutoPhase(c, AxisRef.A))
            {
                if (DateTime.UtcNow > deadline)
                    return false;                       // read AComtStatus[1] for the reason
                Thread.Sleep(100);
            }
            return true;
            // --8<-- [end:auto-phase]
        }

        public static void Stop(MotionController c)
        {
            // --8<-- [start:stop]
            AAMotionAPI.Stop(c, AxisRef.A);       // decelerate to a stop with ADecel
            // --8<-- [end:stop]
        }

        public static void StopRepetition(MotionController c)
        {
            // --8<-- [start:stop-rep]
            AAMotionAPI.StopRep(c, AxisRef.A);    // end a repetitive move after the repetition in progress
            // --8<-- [end:stop-rep]
        }

        public static void Abort(MotionController c)
        {
            // --8<-- [start:abort]
            AAMotionAPI.Abort(c, AxisRef.A);      // emergency deceleration with AEmrgDec
            // --8<-- [end:abort]
        }

        public static void MoveAbs(MotionController c)
        {
            // --8<-- [start:move-abs]
            ISingleAxis a = c.GetAxis(AxisRef.A);
            a.MoveAbs(100000, speed: 20000, accel: 200000, decel: 200000);
            Console.WriteLine("absolute move requested; inspect errors and actual state");
            // Alternative: AAMotionAPI.MoveAbs(c, AxisRef.A, 100000, 20000).
            // --8<-- [end:move-abs]
        }

        public static void MoveAbsAndWait(MotionController c)
        {
            // --8<-- [start:move-abs-wait]
            bool arrived = AAMotionAPI.MoveAbsAndWait(c, AxisRef.A, 100000, 20000, 200000, 200000, timeout: 10000);
            Console.WriteLine("absolute move arrived: " + arrived);
            if (!arrived)
            {
                // timed out, or a status poll failed: the motion may still be running
                AAMotionAPI.Stop(c, AxisRef.A);
                Console.WriteLine("not in target: " + c.LastReadError?.Message);
            }
            // --8<-- [end:move-abs-wait]
        }

        public static void MoveRel(MotionController c)
        {
            // --8<-- [start:move-rel]
            ISingleAxis a = c.GetAxis(AxisRef.A);
            bool ok = a.MoveRelAndWait(-5000, 20000, timeout: 5000);
            Console.WriteLine("relative move arrived: " + ok);
            if (!ok)
                AAMotionAPI.Stop(c, AxisRef.A);
            // --8<-- [end:move-rel]
        }

        public static void MultiAxis(MotionController c)
        {
            // --8<-- [start:multi-axis]
            ISingleAxis x = c.GetAxis(AxisRef.A), y = c.GetAxis(AxisRef.B);
            // same profile per axis, Begins in one bulk; arrival times may differ
            bool ok = AAMotionAPI.MoveAbs(c, 200000, 20000, 200000, x, 100000, y, 50000);
            if (!ok)
                Console.WriteLine("multi-axis command failed; inspect the error hook and each axis state");
            // --8<-- [end:multi-axis]
        }

        public static void Jog(MotionController c)
        {
            // --8<-- [start:jog]
            try
            {
                AAMotionAPI.Jog(c, AxisRef.A, 10000);
                Thread.Sleep(2000);
            }
            finally
            {
                AAMotionAPI.Stop(c, AxisRef.A);
            }
            // --8<-- [end:jog]
        }

        public static void Begin(MotionController c)
        {
            // --8<-- [start:begin]
            ISingleAxis a = c.GetAxis(AxisRef.A);
            a.MotionMode = 1;          // point-to-point
            a.Speed = 20000;
            a.RelTrgt = 0;             // 0: use AbsTrgt
            a.AbsTrgt = 100000;
            AAMotionAPI.Begin(c, AxisRef.A);
            // --8<-- [end:begin]
        }
    }
}

