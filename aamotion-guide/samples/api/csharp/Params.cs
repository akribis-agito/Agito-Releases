// Examples for the "Reading and writing parameters" chapter.
using System;
using AAComm.Services.UserUnits;
using AAMotion;

namespace ApiSamples
{
    public static class Params
    {
        public static void Raw(MotionController c)
        {
            // --8<-- [start:raw]
            if (AAMotionAPI.SendCommandString(c, "ASpeed=20000", out string reply))
                Console.WriteLine("write: " + reply);           // "OK"

            if (c.SendCommandString("APos", out reply))
                Console.WriteLine("APos = " + reply);           // the value, as text

            c.SendCommandString("ALoggerParams[1]", out reply); // array element: [index]
            // --8<-- [end:raw]
        }

        public static void Bulk(MotionController c)
        {
            // --8<-- [start:bulk]
            // several commands in one packet; replies come back separated by '>'
            if (c.SendBulkCommandString("APos;BPos;AVel[1];BVel[1]", out string reply))
            {
                double aPos = double.Parse(c.BulkCommandParser(1, reply));   // 1-based
                double bPos = double.Parse(c.BulkCommandParser(2, reply));
                Console.WriteLine($"A={aPos} B={bPos}  raw: {reply}");
            }
            // --8<-- [end:bulk]
        }

        public static void Values(MotionController c)
        {
            // --8<-- [start:get-value]
            double pos = c.GetValue(AxisRef.A, eKeyword.Pos);          // scalar: index 0
            double vel = c.GetValue(AxisRef.A, eKeyword.Vel, 1);       // array: index 1 and up
            UserUnitAxis u = c.GetUserUnits(AxisRef.A, eKeyword.Pos);
            Console.WriteLine($"APos = {pos} {u.Units} (factor {u.Factor})");
            // --8<-- [end:get-value]
        }

        public static void SetValue(MotionController c)
        {
            // --8<-- [start:set-value]
            // the write twin of GetValue: the units GetValue reads in, index 0 for a scalar
            try
            {
                c.SetValue(AxisRef.A, eKeyword.Speed, 20000);
                c.SetValue(AxisRef.A, eKeyword.GenData, 5, 10);      // AGenData[10] = 5
            }
            catch (ControllerCommandException ex)
            {
                Console.WriteLine($"refused: ERR {ex.ErrorCode} {ex.Command}");
            }
            // --8<-- [end:set-value]
        }

        public static void Typed(MotionController c)
        {
            // --8<-- [start:typed]
            ISingleAxis a = c.GetAxis(AxisRef.A);
            double pos = a.Pos;           // read
            a.Speed = 20000;              // write
            double avg = a.Vel[1];        // array keyword: Vel[1] is the filtered velocity
            // --8<-- [end:typed]
        }

        public static void CanCode(MotionController c)
        {
            // --8<-- [start:can-code]
            int baseCode = c.GetKeywordCANCode(eKeyword.Pos);                 // the connected controller's code
            int full = c.GetKeywordCANCode(eKeyword.LoggerParams, AxisRef.A, 1);
            string type = c.GetParameterType(eKeyword.Pos);                   // "'int' 32 bit", "'float' 32 bit", ...
            // --8<-- [end:can-code]
        }

        public static void GenData(MotionController c)
        {
            // --8<-- [start:gen-data]
            AAMotionAPI.WriteGenData(c, 10, 1234);          // AGenData[10] = 1234
            int v = AAMotionAPI.ReadGenData(c, 10);         // 1-based index

            AAMotionAPI.WriteGenDataF(c, 1, 3.5f);          // float array AGenDataF
            float f = AAMotionAPI.ReadGenDataF(c, 1);

            AAMotionAPI.WriteUserParam(c, 1, 42);           // AUserParam[1]
            int p = AAMotionAPI.ReadUserParam(c, 1);
            // --8<-- [end:gen-data]
        }

        public static void ParFiles(MotionController c)
        {
            // --8<-- [start:par-files]
            // Restore a previously verified backup to RAM. The caller keeps c connected.
            AAMotionAPI.DownloadParameters(c, @"C:\backup\axis.par", calcAndSave: false,
                percent => Console.WriteLine($"download {percent}%"),
                (summary, error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                        Console.WriteLine(error);
                    else if (summary == null || summary.ErrorCount != 0)
                        Console.WriteLine($"Check the download summary. Rejected rows: {summary?.ErrorCount}");
                    else
                        Console.WriteLine("Download finished. Verify the settings in RAM.");
                });
            // --8<-- [end:par-files]
        }
    }
}
