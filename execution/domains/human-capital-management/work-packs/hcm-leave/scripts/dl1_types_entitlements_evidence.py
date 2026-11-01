#!/usr/bin/env python3
"""WP-0280-FU02 DL-1 acceptance evidence (card S-83: the Control Tower's script; the worker neither runs nor changes it).

Runs the Leave tests of Diten.Platform.Application.Tests on a real Mongo replica set through the real HTTP pipeline and
measures that every named test exists and passes, and nothing under Leave fails.

Exit: 0 every check passed · 1 a check is red (with no DL-1 code: the red baseline) · 3 set-up error (dotnet or mongod
missing, build failed), never to be read as a red baseline.

    python execution/domains/human-capital-management/work-packs/hcm-leave/scripts/dl1_types_entitlements_evidence.py
"""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

TEST_PROJECT = "services/Diten.Platform/tests/Diten.Platform.Application.Tests"
FILTER = "FullyQualifiedName~Diten.Platform.Application.Tests.Leave"
MONGOD = "C:/Program Files/MongoDB/Server/7.0/bin/mongod.exe"
REQUIRED = """
Create_returns_201_and_stores_the_tenant_row Duplicate_code_in_the_tenant_is_409_and_writes_nothing
The_same_code_is_allowed_in_another_tenant Health_category_is_refused_on_create_and_on_update
Unknown_or_empty_category_is_refused Label_is_required Unpaid_cannot_reduce_balance Carry_over_limit_rules
Code_cannot_change_on_update Category_cannot_change_on_update Stale_version_is_409_on_update_activate_deactivate
Label_too_long_is_400 Activate_and_deactivate_flip_only_IsActive Another_tenants_type_is_404_with_no_metadata
A_soft_deleted_type_is_404_and_its_code_can_be_reused List_is_ordered_by_code_and_capped_at_50
Read_key_sees_active_types_only_and_manage_also_sees_inactive Read_key_gets_404_for_an_inactive_type
A_hand_written_health_row_is_never_returned Employee_key_set_lists_active_types_and_gets_403_on_every_manage_endpoint
No_key_is_403_on_every_type_route Delete_has_no_route
Set_creates_entitlement_and_balance_together Set_updates_with_expected_version Stale_or_missing_version_is_409
Two_concurrent_first_sets_store_one_row Days_rules Year_rules Inactive_or_non_reducing_type_is_400
Unknown_or_foreign_type_is_404_with_the_same_answer Unknown_employee_is_404_and_writes_nothing
Employee_service_down_is_503_and_writes_nothing Reduction_floor_examples Below_floor_writes_nothing
A_failure_before_commit_leaves_no_entitlement_and_no_balance Unknown_commit_result_is_503_with_the_write_outcome_code
Row_scoped_read_keys_get_403_on_the_entitlement_list List_is_tenant_scoped_filtered_and_paged
Another_tenants_entitlement_is_never_listed Read_key_cannot_set_and_manage_key_cannot_read_without_read_key
Each_command_writes_one_business_event_with_its_fields Audit_record_carries_the_actor_envelope_and_no_free_text
A_refused_command_writes_one_failed_record_with_no_event_name_and_no_after_values
Indexes_are_tenant_first_and_unique_where_the_pack_says Repository_refuses_a_duplicate_code_through_the_index
Repository_refuses_a_duplicate_entitlement_through_the_index Repository_refuses_a_duplicate_balance_through_the_index
Ledger_refuses_a_stale_balance_version_and_writes_nothing
Every_action_has_HasPermission_and_the_controller_is_Authorize Permission_keys_are_the_packs_keys
Production_registration_has_no_fake_employee_gateway The_fake_setting_works_only_in_Development
Only_the_ledger_names_the_balance_repository Outside_Adapters_the_module_names_no_other_modules_repository_entity_or_collection
""".split()


def run_tests(results: Path) -> dict[str, list[str]]:
    if shutil.which("dotnet") is None:
        print("KURULUM: dotnet bulunamadı")
        sys.exit(3)
    mongod = os.environ.get("DITEN_TEST_MONGOD") or MONGOD
    if not Path(mongod).is_file():
        print(f"KURULUM: mongod yok: {mongod}")
        sys.exit(3)
    build = subprocess.run(["dotnet", "build", TEST_PROJECT, "-c", "Release", "--nologo"],
                           capture_output=True, text=True, encoding="utf-8", errors="replace")
    if build.returncode != 0:
        print("KURULUM: derleme başarısız; çıktının sonu:")
        print(build.stdout[-1500:])
        sys.exit(3)
    trx = results / "dl1.trx"
    subprocess.run(["dotnet", "test", TEST_PROJECT, "-c", "Release", "--nologo", "--no-build", "--filter", FILTER,
                    "--results-directory", str(results), "--logger", "trx;LogFileName=dl1.trx"],
                   capture_output=True, env={**os.environ, "DITEN_TEST_MONGOD": mongod})
    outcomes: dict[str, list[str]] = {}
    if not trx.is_file():  # the build passed and the filter matched no test: no DL-1 tests exist yet
        return outcomes
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    for row in ET.parse(trx).getroot().findall(".//t:UnitTestResult", ns):
        name = row.get("testName", "").split("(")[0].rsplit(".", 1)[-1]
        outcomes.setdefault(name, []).append(row.get("outcome", ""))
    return outcomes


def check(outcomes: dict[str, list[str]]) -> list[str]:
    red = [f"test yok: {n}" for n in REQUIRED if n not in outcomes]
    red += [f"geçmedi: {n} {sorted(set(o))}" for n, o in sorted(outcomes.items()) if any(x != "Passed" for x in o)]
    return red


def main() -> int:
    with tempfile.TemporaryDirectory() as tmp:
        outcomes = run_tests(Path(tmp))
    red = check(outcomes)
    print(f"koşulan test adı: {len(outcomes)}, satır: {sum(len(o) for o in outcomes.values())}, beklenen ad: {len(REQUIRED)}")
    for line in red:
        print("KIRMIZI:", line)
    print("sonuç:", "kırmızı" if red else "geçti")
    return 1 if red else 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace", newline="\n")  # card S-87: LF on Windows too
    sys.exit(main())
