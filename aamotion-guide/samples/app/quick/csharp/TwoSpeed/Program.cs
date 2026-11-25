using System;
using AAMotion;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: TwoSpeed <controller IP or absolute firmware_sim.exe path>");
            return 2;
        }

        using (var demo = new Demo(args[0], AxisRef.A))
        {
            var c = demo.Controller;
            // --8<-- [start:operation]
            var axis = c.GetAxis(AxisRef.A);
            Demo.Check(axis.MoveRelTwoSpeedAndWait(10000, 4000, 1000, 8000,
                accel: 50000, decel: 50000, timeout: 10000), "Two-speed move");
            Console.WriteLine($"In target: position = {axis.Pos}");
            // --8<-- [end:operation]
            demo.Complete();
        }
        return 0;
    }
}
