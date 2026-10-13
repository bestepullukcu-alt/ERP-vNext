#!/usr/bin/env bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K05: isolated DB-010 Mongo replica set for the lane.
# One lane-owned mongod, bound to 127.0.0.1 on the K03 port (never 27017), single-member replica set rs<SUFFIX>
# (transactions need a replica set). DB-010: one fixed database per service per lane (Diten<Svc>_<SUFFIX>);
# tests inside the lane isolate by tenant id, not by creating databases.
set -u
. "$(dirname "$0")/ek_lib.sh"
ek_require_env
RAW="$EK_EVIDENCE/raw"
PORT=$(ek_port mongo); RS="rs${EK_DB_SUFFIX}"
[ "$PORT" != 27017 ] || ek_die "refusing 27017"
[ -z "$(ek_listener_pid "$PORT")" ] || ek_die "port $PORT already has a listener"
command -v mongod >/dev/null && command -v mongosh >/dev/null || ek_die "mongod/mongosh not on PATH"

DB="$EK_WORK/mongo/db"; LOG="$EK_WORK/mongo/mongod.log"
mkdir -p "$DB"
mongod --version > "$RAW/mongod-version.txt" 2>&1

ek_run K05 mongod-start "mongod --replSet $RS --port $PORT --bind_ip 127.0.0.1 --dbpath <lane> --fork" -- \
  mongod --replSet "$RS" --port "$PORT" --bind_ip 127.0.0.1 --dbpath "$DB" --logpath "$LOG" \
         --pidfilepath "$EK_WORK/mongo/mongod.pid" --fork > "$RAW/mongod-fork.txt" 2>&1 || ek_die "mongod start failed"

PID=$(cat "$EK_WORK/mongo/mongod.pid")
printf 'mongod\t%s\t%s\t%s\n' "$PID" "$PORT" "$EK_WORK/mongo" >> "$EK_WORK/pids.tsv"

URI="mongodb://127.0.0.1:$PORT/?directConnection=true"
ek_run K05 rs-initiate "rs.initiate single member 127.0.0.1:$PORT" -- \
  mongosh --quiet "$URI" --eval "rs.initiate({_id:'$RS',members:[{_id:0,host:'127.0.0.1:$PORT'}]}).ok" > /dev/null \
  || ek_die "rs.initiate failed"
for _ in $(seq 1 60); do
  st=$(mongosh --quiet "$URI" --eval 'try{rs.status().members[0].stateStr}catch(e){"NA"}' 2>/dev/null)
  [ "$st" = PRIMARY ] && break; sleep 1
done
[ "${st:-}" = PRIMARY ] || ek_die "replica set did not reach PRIMARY"

mongosh --quiet "$URI" --eval "
const s=rs.status(); const b=db.adminCommand({buildInfo:1});
print(JSON.stringify({set:s.set,members:s.members.map(m=>({name:m.name,stateStr:m.stateStr})),
  version:b.version,port:$PORT,operational27017Used:false,
  databasesAtStart:db.adminCommand({listDatabases:1,nameOnly:true}).databases.map(d=>d.name)}))" > "$RAW/mongo-rs-status.json"
echo "K05 PASS: $RS PRIMARY on 127.0.0.1:$PORT (pid $PID)"
