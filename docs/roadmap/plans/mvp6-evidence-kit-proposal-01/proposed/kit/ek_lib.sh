# shellcheck shell=bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit shared shell functions (proposal mvp6-evidence-kit-proposal-01).
# Sourced by the k*.sh phases. Never prints secret values. macOS (bash 3.2+) and Linux compatible.

set -o pipefail

ek_now() { date -u '+%Y-%m-%dT%H:%M:%SZ'; }

ek_die() { printf 'EK FAIL: %s\n' "$*" >&2; exit 1; }

ek_require_env() {
  local v
  for v in EK_LANE_ID EK_DB_SUFFIX EK_SLOT EK_REPO EK_EXPECTED_HEAD EK_WORK EK_EVIDENCE; do
    [ -n "${!v:-}" ] || ek_die "missing lane variable $v (source lane.env first)"
  done
  case "$EK_WORK" in
    "$EK_REPO"|"$EK_REPO"/*) ek_die "EK_WORK must be outside the repository" ;;
  esac
  case "$EK_EVIDENCE" in
    "$EK_REPO"/docs/records/*) ;;
    *) ek_die "EK_EVIDENCE must be under $EK_REPO/docs/records/ (docs-organization: dated record)" ;;
  esac
  mkdir -p "$EK_EVIDENCE/raw" "$EK_WORK"
  chmod 700 "$EK_WORK"
  [ -f "$EK_EVIDENCE/COMMANDS.tsv" ] || printf 'utc_start\tutc_end\tphase\tstep\tcommand_redacted\texit\n' > "$EK_EVIDENCE/COMMANDS.tsv"
}

# ek_run <phase> <step> <redacted-description> -- command args...
# Runs the command, appends one COMMANDS.tsv row, returns the command's exit code.
# The description (not argv) is what is recorded, so a secret can never reach COMMANDS.tsv via argv.
ek_run() {
  local phase=$1 step=$2 desc=$3; shift 3
  [ "${1:-}" = "--" ] && shift
  local t0 rc
  t0=$(ek_now)
  "$@"; rc=$?
  printf '%s\t%s\t%s\t%s\t%s\t%s\n' "$t0" "$(ek_now)" "$phase" "$step" "$desc" "$rc" >> "$EK_EVIDENCE/COMMANDS.tsv"
  return $rc
}

ek_sha256() { shasum -a 256 "$1" | cut -d' ' -f1; }

ek_listener_pid() { lsof -nP -iTCP:"$1" -sTCP:LISTEN -t 2>/dev/null | head -1; }

ek_port() { python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["ports"][sys.argv[2]])' "$EK_WORK/ports.json" "$1"; }

ek_mongo_uri() {
  # Lane Mongo only. 27017 is structurally impossible here: the port comes from ports.json, which k03 refuses to emit.
  local port rs
  port=$(ek_port mongo); rs="rs${EK_DB_SUFFIX}"
  [ "$port" != "27017" ] || ek_die "refusing operational Mongo 27017"
  printf 'mongodb://127.0.0.1:%s/%s?replicaSet=%s&directConnection=false&serverSelectionTimeoutMS=5000' "$port" "${1:-}" "$rs"
}
