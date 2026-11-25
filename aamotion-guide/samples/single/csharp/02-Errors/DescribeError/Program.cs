using System;
using AAMotion;
using AAComm.Services;
// 错误码转成文字 / Error code to text
// 不连接控制器。
internal static class Program
{

    private static int Main()
    {
        try
        {
            // --8<-- [start:operation]
            LocaleProvider.Instance.Locale = "en-US";   // "zh-CN" 返回中文说明
            // 与错误钩子收到的文字相同
            string text = LocaleProvider.Instance.GetError(31);
            Console.WriteLine("ERR 31: " + text);
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
