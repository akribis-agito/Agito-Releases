"""Short SDK workflows used by the Axis Panel buttons."""
import math
import tempfile
from pathlib import Path

import aamotion as am


OPERATIONS = [line.split("\t") for line in
              (Path(__file__).resolve().parent.parent / "operations.tsv").read_text(encoding="utf-8").splitlines()]


def check(ok, operation):
    if not ok:
        raise RuntimeError(operation)


def run(h, axis, operation, arguments):
    letter = "ABCD"[axis.bit_length() - 1]
    signal = letter + "Pos"

    def command(text):
        reply = am.send_command_string(h, text)
        check(reply is not None and not reply.lstrip().startswith("ERR"), text)
        return reply

    def read(axis_id, keyword):
        value = am.get_value(h, axis_id, keyword, 0)
        check(value is not None, "Read failed")
        return value

    def ready(*axes):
        for axis_id in axes:
            check(read(axis_id, am.KEYWORD_MOTOR_ON) == 1 and
                  read(axis_id, am.KEYWORD_MOTION_STAT) == 0, "Enable each idle axis first")

    def numbers(count):
        values = [int(value) for value in arguments.split(",")]
        check(len(values) == count and all(-2147483648 <= x <= 2147483647 for x in values),
              f"Enter {count} comma-separated 32-bit integers")
        return values

    if operation == "SineStopRep":
        return command(letter + "StopRep")
    if operation in ("SinePtp", "SineRepeat", "SinePreCruise", "SineCommit"):
        commit = operation == "SineCommit"
        repeat = operation == "SineRepeat"
        target, pre_target, pre_speed = numbers(3) if operation == "SinePreCruise" else (*numbers(1), 0, 0)
        if commit:
            check(command(letter + "MotionMode") in ("20", "21") and
                  read(axis, am.KEYWORD_MOTION_STAT) != 0, "Start Sine PTP first")
        else:
            ready(axis)
            if operation == "SinePreCruise":
                start = read(axis, am.KEYWORD_POS_REF)
                check(min(start, target) < pre_target < max(start, target) and pre_speed >= 2000,
                      "Pre-cruise point must be between start and target; speed at least 2000")
            command(letter + f"MotionMode={21 if repeat else 20}")
            if repeat:
                command(letter + "RptMode=0")
        for parameter in ("RelTrgt=0", f"AbsTrgt={target}", "Speed=2000",
                          "Accel=10000", "Decel=10000", "JerkInAcc=100000", "JerkInDec=100000",
                          "PreCruRelTrgt=0", f"PreCruAbsTrgt={pre_target}", f"PreCruiseSpd={pre_speed}"):
            command(letter + parameter)
        if commit:
            command(f"ACommitMotion={axis}")
        else:
            check(am.begin(h, axis), "Sine PTP Begin")
        return "Accepted; check InTargetStat and ConFlt"

    if operation == "Pvaj":
        distance_text, gap_text, period_text = arguments.split(",")
        distance, gap, period_us = int(distance_text), int(gap_text), float(period_text)
        check(-2147483648 <= distance <= 2147483647 and distance != 0 and 1 <= gap <= 32 and
              math.isfinite(period_us) and period_us > 0, "Check distance, gap and period")
        interval = gap * period_us / 1_000_000
        jerk = distance / (2 * interval**3)
        check(jerk != 0 and math.isfinite(jerk), "Invalid PVAJ jerk")
        ready(axis)
        table = [5, gap, 1]
        p = v = a = 0.0
        for j in (jerk, -jerk, -jerk, jerk):
            table.extend((p, v, a, j))
            p += v * interval + a * interval**2 / 2 + j * interval**3 / 6
            v += a * interval + j * interval**2 / 2
            a += j * interval
        table.extend((distance, 0, 0, 0))
        command(letter + "PVAJArm=0")
        for index, value in enumerate(table, 1):
            command(f"{letter}PVAJList[{index}]={value:.17g}")
        command(letter + "PVAJValidate=1")
        check(command(letter + "PVAJStatus[1]").strip() == "1", "PVAJ validation failed")
        command(letter + "MotionMode=22")
        command(letter + "PVAJArm=1")
        try:
            check(am.begin(h, axis), "PVAJ Begin")
        except RuntimeError:
            command(letter + "PVAJArm=0")
            raise
        return "Accepted; check PVAJ status and ConFlt"
    if operation == "PvajStatus":
        return ", ".join(command(f"{letter}PVAJStatus[{i}]") for i in (1, 2, 3))

    if operation == "DataLogger":
        check(am.data_rec_record(h, signal, 1, 1, 0, 0, None, 0, 0, False, 5000), "Record failed")
        info = am.data_rec_info(h)
        check(info is not None and info[2] == 1 and info[3] > 0, "No position samples")
        values = am.data_rec_vector_values(h, 0, info[3])
        times = am.data_rec_time(h, info[3])
        check(values and times and len(values) == len(times), "Read recording vectors failed")
        return save("data-logger", "csv", f"time_s,{signal}\n" +
                    "".join(f"{t},{v}\n" for t, v in zip(times, values)))

    if operation == "SlowStart":
        check(command("LoggerOn").strip() == "0", "SlowLogger is already on")
        check(am.configure_slow_logger(h, 10, 0, [signal]), "ConfigureSlowLogger")
        check(am.set_logger_on(h, 1), "LoggerOn")
        return f"Recording {signal}; use Upload and Stop"
    if operation == "SlowUpload":
        return save("slow-logger", "txt", am.logger_upload(h))
    if operation == "SlowStop":
        check(am.set_logger_on(h, 0), "LoggerOff")
        return "SlowLogger stopped"
    if operation == "RecorderStatus":
        return "Lost packets: " + str(am.get_logger_lost_packets(h))

    if operation == "CncFile":
        path = Path(arguments.strip())
        check(path.is_absolute() and path.is_file(), "Enter an existing absolute file path")
        ready(1, 2)
        am.run_cnc_program_from_file(h, str(path))
        return "CNC file sent; check CNC status and error log"
    if operation in ("CncLine", "CncArc", "CncOutput"):
        arc = operation == "CncArc"
        params = numbers(5 if arc else 2)
        check((params[4] in (0, 1)) if arc else any(params), "Check displacement or arc direction")
        ready(1, 2)
        x, y = int(command("APos")), int(command("BPos"))
        target = params[:2] if arc else (x + params[0], y + params[1])
        check(all(-2147483648 <= value <= 2147483647 for value in target), "CNC target range")
        check(am.clear_buffer(h), "CNC ClearBuffer")
        check(am.set_start_positions(h, 3, x, y, 0), "CNC SetStartPositions")
        check(am.set_motion_profile(h, 100, 10000, 10000, 0), "CNC SetMotionProfile")
        if arc:
            check(am.arc_xy(h, *params, 1000, 0), "CNC ArcXY")
        else:
            check(am.linear_absolute_xy(h, *target, 1000, 0), "CNC LinearAbsoluteXY")
        if operation == "CncOutput":
            check(am.group_d_out_set_bit(h, 1, 1), "Queue output set")
            check(am.delay(h, 200), "Queue delay")
            check(am.group_d_out_clear_bit(h, 1, 1), "Queue output clear")
            check(am.linear_absolute_xy(h, x, y, 1000, 0), "Queue return line")
        command("AMotionMode=11")
        command("BMotionMode=11")
        check(am.cnc_begin(h), "CNC Begin")
        return "CNC accepted; use Query completion"
    if operation == "CncStatus":
        return f"CNC complete: {am.is_cnc_completed(h, 1)}"
    cnc_controls = {"CncPause": am.pause, "CncResume": am.resume, "CncStop": am.group_stop}
    if operation in cnc_controls:
        check(cnc_controls[operation](h), operation)
        return "CNC command accepted"

    if operation in ("GearDirect", "GearIndirect"):
        master, numerator = numbers(2)
        check(master >= 0 and numerator != 0, "Enter master CAN code and nonzero numerator")
        ready(axis)
        command(letter + f"GearMaster={master}")
        command(letter + f"MasterFact={numerator}")
        command(letter + "MasterFactDen=65536")
        command(letter + "MasterModRev=0")
        if operation == "GearDirect":
            command(letter + "MasterFilt=64")
        command(letter + ("MotionMode=5" if operation == "GearDirect" else "MotionMode=6"))
        check(am.begin(h, axis), "Gear Begin")
        return "Follower started; master not commanded"
    if operation == "GearStop":
        check(am.stop(h, axis), "Gear Stop")
        return "Follower Stop accepted"
    if operation == "GearStatus":
        return ", ".join(f"{key}={command(letter + key)}" for key in ("GearMaster", "MotionMode", "MotionStat"))

    if operation == "EcamStart":
        table, = numbers(1)
        check(1 <= table <= 10, "ECAM table must be 1–10")
        ready(axis)
        first = int(command(f"{letter}ECAMStart[{table}]"))
        last = int(command(f"{letter}ECAMEnd[{table}]"))
        check(first > 0 and last > first, "Configure an ECAM table first")
        command(f"{letter}ECAMTableNum={table}")
        command(letter + "MotionMode=7")
        check(am.begin(h, axis), "ECAM Begin")
        return "Follower started; check status and cycle count"
    if operation == "EcamStop":
        return command(letter + "StopECAM")
    if operation == "EcamStatus":
        table, = numbers(1)
        check(1 <= table <= 10, "ECAM table must be 1–10")
        return ", ".join(f"{key}={command(letter + key)}" for key in
                         ("MotionMode", "MotorOn", "MotionStat", f"ECAMCycCount[{table}]"))

    if operation == "MultiAxis":
        a, b = numbers(2)
        ready(1, 2)
        commands = [f"{s}MotionMode=1;{s}RelTrgt=0;{s}Speed=2000;{s}Accel=10000;{s}Decel=10000;{s}AbsTrgt={v}"
                    for s, v in (("A", a), ("B", b))]
        reply = am.send_bulk_command_string(h, ";".join(commands) + ";ABegin;BBegin")
        check(reply is not None and "ERR" not in reply, "Two-axis MoveAbs")
        return "Accepted; check InTargetStat on A and B"
    if operation == "PegArm":
        position, = numbers(1)
        check(read(axis, am.KEYWORD_MOTION_STAT) == 0, "Stop the axis first")
        command(letter + "EventOn=0")
        command(letter + "EventCntr=0")
        am.set_single_event_peg(h, axis, position, 1, 0, 50)
        check(command(letter + "EventOn").strip() == "1", "PEG not enabled")
        return "PEG armed; check EventCntr after crossing the position"
    if operation == "PegStop":
        command(letter + "EventOn=0")
        return "PEG disabled"
    if operation == "StopAB":
        stopped = [am.stop(h, axis_id) for axis_id in (1, 2)]
        check(all(stopped), "Stop A/B failed")
        return "Stop A/B accepted"
    if operation == "VectorArc":
        x, y, cx, cy, direction = numbers(5)
        check(direction in (0, 1), "Direction must be 0 or 1")
        ready(1, 2)
        for member in (1, 2):
            check(abs(read(member, am.KEYWORD_POS_REF) - read(member, am.KEYWORD_POS)) <= 1,
                  "Arc start unsettled: check Pos and PosRef")
        check(am.vector_arc(h, 1, 1, 2, cx, cy, x, y, direction, 0,
                            1000, 10000, 10000, 100000, 4), "Vector Arc")
        return "Accepted; check InTargetStat on A and B"
    if operation == "VectorLine":
        goals = numbers(2)
        ready(1, 2)
        check(am.vector_linear_with_mask(h, 1, 3, 1000, 10000, 10000, 100000, 4, goals),
              "Vector Linear")
        return "Accepted; check InTargetStat on A and B"
    if operation in ("VectorPause", "VectorResume", "VectorStop"):
        action = {"VectorPause": am.vector_pause, "VectorResume": am.vector_continue,
                  "VectorStop": am.stop}[operation]
        check(action(h, 1), operation)
        return "Vector command accepted"
    if operation == "SetPosition":
        value, = numbers(1)
        check(read(axis, am.KEYWORD_MOTION_STAT) == 0, "Stop the axis first")
        check(am.set_position(h, axis, value), "SetPosition")
        return f"Pos={command(signal)}"
    if operation == "GenData":
        index, = numbers(1)
        check(index >= 0, "Enter a nonnegative index")
        return command(f"{letter}GenData[{index}]")
    if operation == "TwoSpeed":
        distance, change = numbers(2)
        check(0 < change < distance, "Require 0 < switch distance < positive move distance")
        ready(axis)
        am.move_rel_two_speed(h, axis, distance, 2000, 1000, change, 10000, 10000, -1)
        return "Two-speed request sent; check status and error log"

    if operation in ("ReadInput", "SetOutput", "ClearOutput"):
        bit, = numbers(1)
        check(1 <= bit <= 32, "I/O index must be 1–32")
        if operation == "ReadInput":
            value = int(read(axis, am.KEYWORD_DIN_PORT))
            return f"Input {bit} = {(value >> (bit - 1)) & 1}"
        action = am.d_out_set_bit if operation == "SetOutput" else am.d_out_clear_bit
        check(action(h, axis, bit), operation)
        return f"Output {bit} updated"

    if operation == "LockStart":
        am.lock_enable(h, axis)
        return "Lock enable requested"
    if operation == "LockStop":
        am.lock_dis_enable(h, axis)
        return "Lock disable requested"
    if operation == "ProgramRun":
        thread, task = numbers(2)
        check(am.prog_run(h, axis, thread, task), "ProgRun")
        return "Program start accepted"
    if operation == "ProgramHaltAll":
        check(am.prog_halt_all(h, axis), "ProgHaltAll")
        return "Program halt-all accepted"
    if operation == "ProgramHalt":
        thread, = numbers(1)
        check(am.prog_halt(h, axis, thread), "ProgHalt")
        return "Program halt accepted"

    reads = {"LockRead": ("LockCntr", "LockVal"), "EventStatus": ("EventOn", "EventCntr"),
             "MotionConfig": ("MotionMode", "CtrlMode"), "CiStatus": ("CIGlobalStat",)}
    if operation in reads:
        return ", ".join(f"{key}={command((letter if operation != 'CiStatus' else '') + key)}"
                         for key in reads[operation])
    if operation == "ReadCommand":
        check(arguments and not any(c in arguments for c in ";\r\n"), "Enter one text command")
        return command(arguments)
    raise RuntimeError(operation)


def save(prefix, extension, content):
    check(content, "No data returned")
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", prefix=prefix + "-",
                                     suffix="." + extension, delete=False) as output:
        output.write(content)
        return f"Saved {output.name}"
