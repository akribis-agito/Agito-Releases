using System;
using System.Diagnostics;
using System.Threading;
using AAMotion;

internal sealed class Demo : IDisposable
{
    private readonly AxisRef[] _axes;
    private bool _complete;

    public Demo(string target, params AxisRef[] axes)
    {
        _axes = axes;
        Controller = AAMotionAPI.Initialize(ControllerType.AGM800);
        try
        {
            bool connected;
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Controller.Init();
                connected = AAMotionAPI.ConnectSimulation(Controller, target);
            }
            else
            {
                connected = AAMotionAPI.Connect(Controller, target);
            }
            Check(connected, "Connect");
            foreach (AxisRef axis in axes)
            {
                var motor = Controller.GetAxis(axis);
                Check(motor.MotorOn == 1 && motor.MotionStat == 0, $"{axis}: enable the idle axis first");
            }
        }
        catch
        {
            AAMotionAPI.Disconnect(Controller);
            throw;
        }
    }

    public MotionController Controller { get; }

    public static void Check(bool ok, string operation)
    {
        if (!ok)
            throw new InvalidOperationException(operation);
    }

    public static void Wait(Func<bool> finished, string operation, int timeoutMs = 30000)
    {
        var elapsed = Stopwatch.StartNew();
        while (!finished())
        {
            Check(elapsed.ElapsedMilliseconds < timeoutMs, operation + ": timeout");
            Thread.Sleep(50);
        }
    }

    public void Complete() => _complete = true;

    public void Dispose()
    {
        try
        {
            if (!_complete)
            {
                foreach (AxisRef axis in _axes)
                {
                    try { AAMotionAPI.Stop(Controller, axis); }
                    catch (Exception ex) { Console.Error.WriteLine($"{axis}: stop failed: {ex.Message}"); }
                }
            }
        }
        finally
        {
            AAMotionAPI.Disconnect(Controller);
        }
    }
}
