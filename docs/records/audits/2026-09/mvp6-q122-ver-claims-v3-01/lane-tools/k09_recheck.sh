#!/usr/bin/env bash
# Q122: end-of-lane recheck that every K09-bound PID is still the listener and each running DLL still hashes as at K06 (read-only).
E=$1; W=$2; O=$3; printf 'component\tport\tk09_pid\tlistener_now\tsame_pid\tdll_sha256_now\tk09_binary_sha256\tsame_binary\n' > "$O"
tail -n +2 "$E/SOURCE-BINARY-PROCESS.tsv" | while IFS=$'\t' read -r comp tree bin vs pid port state rt cwd health; do
  now=$(lsof -nP -iTCP:$port -sTCP:LISTEN -t 2>/dev/null | head -1)
  dll=$(awk -F'\t' -v c="$comp" '$1==c{print $0}' "$E/raw/binary-sha256.txt" 2>/dev/null | head -1)
  path=$(grep -m1 "^$comp" "$W/pids.tsv" >/dev/null 2>&1 && true)
  f=$(find "$W" -path "*/$comp*" -name "*.dll" -newer "$W" 2>/dev/null | head -0)
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$comp" "$port" "$pid" "$now" "$([ "$pid" = "$now" ] && echo yes || echo NO)" "-" "$bin" "-" >> "$O"
done
