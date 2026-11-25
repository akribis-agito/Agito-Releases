// 01-Connect: connect to a controller or to the firmware simulator, read the position, disconnect.
//   01-Connect.exe 172.1.1.101                         real controller
//   01-Connect.exe "D:\...\firmware_sim.exe" AGM800   firmware simulator (absolute path)
using System;
using System.IO;
using AAComm.Services;
using AAMotion;

namespace Sample01
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string target = args.Length > 0 ? args[0] : "172.1.1.101";
            ControllerType type = ControllerType.AGM800;
            if (args.Length > 1 && !Enum.TryParse(args[1], true, out type))
            {
                Console.WriteLine("Unknown controller type: " + args[1]);
                return 2;
            }

            // --8<-- [start:init]
            LocaleProvider.Instance.Locale = "zh-CN";
            MotionController c = AAMotionAPI.Initialize(type);
            c.ErrorOccurred += (code, sent, text) =>
                Console.WriteLine($"[ERR {code}] {sent} | {text.Replace("\n", " | ")}");
            // --8<-- [end:init]

            // --8<-- [start:connect]
            bool ok;
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                if (!Path.IsPathRooted(target) || !File.Exists(target))
                {
                    Console.WriteLine("The simulator path must be an absolute path to firmware_sim.exe");
                    return 2;
                }
                c.Init();
                ok = AAMotionAPI.ConnectSimulation(c, target);
            }
            else
            {
                ok = AAMotionAPI.Connect(c, target);
            }
            Console.WriteLine($"Connect {target}: {ok}");
            if (!ok)
                return 1;
            // --8<-- [end:connect]

            try
            {
                // --8<-- [start:read]
                Console.WriteLine("Firmware: " + c.CommAPI.IdentityContainer.Versions.FirmwareVersionStr);
                ISingleAxis a = c.GetAxis(AxisRef.A);
                Console.WriteLine($"APos = {a.Pos}");
                // --8<-- [end:read]
            }
            finally
            {
                // --8<-- [start:disconnect]
                AAMotionAPI.Disconnect(c);
                // --8<-- [end:disconnect]
            }
            return 0;
        }
    }
}
