using System;
using AAMotion;

internal static class Diagnostics
{
    public static void ReadStatus(MotionController c, AxisRef axis)
    {
        // --8<-- [start:diagnostic-status]
        foreach (eKeyword key in new[] { eKeyword.Pos, eKeyword.MotorOn, eKeyword.MotionReason, eKeyword.ConFlt })
        {
            if (c.TryGetValue(axis, key, out double value) && double.IsFinite(value))
                Console.WriteLine($"{axis} {key} = {value}");
            else
                Console.WriteLine($"{axis} {key}: unknown; {c.LastReadError?.Message}");
        }
        // --8<-- [end:diagnostic-status]
    }
}
