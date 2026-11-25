// Examples for the "Vector motion", "Electronic cam" and "Force and current control" chapters.
using System;
using System.Threading;
using AAMotion;

namespace ApiSamples
{
    public static class VectorMotion
    {
        public static void Linear(MotionController c)
        {
            // --8<-- [start:linear]
            // axes B and C move together in a straight line; started from B, the lowest member
            bool ok = AAMotionAPI.VectorLinear(c, AxisRef.B, AxisRef.B | AxisRef.C,
                speed: 30000, accel: 300000, decel: 300000, emrgdec: 1000000, smooth: 4,
                80000, 40000);                       // targets in mask order: B, then C
            // --8<-- [end:linear]
        }

        public static void Arc(MotionController c)
        {
            // --8<-- [start:arc]
            // arc on axes A (X) and B (Y) around the absolute centre (50000, 50000)
            // dir is written to AVecArcDir: 0 = counter-clockwise, 1 = clockwise
            // circles: extra full turns (AVecNumCircles); 0 = just the arc
            bool ok = AAMotionAPI.VectorArc(c, AxisRef.A, AxisRef.A, AxisRef.B,
                center1: 50000, center2: 50000, pos1: 100000, pos2: 50000,
                dir: 0, circles: 0,
                speed: 20000, accel: 200000, decel: 200000, emrgdec: 1000000, smooth: 4);
            // --8<-- [end:arc]
        }

        public static void PauseResume(MotionController c)
        {
            // --8<-- [start:pause]
            AAMotionAPI.VectorPause(c, AxisRef.A);       // AVecPause=1
            AAMotionAPI.VectorContinue(c, AxisRef.A);    // AVecPause=0
            AAMotionAPI.StopVec(c, AxisRef.A);
            // --8<-- [end:pause]
        }
    }

    public static class Cam
    {
        public static void Sine(MotionController c)
        {
            // --8<-- [start:sine]
            ISingleAxis slave = c.GetAxis(AxisRef.B);
            int posRefCode = c.GetKeywordCANCode(eKeyword.PosRef);   // follow the master's reference position

            // 360 points of a sine cam stored in GenData from index 100
            slave.ECAM_CreateSinusoidalTable(amplitude: 20000, offset: 0, numPoints: 360, startIndex: 100);
            // one cam point every 100 master counts, master = axis A's PosRef, repeat forever
            slave.ECAM_ConfigureTable(startIndex: 100, endIndex: 459, startCycIndex: 100, endCycIndex: 459,
                cycles: int.MaxValue, masterKeyword: posRefCode, gap: 100);
            slave.ECAM_Start();

            AAMotionAPI.Jog(c, AxisRef.A, 10000);                     // move the master
            Thread.Sleep(5000);
            Console.WriteLine("cycles: " + slave.ECAM_GetCycleCount());
            slave.ECAM_Stop();
            // --8<-- [end:sine]
        }

        public static void Custom(MotionController c)
        {
            // --8<-- [start:custom]
            int[] profile = new int[100];
            for (int i = 0; i < profile.Length; i++)
                profile[i] = (int)(5000 * (1 - Math.Cos(Math.PI * i / 99.0)));   // a smooth rise of 10000

            AAMotionAPI.ECAM_SetTableRange(c, AxisRef.B, 200, 299);
            AAMotionAPI.ECAM_SetTableData(c, AxisRef.B, profile);             // written to GenData[200..299]
            AAMotionAPI.ECAM_SetMaster(c, AxisRef.B, c.GetKeywordCANCode(eKeyword.PosRef));
            AAMotionAPI.ECAM_SetGap(c, AxisRef.B, 200);
            AAMotionAPI.ECAM_SetCycleParams(c, AxisRef.B, 200, 299, 1);       // run the table once
            AAMotionAPI.ECAM_Start(c, AxisRef.B);
            bool running = AAMotionAPI.ECAM_IsActive(c, AxisRef.B);
            // --8<-- [end:custom]
        }
    }

    public static class Force
    {
        public static void Press(MotionController c)
        {
            // --8<-- [start:press]
            // approach fast, slow down near the part, switch to force control, hold, retract
            AAMotionAPI.ForceCurr_SetMotion(c, AxisRef.A, absTrgt: 50000, speed: 40000);
            AAMotionAPI.ForceCurr_SetSlowApproach(c, AxisRef.A, speedChgNew: 2000, speedChgPos: 45000, speedChgDir: 0, speedChgOn: 1);
            AAMotionAPI.ForceCurr_SetCurrModeSWTrigSrc(c, AxisRef.A, currPosTh: 48000, currPosThDir: 0);
            AAMotionAPI.ForceCurr_SetForceCmdSource(c, AxisRef.A, forceCmdSrc: 1,
                forceCurrSlope: new[] { 100.0 }, forceCmdHTime: new[] { 16384 }, forceCmdVal: new[] { 2000.0 });
            AAMotionAPI.ForceCurr_SetRetractMotion(c, AxisRef.A, fRetractTarget: 0, fRetractSpeed: 40000);
            AAMotionAPI.Begin(c, AxisRef.A);
            // --8<-- [end:press]
        }

        public static void Modes(MotionController c)
        {
            // --8<-- [start:modes]
            AAMotionAPI.GoToForceMode(c, AxisRef.A);   // force loop
            AAMotionAPI.GoToCurrMode(c, AxisRef.A);    // current loop only
            AAMotionAPI.GoToPosMode(c, AxisRef.A);     // back to position control
            // --8<-- [end:modes]
        }

        public static void SetPos(MotionController c)
        {
            // --8<-- [start:set-position]
            // declare the current position to be 0; the value is in counts, never User Units
            AAMotionAPI.SetPosition(c, AxisRef.A, 0);
            // --8<-- [end:set-position]
        }
    }
}
