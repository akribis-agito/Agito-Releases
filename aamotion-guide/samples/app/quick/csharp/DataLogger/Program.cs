using System;
using AAMotion;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: DataLogger <controller IP or absolute firmware_sim.exe path>");
            return 2;
        }

        using (var demo = new Demo(args[0]))
        {
            var c = demo.Controller;
            // --8<-- [start:operation]
            var config = new MotionController.DataLoggerConfiguration { DurationSec = 1, GapMs = 1 };
            config.ParamsToRecord.Add("APos");
            string error = c.ConfigureDataLogger(config);
            Demo.Check(string.IsNullOrEmpty(error), error);
            error = c.StartDataRecording();
            Demo.Check(string.IsNullOrEmpty(error), error);
            error = c.WaitForDataRecordingToEnd(5000);
            Demo.Check(string.IsNullOrEmpty(error), error);
            error = c.UploadDataLoggerData(out var result);
            Demo.Check(string.IsNullOrEmpty(error), error);
            var values = result.Vectors[0].Values;
            Demo.Check(values.Count > 0, "No APos samples");
            Console.WriteLine($"APos: {values.Count} samples");
            // --8<-- [end:operation]
            demo.Complete();
        }
        return 0;
    }
}
