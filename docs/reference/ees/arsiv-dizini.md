# EES arşiv dizini

Ana daldan kalkan EES dosyalarının tek dizini: hangi klasör, neden, nereden geri okunur. Dosyalar **silinmedi**;
git geçmişinde aynen duruyor. Bu dosya her arşiv partisinde güncellenir (insanların başvurduğu EES belgeleri
`docs/reference/ees/` altında durur; ajanların iş sırasında okuduğu kartlar ayrı yerde).

- **Karar:** sahip, 2026-11-16 — "Evet, hepsi önerildiği gibi" (yönetim sohbetindeki soru: "Arşivi başlatmak — önerim:
  Evet; önce yeni ham kanıtı durduran kural"). Yönetici kararı "varsayılan arşiv": ana dalda yalnız çekirdek EES
  dosyaları kalır.
- **Yöntem:** dosya ana daldan `git rm` ile kalkar; geçmiş yeniden yazılmaz; içerik değişmez. Kayıtlar ana dalda
  değiştirilmez, arşive alınabilir (`.antigravity/rules/docs-organization.md` K4).
- **Yeni ham kanıt:** depoya yeni ham kanıt eklenmesini engelleyecek kontrol ertelendi; eski akış kapanınca ölçüme
  göre karar verilecek. Bugünkü kanıt kuralları ve kabul araçları değişmedi.
- **Kaynak commit (dosyaların arşivden önceki hali):** `6ef2c884b177c85f9f2d0caab78b4f63e4b0fa52` (main, 2026-11-16; bütün partilerin dosyaları bu
  commit'te aynen duruyor).
- **Etiket:** `ees-arsiv-oncesi-2026-11` — ilk arşiv PR'ı birleşmeden önce depo sahibi koyar ve korur. Etiket konana
  kadar aşağıdaki komutlarda etiket yerine kaynak commit kullanılır.
- **Ne kalmadı, ne kaldı:** bir dosya ancak şu okuyuculardan hiçbiri onu, klasörünü ya da üst klasörünü (doğrudan ya da
  bir değişkene atayarak) çalışma ağacından anmıyorsa kalktı: depoda kalan herhangi bir çalıştırılabilir dosya (`.sh`,
  `.py`, `.ps1`, `.js`, `.cs` …; `docs/records` altındakiler dahil) ya da `docs/reference/`, `docs/guides/`,
  `execution/` altındaki bir belge. `yol@commit` biçimindeki atıf geçmişten okunduğu için engel değildir. Son 30 günde
  commit almış, açık bir PR'ın ya da çalışma dalının dokunduğu klasörlerde, kendi klasörünü okuyan betiğin girdileri
  de kaldı.

## Geri okuma

```bash
git show 6ef2c884b177:<yol>                                   # tek dosyayı oku
git ls-tree -r --name-only 6ef2c884b177 -- <klasör>           # klasörde ne vardı
git grep -n <kalıp> 6ef2c884b177 -- <klasör>                  # arşivde ara
git restore --source=6ef2c884b177 --worktree -- <klasör>      # çalışma ağacına geri getir (stage etmez; commit etme)
git log --oneline --follow -- <yol>                   # dosyanın tarihçesi
```

Sığ klon (`--depth`) arşivi göremez; önce `git fetch --unshallow`.

## Partiler

| Parti | Ne kalktı | Dosya | MiB | Yerinde bırakılan | Durum |
|---|---|---|---|---|---|
| 1 | `docs/records/audits` ham kanıt (log, ekran görüntüsü, test çıktısı, json/tsv dökümü, arşiv) | 4786 | 250.8 | 852 | bu dalda |
| 4 | `logs/`, `.logs/`, kök artıkları, eski Blueprint sürümleri | 100 | 13.0 | 0 | bu dalda |
| 2 | `docs/records` kalan ham olmayan dosyalar (betik, yama, sha256 listesi …) + `records/{analysis,tests}` | 1379 | 10.0 | 200 | planlı |
| 3 | `docs/roadmap/plans` + kök `plans/` | 809 | 5.6 | 155 | planlı |
| 6 | kimsenin çağırmadığı betikler, `.antigravity` eski betikleri, küçük kalanlar | 47 | 0.3 | 22 | planlı |
| 5 | son 30 günde dokunulmayan iş paketleri + kapanmış iş listesi | 297 | 3.9 | — | planlı; açık PR #425 birleşince |

## Parti 1 — `docs/records/audits` ham kanıt (log, ekran görüntüsü, test çıktısı, json/tsv dökümü, arşiv)

**Neden:** hiçbir CI adımı, test ya da araç bu dosyaları çalışma ağacından okumuyor (12 Kasım taraması; 16 Kasım'da güncel ana dalda yeniden doğrulandı: hepsi aynen duruyor, o günden beri kayıtlara eklenen dosyaların hepsi `.md`).

<details>
<summary>Yerinde bırakılanlar (852 dosya, 65 klasör) ve nedeni</summary>

| Klasör / dosya | Dosya | Neden (örnek) |
|---|---|---|
| `docs/records/audits/2026-09/mod-0183-r1-evidence` | 67 | anan: services/Diten.SupplyChainService/tests/verify_evidence.py |
| `docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0-r1` | 7 | anan: docs/records/audits/2026-09/mod-0184-published-uptake/check_uptake.py |
| `docs/records/audits/2026-09/mod-0184-contract-publication-v1.1.0` | 6 | anan: docs/records/audits/2026-09/mod-0184-consumer-technical-evidence/carrier/check_consumer.py, docs/records/audits/2026-09/mod-0184-consumer-technical-eviden |
| `docs/records/audits/2026-09/mod-0184-dev-01` | 24 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0184-carrier-management.md |
| `docs/records/audits/2026-09/mod-0186-prep-02` | 4 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md |
| `docs/records/audits/2026-09/mvp6-bc-successor-exec-02` | 1 | anan: docs/records/audits/2026-09/mvp6-capacity-ui-draft-01/overlay/frontend/Diten.Web/Models/SupplyChain/CapacityPlans/CapacityPlanViewModels.cs |
| `docs/records/audits/2026-09/mvp6-carrier-auth-le-successor-01` | 5 | anan: docs/records/audits/2026-09/mvp6-carrier-auth-le-successor-ver-01/raw/verify_archive.py |
| `docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-exec-01` | 2 | anan: docs/roadmap/plans/mvp6-effort-carrier-e2e-update-05/RECALCULATE.py |
| `docs/records/audits/2026-09/mvp6-carrier-ui-ver-01` | 38 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0184-carrier-management.md |
| `docs/records/audits/2026-09/mvp6-loads-root-acquisition-amendment-candidate-01` | 4 | anan: docs/records/audits/2026-09/mvp6-loads-root-amendment-independent-rever-01/verify_rever.py, docs/records/audits/2026-09/mvp6-loads-root-amendment-independ |
| `docs/records/audits/2026-09/mvp6-loads-root-acquisition-amendment-candidate-02` | 4 | anan: docs/records/audits/2026-09/mvp6-loads-root-amendment-independent-rever-01/verify_rever.py, docs/records/audits/2026-09/mvp6-loads-root-amendment-release- |
| `docs/records/audits/2026-09/mvp6-loads-root-amendment-independent-rever-01` | 8 | yaşayan hatta kendi klasörünü okuyan betik var |
| `docs/records/audits/2026-09/mvp6-loads-root-amendment-independent-ver-01` | 16 | anan: docs/records/audits/2026-09/mvp6-loads-root-amendment-independent-rever-01/verify_rever.py |
| `docs/records/audits/2026-09/mvp6-mod0186-http-01` | 2 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md |
| `docs/records/audits/2026-09/mvp6-mod0186-r01-independent-ver-01` | 110 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md |
| `docs/records/audits/2026-09/mvp6-mod0186-wp-acceptance-01` | 1 | anan: docs/records/audits/2026-10/mvp6-q210-ver-0186-01/tools/q210_matrix.py |
| `docs/records/audits/2026-09/mvp6-mod0190-401-disposition-ct-02` | 1 | anan: docs/records/audits/2026-10/mvp6-q212-ver-0190-01/tools/q212_matrix.py |
| `docs/records/audits/2026-09/mvp6-mod0190-http-dev-02` | 2 | anan: docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-01/verify_combined_source.py, docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01/ver |
| `docs/records/audits/2026-09/mvp6-mod0190-test-oracle-rework-01` | 2 | anan: docs/records/audits/2026-10/mvp6-q212-ver-0190-01/tools/q212_matrix.py |
| `docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-01` | 16 | anan: docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01/verify.py |
| `docs/records/audits/2026-09/mvp6-mod0192-x07-http-disposition-01` | 1 | anan: docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-01/verify_combined_source.py, docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01/ver |
| `docs/records/audits/2026-09/mvp6-q101-control-audit-01` | 1 | anan: docs/roadmap/plans/mvp6-effort-update-09b/RECALCULATE.py |
| `docs/records/audits/2026-09/mvp6-q101b-control-audit-01` | 1 | anan: docs/roadmap/plans/mvp6-effort-update-09b/RECALCULATE.py |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-r2-01` | 1 | anan: docs/records/audits/2026-09/mvp6-sandop-capacity-final-release-pack-01/build_final.py, docs/records/audits/2026-09/mvp6-sandop-capacity-final-release-pack |
| `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01` | 1 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md |
| `docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01` | 5 | yaşayan hatta kendi klasörünü okuyan betik var |
| `docs/records/audits/2026-09/mvp6-shipment-root-r2-emission-exec-01` | 8 | anan: docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/VERIFY.py |
| `docs/records/audits/2026-09/mvp6-shipment-root-r2-emission-independent-ver-01` | 5 | anan: docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/VERIFY.py |
| `docs/records/audits/2026-09/mvp6-shipment-root-r2-emission-rework-01` | 19 | anan: docs/records/audits/2026-09/mvp6-shipment-root-r2-ct-disposition-01/VERIFY.py |
| `docs/records/audits/2026-09/mvp6-shipment-ui-functional-ct-consolidation-01` | 1 | anan: docs/roadmap/plans/mvp6-effort-shipment-ct-update-07/RECALCULATE.py |
| `docs/records/audits/2026-09/mvp6-shipment-ui-policy-error-ver-01` | 10 | anan: docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/VERIFY.py |
| `docs/records/audits/2026-09/mvp6-shipment-ui-presentation-ver-01` | 25 | anan: docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/VERIFY.py |
| `docs/records/audits/2026-10/hcm-leave-discovery-01` | 2 | anan: execution/delivery/ct-queue-registry.tsv, execution/domains/human-capital-management/work-packs/hcm-leave/CT-QUEUE.tsv |
| `docs/records/audits/2026-10/hcm-leave-discovery-02` | 1 | anan: execution/domains/human-capital-management/work-packs/hcm-leave/prompts/WP-0280-FU02-DL0-v0.1-taslak.md, execution/domains/human-capital-management/work-p |
| `docs/records/audits/2026-10/mod-0173-fu01-ui1-evidence` | 29 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0173-FU01-inventory-ledger-ui.md |
| `docs/records/audits/2026-10/mod-0173-fu01-ui2-evidence` | 52 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0173-FU01-inventory-ledger-ui.md |
| `docs/records/audits/2026-10/mod-0173-fu02-loc1-evidence` | 29 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0173-FU02-location-storage-master.md |
| `docs/records/audits/2026-10/mod-0173-fu02-loc2-evidence` | 57 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0173-FU02-location-storage-master.md |
| `docs/records/audits/2026-10/mod-0173-k1-evidence` | 27 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0173-inventory-ledger-valuation.md |
| `docs/records/audits/2026-10/mod-0173-le1-evidence` | 23 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0173-inventory-ledger-valuation.md |
| `docs/records/audits/2026-10/mod-0173-pm0-evidence` | 15 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0173-FU01-inventory-ledger-ui.md |
| `docs/records/audits/2026-10/mod-0286-fu01-01` | 8 | yaşayan hatta kendi klasörünü okuyan betik var |
| `docs/records/audits/2026-10/mod-0286-uic-baseline-01` | 14 | anan: docs/reference/modules/platform/ui-design-system-page-pattern-governance/compliance-checklist.md, docs/reference/modules/platform/ui-design-system-page-pa |
| `docs/records/audits/2026-10/mod-0286-uic-ci-01` | 2 | anan: execution/domains/platform-shared-services/work-packs/prompts/PSS-UI-P1-03-v1.1.md, execution/domains/platform-shared-services/work-packs/prompts/PSS-UI-P |
| `docs/records/audits/2026-10/mod0023-s17-inspection-01` | 3 | anan: docs/reference/architecture/api/mod-0023-workflow-idempotency.md |
| `docs/records/audits/2026-10/mvp3-mod0193-bom-routings-01` | 19 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0193-bom-routings.md, execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-invento |
| `docs/records/audits/2026-10/mvp6-c13-q358-retraction-01` | 20 | yaşayan hatta kendi klasörünü okuyan betik var |
| `docs/records/audits/2026-10/mvp6-g7-grant-removal-01` | 29 | anan: docs/guides/operations/auth-baseline-grant-retraction-runbook.md |
| `docs/records/audits/2026-10/mvp6-golden-flows-http-01` | 1 | anan: services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Carriers/CarrierContractErrorMessageTests.cs |
| `docs/records/audits/2026-10/mvp6-q201-integration-dryrun-01` | 9 | yaşayan hatta kendi klasörünü okuyan betik var |
| `docs/records/audits/2026-10/mvp6-q202a-integration-write-01` | 5 | yaşayan hatta kendi klasörünü okuyan betik var |
| `docs/records/audits/2026-10/mvp6-q208-suite-baseline-01` | 12 | anan: docs/records/audits/2026-10/mvp6-q210-ver-0186-01/tools/q210_matrix.py, docs/records/audits/2026-10/mvp6-q212-ver-0190-01/tools/q212_matrix.py |
| `docs/records/audits/2026-10/mvp6-q214-failpoint-isolation-01` | 18 | anan: docs/records/audits/2026-10/mvp6-q210-ver-0186-01/tools/q210_matrix.py, docs/records/audits/2026-10/mvp6-q212-ver-0190-01/tools/q212_matrix.py |
| `docs/records/audits/2026-10/mvp6-q231-mod0183-18-0-gap-01` | 1 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md |
| `docs/records/audits/2026-10/mvp6-q281-demand-seam-01` | 2 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md, execution/domains/supply-chain-execution/module-packs/MOD-0192-ca |
| `docs/records/audits/2026-10/mvp6-q358-grant-cleanup-01` | 12 | anan: docs/guides/operations/auth-baseline-grant-retraction-runbook.md, services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Operational/Product |
| `docs/records/audits/2026-10/mvp6-q366-scope-chain-registration-01` | 34 | anan: execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md |
| `docs/records/audits/2026-10/mvp6-q380-contract-repin-01` | 3 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md, execution/domains/supply-chain-execution/module-packs/MOD-0187-claims |
| `docs/records/audits/2026-10/mvp6-q450-dcp009-status-01` | 1 | anan: execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md |
| `docs/records/audits/2026-10/mvp6-r03-r07-runtime-01` | 3 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md |
| `docs/records/audits/2026-10/mvp6-r2-returns-ui-01` | 14 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0185-routing-load-planning.md |
| `docs/records/audits/2026-10/mvp6-sandop-capacity-conformance-02` | 4 | anan: services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityRequestShapeTests.cs |
| `docs/records/audits/2026-10/mvp6-stage-r-row13-01` | 1 | MVP-6 kule kuyruğu Q487 kayıt sütunu düz yolla gösterir; `ct_queue.py` çalışma ağacında arar |
| `docs/records/audits/2026-11/mod-0175-fu01-e3-smoke` | 3 | anan: execution/domains/supply-chain-execution/module-packs/MOD-0175-FU01-quarantine-hold-source-reversal.md |
| `docs/records/audits/2026-11/treasury-wp0289-dl1-dispatch-01` | 1 | anan: execution/domains/master-data-management/work-packs/prompts/WP-0289-DL1-v1.1-TASLAK.md |

</details>

<details>
<summary>Girdisi arşivlenen betikler (55 betik, 37 klasör; hattı kapanmış, tek seferlik tarihsel doğrulama)</summary>

Betik ancak klasörü yukarıdaki `restore` komutuyla geri getirildikten sonra koşar.

| Betik | Geri getirilecek klasör |
|---|---|
| `docs/records/audits/2026-09/mod-0184-consumer-technical-evidence/carrier/check_consumer.py` | `docs/records/audits/2026-09/mod-0184-consumer-technical-evidence` |
| `docs/records/audits/2026-09/mod-0184-consumer-technical-evidence/dependents/verify.py` | `docs/records/audits/2026-09/mod-0184-consumer-technical-evidence` |
| `docs/records/audits/2026-09/mod-0184-consumer-technical-evidence/mod0183/compare.py` | `docs/records/audits/2026-09/mod-0184-consumer-technical-evidence` |
| `docs/records/audits/2026-09/mvp6-capacity-duplicate-candidate-reconcile-01/validate_b_oas.py` | `docs/records/audits/2026-09/mvp6-capacity-duplicate-candidate-reconcile-01` |
| `docs/records/audits/2026-09/mvp6-capacity-duplicate-final-release-prep-01/verify_final.py` | `docs/records/audits/2026-09/mvp6-capacity-duplicate-final-release-prep-01` |
| `docs/records/audits/2026-09/mvp6-capacity-duplicate-name-candidate-01/contract_model_checks.py` | `docs/records/audits/2026-09/mvp6-capacity-duplicate-name-candidate-01` |
| `docs/records/audits/2026-09/mvp6-capacity-duplicate-name-candidate-01/verify_candidate.py` | `docs/records/audits/2026-09/mvp6-capacity-duplicate-name-candidate-01` |
| `docs/records/audits/2026-09/mvp6-carrier-auth-le-successor-ver-01/raw/verify_archive.py` | `docs/records/audits/2026-09/mvp6-carrier-auth-le-successor-ver-01` |
| `docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01/verify_release_prep.py` | `docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01` |
| `docs/records/audits/2026-09/mvp6-mod0187-e4-persistence-01/raw/persistence_probe.py` | `docs/records/audits/2026-09/mvp6-mod0187-e4-persistence-01` |
| `docs/records/audits/2026-09/mvp6-mod0187-e4-persistence-01/raw/restart_probe.py` | `docs/records/audits/2026-09/mvp6-mod0187-e4-persistence-01` |
| `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-evidence-02/raw/probes/eventual_recovery.py` | `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-evidence-02` |
| `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-evidence-02/raw/probes/unknown_commit_runner.py` | `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-evidence-02` |
| `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-evidence-02/raw/probes/unknown_not_committed_251.py` | `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-evidence-02` |
| `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01/raw/probes/eventual_recovery.py` | `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01` |
| `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01/raw/probes/independent_timeline.py` | `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01` |
| `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01/raw/probes/unknown_commit_runner.py` | `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01` |
| `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01/raw/probes/unknown_not_committed_251.py` | `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01` |
| `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01/verify-oracle.py` | `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01` |
| `docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01/verify.py` | `docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01` |
| `docs/records/audits/2026-09/mvp6-q103-accepted-base-01/scripts/compose.py` | `docs/records/audits/2026-09/mvp6-q103-accepted-base-01` |
| `docs/records/audits/2026-09/mvp6-q122-ver-claims-v3-01/lane-scripts/claims_runtime.mjs` | `docs/records/audits/2026-09/mvp6-q122-ver-claims-v3-01` |
| `docs/records/audits/2026-09/mvp6-q122-ver-claims-v3-01/lane-scripts/matrix_setup.py` | `docs/records/audits/2026-09/mvp6-q122-ver-claims-v3-01` |
| `docs/records/audits/2026-09/mvp6-q122-ver-claims-v3-01/lane-tools/k09_recheck.sh` | `docs/records/audits/2026-09/mvp6-q122-ver-claims-v3-01` |
| `docs/records/audits/2026-09/mvp6-q129-ver-claims-v4-01/lane-scripts/claims_runtime.mjs` | `docs/records/audits/2026-09/mvp6-q129-ver-claims-v4-01` |
| `docs/records/audits/2026-09/mvp6-q64d-claims-runtime-01/lane-scripts/claims_runtime.mjs` | `docs/records/audits/2026-09/mvp6-q64d-claims-runtime-01` |
| `docs/records/audits/2026-09/mvp6-q65b-returns-mac-01/lane-config/stop-lane.sh` | `docs/records/audits/2026-09/mvp6-q65b-returns-mac-01` |
| `docs/records/audits/2026-09/mvp6-q84b-sop-mac-01/lane-config/stop-lane.sh` | `docs/records/audits/2026-09/mvp6-q84b-sop-mac-01` |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-01/semantic_checks.py` | `docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-01` |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-01/verify_candidate.py` | `docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-01` |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-candidate-01/verify_candidate.py` | `docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-candidate-01` |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-final-release-pack-01/build_final.py` | `docs/records/audits/2026-09/mvp6-sandop-capacity-final-release-pack-01` |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-final-release-pack-01/verify_final.py` | `docs/records/audits/2026-09/mvp6-sandop-capacity-final-release-pack-01` |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-final-ver-01/raw/verify.py` | `docs/records/audits/2026-09/mvp6-sandop-capacity-final-ver-01` |
| `docs/records/audits/2026-09/mvp6-sandop-oas31-validator-01/validate_oas31.py` | `docs/records/audits/2026-09/mvp6-sandop-oas31-validator-01` |
| `docs/records/audits/2026-09/mvp6-shipment-a12-runtime-independent-ver-02/scripts/fixture_prepare.py` | `docs/records/audits/2026-09/mvp6-shipment-a12-runtime-independent-ver-02` |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/VERIFY.py` | `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01` |
| `docs/records/audits/2026-10/mod-0173-int1-evidence/run-int1-probe.ps1` | `docs/records/audits/2026-10/mod-0173-int1-evidence` |
| `docs/records/audits/2026-10/mod-0173-int2-evidence/run-int2-probe.ps1` | `docs/records/audits/2026-10/mod-0173-int2-evidence` |
| `docs/records/audits/2026-10/mvp6-q185-sop-mac-retest-01/lane-config/stop-lane.sh` | `docs/records/audits/2026-10/mvp6-q185-sop-mac-retest-01` |
| `docs/records/audits/2026-10/mvp6-q216-baseline-variance-01/tools/run10.py` | `docs/records/audits/2026-10/mvp6-q216-baseline-variance-01` |
| `docs/records/audits/2026-10/mvp6-q230-commit-plan-01/tools/plan.py` | `docs/records/audits/2026-10/mvp6-q230-commit-plan-01` |
| `docs/records/audits/2026-10/mvp6-q232-unbreak-01/evidence/run.sh` | `docs/records/audits/2026-10/mvp6-q232-unbreak-01` |
| `docs/records/audits/2026-10/mvp6-q236-unbreak-v2-01/evidence/run.sh` | `docs/records/audits/2026-10/mvp6-q236-unbreak-v2-01` |
| `docs/records/audits/2026-10/mvp6-q335-suite-baseline-01/evidence/run-modules.sh` | `docs/records/audits/2026-10/mvp6-q335-suite-baseline-01` |
| `docs/records/audits/2026-10/mvp6-q339r2-module-registration-01/evidence/boot.sh` | `docs/records/audits/2026-10/mvp6-q339r2-module-registration-01` |
| `docs/records/audits/2026-10/mvp6-q339r2-module-registration-01/evidence/run-modules.sh` | `docs/records/audits/2026-10/mvp6-q339r2-module-registration-01` |
| `docs/records/audits/2026-10/mvp6-q353-permission-seed-discovery-01/evidence/queries/start.sh` | `docs/records/audits/2026-10/mvp6-q353-permission-seed-discovery-01` |
| `docs/records/audits/2026-10/mvp6-q358-permission-seed-01/evidence/scripts/start.sh` | `docs/records/audits/2026-10/mvp6-q358-permission-seed-01` |
| `docs/records/audits/2026-10/mvp6-q358-permission-seed-01/evidence/scripts/stop.sh` | `docs/records/audits/2026-10/mvp6-q358-permission-seed-01` |
| `docs/records/audits/2026-10/mvp6-q362-runtime-closure-01/evidence/scripts/fixture.py` | `docs/records/audits/2026-10/mvp6-q362-runtime-closure-01` |
| `docs/records/audits/2026-10/mvp6-q362-runtime-closure-01/evidence/scripts/helper.py` | `docs/records/audits/2026-10/mvp6-q362-runtime-closure-01` |
| `docs/records/audits/2026-10/mvp6-q362-runtime-closure-01/evidence/scripts/start.sh` | `docs/records/audits/2026-10/mvp6-q362-runtime-closure-01` |
| `docs/records/audits/2026-10/mvp6-q372-observability-close-01/evidence/drive.py` | `docs/records/audits/2026-10/mvp6-q372-observability-close-01` |
| `docs/records/audits/2026-10/mvp6-q88b-capacity-mac-01/lane-config/stop-lane.sh` | `docs/records/audits/2026-10/mvp6-q88b-capacity-mac-01` |

</details>

<details>
<summary>Kalkan dosyalar (368 satır, 4786 dosya, 250.8 MiB)</summary>

| Klasör / dosya | Kalkan dosya | MiB |
|---|---|---|
| `docs/records/audits/2026-09/mod-0184-consumer-technical-evidence` | 6 | 1.22 |
| `docs/records/audits/2026-09/mod-0184-dev-03` | 11 | 0.05 |
| `docs/records/audits/2026-09/mod-0184-publication-gate-r1-evidence` | 3 | 0.00 |
| `docs/records/audits/2026-09/mvp6-base-stack-v1` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-base-stack-v2` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-bc-integration-successor-01` | 9 | 0.15 |
| `docs/records/audits/2026-09/mvp6-bc-successor-ct-handoff-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-bc-successor-exec-01` | 2 | 0.00 |
| `docs/records/audits/2026-09/mvp6-bc-successor-exec-02` | 14 | 0.12 |
| `docs/records/audits/2026-09/mvp6-bc-successor-independent-ver-01` | 8 | 0.11 |
| `docs/records/audits/2026-09/mvp6-browser-evidence-environment-recovery-01` | 6 | 0.00 |
| `docs/records/audits/2026-09/mvp6-capacity-duplicate-candidate-reconcile-01` | 3 | 0.00 |
| `docs/records/audits/2026-09/mvp6-capacity-duplicate-final-release-prep-01` | 13 | 0.03 |
| `docs/records/audits/2026-09/mvp6-capacity-duplicate-final-ver-01` | 2 | 0.07 |
| `docs/records/audits/2026-09/mvp6-capacity-duplicate-name-candidate-01` | 16 | 0.08 |
| `docs/records/audits/2026-09/mvp6-capacity-duplicate-publication-exec-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-capacity-guard-single-activation-01` | 5 | 0.02 |
| `docs/records/audits/2026-09/mvp6-capacity-guard-single-disposition-01` | 8 | 0.04 |
| `docs/records/audits/2026-09/mvp6-capacity-ui-draft-01` | 14 | 0.11 |
| `docs/records/audits/2026-09/mvp6-capacity-ui-draft-02` | 3 | 0.07 |
| `docs/records/audits/2026-09/mvp6-capacity-ui-draft-03` | 3 | 0.07 |
| `docs/records/audits/2026-09/mvp6-carrier-auth-chain-recovery-01` | 7 | 0.77 |
| `docs/records/audits/2026-09/mvp6-carrier-auth-le-implement-01` | 5 | 1.84 |
| `docs/records/audits/2026-09/mvp6-carrier-auth-le-independent-ver-01` | 3 | 0.03 |
| `docs/records/audits/2026-09/mvp6-carrier-auth-le-resolution-01` | 8 | 0.02 |
| `docs/records/audits/2026-09/mvp6-carrier-auth-le-successor-dev-01` | 5 | 0.05 |
| `docs/records/audits/2026-09/mvp6-carrier-auth-le-successor-ver-01` | 10 | 0.01 |
| `docs/records/audits/2026-09/mvp6-carrier-auth-token-findings-close-01` | 8 | 3.85 |
| `docs/records/audits/2026-09/mvp6-carrier-auth-token-rework-dev-01` | 7 | 3.86 |
| `docs/records/audits/2026-09/mvp6-carrier-auth-token-rework-ver-01` | 13 | 0.41 |
| `docs/records/audits/2026-09/mvp6-carrier-auth-token-rework-ver-02` | 5 | 0.06 |
| `docs/records/audits/2026-09/mvp6-carrier-browser-evidence-01` | 44 | 17.78 |
| `docs/records/audits/2026-09/mvp6-carrier-numericdate-exec-01` | 6 | 1.09 |
| `docs/records/audits/2026-09/mvp6-carrier-numericdate-exec-ver-01` | 7 | 0.63 |
| `docs/records/audits/2026-09/mvp6-carrier-numericdate-rework-01` | 9 | 3.93 |
| `docs/records/audits/2026-09/mvp6-carrier-real-auth-ct-review-01` | 6 | 0.01 |
| `docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-close-01` | 5 | 0.01 |
| `docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-exec-01` | 34 | 0.67 |
| `docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-successor-01` | 7 | 0.01 |
| `docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-ver-01` | 5 | 0.01 |
| `docs/records/audits/2026-09/mvp6-carrier-real-auth-e2e-ver-02` | 7 | 0.01 |
| `docs/records/audits/2026-09/mvp6-carrier-ui-quality-close-01` | 33 | 7.04 |
| `docs/records/audits/2026-09/mvp6-carrier-ver-environment-close-01` | 7 | 0.01 |
| `docs/records/audits/2026-09/mvp6-claims-candidate-reconstruct-r2` | 1 | 0.06 |
| `docs/records/audits/2026-09/mvp6-claims-pack-apply-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-claims-r2-independent-ver-01` | 1 | 0.08 |
| `docs/records/audits/2026-09/mvp6-claims-r2-stateful-rework-01` | 1 | 0.09 |
| `docs/records/audits/2026-09/mvp6-claims-stateful-independent-ver-02` | 1 | 0.65 |
| `docs/records/audits/2026-09/mvp6-claims-ui-draft-01` | 14 | 0.08 |
| `docs/records/audits/2026-09/mvp6-claims-ui-draft-02` | 7 | 0.11 |
| `docs/records/audits/2026-09/mvp6-claims-ui-draft-03` | 214 | 8.34 |
| `docs/records/audits/2026-09/mvp6-claims-ui-draft-04` | 2 | 0.08 |
| `docs/records/audits/2026-09/mvp6-claims-v4-integration-target-disposition-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-combined-candidate-ver-01` | 1 | 1.11 |
| `docs/records/audits/2026-09/mvp6-combined-final-release-pack-01` | 5 | 0.04 |
| `docs/records/audits/2026-09/mvp6-combined-final-release-ver-01` | 2 | 0.10 |
| `docs/records/audits/2026-09/mvp6-consumer-isolation-map-01` | 8 | 0.27 |
| `docs/records/audits/2026-09/mvp6-consumer-packages-exact-recovery-01` | 3 | 0.04 |
| `docs/records/audits/2026-09/mvp6-continuation-review-2026-09-16` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-evidence-kit-install-01` | 2 | 0.01 |
| `docs/records/audits/2026-09/mvp6-evidence-kit-v1-1-independent-review-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-evidence-kit-v1-2-recheck-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-final-publication-execution-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-guard-payload-path-disposition-r2` | 15 | 2.45 |
| `docs/records/audits/2026-09/mvp6-integration-baseline-exec-01` | 4 | 0.09 |
| `docs/records/audits/2026-09/mvp6-loads-publication-guard-01` | 20 | 0.11 |
| `docs/records/audits/2026-09/mvp6-loads-publication-guard-independent-ver-01` | 15 | 0.04 |
| `docs/records/audits/2026-09/mvp6-loads-root-acquisition-disposition-01` | 3 | 0.01 |
| `docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01` | 9 | 0.07 |
| `docs/records/audits/2026-09/mvp6-loads-uptake-dev-01` | 6 | 0.01 |
| `docs/records/audits/2026-09/mvp6-loads-uptake-draft-01` | 5 | 0.03 |
| `docs/records/audits/2026-09/mvp6-mod0185-acceptance-consolidation-01` | 4 | 0.03 |
| `docs/records/audits/2026-09/mvp6-mod0185-acceptance-consolidation-02` | 4 | 0.03 |
| `docs/records/audits/2026-09/mvp6-mod0185-evidence-recovery-02` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-mod0185-fresh-evidence-ver-03` | 2 | 0.35 |
| `docs/records/audits/2026-09/mvp6-mod0186-http-01` | 8 | 0.36 |
| `docs/records/audits/2026-09/mvp6-mod0186-http-composition-01` | 9 | 0.29 |
| `docs/records/audits/2026-09/mvp6-mod0186-http-composition-apply-01` | 2 | 0.00 |
| `docs/records/audits/2026-09/mvp6-mod0186-pack-activate-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-mod0186-patch-disposition-02` | 3 | 0.01 |
| `docs/records/audits/2026-09/mvp6-mod0186-r01-rework-01` | 140 | 1.43 |
| `docs/records/audits/2026-09/mvp6-mod0187-acceptance-consolidation-01` | 2 | 0.02 |
| `docs/records/audits/2026-09/mvp6-mod0187-auth-05` | 3 | 0.10 |
| `docs/records/audits/2026-09/mvp6-mod0187-auth05-independent-ver-01` | 3 | 0.10 |
| `docs/records/audits/2026-09/mvp6-mod0187-ct-accept-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-mod0187-e4-persistence-01` | 8 | 0.78 |
| `docs/records/audits/2026-09/mvp6-mod0187-e4-policy-01` | 2 | 0.10 |
| `docs/records/audits/2026-09/mvp6-mod0187-e4-rework-scope-01` | 10 | 0.10 |
| `docs/records/audits/2026-09/mvp6-mod0187-http-composition-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-mod0187-malformed-map-rework-01` | 5 | 0.21 |
| `docs/records/audits/2026-09/mvp6-mod0187-normal-baseline-integration-01` | 4 | 0.52 |
| `docs/records/audits/2026-09/mvp6-mod0187-normal-runtime-ver-01` | 44 | 3.00 |
| `docs/records/audits/2026-09/mvp6-mod0187-r14-apply-01` | 25 | 0.89 |
| `docs/records/audits/2026-09/mvp6-mod0187-r21-apply-01` | 4 | 0.30 |
| `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-evidence-02` | 43 | 4.20 |
| `docs/records/audits/2026-09/mvp6-mod0187-r22-r25-independent-ver-01` | 38 | 1.22 |
| `docs/records/audits/2026-09/mvp6-mod0190-401-disposition-ct-02` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-mod0190-core-dev-01` | 6 | 0.28 |
| `docs/records/audits/2026-09/mvp6-mod0190-core-rever-01` | 2 | 0.03 |
| `docs/records/audits/2026-09/mvp6-mod0190-core-rework-01` | 6 | 0.04 |
| `docs/records/audits/2026-09/mvp6-mod0190-core-ver-01` | 2 | 0.03 |
| `docs/records/audits/2026-09/mvp6-mod0190-ct-acceptance-review-01` | 2 | 0.01 |
| `docs/records/audits/2026-09/mvp6-mod0190-f01-f04-independent-ver-01` | 1 | 0.14 |
| `docs/records/audits/2026-09/mvp6-mod0190-http-dev-01-r1` | 7 | 0.58 |
| `docs/records/audits/2026-09/mvp6-mod0190-http-dev-01` | 13 | 0.54 |
| `docs/records/audits/2026-09/mvp6-mod0190-http-dev-02` | 8 | 0.15 |
| `docs/records/audits/2026-09/mvp6-mod0190-http-di-gap-01` | 3 | 0.00 |
| `docs/records/audits/2026-09/mvp6-mod0190-http-independent-ver-01` | 1 | 0.09 |
| `docs/records/audits/2026-09/mvp6-mod0190-http-integration-prep-01` | 5 | 0.02 |
| `docs/records/audits/2026-09/mvp6-mod0190-isolated-dev-01` | 1 | 0.05 |
| `docs/records/audits/2026-09/mvp6-mod0190-test-oracle-independent-ver-01` | 1 | 0.05 |
| `docs/records/audits/2026-09/mvp6-mod0192-core-dev-01` | 7 | 0.73 |
| `docs/records/audits/2026-09/mvp6-mod0192-core-ver-01` | 2 | 0.23 |
| `docs/records/audits/2026-09/mvp6-mod0192-duplicate-name-uptake-prep-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-mod0192-executor-fault-rever-01` | 4 | 0.11 |
| `docs/records/audits/2026-09/mvp6-mod0192-executor-fault-rework-01` | 22 | 1.46 |
| `docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01` | 11 | 0.02 |
| `docs/records/audits/2026-09/mvp6-mod0192-http-process-dev-01` | 10 | 0.34 |
| `docs/records/audits/2026-09/mvp6-mod0192-http-process-independent-ver-01` | 13 | 0.19 |
| `docs/records/audits/2026-09/mvp6-mod0192-production-rework-pack-01` | 3 | 0.01 |
| `docs/records/audits/2026-09/mvp6-mod0192-restart-harness-rever-01` | 2 | 0.09 |
| `docs/records/audits/2026-09/mvp6-mod0192-restart-harness-rework-01` | 24 | 4.15 |
| `docs/records/audits/2026-09/mvp6-mod0192-x01-x07-independent-ver-01` | 3 | 0.20 |
| `docs/records/audits/2026-09/mvp6-mod0192-x01-x07-production-rework-01` | 3 | 0.03 |
| `docs/records/audits/2026-09/mvp6-mod0192-x07-http-disposition-01` | 6 | 0.04 |
| `docs/records/audits/2026-09/mvp6-pack-apply-independent-ver-01` | 1 | 0.02 |
| `docs/records/audits/2026-09/mvp6-pack-apply-q27-q28-01` | 2 | 0.00 |
| `docs/records/audits/2026-09/mvp6-pack-apply-q27-q28-independent-ver-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-pack-apply-q46-q47-01` | 2 | 0.00 |
| `docs/records/audits/2026-09/mvp6-pack-apply-q53-01` | 2 | 0.00 |
| `docs/records/audits/2026-09/mvp6-pack-apply-q53-independent-ver-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-pack-labels-apply-01` | 2 | 0.01 |
| `docs/records/audits/2026-09/mvp6-pack-labels-independent-ver-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-q101-control-audit-01` | 1 | 0.08 |
| `docs/records/audits/2026-09/mvp6-q101b-control-audit-01` | 2 | 0.19 |
| `docs/records/audits/2026-09/mvp6-q103-accepted-base-01` | 8 | 5.27 |
| `docs/records/audits/2026-09/mvp6-q117-test-guard-fixes-01` | 10 | 1.21 |
| `docs/records/audits/2026-09/mvp6-q119-ver-q117-01` | 14 | 0.95 |
| `docs/records/audits/2026-09/mvp6-q121-mongo-guard-fixes-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-q121b-build-test-01` | 19 | 2.67 |
| `docs/records/audits/2026-09/mvp6-q121c-supplychain-rerun-01` | 17 | 0.36 |
| `docs/records/audits/2026-09/mvp6-q122-ver-claims-v3-01` | 412 | 20.29 |
| `docs/records/audits/2026-09/mvp6-q129-ver-claims-v4-01` | 186 | 8.39 |
| `docs/records/audits/2026-09/mvp6-q131a-port-uri-overlay-01` | 3 | 0.01 |
| `docs/records/audits/2026-09/mvp6-q139-readiness-process-audit-01` | 4 | 0.07 |
| `docs/records/audits/2026-09/mvp6-q144-ui-checklist-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-q145-ver-q144-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-q152-uc01-lock-investigation-01` | 25 | 0.36 |
| `docs/records/audits/2026-09/mvp6-q154-q131-mac-ver-01` | 33 | 3.02 |
| `docs/records/audits/2026-09/mvp6-q164-ui-checklist-02` | 1 | 0.02 |
| `docs/records/audits/2026-09/mvp6-q165-ver-q164-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-q172-ver-sop-v2-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-q173-ver-capacity-v2-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-q174-ver-returns-v2-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-q184-ver-draft-03` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-q24b-kit-validation-01` | 6 | 0.02 |
| `docs/records/audits/2026-09/mvp6-q64b-claims-build-01` | 270 | 12.75 |
| `docs/records/audits/2026-09/mvp6-q64d-claims-runtime-01` | 211 | 11.87 |
| `docs/records/audits/2026-09/mvp6-q65b-returns-mac-01` | 152 | 10.30 |
| `docs/records/audits/2026-09/mvp6-q84b-sop-mac-01` | 218 | 18.36 |
| `docs/records/audits/2026-09/mvp6-r2-combined-release-candidate-01` | 3 | 0.30 |
| `docs/records/audits/2026-09/mvp6-r2-release-handoff-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-readiness-remeasurement-20260924` | 2 | 0.06 |
| `docs/records/audits/2026-09/mvp6-returns-candidate-reconstruct-r2` | 1 | 0.06 |
| `docs/records/audits/2026-09/mvp6-returns-r2-independent-ver-01` | 2 | 1.38 |
| `docs/records/audits/2026-09/mvp6-returns-r2-uuid-rework-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-returns-r2-uuid-ver-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-returns-ui-draft-01` | 14 | 0.11 |
| `docs/records/audits/2026-09/mvp6-returns-ui-draft-02` | 3 | 0.07 |
| `docs/records/audits/2026-09/mvp6-root-candidate-reconstruct-r2-01` | 1 | 0.02 |
| `docs/records/audits/2026-09/mvp6-root-guard-disposition-recovery-01` | 4 | 0.02 |
| `docs/records/audits/2026-09/mvp6-root-guard-fixture-rework-01` | 2 | 0.01 |
| `docs/records/audits/2026-09/mvp6-root-r2-independent-ver-01` | 1 | 0.02 |
| `docs/records/audits/2026-09/mvp6-root-r2-producer-dev-01` | 5 | 0.04 |
| `docs/records/audits/2026-09/mvp6-root-r2-producer-ver-01` | 6 | 0.00 |
| `docs/records/audits/2026-09/mvp6-root-r2-producer-ver-02` | 7 | 0.01 |
| `docs/records/audits/2026-09/mvp6-root-r2-restart-evidence-dev-03` | 6 | 0.01 |
| `docs/records/audits/2026-09/mvp6-root-r2-runtime-evidence-dev-02` | 6 | 0.01 |
| `docs/records/audits/2026-09/mvp6-sandop-annex-status-disposition-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-01` | 2 | 0.01 |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-amendment-candidate-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-candidate-reconcile-01` | 2 | 0.01 |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-final-authority-close-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-final-pack-01` | 2 | 0.00 |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-final-release-pack-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-final-ver-01` | 5 | 0.05 |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-publication-01` | 2 | 0.02 |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-publication-exec-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-sandop-capacity-r2-independent-ver-01` | 1 | 0.01 |
| `docs/records/audits/2026-09/mvp6-sandop-oas31-validator-01` | 2 | 0.00 |
| `docs/records/audits/2026-09/mvp6-sandop-publication-guard-disposition-01` | 6 | 0.03 |
| `docs/records/audits/2026-09/mvp6-shipment-a08-a09-a12-ct-disposition-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-shipment-a12-runtime-independent-ver-01` | 3 | 0.00 |
| `docs/records/audits/2026-09/mvp6-shipment-a12-runtime-independent-ver-02` | 154 | 5.71 |
| `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-independent-ver-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01` | 3 | 0.06 |
| `docs/records/audits/2026-09/mvp6-shipment-a12-ver02-independent-review-01` | 1 | 0.02 |
| `docs/records/audits/2026-09/mvp6-shipment-acceptance-reconcile-01` | 2 | 0.03 |
| `docs/records/audits/2026-09/mvp6-shipment-browser-real-auth-ct-handoff-01` | 2 | 0.00 |
| `docs/records/audits/2026-09/mvp6-shipment-carrier-predecessor-pin-01` | 8 | 0.07 |
| `docs/records/audits/2026-09/mvp6-shipment-integration-rework-exec-01` | 28 | 0.80 |
| `docs/records/audits/2026-09/mvp6-shipment-integration-rework-independent-ver-01` | 37 | 0.25 |
| `docs/records/audits/2026-09/mvp6-shipment-integration-rework-prep-01` | 19 | 0.40 |
| `docs/records/audits/2026-09/mvp6-shipment-iso-final-ver-lnx-01` | 2 | 0.00 |
| `docs/records/audits/2026-09/mvp6-shipment-line-accessibility-ct-disposition-01` | 5 | 0.00 |
| `docs/records/audits/2026-09/mvp6-shipment-line-accessibility-independent-ver-01` | 2 | 0.02 |
| `docs/records/audits/2026-09/mvp6-shipment-line-accessibility-rework-01` | 8 | 0.67 |
| `docs/records/audits/2026-09/mvp6-shipment-pod-ui-exec-01` | 10 | 0.10 |
| `docs/records/audits/2026-09/mvp6-shipment-port-disposition-01` | 10 | 0.02 |
| `docs/records/audits/2026-09/mvp6-shipment-real-auth-browser-ver-01` | 8 | 0.08 |
| `docs/records/audits/2026-09/mvp6-shipment-real-auth-http-recovery-01` | 56 | 2.37 |
| `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01` | 36 | 0.25 |
| `docs/records/audits/2026-09/mvp6-shipment-root-binding-approval-01` | 1 | 0.00 |
| `docs/records/audits/2026-09/mvp6-shipment-shared-ui-ct-disposition-01` | 5 | 0.01 |
| `docs/records/audits/2026-09/mvp6-shipment-shared-ui-disposition-01` | 5 | 0.01 |
| `docs/records/audits/2026-09/mvp6-shipment-shared-ui-exec-01` | 8 | 0.00 |
| `docs/records/audits/2026-09/mvp6-shipment-shared-ui-independent-ver-01` | 4 | 0.02 |
| `docs/records/audits/2026-09/mvp6-shipment-ui-b01-b02-independent-ver-01` | 10 | 0.98 |
| `docs/records/audits/2026-09/mvp6-shipment-ui-b01-b02-rework-01` | 8 | 0.85 |
| `docs/records/audits/2026-09/mvp6-shipment-ui-functional-ct-consolidation-01` | 4 | 0.01 |
| `docs/records/audits/2026-09/mvp6-sop-ui-draft-01` | 14 | 0.10 |
| `docs/records/audits/2026-09/mvp6-sop-ui-draft-02` | 3 | 0.07 |
| `docs/records/audits/2026-09/mvp6-sop-ui-draft-03` | 3 | 0.06 |
| `docs/records/audits/2026-09/mvp6-three-scope-application-01` | 7 | 0.59 |
| `docs/records/audits/2026-10/ct-ci-01` | 5 | 0.31 |
| `docs/records/audits/2026-10/ct-discipline-and-readiness-audit-2026-10-17-01` | 4 | 0.01 |
| `docs/records/audits/2026-10/ct-final-local-01` | 4 | 0.27 |
| `docs/records/audits/2026-10/ct-fix-01` | 9 | 0.30 |
| `docs/records/audits/2026-10/ct-fix-02` | 8 | 0.32 |
| `docs/records/audits/2026-10/ct-fix-03` | 19 | 0.37 |
| `docs/records/audits/2026-10/ct-fix-04` | 8 | 0.29 |
| `docs/records/audits/2026-10/ct-fix-05` | 12 | 0.30 |
| `docs/records/audits/2026-10/ct-fix-06` | 8 | 0.03 |
| `docs/records/audits/2026-10/ct-fix-07` | 6 | 0.02 |
| `docs/records/audits/2026-10/ct-fix-08` | 6 | 0.03 |
| `docs/records/audits/2026-10/ct-fix-09` | 6 | 0.03 |
| `docs/records/audits/2026-10/ct-fix-10` | 6 | 0.02 |
| `docs/records/audits/2026-10/ct-fix-11` | 5 | 0.02 |
| `docs/records/audits/2026-10/ct-i2-list-hook-01` | 7 | 0.30 |
| `docs/records/audits/2026-10/ct-i4-s2-01` | 4 | 0.32 |
| `docs/records/audits/2026-10/ct-merge-preflight-01` | 9 | 0.27 |
| `docs/records/audits/2026-10/ct-mvp6-codex-handoff-01` | 5 | 0.04 |
| `docs/records/audits/2026-10/ct-ops-03` | 2 | 0.29 |
| `docs/records/audits/2026-10/ct-ops-04` | 5 | 0.31 |
| `docs/records/audits/2026-10/ct-pilot-01` | 6 | 0.05 |
| `docs/records/audits/2026-10/ct-pqg-01` | 3 | 0.28 |
| `docs/records/audits/2026-10/ct-pre-merge-fixes-01` | 6 | 0.54 |
| `docs/records/audits/2026-10/ct-route-01` | 10 | 0.38 |
| `docs/records/audits/2026-10/ct-rule-collision-rep001-k4-01` | 1 | 0.00 |
| `docs/records/audits/2026-10/ct-step2-starter-kit-01` | 7 | 0.90 |
| `docs/records/audits/2026-10/ct-step9-docs-01` | 1 | 0.01 |
| `docs/records/audits/2026-10/ct-vdp-early-return-01` | 4 | 0.29 |
| `docs/records/audits/2026-10/hcm-employee-discovery-01` | 1 | 0.05 |
| `docs/records/audits/2026-10/i5b-fix-03` | 4 | 0.01 |
| `docs/records/audits/2026-10/i5b-fix-04` | 3 | 0.01 |
| `docs/records/audits/2026-10/i5b-fix-05` | 5 | 0.27 |
| `docs/records/audits/2026-10/i5b-fix-06` | 3 | 0.01 |
| `docs/records/audits/2026-10/i5b-fix-07` | 3 | 0.01 |
| `docs/records/audits/2026-10/i5b-fix-08` | 4 | 0.00 |
| `docs/records/audits/2026-10/i5b-fix-09` | 5 | 0.02 |
| `docs/records/audits/2026-10/i5b-fix-10` | 5 | 0.03 |
| `docs/records/audits/2026-10/i5b-fix-11` | 5 | 0.03 |
| `docs/records/audits/2026-10/i5b-fix-12` | 5 | 0.03 |
| `docs/records/audits/2026-10/i5b-fix-13` | 5 | 0.03 |
| `docs/records/audits/2026-10/i5b-fix-14` | 5 | 0.03 |
| `docs/records/audits/2026-10/i5b-fix-15` | 9 | 0.05 |
| `docs/records/audits/2026-10/i5b-fix-16` | 11 | 0.09 |
| `docs/records/audits/2026-10/i5b-fix-17` | 9 | 0.06 |
| `docs/records/audits/2026-10/i5b-fix-18` | 8 | 0.04 |
| `docs/records/audits/2026-10/mg-meetings-discovery-01` | 1 | 0.05 |
| `docs/records/audits/2026-10/mod-0022-e5w1-shots` | 11 | 0.23 |
| `docs/records/audits/2026-10/mod-0022-e5w2-shots` | 17 | 0.27 |
| `docs/records/audits/2026-10/mod-0173-int1-evidence` | 14 | 0.11 |
| `docs/records/audits/2026-10/mod-0173-int2-evidence` | 14 | 0.10 |
| `docs/records/audits/2026-10/mod-0173-ops1-evidence` | 9 | 0.03 |
| `docs/records/audits/2026-10/mod-0173-pm1-shots` | 4 | 0.13 |
| `docs/records/audits/2026-10/mod-0173-s1-evidence` | 29 | 0.55 |
| `docs/records/audits/2026-10/mod-0173-s2-evidence` | 29 | 0.83 |
| `docs/records/audits/2026-10/mod-0174-item-shots` | 12 | 0.42 |
| `docs/records/audits/2026-10/mod-0175-q5-shots` | 18 | 0.67 |
| `docs/records/audits/2026-10/mod-0176-f4-shots` | 13 | 0.47 |
| `docs/records/audits/2026-10/mod-0176-f5-sample` | 2 | 0.05 |
| `docs/records/audits/2026-10/mod-0176-f5-shots` | 19 | 0.54 |
| `docs/records/audits/2026-10/mvp1-closure-rep001` | 8 | 0.15 |
| `docs/records/audits/2026-10/mvp1-fix-live1-shots` | 3 | 0.09 |
| `docs/records/audits/2026-10/mvp1-fub2-shots` | 5 | 0.15 |
| `docs/records/audits/2026-10/mvp1-sync1-shots` | 6 | 0.14 |
| `docs/records/audits/2026-10/mvp2-supplier-tenant-01` | 4 | 0.01 |
| `docs/records/audits/2026-10/mvp6-0192-row202-evidence-01` | 6 | 0.01 |
| `docs/records/audits/2026-10/mvp6-c02-error-codes-01` | 1 | 0.00 |
| `docs/records/audits/2026-10/mvp6-c03-gateway-route-01` | 8 | 0.03 |
| `docs/records/audits/2026-10/mvp6-g5-e2e-chain-01` | 2 | 0.00 |
| `docs/records/audits/2026-10/mvp6-gateway-idempotency-replay-01` | 2 | 0.00 |
| `docs/records/audits/2026-10/mvp6-main-merge-completion-gate-01` | 10 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q185-sop-mac-retest-01` | 99 | 7.43 |
| `docs/records/audits/2026-10/mvp6-q189-ver-returns-01` | 1 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q198-backup-01` | 1 | 0.00 |
| `docs/records/audits/2026-10/mvp6-q205-testenv-01` | 5 | 0.25 |
| `docs/records/audits/2026-10/mvp6-q210-ver-0186-01` | 3 | 0.07 |
| `docs/records/audits/2026-10/mvp6-q211-ver-0187-01` | 3 | 0.04 |
| `docs/records/audits/2026-10/mvp6-q212-ver-0190-01` | 4 | 0.07 |
| `docs/records/audits/2026-10/mvp6-q213-ver-0192-01` | 4 | 0.04 |
| `docs/records/audits/2026-10/mvp6-q216-baseline-variance-01` | 20 | 0.17 |
| `docs/records/audits/2026-10/mvp6-q218-contract-pin-01` | 2 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q220-sandop-registration-01` | 16 | 0.03 |
| `docs/records/audits/2026-10/mvp6-q221-q209-prework-01` | 1 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q230-commit-plan-01` | 78 | 2.60 |
| `docs/records/audits/2026-10/mvp6-q231-mod0183-18-0-gap-01` | 2 | 0.02 |
| `docs/records/audits/2026-10/mvp6-q232-unbreak-01` | 12 | 0.17 |
| `docs/records/audits/2026-10/mvp6-q233-q234-pack-reconcile-01` | 8 | 0.26 |
| `docs/records/audits/2026-10/mvp6-q235-secret-ruling-01` | 1 | 0.02 |
| `docs/records/audits/2026-10/mvp6-q236-ct-verify-01` | 2 | 0.00 |
| `docs/records/audits/2026-10/mvp6-q236-unbreak-v2-01` | 11 | 0.07 |
| `docs/records/audits/2026-10/mvp6-q244-disk-01` | 3 | 0.02 |
| `docs/records/audits/2026-10/mvp6-q245-ver-0183-01` | 3 | 0.05 |
| `docs/records/audits/2026-10/mvp6-q246r2-worktree-rescue-01` | 9 | 2.92 |
| `docs/records/audits/2026-10/mvp6-q266-testenv-01` | 12 | 0.06 |
| `docs/records/audits/2026-10/mvp6-q271-q272-compose-01` | 5 | 0.02 |
| `docs/records/audits/2026-10/mvp6-q313-intake-pii-01` | 3 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q332-mod0183-conformance-01` | 2 | 0.05 |
| `docs/records/audits/2026-10/mvp6-q335-suite-baseline-01` | 15 | 0.06 |
| `docs/records/audits/2026-10/mvp6-q339r2-module-registration-01` | 13 | 0.05 |
| `docs/records/audits/2026-10/mvp6-q353-permission-seed-discovery-01` | 10 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q358-permission-seed-01` | 26 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q362-runtime-closure-01` | 42 | 1.02 |
| `docs/records/audits/2026-10/mvp6-q371-ux-l10n-close-01` | 21 | 0.25 |
| `docs/records/audits/2026-10/mvp6-q372-observability-close-01` | 10 | 0.02 |
| `docs/records/audits/2026-10/mvp6-q374-datetime-utc-local-01` | 9 | 0.00 |
| `docs/records/audits/2026-10/mvp6-q381-uncomposed-routes-404-01` | 11 | 0.02 |
| `docs/records/audits/2026-10/mvp6-q382-date-tests-01` | 4 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q394-web-cross-cutting-01` | 10 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q403-intent-definition-01` | 1 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q419-shipment-intent-root-01` | 15 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q420-denial-not-outage-01` | 12 | 0.02 |
| `docs/records/audits/2026-10/mvp6-q446-gate-18-0-four-modules-01` | 2 | 0.03 |
| `docs/records/audits/2026-10/mvp6-q447-return-403-test-01` | 5 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q448-flaky-p95-01` | 11 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q449-scope-intent-01` | 2 | 0.00 |
| `docs/records/audits/2026-10/mvp6-q453-observability-four-01` | 5 | 0.01 |
| `docs/records/audits/2026-10/mvp6-q484-capacity-branch-01` | 4 | 0.04 |
| `docs/records/audits/2026-10/mvp6-q88b-capacity-mac-01` | 195 | 18.47 |
| `docs/records/audits/2026-10/mvp6-r03-r07-runtime-01` | 14 | 6.68 |
| `docs/records/audits/2026-10/mvp6-r04-pii-logs-01` | 1 | 0.01 |
| `docs/records/audits/2026-10/mvp6-r3-recipe-amend-guard-01` | 3 | 0.00 |
| `docs/records/audits/2026-10/mvp6-r4a-carriers-ui-01` | 9 | 0.01 |
| `docs/records/audits/2026-10/mvp6-r4b-loads-ui-01` | 10 | 0.02 |
| `docs/records/audits/2026-10/mvp6-r4c-claims-ui-01` | 15 | 0.04 |
| `docs/records/audits/2026-10/mvp6-returns-guard-01` | 3 | 0.01 |
| `docs/records/audits/2026-10/mvp6-returns-ui-draft-03` | 3 | 0.07 |
| `docs/records/audits/2026-10/mvp6-sandop-capacity-conformance-01` | 5 | 0.01 |
| `docs/records/audits/2026-10/mvp6-stage-r-gateway-contract-01` | 1 | 0.00 |
| `docs/records/audits/2026-10/mvp6-stage-r-row1-loads-config-01` | 6 | 2.28 |
| `docs/records/audits/2026-10/mvp6-stage-r-row13-01` | 2 | 0.52 |
| `docs/records/audits/2026-10/mvp6-stage-r-row13-02-returns-claims` | 3 | 0.11 |
| `docs/records/audits/2026-10/mvp6-stage-r-row5-returns-01` | 14 | 0.08 |
| `docs/records/audits/2026-10/mvp6-stage-r-stack-up-01` | 1 | 0.00 |
| `docs/records/audits/2026-10/pss-ui-p1-03-discovery-01` | 1 | 0.07 |
| `docs/records/audits/2026-10/pvg-m1-01-discovery-01` | 3 | 0.17 |
| `docs/records/audits/2026-10/pvg-m1-01-discovery-02` | 1 | 0.06 |
| `docs/records/audits/2026-10/pvg-wave1-dispatch-01` | 3 | 0.00 |
| `docs/records/audits/2026-10/pvg-wave2-dispatch-01` | 2 | 0.00 |
| `docs/records/audits/2026-10/pvg-wave3-dispatch-01` | 2 | 0.00 |
| `docs/records/audits/2026-10/pvg-wave4-dispatch-01` | 3 | 0.00 |
| `docs/records/audits/2026-10/pvg-wave5-dispatch-01` | 1 | 0.00 |
| `docs/records/audits/2026-10/pvg-wave8-dispatch-01` | 1 | 0.00 |
| `docs/records/audits/2026-10/settings-hygiene-01` | 2 | 0.01 |
| `docs/records/audits/2026-11/mod-0178-dl3-375px` | 2 | 0.03 |
| `docs/records/audits/2026-11/treasury-wp0289-dl1-dispatch-01` | 3 | 0.12 |

</details>

## Parti 4 — `logs/`, `.logs/`, kök artıkları, eski Blueprint sürümleri

**Neden:** `logs/` ve `.logs/` eski koşu çıktıları, hiçbir betik bu klasörlere yazmıyor ya da okumuyor; kökteki üç artık yanlışlıkla eklenmiş çıktı; Blueprint 8.1/8.7 ve değişiklik notları — kimlik kapısı (`verify_module_id.py:29`) yalnız 8.8 tablosunu okur, o yerinde.

<details>
<summary>Kalkan dosyalar (66 satır, 100 dosya, 13.0 MiB)</summary>

| Klasör / dosya | Kalkan dosya | MiB |
|---|---|---|
| `.logs/AuthService.err.log` | 1 | 0.00 |
| `.logs/AuthService.out.log` | 1 | 0.01 |
| `.logs/DevEnablementService.err.log` | 1 | 0.00 |
| `.logs/DevEnablementService.out.log` | 1 | 0.00 |
| `.logs/EnterpriseStrategyService.err.log` | 1 | 0.00 |
| `.logs/EnterpriseStrategyService.out.log` | 1 | 0.00 |
| `.logs/Frontend.err.log` | 1 | 0.00 |
| `.logs/Frontend.out.log` | 1 | 0.02 |
| `.logs/Gateway.err.log` | 1 | 0.00 |
| `.logs/Gateway.out.log` | 1 | 0.19 |
| `.logs/PlatformService.err.log` | 1 | 0.13 |
| `.logs/PlatformService.out.log` | 1 | 0.16 |
| `.logs/restart-services` | 12 | 1.50 |
| `.logs/watch-auth.err.log` | 1 | 0.00 |
| `.logs/watch-auth.out.log` | 1 | 0.00 |
| `.logs/watch-devenablement.err.log` | 1 | 0.00 |
| `.logs/watch-devenablement.out.log` | 1 | 0.00 |
| `.logs/watch-frontend.err.log` | 1 | 0.00 |
| `.logs/watch-frontend.out.log` | 1 | 0.00 |
| `.logs/watch-gateway.err.log` | 1 | 0.00 |
| `.logs/watch-gateway.out.log` | 1 | 0.00 |
| `.logs/watch-platform.err.log` | 1 | 0.00 |
| `.logs/watch-platform.out.log` | 1 | 0.14 |
| `docs/reference` | 4 | 2.76 |
| `exit=0,` | 1 | 0.00 |
| `fleet-detached.log` | 1 | 2.48 |
| `grep.exe.stackdump` | 1 | 0.00 |
| `logs/Auth-err.log` | 1 | 0.00 |
| `logs/Auth-out.log` | 1 | 0.03 |
| `logs/Auth-restart-out.log` | 1 | 0.03 |
| `logs/Auth-watch-err.log` | 1 | 0.00 |
| `logs/Auth-watch-out.log` | 1 | 0.65 |
| `logs/Crm-fu07-err.log` | 1 | 0.00 |
| `logs/Crm-fu07-out.log` | 1 | 0.04 |
| `logs/Crm-watch-err.log` | 1 | 0.00 |
| `logs/Crm-watch-out.log` | 1 | 0.00 |
| `logs/DevEnablement-err.log` | 1 | 0.00 |
| `logs/DevEnablement-out.log` | 1 | 0.02 |
| `logs/DevEnablement-watch-err.log` | 1 | 0.00 |
| `logs/DevEnablement-watch-out.log` | 1 | 0.02 |
| `logs/Gateway-err.log` | 1 | 0.00 |
| `logs/Gateway-out.log` | 1 | 0.04 |
| `logs/Gateway-watch-err.log` | 1 | 0.00 |
| `logs/Gateway-watch-out.log` | 1 | 0.30 |
| `logs/Hcm-watch-err.log` | 1 | 0.00 |
| `logs/Hcm-watch-out.log` | 1 | 0.00 |
| `logs/Mdm-err.log` | 1 | 0.00 |
| `logs/Mdm-out.log` | 1 | 0.03 |
| `logs/Mdm-watch-err.log` | 1 | 0.00 |
| `logs/Mdm-watch-out.log` | 1 | 0.00 |
| `logs/Platform-err.log` | 1 | 0.01 |
| `logs/Platform-manual.log` | 1 | 0.00 |
| `logs/Platform-out.log` | 1 | 0.15 |
| `logs/Platform-watch-err.log` | 1 | 0.00 |
| `logs/Platform-watch-out.log` | 1 | 2.27 |
| `logs/Web-err.log` | 1 | 0.01 |
| `logs/Web-fu07-err.log` | 1 | 0.00 |
| `logs/Web-fu07-out.log` | 1 | 0.00 |
| `logs/Web-out.log` | 1 | 0.03 |
| `logs/Web-watch-err.log` | 1 | 0.01 |
| `logs/Web-watch-out.log` | 1 | 0.08 |
| `logs/dotnet-watch` | 21 | 1.58 |
| `logs/platform-manual.err.log` | 1 | 0.00 |
| `logs/platform-manual.out.log` | 1 | 0.02 |
| `logs/watch-suite-launcher.err.log` | 1 | 0.00 |
| `logs/watch-suite-launcher.log` | 1 | 0.28 |

</details>
