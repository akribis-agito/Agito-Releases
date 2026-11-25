using System;
using AAMotion;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: ReadInput <controller IP or absolute firmware_sim.exe path>");
            return 2;
        }

        using (var demo = new Demo(args[0]))
        {
            var c = demo.Controller;
            // --8<-- [start:operation]
            var io = c.GetIO(AxisRef.A);
            Console.WriteLine($"Input 1 = {io.GetDInPort(0)}");
            // --8<-- [end:operation]
            demo.Complete();
        }
        return 0;
    }
}
