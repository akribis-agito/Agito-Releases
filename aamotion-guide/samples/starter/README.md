# Workbench starter sources

These AGM800 starter projects support axis A or B. The C# project supports `inputs`, `move` and untriggered `record`; native C/C++ and Python support `inputs` and `move`. The browser wizard packages these sources with the selected scenario, instructions, Python binding and a source manifest. SDK binaries are not bundled.

校验不连接设备；实际执行需要显式地址与 `--execute`。教学单位不会自动转换成机器用户单位。运动工程只验证已有换相，不自动换相或回零；运行前由设备流程确认互锁与运动范围。停止、关使能、断开各自尝试清理；通信失败时不能据此确认物理停机。采集工程不发运动命令。

Validation does not connect to hardware. Actual execution requires an explicit address and `--execute`. Teaching units do not automatically convert to machine user units. Motion projects check existing commutation; they do not automatically commutate or home. Verify machine interlocks and travel using your setup process first. Stop, MotorOff and Disconnect are attempted separately; a failed connection does not establish physical standstill. The recording project sends no move.

From a downloaded project directory:

| Language | Validate | Execute against an explicit configured target |
|---|---|---|
| C# (.NET SDK 10) | `dotnet run -- --validate` | `dotnet run -- <address> --execute` |
| Python (64-bit 3.10+) | `python main.py --validate` | set `AAMOTION_SDK_DIR`, then `python main.py <address> --execute` |
| C/C++ (CMake 3.20+) | `cmake -S . -B build`, `cmake --build build`, then `./build/starter_validate --validate` | configure with `-DAAMOTION_SDK_DIR=<extracted SDK>`, build, then `./build/starter <address> --execute` |

Windows Visual Studio builds put executables in `build/Debug/`; use `./build/Debug/starter_validate.exe --validate` and `./build/Debug/starter.exe <address> --execute`. Use an x64 generator. Pass `-DSTARTER_CPP=ON` for C++ mode. The native project must be rebuilt after editing `scenario.h`. C# reads `scenario.json` copied beside its executable; rebuild after editing. Python reads its sibling `scenario.json`.

On Linux and macOS the native SDK uses the matching runtime folder in the archive. Native starter execution is intended for desktop x64 Linux, x64 Windows and the SDK's macOS architectures. Build/validation here does not establish runtime compatibility on every platform. Python uses the guide's existing Windows/Linux wrapper.

The timeout bounds the wait/polling loop; a blocking transport call may extend wall-clock completion. Consult the guide's connection, point-to-point and recording chapters for per-command timeout semantics. Inspect the SDK error hook output and the AACommServer log when a call fails.
