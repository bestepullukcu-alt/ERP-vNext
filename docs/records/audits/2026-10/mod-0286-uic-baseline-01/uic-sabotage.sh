#!/bin/sh
# uic-sabotage.sh - MOD-0286 AC-13 verifier sabotage with a named new red (pack revision 06, sha256 7f6868d9).
# Working directory: the repo root. Usage (evidence flow step 4, after auto-01 is committed):
#   python3 scripts/run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-baseline-01/sabotage-01 -- sh docs/records/audits/2026-10/mod-0286-uic-baseline-01/uic-sabotage.sh <absolute scratch path outside the repo> docs/records/audits/2026-10/mod-0286-uic-baseline-01/sabotage-01
# $1 = SCRATCH: an absolute path outside the repository that does not exist yet; the script builds a copy in repo shape there
#      (frontend/Diten.Web/{Views,Models,wwwroot/assets/js,wwwroot/assets/css} + the DEV-0000 Golden Reference Slim pack).
#      Limit (AC-13): the recorded argv holds this absolute path; reproduction rebuilds the copy from the same file list.
# $2 = OUT: the run directory; receives baseline.txt and sabotage.txt (the two verifier reports). Nothing else is written inside the repo.
# Expected (measured 2026-10-22 / 2026-10-23): baseline 97 [PASS] / 1 [FAIL] (FU-4), sabotage 7 [PASS] / 2 [FAIL], exit 1 -
# the verifier stops at the two missing files: "[FAIL] _IndexL10n.cshtml exists" and "[FAIL] index.l10n.js exists".
set -u
SCRATCH="$1"
OUT="$2"
case "$SCRATCH" in
  /*) ;;
  *) echo "refused: scratch path must be absolute ($SCRATCH)"; exit 2 ;;
esac
case "$SCRATCH" in
  "$PWD"|"$PWD"/*) echo "refused: scratch path must be outside the repository ($SCRATCH)"; exit 2 ;;
esac
if [ -e "$SCRATCH" ]; then echo "refused: scratch path already exists ($SCRATCH)"; exit 2; fi
mkdir -p "$OUT" "$SCRATCH/frontend/Diten.Web/wwwroot/assets" "$SCRATCH/execution/domains/developer-enablement/module-packs" || exit 2
V="python3 .antigravity/scripts/verify_datatable_page.py"
A="--area DevEnablement --module GoldenReferenceSlim --reference slim --api-profile direct-gateway --format report"
echo "copy"
cp -R frontend/Diten.Web/Views "$SCRATCH/frontend/Diten.Web/Views" &&
cp -R frontend/Diten.Web/Models "$SCRATCH/frontend/Diten.Web/Models" &&
cp -R frontend/Diten.Web/wwwroot/assets/js "$SCRATCH/frontend/Diten.Web/wwwroot/assets/js" &&
cp -R frontend/Diten.Web/wwwroot/assets/css "$SCRATCH/frontend/Diten.Web/wwwroot/assets/css" &&
cp execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md "$SCRATCH/execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md"
echo "exit=$?"
echo "baseline"
$V "$SCRATCH" $A > "$OUT/baseline.txt" 2>&1
echo "exit=$?"
echo "baseline PASS=$(grep -c '^\[PASS\]' "$OUT/baseline.txt") FAIL=$(grep -c '^\[FAIL\]' "$OUT/baseline.txt")"
echo "sabotage: delete _IndexL10n.cshtml and index.l10n.js in the copy"
rm "$SCRATCH/frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/_IndexL10n.cshtml" "$SCRATCH/frontend/Diten.Web/wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.l10n.js"
echo "exit=$?"
$V "$SCRATCH" $A > "$OUT/sabotage.txt" 2>&1
echo "exit=$?"
echo "sabotage PASS=$(grep -c '^\[PASS\]' "$OUT/sabotage.txt") FAIL=$(grep -c '^\[FAIL\]' "$OUT/sabotage.txt")"
echo "named red: _IndexL10n.cshtml exists=$(grep -c '^\[FAIL\] _IndexL10n.cshtml exists' "$OUT/sabotage.txt") index.l10n.js exists=$(grep -c '^\[FAIL\] index.l10n.js exists' "$OUT/sabotage.txt")"
grep '^\[FAIL\]' "$OUT/baseline.txt" "$OUT/sabotage.txt"
echo "exit=$?"
