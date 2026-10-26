#!/bin/sh
# uic-resx-baseline.sh - MOD-0286 RC-24 key-level resx baseline (pack revision 06, sha256 7f6868d9; F-16).
# Working directory: the repo root. Usage (evidence flow step 1, committed BEFORE the first auto-01 run, never rewritten by a run):
#   sh docs/records/audits/2026-10/mod-0286-uic-baseline-01/uic-resx-baseline.sh docs/records/audits/2026-10/mod-0286-uic-baseline-01/resx-baseline
# Writes 12 files under "$OUT_BASE": missing-<lang>.txt (keys of SharedResource.en.resx absent in <lang>) and
# equal-<lang>.txt (keys whose value equals the English value, XML-escaped as written) for tr fr es zh ar ru.
# The same grep / awk / sort / comm / join lines as RC-24 of uic-auto.sh; the intermediate keys-/kv- files are built in a
# temporary directory and removed (RC-24 rebuilds them under its own run directory). No comparison: this is the baseline.
set -u
OUT_BASE="$1"
mkdir -p "$OUT_BASE"
W="$(mktemp -d "${TMPDIR:-/tmp}/uic-resx-baseline.XXXXXX")" || exit 2
TAB="$(printf '\t')"
LC_ALL=C; export LC_ALL
for l in en tr fr es zh ar ru; do f=frontend/Diten.Web/Resources/SharedResource.$l.resx; grep -o '<data name="[^"]*"' $f | sed 's/<data name="//;s/"$//' | sort -u > $W/keys-$l.txt; awk '/<data name="/{match($0,/name="[^"]*"/); k=substr($0,RSTART+6,RLENGTH-7); if ($0 !~ /<value>/) getline; if (match($0,/<value>.*<\/value>/)) v=substr($0,RSTART+7,RLENGTH-15); else v=""; print k "\t" v}' $f | sort -t "$TAB" -k1,1 > $W/kv-$l.txt; done
for l in tr fr es zh ar ru; do comm -23 $W/keys-en.txt $W/keys-$l.txt > $OUT_BASE/missing-$l.txt; join -t "$TAB" -j 1 $W/kv-en.txt $W/kv-$l.txt | awk -F'\t' '$2==$3 && $2!="" {print $1}' > $OUT_BASE/equal-$l.txt; printf "%s missing=%s equal=%s\n" $l $(wc -l < $OUT_BASE/missing-$l.txt) $(wc -l < $OUT_BASE/equal-$l.txt); done
echo "exit=$?"
case "$W" in */uic-resx-baseline.*) rm -rf "$W" ;; esac
