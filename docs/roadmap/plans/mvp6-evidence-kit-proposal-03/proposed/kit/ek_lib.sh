# shellcheck shell=bash
# CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.2 shared shell functions (proposal mvp6-evidence-kit-proposal-03).
# Sourced by the k*.sh phases. Never prints secret values. macOS (bash 3.2+) and Linux compatible.
# v1.1: G2 one sanitised COMMANDS.tsv row per command with the exit code captured directly; G3 attempt-suffixed names;
#       D4 supervisor socket lives OUTSIDE the lane workspace so K11 can remove the workspace before the final seal.
# v1.2: F5 every git call goes through ek_git (read-only subcommands, GIT_OPTIONAL_LOCKS=0, --no-optional-locks);
#       F7 the socket defaults to the per-user $TMPDIR (macOS: a 0700 folder under /var/folders), never shared /tmp.

set -o pipefail
export GIT_OPTIONAL_LOCKS=0

# F5: read-only git only. Any other subcommand is refused before git runs.
ek_git() {
  local repo=$1; shift
  case "${1:-}" in
    rev-parse|status|archive|show|ls-files|ls-tree|cat-file|log|diff) ;;
    *) ek_die "refusing git ${1:-<none>}: the kit runs read-only git commands only" ;;
  esac
  GIT_OPTIONAL_LOCKS=0 git --no-optional-locks -C "$repo" "$@"
}

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
  [ -n "${TMPDIR:-}" ] || [ -n "${EK_SOCK:-}" ] || ek_die "TMPDIR is not set: set EK_SOCK to a path in a folder only you can write (F7)"
  : "${EK_SOCK:=${TMPDIR%/}/ek-${EK_LANE_ID}.sock}"
  export EK_SOCK
  mkdir -p "$EK_EVIDENCE/raw" "$EK_WORK"
  chmod 700 "$EK_WORK"
  [ -f "$EK_EVIDENCE/COMMANDS.tsv" ] || printf 'utc_start\tutc_end\tphase\tstep\tcommand_redacted\texit\n' > "$EK_EVIDENCE/COMMANDS.tsv"
}

# One line, no tabs: keeps COMMANDS.tsv one row per command (G2).
ek_one_line() { printf '%s' "$*" | tr '\t\r\n' '   '; }

# ek_run <phase> <step> <redacted-description> -- command args...
# Runs the command, appends ONE COMMANDS.tsv row, returns the command's exit code.
# The exit code is read from "$?" on the line right after the command, never after a $(date) or any other
# substitution (G2). The description (not argv) is recorded, so a secret can never reach COMMANDS.tsv via argv.
ek_run() {
  local phase=$1 step=$2 desc=$3; shift 3
  [ "${1:-}" = "--" ] && shift
  local t0 t1 rc
  t0=$(ek_now)
  "$@"
  rc=$?
  t1=$(ek_now)
  printf '%s\t%s\t%s\t%s\t%s\t%s\n' "$t0" "$t1" "$(ek_one_line "$phase")" "$(ek_one_line "$step")" \
    "$(ek_one_line "$desc")" "$rc" >> "$EK_EVIDENCE/COMMANDS.tsv"
  return $rc
}

# ek_unique <dir> <base> <ext>: prints <dir>/<base>-a<N>.<ext> for the first N that does not exist (G3: never overwrite).
ek_unique() {
  local d=$1 b=$2 x=$3 n=1
  while [ -e "$d/$b-a$n.$x" ]; do n=$((n + 1)); done
  printf '%s/%s-a%s.%s' "$d" "$b" "$n" "$x"
}

ek_sha256() { shasum -a 256 "$1" | cut -d' ' -f1; }

ek_listener_pid() { lsof -nP -iTCP:"$1" -sTCP:LISTEN -t 2>/dev/null | head -1; }

ek_port() { python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["ports"][sys.argv[2]])' "$EK_WORK/ports.json" "$1"; }

# Talk to the lane supervisor (the only process that holds lane secrets). Prints its one-line JSON reply;
# exit status 0 only if the reply says ok.
ek_ctl() { python3 "$(dirname "${BASH_SOURCE[0]}")/k00_ctl.py" "$EK_SOCK" "$@"; }

ek_mongo_uri() {
  # Lane Mongo only. 27017 is structurally impossible here: the port comes from ports.json, which k03 refuses to emit.
  local port rs
  port=$(ek_port mongo); rs="rs${EK_DB_SUFFIX}"
  [ "$port" != "27017" ] || ek_die "refusing operational Mongo 27017"
  printf 'mongodb://127.0.0.1:%s/%s?replicaSet=%s&directConnection=false&serverSelectionTimeoutMS=5000' "$port" "${1:-}" "$rs"
}
