using System;
using System.IO;
using System.Threading;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: SlowLogger <controller IP or absolute firmware_sim.exe path>");
            return 2;
        }

        using (var demo = new Demo(args[0]))
        {
            var c = demo.Controller;
            // --8<-- [start:operation]
            c.ConfigureSlowLogger(10, 0, "APos");
            c.LoggerOn = 1;
            try
            {
                Thread.Sleep(2000);
                string text = c.LoggerUpload();
                Demo.Check(!string.IsNullOrEmpty(text), "No logger data");
                File.WriteAllText("slow-logger.txt", text);
            }
            finally { c.LoggerOn = 0; }
            Console.WriteLine("Saved slow-logger.txt");
            // --8<-- [end:operation]
            demo.Complete();
        }
        return 0;
    }
}
