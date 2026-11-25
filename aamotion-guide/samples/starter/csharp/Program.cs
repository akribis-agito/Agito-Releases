using System.Text.Json;
using AAMotion;

internal sealed class Scenario
{
    public string Goal { get; set; } = "inputs";
    public string Axis { get; set; } = "A";
    public double Target { get; set; } = 80;
    public double Speed { get; set; } = 35;
    public double Accel { get; set; } = 60;
    public double Decel { get; set; } = 60;
    public int TimeoutMs { get; set; } = 8000;

    public void Validate()
    {
        if (Goal is not ("move" or "inputs" or "record") || Axis is not ("A" or "B"))
            throw new InvalidDataException("Unknown goal or axis");
        if (!double.IsFinite(Target) || Math.Abs(Target) > 100 || !double.IsFinite(Speed) || Speed <= 0 || Speed > 100 ||
            !double.IsFinite(Accel) || Accel <= 0 || Accel > 500 || !double.IsFinite(Decel) || Decel <= 0 || Decel > 500 || TimeoutMs < 100 || TimeoutMs > 60000)
            throw new InvalidDataException("Invalid motion parameters or timeout");
    }
}

internal static class Program
{
    private static int Main(string[] args)
    {
        MotionController? c = null;
        bool touchedMotion = false;
        AxisRef axis = AxisRef.A;
        int result = 0;
        try
        {
            var s = JsonSerializer.Deserialize<Scenario>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scenario.json")))
                ?? throw new InvalidDataException("Empty scenario");
            s.Validate();
            if (args.Length == 1 && args[0] == "--validate")
            {
                Console.WriteLine("Valid scenario; no SDK initialization or controller connection");
                return 0;
            }
            if (args.Length != 2 || args[1] != "--execute" || string.IsNullOrWhiteSpace(args[0]))
                throw new ArgumentException("Usage: Starter --validate | Starter <controller-IP|absolute-firmware_sim.exe> --execute");
            string target = args[0].Trim();
            bool simulation = target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            if (simulation && (!Path.IsPathRooted(target) || !File.Exists(target)))
                throw new ArgumentException("Simulator requires an existing absolute path");
            axis = s.Axis == "A" ? AxisRef.A : AxisRef.B;
            c = AAMotionAPI.Initialize(ControllerType.AGM800);
            c.ErrorOccurred += (code, sent, text) => Console.Error.WriteLine($"[ERR {code}] {sent} | {text}");
            if (simulation) c.Init();
            if (!(simulation ? AAMotionAPI.ConnectSimulation(c, target) : AAMotionAPI.Connect(c, target)))
                throw new IOException("Connect failed; inspect the address, model and AACommServer log");
            AAMotionAPI.SetDefaultTimeout(c, s.TimeoutMs);
            if (s.Goal == "inputs")
            {
                if (!c.TryGetValue(axis, eKeyword.DInPort, out double bits) || !double.IsFinite(bits))
                    throw new IOException("DInPort read failed; value unknown");
                Console.WriteLine($"{s.Axis} DInPort = {bits}; input 1 = {bits % 2 != 0}");
            }
            else if (s.Goal == "record")
            {
                string err = c.GetDataRecordingCapacity(out int maxVectors, out int maxSamples);
                if (err.Length != 0) throw new IOException(err);
                if (maxVectors < 2 || maxSamples < 1000) throw new IOException("Insufficient recorder capacity");
                var cfg = new MotionController.DataLoggerConfiguration { DurationSec = 0.5, GapMs = 1 };
                cfg.ParamsToRecord.Add(s.Axis + "Pos");
                cfg.ParamsToRecord.Add(s.Axis + "Vel[1]");
                err = c.ConfigureDataLogger(cfg);
                if (err.Length != 0) throw new IOException(err);
                err = c.StartDataRecording();
                if (err.Length != 0) throw new IOException(err);
                err = c.WaitForDataRecordingToEnd((uint)s.TimeoutMs);
                if (err.Length != 0) throw new IOException(err);
                err = c.UploadDataLoggerDataNonblocking(out MotionController.DataLoggerResult data, n => Console.WriteLine($"received {n} samples"));
                if (err.Length != 0) throw new IOException(err);
                Console.WriteLine(MotionController.GenerateCsvFile(data, Directory.GetCurrentDirectory()));
            }
            else
            {
                if (!AAMotionAPI.IsAutoPhase(c, axis))
                    throw new InvalidOperationException("Commutation not confirmed; inspect errors and verify machine preconditions with your setup tool first");
                touchedMotion = true;
                AAMotionAPI.MotorOn(c, axis);
                if (!AAMotionAPI.MoveAbsAndWait(c, axis, s.Target, s.Speed, s.Accel, s.Decel, s.TimeoutMs))
                    throw new IOException("Move wait failed; not proof of a stop. Inspect LastReadError and live state");
                Console.WriteLine($"In target: {c.GetAxis(axis).Pos}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            result = 1;
        }
        finally
        {
            if (c != null)
            {
                if (touchedMotion)
                {
                    try { AAMotionAPI.Stop(c, axis); } catch (Exception ex) { Console.Error.WriteLine("Stop not confirmed: " + ex.Message); result = 1; }
                    try { AAMotionAPI.MotorOff(c, axis); } catch (Exception ex) { Console.Error.WriteLine("MotorOff not confirmed: " + ex.Message); result = 1; }
                }
                try { AAMotionAPI.Disconnect(c); } catch (Exception ex) { Console.Error.WriteLine("Disconnect failed: " + ex.Message); result = 1; }
            }
        }
        return result;
    }
}
