// Examples for the "Initialization and connection" chapter. Each method is one example; the manual shows the
// part between its snippet markers.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AAComm.Services;
using AAComm.Shared;
using AAMotion;

namespace ApiSamples
{
    public static class Lifecycle
    {
        public static MotionController Initialize()
        {
            // --8<-- [start:initialize]
            MotionController c = AAMotionAPI.Initialize(ControllerType.AGM800);
            // c is ready to connect; the AACommServer has been started if it was not running
            // --8<-- [end:initialize]
            return c;
        }

        public static void Init(MotionController c)
        {
            // --8<-- [start:init]
            c.Init();   // start AACommServer if it is not running; does nothing if it is

            // or start a specific server executable and know whether it is serving
            bool serving = c.EnsureAACommServerRunning(@"C:\MyApp\aacommserver\AACommServer.exe");
            // --8<-- [end:init]
        }

        public static bool Connect(MotionController c)
        {
            // --8<-- [start:connect]
            bool ok = AAMotionAPI.Connect(c, "172.1.1.101");
            if (!ok)
            {
                Console.WriteLine("Connect failed: check IP, cable, subnet and the AACommServer log");
                return false;
            }

            // with PC-side TCP immediate ACK enabled (third argument false)
            // bool ok2 = AAMotionAPI.Connect(c, "172.1.1.101", false, false);
            // --8<-- [end:connect]
            return true;
        }

        public static bool ConnectSimulation(MotionController c)
        {
            // --8<-- [start:connect-simulation]
            c.Init();   // ConnectSimulation needs the server already running
            bool ok = AAMotionAPI.ConnectSimulation(c, @"D:\sim\AGM800\firmware_sim.exe");

            // declare the product so no discovery probe runs
            // bool ok2 = AAMotionAPI.ConnectSimulation(c, @"D:\sim\AGM800\firmware_sim.exe", "AGM800", null);
            // --8<-- [end:connect-simulation]
            return ok;
        }

        public static void Disconnect(MotionController c)
        {
            // --8<-- [start:disconnect]
            try
            {
                // ... work with the controller ...
            }
            finally
            {
                AAMotionAPI.Disconnect(c);   // the AACommServer keeps running for other clients
            }
            // --8<-- [end:disconnect]
        }

        public static void IsConnected(MotionController c)
        {
            // --8<-- [start:is-connected]
            if (!AAMotionAPI.IsConnected(c))
                Console.WriteLine("Not connected");
            // --8<-- [end:is-connected]
        }

        public static async Task Watchdog(MotionController c)
        {
            // --8<-- [start:check-connection]
            // a background health check: never on the UI thread, never in a tight loop
            while (true)
            {
                await Task.Delay(2000);
                bool reachable = await Task.Run(() => AAMotionAPI.CheckConnection(c));

                // one command round trip: is the controller itself answering?
                ConnectionProbeResult probe = await Task.Run(() => AAMotionAPI.ProbeConnection(c));
                if (probe.IsAlive && probe.ControllerRestarted)
                    Console.WriteLine("The controller restarted since the last probe");
                else if (!probe.IsAlive)
                    Console.WriteLine($"No answer ({probe.Failure}): {probe.Message}");

                if (!reachable || !AAMotionAPI.IsConnected(c))
                {
                    bool back = await Task.Run(() => AAMotionAPI.TryReconnect(c));
                    Console.WriteLine(back ? "Reconnected" : "Controller still unreachable");
                }
            }
            // --8<-- [end:check-connection]
        }

        public static void Reconnect(MotionController c)
        {
            // --8<-- [start:try-reconnect]
            // null address: the last address that connected successfully
            bool ok = AAMotionAPI.TryReconnect(c);

            // explicit address, with PC-side immediate ACK enabled
            bool ok2 = AAMotionAPI.TryReconnect(c, "172.1.1.101", false);
            // --8<-- [end:try-reconnect]
        }

        public static void Timeout(MotionController c)
        {
            // --8<-- [start:timeout]
            AAMotionAPI.SetDefaultTimeout(c, 5000);       // 10 – 60000 ms
            int ms = AAMotionAPI.GetDefaultTimeout(c);

            // raise it only around a slow step, then restore
            int saved = c.GetDefaultTimeout();
            c.SetDefaultTimeout(20000);
            try
            {
                c.SendCommandString("ASave", out string reply);
            }
            finally
            {
                c.SetDefaultTimeout(saved);
            }
            // --8<-- [end:timeout]
        }

        public static void Scan()
        {
            // --8<-- [start:scan]
            // fastScan false: ARP requests 50 ms apart, safe for older drives
            List<string> ips = AAMotionAPI.ScanController(false, "172.1.1");
            foreach (string ip in ips)
                Console.WriteLine("Controller at " + ip);

            List<ConnectionData> found = AAMotionAPI.ScanControllerDetailed(false, "172.1.1", 100, 120);
            Console.WriteLine(found.Count + " controller(s) between .100 and .120");
            // --8<-- [end:scan]
        }

        public static void Shutdown(MotionController c)
        {
            // --8<-- [start:shutdown]
            // disconnects c, then asks the shared AACommServer to exit;
            // the server ignores the request while another program still uses it
            AAMotionAPI.Shutdown(c);
            // --8<-- [end:shutdown]
        }

        public static void Dispose(MotionController c)
        {
            // --8<-- [start:dispose]
            AAMotionAPI.Dispose(c);   // releases this controller's resources; do not use c afterwards
            // --8<-- [end:dispose]
        }
    }
}
