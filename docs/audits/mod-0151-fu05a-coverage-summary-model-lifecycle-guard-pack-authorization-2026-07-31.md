# MOD-0151 FU05A — CoverageSummary Model Lifecycle Guard — Pack Authorization

- **Tarih:** 2026-07-31
- **Modül:** MOD-0151 — Territory Management (`Diten.CrmService`)
- **Task tipi:** Module pack authorization / governance hizalama (kod değil, runtime değil)
- **Target file:** `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md`
- **Owner:** module-pack-author
- **Verdict:** **PASS**

---

## 1. Preflight

- Task, MOD-0151 module pack içinde **FU05A** kapsamını yetkilendirme ve governance hizalama task'ıdır.
  Kod yazma / runtime implementation task'ı **değildir**.
- Amaç: FU05 Live Smoke Closeout sırasında bulunan **CoverageSummary model lifecycle guard** boşluğunu kapatacak
  FU05A kapsamını additive olarak yetkilendirmek.
- Bu task **workflow / approval / ChangeRequest değildir**. FU06 / FU06A / FU06B controlled-activation işleri
  bilinçli olarak en sona bırakılmıştır.
- Otorite sırası korundu: Blueprint Excel > Module Pack > Domain Config > AGENTS.md > `.antigravity/rules/`.

## 2. FU05 Live Smoke Dependency Confirmation

- **FU05 Account Assignment Apply + History Live Smoke Closeout PASS** doğrulandı:
  `docs/audits/mod-0151-fu05-account-assignment-apply-history-live-smoke-closeout-2026-07-31.md` — **90 kontrol / 90 PASS / 0 FAIL**.
- Canlıda doğrulanan FU05 zinciri: Preview → Apply → History → CoverageSummary → Duplicate 409 → Override → End (hepsi PASS).
- Aynı raporda CoverageSummary model-lifecycle guard eksikliği açık **follow-up** olarak kaydedilmişti.
- Bağımlılık koşulu (FU05 chain PASS) **karşılandı**; FU05A yetkilendirmesinin önü açıktır.

## 3. CoverageSummary Lifecycle Gap Summary

- FU05 current coverage doğru çalışıyor; ancak CoverageSummary bağlı **territory model'in lifecycle status'unu**
  current projeksiyonda uygulamıyordu.
- Risk: deactivated / inactive / archived / superseded bir modele bağlı `AccountTerritoryAssignment`,
  CoverageSummary veya current coverage query içinde hâlâ **current** gibi dönebiliyordu.
- Etkilenen tüketiciler: "bu account şu an hangi territory'de?", "bu account'tan hangi MR sorumlu?",
  contact-derived territory coverage, FU09 Visit/Route Readiness API, MOD-0155 Visit Planning ve MR'ın
  "benim account'larım / benim doktorlarım" listesi.

## 4. Pack Frontmatter Changes

- `runtime_code_scope` içine **additive** olarak eklendi: `FU05A-coverage-summary-model-lifecycle-guard`.
- Mevcut scope'lar **korundu**: FU01, FU02, FU02A, FU02B, FU03, FU04, FU04A, FU04B, FU05 — hiçbiri silinmedi.
- Header banner'a FU05A additive read-projection guard açıklaması eklendi.
- §22.1 authorization update bloğuna **FU05A authorization update (2026-07-31)** notu eklendi.
- Yeni bölüm **§22.2a FU05A — CoverageSummary Model Lifecycle Guard** eklendi (allowed scope + policy + contract
  flags + exclusions).
- §22 FU tablosuna **FU05A** satırı eklendi.
- §23 Risks/Follow-ups tablosuna **F19** eklendi.
- §24 Acceptance Criteria'ya FU05A onay maddesi eklendi.
- §25 Next Recommended Prompt'a FU05A implementation prompt'u eklendi.
- **Governance hizalama (çakışma giderme):** §17'de zaten prose olarak geçen `FU05A — Assignment RBAC Permission
  Catalog Alignment` follow-up etiketi, FU04A-RBAC deseniyle uyumlu olacak şekilde **`FU05-RBAC — Assignment RBAC
  Permission Catalog Alignment`** olarak yeniden adlandırıldı. Bu sadece bir **follow-up etiket** değişikliğidir;
  RBAC scope'u açmaz. `FU05A` etiketi artık yalnız CoverageSummary Model Lifecycle Guard'a aittir.

## 5. FU05A Authorized Scope

1. **CoverageSummary current guard.** Current CoverageSummary/coverage query yalnız operationally valid model
   üzerinden döner. Şartlar: model `active`; model effective-window `effectiveAt`'i kapsar; model archived/inactive/
   superseded değil; assignment active/open; assignment effective-window `effectiveAt`'i kapsar; assignment soft-delete
   değil; assignment ended değil; tenant claim'den; Account master mutate edilmez.
2. **Historical coverage ayrımı.** Ended/inactive/archived/superseded modele bağlı assignment'lar history'de görünür;
   current CoverageSummary'de görünmez.
3. **`effectiveAt` davranışı.** Geçmiş tarih → o tarihte active model+assignment; bugün → yalnız bugün active olan;
   deactivated/archived model bugün current görünmez.
4. **Deactivation/archive sonrası.** Current CoverageSummary o modele bağlı atamaları current göstermez; history
   silinmez; assignment hard delete edilmez; assignment status otomatik ended **yapılmaz** — current projeksiyon guard
   ile filtrelenir.
5. **Account master boundary.** Account'a `TerritoryId`/`ZoneId`/`MRId` yazılmaz; CoverageSummary ayrı read model kalır.
6. **Contact-derived coverage readiness.** Contact'a doğrudan TerritoryAssignment yapılmaz; coverage
   `AccountContactLink → Account → current AccountTerritoryAssignment/CoverageSummary` üzerinden türetilir; FU05A guard
   bu türetme için prerequisite'tir.

## 6. FU05A Explicit Exclusions

Workflow approval; controlled activation; ChangeRequest / Change Approval Trace; MOD-0023 integration; lifecycle
guard dışında apply davranışı değiştirmek; assignment rule/preview değiştirmek; resource assignment davranışı
değiştirmek; FU04A replacement/transfer değiştirmek; FU04B Plan vs Current değiştirmek; Account master mutasyonu;
Contact mutasyonu; `ContactTerritoryAssignment` eklemek; evidence pack; import/export; visit/route planning
implementation; Brand Scope; Product/Brand master; hard delete; Mongo hand-edit; RBAC seed/grant (ayrıca
yetkilendirilmedikçe); MOD-0048 publish (ayrıca yetkilendirilmedikçe); `crm.territory.delete`;
`crm.micro-zone.manage`.

## 7. Current Coverage Policy

- CoverageSummary current sayılması için territory model `active` olmalı → **Evet**.
- Archived/inactive/superseded modele bağlı assignment'lar current coverage döner mi → **Hayır**.
- Current = yalnız active model + active assignment + effective-window içi.

## 8. Historical Coverage Policy

- Ended/inactive/archived/superseded modele bağlı assignment'lar history query'lerinde görünmeye **devam eder**.
- History silinmez; assignment hard delete edilmez; assignment status otomatik değiştirilmez.
- History = geçmiş kayıtlar; Current CoverageSummary = yalnız active projeksiyon.

## 9. EffectiveAt Policy

- Geçmiş tarih sorgusunda o tarihte active/effective olan model ve assignment dikkate alınır.
- Bugün sorulduğunda yalnız bugün active olan model ve assignment dikkate alınır.
- Model sorulan tarihte active değilse current coverage dönmez.

## 10. Contact Derived Coverage Readiness Note

- Contact için doğrudan `TerritoryAssignment` yapılmaz kararı **korundu**.
- Contact coverage = `AccountContactLink → Account → current AccountTerritoryAssignment / CoverageSummary`.
- Bu nedenle CoverageSummary model lifecycle guard, contact-derived coverage için **prerequisite** kabul edildi ve
  FU09'dan önce kapatılması gerektiği kayda geçirildi.

## 11. Contract Flag Notes

- FU05A sonrası önerilen additive flag: `supportsCoverageSummaryModelLifecycleGuard: true`.
- Mevcut flag'ler korundu: `supportsAccountAssignmentApply`, `supportsAssignmentHistory`, `supportsCoverageSummary`,
  `supportsResourceAssignmentPlanVsCurrent`.
- `supportsWorkflowActivation = false` **korundu**; workflow readiness/approval flag'i **eklenmedi**.

## 12. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** |
| MOD-0023 code changed? | **No** |
| Workflow scope opened? | **No** |
| ChangeRequest scope opened? | **No** |
| RBAC seed/grant changed? | **No** |
| MOD-0048 publish changed? | **No** |
| FU05A scope added? | **Yes** |
| Existing FU scopes preserved? | **Yes** |
| Account master mutation opened? | **No** |
| Contact mutation opened? | **No** |
| ContactTerritoryAssignment opened? | **No** |
| Resource assignment behavior changed? | **No** |
| FU04A/FU04B behavior changed? | **No** |
| Evidence/import-export opened? | **No** |
| Visit/route implementation opened? | **No** |
| Brand/Product opened? | **No** |
| Hard delete allowed? | **No** |
| supportsWorkflowActivation remains false? | **Yes** |
| Contact derived territory decision preserved? | **Yes** |

## 13. Created / Updated Files

- **Updated:** `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md`
  - frontmatter `runtime_code_scope` (+FU05A additive)
  - header banner FU05A notu
  - §22.1 FU05A authorization update
  - **yeni** §22.2a FU05A — CoverageSummary Model Lifecycle Guard
  - §22 FU tablosu FU05A satırı
  - §23 F19 follow-up
  - §24 acceptance criteria FU05A maddesi
  - §25 Next Recommended Prompt
  - §17 RBAC follow-up etiketi `FU05A → FU05-RBAC` yeniden adlandırma (çakışma giderme)
- **Created:** `docs/audits/mod-0151-fu05a-coverage-summary-model-lifecycle-guard-pack-authorization-2026-07-31.md` (bu rapor)

## 14. Final Verdict

**PASS**

- FU05A scope additive olarak eklendi; mevcut FU scope'ları korundu.
- CoverageSummary model lifecycle guard policy netleşti.
- History / current ayrımı netleşti.
- EffectiveAt policy netleşti.
- Contact-derived coverage prerequisite kaydedildi.
- Workflow / approval kapsamı açılmadı; `supportsWorkflowActivation=false` korundu.
- Ek governance kazanımı: prose'daki `FU05A` isim çakışması `FU05-RBAC` relabel'ı ile giderildi.
- Implementation prompt'u hazırlanabilir.

## 15. Next Recommended Prompt

```
@orchestrator MOD-0151 FU05A — CoverageSummary Model Lifecycle Guard
```
