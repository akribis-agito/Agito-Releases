// Examples for the "Data recording", "Slow logger" and "FastReadCache" chapters.
using System;
using System.Diagnostics;
using AAMotion;

namespace ApiSamples
{
    public static class Recording
    {
        public static void TestTrigger(MotionController c)
        {
            // --8<-- [start:force-trigger]
            var config = new MotionController.DataLoggerConfiguration
            {
                DurationSec = 0.25,
                GapMs = 1,
                TriggerPosition = 10,
                Trigger = new MotionController.DataLoggerTrigger
                {
                    Type = 5, Source = "AMotionStat", Value = 0, Mask = -1,
                },
            };
            config.ParamsToRecord.Add("APos");

            string error = c.ConfigureDataLogger(config);
            if (string.IsNullOrEmpty(error)) error = c.StartDataRecording();
            if (!string.IsNullOrEmpty(error)) { Console.WriteLine(error); return; }

            try
            {
                var timer = Stopwatch.StartNew();
                while (timer.ElapsedMilliseconds < 3000)
                {
                    error = c.GetDataRecordingStatus(out int status);
                    if (!string.IsNullOrEmpty(error)) { Console.WriteLine(error); return; }
                    if (status == 2) break;
                    System.Threading.Thread.Sleep(10);
                }
                if (timer.ElapsedMilliseconds >= 3000) { Console.WriteLine("Recorder did not arm."); return; }
                error = c.ForceDataLoggerTrigger();
                if (string.IsNullOrEmpty(error)) error = c.WaitForDataRecordingToEnd(3000);
                if (!string.IsNullOrEmpty(error)) { Console.WriteLine(error); return; }
                error = c.GetDataRecordingStatus(out int finalStatus);
                if (!string.IsNullOrEmpty(error)) { Console.WriteLine(error); return; }
                if (finalStatus != 4) { Console.WriteLine($"Recording ended in state {finalStatus}."); return; }
                error = c.UploadDataLoggerData(out MotionController.DataLoggerResult result);
                Console.WriteLine(string.IsNullOrEmpty(error) ? $"{result.Vectors[0].Values.Count} samples" : error);
            }
            finally
            {
                c.StopDataRecording();
            }
            // --8<-- [end:force-trigger]
        }

        public static void Record(MotionController c)
        {
            // --8<-- [start:record]
            var cfg = new MotionController.DataLoggerConfiguration
            {
                DurationSec = 0.5,          // 0.5 s of data
                GapMs = 0.25,               // one sample every 0.25 ms
                TriggerPosition = 10,       // 10 % of the buffer before the trigger
                Trigger = new MotionController.DataLoggerTrigger
                {
                    Type = 5,               // RecTrigTyp 5: rising edge above Value
                    Source = "AMotionStat", // watch axis A's motion status
                    Value = 0,            // fires when the axis starts moving
                },
            };
            cfg.ParamsToRecord.Add("APos");
            cfg.ParamsToRecord.Add("AVel[1]");      // an array keyword needs its index
            cfg.ParamsToRecord.Add("APosErr");

            // configure, start, wait for the trigger and the end, upload
            // start a move shortly after the recording is armed, so the trigger has something to catch
            System.Threading.Tasks.Task.Delay(200).ContinueWith(_ => c.GetAxis(AxisRef.A).MoveRel(50000, 20000));
            string err = c.RecordData(cfg, out MotionController.DataLoggerResult result, false, 5000);
            if (err.Length > 0) { Console.WriteLine("recording failed: " + err); return; }

            foreach (MotionController.DataLoggerVector v in result.Vectors)
                Console.WriteLine($"{v.Parameter}: {v.Values.Count} samples");
            string csv = MotionController.GenerateCsvFile(result, @"C:\data");   // timestamped file name
            // --8<-- [end:record]
        }

        public static void Steps(MotionController c)
        {
            // --8<-- [start:steps]
            var cfg = new MotionController.DataLoggerConfiguration { DurationSec = 1, GapMs = 1 };
            cfg.ParamsToRecord.Add("APos");
            string err = c.ConfigureDataLogger(cfg);
            if (!string.IsNullOrEmpty(err)) { Console.WriteLine(err); return; }
            err = c.StartDataRecording();
            if (!string.IsNullOrEmpty(err)) { Console.WriteLine(err); return; }
            err = c.WaitForDataRecordingToEnd(3000);
            if (!string.IsNullOrEmpty(err)) { Console.WriteLine(err); return; }
            err = c.GetDataRecordingStatus(out int status);
            if (!string.IsNullOrEmpty(err)) { Console.WriteLine(err); return; }
            if (status != 4) { Console.WriteLine($"Recording stopped, status {status}"); return; }

            err = c.UploadDataLoggerData(out MotionController.DataLoggerResult result);
            if (!string.IsNullOrEmpty(err)) { Console.WriteLine(err); return; }
            Console.WriteLine($"{result.Vectors[0].Values.Count} samples");
            // --8<-- [end:steps]
        }
    }

    public static class Slow
    {
        public static void Logger(MotionController c)
        {
            // --8<-- [start:slow]
            c.ConfigureSlowLogger(10, 0, "APos", "AVel[1]", "BPos");
            try
            {
                c.LoggerOn = 1;
                System.Threading.Thread.Sleep(100);
                Console.WriteLine(c.LoggerUpload());
            }
            finally
            {
                c.LoggerOn = 0;
            }
            // --8<-- [end:slow]
        }
        public static void WriteCsv(MotionController c)
        {
            // --8<-- [start:slow-csv]
            object[] channels = { "APos", "AVel[1]", "BPos" };
            c.ConfigureSlowLogger(10, 0, channels);
            using (var csv = System.IO.File.CreateText("slow-logger.csv"))
            {
                csv.WriteLine("packet_id,APos,AVel[1],BPos");
                try
                {
                    c.LoggerOn = 1;
                    int previousLost = c.LoggerLostPackets;
                    for (int i = 0; i < 40; i++)
                    {
                        System.Threading.Thread.Sleep(50);
                        var data = c.LoggerUpload(channels);
                        foreach (var packet in data.Packets)
                            csv.WriteLine(FormattableString.Invariant(
                                $"{packet.Id},{packet.Values[0]},{packet.Values[1]},{packet.Values[2]}"));
                        int lost = c.LoggerLostPackets;
                        if (lost != previousLost)
                            Console.WriteLine($"Lost packets: {lost}");
                        previousLost = lost;
                    }
                }
                finally
                {
                    c.LoggerOn = 0;
                }
            }
            // --8<-- [end:slow-csv]
        }

        public static void ConfigureByCode(MotionController c)
        {
            // --8<-- [start:slow-can]
            int aPos = c.GetKeywordCANCode(eKeyword.Pos, AxisRef.A);
            int aVel = c.GetKeywordCANCode(eKeyword.Vel, AxisRef.A, 1);
            c.ConfigureSlowLogger(10, 0, aPos, aVel);
            // --8<-- [end:slow-can]
        }

        public static void ConfigureTyped(MotionController c)
        {
            // --8<-- [start:slow-typed]
            var aPos = new MotionController.SlowLoggerParameter(
                AxisRef.A, eKeyword.Pos);
            var bVel = new MotionController.SlowLoggerParameter(
                AxisRef.B, eKeyword.Vel, 1);
            c.ConfigureSlowLogger(10, 0, aPos, bVel);
            // --8<-- [end:slow-typed]
        }

        public static void ReadStatus(MotionController c)
        {
            // --8<-- [start:slow-status]
            Console.WriteLine($"State: {c.LoggerState}");
            Console.WriteLine($"Free slots: {c.LoggerFreeSpace}");
            Console.WriteLine($"Pushed: {c.LoggerPushedPackets}");
            Console.WriteLine($"Lost: {c.LoggerLostPackets}");
            // --8<-- [end:slow-status]
        }
    }

    public static class Cache
    {
        public static void Fast(MotionController c)
        {
            // --8<-- [start:cache]
            AAMotionConfig.FastReadEnabled = true;                    // property reads may use the cache
            AAMotionAPI.RegisterCacheKeywords(c, AxisRef.A, eKeyword.Pos, eKeyword.MotionStat);
            c.FastReadCache.RegisterArrayElements(AxisRef.A, eKeyword.Vel, 1);
            AAMotionAPI.EnableFastReadCache(c, 5);                   // refresh every 5 ms in the background

            ISingleAxis a = c.GetAxis(AxisRef.A);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                double pos = a.Pos;                                   // from the cache: no round trip
            }
            Console.WriteLine($"1000 reads: {sw.ElapsedMilliseconds} ms, value age {AAMotionAPI.GetCacheAgeMs(c, AxisRef.A, eKeyword.Pos)} ms");

            c.DisableFastReadCache();
            AAMotionConfig.FastReadEnabled = false;
            // --8<-- [end:cache]
        }

        public static void Events(MotionController c)
        {
            // --8<-- [start:events]
            FastReadCache cache = c.FastReadCache;
            cache.RefreshCompleted += () =>
            {
                double? pos = cache.GetCachedValue(AxisRef.A, eKeyword.Pos);
                if (pos.HasValue) Console.WriteLine("APos = " + pos.Value);
            };
            cache.RefreshError += ex => Console.WriteLine("refresh failed: " + ex.Message);
            // --8<-- [end:events]
        }
    }
}

