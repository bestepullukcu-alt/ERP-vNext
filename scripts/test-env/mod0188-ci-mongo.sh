#!/usr/bin/env bash
set -euo pipefail

state="$RUNNER_TEMP/mod0188-ci-mongo-state"

stop_owned() {
  if [[ ! -f "$state" ]]; then
    return
  fi
  dir=$(cat "$state")
  case "$dir" in
    "$RUNNER_TEMP"/mod0188-ci-mongo-*) ;;
    *) echo "Unexpected MOD-0188 test directory" >&2; exit 1 ;;
  esac
  if [[ -f "$dir/mongod.pid" ]]; then
    pid=$(cat "$dir/mongod.pid")
    if [[ "$pid" =~ ^[0-9]+$ ]] && [[ -e "/proc/$pid/cmdline" ]]; then
      if ! tr '\0' ' ' < "/proc/$pid/cmdline" | grep -Fq -- "--dbpath $dir/data"; then
        echo "PID $pid is not the MOD-0188 test mongod" >&2
        exit 1
      fi
      kill "$pid"
      for _ in $(seq 1 30); do
        [[ ! -e "/proc/$pid/cmdline" ]] && break
        sleep 1
      done
      [[ ! -e "/proc/$pid/cmdline" ]] || { echo "Owned mongod did not stop" >&2; exit 1; }
    fi
  fi
  rm -rf -- "$dir"
  rm -f -- "$state"
}

case "${1:-}" in
  stop) stop_owned ;;
  start)
    [[ ! -e "$state" ]] || { echo "MOD-0188 test Mongo already started" >&2; exit 1; }
    dir=$(mktemp -d "$RUNNER_TEMP/mod0188-ci-mongo-XXXXXXXX")
    printf '%s\n' "$dir" > "$state"
    mkdir "$dir/data"
    port=''
    for candidate in 31994 32994 33994 34994; do
      if python3 - "$candidate" <<'PY'
import socket
import sys
with socket.socket() as sock:
    sock.bind(("127.0.0.1", int(sys.argv[1])))
PY
      then
        port="$candidate"
        break
      fi
    done
    [[ -n "$port" ]] || { echo "No free MOD-0188 test port" >&2; exit 1; }
    "$DITEN_TEST_MONGOD" --replSet rs-mod0188 --port "$port" --bind_ip 127.0.0.1 \
      --dbpath "$dir/data" --logpath "$dir/mongod.log" --pidfilepath "$dir/mongod.pid" --fork
    python3 -m venv "$dir/venv"
    "$dir/venv/bin/pip" install --disable-pip-version-check 'pymongo==4.10.1'
    "$dir/venv/bin/python" - "$port" <<'PY'
import sys
import time
from pymongo import MongoClient

port = int(sys.argv[1])
client = MongoClient(f"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=1000")
deadline = time.monotonic() + 30
while True:
    try:
        client.admin.command({"replSetInitiate": {"_id": "rs-mod0188", "members": [{"_id": 0, "host": f"127.0.0.1:{port}"}]}})
        break
    except Exception:
        if time.monotonic() >= deadline:
            raise
        time.sleep(0.5)
while True:
    try:
        if client.admin.command("hello").get("isWritablePrimary"):
            break
    except Exception:
        pass
    if time.monotonic() >= deadline:
        raise TimeoutError("MOD-0188 test replica set did not become PRIMARY")
    time.sleep(0.5)
PY
    echo "MOD0188_TEST_MONGO_URI=mongodb://127.0.0.1:$port/?replicaSet=rs-mod0188&serverSelectionTimeoutMS=5000" >> "$GITHUB_ENV"
    echo "[mod0188] owned test replica set ready on $port"
    ;;
  *) echo 'Usage: mod0188-ci-mongo.sh start|stop' >&2; exit 2 ;;
esac
