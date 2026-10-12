#!/usr/bin/env python3
import http.client
import json
import socket
import threading


def strict_utf8(raw: bytes) -> bool:
    try:
        raw.decode("utf-8", errors="strict")
        return True
    except UnicodeDecodeError:
        return False


def capture(value: str) -> bytes:
    listener = socket.socket()
    listener.bind(("127.0.0.1", 0))
    listener.listen(1)
    port = listener.getsockname()[1]
    result = bytearray()

    def receive() -> None:
        connection, _ = listener.accept()
        while b"\r\n\r\n" not in result:
            chunk = connection.recv(65536)
            if not chunk:
                break
            result.extend(chunk)
        connection.sendall(b"HTTP/1.1 204 No Content\r\nContent-Length: 0\r\n\r\n")
        connection.close()
        listener.close()

    thread = threading.Thread(target=receive)
    thread.start()
    client = http.client.HTTPConnection("127.0.0.1", port)
    client.putrequest("GET", "/")
    client.putheader("Idempotency-Key", value)
    client.endheaders()
    client.getresponse().read()
    client.close()
    thread.join()
    return bytes(result)


wire = capture("é" * 128)
line = next(item for item in wire.split(b"\r\n") if item.lower().startswith(b"idempotency-key:"))
value = line.split(b":", 1)[1].lstrip()
print(json.dumps({
    "client": "python-http.client",
    "scalarCount": 128,
    "wireByteCount": len(value),
    "uniqueWireBytesHex": sorted({f"{byte:02x}" for byte in value}),
    "latin1RoundTrip": value.decode("latin-1") == "é" * 128,
    "strictUtf8Decode": "PASS" if strict_utf8(value) else "REJECT",
    "requiredUtf8PrefixHex": ("é" * 2).encode("utf-8").hex(),
}, indent=2))
