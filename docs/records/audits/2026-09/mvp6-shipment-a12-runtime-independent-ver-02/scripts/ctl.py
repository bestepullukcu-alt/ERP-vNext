#!/usr/bin/env python3
"""Send one JSON request to the lane supervisor socket. Usage: ctl.py W '{"op":"launch","svc":"auth"}'"""
import json, socket, sys
s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
s.connect(sys.argv[1] + "/ctl.sock")
s.sendall((json.dumps(json.loads(sys.argv[2])) + "\n").encode())
print(s.makefile().readline().strip())
