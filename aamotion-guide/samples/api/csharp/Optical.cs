// Examples for the "Central-i" and "Optical coupling light finding" chapters.
using System;
using System.Globalization;
using System.Threading;
using AAMotion;

namespace ApiSamples
{
    public static class CentralI
    {
        public static void Group(MotionController c)
        {
            // --8<-- [start:ci]
            AAMotionAPI.CiConnect(c, AxisRef.E);               // bring a Central-i slave online
            ICiGroupAxis g = c.GetCiGroup(AxisRef.A);          // Central-i CNC group A
            g.ClearBuffer();
            g.LinearAbsoluteXYT(100000, 50000, 9000, 40000, 0); // X, Y and the T (rotary) axis together
            g.CiGroupDOutSetBit(AxisRef.A, 2);                 // output 2 on at this point (1-based number)
            g.Begin();
            // --8<-- [end:ci]
        }
    }

    public static class Optical
    {
        // --8<-- [start:firmware-data]
        // index must come from the address table for the installed firmware.
        // This example uses axis A; change the prefix only after checking that table.
        public static bool TryReadFirmwareData(MotionController c, int index, out int value)
        {
            value = 0;
            if (index < 0) return false;
            if (!c.SendCommandString($"AGenData[{index}]", out string reply))
                return false;
            string number = reply.Trim().TrimEnd('>').Trim();
            return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool WriteFirmwareData(MotionController c, int index, int value)
        {
            if (index < 0) return false;
            string command = "AGenData[" + index.ToString(CultureInfo.InvariantCulture) + "]="
                + value.ToString(CultureInfo.InvariantCulture);
            return c.SendCommandString(command, out string reply);
        }
        // --8<-- [end:firmware-data]

        public static void ReadFirmwareData(MotionController c)
        {
            Console.Write("Axis A GenData index from your firmware table (blank cancels): ");
            if (!int.TryParse(Console.ReadLine(), out int index)) return;
            if (TryReadFirmwareData(c, index, out int value))
                Console.WriteLine($"AGenData[{index}] = {value}");
            else
                Console.WriteLine("read/parse failed; inspect the error hook and firmware address table");
        }

        public static bool Spiral(MotionController c)
        {
            // --8<-- [start:spiral]
            OpticalCouplingLightFinding oc = AAMotionAPI.CreateOpticalCouplingLightFinding(c);
            oc.ApplySpiralConfig(new SpiralConfig
            {
                NumOfAInPort = 1,        // power read on analog input 1
                NumOfXAxis = 0,          // coupling axes, as numbered by the algorithm (0, 1, 2)
                NumOfYAxis = 1,
                SpiralPitch = 50,
                SpiralMaxRadius = 2000,
                SpiralAngleCoeff = 10,
                SpiralScanSpeed = 2000,
                SpiralRtnSpeed = 5000,
                PowerTh = 1000,
            });

            c.WriteGenData(602, 0);      // clear the previous result before starting
            oc.StartSpiralScan();        // AGenData[500] = 1

            DateTime deadline = DateTime.UtcNow.AddSeconds(60);
            while (oc.CouplingResultCode == 0 && DateTime.UtcNow < deadline)
                Thread.Sleep(100);

            if (oc.CouplingResultCode == 0)
            {
                oc.EmergencyStop();
                Console.WriteLine("scan timed out; stop requested, verify actual state");
                return false;
            }

            SpiralScanResult r = oc.GetSpiralScanResult();
            if (r.IsSuccess)
                Console.WriteLine($"max power {r.MaxPower} at ({r.MaxPowerXPos}, {r.MaxPowerYPos})");
            else
                Console.WriteLine($"failed: {r.CouplingResult}, error {r.ErrCode}");
            return r.IsSuccess;
            // --8<-- [end:spiral]
        }

        public static void Curves(MotionController c, OpticalCouplingLightFinding oc, SpiralScanResult r)
        {
            // --8<-- [start:curves]
            oc.SyncAllGenData();                                      // one bulk read of GenData into a local copy
            // spiral data areas in GenData: X 1000-3499, Y 3500-5999, channel 1 power 6000-8499
            int[] x = oc.SpiralXPosArray(1000, 1000 + r.LastSampleIndex);
            int[] y = oc.SpiralYPosArray(3500, 3500 + r.LastSampleIndex);
            int[] p = oc.Channel1PowerArray(6000, 6000 + r.LastSampleIndex);
            // --8<-- [end:curves]
        }

        public static void Stop(OpticalCouplingLightFinding oc)
        {
            // --8<-- [start:stop]
            oc.EmergencyStop();          // AGenData[501] = non-zero: stop and leave the coupling logic
            // --8<-- [end:stop]
        }
    }
}

