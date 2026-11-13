# WP-0181-DL6 — MOD-0181 Sayım: Gateway kapsama testi (DL-6, AC-45)

- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md` (r8; onay satırı approvals.tsv'de). Dilim: **DL-6 — Gateway**, yalnız kapsama testi (paket F-9).
- Sahip: integration-agent (Gateway testi). Talimatı veren: MVP-5 WMS CT, sahip Beste (2026-11-13).
- Kapsam: **yalnız test.** `ocelot.json` değişmez (Q-15 = A); servis, sözleşme ve Web değişmez.
- Git: MVP-1 hat dalı (`feature/sce/mod-0173-inventory-ledger-valuation`), ana checkout. CT etiketi `ct/line-ff-2026-11-13f` (r2, 2026-11-13): `git merge-base --is-ancestor ct/line-ff-2026-11-13f HEAD` doğruysa devam; değilse `git merge --ff-only ct/line-ff-2026-11-13f`; ff değilse `git merge --no-ff ct/line-ff-2026-11-13f` (yalnız CT kayıtları), çakışmada `--abort` ve dur. Aynı checkout'ta MOD-0180 DL-1 (Inventory), MOD-0178-FU01 DL-1 (Web) ve MOD-0177-FU01 (test) oturumları çalışıyor: stage yalnız açık yolla ve yalnız bu WP'nin dosyası; derleme / test / sabotaj ayrık worktree'de. Açık yol ile stage; yerel commit; push / PR yok.

## Yapılacak

1. **AC-45:** `gateway/Diten.ApiGateway.Tests/Mod0181CycleCountRouteTests.cs` (ayrı dosya). Test, MOD-0181'in **her** ucunu (CycleCounting
   controller'ları + DL-5 kanıt paketi uçları: `CountEvidenceExportsController` dahil) mevcut `/api/warehouse/{everything}` rotasıyla
   5067'ye (Inventory) yönlendirildiği haliyle doğrular.
   - Uç listesi **koddan** okunur (controller sınıflarının `[Route]` + eylem `[HttpX]` şablonları, `Controllers.CycleCounting` namespace'i),
     elle yazılmaz; liste WMS-COUNT-BUNDLE 1.1.0 (FROZEN) işlem listesiyle **eşit** olmalı (işlem sayısı eşitliği + her işlemin bir rotaya düşmesi).
   - Her uç için: upstream yol `/api/warehouse/...` → downstream `/api/warehouse/...`, host 5067, method korunur; kimlik doğrulama başlığı iletilir.
   - Emsal: `Mod0178WarehouseRouteTests.cs` (aynı tarama deseni) ve MOD-0180 paketindeki `Mod0180WaveRouteTests` tanımı.
2. **Mod0178WarehouseRouteTests'e dokunulmaz:** 0181 uçları o dosyanın sayısına zaten dahildir (70 → 73, XMC-001 Beste). r2 notu: aynı dalda
   MOD-0180 DL-1 bu sayıyı kendi uçları kadar artırabilir (X-14, integration-agent; 73 → 87 civarı) — bu beklenen bir değişikliktir, bu WP'yi
   durdurmaz. Bu WP yalnız 0181 uçlarının o sayının içinde olduğunu (her 0181 ucunun `/api/warehouse/{everything}` rotasına düştüğünü) ölçer.
3. **Sabotaj (Çekirdek 10):** en az iki — (a) `ocelot.json` kopyasında (test fikstüründe, gerçek dosyada değil) `/api/warehouse/{everything}`
   downstream portunu 5068 yap → yeni test kırmızı; (b) bir CycleCounting eylemini geçici olarak `/api/cyclecount/...` rotasına taşı → yeni test
   kırmızı (uç rotaya düşmüyor) ve WMS-COUNT-BUNDLE eşitliği kırmızı. Her ikisi bayt-eşit geri alınır.

## Kabul

- `Diten.ApiGateway.Tests` tam takım: yeni test yeşil; öncesine göre yeni kırmızı yok (taban aynı gün ölçülür, kırmızı adlarıyla).
- `git diff --stat`: yalnız yeni test dosyası (ve gerekirse test projesine eklenti). `ocelot.json` değişmedi.
- Rapor: uç sayısı ve listesi, test sonuçları (taban karşılaştırması), sabotaj sonuçları, commit SHA.
- Ortam: `Diten.AuthService.AccountKindAcceptanceHost.Tests` koşulmaz; 27017 kullanılmaz.
