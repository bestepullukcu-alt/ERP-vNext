#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

# AUD-001 — "the audit debt ledger only shrinks", the CI half.
#
# The architecture test pins the ledger's COUNTS; this step looks at the DIFF: a pull request that ADDS a list line
# ("- SomeCommand") to tests/architecture/audit-ledger/ is red, whatever the counts say — adding debt, K2 debt or a
# writing query is a Control Tower decision, not a line in a PR.
#
# It needs the base branch. On a pull request that is GITHUB_BASE_REF; the checkout is shallow (actions/checkout
# default depth 1 — measured 2026-10-02, phase1-gates.yml sets no fetch-depth), so the base TIP is fetched here with
# depth 1 and compared tree-to-tree — no merge base, no history, the workflow file is not touched. Locally:
#   AUDIT_LEDGER_BASE=<ref> [AUDIT_LEDGER_HEAD=WORKTREE] ./scripts/run_phase1_gates.sh --audit-ledger-only
# When no base is known or it cannot be fetched the step says SKIPPED in capitals. It never passes silently.
check_audit_ledger_growth() {
  local ledger_dir="tests/architecture/audit-ledger"
  local base="${AUDIT_LEDGER_BASE:-}"
  # AUDIT_LEDGER_HEAD=WORKTREE compares the base with the files on disk (before committing); default is HEAD.
  local head="${AUDIT_LEDGER_HEAD:-HEAD}"
  [ "$head" = "WORKTREE" ] && head=""

  if [ -z "$base" ] && [ -n "${GITHUB_BASE_REF:-}" ]; then
    if git fetch --no-tags --depth=1 origin "$GITHUB_BASE_REF" >/dev/null 2>&1; then
      base="FETCH_HEAD"
    else
      echo "[phase1] audit ledger growth: *** SKIPPED *** — base branch '$GITHUB_BASE_REF' could not be fetched."
      echo "[phase1]   NOTHING WAS CHECKED. The pinned counts in AuditTrailStandardTests are the only guard in this run."
      return 0
    fi
  fi

  if [ -z "$base" ]; then
    echo "[phase1] audit ledger growth: *** SKIPPED *** — no base branch (not a pull request, AUDIT_LEDGER_BASE not set)."
    echo "[phase1]   NOTHING WAS CHECKED. The pinned counts in AuditTrailStandardTests are the only guard in this run."
    return 0
  fi

  if ! git rev-parse --verify --quiet "$base^{commit}" >/dev/null; then
    echo "[phase1] audit ledger growth: *** SKIPPED *** — base '$base' is not a commit in this checkout."
    echo "[phase1]   NOTHING WAS CHECKED."
    return 0
  fi

  if ! git cat-file -e "$base:$ledger_dir" 2>/dev/null; then
    echo "[phase1] audit ledger growth: *** NOT CHECKED *** — the ledger does not exist on the base ($base)."
    echo "[phase1]   This change INTRODUCES it; every line is new by definition. From the next change on it is checked."
    return 0
  fi

  # A line whose command is named in CT-DECISIONS.md was added by a recorded Control Tower decision. The file is in
  # the same diff, so the decision is reviewed with the line it allows.
  local decisions="$ledger_dir/CT-DECISIONS.md"
  local added="" line name
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    name="${line#+- }"
    if [ -f "$decisions" ] && grep -qwF -- "$name" "$decisions"; then
      echo "[phase1] audit ledger growth: allowed by a recorded decision — $name"
    else
      added="${added}${line}"$'\n'
    fi
  done <<< "$(git diff --unified=0 "$base" ${head:+"$head"} -- "$ledger_dir" ':!'"$decisions" | grep -E '^\+- ' || true)"

  if [ -n "$added" ]; then
    echo "[phase1] audit ledger growth: FAILED — list lines were ADDED to $ledger_dir (base: $base):"
    printf '%s' "$added" | sed 's/^/[phase1]     /'
    echo "[phase1]   The ledger only shrinks (.antigravity/rules/audit-trail-standard.md §1)."
    echo "[phase1]   Audit the command, or declare a reasoned exception. Adding debt needs a Control Tower decision,"
    echo "[phase1]   recorded in $decisions (command name + decision reference) in the same change."
    return 1
  fi

  echo "[phase1] audit ledger growth: ok — no list line added against $base"
}

if [ "${1:-}" = "--audit-ledger-only" ]; then
  check_audit_ledger_growth
  exit $?
fi

echo "[phase1] audit ledger growth"
check_audit_ledger_growth

echo "[phase1] validate event schemas"
./scripts/validate_event_schemas.sh

echo "[phase1] cross-db enforcement"
./scripts/check_cross_db_enforcement.sh

echo "[phase1] build gateway"
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug

echo "[phase1] build auth"
dotnet build services/Diten.AuthService/Diten.AuthService.sln -c Debug

echo "[phase1] build platform"
dotnet build services/Diten.Platform/Diten.Platform.sln -c Debug

if [ -f "services/Diten.MdmService/Diten.MdmService.sln" ]; then
  echo "[phase1] build mdm"
  dotnet build services/Diten.MdmService/Diten.MdmService.sln -c Debug
else
  echo "[phase1] skip mdm: services/Diten.MdmService/Diten.MdmService.sln not found"
fi

echo "[phase1] test tenancy"
dotnet test tests/tenancy/TenantArchitecture.TenancyTests/TenantArchitecture.TenancyTests.csproj -c Debug

echo "[phase1] test architecture"
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj -c Debug

# Menü adlarının 7 dilde var olduğunu manifest kaynaklarından türeterek doğrulayan guard buradadır
# (NavManifestL10nGuardTests). Hatta koşmazsa hiçbir şeyi korumaz: bir modülün Nav.Module/Nav.Page
# anahtarı unutulduğunda menü sessizce HAM İNGİLİZCE basar — build geçer, testler yeşil kalır, kusur
# yalnız ekrana bakınca görülür. 2026-08-10'da bu üç kez yaşandı (Edit · Recurring Task Rules ·
# "Görevler / Tasks"), üçü de gözle bulundu. Guard yazıldı ama hat onu koşmuyordu; bu satır o boşluğu
# kapatıyor. Proje ayrıca token bridge ve ekran sözleşmesi testlerini de taşır.
echo "[phase1] test web (nav l10n guard + web contracts)"
dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Debug

echo "[phase1] gates passed"
