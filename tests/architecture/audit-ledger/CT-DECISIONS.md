# Deftere satır EKLEYEN Control Tower kararları

Defter yalnız küçülür (kural AUD-001 §1). Bir listeye (`## Bilinen borç`, `## K2 borcu`, `## Yazan sorgular`) satır eklemek
Control Tower kararıdır ve **bu dosyaya yazılmadan** CI'dan geçmez: `scripts/run_phase1_gates.sh` taban dala göre eklenen her
`- Ad` satırını arar ve adı burada geçmeyeni reddeder. Karar, izin verdiği satırla aynı değişiklikte gözden geçirilir.

Aynı değişiklikte `AuditTrailStandardTests.PinnedCounts` içindeki sayı da yükseltilir; ikisi birlikte gerekir.

| tarih | servis | eklenen ad | karar / gerekçe | kararı veren |
|---|---|---|---|---|

Not: defterin ilk kez geldiği değişiklik (taban dalda `tests/architecture/audit-ledger/` yokken) bu denetimden muaftır —
o değişiklikte her satır tanım gereği yenidir. 2026-10-02'de eklenen `## K2 borcu` (200 satır) ve `## Yazan sorgular` (3 satır)
bölümleri o ilk gelişin parçasıdır (P-2026-10-02-03-FIX1, madde 2 ve 6).
