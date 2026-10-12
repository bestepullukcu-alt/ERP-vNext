# WP-0173-OPS1 — services absent from the run scripts (report only, not changed)

Measured 2026-10-02 against `launchSettings.json` (http profile) and AGENTS.md §3. Owners are separate; nothing below
was added by OPS1.

| service | port (launchSettings) | §3 | run_all.sh | run_watch.sh | run-diten.sh | watch-diten.ps1 |
|---|---|---|---|---|---|---|
| HcmService | 5060 | yes | ✓ | ✓ | **missing** (build, launch and port 5060 in clean-up) | ✓ |
| CrmService | 5061 | no (5061 is SupplyChain in §3 — drift noted there) | **missing** | **missing** | **missing** | ✓ |
| SupplyChainService | 5061 (§3) | yes | not on this branch | — | — | — |
| PpmService | 5062 | no (EA drift) | **missing** | **missing** | **missing** | **missing** |
| HumanCapitalService | 5063 | no (EA drift) | **missing** | **missing** | **missing** | **missing** |
| TalentEcosystemService | 5064 | no (EA drift) | **missing** | **missing** | **missing** | **missing** |
| ProcurementService | 5065 | yes | **missing** | **missing** | **missing** | **missing** |
| EnterpriseStrategyService | 5003 / 5004 | no (outside the 5011+ band) | **missing** | **missing** | **missing** | **missing** |
| ManagementGovernanceService | no launchSettings found | no | — | — | — | — |
| PvgService | no launchSettings found | no | — | — | — | — |

Also observed (not changed): `run_all.sh` echo banner has no MDM line although MDM is launched; `run-diten.sh`
clean-up omits 5060.
