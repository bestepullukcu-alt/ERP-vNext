#!/usr/bin/env bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.1 phase K10a: take one DB snapshot into a NEW, attempt-suffixed file.
#   k10_snap.sh <label> '<EK_SNAP json>'      -> E/raw/db/<label>-a<N>.json (prints the path)
# G3: the A12 VER-02 late-async attempts 1 and 2 both wrote late-before.json / late-after-softdelete.json, so attempt 1's
# pair was lost. This wrapper never overwrites (noclobber + first free attempt number) and logs one COMMANDS.tsv row.
set -u
. "$(dirname "$0")/ek_lib.sh"
ek_require_env
label=${1:?label}; spec=${2:?EK_SNAP json}
mkdir -p "$EK_EVIDENCE/raw/db"
out=$(ek_unique "$EK_EVIDENCE/raw/db" "$label" json)
set -o noclobber
EK_SNAP="$spec" ek_run K10 "snap-$label" "mongosh <lane uri> --file k10_db_snapshot.js > raw/db/$(basename "$out")" -- \
  mongosh --quiet "$(ek_mongo_uri)" --file "$(dirname "$0")/k10_db_snapshot.js" > "$out" || ek_die "snapshot $label failed"
grep -q '"error"' "$out" && ek_die "snapshot $label refused (see $out)"
printf '%s\n' "$out"
