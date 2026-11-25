// Examples for the "Digital I/O" and "PEG and position lock" chapters.
using System;
using AAMotion;

namespace ApiSamples
{
    public static class Io
    {
        public static void Outputs(MotionController c)
        {
            // --8<-- [start:outputs]
            AAMotionAPI.DOutSetBit(c, AxisRef.A, 1);      // output 1 on (1-based)
            AAMotionAPI.DOutToggleBit(c, AxisRef.A, 2);
            AAMotionAPI.DOutClearBit(c, AxisRef.A, 1);

            int out1 = AAMotionAPI.GetDOutPort(c, AxisRef.A, 0);   // output 1 is register bit 0
            // --8<-- [end:outputs]
        }

        public static void Inputs(MotionController c)
        {
            // --8<-- [start:inputs]
            IIOModule io = c.GetIO(AxisRef.A);
            int in3 = io.GetDInPort(2);           // input 3 is register bit 2
            int all = io.DInPort;                 // the whole input register
            bool in3Again = ((all >> 2) & 1) == 1;
            // --8<-- [end:inputs]
        }

        public static void SinglePeg(MotionController c)
        {
            // --8<-- [start:single-peg]
            // one pulse when axis A passes 50000, on the output chosen by eventSelect
            AAMotionAPI.SetSingleEventPEG(c, AxisRef.A, eventBegPos: 50000, eventSelect: 1);
            AAMotionAPI.MoveAbs(c, AxisRef.A, 100000, 20000);
            // ...
            AAMotionAPI.EventDisEnable(c, AxisRef.A);
            // --8<-- [end:single-peg]
        }

        public static void GapPeg(MotionController c)
        {
            // --8<-- [start:gap-peg]
            // a pulse every 1000 between 10000 and 90000: 81 pulses, e.g. to trigger a camera
            AAMotionAPI.SetEventFixedGapPEG(c, AxisRef.A, 10000, 1000, 90000, eventSelect: 1, eventPulseWid: 10);
            AAMotionAPI.MoveAbs(c, AxisRef.A, 100000, 20000);
            // --8<-- [end:gap-peg]
        }

        public static void TablePeg(MotionController c)
        {
            // --8<-- [start:table-peg]
            string[] setup = { "AEventOn=0", "AEventType=2", "AEventPulseRes=0",
                               "AEventPulseWid=10", "AEventTableWid[1]=-1" };
            foreach (string command in setup)
            {
                if (!c.SendCommandString(command, out string reply))
                {
                    Console.WriteLine(reply);
                    return;
                }
            }
            double[] positions = { 12000, 15500, 23000, 41000 };
            for (int i = 0; i < positions.Length; i++)
            {
                if (!AAMotionAPI.SetEventTableValue(c, AxisRef.A, i + 1, positions[i]) ||
                    !AAMotionAPI.SetEventTableSelect(c, AxisRef.A, i + 1, 1))
                    return;
            }
            if (AAMotionAPI.SetEventTableBeginIndex(c, AxisRef.A, 1) &&
                AAMotionAPI.SetEventTableEndIndex(c, AxisRef.A, positions.Length) &&
                !c.SendCommandString("AEventOn=1", out string armReply))
                Console.WriteLine(armReply);
            // --8<-- [end:table-peg]
        }

        public static void Lock(MotionController c)
        {
            // --8<-- [start:lock]
            ISingleAxis a = c.GetAxis(AxisRef.A);
            AAMotionAPI.LockDisEnable(c, AxisRef.A);
            AAMotionAPI.SetLock(c, AxisRef.A, enableLock: false, lockSignalPolarity: 1, lockSource: 1);
            try
            {
                AAMotionAPI.LockEnable(c, AxisRef.A);
                int before = a.LockCntr;
                Console.WriteLine("Apply one edge to the selected input, then press Enter.");
                Console.ReadLine();
                if (a.LockCntr != before)
                    Console.WriteLine($"Captured position: {a.LockVal}");
                else
                    Console.WriteLine("No new capture.");
            }
            finally
            {
                AAMotionAPI.LockDisEnable(c, AxisRef.A);
            }
            // --8<-- [end:lock]
        }
    }
}
