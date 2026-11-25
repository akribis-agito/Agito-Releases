using System;
using AAMotion;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: HomeAxis <controller IP or absolute firmware_sim.exe path>");
            return 2;
        }

        using (var demo = new Demo(args[0], AxisRef.A))
        {
            var c = demo.Controller;
            // --8<-- [start:operation]
            var axis = c.GetAxis(AxisRef.A);
            axis.Home();
            Demo.Wait(() =>
            {
                double status = c.GetValue(AxisRef.A, eKeyword.HomingStat);
                Demo.Check(status >= 0, $"Homing failed: {status}");
                return status == 100;
            }, "Homing");
            Console.WriteLine($"Home complete: position = {axis.Pos}");
            // --8<-- [end:operation]
            demo.Complete();
        }
        return 0;
    }
}
