#include <winsock2.h>
#include <ws2tcpip.h>
#include <array>
#include <cstdint>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <string>
#include <vector>
#pragma comment(lib, "Ws2_32.lib")

struct Connection
{
    WSADATA data{};
    SOCKET socket = INVALID_SOCKET;

    Connection()
    {
        if (WSAStartup(MAKEWORD(2, 2), &data)) throw std::runtime_error("WSAStartup");
    }
    ~Connection()
    {
        if (socket != INVALID_SOCKET) closesocket(socket);
        WSACleanup();
    }
    void connect(const char* host, int port)
    {
        sockaddr_in address{};
        address.sin_family = AF_INET;
        address.sin_port = htons(static_cast<unsigned short>(port));
        if (InetPtonA(AF_INET, host, &address.sin_addr) != 1) throw std::runtime_error("Enter an IPv4 address");
        socket = ::socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        DWORD timeout = 2000;
        if (socket == INVALID_SOCKET ||
            setsockopt(socket, SOL_SOCKET, SO_RCVTIMEO, reinterpret_cast<const char*>(&timeout), sizeof timeout) ||
            setsockopt(socket, SOL_SOCKET, SO_SNDTIMEO, reinterpret_cast<const char*>(&timeout), sizeof timeout) ||
            ::connect(socket, reinterpret_cast<const sockaddr*>(&address), sizeof address))
            throw std::runtime_error("Connect failed");
    }
    void send_command(char prefix, const std::vector<std::string>& commands)
    {
        std::string frame(1, prefix);
        for (size_t i = 0; i < commands.size(); ++i)
        {
            if (i) frame += ';';
            frame += commands[i];
        }
        frame += '\0';
        size_t offset = 0;
        while (offset < frame.size())
        {
            int n = send(socket, frame.data() + offset, static_cast<int>(frame.size() - offset), 0);
            if (n <= 0) throw std::runtime_error("Send failed");
            offset += n;
        }
    }
    int read_int(const std::string& key)
    {
        send_command('A', {key});
        std::array<unsigned char, 6> reply{};
        size_t size = 0;
        while (size < reply.size())
        {
            int n = recv(socket, reinterpret_cast<char*>(reply.data() + size),
                         static_cast<int>(reply.size() - size), 0);
            if (n <= 0)
            {
                if (size == 4 && reply[0] == 0 && reply[3] == '>')
                    throw std::runtime_error("Controller error " + std::to_string((reply[1] << 8) | reply[2]));
                throw std::runtime_error("Incomplete binary reply");
            }
            size += n;
        }
        if (reply[0] != 0 || reply[5] != '>') throw std::runtime_error("Unexpected binary reply");
        uint32_t value = 0;
        for (int i = 1; i <= 4; ++i) value = (value << 8) | reply[i];
        return static_cast<int>((value & 0x80000000u) ? int64_t(value) - 0x100000000LL : value);
    }
    std::vector<std::string> batch(const std::vector<std::string>& commands)
    {
        send_command('L', commands);
        std::vector<std::string> values;
        for (size_t i = 0; i < commands.size(); ++i)
        {
            std::string field;
            for (char byte; ; )
            {
                if (recv(socket, &byte, 1, 0) != 1) throw std::runtime_error("Incomplete ASCII reply");
                if (byte == '>') break;
                if (field.size() == 128 || static_cast<unsigned char>(byte) > 127)
                    throw std::runtime_error("Invalid ASCII field");
                field += byte;
            }
            values.push_back(field);
        }
        return values;
    }
};

static int integer(const char* text)
{
    size_t end;
    int n = std::stoi(text, &end);
    if (text[end]) throw std::runtime_error("Expected an integer");
    return n;
}

int main(int argc, char** argv)
{
    if (argc != 3 && argc != 5)
    {
        std::cerr << "Usage: keyword_tcp IPV4 PORT [GENDATA_INDEX VALUE]\n";
        return 2;
    }
    try
    {
        int port = integer(argv[2]);
        if (port < 1 || port > 65535) throw std::runtime_error("Invalid port");
        Connection client;
        client.connect(argv[1], port);
        std::cout << "ASpeed = " << client.read_int("ASpeed") << '\n';
        auto state = client.batch({"AMotorOn", "AMotionStat"});
        std::cout << "AMotorOn, AMotionStat = " << state[0] << ", " << state[1] << '\n';
        if (argc == 5)
        {
            int index = integer(argv[3]), value = integer(argv[4]);
            if (index < 1 || index > 65535) throw std::runtime_error("Invalid GenData index");
            std::string key = "AGenData[" + std::to_string(index) + "]";
            std::cout << "Before = " << client.read_int(key) << '\n';
            if (client.batch({key + "=" + std::to_string(value)})[0] != "OK")
                throw std::runtime_error("Write refused");
            if (client.read_int(key) != value) throw std::runtime_error("Readback mismatch");
            std::cout << "Verified " << key << " = " << value << '\n';
        }
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
