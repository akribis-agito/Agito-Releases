using System;
using AAMotion;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: MoveTwoAxes <controller IP or absolute firmware_sim.exe path>");
            return 2;
        }

        using (var demo = new Demo(args[0], AxisRef.A, AxisRef.B))
        {
            var c = demo.Controller;
            // --8<-- [start:operation]
            var a = c.GetAxis(AxisRef.A);
            var b = c.GetAxis(AxisRef.B);
            Demo.Check(AAMotionAPI.MoveAbs(c, 10000, 2000, 10000, a, 10000, b, 5000), "MoveAbs");
            Demo.Wait(() => a.InTargetStat == 4 && b.InTargetStat == 4, "Both axes in target");
            Console.WriteLine($"A={a.Pos}, B={b.Pos}");
            // --8<-- [end:operation]
            demo.Complete();
        }
        return 0;
    }
}
