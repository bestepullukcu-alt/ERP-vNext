---
record_id: PSS-MOD0286-TABAN-01
status: decided — sahip kararı `taban-karar` (2026-10-27, PSS-UI CT sohbetinde sahibin kendi sözüyle)
prepared_at_local: 2026-10-27
prepared_by: PSS-UI Control Tower (Claude Code oturumu 05ce4e4b)
block: MOD-0286 UI Design System & Page Pattern Governance
pack: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md (rev 13, ce134e13…, main'de onaylı, PR #263)
queue: pss-ui
work_package: PSS-UI-P1-04
---

# MOD-0286 DL-3 — ilk UI uyum tabanı (`tests/ui-compliance-baseline.tsv`) kararı

## Neden bir karar

Paket DL-3 bölüm 2 (satır 422-426) ilk tabanın "bir kez, yerelde, seçicinin ayrı bir kipiyle bütün sayfalar üzerinde
taslaklanması, gözden geçirilmesi, sahiplerin doldurulması ve **sahip kararıyla** commit edilmesi"ni ister; AC-19 (satır 680)
her satırın bir FU ve bir sahip taşımasını, satır eklemenin ya da `count` artırmanın sahip kararını satırın `reason`
hücresine yazmayı ister. Bu kayıt o ilk kararın metnidir; iş paketi PSS-UI-P1-04 tabanı yalnız bu kararla commit eder.

## Karar (sahip `taban-karar`, 2026-10-27)

1. **İlk taban = bugünün kırmızıları, ölçüldüğü gibi.** Satırlar seçicinin `--draft-baseline` kipinin iş paketinin
   commit'indeki çıktısıdır (paket bölüm 2'nin anahtar ve sayım kuralı; satır kontrollerinde dosya başına eşleşme sayısı).
   Hiçbir kırmızı tabandan elle çıkarılmaz ya da eklenmez; iki `bilgi:` satırı paketin yazdığı metinle eklenir.
2. **`fu` sütunu (paket satır 418-420):** CI-3 `personalizationClient sends tenant header only for tenant users` satırları
   `FU-4`; iki `bilgi:` satırı `FU-12` ve `FU-15`; diğer her satır `FU-14` (eski sayfa UI borcu).
3. **`owner` sütunu:** FU-4 satırları ve iki `bilgi:` satırı `developer-enablement`; diğer satırlar sayfanın alanına
   (`Views/{Area}`) göre aşağıdaki tablodan. Tablo bir sahiplik etiketidir, kod sahipliği değiştirmez; yanlış bir etiket
   sonraki bir sahip kararıyla düzeltilir.
4. **`reason` sütunu:** her satırda `sahip kararı pss-mod0286-taban-01 (ilk taban, 2026-10-27)`; `bilgi:` satırlarında
   ayrıca paketin gerekçesi.
5. **Sonraki değişiklikler:** satır eklemek ya da `count` artırmak yeni bir sahip kararıyla (AC-19); satır silmek ya da
   `count` düşürmek seçici `kapandı - …` yazdığında.

| `Views/{Area}` | owner |
|---|---|
| CRM, Account | commercial-suite |
| Pharmacovigilance | pharmacovigilance |
| PPM, DeliveryExecutionManagement, DemandIdeas, DecompositionTreeBuilder | portfolio-delivery |
| HCM, HumanCapital, TalentEcosystem, TimeEntry, Organization | human-capital-management |
| Inventory, Expiry, Quarantine, Recall, Traceability, SupplyChain, Manufacturing | supply-chain-execution |
| MDM, MasterData, MasterDataManagement | master-data-management |
| Procurement | procurement |
| Governance, ManagementGovernance | management-governance |
| EnterpriseStrategyBusinessPerformance | enterprise-strategy-business-performance |
| DevEnablement | developer-enablement |
| Platform, Home, Profile, Tasks, WorkCenterNext, WorkingCalendar, Meetings, DocumentManagement, ESignature | platform-shared-services |
| tablo dışı yeni bir alan | `belirsiz — sahip belirleyecek` (satır yine yazılır, iş paketi raporunda listelenir) |

## Ölçüm

`Views/` altındaki alan klasörleri 2026-10-27'de `ls frontend/Diten.Web/Views` ile okundu (`Shared` hariç 38 alan klasörü;
`Archive` donuktur). Domain yapılandırmalarının hiçbiri alan → domain eşlemesi taşımıyor (`grep -o 'Views/[A-Za-z]*'
execution/domains/*/domain-config.md` yalnız `Archive`, `Shared`, `Pharmacovigilance`, `Ppm` verdi); tablo bu yüzden
alan adlarından CT önerisidir.
