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
# When no base is known, it cannot be fetched, or the diff cannot be produced, the step says SKIPPED in capitals.
# It never passes silently. Only the service ledgers (Diten.*.md) are read; a line moved inside one file is not growth.
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
  local added="" line name file changed file_diff removed
  # Only the service ledgers (Diten.*.md) carry lists. The README's own bullets are prose, not ledger lines.
  # A diff that cannot be produced is reported, never read as "nothing added".
  if ! changed="$(git diff --name-only "$base" ${head:+"$head"} -- "$ledger_dir/Diten.*.md")"; then
    echo "[phase1] audit ledger growth: *** SKIPPED *** — the ledger diff against '$base' could not be produced."
    echo "[phase1]   NOTHING WAS CHECKED."
    return 0
  fi

  while IFS= read -r file; do
    [ -z "$file" ] && continue
    if ! file_diff="$(git diff --unified=0 "$base" ${head:+"$head"} -- "$file")"; then
      echo "[phase1] audit ledger growth: *** SKIPPED *** — the diff of $file could not be produced. NOTHING WAS CHECKED."
      return 0
    fi
    # A line that left one place of the file and entered another (a sort, a move between sections) is not growth:
    # the same name is removed and added in the same file. The pinned counts still watch each section's size.
    removed="$(printf '%s\n' "$file_diff" | grep -E '^-- ' | sed 's/^-- //' || true)"
    while IFS= read -r line; do
      [ -z "$line" ] && continue
      name="${line#+- }"
      if [ -n "$removed" ] && printf '%s\n' "$removed" | grep -qxF -- "$name"; then
        continue
      fi
      if [ -f "$decisions" ] && grep -qwF -- "$name" "$decisions"; then
        echo "[phase1] audit ledger growth: allowed by a recorded decision — $name"
      else
        added="${added}${line}  (${file##*/})"$'\n'
      fi
    done <<< "$(printf '%s\n' "$file_diff" | grep -E '^\+- ' || true)"
  done <<< "$changed"

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

# BL-609 (WP-CI-SERVICE-TESTS-04) — the page-help ledger's ceilings only go down, against the BASE branch: the web test
# measures rows under the ceiling but cannot see the ceiling raised in the same PR. Same base as the audit step (the
# PR base tip, fetched with depth 1; locally PAGE_HELP_LEDGER_BASE=<ref>). No base, no ledger here or on the base →
# the script SKIPS by name. A deliberate raise is a reasoned CT line in the ledger (scripts/check_page_help_ledger_ceiling.py).
check_page_help_ledger_ceiling() {
  local base="${PAGE_HELP_LEDGER_BASE:-}"
  if [ -z "$base" ] && [ -n "${GITHUB_BASE_REF:-}" ]; then
    if git fetch --no-tags --depth=1 origin "$GITHUB_BASE_REF" >/dev/null 2>&1; then
      base="FETCH_HEAD"
    else
      echo "[phase1] page-help ceiling: *** SKIPPED *** — base branch '$GITHUB_BASE_REF' could not be fetched. NOTHING WAS CHECKED."
      return 0
    fi
  fi
  if [ -z "$base" ]; then
    python3 scripts/check_page_help_ledger_ceiling.py
  else
    python3 scripts/check_page_help_ledger_ceiling.py --base "$base"
  fi
}

echo "[phase1] page-help ledger ceiling"
check_page_help_ledger_ceiling

echo "[phase1] validate event schemas"
./scripts/validate_event_schemas.sh

echo "[phase1] cross-db enforcement"
./scripts/check_cross_db_enforcement.sh

# CT-CI-01 (owner decision, relayed by Codex 2026-10-16): the Control Tower tools' own self-tests, before the heavy
# builds. Standard library and git only — measured in a depth-1 clone with a package-free Python. Each line fails fast
# like every other step; nothing here reads or changes a test result of the suites below.
echo "[phase1] CT tool self-tests"
python3 -B scripts/ct_queue.py --self-test
python3 -B scripts/acceptance_record.py --self-test
python3 -B scripts/prompt_gate.py --self-test
python3 -B scripts/status_report.py --self-test
python3 -B .antigravity/scripts/list_screen_touch_hook.py --self-test
python3 -B scripts/known_failures.py --self-test
python3 -B scripts/ct_route.py --self-test

# AUDIT-FU-01 (4): a nav key written in a module pack has the runtime's normalized form (NavNameLocalizer.Normalize);
# the keys written before this check are listed in the script (KNOWN, only shrinks). Standard library and git only.
echo "[phase1] pack nav keys"
python3 -B scripts/check_pack_nav_keys.py --self-test
python3 -B scripts/check_pack_nav_keys.py

# CT-APR-01: the version-bound pack approval's own self-test (a throw-away git repository; standard library and git only)
echo "[phase1] pack approval self-test"
python3 -B scripts/pack_approval.py --self-test

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

dotnet build services/Diten.SafetyStockService/tests/Diten.SafetyStockService.Calculation.Tests/Diten.SafetyStockService.Calculation.Tests.csproj -c Debug
dotnet build services/Diten.SafetyStockService/tests/Diten.SafetyStockService.PolicyState.Tests/Diten.SafetyStockService.PolicyState.Tests.csproj -c Debug
dotnet build services/Diten.SafetyStockService/tests/Diten.SafetyStockService.Read.Tests/Diten.SafetyStockService.Read.Tests.csproj -c Debug

# CT-FIX-03 (R0, Codex): the Control Tower CLI regression set (scripts/tests/test_ct_cli_regression.py) builds a
# synthetic Blueprint workbook and needs openpyxl. It is installed from the repository's own pins
# (scripts/requirements-tools.txt) into an isolated virtual environment under the runner's temp folder — never into the
# global Python, no new action, no secret. When the venv cannot be made or the pins cannot be installed, the step fails
# with the reason: the regression set is never skipped (it also raises without openpyxl). The architecture guard
# (CiServiceTestsGuardTests) keeps its contract: no double-pipe operator here, and the exact discover line below, run with the venv's
# python3 first on PATH for that one step only.
ct_tools_python() {
  local venv="${CT_TOOLS_VENV:-${RUNNER_TEMP:-${TMPDIR:-/tmp}}/ct-tools-venv}"
  if [ ! -x "$venv/bin/python3" ]; then
    if ! python3 -m venv "$venv" >&2; then
      echo "[phase1] CT tools venv: 'python3 -m venv $venv' failed" >&2
      return 1
    fi
  fi
  if ! "$venv/bin/python3" -m pip install --quiet --disable-pip-version-check -r scripts/requirements-tools.txt >&2; then
    echo "[phase1] CT tools venv: pip install -r scripts/requirements-tools.txt failed" >&2
    return 1
  fi
  echo "$venv/bin/python3"
}

# WP-CI-SERVICE-TESTS-03 FIX1: the CI helper scripts' own tests (the skip allow-list of check_trx_no_skipped.py), and
# since CT-FIX-03 the Control Tower CLI regression set, with the tools' pinned packages.
echo "[phase1] test ci scripts"
CT_TOOLS_PY="$(ct_tools_python)"
CT_SAVED_PATH="$PATH"
PATH="$(dirname "$CT_TOOLS_PY"):$PATH"
echo "[phase1] python3 for scripts/tests: $(command -v python3)"
python3 -m unittest discover -s scripts/tests -p 'test_*.py'
PATH="$CT_SAVED_PATH"

# WP-CI-SERVICE-TESTS-03 (CT decision b; FIX1: here, not in the workflow, so a local gates run builds them too). These
# service test projects are NOT in their service's solution, so the solution builds above never compile them —
# e3d3a7331 broke Platform.Eventing.Tests and nobody saw it. BUILD only: their tests need RabbitMQ / an owned mongod and
# run in service-tests.yml. Guard: CiServiceTestsGuardTests (both ways — every such project built here, nothing else).
echo "[phase1] build test projects outside their solution"
dotnet build services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/Diten.Platform.Eventing.Tests.csproj -c Debug
dotnet build services/Diten.Platform/tests/Diten.Platform.BackgroundJobs.Tests/Diten.Platform.BackgroundJobs.Tests.csproj -c Debug
dotnet build services/Diten.AuthService/tests/Diten.AuthService.AccountKindAcceptanceHost.Tests/Diten.AuthService.AccountKindAcceptanceHost.Tests.csproj -c Debug
dotnet build services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets.Tests/Diten.BuildingBlocks.Security.Secrets.Tests.csproj -c Debug
dotnet build services/Diten.ProcurementService/tests/Diten.ProcurementService.Api.Tests/Diten.ProcurementService.Api.Tests.csproj -c Debug
dotnet build services/Diten.PlanningService/tests/Diten.PlanningService.Cycles.Tests/Diten.PlanningService.Cycles.Tests.csproj -c Debug
dotnet build services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Diten.PvgService.Api.Tests.csproj -c Debug
dotnet build services/Diten.PvgService/tests/Diten.PvgService.CaseProcessing.Tests/Diten.PvgService.CaseProcessing.Tests.csproj -c Debug
dotnet build services/Diten.PvgService/tests/Diten.PvgService.MeddraCoding.Tests/Diten.PvgService.MeddraCoding.Tests.csproj -c Debug
dotnet build services/Diten.PvgService/tests/Diten.PvgService.RegPvBase.Tests/Diten.PvgService.RegPvBase.Tests.csproj -c Debug
dotnet build services/Diten.PvgService/tests/Diten.PvgService.SignalManagement.Tests/Diten.PvgService.SignalManagement.Tests.csproj -c Debug
dotnet build gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj -c Debug

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
