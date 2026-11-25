using System;
using AAMotion;
using System.Collections.Generic;
using AAComm.Services;
// 扫描网段 / Scan the network
// 不连接控制器。
internal static class Program
{

    private static int Main()
    {
        try
        {
            // --8<-- [start:operation]
            // 改成控制器所在网段的前三段
            const string subnet = "172.1.1";
            // false：ARP 请求间隔 50 ms（约 13 s），任何固件都安全
            List<string> ips = AAMotionAPI.ScanController(false, subnet);
            foreach (string ip in ips)
                Console.WriteLine("controller at " + ip);
            List<ConnectionData> found =
                AAMotionAPI.ScanControllerDetailed(false, subnet, 100, 120);
            Console.WriteLine(
                found.Count + " controller(s) between .100 and .120");
            // --8<-- [end:operation]
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return 5;
        }
    }
}
