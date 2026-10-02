#!/bin/bash
# WP-0173-PM0 D4 — sabotage proofs (K3): break the guard → its test RED → restore → GREEN, file hash identical.
# Copy of the UI2 script with the two PM0 guards of the WP.
set -u
W=/c/Users/Ali/erp-vnext-wt-pm0
S="$(dirname "$0")"
cd "$W" || exit 1

run_web() { dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release -nologo --filter "$1" 2>&1 | grep -E 'Başarılı!|Başarısız!|Başarısız Diten' | sed 's/^ *//'; }

sabotage() { # name file from to runner filter
  local name="$1" file="$2" from="$3" to="$4" runner="$5" filter="$6"
  echo "=================================================================="
  echo "## ($name) $(date -Iseconds)"
  echo "file: $file"
  local before; before=$(sha256sum "$file" | cut -d' ' -f1)
  echo "sha256 before : $before"
  cp "$file" "$S/sabotage-backup.tmp"
  local count; count=$(grep -cF -- "$from" "$file")
  echo "occurrences of the guarded text: $count"
  node -e "const fs=require('fs');const f=process.argv[1];const s=fs.readFileSync(f,'utf8');const i=s.indexOf(process.argv[2]);if(i<0)process.exit(3);fs.writeFileSync(f,s.slice(0,i)+process.argv[3]+s.slice(i+process.argv[2].length));" "$file" "$from" "$to" || { echo "SABOTAGE NOT APPLIED"; return; }
  echo "--- sabotage diff:"; diff "$S/sabotage-backup.tmp" "$file"
  echo "--- tests with the guard broken (expected RED):"; $runner "$filter"
  cp "$S/sabotage-backup.tmp" "$file"
  local after; after=$(sha256sum "$file" | cut -d' ' -f1)
  echo "sha256 restored: $after  identical=$([ "$before" = "$after" ] && echo yes || echo NO)"
  echo "--- tests restored (expected GREEN):"; $runner "$filter"
  rm -f "$S/sabotage-backup.tmp"
}

# (a) the bridge is honoured in ANY environment: the "only in Development" condition removed
sabotage "a: bridge ignored outside Development (+ warning)" \
  "frontend/Diten.Web/Models/Inventory/ProductMasterBridge.cs" \
  'if (!string.Equals(environmentName, Environments.Development, StringComparison.Ordinal))' \
  'if (false)' \
  run_web "FullyQualifiedName~ProductMasterBridgeTests.With_the_setting_outside_Development_it_is_ignored_and_a_warning_is_logged"

# (b) FU-UI2-05: the reversal dates are taken at send time again instead of the instant fixed when the dialog opened
sabotage "b: reversal dates fixed with the key (re-send replays)" \
  "frontend/Diten.Web/Controllers/StockMovementsController.cs" \
  'var now = reversalAt;' \
  'var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);' \
  run_web "FullyQualifiedName~InventoryProxyControllerTests.A_resent_reversal_dialog_carries_the_same_dates_so_the_service_replays_it"

echo "=================================================================="
echo "## working tree after both (must be empty):"
git status --porcelain -- frontend/Diten.Web/Models/Inventory/ProductMasterBridge.cs frontend/Diten.Web/Controllers/StockMovementsController.cs
echo "## git diff HEAD (must be empty):"
git diff HEAD --stat -- frontend
