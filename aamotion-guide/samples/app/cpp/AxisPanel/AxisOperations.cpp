#include "AxisOperations.h"
#include <cmath>
#include <filesystem>
#include <fstream>
#include <initializer_list>
#include <iomanip>
#include <locale>
#include <limits>
#include <sstream>
#include <stdexcept>
#include <vector>
#include <windows.h>

static void Check(bool ok, const std::string& message)
{
    if (!ok) throw std::runtime_error(message);
}

static int Integer(const std::string& text)
{
    size_t end;
    int value = std::stoi(text, &end);
    Check(text.find_first_not_of(" \r\n\t", end) == std::string::npos, "Expected an integer");
    return value;
}

static std::string Save(const std::string& content, const wchar_t* extension)
{
    Check(!content.empty(), "No data returned");
    wchar_t folder[MAX_PATH], file[MAX_PATH];
    DWORD length = GetTempPathW(MAX_PATH, folder);
    Check(length && length < MAX_PATH && GetTempFileNameW(folder, L"aam", 0, file), "Create temporary file");
    std::filesystem::path path(file);
    auto named = path;
    named.replace_extension(extension);
    if (!MoveFileW(path.c_str(), named.c_str()))
    {
        DeleteFileW(file);
        throw std::runtime_error("Name recording file");
    }
    path = named;
    std::ofstream output(path);
    output << content;
    output.close();
    Check(static_cast<bool>(output), "Write recording");
    return "Saved " + path.u8string();
}

std::string RunOperation(aamotion_controller h, int axis, const std::string& operation, const std::string& input)
{
    using std::to_string;
    std::string letter(1, axis == 1 ? 'A' : axis == 2 ? 'B' : axis == 4 ? 'C' : 'D');
    std::string signal = letter + "Pos";
    auto command = [&](const std::string& text) {
        char reply[4096] = {};
        Check(aamotion_send_command_string(h, text.c_str(), reply, sizeof reply), text);
        std::string result(reply);
        auto first = result.find_first_not_of(" \r\n\t");
        if (first == std::string::npos) return std::string();
        Check(result.compare(first, 3, "ERR") != 0, text + ": " + result);
        return result.substr(first, result.find_last_not_of(" \r\n\t") - first + 1);
    };
    auto ready = [&](const std::string& id) {
        Check(command(id + "MotorOn") == "1" && command(id + "MotionStat") == "0",
              "Enable each idle axis first");
    };
    auto numbers = [&](size_t count) {
        std::vector<int> values;
        std::istringstream parts(input);
        for (std::string part; std::getline(parts, part, ',');) values.push_back(Integer(part));
        Check(values.size() == count && input.back() != ',', "Enter comma-separated 32-bit integers");
        return values;
    };
    auto read = [&](std::initializer_list<const char*> keys) {
        std::string result;
        for (auto key : keys)
        {
            if (!result.empty()) result += ", ";
            result += std::string(key) + "=" + command(letter + key);
        }
        return result;
    };

    if (operation == "SineStopRep") command(letter + "StopRep");
    else if (operation == "SinePtp" || operation == "SineRepeat" ||
             operation == "SinePreCruise" || operation == "SineCommit")
    {
        bool commit = operation == "SineCommit", repeat = operation == "SineRepeat";
        auto args = operation == "SinePreCruise" ? numbers(3) : std::vector<int>{numbers(1)[0], 0, 0};
        if (commit)
        {
            std::string mode = command(letter + "MotionMode");
            Check((mode == "20" || mode == "21") && command(letter + "MotionStat") != "0", "Start Sine PTP first");
        }
        else
        {
            ready(letter);
            if (operation == "SinePreCruise")
            {
                double start = std::stod(command(letter + "PosRef"));
                Check((start < args[1] && args[1] < args[0] || args[0] < args[1] && args[1] < start) &&
                      args[2] >= 2000, "Pre-cruise point must be between start and target; speed at least 2000");
            }
            command(letter + (repeat ? "MotionMode=21" : "MotionMode=20"));
            if (repeat) command(letter + "RptMode=0");
        }
        for (const auto& parameter : {"RelTrgt=0", "Speed=2000", "Accel=10000", "Decel=10000",
             "JerkInAcc=100000", "JerkInDec=100000", "PreCruRelTrgt=0"})
            command(letter + parameter);
        command(letter + "AbsTrgt=" + to_string(args[0]));
        command(letter + "PreCruAbsTrgt=" + to_string(args[1]));
        command(letter + "PreCruiseSpd=" + to_string(args[2]));
        if (commit) command("ACommitMotion=" + to_string(axis));
        else Check(aamotion_begin(h, axis), "Sine PTP Begin");
    }
    else if (operation == "Pvaj")
    {
        std::istringstream fields(input);
        std::string travelText, gapText, periodText;
        Check(static_cast<bool>(std::getline(fields, travelText, ',')) &&
              static_cast<bool>(std::getline(fields, gapText, ',')) &&
              static_cast<bool>(std::getline(fields, periodText)), "Enter distance, gap and period");
        int travel = Integer(travelText), sampleGap = Integer(gapText);
        std::istringstream periodInput(periodText);
        periodInput.imbue(std::locale::classic());
        double period;
        Check(static_cast<bool>(periodInput >> period) && (periodInput >> std::ws).eof() &&
              travel && sampleGap >= 1 && sampleGap <= 32 && std::isfinite(period) && period > 0,
              "Check distance, gap and period");
        double dt = sampleGap * (period / 1e6);
        double jerk = travel / (2 * dt * dt * dt);
        Check(jerk != 0 && std::isfinite(jerk), "Invalid PVAJ jerk");
        ready(letter);
        std::vector<double> table{5, static_cast<double>(sampleGap), 1};
        double p = 0, v = 0, a = 0;
        for (double j : {jerk, -jerk, -jerk, jerk})
        {
            table.insert(table.end(), {p, v, a, j});
            p += v * dt + a * dt * dt / 2 + j * dt * dt * dt / 6;
            v += a * dt + j * dt * dt / 2;
            a += j * dt;
        }
        table.insert(table.end(), {static_cast<double>(travel), 0, 0, 0});
        command(letter + "PVAJArm=0");
        for (size_t i = 0; i < table.size(); ++i)
        {
            std::ostringstream value;
            value.imbue(std::locale::classic());
            value << std::setprecision(17) << table[i];
            command(letter + "PVAJList[" + to_string(i + 1) + "]=" + value.str());
        }
        command(letter + "PVAJValidate=1");
        Check(command(letter + "PVAJStatus[1]") == "1", "PVAJ validation failed");
        command(letter + "MotionMode=22");
        command(letter + "PVAJArm=1");
        if (!aamotion_begin(h, axis))
        {
            command(letter + "PVAJArm=0");
            Check(false, "PVAJ Begin failed");
        }
    }
    else if (operation == "PvajStatus") return read({"PVAJStatus[1]", "PVAJStatus[2]", "PVAJStatus[3]"});
    else if (operation == "DataLogger")
    {
        Check(aamotion_data_rec_record(h, signal.c_str(), 1, 1, 0, 0, nullptr, 0, 0, false, 5000), "Record");
        int length, gap, vectors, samples;
        Check(aamotion_data_rec_info(h, &length, &gap, &vectors, &samples), "Read recording info");
        Check(vectors == 1 && samples > 0, "No samples");
        std::vector<double> time(samples), values(samples);
        int timeCount, valueCount;
        Check(aamotion_data_rec_time(h, time.data(), samples, &timeCount) &&
              aamotion_data_rec_vector_values(h, 0, values.data(), samples, &valueCount) &&
              timeCount == valueCount && valueCount > 0, "Read recording vectors");
        std::ostringstream csv;
        csv << "time_s," << signal << '\n';
        for (int i = 0; i < valueCount; ++i) csv << time[i] << ',' << values[i] << '\n';
        return Save(csv.str(), L".csv");
    }
    else if (operation == "SlowStart")
    {
        Check(command("LoggerOn") == "0", "SlowLogger is already on");
        const char* signals[]{signal.c_str()};
        Check(aamotion_configure_slow_logger(h, 10, 0, signals, 1), "ConfigureSlowLogger");
        Check(aamotion_set_logger_on(h, 1), "Start SlowLogger");
    }
    else if (operation == "SlowUpload")
    {
        std::vector<char> data(65536);
        Check(aamotion_logger_upload(h, data.data(), static_cast<int>(data.size())), "Upload SlowLogger");
        return Save(data.data(), L".txt");
    }
    else if (operation == "SlowStop") Check(aamotion_set_logger_on(h, 0), "Stop SlowLogger");
    else if (operation == "RecorderStatus") return "Lost packets: " + to_string(aamotion_get_logger_lost_packets(h));
    else if (operation == "CncFile")
    {
        auto file = std::filesystem::u8path(input);
        Check(file.is_absolute() && std::filesystem::is_regular_file(file), "Enter an existing absolute file path");
        ready("A");
        ready("B");
        aamotion_run_cnc_program_from_file(h, input.c_str());
    }
    else if (operation == "CncLine" || operation == "CncArc" || operation == "CncOutput")
    {
        const bool arc = operation == "CncArc";
        auto args = numbers(arc ? 5 : 2);
        Check(arc ? args[4] == 0 || args[4] == 1 : args[0] || args[1], "Check displacement or arc direction");
        ready("A");
        ready("B");
        int x = Integer(command("APos")), y = Integer(command("BPos"));
        auto target = [](int position, int distance) {
            long long value = static_cast<long long>(position) + distance;
            Check(value >= (std::numeric_limits<int>::min)() && value <= (std::numeric_limits<int>::max)(),
                  "CNC target range");
            return static_cast<int>(value);
        };
        int tx = arc ? args[0] : target(x, args[0]);
        int ty = arc ? args[1] : target(y, args[1]);
        Check(aamotion_clear_buffer(h), "CNC ClearBuffer");
        Check(aamotion_set_start_positions(h, 3, x, y, 0), "CNC SetStartPositions");
        Check(aamotion_set_motion_profile(h, 100, 10000, 10000, 0), "CNC SetMotionProfile");
        if (arc)
            Check(aamotion_arc_xy(h, tx, ty, args[2], args[3], args[4], 1000, 0), "CNC ArcXY");
        else
            Check(aamotion_linear_absolute_xy(h, tx, ty, 1000, 0), "CNC LinearAbsoluteXY");
        if (operation == "CncOutput")
        {
            Check(aamotion_group_d_out_set_bit(h, 1, 1), "Queue output set");
            Check(aamotion_delay(h, 200), "Queue delay");
            Check(aamotion_group_d_out_clear_bit(h, 1, 1), "Queue output clear");
            Check(aamotion_linear_absolute_xy(h, x, y, 1000, 0), "Queue return line");
        }
        command("AMotionMode=11");
        command("BMotionMode=11");
        Check(aamotion_cnc_begin(h), "CNC Begin");
    }
    else if (operation == "CncPause") Check(aamotion_pause(h), operation);
    else if (operation == "CncResume") Check(aamotion_resume(h), operation);
    else if (operation == "CncStop") Check(aamotion_group_stop(h), operation);
    else if (operation == "CncStatus") return std::string("CNC complete: ") + (aamotion_is_cnc_completed(h, 1) ? "yes" : "no");
    else if (operation == "GearDirect" || operation == "GearIndirect")
    {
        auto gear = numbers(2);
        Check(gear[0] >= 0 && gear[1], "Enter master CAN code and nonzero numerator");
        ready(letter);
        command(letter + "GearMaster=" + to_string(gear[0]));
        command(letter + "MasterFact=" + to_string(gear[1]));
        command(letter + "MasterFactDen=65536");
        command(letter + "MasterModRev=0");
        if (operation == "GearDirect") command(letter + "MasterFilt=64");
        command(letter + (operation == "GearDirect" ? "MotionMode=5" : "MotionMode=6"));
        Check(aamotion_begin(h, axis), "Gear Begin");
    }
    else if (operation == "GearStop") Check(aamotion_stop(h, axis), operation);
    else if (operation == "GearStatus") return read({"GearMaster", "MotionMode", "MotionStat"});
    else if (operation == "EcamStart")
    {
        int table = numbers(1)[0];
        Check(table >= 1 && table <= 10, "ECAM table must be 1–10");
        ready(letter);
        std::string index = "[" + to_string(table) + "]";
        int first = Integer(command(letter + "ECAMStart" + index));
        int last = Integer(command(letter + "ECAMEnd" + index));
        Check(first > 0 && last > first, "Configure an ECAM table first");
        command(letter + "ECAMTableNum=" + to_string(table));
        command(letter + "MotionMode=7");
        Check(aamotion_begin(h, axis), "ECAM Begin");
    }
    else if (operation == "EcamStop") command(letter + "StopECAM");
    else if (operation == "EcamStatus")
    {
        int table = numbers(1)[0];
        Check(table >= 1 && table <= 10, "ECAM table must be 1–10");
        return read({"MotionMode", "MotorOn", "MotionStat"}) +
            ", ECAMCycCount=" + command(letter + "ECAMCycCount[" + to_string(table) + "]");
    }
    else if (operation == "MultiAxis")
    {
        auto goals = numbers(2);
        ready("A");
        ready("B");
        auto setup = [](const std::string& id, int position) {
            return id + "MotionMode=1;" + id + "RelTrgt=0;" + id + "Speed=2000;" +
                id + "Accel=10000;" + id + "Decel=10000;" + id + "AbsTrgt=" + to_string(position);
        };
        auto request = setup("A", goals[0]) + ";" + setup("B", goals[1]) + ";ABegin;BBegin";
        char reply[4096] = {};
        Check(aamotion_send_bulk_command_string(h, request.c_str(), reply, sizeof reply) &&
              std::string(reply).find("ERR") == std::string::npos, "Two-axis MoveAbs");
    }
    else if (operation == "PegArm")
    {
        int position = numbers(1)[0];
        Check(command(letter + "MotionStat") == "0", "Stop the axis first");
        command(letter + "EventOn=0");
        command(letter + "EventCntr=0");
        aamotion_set_single_event_peg(h, axis, position, 1, 0, 50);
        Check(command(letter + "EventOn") == "1", "PEG not enabled");
    }
    else if (operation == "PegStop") command(letter + "EventOn=0");
    else if (operation == "StopAB")
    {
        bool stoppedA = aamotion_stop(h, 1);
        bool stoppedB = aamotion_stop(h, 2);
        Check(stoppedA && stoppedB, "Stop A/B failed");
    }
    else if (operation == "VectorArc")
    {
        auto args = numbers(5);
        Check(args[4] == 0 || args[4] == 1, "Direction must be 0 or 1");
        for (const std::string member : {"A", "B"})
        {
            ready(member);
            Check(std::abs(std::stod(command(member + "PosRef")) - std::stod(command(member + "Pos"))) <= 1,
                  "Arc start unsettled: check Pos and PosRef");
        }
        Check(aamotion_vector_arc(h, 1, 1, 2, args[2], args[3], args[0], args[1], args[4], 0,
              1000, 10000, 10000, 100000, 4), "Vector Arc");
    }
    else if (operation == "VectorLine")
    {
        auto goals = numbers(2);
        ready("A");
        ready("B");
        double target[] = {static_cast<double>(goals[0]), static_cast<double>(goals[1])};
        Check(aamotion_vector_linear_with_mask(h, 1, 3, 1000, 10000, 10000, 100000, 4, target, 2),
              "Vector Linear");
    }
    else if (operation == "VectorPause") Check(aamotion_vector_pause(h, 1), operation);
    else if (operation == "VectorResume") Check(aamotion_vector_continue(h, 1), operation);
    else if (operation == "VectorStop") Check(aamotion_stop(h, 1), operation);
    else if (operation == "SetPosition")
    {
        Check(command(letter + "MotionStat") == "0", "Stop the axis first");
        Check(aamotion_set_position(h, axis, numbers(1)[0]), "SetPosition");
        return "Pos=" + command(signal);
    }
    else if (operation == "GenData")
    {
        int index = numbers(1)[0];
        Check(index >= 0, "Enter a nonnegative index");
        return command(letter + "GenData[" + to_string(index) + "]");
    }
    else if (operation == "TwoSpeed")
    {
        auto distance = numbers(2);
        Check(distance[1] > 0 && distance[1] < distance[0], "Require 0 < switch distance < move distance");
        ready(letter);
        aamotion_move_rel_two_speed(h, axis, distance[0], 2000, 1000, distance[1], 10000, 10000, -1);
    }
    else if (operation == "ReadInput" || operation == "SetOutput" || operation == "ClearOutput")
    {
        int bit = numbers(1)[0];
        Check(bit >= 1 && bit <= 32, "I/O index must be 1–32");
        if (operation == "ReadInput")
            return "Input = " + to_string((std::stoll(command(letter + "DInPort")) >> (bit - 1)) & 1);
        Check(operation == "SetOutput" ? aamotion_d_out_set_bit(h, axis, bit) :
              aamotion_d_out_clear_bit(h, axis, bit), operation);
    }
    else if (operation == "LockStart") aamotion_lock_enable(h, axis);
    else if (operation == "LockStop") aamotion_lock_dis_enable(h, axis);
    else if (operation == "LockRead") return read({"LockCntr", "LockVal"});
    else if (operation == "EventStatus") return read({"EventOn", "EventCntr"});
    else if (operation == "MotionConfig") return read({"MotionMode", "CtrlMode"});
    else if (operation == "CiStatus") return command("CIGlobalStat");
    else if (operation == "ProgramRun")
    {
        auto program = numbers(2);
        Check(aamotion_prog_run(h, axis, program[0], program[1]), operation);
    }
    else if (operation == "ProgramHaltAll") Check(aamotion_prog_halt_all(h, axis), operation);
    else if (operation == "ProgramHalt") Check(aamotion_prog_halt(h, axis, numbers(1)[0]), operation);
    else if (operation == "ReadCommand")
    {
        Check(!input.empty() && input.find_first_of(";\r\n") == std::string::npos, "Enter one command");
        return command(input);
    }
    else throw std::runtime_error("Unknown operation: " + operation);
    return operation + " sent; check status and error log";
}
