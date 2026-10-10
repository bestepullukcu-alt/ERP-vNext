# Authority — MVP6-WP-183-ISO-FINAL-VER (Q61 v1.1, chat lane, Linux VM attempt)

- Prompt: Q61 v1.1, Agent Lane AL-MVP6-183-ISOVER-LNX-01 (VER, independent, runtime), Risk HIGH, module MOD-0183 Shipment Tracking & POD (UI).
- Owner decision (CT conversation, 2026-09-26 ~12:12, question tool): "try Q61 in a chat lane on Linux". The approved environment rule still names native macOS .NET, so any Linux result would be **PROVISIONAL**: acceptable only after a separate owner decision to accept Linux runtime evidence, or a later Mac rerun.
- Base: `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Allowed writes: this folder (repository) and `/tmp/**` inside the VM. Everything else in the repository is protected.
- Step 0 rule (prompt §0e): if any of .NET 8.0.417, MongoDB 8.x or Playwright+Chromium cannot be installed (network blocked, unsupported arch, no disk), STOP; write SOP-22 with verdict "NOT RUNNABLE ON LINUX CHAT LANE"; no workarounds (no mirrors, unofficial binaries, proxies or tunnels).
- Authority sources named by the prompt (not opened beyond Step 0, because Step 0 failed): MOD-0183 pack; `mvp6-shipment-acceptance-reconcile-01/ACCEPTANCE-MATRIX.tsv` + `SCOPE-CHANGE-RECORD.tsv`; A12 successor source `7b6a0d1a…314d`; Auth overlay `f50350b8…e2cd`; A12 ver-02 method and CT disposition; PNG method decision.
