#!/usr/bin/env python3
"""BL-590 — turn a freshly started `mongod --replSet <set>` into a one-node replica set and wait until it is primary.

Why: MDM writes in transactions (and Platform has a transaction executor); a transaction needs a replica set, so the
CI job's mongod is a single-node replica set, reached with directConnection=true. The pinned 7.0 tarball has no shell
(mongosh is a separate download), so this speaks the wire protocol itself — python3 standard library only, no new
download, no package to pin.

The wait is bounded: past --timeout seconds it stops with a NAMED error, exit 1. It never passes silently.

    python3 scripts/mongo_rs_initiate.py --port 27017 --set rs0 --timeout 60
"""

from __future__ import annotations

import argparse
import socket
import struct
import sys
import time

PREFIX = "[mongo-rs]"
OP_MSG = 2013


# ----------------------------------------------------------------------------------------------------- BSON (minimal)

def _cstring(text: str) -> bytes:
    return text.encode("utf-8") + b"\x00"


def _element(name: str, value) -> bytes:
    if isinstance(value, bool):
        return b"\x08" + _cstring(name) + (b"\x01" if value else b"\x00")
    if isinstance(value, int):
        return b"\x10" + _cstring(name) + struct.pack("<i", value)
    if isinstance(value, float):
        return b"\x01" + _cstring(name) + struct.pack("<d", value)
    if isinstance(value, str):
        data = value.encode("utf-8") + b"\x00"
        return b"\x02" + _cstring(name) + struct.pack("<i", len(data)) + data
    if isinstance(value, dict):
        return b"\x03" + _cstring(name) + encode(value)
    if isinstance(value, list):
        return b"\x04" + _cstring(name) + encode({str(i): item for i, item in enumerate(value)})
    raise TypeError(f"cannot encode {type(value).__name__}")


def encode(document: dict) -> bytes:
    body = b"".join(_element(key, value) for key, value in document.items())
    return struct.pack("<i", len(body) + 5) + body + b"\x00"


def decode(data: bytes, offset: int = 0) -> tuple[dict, int]:
    """Returns (document, offset after it)."""
    (length,) = struct.unpack_from("<i", data, offset)
    end = offset + length - 1
    position = offset + 4
    result: dict = {}
    while position < end:
        kind = data[position]
        position += 1
        name_end = data.index(b"\x00", position)
        name = data[position:name_end].decode("utf-8")
        position = name_end + 1
        if kind == 0x01:
            (value,) = struct.unpack_from("<d", data, position); position += 8
        elif kind == 0x02:
            (size,) = struct.unpack_from("<i", data, position)
            value = data[position + 4:position + 4 + size - 1].decode("utf-8"); position += 4 + size
        elif kind in (0x03, 0x04):
            value, position = decode(data, position)
            if kind == 0x04:
                value = [value[key] for key in sorted(value, key=int)]
        elif kind == 0x05:
            (size,) = struct.unpack_from("<i", data, position)
            value = data[position + 5:position + 5 + size]; position += 5 + size
        elif kind == 0x07:
            value = data[position:position + 12].hex(); position += 12
        elif kind == 0x08:
            value = data[position] == 1; position += 1
        elif kind in (0x09, 0x11, 0x12):
            (value,) = struct.unpack_from("<q", data, position); position += 8
        elif kind == 0x0A:
            value = None
        elif kind == 0x10:
            (value,) = struct.unpack_from("<i", data, position); position += 4
        elif kind == 0x13:
            value = data[position:position + 16]; position += 16
        else:
            raise ValueError(f"unsupported BSON type 0x{kind:02x} in field {name}")
        result[name] = value
    return result, offset + length


# ------------------------------------------------------------------------------------------------------- wire (OP_MSG)

def command(sock: socket.socket, document: dict, request_id: int) -> dict:
    payload = struct.pack("<I", 0) + b"\x00" + encode(document)
    sock.sendall(struct.pack("<iiii", 16 + len(payload), request_id, 0, OP_MSG) + payload)
    header = _read(sock, 16)
    length, _, _, opcode = struct.unpack("<iiii", header)
    body = _read(sock, length - 16)
    if opcode != OP_MSG or body[4] != 0:
        raise ValueError(f"unexpected reply (opcode {opcode})")
    reply, _ = decode(body, 5)
    return reply


def _read(sock: socket.socket, size: int) -> bytes:
    chunks = b""
    while len(chunks) < size:
        chunk = sock.recv(size - len(chunks))
        if not chunk:
            raise ConnectionError("connection closed by mongod")
        chunks += chunk
    return chunks


def fail(code: str, detail: str) -> int:
    print(f"{PREFIX} *** FAILED *** {code} — {detail}")
    return 1


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--set", dest="replica_set", required=True)
    parser.add_argument("--timeout", type=float, default=60.0)
    args = parser.parse_args(argv)
    deadline = time.monotonic() + args.timeout
    member = f"{args.host}:{args.port}"

    sock = None
    while sock is None:
        try:
            sock = socket.create_connection((args.host, args.port), timeout=5)
        except OSError as error:
            if time.monotonic() > deadline:
                return fail("MONGOD_NOT_REACHABLE", f"{member} did not accept a connection in {args.timeout:.0f} s ({error})")
            time.sleep(0.5)

    with sock:
        reply = command(sock, {"replSetInitiate": {"_id": args.replica_set, "members": [{"_id": 0, "host": member}]},
                               "$db": "admin"}, 1)
        if reply.get("ok") != 1.0 and reply.get("codeName") != "AlreadyInitialized":
            if reply.get("codeName") == "NoReplicationEnabled":
                return fail("REPLICA_SET_NOT_ENABLED", f"{member} was not started with --replSet ({reply.get('errmsg')})")
            return fail("REPLICA_SET_INITIATE_REFUSED", f"{reply.get('codeName')}: {reply.get('errmsg')}")

        request_id = 2
        while True:
            hello = command(sock, {"hello": 1, "$db": "admin"}, request_id)
            request_id += 1
            if hello.get("isWritablePrimary") is True and hello.get("setName") == args.replica_set:
                print(f"{PREFIX} ok — {member} is the primary of replica set {args.replica_set}")
                return 0
            if time.monotonic() > deadline:
                return fail("REPLICA_SET_NOT_PRIMARY",
                            f"{member} not a writable primary of {args.replica_set} within {args.timeout:.0f} s")
            time.sleep(0.5)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
