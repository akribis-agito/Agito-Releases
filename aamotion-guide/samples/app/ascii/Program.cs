using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;

if (args.Length != 2 && args.Length != 4)
{
    Console.Error.WriteLine("Usage: KeywordTcp HOST PORT [GENDATA_INDEX VALUE]");
    return 2;
}
// Wire numbers are ASCII: no culture's minus sign or digits.
var inv = CultureInfo.InvariantCulture;
using var tcp = new TcpClient();
tcp.ConnectAsync(args[0], int.Parse(args[1], inv)).WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
using var stream = tcp.GetStream();
stream.ReadTimeout = stream.WriteTimeout = 2000;

void Send(string prefix, params string[] commands)
{
    stream.Write(Encoding.ASCII.GetBytes(prefix + string.Join(";", commands) + "\0"));
}

int ReadInt(string keyword)
{
    Send("A", keyword);
    byte[] reply = new byte[6];
    int size = 0;
    try
    {
        while (size < reply.Length)
        {
            int count = stream.Read(reply, size, reply.Length - size);
            if (count == 0) throw new EndOfStreamException();
            size += count;
        }
    }
    catch (IOException error) when (size == 4 && reply[0] == 0 && reply[3] == '>')
    {
        throw new InvalidDataException($"Controller error {BinaryPrimitives.ReadUInt16BigEndian(reply.AsSpan(1, 2))}", error);
    }
    if (reply[0] != 0 || reply[5] != '>') throw new InvalidDataException("Unexpected binary reply");
    return BinaryPrimitives.ReadInt32BigEndian(reply.AsSpan(1, 4));
}

string[] Batch(params string[] commands)
{
    Send("L", commands);
    string[] values = new string[commands.Length];
    for (int i = 0; i < values.Length; i++)
    {
        var field = new StringBuilder();
        int next;
        while ((next = stream.ReadByte()) != '>')
        {
            if (next < 0) throw new EndOfStreamException();
            if (field.Length == 128 || next > 127) throw new InvalidDataException("Invalid ASCII reply");
            field.Append((char)next);
        }
        values[i] = field.ToString().Trim();
    }
    return values;
}

Console.WriteLine($"ASpeed = {ReadInt("ASpeed")}");
Console.WriteLine("AMotorOn, AMotionStat = " + string.Join(", ", Batch("AMotorOn", "AMotionStat")));
if (args.Length == 4)
{
    if (!ushort.TryParse(args[2], NumberStyles.None, inv, out ushort index) || index == 0
        || !int.TryParse(args[3], NumberStyles.AllowLeadingSign, inv, out int value))
        throw new ArgumentException("Use a valid index and a signed 32-bit value");
    string key = string.Create(inv, $"AGenData[{index}]");
    Console.WriteLine($"Before = {ReadInt(key)}");
    if (Batch(string.Create(inv, $"{key}={value}"))[0] != "OK") throw new InvalidDataException("Write refused");
    if (ReadInt(key) != value) throw new InvalidDataException("Readback mismatch");
    Console.WriteLine($"Verified {key} = {value}");
}
return 0;
