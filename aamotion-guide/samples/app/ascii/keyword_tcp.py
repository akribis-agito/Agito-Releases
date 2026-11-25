"""Direct TCP ASCII keyword demo. The default operations only read."""
import argparse
import socket


def receive_exact(sock, size):
    data = bytearray()
    while len(data) < size:
        try:
            chunk = sock.recv(size - len(data))
        except TimeoutError:
            if len(data) == 4 and data[0] == 0 and data[-1] == 62:
                raise RuntimeError(f"Controller error {int.from_bytes(data[1:3], 'big')}") from None
            raise
        if not chunk:
            if len(data) == 4 and data[0] == 0 and data[-1] == 62:
                raise RuntimeError(f"Controller error {int.from_bytes(data[1:3], 'big')}")
            raise ConnectionError("Controller closed the connection")
        data.extend(chunk)
    return bytes(data)


def send(sock, prefix, commands):
    sock.sendall((prefix + ";".join(commands)).encode("ascii") + b"\0")


def read_int(sock, keyword):
    send(sock, "A", [keyword])
    reply = receive_exact(sock, 6)
    if reply[0] != 0 or reply[-1] != 62:
        raise ValueError("Unexpected binary reply: " + reply.hex(" "))
    return int.from_bytes(reply[1:5], "big", signed=True)


def batch(sock, commands):
    send(sock, "L", commands)
    results = []
    for _ in commands:
        field = bytearray()
        while (byte := receive_exact(sock, 1)) != b">":
            field.extend(byte)
            if len(field) > 128:
                raise ValueError("Oversize ASCII field")
        results.append(field.decode("ascii").strip())
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("host")
    parser.add_argument("--port", type=int, default=50000)
    parser.add_argument("--write", nargs=2, type=int, metavar=("GENDATA_INDEX", "VALUE"))
    args = parser.parse_args()
    with socket.create_connection((args.host, args.port), timeout=2) as sock:
        print("ASpeed =", read_int(sock, "ASpeed"))
        print("AMotorOn, AMotionStat =", batch(sock, ["AMotorOn", "AMotionStat"]))
        if args.write:
            index, value = args.write
            if not (1 <= index <= 65535 and -(2**31) <= value < 2**31):
                parser.error("Use a valid index and a signed 32-bit value")
            key = f"AGenData[{index}]"
            print("Before =", read_int(sock, key))
            reply = batch(sock, [f"{key}={value}"])
            if reply != ["OK"]:
                raise RuntimeError(f"Write reply: {reply}")
            if read_int(sock, key) != value:
                raise RuntimeError("Readback mismatch")
            print("Verified", key, "=", value)


if __name__ == "__main__":
    main()
