using System;
using System.Threading.Tasks;
using AAMotion;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] == args[1])
        {
            Console.Error.WriteLine("Usage: MultiController <first-ip> <second-ip>");
            return 1;
        }

        Task<bool> first = Task.Run(() => ReadPosition(args[0]));
        Task<bool> second = Task.Run(() => ReadPosition(args[1]));
        Task.WaitAll(first, second);
        return first.Result && second.Result ? 0 : 2;
    }

    private static bool ReadPosition(string ip)
    {
        MotionController controller = AAMotionAPI.Initialize(ControllerType.AGM800);
        try
        {
            if (!AAMotionAPI.Connect(controller, ip))
            {
                Console.Error.WriteLine($"{ip}: connection failed");
                return false;
            }

            Console.WriteLine($"{ip}: APos={controller.GetAxis(AxisRef.A).Pos}");
            return true;
        }
        catch (ControllerCommandException error)
        {
            Console.Error.WriteLine($"{ip}: {error.Command} ERR {error.ErrorCode}");
            return false;
        }
        finally
        {
            AAMotionAPI.Disconnect(controller);
        }
    }
}
