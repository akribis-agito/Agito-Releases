using System;
using System.Globalization;
using System.IO;
using System.Linq;
using AAMotion;

namespace AxisPanel
{
    internal static class AxisOperations
    {
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
        }

        private static string Save(string extension, string content)
        {
            Check(!string.IsNullOrWhiteSpace(content), "No data returned");
            string file = Path.Combine(Path.GetTempPath(), "aamotion-" + Guid.NewGuid() + extension);
            File.WriteAllText(file, content);
            return "Saved " + file;
        }

        internal static string Run(MotionController c, AxisRef axis, string operation, string input)
        {
            var motor = c.GetAxis(axis);
            var group = c.GetGroup(AxisRef.A);
            string letter = axis.ToString(), signal = letter + "Pos";
            string Command(string text)
            {
                bool ok = c.SendCommandString(text, out string reply);
                reply = (reply ?? "").Trim();
                Check(ok && !reply.StartsWith("ERR", StringComparison.Ordinal), text + ": " + reply);
                return reply;
            }
            int[] Numbers(int count)
            {
                string[] parts = input.Split(',');
                Check(parts.Length == count, $"Enter {count} comma-separated integers");
                return parts.Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            }
            void Ready(AxisRef id)
            {
                Check(Command(id + "MotorOn") == "1" && Command(id + "MotionStat") == "0",
                    "Enable each idle axis first");
            }

            switch (operation)
            {
                case "SineStopRep": Command(letter + "StopRep"); break;
                case "SinePtp":
                case "SineRepeat":
                case "SinePreCruise":
                case "SineCommit":
                    bool commit = operation == "SineCommit", repeat = operation == "SineRepeat";
                    var sineTargets = operation == "SinePreCruise" ? Numbers(3) : new[] { Numbers(1)[0], 0, 0 };
                    if (commit)
                        Check((motor.MotionMode == 20 || motor.MotionMode == 21) && motor.MotionStat != 0, "Start Sine PTP first");
                    else
                    {
                        Ready(axis);
                        if (operation == "SinePreCruise")
                        {
                            double start = motor.PosRef;
                            Check(Math.Min(start, sineTargets[0]) < sineTargets[1] && sineTargets[1] < Math.Max(start, sineTargets[0]) &&
                                sineTargets[2] >= 2000, "Pre-cruise point must be between start and target; speed at least 2000");
                        }
                        if (repeat) Command(letter + "RptMode=0");
                    }
                    var move = new AAMotionAPI.SinePtpMotionConfig
                    {
                        MotionMode = repeat ? 21 : 20, AbsTrgt = sineTargets[0], Speed = 2000,
                        Accel = 10000, Decel = 10000, JerkInAcc = 100000, JerkInDec = 100000,
                        PreCruAbsTrgt = sineTargets[1], PreCruiseSpd = sineTargets[2]
                    };
                    Check(commit ? AAMotionAPI.SinePtpCommit(c, axis, move) :
                        AAMotionAPI.SinePtpBegin(c, axis, move), operation);
                    break;
                case "Pvaj":
                    var point = input.Split(',');
                    Check(point.Length == 3, "Enter distance, gap and period");
                    int travel = int.Parse(point[0], CultureInfo.InvariantCulture);
                    int sampleGap = int.Parse(point[1], CultureInfo.InvariantCulture);
                    double period = double.Parse(point[2], NumberStyles.Float, CultureInfo.InvariantCulture);
                    Check(travel != 0 && sampleGap >= 1 && sampleGap <= 32 &&
                        period > 0 && !double.IsInfinity(period), "Check distance, gap and period");
                    double dt = sampleGap * (period / 1e6);
                    double jerk = travel / (2 * dt * dt * dt);
                    Check(jerk != 0 && !double.IsInfinity(jerk), "Invalid PVAJ jerk");
                    Ready(axis);
                    var table = new System.Collections.Generic.List<double> { 5, sampleGap, 1 };
                    double p = 0, v = 0, a = 0;
                    foreach (double j in new[] { jerk, -jerk, -jerk, jerk })
                    {
                        table.AddRange(new[] { p, v, a, j });
                        p += v * dt + a * dt * dt / 2 + j * dt * dt * dt / 6;
                        v += a * dt + j * dt * dt / 2;
                        a += j * dt;
                    }
                    table.AddRange(new double[] { travel, 0, 0, 0 });
                    Command(letter + "PVAJArm=0");
                    for (int i = 0; i < table.Count; i++)
                        Command(FormattableString.Invariant($"{letter}PVAJList[{i + 1}]={table[i]:G17}"));
                    Command(letter + "PVAJValidate=1");
                    Check(Command(letter + "PVAJStatus[1]") == "1", "PVAJ validation failed");
                    Command(letter + "MotionMode=22");
                    Command(letter + "PVAJArm=1");
                    if (!motor.Begin())
                    {
                        Command(letter + "PVAJArm=0");
                        Check(false, "PVAJ Begin failed");
                    }
                    break;
                case "PvajStatus":
                    return string.Join(", ", Enumerable.Range(1, 3).Select(i => Command($"{letter}PVAJStatus[{i}]")));
                case "DataLogger":
                    var config = new MotionController.DataLoggerConfiguration { DurationSec = 1, GapMs = 1 };
                    config.ParamsToRecord.Add(signal);
                    string error = c.ConfigureDataLogger(config);
                    Check(string.IsNullOrEmpty(error), error);
                    error = c.StartDataRecording();
                    Check(string.IsNullOrEmpty(error), error);
                    error = c.WaitForDataRecordingToEnd(5000);
                    Check(string.IsNullOrEmpty(error), error);
                    error = c.UploadDataLoggerData(out var recording);
                    Check(string.IsNullOrEmpty(error), error);
                    var values = recording.Vectors[0].Values;
                    var times = recording.TimeSec;
                    Check(values.Count > 0 && times.Count == values.Count, "No complete samples");
                    return Save(".csv", "time_s," + signal + "\n" + string.Join("\n", times.Zip(values,
                        (time, sample) => FormattableString.Invariant($"{time},{sample}"))));
                case "SlowStart":
                    Check(Command("LoggerOn") == "0", "SlowLogger is already on");
                    c.ConfigureSlowLogger(10, 0, signal);
                    c.LoggerOn = 1;
                    break;
                case "SlowUpload": return Save(".txt", c.LoggerUpload());
                case "SlowStop": c.LoggerOn = 0; break;
                case "RecorderStatus": return "Lost packets: " + c.LoggerLostPackets;
                case "CncFile":
                    string programFile = input.Trim();
                    Check(Path.IsPathRooted(programFile) && File.Exists(programFile), "Enter an existing absolute file path");
                    Ready(AxisRef.A);
                    Ready(AxisRef.B);
                    AAMotionAPI.RunCNCProgramFromFile(c, programFile);
                    break;
                case "CncOutput":
                case "CncLine":
                case "CncArc":
                    bool arc = operation == "CncArc";
                    var path = Numbers(arc ? 5 : 2);
                    Check(arc ? path[4] == 0 || path[4] == 1 : path[0] != 0 || path[1] != 0,
                        "Check displacement or arc direction");
                    Ready(AxisRef.A);
                    Ready(AxisRef.B);
                    int x = int.Parse(Command("APos"), CultureInfo.InvariantCulture), y = int.Parse(Command("BPos"), CultureInfo.InvariantCulture);
                    int targetX = arc ? path[0] : checked(x + path[0]);
                    int targetY = arc ? path[1] : checked(y + path[1]);
                    Check(group.ClearBuffer(), "CNC ClearBuffer");
                    Check(group.SetStartPositions(x, y, null), "CNC SetStartPositions");
                    Check(group.SetMotionProfile(100, 10000, 10000, 0), "CNC SetMotionProfile");
                    if (arc)
                        Check(group.ArcXY(targetX, targetY, path[2], path[3], path[4], 1000, 0), "CNC ArcXY");
                    else
                        Check(group.LinearAbsoluteXY(targetX, targetY, 1000, 0), "CNC LinearAbsoluteXY");
                    if (operation == "CncOutput")
                    {
                        Check(group.GroupDOutSetBit(AxisRef.A, 1), "Queue output set");
                        Check(group.Delay(200), "Queue delay");
                        Check(group.GroupDOutClearBit(AxisRef.A, 1), "Queue output clear");
                        Check(group.LinearAbsoluteXY(x, y, 1000, 0), "Queue return line");
                    }
                    Command("AMotionMode=11");
                    Command("BMotionMode=11");
                    Check(group.Begin(), "CNC Begin");
                    break;
                case "CncPause": group.Pause(); break;
                case "CncResume": group.Resume(); break;
                case "CncStop": group.Stop(); break;
                case "CncStatus": return "CNC complete: " + group.IsCNCCompleted(AxisRef.A);
                case "GearDirect":
                case "GearIndirect":
                    var gear = Numbers(2);
                    Check(gear[0] >= 0 && gear[1] != 0, "Enter master CAN code and nonzero numerator");
                    Ready(axis);
                    Command(letter + "GearMaster=" + gear[0]);
                    Command(letter + "MasterFact=" + gear[1]);
                    Command(letter + "MasterFactDen=65536");
                    Command(letter + "MasterModRev=0");
                    if (operation == "GearDirect") Command(letter + "MasterFilt=64");
                    Command(letter + (operation == "GearDirect" ? "MotionMode=5" : "MotionMode=6"));
                    Check(motor.Begin(), "Gear Begin");
                    break;
                case "GearStop": Check(motor.Stop(), operation); break;
                case "GearStatus": return Read("GearMaster", "MotionMode", "MotionStat");
                case "EcamStart":
                    int camTable = Numbers(1)[0];
                    Check(camTable >= 1 && camTable <= 10, "ECAM table must be 1–10");
                    Ready(axis);
                    int first = int.Parse(Command($"{letter}ECAMStart[{camTable}]"), CultureInfo.InvariantCulture);
                    int last = int.Parse(Command($"{letter}ECAMEnd[{camTable}]"), CultureInfo.InvariantCulture);
                    Check(first > 0 && last > first, "Configure an ECAM table first");
                    Command($"{letter}ECAMTableNum={camTable}");
                    Command(letter + "MotionMode=7");
                    Check(motor.Begin(), "ECAM Begin");
                    break;
                case "EcamStop": Command(letter + "StopECAM"); break;
                case "EcamStatus":
                    int tableNumber = Numbers(1)[0];
                    Check(tableNumber >= 1 && tableNumber <= 10, "ECAM table must be 1–10");
                    return Read("MotionMode", "MotorOn", "MotionStat", $"ECAMCycCount[{tableNumber}]");
                case "MultiAxis":
                    var targets = Numbers(2);
                    Ready(AxisRef.A);
                    Ready(AxisRef.B);
                    string Setup(string s, int target) =>
                        $"{s}MotionMode=1;{s}RelTrgt=0;{s}Speed=2000;{s}Accel=10000;{s}Decel=10000;{s}AbsTrgt={target}";
                    Check(c.SendBulkCommandString(Setup("A", targets[0]) + ";" + Setup("B", targets[1]) +
                        ";ABegin;BBegin", out string reply) && !reply.Contains("ERR"), "Two-axis MoveAbs");
                    break;
                case "PegArm":
                    int triggerPosition = Numbers(1)[0];
                    Check(Command(letter + "MotionStat") == "0", "Stop the axis first");
                    Command(letter + "EventOn=0");
                    Command(letter + "EventCntr=0");
                    AAMotionAPI.SetSingleEventPEG(c, axis, triggerPosition, 1, 0, 50);
                    Check(Command(letter + "EventOn") == "1", "PEG not enabled");
                    break;
                case "PegStop": Command(letter + "EventOn=0"); break;
                case "StopAB":
                    bool stopA = c.GetAxis(AxisRef.A).Stop();
                    bool stopB = c.GetAxis(AxisRef.B).Stop();
                    Check(stopA && stopB, "Stop A/B failed");
                    break;
                case "VectorArc":
                    var arcArgs = Numbers(5);
                    Check(arcArgs[4] == 0 || arcArgs[4] == 1, "Direction must be 0 or 1");
                    foreach (AxisRef member in new[] { AxisRef.A, AxisRef.B })
                    {
                        Ready(member);
                        var memberAxis = c.GetAxis(member);
                        Check(Math.Abs(memberAxis.PosRef - memberAxis.Pos) <= 1, "Arc start unsettled: check Pos and PosRef");
                    }
                    Check(AAMotionAPI.VectorArc(c, AxisRef.A, AxisRef.A, AxisRef.B,
                        arcArgs[2], arcArgs[3], arcArgs[0], arcArgs[1], arcArgs[4], 0,
                        1000, 10000, 10000, 100000, 4), "Vector Arc");
                    break;
                case "VectorLine":
                    var goals = Numbers(2);
                    Ready(AxisRef.A);
                    Ready(AxisRef.B);
                    Check(AAMotionAPI.VectorLinear(c, AxisRef.A, AxisRef.A | AxisRef.B,
                        1000, 10000, 10000, 100000, 4, goals[0], goals[1]), "Vector Linear");
                    break;
                case "VectorPause": Check(AAMotionAPI.VectorPause(c, AxisRef.A), operation); break;
                case "VectorResume": Check(AAMotionAPI.VectorContinue(c, AxisRef.A), operation); break;
                case "VectorStop": Check(c.GetAxis(AxisRef.A).Stop(), operation); break;
                case "SetPosition":
                    Check(Command(letter + "MotionStat") == "0", "Stop the axis first");
                    Check(AAMotionAPI.SetPosition(c, axis, Numbers(1)[0]), "SetPosition");
                    return "Pos=" + Command(signal);
                case "GenData":
                    int index = Numbers(1)[0];
                    Check(index >= 0, "Enter a nonnegative index");
                    return Command($"{letter}GenData[{index}]");
                case "TwoSpeed":
                    var distances = Numbers(2);
                    Check(0 < distances[1] && distances[1] < distances[0], "Require 0 < switch distance < move distance");
                    Ready(axis);
                    motor.MoveRelTwoSpeed(distances[0], 2000, 1000, distances[1], 10000, 10000, -1);
                    break;
                case "ReadInput":
                case "SetOutput":
                case "ClearOutput":
                    int bit = Numbers(1)[0];
                    Check(bit >= 1 && bit <= 32, "I/O index must be 1–32");
                    if (operation == "ReadInput")
                        return "Input = " + ((long.Parse(Command(letter + "DInPort"), CultureInfo.InvariantCulture) >> (bit - 1)) & 1);
                    Check(operation == "SetOutput" ? AAMotionAPI.DOutSetBit(c, axis, bit) :
                        AAMotionAPI.DOutClearBit(c, axis, bit), operation);
                    break;
                case "LockStart": Command(letter + "LockEn=1"); break;
                case "LockStop": Command(letter + "LockEn=0"); break;
                case "LockRead": return Read("LockCntr", "LockVal");
                case "EventStatus": return Read("EventOn", "EventCntr");
                case "MotionConfig": return Read("MotionMode", "CtrlMode");
                case "CiStatus": return Command("CIGlobalStat");
                case "ProgramRun":
                    var program = Numbers(2);
                    Check(motor.ProgRun(program[0], program[1]), "ProgRun");
                    break;
                case "ProgramHaltAll": Check(motor.ProgHaltAll(), "ProgHaltAll"); break;
                case "ProgramHalt": Check(motor.ProgHalt(Numbers(1)[0]), "ProgHalt"); break;
                case "ReadCommand":
                    Check(input.Length > 0 && input.IndexOfAny(new[] { ';', '\r', '\n' }) < 0, "Enter one command");
                    return Command(input);
                default: throw new ArgumentException(operation);
            }
            return operation + " sent; check status and error log";

            string Read(params string[] keys) => string.Join(", ", keys.Select(key => key + "=" + Command(letter + key)));
        }
    }
}
