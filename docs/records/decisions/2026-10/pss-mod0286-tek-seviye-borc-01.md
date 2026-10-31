---
record_id: PSS-MOD0286-TEK-SEVIYE-BORC-01
status: decided — sahip kararı `tek-seviye-borc-01` ve sahip tablosu için "Uygun" (2026-10-31, PSS-UI CT sohbetinde sahibin kendi sözüyle)
prepared_at_local: 2026-10-31
prepared_by: PSS-UI Control Tower (Claude Code oturumu 05ce4e4b)
block: MOD-0286 UI Design System & Page Pattern Governance
pack: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md (rev 15, 0c53533e…, main'de onaylı, PR #312, merge 819cc642a)
queue: pss-ui
work_package: PSS-UI-P1-05
---

# MOD-0286 DL-3 — tek seviye sayfaların borç satırları kararı

## Neden bir karar

Paket rev 15, AC-21 (a): tek seviye sayfaların bugünkü kırmızıları (`FU-16`), okuyucu ve eşleme değişikliğiyle aynı PR'da
`tests/ui-compliance-baseline.tsv`'ye yeni bir sahip kararıyla girer. Bu karar ilk tabanın kararı
(`pss-mod0286-taban-01`) değildir; o karar tek seviye sayfaları kapsamıyordu ve FU-16 satırlarının `reason` hücresinde
`taban-01` geçmemelidir (AC-21 (a) ölçümü). Sahiplerin belirlenmesi de paket gereği sahibindir (DL-3 bölüm 2).

Sahip önce `tek-seviye-taban-01` adını verdi (CT önerisi). Ad AC-21 (a) ölçümünü bozduğu için CT yeni ad istedi ve sahip
`tek-seviye-borc-01` dedi. Kararın içeriği değişmedi.

## Karar

1. **Satırlar = iş paketinin commit'indeki kırmızılar, ölçüldüğü gibi.** FU-16 satırları seçicinin `--draft-baseline`
   çıktısından alınır; hiçbir kırmızı elle çıkarılmaz ya da eklenmez. Mevcut taban satırları değişmez.
2. **`fu`:** `FU-16`.
3. **`reason`:** her satırda `sahip kararı tek-seviye-borc-01 (tek seviye sayfalar, 2026-10-31)`.
4. **`owner`:** aşağıdaki tablo. Tablo bir sahiplik etiketidir, kod sahipliği değiştirmez; yanlış bir etiket sonraki bir
   sahip kararıyla düzeltilir.
5. **Sonraki değişiklikler:** satır eklemek ya da `count` artırmak yeni bir sahip kararıyla (AC-19); satır silmek ya da
   `count` düşürmek seçici `kapandı - …` yazdığında.

| Tek seviye sayfa (`Views/{Module}`) | owner | Satır (2026-10-31) |
|---|---|---|
| EnterpriseStrategyBusinessPerformance | enterprise-strategy-business-performance | 62 |
| DemandIdeas, DeliveryExecutionManagement, PPM, DecompositionTreeBuilder | portfolio-delivery | 12 + 7 + 2 + 2 |
| ManagementGovernance, Meetings | management-governance | 12 + 5 |
| Account, ESignature, WorkCenterNext, Tasks, Home | platform-shared-services | 9 + 4 + 2 + 1 + 1 |
| Recall, Expiry, Quarantine, Inventory | supply-chain-execution | 5 + 5 + 4 + 1 |
| TimeEntry | human-capital-management | 0 |
| Profile, Platform | platform-shared-services | 0 |

Son iki satırda bugün borç yok; sahibe gösterilen tabloda yer almadılar. Sahipleri ilk tabanın tablosundan
(`pss-mod0286-taban-01`) alındı; satır oluşursa etiket için yeni karar gerekir.

Sahibe iki fark ayrıca söylendi: `Meetings` ilk tabloda platform-shared-services'teydi, toplantı paketi
(`execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md`) yönetim alanında olduğu için
burada management-governance; `Account` ilk tabloda commercial-suite'teydi (CRM hesabı anlamında, paket DL-3 bölüm 2 bu
sayfaya uygulanmamasını ister), Login, ForgotPassword, ResetPassword sayfaları için burada platform-shared-services.

## Ölçüm

Origin/main `763e1062a` (2026-10-31), seçiciden bağımsız betik (git ağacı + paketin desen bloğu): 134 satır; CI-1 81,
CI-2 4 / 12, CI-4 2 / 2, CI-6 35 / 73, CI-7 8 / 24, CI-8 1 / 1, CI-9 1, CI-10 2 / 4 (satır / eşleşme). Paket AC-21 ölçümüyle
aynı (2026-10-29: 134). AC-21 (b) sayıları da aynı: 19 klasör, 162 `.cshtml` (121 kısmi olmayan), 87 JS, 106 FU-18 dosyası.
