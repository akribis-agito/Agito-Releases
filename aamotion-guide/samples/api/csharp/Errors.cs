// Examples for the "Error handling" chapter.
using System;
using AAComm.Services;
using AAMotion;

namespace ApiSamples
{
    public static class Errors
    {
        public static void Hook(MotionController c)
        {
            // --8<-- [start:hook]
            LocaleProvider.Instance.Locale = "zh-CN";   // descriptions in Chinese; "en-US" for English

            Action<int, string, string> onError = (code, sent, text) =>
                Console.WriteLine($"[ERR {code}] {sent} | {text.Replace("\n", " | ")}");

            AAMotionAPI.SetErrorOccurredHandler(c, onError);   // same as c.ErrorOccurred += onError
            // ...
            AAMotionAPI.ClearErrorOccurredHandler(c, onError); // the same delegate instance
            // --8<-- [end:hook]
        }

        public static void Reported(MotionController c)
        {
            // --8<-- [start:error-reported]
            // the same errors as ErrorOccurred, with the AAMotion member that was called
            EventHandler<ControllerErrorEventArgs> onReport = (sender, e) =>
                Console.WriteLine($"[{e.Source}] {e.Method ?? "-"} {e.Axis} {e.Keyword}: ERR {e.ErrorCode} {e.Command}");

            AAMotionAPI.SetErrorReportedHandler(c, onReport);   // same as c.ErrorReported += onReport
            // ...
            AAMotionAPI.ClearErrorReportedHandler(c, onReport);
            // --8<-- [end:error-reported]
        }

        public static void Exception(MotionController c)
        {
            // --8<-- [start:exception]
            ISingleAxis a = c.GetAxis(AxisRef.A);
            try
            {
                a.MotorOn = 1;
            }
            catch (ControllerCommandException ex)
            {
                // ErrorCode: the controller's ERR number, or ControllerCommandException.NoControllerCode (-1)
                // when the failure did not come from the controller (timeout, link lost, ...)
                Console.WriteLine($"{ex.Command} refused: ERR {ex.ErrorCode} ({ex.Keyword}) {ex.Message}");
            }
            // --8<-- [end:exception]
        }

        public static void TryRead(MotionController c)
        {
            // --8<-- [start:try-read]
            if (c.TryGetValue(AxisRef.A, eKeyword.Pos, out double pos))
            {
                Console.WriteLine("APos = " + pos);
            }
            else
            {
                ReadError e = c.LastReadError;
                Console.WriteLine($"read failed: {e.Command} ERR {e.ErrorCode} {e.Message} (failures so far: {c.ReadFailures})");
            }
            // --8<-- [end:try-read]
        }

        public static void Describe()
        {
            // --8<-- [start:describe]
            LocaleProvider.Instance.Locale = "zh-CN";
            string text = LocaleProvider.Instance.GetError(31);   // the same text the error hook receives
            Console.WriteLine("ERR 31: " + text);
            // --8<-- [end:describe]
        }
    }
}
