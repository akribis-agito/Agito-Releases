using System;
using AAMotion;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: CncLine <controller IP or absolute firmware_sim.exe path>");
            return 2;
        }

        using (var demo = new Demo(args[0], AxisRef.A, AxisRef.B))
        {
            var c = demo.Controller;
            // --8<-- [start:operation]
            var group = c.GetGroup(AxisRef.A);
            int x = (int)c.GetAxis(AxisRef.A).Pos;
            int y = (int)c.GetAxis(AxisRef.B).Pos;
            Demo.Check(group.ClearBuffer(), "ClearBuffer");
            Demo.Check(group.SetStartPositions(x, y, null), "SetStartPositions");
            Demo.Check(group.SetMotionProfile(100, 10000, 10000, 0), "SetMotionProfile");
            Demo.Check(group.LinearAbsoluteXY(x + 1000, y + 1000, 1000, 0), "LinearAbsoluteXY");
            Demo.Check(c.SendCommandString("AMotionMode=11", out _) &&
                       c.SendCommandString("BMotionMode=11", out _), "Set CNC mode");
            Demo.Check(group.Begin(), "Begin");
            try { Demo.Wait(() => group.IsCNCCompleted(AxisRef.A), "CNC completion"); }
            catch { group.Stop(); throw; }
            Console.WriteLine($"CNC complete: A={c.GetAxis(AxisRef.A).Pos}, B={c.GetAxis(AxisRef.B).Pos}");
            // --8<-- [end:operation]
            demo.Complete();
        }
        return 0;
    }
}
