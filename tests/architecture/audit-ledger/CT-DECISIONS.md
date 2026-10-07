# Deftere satır EKLEYEN Control Tower kararları

Defter yalnız küçülür (kural AUD-001 §1). Bir listeye (`## Bilinen borç`, `## K2 borcu`, `## Yazan sorgular`) satır eklemek
Control Tower kararıdır ve **bu dosyaya yazılmadan** CI'dan geçmez: `scripts/run_phase1_gates.sh` taban dala göre eklenen her
`- Ad` satırını arar ve adı burada geçmeyeni reddeder. Karar, izin verdiği satırla aynı değişiklikte gözden geçirilir.

Aynı değişiklikte `AuditTrailStandardTests.PinnedCounts` içindeki sayı da yükseltilir; ikisi birlikte gerekir.

| tarih | servis | eklenen ad | karar / gerekçe | kararı veren |
|---|---|---|---|---|
| 2026-10-07 | Diten.MdmService | `## İzler` mdm-guvenilir-kaynak-niyeti (b, yazıcı) | MDM ürün komutlarının denetim niyeti durum değişikliğiyle aynı yazımda / işlemde kalıcılaşır ve Platform'un güvenilir kaynak ucuna iletilir (merkezi günlük Platform) → yol b. `AcceptedTrails` +1. Ölçüm: 19 komutun hiçbiri izsiz yazmıyor (WP-MDM-SETTLE-CAP-01 madde 3) | Control Tower |
| 2026-10-07 | Diten.MdmService | `## Dolaylı` 11 satır (Start…Workflow ×8, Withdraw…Approval ×3) | Komut işleyicisi akışa özgü işlemciyi çağırır; işlemci izin yazma üyesini aynı yazımda çağırır. `PinnedCounts.Indirect` 8 → 19. Kimlik başlangıçlarında niyet Platform başlangıcından sonra yazılır, kurtarma tamamlar (🟡, defter notu) | Control Tower |
| 2026-10-07 | Diten.MdmService | `## İstisnalar` RetireGskuIdentityPairCommand, RetireLskuIdentityCommand (İ5) | Her çağrı 403 `DirectRetirementIsSystemOnly` döner, hiçbir kalıcı kaydı değiştirmez; emeklilik onaylı istek akışından geçer. Kodda silme yok (API yüzeyi kararı). `PinnedCounts.Exceptions` 0 → 2. Ölçüm sınırı: İ5 komutuna iz dışı bir yazım eklenirse kural yakalamaz → BL-575 | Control Tower |

Not: defterin ilk kez geldiği değişiklik (taban dalda `tests/architecture/audit-ledger/` yokken) bu denetimden muaftır —
o değişiklikte her satır tanım gereği yenidir. 2026-10-02'de eklenen `## K2 borcu` (200 satır) ve `## Yazan sorgular` (3 satır)
bölümleri o ilk gelişin parçasıdır (P-2026-10-02-03-FIX1, madde 2 ve 6).
