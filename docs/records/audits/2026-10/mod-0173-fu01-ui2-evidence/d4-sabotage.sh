#!/bin/bash
# WP-0173-UI2 D4 — sabotage proofs (K3): break the guard → its test RED → restore → GREEN, file hash identical.
# Copy of the LOC2 script with the three UI-2 guards of the WP.
set -u
W=/c/Users/Ali/erp-vnext-wt-ui2
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

# (a) "Correct" only on the right rows: the row condition (manual · not a reversal · one of the 7 manual types) forced true
sabotage "a: Correct offered only on an eligible row" \
  "frontend/Diten.Web/Models/Inventory/InventoryListAdapter.cs" \
  'IsReversible(t) && MovementRules.ManualTypes.Contains(t.MovementType ?? string.Empty, StringComparer.Ordinal);' \
  'true;' \
  run_web "FullyQualifiedName~MovementFormTests.Correct_is_offered_only_on_a_manual_unreversed_row_of_a_manual_type"

# (b) S-3 direction matrix: an inbound no longer requires its target location
sabotage "b: direction matrix — inbound target location required" \
  "frontend/Diten.Web/Models/Inventory/MovementRules.cs" \
  'toLocation = Required(form.ToLocationId, Fields.TargetLocation, errors);
                toStatus = form.ToStockStatus?.Trim();' \
  'toLocation = form.ToLocationId?.Trim();
                toStatus = form.ToStockStatus?.Trim();' \
  run_web "FullyQualifiedName~MovementFormTests.An_inbound_needs_target_warehouse_location_and_status"

# (c) double-submit protection: every post mints its own key instead of the form's (a re-post would post twice)
sabotage "c: one key per form (double submit replays)" \
  "frontend/Diten.Web/Controllers/StockMovementsController.cs" \
  'var reply = await gateway.PostAsync("/api/inventory/movements", check.Payload!, MovementKey(key), cancellationToken);' \
  'var reply = await gateway.PostAsync("/api/inventory/movements", check.Payload!, MovementKey(Guid.NewGuid()), cancellationToken);' \
  run_web "FullyQualifiedName~MovementFormTests.The_same_form_posted_twice_sends_the_forms_key_both_times"

echo "=================================================================="
echo "## working tree after all three (git status of the touched files must equal the pre-run status):"
git status --porcelain -- frontend/Diten.Web/Models/Inventory/InventoryListAdapter.cs frontend/Diten.Web/Models/Inventory/MovementRules.cs frontend/Diten.Web/Controllers/StockMovementsController.cs
