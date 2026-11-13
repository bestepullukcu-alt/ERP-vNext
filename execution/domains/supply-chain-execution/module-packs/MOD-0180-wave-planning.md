---
id: MOD-0180
name: Wave Planning
domain: supply-chain-execution
service: Diten.InventoryService
shell: tenant
golden_reference: slim
data_mode: server
screens:
  - module: Waves
    data_mode: server
  - module: WaveExceptions
    data_mode: server
entity_base: EntityBase
status: draft
approval_status: pending
revision: "r10 (2026-11-13) — r9 bağımsız kontrolü UYGUN (`docs/records/reviews/MOD-0180/2026-11-13-21e14601.md`); CT kodda doğruladı: MOD-0173 `InventoryLedgerManifestCompletenessTests.Every_enforced_permission_is_reachable_from_the_manifest` InventoryService derlemesindeki bütün controller'ları tarar ve yalnız adlı namespace'leri dışlar — `Controllers.WavePlanning` dışlanmazsa `warehouse.waves.*` anahtarları INVENTORY-LEDGER manifestinde aranır ve DL-1 testi kırmızıya çevirir; r9 bunu anmıyordu. Kararlar 03 eki X-17 (sahip Beste 2026-11-13, 'evet', commit `ad1100158`) ile XMC-001 X-17 satırı oldu: DL-1 dışlama listesine `.Controllers.WavePlanning` satırını yorumuyla, ilk 0180 controller'ıyla aynı commit'te ekler (MOD-0181 emsali `446cb58db`); §5, §5.1 X-17, §6, AC-5 INVENTORY-LEDGER bütünlük vakası + yeni S-5e, §17, §18, §19.1, §24 DL-1 satırı ve kapanış cümlesi (istisnalar X-14 ve X-17), kapanış tablosu §19.13. Sahibe açık soru yok. Mekanizma, yazma komutu (12) ve AC (56) değişmedi. r9 (2026-11-13) — bağımsız inceleme r8 (`docs/records/reviews/MOD-0180/2026-11-13-e2666ea4.md`, KOŞULLU, F-1 orta, F-2 düşük) ve kararlar 03 eki X-14 kapsam teyidi (sahip Beste 2026-11-13, 'evet', commit `6235794ab`) işlendi; F-1, F-2 kapandı (kapanış tablosu §19.12); sahibe açık soru yok. DL-6 DL-5 kabulünden sonraya sabitlendi — AC-55'in klasör / WMS-WAVE-BUNDLE taslak eşitliği ancak bütün 0180 uçları teslim edilince sağlanır (§15, §23, §24 sırası, AC-55, §17) (F-1); X-14 onay atıfları `c7dc4ea6a` + kapsam teyidi `6235794ab` (uç ekleyen her dilim, ayrı sahip onayı gerekmez), dilim kabul kayıtları satır numarasına değil `Assert.Equal(<n>, endpoints.Count)` satırına atıf yapar (§5.1 X-14, §6, §15, AC-55, §18, §24) (F-2). Yalnız metin; mekanizma, yazma komutu (12), AC (56) ve S adları değişmedi. r8 (2026-11-13) — bağımsız inceleme r7 (`docs/records/reviews/MOD-0180/2026-11-13-f357b120.md`, KOŞULLU, F-1 orta, F-2…F-4 düşük) ve kararlar 03 eki X-14 (sahip onayı, commit `c7dc4ea6a`) işlendi; F-1…F-4 kapandı (kapanış tablosu §19.11); CT kararları, sahibe yeni soru açılmadı. X-14 XMC-001 satırı oldu: `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:97` uç sayısını uç ekleyen her dilim (DL-1, DL-2, DL-3, DL-5) dilim başında ölçülen sayı + kendi uç sayısıyla, uçlarıyla aynı commit'te günceller (integration-agent; MOD-0181 emsali); AC-55'e 0178 sayı vakası + S-55c, §15, §24 kapanış cümlesi ve S-5c (iv) sabit 73'ten arındı (F-1); şüpheliler takılı yeniden denemelerden sonra sıralanır, yeni AC-15 (p16e) + S-15bd, :1084-1085 cümlesi düzeltildi (F-2); S-5c (3)'ün geçici eylemi sabit şablonlu `HttpGet` (`wave-sabotage`), beklenen sonuç bu şablonla (F-3); tarama maliyeti doğru yazıldı — mekanizma değişmedi (F-4). Yazma komutu 12, AC 56 — değişmedi. r7 (2026-11-13) — bağımsız inceleme r6 (`docs/records/reviews/MOD-0180/2026-11-13-127005fa.md`, KOŞULLU, F-1 orta, F-2…F-5 düşük) işlendi; F-1…F-5 kapandı (kapanış tablosu §19.10); CT kararları, sahibe yeni soru açılmadı. Uygun depo kümesi her geçişte yeniden taranır, tur sırasında kapanan depo sıradaki geçişe katılır, yeni kapanan depo en geç bir geçiş (tur koşmuyorsa en geç 60 sn) bekler — sahibin teyidi (Q-23 = A) değişmeden geçerli (F-1); liste doluyken atlama tavanı depo başına tur başına 200, `stopReason` `ListFullSkipLimit`, imleçle kaldığı yerden, `Warning` parti başına özet (F-2); art arda bağlantı durması sayacı yalnız aynı turda önce başarılı 0178 çağrısı varsa artar, turu durduran sipariş şüpheli olup sonda denenir, eşikte liste doluysa atlanır ve sayaç kalır, kapsam cümlesi düzeltildi (F-3); S-5c dokunuşları (`PICKTASKS`, `PickTasksController` GET) ve beklenen ek kırmızılar adıyla, X-16 / §14 / AC-5 kapsam cümleleri daraltıldı (F-4); iki döngü ayrımına AC-15 (r) + S-15bb (DL-3) (F-5). AC-15'e (p16a)…(p16d), (p18), (p19), (r); yeni S-15as…S-15az, S-15ba, S-15bb, S-15bc. Yazma komutu 12, AC 56 — değişmedi. r6 (2026-11-13) — bağımsız inceleme r5 (`docs/records/reviews/MOD-0180/2026-11-13-0517db83.md`, KOŞULLU, F-1 orta, F-2…F-6 düşük) ve sahip cevapları (Beste 2026-11-13, kararlar 03 eki: Q-23 = A, X-16 bekçisi onaylı) işlendi; F-1…F-6 kapandı (kapanış tablosu §19.9). Yeni mekanizma yok, en dar düzeltmeler: 50 parti sınırı depo başına, depolar dönüşümlü, tur süresi en kötü ~12,5 dk, dalga uzlaştırması ayrı döngü, Q-23 = A (F-1); liste doluyken hatalı sipariş atlanır ve tur sürer, `StuckListFull` stopReason'dan kalktı, kurtarma yolu (F-2); aynı siparişte art arda 3 bağlantı durması → takılı liste (`RepeatedConnectionError`) + gösterge ve alarm (F-3); kalan pencere üyelikli (ii)–(iii), üyeliksiz (i)–(iii) (F-4); S-5c düzeltildi, yeni S-5d, AC-5 vakası sahip onaylı (F-5); (p10b) olay / kayıt, `RoundBudgetReached`, `Error` log'u girişte bir kez (F-6). AC-15'e (p16), (p17); yeni S-15am…S-15ar. Yazma komutu 12, AC 56 — değişmedi. r5 (2026-11-13) — bağımsız inceleme r4 (`docs/records/reviews/MOD-0180/2026-11-13-7e92bce7.md`, KOŞULLU, F-1 orta, F-2…F-6 düşük) işlendi; F-1…F-6 kapandı (kapanış tablosu §19.8). Başlıca değişiklikler (hepsi §8.19 kapatma turu): art arda hata sınırı turu durdursa da tetikleyen siparişler `PendingReleaseStuck[]`'a yazılır ve sonraki tur onları sıranın sonuna alır; parti başına yeni takılı sınırı (10) ve liste üst sınırı (1.000); cevapta `pendingRelease.stopReason` (F-1); takılı liste yazımı `Version`'ı artırmaz, `WaveManaged = false` koşullu ve işlem içinde delta olarak yazılır, yeniden açmada temizlenir, her tur başında bekleyen kümesiyle uzlaştırılır, `Code` kararlı kategori (`OutboundUnreadable` / `PlanInconsistent` / `Unexpected`) ve ilk hatanınkidir (F-2); bir turda her outbound en çok bir kez denenir, tur başına parti sınırı (50), takılı yeniden deneme bütçesi (50, en uzun süredir denenmeyen önce) (F-3); adım (ii) üyelik kapanışı ayar kaydına `WaveManaged = false` koşullu kapı yazımı yapar, (ii)–(iii) arasında kalan dar pencere ve kurtarma yolu yazıldı (F-4); S-15f beklenen kırmızısı ve ikinci bekçi (0178 tekil `Seq` index'i) (F-5); künye, (n3), (g), §24 DL-5, AC-27 (d) en kötü durum, metrik adları `inventory_` / `_total` (F-6). AC-15'e (n5), (n6), (p7)…(p15); S-15f ve S-15v yeniden yazıldı, S-15i daraldı; yeni S-15w…S-15z, S-15aa…S-15al. CT eki: X-16 yazıldı — MOD-0178 r29 `adfe1bd7d`, inceleme UYGUN (`docs/records/reviews/MOD-0178/2026-11-13-2e4036b6.md`), Ali'nin onay turu (r29 + bu paket) açık; r29 incelemesi F-1'in bekçisi AC-5 alt ad alanı vakası + S-5c (DL-1; CT kararı, sahip onayı bekliyor). Yazma komutu 12, AC 56 — değişmedi. r4 (2026-11-13) — bağımsız inceleme r3 (`docs/records/reviews/MOD-0180/2026-11-12-311bcfa3.md`, KOŞULLU, F-1…F-7) ve kararlar 03 eki (Q-17 = 24 Kasım 2026; Beste'nin 'hemen' teyidi: ilk tur istek içinde en çok 200 sipariş / 2.000 görev, kalan en geç bir bakım turu) işlendi; `.antigravity` kapısı commit `0aa802519` ile yazıldı (PKS-001 §4 `warehouse.waves.*` satırı + :92 adlı alt ad alanı istisnası, RCS-001 `WAV`, X-12; sahip onayı Beste 2026-11-12); F-1…F-7 kapandı (kapanış tablosu §19.7). Başlıca değişiklikler: Taslak üyelik kapanışı görev açılışından ÖNCE yazılır ve `AddWaveOrders` / taşıma / serbest bırakma R1'i ayar kaydına `Version` koşullu yazımla C1'e karşı sıralanır — yetim üyelik doğmaz (r5 düzeltmesi: adım (ii) ile (iii) arasındaki dar pencere dışında — F-4, §8.19), kapalı depoda dalga serbest bırakılamaz (F-1, §8.19); siparişe özgü hata siparişi atlar, bağlantı hatası turu durdurur, takılan siparişler GET ayar cevabında, olay ve merkezi kayıt yalnız bir şey değiştiğinde (F-3); bakım turu kalanı partilerle tamamen açar; ilk tur bütçesi 200 sipariş / 2.000 görev (F-6, AC-27 d); `IssueImmediateAsync` `NotDeferred` ve `Releasing` üyelerinin sayımı (F-5); S-15b / S-15e / S-15f / S-15g düzeltildi, S-15i…S-15v eklendi (F-2; r5 düzeltmesi — r4 künyesi `S-15t` yazıyordu, F-6 a); `WmsPickTaskDto` ve CSV sırası (F-4); §24 DL-1 / DL-2 sabotajları adıyla (F-7). Yazma komutu 12, AC 56 — değişmedi. r3 (2026-11-12) — bağımsız inceleme r2 (`docs/records/reviews/MOD-0180/2026-11-12-05180e86.md`, KOŞULLU, F-1…F-9) ve kararlar 03 eki (Y-1 = A: dalga yönetimi kapatılırken bekleyen siparişler reddedilmez, iptal edilmez; görevleri dalga yokmuş gibi hemen açılır) işlendi; F-1…F-9 kapandı (kapanış tablosu §19.6). Başlıca değişiklikler: r2'nin 409 `WAVE_SETTINGS_DEFERRED_ORDERS` kuralı ve hata kodu kaldırıldı, yerine kapatma akışı §8.19 (ayar işlemi + aynı istekte ilk tur + tekrar edilebilir bakım turu `ReleasePendingOrdersCommand`; 0178 portuna `IssueImmediateAsync`; yazma komutu 11 → 12; AC-15 yeniden yazıldı); X-7 / X-8 dosya listesi HEAD `33bb4343a`'da ölçülüp tamamlandı (`orderBy=waveSequence`); PKS-001 §4 :92 istisna cümlesi önerisi; X-16'nın MOD-0178 r29'u (bağımsız inceleme + Ali onayı, r28 ile aynı onay turu); iptal edilen serbest bırakma denemesinin `Excluded` üyelikleri `Removed` (F-7); S-39 / AC-54 Layout bölünmesi (F-3); AC-55 önek süzgeci (F-8); `PageHelpCatalog.cs` (F-9); `recall` ailesi `secondary` (F-6). r2 (2026-11-12) — bağımsız inceleme r1 (`docs/records/reviews/MOD-0180/2026-11-12-c7c774c6.md`, KOŞULLU, F-1…F-16) ve sahip kararları 03 (Y-1…Y-4, Q-6 = B, diğer Q'lar öneri gibi, Q-17 sahipten, Q-20 Ali) işlendi; F-1…F-16 kapandı (kapanış tablosu §19.5). Başlıca değişiklikler: depoda ertelenmiş sipariş varken dalga yönetimini kapatma 409 (Y-1, AC-15); Excluded / OrderCancelled → PartiallyCompleted (D-9 karar 03 Y-2 ile genişledi); ayrı ayar izni warehouse.waves.settings.configure (Tier-3, Q-6 = B); WMS-BUNDLE eklemeli sürümü (OutboundEventKind dahil) DL-2 başlamadan önce CT adımı (X-8); Governance Rol İzinleri köprüsü X-15; .antigravity satırları paket onayından önce (Y-4); atıflar dc010b911'e sabitlendi. r1: ilk taslak (PA-MOD0180-01; girdiler BRIEF-MOD-0180, kararlar 01 / 02, mockup v2). Onaylayan Ali (GMG-ERP) — D-17, karar 02; yazan ≠ onaylayan. Bu sürümde onay satırı yoktur."
status_note: "draft r10 — kod izni yok. r10: XMC-001 X-17 eklendi (MOD-0173 InventoryLedgerManifestCompletenessTests dışlama listesine .Controllers.WavePlanning satırı yorumuyla, DL-1, ilk 0180 controller'ıyla aynı commit; sahip onayı ad1100158; MOD-0181 emsali 446cb58db; AC-5 INVENTORY-LEDGER bütünlük vakası + S-5e; §19.13). Sahibe açık soru yok. Kalan: r10 kısa bağımsız kontrolü + Ali'nin onay turu + onay satırı (CT-APR-01). r9 bağımsız kontrolü (21e14601) UYGUN. r8 bağımsız incelemesi (e2666ea4, KOŞULLU) F-1, F-2 r9'da kapandı (§19.12; DL-6 DL-5 kabulünden sonra; X-14 onayı c7dc4ea6a + kapsam teyidi 6235794ab — uç ekleyen her dilim, kabul kaydı Assert.Equal satırına atıf). Sahibe açık soru yok. r7 bağımsız incelemesi (f357b120, KOŞULLU) F-1…F-4 r8'de kapandı (§19.11; CT kararları, sahibe yeni soru yok; X-14 Gateway uç sayısı XMC-001 satırı — sahip onayı c7dc4ea6a, uç ekleyen her dilim :97'yi kendi uç sayısıyla günceller). r6 bağımsız incelemesi (127005fa, KOŞULLU) F-1…F-5 r7'de kapanmıştı (§19.10). r5 incelemesi (0517db83) F-1…F-6 r6'da kapanmıştı (§19.9). Q-1…Q-19, Q-21, Q-22, Q-23 ve Y-1…Y-4 kapandı (Q-17 = 24 Kasım 2026, karar 03 eki; 'hemen' teyidi §8.19; Q-23 = A — 10.200'ü aşan fazla sonraki turda, 'hemen' sayılır, Beste 2026-11-13; r7: depo kümesi her geçişte yeniden taranır, teyit başka depo ve kiracılar için de geçerli). X-16 bekçisi (AC-5 alt ad alanı vakası, S-5c, S-5d, DL-1) sahip onaylı 2026-11-13 (karar 03 eki). `.antigravity` kapısı (1), (1b), (2), (3)-X-12 yazıldı — commit 0aa802519, sahip onayı Beste 2026-11-12. X-16 yazıldı — MOD-0178 r29 adfe1bd7d, inceleme UYGUN (2e4036b6). Onaydan önce kalan: r10 kısa bağımsız kontrolü, Ali'nin onay turu (MOD-0178 r28 + r29 + bu paket) ve onay satırı (CT-APR-01, Ali — D-17). Onayı bekletmeyen açıklar: Q-20 rol şablonu (Ali), CT-1 (DL-2 öncesi), X-15 sahibi (DL-1 öncesi). DL-2 öncesi CT adımı: WMS-BUNDLE eklemeli sürümü (X-8). WMS-WAVE-BUNDLE taslağı DL-1'in ilk adımı, dondurma ayrı CT adımı."
owner: supply-chain-execution / merkez (Beste) — MVP-5
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-12
target: 2026-11-24
form_field_count: 8
dependencies: [MOD-0178 (S2 outbound + tahsis + pick görevi; XMC-001 seam'leri §5), MOD-0173 (defter yalnız dolaylı — 0180 stok okumaz / yazmaz), MOD-0173-FU02 (LOCATION yolu ve bin kodu), MOD-0040-FU02 (RCS-001 kayıt kodu servisi; DL-8 gelene kadar ara üretici Q-4), MOD-0018 (RBAC; Governance Rol İzinleri köprüsü X-15), BL-501 (merkezi denetim), WMS-BUNDLE (MOD-0178 sözleşmesi; eklemeli üye + OutboundEventKind değerleri — X-8, DL-2 öncesi CT adımı), WAREHOUSE-OUTBOUND 1.0.0 (okuma), LOCATION v1.1 (okuma), WMS-WAVE-BUNDLE (taslak yok — bu paketin sözleşmesi), DEV-0000]
---

# MOD-0180 — Wave Planning (Dalga Planlama)

> **draft (r10, 2026-11-13).** `status: draft`, `approval_status: pending`. **Paket onaylanmadan geliştirme başlamaz:** kod yalnız
> `approved` / `ready-for-dev` künye + bağımsız inceleme kaydı (CT-APR-02) + onay satırı (CT-APR-01, **onaylayan Ali / GitHub `GMG-ERP`**,
> D-17) ile yazılır (AGENTS.md Çekirdek madde 7). Bu sürüm kod, commit, push, PR ya da onay izni değildir.
>
> **r10 revizyon notu (2026-11-13, çok kısa tur).** Girdiler: r9 bağımsız kontrolü `docs/records/reviews/MOD-0180/2026-11-13-21e14601.md`
> (UYGUN; hedef r9 LF sha256 `21e14601…`), CT'nin kod bulgusu ve kararlar 03 eki X-17 (sahip Beste 2026-11-13, "evet", commit `ad1100158`).
> **Bulgu:** MOD-0173'ün `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Api/InventoryLedgerManifestCompletenessTests.cs`
> `Every_enforced_permission_is_reachable_from_the_manifest` (yöntem `:209`, HEAD `ad1100158`) servis derlemesindeki **bütün** controller'ları
> tarar ve yalnız namespace'i `.Controllers.Traceability` / `.Quarantine` / `.Expiry` / `.Recall` / `.Warehouse` / `.CycleCounting` ile
> bitenleri dışlar (`:213-223`); 0180 controller'ları `Diten.InventoryService.Api.Controllers.WavePlanning`'dedir (§3) — dışlanmazsa
> `warehouse.waves.*` anahtarları INVENTORY-LEDGER manifestinde aranır ve DL-1 testi kırmızıya çevirir; r9 bunu anmıyordu. **Değişiklik:**
> §5.1'e **X-17** XMC-001 satırı (DL-1 dışlama listesine `.Controllers.WavePlanning` satırı yorumuyla, ilk 0180 controller'ıyla aynı commit'te;
> MOD-0181 emsali `446cb58db`, yorumu "XMC-001 Beste 2026-11-09"); §5 Repo Scope satırı; §6 korunan yol maddesi; AC-5'e INVENTORY-LEDGER
> bütünlük vakası + yeni **S-5e**; §17 Mimari; §18 XMC maddesi; §19.1; §24 DL-1 satırı ve kapanış cümlesi (istisnalar artık X-14 ve
> X-17); kapanış tablosu §19.13. Sahibe açık soru yok. Mekanizma, yazma komutu sayısı (12), AC sayısı (56, boşluksuz), form alanı sayısı (8)
> ve `golden_reference: slim` **değişmedi**; yeni S adı yalnız S-5e. Kalan: r10 kısa bağımsız kontrolü + Ali'nin onay turu + CT-APR-01.
>
> **r9 revizyon notu (2026-11-13, çok kısa tur).** Girdiler: bağımsız inceleme r8 `docs/records/reviews/MOD-0180/2026-11-13-e2666ea4.md`
> (KOŞULLU; 1 orta, 1 düşük bulgu; hedef r8 LF sha256 `e2666ea4…`) ve kararlar 03 ekinin X-14 kapsam teyidi (sahip Beste 2026-11-13, "evet",
> commit `6235794ab`). F-1 ve F-2 bu sürümde kapandı (§19.12 "r8 → r9 kapanış tablosu"); sahibe açık soru kalmadı. **(F-1, orta)** DL-6
> **DL-5 kabulünden sonraya** sabitlendi: AC-1 taslağı DL-1'de §3'ün 23 ucunu yazar, klasörde ise DL-1 / DL-2 / DL-3 sonrası 14 / 16 / 21 uç
> vardır; AC-55'in klasör / taslak eşitliği ancak DL-5 sonrası sağlanır. İncelemenin önerdiği en dar yol seçildi (taslağı dilim etiketiyle
> sınırlama yolu seçilmedi); §15, §17, §23 DL-6 satırı, §24 sıra cümlesi ve DL-6 satırı, AC-55. **(F-2, düşük)** X-14 onay atıfları
> `c7dc4ea6a` + kapsam teyidi `6235794ab` oldu (onay uç ekleyen her dilimi — DL-1, DL-2, DL-3, DL-5 — kapsar, dilim başına ayrı sahip onayı
> gerekmez); dilim kabul kayıtları güncellenen satıra satır numarasıyla değil `Assert.Equal(<n>, endpoints.Count)` satırıyla atıf yapar
> (`:97` yalnız bugünkü HEAD ölçümüdür; DL-1 yorum eklediğinde satır kayar). Yalnız metin değişti: mekanizma, yazma komutu sayısı (12), AC
> sayısı (56, boşluksuz), S adları, form alanı sayısı (8) ve `golden_reference: slim` **değişmedi**. Atıflar HEAD **`6235794ab`**'de geçerli
> (`git diff --stat 0b5062383 6235794ab -- services gateway docs/analysis frontend` boş; `Mod0178WarehouseRouteTests.cs:97` hâlâ
> `Assert.Equal(73, endpoints.Count)`).
>
> **r8 revizyon notu (2026-11-13, kısa tur).** Girdiler: bağımsız inceleme r7 `docs/records/reviews/MOD-0180/2026-11-13-f357b120.md`
> (KOŞULLU; 1 orta, 3 düşük bulgu; hedef r7 LF sha256 `f357b120…`) ve kararlar 03 ekinin X-14 maddesi (sahip onayı Beste 2026-11-13, "tamam",
> commit `c7dc4ea6a`). F-1…F-4'ün hepsi bu sürümde kapandı (§19.11 "r7 → r8 kapanış tablosu"); seçimler CT kararıdır, sahibe yeni soru
> açılmadı. **(F-1, orta)** X-14 bilgi satırından **XMC-001 satırına** çevrildi: `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:97`
> (`Assert.Equal(73, endpoints.Count)`, ölçüm HEAD `0b5062383`) 0180 uçlarını da sayar; uç ekleyen her dilim (DL-1, DL-2, DL-3, DL-5) bu satırı
> **dilim başında ölçülen sayı + dilimin eklediği 0180 uç sayısı** yapar, uçlarıyla aynı commit'te (integration-agent; MOD-0181 emsali) — arada
> kırmızı pencere kalmaz. AC-55'e 0178 sayı vakası ve S-55c; §5, §6, §7, §15, §17, §18, §24 ve S-5c (iv) sabit 73'ten arındı. **(F-2)** Şüpheliler
> artık takılı yeniden denemelerden **sonra** sıralanır (turun en sonu); yeni AC-15 (p16e) + S-15bd; "başka bekleyen yok" cümlesi düzeltildi.
> **(F-3)** S-5c (3)'ün geçici `GET` eylemi sabit şablon `wave-sabotage` taşır; beklenen kırmızılar bu rotayla yazıldı. **(F-4)** Bekleyen
> taramasının maliyeti doğru yazıldı (dışlama kümesi depo başına tur içinde ≤ 10.000 kimlik); mekanizma değişmedi. Yazma komutu sayısı (12), AC
> sayısı (56, boşluksuz), form alanı sayısı (8) ve `golden_reference: slim` **değişmedi**. Yeni test atıfları HEAD **`0b5062383`**'te ölçüldü
> (`git diff --stat 90f6a1da8 0b5062383 -- services gateway docs/analysis frontend` boş — r7 atıfları geçerli).
>
> **r7 revizyon notu (2026-11-13).** Girdi: bağımsız inceleme r6 `docs/records/reviews/MOD-0180/2026-11-13-127005fa.md` (KOŞULLU; 1 orta,
> 4 düşük bulgu; hedef r6 LF sha256 `127005fa…`). F-1…F-5'in hepsi bu sürümde kapandı (§19.10 "r6 → r7 kapanış tablosu"); seçimler CT
> kararıdır, sahibe yeni soru açılmadı. **Davranış değişiklikleri (hepsi §8.19 bakım turu):** (1) uygun depo kümesi turun başında bir kez
> değil **her geçişin başında** yeniden taranır; tur sırasında kapanan depo sıradaki geçişe katılır, turda işi biten depo yeni bekleyenle geri
> döner; yeni kapanan depo tur koşuyorsa en geç bir geçiş, koşmuyorsa en geç 60 sn bekler — sahibin "kalanı en geç bir bakım turu" teyidi
> başka depo ve kiracılar için de geçerli kalır (F-1); (2) liste doluyken atlanan siparişlere depo başına tur başına **200** tavanı (yeni
> `stopReason` `ListFullSkipLimit`), imleçle kaldığı yerden sürme ve parti başına özet `Warning` log'u (F-2); (3) art arda bağlantı
> durması sayacı yalnız aynı turda önce başarılı bir 0178 çağrısı varsa artar; turu bağlantı hatasıyla durduran sipariş şüpheli işaretlenip
> sonraki turlarda takılı olmayanların sonunda denenir; "ilk denenen" koşulu ve "bağlantı hatasız turda sıfırla" kuralı kalktı; eşikte liste
> doluysa sipariş atlanır, tavana sayılır, sayaç kalır; kapsam cümlesi düzeltildi (F-3). **Metin ve bekçi:** S-5c'nin dokunuşları ve beklenen ek
> kırmızıları adıyla, X-16 / §14 / AC-5 kapsam cümleleri (F-4); iki döngü ayrımına AC-15 (r) + S-15bb, DL-3 kabulünde (F-5). Yazma komutu
> sayısı (12), AC sayısı (56, boşluksuz), form alanı sayısı (8) ve `golden_reference: slim` **değişmedi**. Yeni test atıfları HEAD
> **`90f6a1da8`**'de ölçüldü.
>
> **r6 revizyon notu (2026-11-13).** Girdiler: bağımsız inceleme r5 `docs/records/reviews/MOD-0180/2026-11-13-0517db83.md` (KOŞULLU; 1 orta,
> 5 düşük bulgu; hedef r5 LF sha256 `0517db83…`) ve sahip cevapları (Beste 2026-11-13, sahip sohbet cevabı — kararlar 03 eki
> `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md`): **Q-23 = A** ("hemen" teyidinin depo başına 10.200 bekleyen
> sipariş sınırı kabul — fazlası sonraki turda açılır) ve **X-16 bekçisi onaylı** (AC-5 alt ad alanı vakası DL-1'de). F-1…F-6'nın hepsi bu
> sürümde kapandı (§19.9 "r5 → r6 kapanış tablosu"). **Yeni mekanizma eklenmedi; en dar düzeltmeler:** 50 parti sınırı **depo başına**,
> depolar dönüşümlü (her geçişte her depoya bir parti), tur süresi en kötü ~12,5 dk, dalga uzlaştırması ayrı döngü (F-1); liste doluyken hatalı
> sipariş atlanır ve tur sürer, kurtarma yolu yazıldı (F-2); aynı siparişte art arda 3 bağlantı durması → takılı liste + gösterge ve alarm
> (F-3); kalan pencere üyeliksiz siparişte (i)–(iii) (F-4); S-5c düzeltildi, yeni S-5d (F-5); (p10b), `RoundBudgetReached`, log seli (F-6).
> Yazma komutu sayısı (12), AC sayısı (56, boşluksuz), form alanı sayısı (8) ve `golden_reference: slim` **değişmedi**. Yeni test atıfları
> (`WarehouseRegistrationTests.cs:37`, `:39`, `:45`, `:56`, `:100`) HEAD **`d834454ad`**'de ölçüldü.
>
> **r5 revizyon notu (2026-11-13).** Girdi: bağımsız inceleme r4 `docs/records/reviews/MOD-0180/2026-11-13-7e92bce7.md` (KOŞULLU; 1 orta,
> 5 düşük bulgu; hedef r4 LF sha256 `7e92bce7…`). F-1…F-6'nın hepsi bu sürümde incelemenin önerileriyle kapandı; her bulgunun değişikliği ve
> bölümü §19.8 "r4 → r5 kapanış tablosu"ndadır. **Davranış değişiklikleri (hepsi §8.19 kapatma turu):** (1) art arda hata sınırı turu
> durdursa da tetikleyen siparişler `PendingReleaseStuck[]`'a yazılır, sonraki tur onları sıranın sonuna alır ve yeni siparişleri açar;
> yaygın arızada listeye parti başına en çok 10 yeni sipariş eklenir, liste en çok 1.000 sipariştir; kapatma cevabı `stopReason` taşır (F-1);
> (2) takılı liste yazımı `Version`'ı **artırmaz**, `WaveManaged = false` koşullu ve işlem içinde delta olarak yazılır, depo yeniden açılınca
> **temizlenir**, her tur başında bekleyen kümesiyle uzlaştırılır; `Code` kararlı kategori ve ilk hatanınkidir (F-2); (3) bir turda her outbound
> en çok bir kez denenir, tur başına en çok 50 parti, takılı yeniden deneme tur başına en çok 50 sipariş (en uzun süredir denenmeyen önce) (F-3);
> (4) adım (ii)'nin üyelik kapanışı ayar kaydına `WaveManaged = false` koşullu kapı yazımı yapar; (ii) ile (iii) arasında kalan dar pencere
> ve kurtarma yolu yazıldı — r4'ün "yetim üyelik hiç doğmaz" cümlesi yumuşatıldı (F-4). S-15f'in beklenen kırmızısı düzeltildi (F-5); metin ve
> ölçüm ayrıntıları (F-6). Yazma komutu sayısı (12), AC sayısı (56, boşluksuz), form alanı sayısı (8) ve `golden_reference: slim`
> **değişmedi**. Yeni kod atıfları (`WarehouseIndexConfiguration.cs:147-148`, `OutboundShipmentReads.cs:124`, `RefusalAuditMetrics.cs:13`)
> HEAD **`1aaa0db22`**'de ölçüldü. **CT eki (r5):** X-16 **yazıldı** — MOD-0178 r29 `adfe1bd7d`, bağımsız inceleme UYGUN
> (`docs/records/reviews/MOD-0178/2026-11-13-2e4036b6.md`; not MOD-0178 §14.1 `:2128-2152`); Ali'nin onay turu (r29 + bu paket) açık. r29
> incelemesinin F-1'i (X-16 yasağını ölçen bekçi yok) AC-5'e DL-1 vakası + S-5c olarak eklendi — **CT kararı, sahip onayı bekliyor** (§16 AC-5).
>
> **r4 revizyon notu (2026-11-13).** Girdiler: bağımsız inceleme r3 `docs/records/reviews/MOD-0180/2026-11-12-311bcfa3.md` (KOŞULLU; 3 orta,
> 4 düşük bulgu; hedef r3 LF sha256 `311bcfa3…`), kararlar 03 eki (`docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md`,
> commit `c30c47e90`: **Q-17 = 24 Kasım 2026**; **"hemen" teyidi** — kapatma isteğinde ilk tur en çok 200 sipariş / 2.000 görev, kalanı en geç
> bir bakım turu) ve `.antigravity` kapısı commit'i **`0aa802519`** (PKS-001 §4 `warehouse.waves.*` satırı `:106` + `:92` adlı alt ad alanı
> istisnası, RCS-001 §3 `WAV` `:72`, Platform `RecordCodeTypeCatalogTests` `WAV` `:30` — X-12; sahip onayı Beste 2026-11-12; sabotajla kırmızı
> doğrulandı). F-1…F-7'nin hepsi bu sürümde kapandı; her bulgunun değişikliği ve bölümü §19.7 "r3 → r4 kapanış tablosu"ndadır. **Davranış
> değişiklikleri (§8.19):** (1) kapatma turunda Taslak üyelik kapanışı görev açılışından **önce** yazılır; `AddWaveOrders`, "Sonraki dalgaya
> taşı"nın ekleme adımı ve serbest bırakmanın R1 işlemi ayar kaydına `Version` koşullu yazımla C1'e karşı sıralanır (F-1); (2) siparişe özgü
> hata siparişi atlatır, bağlantı hatası turu durdurur, takılan siparişler GET ayar cevabında görünür; olay ve merkezi kayıt yalnız bir şey
> değiştiğinde (F-3); (3) ilk tur bütçesi 200 sipariş **ve** 2.000 görev; bakım turu kalanı aynı turda partilerle açar (F-6, "hemen" teyidi).
> Yazma komutu sayısı (12), AC sayısı (56), form alanı sayısı (8) ve `golden_reference: slim` **değişmedi**. Yeni atıflar HEAD **`c30c47e90`**'da
> ölçüldü; `git diff --stat 33bb4343a c30c47e90 -- services gateway docs/analysis` yalnız X-12 test satırıdır (`RecordCodeTypeCatalogTests.cs`,
> +1), bu yüzden r2 / r3 kod atıfları geçerlidir.
>
> **r3 revizyon notu (2026-11-12).** Girdiler: bağımsız inceleme r2 `docs/records/reviews/MOD-0180/2026-11-12-05180e86.md` (KOŞULLU; 2 orta,
> 7 düşük bulgu; hedef r2 LF sha256 `05180e86…`) ve sahip kararları 03'ün **eki** (`docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md`
> "Ek — Y-1 netleştirmesi", commit `33bb4343a`; Beste "öneriler tamam"). F-1…F-9'un hepsi bu sürümde kapandı; her bulgunun değişikliği ve bölümü
> §19.6 "r2 → r3 kapanış tablosu"ndadır. **Davranış değişikliği (Y-1 = A):** dalga yönetimini kapatma artık **reddedilmez**; r2'nin 409
> `WAVE_SETTINGS_DEFERRED_ORDERS` kuralı ve hata kodu kaldırıldı; kapatma anında depodaki her bekleyen (`Allocated` + `Deferred`) siparişin
> toplama görevleri dalga yokmuş gibi açılır (MOD-0178 normal akışı, X-4 portunun yeni `IssueImmediateAsync` yöntemiyle); dalgadaki
> (`Released` / `Picking`) siparişler etkilenmez — §8.19, AC-15. Yeni sistem yazma komutu `ReleasePendingOrdersCommand` (11 → 12). Form alanı
> sayısı (8) ve `golden_reference: slim` **değişmedi**. Yeni kod atıfları (F-1, F-6, F-8, F-9) HEAD **`33bb4343a`**'da ölçüldü;
> `git diff --stat dc010b911 33bb4343a -- services gateway docs/analysis` boştur, bu yüzden r2'nin `dc010b911:` atıfları geçerlidir.
>
> **r2 revizyon notu (2026-11-12).** Girdiler: bağımsız inceleme r1 `docs/records/reviews/MOD-0180/2026-11-12-c7c774c6.md` (KOŞULLU; 6 orta,
> 10 düşük bulgu) ve sahip kararları 03 `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md` (Y-1…Y-4; Q-6 = B;
> Q-1…Q-5, Q-7…Q-16, Q-18, Q-19, Q-21, Q-22 öneri gibi; Q-17 sahipten; Q-20 Ali). F-1…F-16'nın hepsi bu sürümde kapandı; her bulgu ve
> kararın değişikliği ve bölümü §19.5 "r1 → r2 kapanış tablosu"ndadır. Davranış değişiklikleri: Y-1 (dalga yönetimini kapatma 409 —
> §4.5, §8.6, AC-15; **r3'te kaldırıldı**, yerine §8.19), Y-2 (D-9 genişlemesi — §8.3, §8.9, AC-30), Q-6 = B (ayar izni — §14, AC-15, AC-48), X-8 sırası (DL-2 öncesi), X-15
> (rol köprüsü — AC-56). Form alanı sayısı (8) ve `golden_reference: slim` **değişmedi** (Q-1 = A, karar 03).
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`.
>
> **Atıf kısaltmaları.** `INV/` = `services/Diten.InventoryService/src/Diten.InventoryService.`; `INVT/` =
> `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`. Kod atıfları r2'de **`dc010b911`** üzerinde yeniden ölçüldü ve
> `dc010b911:INV/...:satır` biçiminde sabitlendi (`dc010b911`, r1'in ölçtüğü MVP-1 kod hattı ucu `a3f329ece`'nin torunudur; aradaki fark yalnız
> MOD-0181 DL-5 kodu — 0180'in atıf yaptığı 0178 dosyaları değişmedi, `SingleSystemOfRecordArchitectureTests` ve `CentralAuditDeclarationTests`
> satırları kaydı, yeni satırlar yazıldı). **Öneksiz depo belgesi atıfları** (MOD-0178 **r28**, MOD-0181 **r8**, sözleşmeler, kurallar) de
> `dc010b911` ağacındaki konumlardır. **Çıkarım** ve **öneri** olarak işaretlenmeyen her cümle bir kaynağa dayanır; kaynaksız değer yazılmadı
> (bilinmeyen = Q-n ya da TBD).
>
> **Mockup bağı.** Ekran düzeni (pano, ayrıntı sekmeleri, planlayıcı, istisna kuyruğu, performans, durum örnekleri) **mockup v2'ye göre**dir
> (`C:\Users\Ali\Downloads\Dalga Planlama (standalone) v2.html`, depo dışı). Paket mockup'ı kopyalamaz; kuralı paket koyar, mockup ile paket
> çelişirse paket geçerlidir. Mockup v2'den okunan farklar §19.2'dedir.

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed) — r1'de koşuldu (HEAD `a3f329ece`, araç sha256 `ae87a566…`); r2'de `dc010b911`'de yeniden koşuldu
(araç aynı, `ae87a56615f5…`):**

| Komut | Sonuç |
|---|---|
| r2: `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --check-id MOD-0180 --name "Wave Planning"` | çıkış 0 — `OK  MOD-0180: proven against Blueprint/registry.` |
| r2: `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "Wave Planning"` | çıkış 3; `ÖZET bulgular: blueprint 1 · sor 0 · registry 1 · paket 2 · gateway 0 · görev 1`; rapor sha256 `ba84a0e299438c92c9c7af18d849970346ccc22e1138282d6ec016b93e45e208`, 605 satır. Paket 2 = r1'deki `MDM-059` + bu paketin kendisi (`MOD-0180-wave-planning.md`, r1 commit'i) — yeni aday yok |
| r1: `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "Wave Planning"` | çıkış 3 ("inceleme gerekli"); `ÖZET bulgular: blueprint 1 · sor 0 · registry 1 · paket 1 · gateway 0 · görev 1`; rapor sha256 `467aa3da2ec477e5f83b0637e761f434b9364a039c8d0c12f0fd67638bf79d14`; 562 ref incelendi |
| r1: `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "Dalga Planlama"` | çıkış 3; `ÖZET bulgular: blueprint 0 · sor 0 · registry 0 · paket 0 · gateway 0 · görev 1` (yalnız aynı CT kuyruğu satırı) |

**Keşif bulgularının her birine karar:**

| Bulgu | Karar |
|---|---|
| Blueprint 1 — `MOD-0180 'Wave Planning'`, veri nesnesi `waves` | **Bu paketin kaynağı.** Blueprint 8.8 `Blueprint_Data` satır 181 (aşağıda) |
| Registry 1 — `MOD-0180` reserved / planned | **Bu paketin kaynağı.** `execution/registries/module-id-registry.md:318` |
| Paket 1 — `execution/domains/master-data-management/module-packs/MDM-059-wave-planning.md` (8 eski dal: `origin/feature/module-catalog`, `origin/feature/dev-enablement`, …) | **Mükerrer değil — eski toplu taslak, yanlış domain, onaysız; devralınmadı.** İçeriği okundu (`git show refs/remotes/origin/feature/module-catalog:…`): `id: MDM-059`, `domain: master-data-management`, `status: draft`, `owner: ai-agent`, tek commit `8c3498bef` ("Initial commit", 2026-04-24), gövde yalnız şablon maddeleri. Aynı durum MOD-0178'de (`MDM-057`, `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md:119`) ve MOD-0181'de (`MDM-060`, `execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md:219`) aynı kararla kapandı. Okunmaz, yazılmaz |
| Görev 1 — `execution/domains/supply-chain-execution/work-packs/mvp5-wms/CT-QUEUE.tsv` `WMS-MVP5-MOD0180` [INSPECTION_REQUIRED] "pack yok" | **Bu paketin iş satırı** (CT kuyruğu); çakışma değil |
| Gateway 0 | Karar §15 — mevcut `/api/warehouse/{everything}` ailesi |

Çakışma ya da mükerrer **yoktur**; CT'ye durdurma yazısı gerekmedi.

**Blueprint 8.8 satır 181 (bu oturumda xlsx'ten okundu):** Module ID `MOD-0180` · Suite `Supply Chain Execution Suite` · Capability Group
`Warehouse (WMS)` · Aim "Optimize warehouse operations via standardized events/contracts and exception-centric execution." · Module Name
`Wave Planning` · Wave `W-4` · Dependency Gate `Putaway / Picking / Packing` · Outcome `Ops optimization` · Soft Pages `Wave Planner View;
Performance KPIs; Exception Queue` · Placement `Domain App (WMS)` · Min Integration Contract `WMS-WAVE-BUNDLE (wave objects, optimization
params, KPI bindings)` · SoR `waves, wave assignments, wave outcomes` · SoR Primary Object `waves` · Build/Buy `Buy/Partner` · SLO `Tier 2` ·
AI Tier `Assist` · AI Use Cases `Summarize; Extract; Recommend; Route` · AI Gate `Prompt Registry; HITL; Model Registry; Eval/Drift; Logging` ·
AI Risk `Medium`.

**Blueprint sapması (W-0180-01) — Build/Buy:** Blueprint `Buy/Partner` der. **Karar: Build** (Q-13 = A, karar 03). Gerekçe: dalga, MOD-0178'in tahsis planına
ve görev üretimine süreç içi bağlıdır (§8.6); dış bir ürün bu seam'le konuşamaz. Emsal: MOD-0178 ve MOD-0181 Blueprint'te `Buy/Partner`'dır,
sahip kararı Build'dir (`execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md:235-239`).

**Diğer kimlik kaynakları:** `execution/domains/supply-chain-execution/domain-config.md:27` (WMS listesi `MOD-0180 Wave Planning`); plan
`docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md:78` ("MOD-0180 Dalga Planlama (W-4)") ve `:124` ("… MOD-0181 sayım (0178 ile
paralel) → MOD-0180 dalga → MOD-0182 barkod"); MOD-0178 paketi (r28) `:171-172` ("MOD-0180 Dalga ve MOD-0182 Barkod sonra") ve `:202` ("Dalga planlama,
sayım | MOD-0180 / MOD-0181 | yok").

## 1. Module Summary

MOD-0180, **tek bir depoda** sevke hazırlanacak outbound siparişleri (MOD-0178 `OutboundShipment`) **toplama dalgalarına** gruplar ve
toplama görevlerini dalga dalga **serbest bırakır**. Dalga **tahsis yapmaz**: yalnız tahsisi bitmiş (`Allocated`) outbound'ları gruplar;
toplama görevini **MOD-0178 üretir** (dalga serbest bırakıldığında, 0178'in kendi kalıcı pick planından — §8.6). Blueprint SoR nesneleri:
**waves** (dalga kaydı), **wave assignments** (dalga–sipariş üyeliği ve görev sırası), **wave outcomes** (ilerleme, istisna, tamamlanma) —
Blueprint 8.8 satır 181.

**Kapsam kararları (bağlayıcı):** toplama dalgası, tek depo (sahip, BRIEF `:6`); kurma **elle** + **"Siparişleri öner"** düğmesi, periyodik
otomatik kurma yok (D-1); rota / sıra optimizasyonu **v1'de var** (D-2, §8.7); kesim saati deponun yerel saat diliminde (D-3, §8.4); toplama +
paketleme + sevk ilerlemesi görünür, MOD-0178'den **okunur** (D-5, §8.8).

**Temel sınır:** 0180 stok bakiyesi, rezervasyon, görev ya da outbound **yazmaz**. MOD-0178'in koleksiyonlarına (`wms_tasks`,
`wms_outbound_shipments`, …) tek yazıcı MOD-0178 kodudur; 0180 yalnız 0178'in **sahip olduğu** süreç içi port üzerinden "bu dalganın görevlerini
aç / kalanını geri çek" ister (§8.6, XMC-001 §5).

Hedef kullanıcı: **depo planlayıcısı** (dalgayı kurar, sipariş ekler / çıkarır, serbest bırakır), **vardiya sorumlusu** (izler, istisnaları
çözer, kalanı geri çeker), **toplayıcı** (görevleri MOD-0178 Toplama Görevleri ekranında yapar; bu modülde ekranı yok — BRIEF `:12-13`). Kiracı
modülüdür (`_LayoutTenantShell`, 7 dil).

## 2. Ownership and Boundaries

### 2.1 Kapsam
- `Wave` (dalga baş kaydı), `WaveOrder` (dalga–outbound üyeliği), `WaveEvent` (insert-only zaman çizelgesi), `WaveException` (istisna kuyruğu
  kaydı), `WaveWarehouseSettings` (depo başına dalga ayarı: dalga yönetimi anahtarı, saat dilimi, eşikler).
- Dalga kurma (elle + öneri), sınır uyarıları, serbest bırakma (rota sırası + 0178 görev üretimi isteği), geri çekme, iptal (yalnız Taslak),
  ilerleme okuması, durum uzlaştırması, istisna tespiti ve çözümü, performans KPI'ları + CSV.
- Ekranlar: Dalga panosu (+ ayrıntı), Planlayıcı, İstisna kuyruğu, Performans (Blueprint soft pages — §8.15).
- `WMS-WAVE-BUNDLE` sözleşmesi (taslak bu paketin DL-1 işi; bugün yok — `docs/analysis/contracts/drafts/` altında yalnız
  `bank-account-lookup-validation-v1.0-proposal.md` var, ölçüldü).

### 2.2 Kapsam dışı
| Şey | Sahibi | 0180 nasıl dokunur |
|---|---|---|
| Stok bakiyesi, hareket, rezervasyon, tahsis | MOD-0173 / MOD-0173-FU03 / MOD-0178 | **dokunmaz**; tahsisi 0178 outbound'la birlikte yapar (`execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md:1235-1236`) |
| Outbound kaydı, pick görevi, onay, koli, sevke hazır | MOD-0178 | outbound / görev / olay koleksiyonlarını süreç içi **yalnız `Find`** ile okur; görev üretimi ve geri çekme **0178'in portuyla** (§8.6) |
| Lokasyon ağacı, bin kodu | MOD-0173-FU02 | okur (yol + kod); düğüm yaratmaz |
| Sevkiyat, taşıyıcı, POD | MOD-0183 / MOD-0184 (MVP-6) | yok; taşıyıcı süzgeci v1'de yok (Q-1) |
| Satış siparişi / müşteri kimliği | O2C (SoR yok — MOD-0178 `OrderRef` opak) | yalnız outbound'un `ShipTo` ad / şehir / posta kodu / ülkesi ile süzer (Q-21) |
| Depolar arası dalga, yerine koyma (replenishment) dalgası | — | yok (tek depo, toplama dalgası) |
| Periyodik otomatik dalga kurma | — | yok (D-1); FU-0180-AUTO |
| Toplayıcı ataması | MOD-0178 (`warehouse.pick-tasks.update`) | yapmaz; yük sekmesinde toplayıcı başına görev sayısını **gösterir** |
| AI Assist | Blueprint AI kapıları | v1'de yok (Q-14) |

### 2.3 Sınır kuralları
1. **İkinci bakiye yok, ikinci görev kaydı yok.** 0180'in hiçbir varlığı `OnHand` / `Available` / `Reserved` alanı ya da görev miktarı
   toplamı tutmaz; ilerleme okuma anında 0178 verisinden hesaplanır, saklanmaz (D-5).
2. **0178 verisine yazılmaz.** 0180 üretim kodu `IOutboundShipmentRepository`, `IWarehouseTaskRepository` ya da `WarehouseCollections.*` üzerinde
   `Insert*` / `Update*` / `Replace*` / `Delete*` / `BulkWrite` çağırmaz; okumalar 0180 Persistence'ta `WarehouseCollections.*` sabitleriyle
   **yalnız `Find`**'dır (0181 emsali: `execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md:299-302`). Görev üretimi
   ve geri çekme 0178'in **kendi kodundadır** (port `IWavePickIssuance`, §8.6); 0180 onu çağırır (AC-16).
3. **Sipariş bölünmez (D-8).** Bir outbound aynı anda en çok **bir** etkin dalgadadır ve bütün satırları / tahsisleri / görevleriyle birlikte
   dalgadadır.
4. **0178 davranışı varsayılanda değişmez.** Dalga yönetimi kapalı (varsayılan) depoda 0178 bugünkü gibi çalışır; 0178 test dosyaları değişmez
   ve yeşil kalır (AC-18).
5. Her okuma ve yazma kiracı + şirket (`TenantId` + `LegalEntityId`, sunucuda çözülür) + depo (`WarehouseId`) kapsamındadır; kiracılar /
   şirketler arası erişim **404** (`.antigravity/rules/multi-tenancy.md`).
6. **Silme yok.** Dalga, üyelik, olay, istisna ve ayar kaydı silinmez; DELETE ucu yoktur; Taslak dalga yalnız **iptal** edilir (durum).

## 3. Owned Objects

| Nesne | Dilim | Koleksiyon (öneri) | Not |
|---|---|---|---|
| `Wave` | DL-1 | `wms_wave_waves` | Değişebilir baş kayıt, `Version` (If-Match, D-14); durum makinesi §8.3; serbest bırakmada kalıcı `PickPlan[]` (DL-2) |
| `WaveOrder` | DL-1 | `wms_wave_orders` | Dalga–outbound üyeliği; tekil kısmi index ile "sipariş tek etkin dalgada" (D-8) |
| `WaveEvent` | DL-1 | `wms_wave_events` | Insert-only zaman çizelgesi (olay geçmişi sekmesi); yerel GxP izi |
| `WaveException` | DL-3 | `wms_wave_exceptions` | İstisna kuyruğu kaydı (Açık / Çözüldü / Yok sayıldı); tespit anahtarıyla tekil |
| `WaveWarehouseSettings` | DL-1 | `wms_wave_settings` | Depo başına tek kayıt: `WaveManaged`, `TimeZoneId`, D-6 / D-11 eşikleri |
| Koleksiyon sabitleri | DL-1 | `WaveCollections` (0180 Persistence, `Persistence/WavePlanning/`) | MOD-0178'in `WarehouseCollections`'ıyla paylaşılmaz (0181 `CycleCountCollections` emsali, `dc010b911:INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs:88-89`) |

Koleksiyon öneki `wms_wave_*` önerisidir (Blueprint Placement "Domain App (WMS)"); kesin ad DL-1'in ilk ölçümüyle sabitlenir.

**Komutlar (yazma, 12):** `CreateWaveCommand`, `UpdateWaveCommand`, `AddWaveOrdersCommand`, `RemoveWaveOrdersCommand`, `CancelWaveCommand`,
`ReleaseWaveCommand`, `CompleteWaveReleaseCommand` (yalnız bakım servisi), `RecallWaveRemainingCommand`, `ResolveWaveExceptionCommand`,
`ReconcileWaveCommand` (yalnız uzlaştırma servisi), `UpdateWaveWarehouseSettingsCommand`, `ReleasePendingOrdersCommand` (yalnız bakım servisi;
r3 — dalga yönetimi kapalı depoda bekleyen siparişlerin görevlerini açan tekrar edilebilir tur, §8.19).

**Sorgular:** `GetWaveListQuery`, `GetWaveByIdQuery`, `GetWaveDashboardQuery`, `GetWaveOrderListQuery`, `GetWaveTaskListQuery`,
`GetWaveLoadQuery`, `GetWaveEventListQuery`, `GetWaveCandidateListQuery` (planlayıcı sol listesi), `GetWaveSuggestionListQuery` (D-1 "Öner"),
`GetWaveExceptionListQuery`, `GetWaveExceptionByIdQuery`, `GetWaveKpiSeriesQuery`, `ExportWaveKpiCsvQuery`, `GetWaveWarehouseSettingsQuery`.

**API (taban `/api/warehouse`, aile `/api/warehouse/waves…` — §15; yol adları sözleşme taslağında kesinleşir). PUT / DELETE yok, komut uçları:**
`GET/POST /waves` · `GET /waves/{waveId}` · `POST /waves/{waveId}/update` · `GET /waves/{waveId}/orders` · `POST /waves/{waveId}/orders` (ekle) ·
`POST /waves/{waveId}/orders/remove` · `GET /waves/{waveId}/suggestions` · `POST /waves/{waveId}/release` · `POST /waves/{waveId}/cancel` ·
`POST /waves/{waveId}/recall` · `GET /waves/{waveId}/tasks` · `GET /waves/{waveId}/load` · `GET /waves/{waveId}/events` · `GET /waves/dashboard` ·
`GET /waves/candidates` · `GET /waves/exceptions` · `GET /waves/exceptions/{exceptionId}` · `POST /waves/exceptions/{exceptionId}/resolve` ·
`GET /waves/kpis` · `GET /waves/kpis/export` (`text/csv`) · `GET /waves/settings/{warehouseId}` · `POST /waves/settings/{warehouseId}/update`.
Sabit kesimler (`dashboard`, `candidates`, `exceptions`, `kpis`, `settings`) parametreli yoldan önce eşleşir; `{waveId}` ve `{exceptionId}` `:guid`
kısıtlıdır (yeni, donmuş olmayan yollar — MOD-0178 §8.18'deki donmuş yol notu bu yollara uygulanmaz).

**Controller tabanı:** `WavesController`, `WaveExceptionsController`, `WaveKpisController` — üçü de `[Route("api/warehouse")]` taşır, eylem
şablonları tam yol parçasıdır (`[HttpPost("waves/{waveId:guid}/release")]` …); dosyalar **`INV/Api/Controllers/WavePlanning/`** altında ve
namespace `Diten.InventoryService.Api.Controllers.WavePlanning`'dir. **Neden ayrı namespace:** 0178'in kayıt testi
`Controllers.Warehouse` namespace'indeki bütün controller'ların zorladığı anahtarları 0178 manifestiyle eşit sayar
(`dc010b911:INVT/Warehouse/WarehouseRegistrationTests.cs:49-56`); 0180 controller'ı oraya konursa o test kırmızı olur (AC-5 sabotajı).

**Frontend rotaları:** `/Warehouse/Waves` (pano), `/Warehouse/Waves/Details/{waveId}` (ayrıntı, nav değil), `/Warehouse/WavePlanner`
(planlayıcı), `/Warehouse/WaveExceptions` (istisna kuyruğu), `/Warehouse/WaveKpis` (performans) — BRIEF `:22`, `:53`, `:59`, `:66`.

**İzinler:** §14.

## 4. Entity Fields

Tüm varlıklar InventoryService `EntityBase`'inden türer: `Id`, `TenantId`, `LegalEntityId`, `CreatedAt`, `UpdatedAt`, `Version` (int),
`IsDeleted`, `DeletedAt` (MOD-0178 §4 ölçümü, `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md:286-289`).
`IsDeleted` 0180'de hiç `true` olmaz (silme yok, §2.3 kural 6). `TenantId` / `LegalEntityId` sunucuda çözülür; gövde ya da sorguda gelirse 400.
Kullanıcı alanı `EntityBase`'de yoktur; gereken varlık kendi alanını taşır (`CreatedBy`). Zamanlar UTC saklanır; kesim saati ayrıca yerel
değer + saat dilimi ile saklanır (§8.4).

### 4.1 Wave
| Alan | Tip | Kural |
|---|---|---|
| `Number` | string | Görünen kod `{ŞİRKET}-WAV-{000001}` (RCS-001, OTOMATİK; §8.14); kiracı genelinde tekil `(TenantId, Number)`; değiştirilemez |
| `CompanyCodePrefix` | string `^[A-Z]{2,5}$` | numaraya yazılan şirket öneki (MOD-0178 M-2 deseni) |
| `WarehouseId`, `WarehouseCode` | uuid, string | LOCATION `Warehouse`, `Active`, şirket kapsamında; yaratmadan sonra **değişmez** (BRIEF `:36`) — **form alanı 1** |
| `WaveType` | enum | `Cargo` (Kargo çıkışı) \| `Customer` (Müşteri) \| `Region` (Bölge) \| `Manual` (Elle) — BRIEF `:28`; yalnız sınıflandırma ve süzgeç varsayılanı, iş kuralı değiştirmez — **form alanı 2** |
| `CutoffLocal` | yerel tarih-saat (saniyesiz) | deponun saat diliminde kesim saati (D-3) — **form alanı 3**; `CutoffAtUtc` ve `TimeZoneId` sunucuda yazılır (§8.4) |
| `CutoffAtUtc` | date-time | `CutoffLocal` + `TimeZoneId`'den; sıralama, risk ve KPI bununla |
| `TimeZoneId` | string (IANA) | yaratma anında depo ayarından kopyalanır (`WaveWarehouseSettings.TimeZoneId`); sonra değişmez |
| `ShipToFilter` | string ≤ 100 \| null | "Müşteri ya da teslim bölgesi" (mockup v2): outbound `ShipTo.name` / `city` / `postalCode` / `country` üzerinde büyük-küçük harf duyarsız içerir eşleşmesi (Q-21) — **form alanı 4** |
| `MaxOrders` | int 1..1000 \| null | dalga sipariş sınırı (D-7: aşım engellenmez, uyarı) — **form alanı 5** |
| `MaxLines` | int 1..20000 \| null | dalga satır sınırı (D-7) — **form alanı 6** |
| `ResponsibleUserId` | uuid \| null | sorumlu; kullanıcı dizini okuması (0178 `IWarehouseAssigneeDirectory` emsali; 0180 kendi portunu açar) — **form alanı 7** |
| `Note` | string ≤ 500 \| null | — **form alanı 8** |
| `Status` | enum | `Draft` \| `Releasing` (iç) \| `Released` \| `Picking` \| `Completed` \| `PartiallyCompleted` \| `Cancelled` (§8.3) |
| `PickPlan[]` | gömülü | serbest bırakmada bir kez yazılır, sonra değişmez: `{ Sequence, OutboundId, LineSeq, AllocSeq, LocationId, BinCode, ZoneKey, Aisle?, Rack?, Level?, Position?, RouteFallback }` (§8.7) |
| `RouteSummary` | gömülü \| null | `{ TaskCount, FallbackCount, ProxyDistance, ComputedAtUtc, Algorithm = "S-SHAPE-v1" }` |
| `ReleaseLeaseUntilUtc` | date-time \| null | yalnız `Releasing`'de; bakım kurtarma kilidi (§8.6) |
| `ReleaseRequest` | gömülü \| null | `{ IdempotencyKey, RequestFingerprint, ActorUserId, ConfirmLimitOverride }` — bakımın aynı isteği yeniden kurması için |
| `CreatedBy`, `ReleasedBy`, `ReleasedAt`, `CancelledBy`, `CancelledAt`, `CancelReason`, `CompletedAt` | — | sunucu yazar; `CompletedAt` §8.8 kuralıyla 0178 olay zamanından |
| `Recalls[]` | gömülü | her geri çekme: `{ By, At, Reason, WithdrawnOutboundIds[], StartedOutboundIds[] }` (D-9) |
| `IdempotencyKey`, `RequestFingerprint` | string | yaratma anahtarı (8–100); tekil `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)` (MOD-0178 §4.1 deseni) |
| `Version` | int | If-Match `W/"<version>"` (D-14) |

**form_field_count = 8** (yukarıdaki kalın sekiz alan) → `golden_reference: slim` (≤ 8; `.antigravity/rules/module-pack-standard.md` §2).
**Slim / Compact kararı (Q-1 = A, karar 03 — kesin; r2'de değişmedi):** mockup v2 yaratma panelinde **dokuz** alan gösterir: yukarıdaki sekizine ek **"Taşıyıcı / sevk
yöntemi"**. Bu alanın v1'de **veri kaynağı yoktur**: WAREHOUSE-OUTBOUND outbound'u taşıyıcı alanı taşımaz (`Address`
`docs/analysis/contracts/warehouse-outbound.openapi.yaml:76-85`; outbound alanları MOD-0178 §4.5 `:422-443` — taşıyıcı yok); taşıyıcı MVP-6'da
sevkiyata `ReadyToShip`'ten sonra bağlanır. Karar (Q-1 = A): alan v1 formunda yoktur → 8 alan → **slim** (mockup'ın yan paneli korunur);
taşıyıcı süzgeci FU-0180-CARRIER. (Reddedilen B: 9 alan → compact.)

### 4.2 WaveOrder
| Alan | Tip | Kural |
|---|---|---|
| `WaveId`, `WarehouseId` | uuid | baş kayıt; depo dalganınkiyle aynı olmalı |
| `OutboundId`, `OutboundNumber` | uuid, string | MOD-0178 outbound; numara gösterim kopyası |
| `LineCount`, `AllocationCount`, `PlannedTaskCount` | int | eklemede 0178 outbound'undan okunur (satır, tahsis, `PlannedBins` sayısı); sınır uyarıları için (§8.5); stok değil |
| `State` | enum | `Planned` (Taslakta) \| `Issued` (görevleri açıldı) \| `Withdrawn` (geri çekildi) \| `Excluded` (serbest bırakmada uygun çıkmadı) \| `Removed` (Taslaktan çıkarıldı) \| `OrderCancelled` (outbound 0178'de iptal) |
| `Active` | bool | `Planned` \| `Issued` iken `true`; diğerlerinde `false` — tekil kısmi index'in filtresi (D-8) |
| `IssuedTaskIds[]`, `IssuedTaskNumbers[]`, `IssuedAtUtc` | — | 0178 port sonucundan (§8.6); sonra değişmez |
| `AddedBy`, `AddedAt`, `AddedVia` | — | `AddedVia` = `Manual` \| `Suggestion` (D-1; olay geçmişinde "Siparişleri öner") |
| `ClosedBy`, `ClosedAt`, `CloseReason` | — | `Withdrawn` / `Removed` / `Excluded` / `OrderCancelled` geçişinde; sistem kapanışlarında `CloseReason` = `ReleaseAborted` (iptal edilen serbest bırakma denemesi, §8.6 adım 5) ya da `WaveManagementDisabled` (dalga yönetimi kapatıldı, §8.19), `ClosedBy` = `system` |
| `MovedToWaveId` | uuid \| null | "Sonraki dalgaya taşı"da hedef dalga (D-13) |
| `Version` | int | If-Match |

### 4.3 WaveEvent
Insert-only (`wms_wave_events`). Alanlar: `WaveId`, `Seq` (dalga içinde artan), `Kind` (`Created`, `Updated`, `OrdersAdded`, `OrdersRemoved`,
`Cancelled`, `ReleaseStarted`, `Released`, `ReleaseAborted`, `OrderExcluded`, `PickingStarted`, `RemainingRecalled`, `OrderWithdrawn`,
`OrderMoved`, `OrderCancelledUpstream`, `Completed`, `PartiallyCompleted`, `ExceptionRaised`, `ExceptionResolved`, `ExceptionIgnored`,
`ExceptionCleared`; r3 ayar akışı: `SettingsUpdated`, `PendingOrdersIssued`; r4 (F-3): `PendingOrdersStuckChanged` — takılan sipariş
listesine giren / çıkan siparişler, §8.19; r5 (F-2): çıkan siparişin nedeni `Issued` \| `NoLongerPending`; yeniden açmada (`false` → `true`)
temizlenen liste `SettingsUpdated`'in `Data`'sında `clearedStuckOrders[]`), `ActorUserId` \| `system`, `At`, `RequestOp`, `IdempotencyKey?`,
`RequestFingerprint?`, `Data` (önceki / sonraki değerler, gerekçe, sipariş ve görev numaraları). **Akış (r3):** `WaveId` alanı akış kimliğidir —
dalga olaylarında dalganın, depo ayarı olaylarında `WaveWarehouseSettings.Id`'nin kimliği; `Stream` = `Wave` \| `Settings` alanı ikisini ayırır
(r2 §8.13 ayar değişikliğinin gerekçesini "olaya" yazıyordu ama akışını tanımlamıyordu). İlgili iş yazımıyla **aynı Mongo işleminde** yazılır;
yazılamazsa iş yazımı geri alınır; silinmez (MOD-0178 §8.8 "Yerel GxP izi" deseni). Olay geçmişi sekmesi dalga akışından okunur (BRIEF `:51`).

### 4.4 WaveException
| Alan | Tip | Kural |
|---|---|---|
| `WaveId`, `WarehouseId` | uuid | — |
| `Kind` | enum | `ShortPick` \| `StockMissing` \| `LotUnderRecall` \| `CutoffMissed` \| `TaskWaitingLong` \| `OrderCancelledInWave` \| `OrderNotIssuable` (BRIEF `:61-62` + serbest bırakma kısmi hatası §8.6) |
| `SubjectKey` | string | tespit anahtarı: `{Kind}:{waveId}:{outboundId}[:{taskId}[:{seq}]]`; tekil `(TenantId, LegalEntityId, SubjectKey)` — aynı istisna iki kez açılmaz |
| `OutboundId`, `OutboundNumber`, `TaskId?`, `TaskNumber?`, `LineSeq?` | — | konu |
| `DetectedAt` | date-time | tespit eden uzlaştırma turunun zamanı |
| `Status` | enum | `Open` \| `Resolved` \| `Ignored` |
| `Resolution` | gömülü \| null | `{ Action: RemoveOrder \| MoveToNextWave \| Ignore \| SystemCleared, TargetWaveId?, By?, At, Reason? }` |
| `Version` | int | If-Match |

### 4.5 WaveWarehouseSettings
Depo başına tek kayıt (`wms_wave_settings`, tekil `(TenantId, LegalEntityId, WarehouseId)`): `WaveManaged` (bool, varsayılan `false` — §8.6,
Q-2), `TimeZoneId` (IANA, ör. `Europe/Istanbul`; `WaveManaged = true` için zorunlu — D-3, Q-3), `RiskMinutesBeforeCutoff` (varsayılan 90 —
D-6), `RiskProgressPercent` (varsayılan 70 — D-6), `PendingTaskMinutes` (varsayılan 30 — D-11), `Version`. Kayıt yoksa: `WaveManaged = false`,
eşikler varsayılan, saat dilimi yok (dalga yaratılamaz — 422 `WAVE_SETTINGS_REQUIRED`). **r4 sistem alanları (kullanıcı yazmaz, ayar ucunun
gövdesinde gelirse 400):** `GateSeq` (int — üyelik ve serbest bırakma yazımlarının C1'e karşı sıralama sayacı; `Version`'dan ayrıdır, artışı
kullanıcının ayar `If-Match`'ini bozmaz — §8.19 "Sıralama", F-1) ve `PendingReleaseStuck[]` (`{ OutboundId, OutboundNumber, Code,
FirstFailedAtUtc }` — kapalı depoda siparişe özgü hatayla takılan bekleyen siparişler; yalnız liste değiştiğinde yazılır — §8.19 "Kısmi
başarısızlık", F-3). **r5 — takılı liste yaşam döngüsü (F-2):** (a) yazımı `Version`'ı **artırmaz** (`GateSeq` gibi sistem alanıdır;
kullanıcının ayar `If-Match`'i liste değişiminde bozulmaz); (b) yazım **`WaveManaged = false` koşullu ve atomiktir:** partinin 0180 işlemi
ayar belgesini işlem içinde okur, partinin deltasını (giren, çıkan) bu son belgeye uygular ve filtre `WaveManaged = false` ile yazar;
eşzamanlı iki yazan (ilk tur ile bakım turu, iki bakım örneği) Mongo yazım çakışmasıyla sıralanır, kaybeden işlem geri çağrısı yeniden
çalışınca kendi deltasını kazananın listesine uygular — "son yazan kazanır" yoktur; eşleşme 0 (depo bu arada yeniden açıldı) → delta yazılmaz,
`PendingOrdersStuckChanged` olayı da yazılmaz; partide açılan sipariş ya da kapanan üyelik varsa (bunlar olmuştur) `PendingOrdersIssued`
olayı ve partinin merkezi kaydı yine yazılır, yoksa parti hiçbir şey yazmaz (olay 0, merkezi kayıt 0); tur `Reopened` ile durur (r6, F-6 a);
(c) **yeniden açmada temizlenir** (seçim): `false` → `true` ayar işlemi listeyi aynı işlemde boşaltır, temizlenen siparişler `SettingsUpdated`
olayının `Data.clearedStuckOrders[]`'ındadır ve depo göstergesi 0'a iner. Gerekçe: liste yalnız kapalı depodaki otomatik açılışın durumudur;
açık depoda bu siparişler planlayıcıda yeniden uygundur ve dalga serbest bırakmasında kendi istisna yolundan geçer (§8.6); "yalnız kapalı depoda
göster" seçeneği bayat listeyi saklar ve depo yeniden kapatıldığında eski `FirstFailedAtUtc` ile diriltir — reddedildi; (d) her tur başında
listedeki ama artık bekleyen olmayan siparişler (0178'de iptal, başka yoldan açılmış, `Deferred` değil) listeden çıkar (neden
`NoLongerPending`) — bakım turu bu yüzden depoda bekleyen sipariş **ya da** boş olmayan takılı liste varken koşar (§8.19 adım 4); (e) `Code`
istisna türünün adı değil **kararlı kategori**dir: `OutboundUnreadable` (outbound belgesi okunamadı / çözümlenemedi) \| `PlanInconsistent`
(kalıcı plan tutarsızlığı) \| `Unexpected` (eşleme tablosunda olmayan her tür) \| `RepeatedConnectionError` (r6, F-3: aynı siparişte art arda
bağlantı durması — §8.19 "Kısmi başarısızlık"); eşleme `PendingOrderReleaseEngine`'de tek tablodadır, istisna
türü adı yalnız log'a yazılır; `Code` siparişin **ilk** hatasının kategorisidir — sonraki turda farklı kategoriyle başarısız olması liste
değişikliği sayılmaz (yeni kategori log ve metriğe gider); (f) liste en çok `Warehouse:Wave:PendingReleaseStuckMax` (öneri **1.000**) siparişi
taşır (belge boyutu — öğe başına birkaç yüz bayt, çıkarım; §8.19 "Kısmi başarısızlık").

**Dalga yönetimini kapatma (Y-1 = A, karar 03 eki — r3):** `WaveManaged` `true` → `false` **reddedilmez**; bekleyen siparişler reddedilmez ve
iptal edilmez; kapatma anında her bekleyen siparişin toplama görevleri dalga yokmuş gibi açılır (MOD-0178 normal akışı) — akış, yarış, kısmi hata
ve denetim §8.19'dadır. r2'nin 409 `WAVE_SETTINGS_DEFERRED_ORDERS` kuralı ve hata kodu **kaldırıldı**. Diğer ayar alanlarının değişikliği ve
`false` → `true` her zaman serbesttir.

**Bekleyen sipariş (tek tanım, F-2 c):** aynı kiracı + şirket + `WarehouseId`'de MOD-0178 outbound'u `Status = Allocated` **ve**
`PickIssuance.State = Deferred`. Süzgeç iki koşulludur çünkü 0178 outbound iptali `PickIssuance`'a dokunmaz (X-1) — iptal edilmiş bir outbound
`Deferred` taşıyabilir ve sayılmamalıdır; `Allocating` outbound sayılmaz (henüz `Deferred` değildir; kapatmadan sonra politika `Immediate` döner,
§8.19 yarış kuralı). Okuma 0178 koleksiyonlarında **yalnız `Find`**'dır (§2.3 kural 2). **`deferredOrderCount`:** `GET /waves/settings/{warehouseId}`
cevabı bu tanımın sayısını taşır (Taslak dalgadaki `Planned` üyeler **ve `Releasing` dalganın henüz açılmamış üyeleri** dahil — onlar
port çağrısına kadar `Deferred`'dır); dalga yönetimli depoda bilgi, kapalı depoda "görevleri henüz açılmamış sipariş". **`releasingOrderCount`
(r4, F-5):** aynı cevapta, `deferredOrderCount`'un içinden etkin üyeliği `Releasing` dalgada olanların sayısı — onları kapatma turu değil serbest
bırakma / kurtarma (`CompleteWaveReleaseCommand`) dalgalı açar; bakım turunun açacağı = `deferredOrderCount − releasingOrderCount`. **`stuckOrders[]`
(r4, F-3):** `PendingReleaseStuck[]`'ın görünümü (`outboundId`, `outboundNumber`, `code`, `firstFailedAtUtc`; r5: `code` kategori, açık depoda
liste boştur). Pano KPI'ı "dalgaya girmemiş
sipariş" = bu tanım **eksi** etkin dalga üyeliği olanlar (`Releasing` üyeleri etkin üyeliklidir, sayılmaz). AC-15 (c), (f), (h), (l), (p).

### 4.6 Indexes
Kiracı + şirket önce (MOD-0178 §4.6 deseni):
- `wms_wave_waves`: tekil `(TenantId, Number)`; tekil kısmi `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)`;
  `(TenantId, LegalEntityId, WarehouseId, Status, CutoffAtUtc)` (pano, D-13 hedef arama); `(TenantId, Status, ReleaseLeaseUntilUtc)` (bakım).
- `wms_wave_orders`: **tekil kısmi** `(TenantId, LegalEntityId, OutboundId)` `PartialFilterExpression { Active: true }` (D-8);
  `(TenantId, LegalEntityId, WaveId, State)`.
- `wms_wave_events`: tekil `(TenantId, WaveId, Seq)`; istek anahtarı tekil kısmi `(TenantId, WaveId, ActorUserId, RequestOp, IdempotencyKey)`
  (MOD-0178 `wms_task_events` deseni, `:491-496`).
- `wms_wave_exceptions`: tekil `(TenantId, LegalEntityId, SubjectKey)`; `(TenantId, LegalEntityId, WarehouseId, Status, DetectedAt)`.
- `wms_wave_settings`: tekil `(TenantId, LegalEntityId, WarehouseId)`.
- MOD-0178 koleksiyonlarına 0180 **index eklemez** (okumalar mevcut index'lerle — §8.8; gerekirse XMC-001 ile sorulur).

## 5. Repo Scope

| Yol | Ne | Dilim |
|---|---|---|
| `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` (yazılacak) | WMS-WAVE-BUNDLE taslağı (contract-first; **DL-1'in ilk adımı**; dondurma ayrı CT adımı) | DL-1 |
| `INV/Domain/WavePlanning/{Entities,Enums,Rules,Repositories}/` | varlıklar, durum / sınır / risk / rota kuralları (saf) | DL-1…DL-3 |
| `INV/Application/Features/WavePlanning/` | ortak: `WaveContract.cs`, `WaveErrorCodes.cs`, `WaveCentralAudit.cs`, `Ports/`, `Services/` (rota, serbest bırakma motoru, uzlaştırma, istisna tespiti, KPI) | DL-1…DL-5 |
| `INV/Application/Features/Waves/` | Golden Slim: Commands, Queries, Handlers/CommandHandlers, Handlers/QueryHandlers, Validators, `WaveModels.cs` | DL-1…DL-3 |
| `INV/Application/Features/WaveExceptions/` | aynı yapı (çözüm komutu, liste / ayrıntı sorguları) | DL-3 |
| `INV/Application/Features/WaveKpis/` | aynı yapı (yalnız sorgular + CSV) | DL-5 |
| `INV/Persistence/WavePlanning/` | depolar, `WaveCollections`, index yapılandırması, 0178 okumaları (**yalnız `Find`**, `WarehouseCollections.*` sabitleriyle), `WaveMaintenanceHostedService` (yalnız `CompleteWaveReleaseCommand` / `ReconcileWaveCommand` / `ReleasePendingOrdersCommand`'ı (r3, §8.19; r4: depo başına parti parti, kalan bitene dek) `IMediator` ile gönderir; kendi işlemi yok; testten çağrılabilen tek tur yöntemi — 0181 emsali `PendingConfirmationReconcilerHostedService`) | DL-1…DL-3 |
| `INV/Api/Controllers/WavePlanning/` | `WavesController`, `WaveExceptionsController`, `WaveKpisController` — `[Route("api/warehouse")]` (§3) | DL-1…DL-5 |
| `INV/Api/WavePlanning/` | `WavePlanningManifestProvider`, `WavePlanningServiceCollectionExtensions` | DL-1 |
| `INV/Infrastructure/Authorization/WavePermissions.cs` | izin sabitleri | DL-1 |
| `INVT/WavePlanning/` | birim, entegrasyon, mimari testler (0180'in kendi test ağacı; 0178 davranış testleri **burada**) | her dilim |
| `frontend/Diten.Web/Views/Warehouse/{Waves,WavePlanner,WaveExceptions,WaveKpis}/`, `frontend/Diten.Web/Controllers/`, `frontend/Diten.Web/wwwroot/assets/js/Warehouse/…`, `frontend/Diten.Web/Resources/Views/Warehouse/…` | ekranlar, proxy controller'lar, JS, resx (7 dil) | DL-4, DL-5 |
| `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx`, `frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` | ortak Web kaynakları — **yalnız eklemeli anahtar**: nav (`Nav.Module.*`, `Nav.Page.*`), sayfa yardımı (`Help.*`, §25) ve Rol İzinleri satır adları (`Perm.Entity.*` — `SharedResource`'ta; 0181 `Perm.Entity.COUNTS` / `.VARIANCES` emsali, `SharedResource.en.resx:1317`, `:1320`; ekranın fiil köprüsü ayrı Governance dosyalarıdır — X-15); mevcut anahtar değişmez | `Perm.Entity.*` DL-1, DL-3, DL-5 (manifestin anahtar bildirdiği dilim); nav + yardım DL-4, DL-5 |
| `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs` (ortak Web dosyası; F-9, ölçüm `33bb4343a`) | sayfa yardımının `Related.*` bağlantıları (`Related` sözlüğü `:17-34`; 0181 satırları `:32-33`) ve adım ikonları (`StepIcons` `:36-108`; 0181 satırları `:102-107`) — §25'teki `Related.WAVEPLANNER` / `Related.WAVEEXCEPTIONS` ve adım ikonları için; **yalnız eklemeli satır** (`WAVES`, `WAVEDETAIL`, `WAVEPLANNER`, `WAVEEXCEPTIONS`, DL-5'te `WAVEKPIS`), mevcut satır değişmez (0181 emsali `e0bf26918` DL-4, `9f8696dd4` DL-5) | DL-4, DL-5 |
| `frontend/Diten.Web.Tests/Warehouse/WavePlanningL10nAndHelpTests.cs` (yeni) | 0180'in kendi L10n / Layout / yardım testi (0181 `CycleCountL10nAndHelpTests.cs:235` emsali — Layout satırı); 0178 testi değişmez | DL-4 |
| `gateway/Diten.ApiGateway.Tests/Mod0180WaveRouteTests.cs` (yeni, integration-agent) | uç kapsama testi — üretim `ocelot.json`'u (test projesine bağlı kopya) ve üretim controller kaynağını okur (§15); `ocelot.json` değişmez | DL-6 |
| `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — **yalnız `:97`** ve üstündeki uç yorumu (`:80-96`) (MOD-0178'in testi; XMC-001 X-14, integration-agent; r8) | yönlendirilen `api/warehouse` uç sayısı: dilim başında ölçülen sayı + dilimin eklediği 0180 uç sayısı, yorumda dilimin uçları adıyla; dilimin uçlarıyla aynı commit'te | DL-1, DL-2, DL-3, DL-5 |
| `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Api/InventoryLedgerManifestCompletenessTests.cs` — **yalnız** `Every_enforced_permission_is_reachable_from_the_manifest` dışlama listesine bir satır + yorumu (MOD-0173'ün testi; XMC-001 X-17; r10) | `.Where(t => t.Namespace?.EndsWith(".Controllers.WavePlanning", StringComparison.Ordinal) != true)` ve üstünde yorum — 0180 controller'ları WAVE-PLANNING manifestine aittir, kendi bütünlük testiyle ölçülür (AC-5); ilk 0180 controller'ıyla aynı commit'te | DL-1 |

### 5.1 XMC-001 — başka modülün dosyalarına dokunuşlar (her biri dokunmadan önce sahibine sorulur; tek tek)

| # | Dosya (ölçüm `dc010b911`) | Bugün | Gereken değişiklik (yalnız eklemeli) | Sahip | Dilim |
|---|---|---|---|---|---|
| X-1 | `INV/Domain/Warehouse/Entities/OutboundShipmentEntities.cs` | outbound'da görev üretim kipi yok | nullable `PickIssuance { State: Deferred \| Issued, WaveId?, WaveNumber?, IssuedAtUtc? }`; `null` = bugünkü davranış (görev tahsiste açılır); `Issued` + `WaveId = null` = dalga yönetimi kapatılınca dalgasız açıldı (r3, §8.19); **0178 outbound iptali `PickIssuance`'a dokunmaz** — 0180'in bekleyen sipariş tanımı bu yüzden `Status = Allocated` + `Deferred` iki koşulludur (F-2 c, §4.5) | MOD-0178 (MVP-1 CT, Beste) | DL-2 |
| X-2 | `INV/Domain/Warehouse/Entities/` altındaki `WarehouseTask` varlığı | dalga bağı yok | nullable `WaveRef { WaveId, WaveNumber, Sequence }` (yalnız `Kind = Pick`) | MOD-0178 | DL-2 |
| X-3 | `dc010b911:INV/Application/Features/OutboundShipments/Services/OutboundAllocationService.cs:172-185`, `:200-213`, `:227-233`, `:248`, `:288` | işlem B pick görevi numaralarını işlem dışında alır (`:172-185`), görevleri kurar (`:200-213`), outbound + olay + görevleri tek `InTransactionAsync`'te yazar (`:227-233`); görev kurucusu `PickTask` `private static` (`:248`), `Created` olay kurucusu `TaskCreated` `private` örnek yöntemi (`:288`) | (a) **kurucuların taşınması (F-16):** `PickTask` ve `TaskCreated` **davranış değişmeden** 0178 içinde ortak bir yardımcıya taşınır (öneri `INV/Application/Features/OutboundShipments/Services/OutboundPickTaskFactory.cs`, X-4 dosyaları arasında); `OutboundAllocationService` ve `WavePickIssuanceService` aynı yardımcıyı çağırır (AC-19'un "0178 `PickTask` kuruluşuyla aynı" şartı bu yüzden tek kaynaktan karşılanır); taşımanın davranış değiştirmediğinin kanıtı 0178'in mevcut S2 testlerinin değişmeden yeşil kalmasıdır (AC-18). (b) **erteleme dalı:** `IPickIssuancePolicy.ForWarehouse(...)` `Deferred` derse numara alınmaz, görev kurulmaz, outbound `Allocated` + `PickIssuance.State = Deferred` yazılır, `Allocated` olayının `pickTasks` verisi boş + `pickIssuance = Deferred`; `Immediate` (varsayılan) dalında kod yolu **değişmez** | MOD-0178 | DL-2 |
| X-4 | yeni 0178 dosyaları: `INV/Application/Features/Warehouse/Ports/IPickIssuancePolicy.cs` (+ varsayılan `ImmediatePickIssuancePolicy`), `INV/Application/Features/Warehouse/Ports/IWavePickIssuance.cs`, `INV/Application/Features/OutboundShipments/Services/WavePickIssuanceService.cs`, `INV/Application/Features/OutboundShipments/Services/OutboundPickTaskFactory.cs` (X-3 a) | yok | port + 0178 içi uygulama (§8.6): görev açma (dalgalı `IssueAsync`, r3 dalgasız `IssueImmediateAsync` — §8.19; r4: `IssueImmediateAsync` sonuç kümesinde `NotDeferred` da var, F-5) ve geri çekme **0178 kodunda**; **adlar (F-12, tek ad):** port `IPickIssuancePolicy` (0178), 0180'in uygulaması `WavePickIssuancePolicy` (0180 `Features/WavePlanning/Ports/` altında, depo ayarını okur); 0180 yalnız `WavePickIssuancePolicy`'yi `IPickIssuancePolicy` olarak kaydeder (DI, X-11) ve `IWavePickIssuance`'ı çağırır | MOD-0178 | DL-2 |
| X-5 | `dc010b911:INV/Domain/Warehouse/Enums/OutboundShipmentEnums.cs:22-39` | `OutboundEventKind` 15 değer | eklemeli `PickTasksIssued`, `PickTasksWithdrawn` | MOD-0178 | DL-2 |
| X-6 | `dc010b911:INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs:14-33` | `TypePrefixes` = PUT / PCK / OUT / PKG / CNT (`:28`); bilinmeyen önek reddedilir | **Q-4 = A (karar 03):** `Wave = "WAV"` sabiti + listeye `WAV` (MOD-0181 `CNT` emsali, `:26`) | MOD-0178 | DL-1 |
| X-7 | MOD-0178 Toplama Görevleri listesi (ölçüm `33bb4343a`, F-1): sorgu `GetPickTaskListQuery` (`INV/Application/Features/PickTasks/Queries/PickTaskQueries.cs:12`; `orderBy` beyaz listesi `:68` yalnız `createdAt` / `-createdAt`, başka değer 400); API `INV/Api/Controllers/Warehouse/PickTasksController.cs:33-35` (`orderBy` sorgu parametresi var, `waveId` yok); Web proxy `frontend/Diten.Web/Controllers/PickTasksController.cs:111` (sabit `orderBy=-createdAt`) ve `:118` (süzgeç beyaz listesi `warehouseId` / `assigneeUserId` / `outboundId` — listede olmayan parametre gateway'e geçmez); Web `Views/Warehouse/PickTasks/` (`_DataTable.cshtml`, `_Filter.cshtml`, `_IndexL10n.cshtml`), `wwwroot/assets/js/Warehouse/PickTasks/index.js`, `Resources/Views/Warehouse/PickTasks/PickTasksIndex.{en,tr,fr,es,zh,ar,ru}.resx`; liste sütununun verisi API'de `WarehouseTaskDto.wave` (X-8 üretim karşılığı), **Web'de `frontend/Diten.Web/Models/Warehouse/OutboundDtos.cs:169` `WmsPickTaskDto`** (r4, F-4; ölçüm `c30c47e90`) — Web liste ve QuickView satırı bu sınıftan `PickTasksController.cs:332` `Row` ile kurulur; `ListQuery` (`:109`) hem liste (`:54`) hem dışa aktarım (`:79`) tarafından kullanılır | dalga yok | **Q-12 = A (karar 03):** isteğe bağlı `waveId` süzgeci (API `[FromQuery]` + sorgu + Web `:118` beyaz listesine eklemeli ad); **`orderBy=waveSequence`** — 0178'in gerçek sıralama parametresi `orderBy`'dır, `sort` değil (r2 yazımı düzeltildi, F-1); `:68` beyaz listesine eklemeli değer, Web proxy yalnız `waveId` verildiğinde `orderBy=waveSequence` geçirir; liste sütunu "Dalga" (`WaveNumber` + sıra); **Web DTO (r4, F-4):** `WmsPickTaskDto`'ya eklemeli `Wave { WaveId, WaveNumber, Sequence }` üyesi (yeni küçük sınıf aynı dosyada) ve `Row`'a eklemeli `wave` alanı; **CSV sırası (r4, F-4):** `ListQuery` dışa aktarımda da çağrıldığı için `waveId` verilince CSV satırları da **dalga sırasıyla** (`waveSequence`) gelir — bilinçli davranış: ekran ve CSV aynı sırada (toplayıcı listesi dalga sırasıyla yazdırılır); `waveId` yokken CSV sırası (`-createdAt`) ve CSV sütunları (Web `:97`) değişmez, CSV'ye "Dalga" sütunu v1'de eklenmez; varsayılan sıralama (`-createdAt`) değişmez | MOD-0178 | DL-4 |
| X-8 | `docs/analysis/contracts/wms-bundle.openapi.yaml` (MOD-0178'in sözleşmesi, `version: 1.1.0` `:64`) | dalga üyesi yok; `OutboundEventKind` kapalı enum, 15 değer (`:1812-1814`); servis sözleşme testleri şemayı **kapalı** okur — `additionalProperties` yazmayan her nesne `false` sayılır (`dc010b911:INVT/TestInfrastructure/ContractSchemas.cs:19-21`, `:404-407`) | eklemeli: (1) `OutboundEventKind` enum'una `PickTasksIssued`, `PickTasksWithdrawn` (X-5'in tel karşılığı); (2) pick görevi (`WarehouseTask` `:1352`, `OutboundPickTask` `:1694`) `wave` üyesi; (3) outbound görünümü (`OutboundShipmentView` `:1757`) `pickIssuance` üyesi (`waveId` boş olabilir — dalgasız açılış, §8.19); (4) X-7 süzgeç / sıralama: `listPickTasks` (`:396`) `orderBy` enum'u bugün `[createdAt, -createdAt]` (`:406`) → eklemeli `waveSequence`, yeni sorgu parametresi `waveId`. **Üretim karşılığı (F-1; ölçüm `33bb4343a`; aynı CT adımında sözleşmeyle eşlenir, kod DL-2'de X-2 ile):** (2) için görev DTO'ları `INV/Application/Features/PutawayTasks/PutawayTaskModels.cs:74` `WarehouseTaskDto` (eşleme `INV/Application/Features/PutawayTasks/Services/PutawayTaskReads.cs:22`) ve `INV/Application/Features/OutboundShipments/OutboundShipmentModels.cs:70` `OutboundPickTaskDto` (eşleme `INV/Application/Features/OutboundShipments/Services/OutboundShipmentReads.cs:91`); (3) için `OutboundShipmentModels.cs:87` `OutboundShipmentViewDto` (eşleme `OutboundShipmentReads.cs:61` `View`). Bu dosyalar r2'nin XMC listesinde yoktu. **Sıra (F-3):** şemalar kapalı olduğu için DTO'ya `pickIssuance` / `wave` eklendiği an değişmeyen 0178 sözleşme testleri kırmızı olur → eklemeli sürüm **DL-2 başlamadan önce** yazılır ve dondurulur (CT adımı; 0180 dev dilimi yazmaz). **Sürüm numarası sabitlenmez:** MOD-0178 r28 WMS-BUNDLE 1.2.0'ı Item adımına ayırdı (MOD-0178 paketi `:25`); 0180 üyeleri "0178 CT ile eşgüdümlenen bir sonraki eklemeli sürüm (1.2.0 ya da 1.3.0)"e girer — **hangisi olacağı CT kararıdır** (§19.4 CT-1). (4) aynı sürüme girer (Q-12 = A); CT ayrı sürüm seçerse (4) DL-4 başlamadan önce | MOD-0178 + CT | DL-2 başlamadan önce CT adımı ((4) en geç DL-4 öncesi) |
| X-9 | `dc010b911:INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs:88-89`, `:92-93`, `:100-115`, `:120` | 0181 bildirimleri `CycleCountCollections.All` (`:88-89`) ve `.Evidence` (`:92-93`); `declared` listesi `:100-115`; ad öneki süzgecinde `wms_` zaten var (`:120`, MOD-0178 DL-1) | `WaveCollections.All` için `Assert.Equal` (0181 satırlarının yanında) + `declared`'a `.Concat(WaveCollections.All)`; önek süzgeci **değişmez** (`wms_wave_*` `wms_` ile yakalanır) | MOD-0173 (MVP-1 CT, Beste) | DL-1 (DL-3'te `wms_wave_exceptions` aynı listeye eklenir) |
| X-10 | `dc010b911:INVT/CentralAudit/CentralAuditDeclarationTests.cs:53`, `:60-69`, `:130` | `Assert.Equal(117, WriteCommands.Length)` (`:53`; 116 → 117 MOD-0181 DL-5, `c76c02944`); sağlayıcı listesi (`:60-69`); `Assert.Equal(14, seen)` (`:130`) | sayı = **dilim başındaki ölçülen sayı + dilimin 0180 yazma komutları** (0181 emsali, `:44-52` yorumları; 0180 toplamı **12** — r3'te `ReleasePendingOrdersCommand` eklendi, DL-2) — paket sabit bir taban sayı yazmaz, dilim kaydı tabanı o dilimin başlangıç commit'inde ölçer; listeye `WavePlanningManifestProvider`; `seen` 0180 depolarının `WithTransactionAsync` geri çağrıları kadar artar (yalnız yeni işlem açan depo) | MOD-0173 (MVP-1 CT, Beste) | DL-1…DL-3 |
| X-11 | `INV/Api/Program.cs` | manifest kayıtları | `WavePlanningManifestProvider` + `AddWavePlanning()` (politika uygulamasının DI kaydı dahil; kompozisyon kökü) | MOD-0173 (MVP-1 CT, Beste) | DL-1, DL-2 |
| X-12 | `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs` (commit `2f7b6145e` `RuleAheadOfCatalogue`) | `CNT` → "MOD-0040-FU02 DL-8" satırı; RCS-001 §3'teki her OTOMATİK satır ya katalogda ya bu sözlükte olmalı | RCS-001 §3'e `WAV` satırı yazıldığı **aynı adımda** sözlüğe `["WAV"] = "MOD-0040-FU02 DL-8"` (yoksa test kırmızı). **Durum (r4): yazıldı — commit `0aa802519`, sahip onayı Beste 2026-11-12** (`RecordCodeTypeCatalogTests.cs:30`; commit iletisi: `WAV` girdisi düşürülünce test kırmızı 16/17, geri alındı) | MOD-0040-FU02 (Beste, K2) | `.antigravity` kapısı adımı (§6) — **kapandı** |
| X-13 | `services/Diten.AuthService/**` rol şablonu | — | planlayıcı / vardiya sorumlusu / performans izleyici rol önerisi (§14) | AuthService — Ali (Auth) | ayrı WP |
| X-14 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` **`:97`** (ölçüm HEAD `0b5062383`; r8, r7 incelemesi F-1) | 0178 uç kapsama testi; üretim `ocelot.json`'u okur (`:23`; test projesine bağlı kopya `gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj:26`) ve **bütün** InventoryService controller'larını özyineli tarar — sınıf `[Route]`'u `api/warehouse` ile başlayan her controller (`:219-236`, önek süzgeci `:228`); `:97` `Assert.Equal(73, endpoints.Count)` yönlendirilen uç sayısını sabitler, üstündeki yorum (`:80-96`) her artışı adıyla sayar (son artış MOD-0181 DL-5, 70 → 73, `a55087094`) | **r8 — XMC-001 değişikliği (r7'nin "değişmez" yazımı yanlıştı):** 0180 controller'ları (`[Route("api/warehouse")]`, `Controllers/WavePlanning/`) taramaya kendiliğinden girer; her 0180 ucu `/api/warehouse/{everything}` ile tek rotaya eşleşir, PATCH ve DELETE yoktur (§3) — rota iddiaları (`:98-108`) değişmeden yeşil kalır, ama `:97` ilk 0180 ucuyla kırmızı olur. **Kural:** uç ekleyen her dilim (DL-1, DL-2, DL-3, DL-5 — §23 / §24; DL-4 yalnız Web ekranları ve var olan `GET /pick-tasks`'a sorgu parametresi ekler (X-7), yeni servis ucu açmaz; DL-6 yalnız test) `:97`'yi **dilim başında ölçülen sayı + dilimin eklediği 0180 uç sayısı** yapar ve yorumuna dilimin uçlarını adıyla ekler (MOD-0181 emsali — MOD-0181 paketi `:1029-1036`, commit `a55087094`); değişiklik dilimin controller uçlarıyla **aynı commit'tedir** ve dilim kabulünde Gateway test projesi koşar — arada kırmızı pencere kalmaz (AC-55 0178 sayı vakası, S-55c). Paket sabit bir taban yazmaz (X-10 deseni). Beklenen dağılım (**çıkarım, bağlayıcı değil** — §3'ün 23 ucu, §14 izin dilimleri ve §23 kapsamından; kesin sayı dilimin controller farkından sayılır): DL-1 14 (`GET` / `POST /waves`, `GET /waves/{waveId}`, `update`, `GET` / `POST orders`, `orders/remove`, `suggestions`, `cancel`, `candidates`, `load`, `events`, `GET` / `POST settings`), DL-2 2 (`release`, `tasks`), DL-3 5 (`recall`, `exceptions` ×2, `resolve`, `dashboard`), DL-5 2 (`kpis`, `kpis/export`); arada başka modül uç eklemezse 73 → 87 → 89 → 94 → 96. `ocelot.json` ve `:97` / yorum dışındaki satırlar değişmez. **Sahip onayı:** Beste 2026-11-13, "tamam" (kararlar 03 eki, commit `c7dc4ea6a` — MOD-0181'deki "70'ten 73'e" yoluyla aynı) + **kapsam teyidi** (r9, r8 incelemesi F-2; Beste 2026-11-13, "evet", kararlar 03 eki, commit `6235794ab`): onay uç ekleyen **her** dilimi (DL-1, DL-2, DL-3, DL-5) kapsar, dilim başına ayrı sahip onayı gerekmez; her dilimin artışı (önce → sonra) dilim kabul kaydına yazılır ve kayıt güncellenen satıra **satır numarasıyla değil** `Assert.Equal(<n>, endpoints.Count)` satırıyla atıf yapar (`:97` yalnız bugünkü HEAD ölçümüdür; DL-1 yorum eklediğinde satır kayar). 0180'in kendi kapsama testi ayrı dosyadır (`Mod0180WaveRouteTests.cs`, DL-6) | integration-agent (test dosyası); rota ailesi MOD-0178 (Beste) | DL-1, DL-2, DL-3, DL-5 |
| X-15 | Governance Rol İzinleri köprüsü: `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml` (`ActionVerbs` köprüsü; `configure` `:239`, `release` `:269`, `resolve` `:276`, `read` / `create` / `update` / `export` / `cancel` `:165-196` var; **`recall` yok**), `frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (`ActionVerb_*`), `frontend/Diten.Web/wwwroot/assets/js/Governance/RoleAssignments/perm-label.js` (`ACTION_FAMILIES` `:52-54`; `configure` `:52`, `release` / `resolve` `:54` var; **`recall` fiil olarak yok**) | ölçen bekçi `frontend/Diten.Web/tests/role-assignments-label-ledger.test.js` üretim manifestlerini okur; defter `docs/roadmap/backlog/role-permission-label-ledger.md` "Tavan: fiil 0 · satır 96" — fiil defteri **boş kalmak zorunda**, yeni fiil deftere yazılamaz | eklemeli: (1) yeni fiil **`recall`** (`warehouse.waves.recall`, DL-3): köprüye `["recall"] = Localizer["ActionVerb_Recall"].Value`, 7 resx'e `ActionVerb_Recall` (en dışı değerler en'den farklı), `perm-label.js` `ACTION_FAMILIES`'te bir aileye `recall` (öneri: **`secondary`** — geri alma ailesi, `bx-undo`, `withdraw` / `cancel` / `rollback` içerir, `perm-label.js:55`; r2'nin "`danger` / geri alma" yazımı karışıktı — `danger` `:53` silme ailesidir, F-6; kesin aile sahibin); (2) **`settings.configure`** (Q-6 = B, DL-1): fiil `configure` köprüde **var** (`:239`, aile `:52`) — fiil eklemesi gerekmez, yalnız satır adı; (3) satır adları `SharedResource.{7}.resx` `Perm.Entity.*` (§5 tablosu; beklenen kodlar `WAVES`, `WAVESSETTINGS`, `WAVESEXCEPTIONS`, `WAVESKPIS` — **çıkarım**, kesin kodu üretim `PermLabel.split` verir, DL-1'de vitest ile ölçülür). **Sahip (depoda ölçüldü):** bu dosyaları sahiplenen bir module pack ya da domain-config satırı **yok** (`git grep` `execution/`: 0); ekranı Ali kurdu (`af9602775`, `d593edf4a`, `8bcd75314`; BL-508 / WP-WEB-ROLE-PERMISSION-LABELS-01), son eklemeli değişiklik MOD-0181 DL-3 (`65b968b64`, Beste "Köprüye eklemeli yaz"), 0181 r8 bu dokunuşun sahip teyidini hâlâ açık yazar (MOD-0181 paketi `:1395`). **XMC-001 durumu:** açık — dokunmadan önce sorulur; soru Ali'ye (RBAC / MOD-0018, ekranın kurucusu) ve Beste'ye (merkez) | Governance Rol İzinleri — **sahip TBD** (öneri Ali, MOD-0018) | DL-1 (satır adları), DL-3 (`recall` + satır), DL-5 (satır) |
| X-16 | `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md` §14.1 (r28 `:2117-2120`; r29 notu `:2128-2152`) | "`warehouse.*` MOD-0178'in; MOD-0180 / 0181 / 0182 kendi namespace'lerini açar" (Q-08) | Q-7 = A (karar 03): §14.1'e not — "`warehouse.waves.*` alt ad alanı MOD-0180'indir (D-4, 2026-11-12; PKS-001 §4 satırı)"; 0178 paket revizyonu (yalnız not, davranış yok). **Sürüm ve onay (F-5):** not MOD-0178 paketini **r29**'a taşır; r29 CT-APR-02 bağımsız incelemesi + CT-APR-01 onay satırı (onaylayan Ali) ister; **sahip kararı (karar 03 eki):** r29, MOD-0178 r28 ile **aynı onay turunda** Ali'ye götürülür (§6, §24). **Durum (r5): yazıldı — MOD-0178 r29 `adfe1bd7d`, inceleme UYGUN** (`docs/records/reviews/MOD-0178/2026-11-13-2e4036b6.md`; not MOD-0178 §14.1 `:2128-2152` — r4'teki `:2117-2120` atfı r29'da kaydı); **Ali'nin onay turu (r29 + bu paket) açık**. **Bekçi (r5, CT kararı; r6: sahip onayladı 2026-11-13, kararlar 03 eki; r29 incelemesi F-1):** notun "MOD-0178 bu alt ad alanında anahtar tanımlamaz" yasağını **adıyla** ölçen test yok (`WarehouseRegistrationTests.cs:37` yalnız `StartsWith("warehouse.")` denetler). **Kapsam (r7, r6 incelemesi F-4):** 0178 manifestine **kazara** eklenen anahtarı bugün 0178'in sabitleme testleri dolaylı yakalar (sayfa eylemi → AC20 `:80-97` / AC34 `:130-149` / AC36 `:174`; yeni sayfa → `:122`; `ApiPermissions` → `:100`; `Controllers.Warehouse`'a yeni HTTP eylemi → `WmsBundleContractTests.cs:243-244`; ölçüm HEAD `90f6a1da8`); gerçek boşluk bu sabitlemeleri de **bilinçli güncelleyen** bir eklemedir — onu yalnız 0180 DL-1'deki AC-5 alt ad alanı vakası (S-5c, S-5d) yakalar; 0178 test dosyaları değişmez | MOD-0178 (MVP-1 CT, Beste) | `.antigravity` kapısı adımıyla (§6) — **metin kapandı; onay turu açık** |
| X-17 | `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Api/InventoryLedgerManifestCompletenessTests.cs` — `Every_enforced_permission_is_reachable_from_the_manifest` (ölçüm HEAD `ad1100158`: yöntem `:209`, dışlama listesi `:213-223`; r10) | MOD-0173 (FU-RSV-01) bekçisi: servis derlemesindeki **bütün** controller'ların `[HasPermission]` anahtarlarını toplar (`:211-228`) ve her birinin INVENTORY-LEDGER manifestinde (`InventoryLedgerManifestProvider`) bildirildiğini ister (`Assert.All` iddiası); yalnız namespace'i `.Controllers.Traceability` (`:213`), `.Quarantine` (`:215`), `.Expiry` (`:217`), `.Recall` (`:219`), `.Warehouse` (`:221`) ve `.CycleCounting` (`:223`; MOD-0181, yorum `:222` "XMC-001 Beste 2026-11-09") ile bitenler dışlanır. 0180 controller'ları `Diten.InventoryService.Api.Controllers.WavePlanning`'dedir (§3) — **dışlanmazsa** DL-1'in ilk `[HasPermission(WavePermissions.*)]` eylemiyle `warehouse.waves.*` anahtarları INVENTORY-LEDGER manifestinde aranır ve test kırmızı olur. Servisin namespace dışlamasıyla bütün controller'ları tarayan başka bekçisi yok (ölçüm: `EndsWith(".Controllers` deseni yalnız bu dosyada ve 0181'in kendi `CycleCountArchitectureTests.cs:175`'inde — o yalnız kendi namespace'ini seçer) | eklemeli tek satır + yorumu, `.CycleCounting` satırının altına (0181 biçimi; yorum metni öneri): `// MOD-0180: the wave controllers belong to WAVE-PLANNING (own completeness test, MOD-0180 AC-5; XMC-001 Beste 2026-11-13).` + `.Where(t => t.Namespace?.EndsWith(".Controllers.WavePlanning", StringComparison.Ordinal) != true)`; başka satır değişmez (önceki dışlamalar, `Assert.Contains(InventoryPermissions.ManageReservations, enforced)`, `Assert.All` iddiası). Değişiklik DL-1'in ilk 0180 controller'ıyla **aynı commit'tedir** (MOD-0181 emsali `446cb58db` — dışlama satırı ve X-14 karşılığı Gateway sayısı tek commit'te) — arada kırmızı pencere kalmaz; sonraki dilimlerde dosya değişmez (aynı namespace). Kanıt: AC-5 INVENTORY-LEDGER bütünlük vakası + S-5e. **Sahip onayı:** Beste 2026-11-13, "evet" (kararlar 03 eki X-17, commit `ad1100158`); ayrı sahip onayı gerekmez | MOD-0173 (MVP-1 CT, Beste) | DL-1 |

**MOD-0178 bekçi testleri değişmez (bağlayıcı):** `INVT/Warehouse/**`, `INVT/PickTasks/**` (varsa) ve 0178 adını taşıyan mimari testler
**hiçbir satırı değişmeden** yeşil kalır (Gateway'deki `Mod0178WarehouseRouteTests.cs` bu kuralın **tek** istisnasıdır: yalnız `:97` uç
sayısı ve üstündeki yorum, uç ekleyen dilimde, X-14 — r8); 0178'e eklenen davranış (X-1…X-5) 0180'in test ağacında (`INVT/WavePlanning/`) ölçülür. Paylaşılan servis
geneli bekçiler (X-9, X-10, X-17 — r10) MOD-0173'ündür ve yalnız eklemeli değişir (MOD-0181 emsali). Kanıt: kabul kaydında
`git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse` boş (AC-18).

## 6. Protected Paths
- `.antigravity/**` — kullanıcı onayı ister (Çekirdek md. 6). **Kapı — paket onayından ÖNCE, sahip onayıyla (Y-4, karar 03; D-4 "paket
  hazırlanırken yazılır"):** iki tablo satırı + PKS-001 :92 istisna cümlesi (r3, F-4) + aynı adımda X-12 ve X-16 (X-16'nın MOD-0178 r29'u
  ayrıca incelenir ve onaylanır — madde (4), F-5). **Hazırlayan:** CT (satırları hazırlar); **onaylayan:** Beste (sahip —
  `.antigravity/**` değişikliği kullanıcı onayıyla); bu paket ve geliştirme dilimleri `.antigravity/**`'a **yazmaz**. **Kapanış:** paketin
  onay satırı (CT-APR-01) yazılmadan önce; kapı kapanmadan paket `approved` / `ready-for-dev` olmaz (§18, §24). r1'in "modül PR'ı açılmadan
  önce; paket onayını engellemez" zamanlaması (MOD-0181 emsali, MOD-0181 paketi `:573-576`) karar 03 ile **değişti**.
  **Kapı durumu (r4):** (1), (1b), (2) ve (3)-X-12 **yazıldı — commit `0aa802519`, sahip onayı Beste 2026-11-12** (ölçüm `c30c47e90`:
  PKS-001 `:92` istisna cümlesi, `:105` `warehouse.*` satırı "`warehouse.waves.*` hariç — MOD-0180", `:106` yeni `warehouse.waves.*` satırı;
  RCS-001 §3 `:72` `WAV`; `RecordCodeTypeCatalogTests.cs:30` `["WAV"]`). Yazılan metin aşağıdaki önerilerle aynıdır (r3 önerisi, kayıt için
  korunur). **r5 (CT eki):** (3)-X-16 **yazıldı — MOD-0178 r29 `adfe1bd7d`, inceleme UYGUN**
  (`docs/records/reviews/MOD-0178/2026-11-13-2e4036b6.md`; not MOD-0178 §14.1 `:2128-2152`); **(4) Ali'nin onay turu (r29 + bu paket) açık.** Önerilen satır metinleri (r3; kapı adımında sahip onayıyla yazıldı):
  (1) **[yazıldı — commit 0aa802519, sahip onayı Beste 2026-11-12]** **PKS-001 §4** (`.antigravity/rules/permission-key-standard.md:105`; bugün `warehouse.*` satırı bütünüyle MOD-0178'in, Q-08 "one
  namespace per WMS module"), Q-7 = A — `warehouse.*` satırının sonuna "(`warehouse.waves.*` hariç — MOD-0180)" ve hemen altına yeni satır:
  `` | `warehouse.waves.*` | `Diten.InventoryService` — MOD-0180 Wave Planning (dalgalar, dalga istisnaları, dalga KPI'ları, depo dalga
  ayarı; owner decision D-4 2026-11-12 — `warehouse.*` altında MOD-0180'e ayrılmış alt ad alanı, Q-08 "one namespace per WMS module"
  kuralına adlı istisna; MOD-0180 pack §14; base path `/api/warehouse/waves`) | MOD-0180 (Wave Planning) | ``
  (1b) **[yazıldı — commit 0aa802519, sahip onayı Beste 2026-11-12]** **PKS-001 §4 "locked" kural cümlesi** (`.antigravity/rules/permission-key-standard.md:92`, bugün: "Each **namespace** (first segment) is
  owned by **exactly one** module/service. The owner is the sole authority that may define, deprecate or alias keys in its namespace. No key
  may be created outside its owning namespace.") — yalnız tablo satırı eklenip bu cümle değişmezse standart kendisiyle çelişir (F-4); cümlenin
  sonuna eklenecek önerilen metin: `` Exception — a **named sub-namespace** listed in its own row of the table below (today only
  `warehouse.waves.*` → MOD-0180, owner decision D-4 2026-11-12) is owned by the module named in that row: only that module defines,
  deprecates or aliases keys under it, and the first-segment owner (MOD-0178 for `warehouse.*`) does not. A named sub-namespace is created
  only by an owner decision recorded in both modules' packs. `` Sahip kararı (karar 03 eki): bu istisna cümlesi `.antigravity` kapısı onayına
  dahildir.
  (2) **[yazıldı — commit 0aa802519, sahip onayı Beste 2026-11-12]** **RCS-001 §3** (r3 ölçümü: `.antigravity/rules/record-coding-standard.md:45-75`
  tablosunda `WAV` yoktu; satır `:71` `CNT` emsali; r4: `:72`):
  `` | Dalga (MOD-0180) | OTOMATİK | `WAV` | yok — **yeni** (sahip kararı D-4, 2026-11-12; kod yok); Inventory servisinde MOD-0178'in geçici
  sayacından üretilecek (`IWarehouseRecordCodeAllocator`, MOD-0180 Q-4 = A); ortak servise (MOD-0040-FU02) geçişte DL-8'e `WAV` eklenir
  (FU-0180-RCS) | ``
  (3) aynı adımda **X-12** — **[yazıldı — commit 0aa802519, sahip onayı Beste 2026-11-12]** (Platform `RecordCodeTypeCatalogTests`
  `RuleAheadOfCatalogue` sözlüğüne `["WAV"] = "MOD-0040-FU02 DL-8"` — yoksa RCS-001 satırı bu testi kırmızı yapar) ve **X-16** — **[yazıldı — MOD-0178 r29
  `adfe1bd7d`, inceleme UYGUN; Ali'nin onay turu (r29 + bu paket) açık]** (MOD-0178 §14.1 notu `:2128-2152`; r5). İzleme: §18.
  (4) **[r29 yazıldı ve incelendi (UYGUN, `docs/records/reviews/MOD-0178/2026-11-13-2e4036b6.md`); Ali'nin onay turu (r29 + bu paket) açık]**
  **MOD-0178 r29 (F-5):** X-16 notu MOD-0178 paketini r28'den **r29**'a taşır. CT-APR-02 gereği r29 ayrı bir bağımsız inceleme kaydı ve
  CT-APR-01 onay satırı (onaylayan Ali) ister. **Sahip kararı (karar 03 eki):** r29, MOD-0178 r28 ile **aynı onay turunda** Ali'ye götürülür
  (r28'in onayı bugün bekliyor — künye `approval_status: pending`, inceleme `docs/records/reviews/MOD-0178/2026-11-12-723a765b.md`). Sıra: kapı
  adımı (1), (1b), (2), (3)-X-12 (**r4: tamam**, `0aa802519`) → MOD-0178 r29 yazımı (**r5: tamam**, `adfe1bd7d`) → r29 bağımsız
  incelemesi (**r5: UYGUN**, `2e4036b6`) → Ali'nin onay turu (r28 + r29) → bu paketin onay satırı. r28 ve
  r29'un iki ayrı sha'yla iki onay satırı mı, yalnız r29 satırı mı alacağı Ali'nin turunun işidir (çıkarım; paket bunu belirlemez).
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız integration-agent; bu paket değişiklik **istemez** (§15). AC-55'in S-55a sabotajı dosyayı
  integration-agent eliyle geçici bozar ve geri alır; kalıcı fark bırakmaz.
- `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — MOD-0178'in testi; yalnız integration-agent ve **yalnız** `:97` uç sayısı +
  üstündeki uç yorumu (`:80-96`), uç ekleyen dilimde (DL-1, DL-2, DL-3, DL-5) — X-14, sahip onayı `c7dc4ea6a` (r8) + kapsam teyidi `6235794ab` (r9; kabul kaydı
  `Assert.Equal(<n>, endpoints.Count)` satırına atıf yapar, satır numarasına değil). Başka satır değişmez.
- `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Api/InventoryLedgerManifestCompletenessTests.cs` — MOD-0173'ün testi; **yalnız** DL-1'de, ilk 0180
  controller'ıyla aynı commit'te `Every_enforced_permission_is_reachable_from_the_manifest` dışlama listesine `.Controllers.WavePlanning` satırı +
  yorumu — X-17, sahip onayı `ad1100158` (r10; MOD-0181 emsali `446cb58db`). Başka satır değişmez; DL-2…DL-6 dosyaya dokunmaz.
- `services/Diten.Building.Blocks/**` (BL-501 olduğu gibi).
- MOD-0178 kodu ve testleri — yalnız §5.1 satırlarının "Dosya" sütununda ve X-7 / X-8 "üretim karşılığı" listesinde **adıyla** sayılan
  dosyalar (r3'te tamamlandı, F-1: X-8 için `OutboundShipmentModels.cs`, `OutboundShipmentReads.cs`, `PutawayTaskModels.cs` `WarehouseTaskDto`,
  `PutawayTaskReads.cs`; X-7 için Web `Controllers/PickTasksController.cs`; r4, F-4: X-7 için Web DTO
  `frontend/Diten.Web/Models/Warehouse/OutboundDtos.cs` — yalnız `WmsPickTaskDto` (`:169`) ve yeni dalga alt sınıfı, eklemeli). Bu listede olmayan bir 0178 dosyasına dokunmak gerekirse dilim durur
  ve XMC-001 ile sahibine sorulur — r2'nin "başka 0178 dosyası yok" cümlesi DL-2 / DL-4'ün dokunacağı dosyaları saymıyordu. MOD-0173
  dosyaları yalnız §5.1 X-9, X-10, X-11, X-17 (r10) satırlarıyla; MOD-0174 / 0175 / 0176 / 0177 / 0181 dosyası yok.
- Donmuş sözleşmeler: `docs/analysis/contracts/warehouse-outbound.openapi.yaml` (1.0.0 — 0180 değiştirmez), `wms-bundle.openapi.yaml` (yalnız X-8,
  0178'in CT adımıyla), `location.openapi.yaml`, `record-code-allocate.md`.
- Başka domain servisleri; `services/Diten.AuthService/**` (X-13, ayrı WP); Archive; `_Layout.cshtml`.

## 7. Dependencies

| Bağımlılık | Tür | Durum (ölçüm) | Kullanım | Değişiklik | Sahip |
|---|---|---|---|---|---|
| MOD-0178 S2 outbound + tahsis | HARD | kod var — `dc010b911:INV/Application/Features/OutboundShipments/Services/OutboundAllocationService.cs`; kalıcı pick planı `PlannedBins[]` (MOD-0178 §4.5 `:452`) | uygun sipariş listesi, satır / tahsis / plan okuması | X-1, X-3 | MVP-1 CT (Beste) |
| MOD-0178 pick görevi + onay | HARD | görev outbound'la birlikte açılır (`OutboundAllocationService.cs:200-213`); tek başına iptal edilmez, outbound iptali kapatır (`MOD-0178…md:1397-1398`; kod `dc010b911:INV/Application/Features/OutboundShipments/Handlers/CommandHandlers/OutboundFlowHandlers.cs:161`); bütün görevler terminal → outbound `Picked` (`dc010b911:INV/Application/Features/PickTasks/Services/PickTaskCompletion.cs:39`) | görev üretimi (port), ilerleme, istisna | X-2, X-4, X-5 | MVP-1 CT (Beste) |
| MOD-0178 paketleme / sevk durumu | okuma | outbound durumları `Packed` / `ReadyToShip` / `Shipped` (MOD-0178 §8.18 `:1335-1345`) | D-5 ilerleme | yok | MVP-1 CT (Beste) |
| MOD-0173-FU02 LOCATION | HARD (okuma) | yol `Site→Warehouse→StorageLocation→Zone→Bin` (`docs/analysis/contracts/location.openapi.yaml:6`); v1.1 toplu bin kodu `{AISLE}-{RACK:00}-{LEVEL:00}-{POS:00}` (`:148`, `:280-287`); **koridor / raf alanı yok** — yalnız kod (ölçüldü `:205-252`); **saat dilimi alanı yok** (`git grep -i timezone` LOCATION sözleşmesi + InventoryService: 0) | rota girdisi (§8.7), depo doğrulaması | yok (Q-3 = A, karar 03) | MVP-1 CT (Beste) |
| MOD-0040-FU02 RCS-001 | SOFT (hedef üretici) | `status: ready-for-dev`, `approval_status: pending`; Inventory bağlantısı ve WMS türleri **DL-8**'de (`execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md:2604`); DL-8 bugün yok | `WAV` kodu | ara üretici Q-4 | Beste (K2) |
| MOD-0018 RBAC + AuthService rol şablonu | HARD | `[HasPermission]` + manifest | izinler, roller | rol şablonu ayrı WP (X-13) | Ali (Auth) |
| BL-501 merkezi denetim | HARD | InventoryService'te var — `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (`inventory-merkezi-iletim`, yol b) | §21 | yok | Building.Blocks korunan yol |
| Kullanıcı dizini | SOFT | 0178 `IWarehouseAssigneeDirectory` (`INV/Application/Features/Warehouse/Ports/IWarehouseAssigneeDirectory.cs`) | sorumlu seçimi, toplayıcı adları | yok (0180 kendi portunu açar) | MVP-1 CT |
| MDM tüzel kişilik `Code` | HARD | MOD-0178 M-2 yolu | `{ŞİRKET}` | yok | MDM |
| WMS-WAVE-BUNDLE | HARD (contract-first) | yok (taslak dizininde yok, ölçüldü) | sözleşme taslağı | bu paket (DL-1) | supply-chain-execution (Beste) |
| WMS-BUNDLE (0178) | HARD (DL-2 kapısı) | 1.1.0 (`docs/analysis/contracts/wms-bundle.openapi.yaml:64`); kapalı şema okuması (`dc010b911:INVT/TestInfrastructure/ContractSchemas.cs:19-21`) | X-8 eklemeli üyeler + `OutboundEventKind` değerleri + `listPickTasks` `orderBy` `waveSequence` / `waveId` (r3, F-1) | 0178 CT adımı, **DL-2 başlamadan önce**; sürüm numarası CT kararı (CT-1) | MOD-0178 + CT |
| Governance Rol İzinleri köprüsü | HARD (DL-1 / DL-3 / DL-5 kabulü) | `role-assignments-label-ledger.test.js` üretim manifestlerini okur; `recall` fiili köprüde yok (X-15) | yeni fiil + satır adları 7 dilde | X-15 (eklemeli) | sahip TBD (öneri Ali) |
| Gateway | — | `/api/warehouse/{everything}` → 5067 (`dc010b911:gateway/Diten.ApiGateway/ocelot.json:1153-1161`) | §15 | `ocelot.json` değişmez; `Mod0178WarehouseRouteTests.cs:97` uç sayısı uç ekleyen her dilimde (X-14, r8) | integration-agent |
| MVP-6 taşıyıcı / sevkiyat | bağımlılık DEĞİL (v1) | taşıyıcı outbound'da yok | Q-1 | — | MVP-6 |

## 8. Runtime Constraints

### 8.1 Servis ve kapsam
- Servis `Diten.InventoryService`, port **5067** (`AGENTS.md` §3; `domain-config.md:72`). Web (5001) yalnız Gateway (5000) üzerinden çağırır.
- MongoDB (repo deseni); her 0180 yazımı `WithTransactionAsync` içinde; olay + iş yazımı + merkezi denetim enlist'i aynı işlemde (§8.13).
- Kiracı + şirket: `TenantId` token'dan, `LegalEntityId` `X-Legal-Entity-Id`'den sunucuda çözülür (scope middleware; `/api/warehouse` zaten
  kapsanır — MOD-0181 §15 ölçümü). Başka kiracının / şirketin kaydı **404**.
- Depo kapsamı: her sorgu `warehouseId` ister (pano ve kuyrukta zorunlu süzgeç — BRIEF `:31`); dalga başka depodaki outbound'u içeremez.
- Silme yok; DELETE / PUT ucu yok (§3). `LedgerImmutabilityArchitectureTests` PUT / DELETE listesi (`dc010b911:INVT/Architecture/LedgerImmutabilityArchitectureTests.cs:131-160`) **değişmez** (AC-8).

### 8.2 Dalga kurma ve uygunluk
Kararlar: D-1, D-8.
1. **Yaratma** (`CreateWaveCommand`): depo ayarında `WaveManaged = true` ve `TimeZoneId` dolu olmalı (aksi 422 `WAVE_SETTINGS_REQUIRED`);
   dalga `Draft` doğar, siparişsiz olabilir. Aynı koşul **sipariş ekleme**, **serbest bırakma** ve "Sonraki dalgaya taşı"da da aranır —
   dalga yönetimi kapatılmış depoda bu üçü 422 `WAVE_SETTINGS_REQUIRED` (r3, §8.19); Taslak iptali, Taslaktan çıkarma, geri çekme, istisna
   "Siparişi çıkar" / "Yok say", okumalar ve uzlaştırma çalışmayı sürdürür. **Sıralama (r4, F-1):** ön kontrolde okunan ayar yazım anına kadar
   değişebilir; bu yüzden sipariş ekleme, "Sonraki dalgaya taşı"nın ekleme adımı ve serbest bırakmanın R1 işlemi koşulu **kendi Mongo
   işlemlerinde** ayar kaydına koşullu yazımla yeniden doğrular (§8.19 "Sıralama") — kapatmadan (C1) sonra işlenen yazım 422
   `WAVE_SETTINGS_REQUIRED` alır, yazım yok.
2. **Uygun sipariş** (planlayıcı sol listesi, öneri ve ekleme için tek tanım): aynı kiracı + şirket + depo; MOD-0178 outbound `Status =
   Allocated` **ve** `PickIssuance.State = Deferred` (görevleri henüz açılmamış — §8.6); etkin dalga üyeliği yok (`wms_wave_orders` `Active =
   true` kaydı yok). Planlayıcıda `Allocating` (iç) outbound gri ve "rezervasyon bekliyor" nedeniyle gösterilir, eklenemez (BRIEF `:57`);
   `AllocationFailed`, `Picked` ve sonrası, `Cancelled` ve görevleri açılmış (`PickIssuance` `null` ya da `Issued`) outbound listede yoktur.
3. **Ekleme** (`AddWaveOrdersCommand`, yalnız `Draft`): `outboundIds` 1..200; her biri uygun olmalı (aksi 422 `WAVE_ORDER_NOT_ELIGIBLE`,
   satır sonucu); sipariş **bütünüyle** eklenir (D-8). Başka etkin dalgadaysa 409 `WAVE_ORDER_IN_OTHER_WAVE`; eşzamanlı iki eklemede tekil
   kısmi index ikinciyi reddeder (aynı işlem, kaybedende kayıt yok). Sınır aşımı engellemez (§8.5). Üyelik yazımı ile aynı işlemde ayar kaydına
  koşullu yazım (§8.19 "Sıralama"; r4, F-1).
4. **Çıkarma** (`RemoveWaveOrdersCommand`, yalnız `Draft`): üyelik `Removed`, `Active = false`; gerekçe isteğe bağlı.
5. **Öner** (`GetWaveSuggestionListQuery`, D-1): uygun siparişlerden dalganın `ShipToFilter`'ına uyanlar, outbound `CreatedAt` artan (en eski
   önce), dalganın kalan `MaxOrders` / `MaxLines` kapasitesi kadar (sınır yoksa en çok 200). **Yazmaz**; kullanıcı seçip "Ekle" der
   (`AddedVia = Suggestion`). Periyodik otomatik kurma yok.
6. **Sipariş başına kesim saati yok:** outbound kesim / sevk tarihi taşımaz (MOD-0178 §4.5 `:422-443`); planlayıcı sütunu "—" gösterir,
   dalganın kesimi dalganındır (Q-22).

### 8.3 Durum makinesi
| Durum | Geçiş | Komut | Koşul |
|---|---|---|---|
| `Draft` | → `Releasing` → `Released` | `ReleaseWaveCommand` (+ gerekirse `CompleteWaveReleaseCommand`) | §8.6; ≥ 1 sipariş |
| `Draft` | → `Cancelled` | `CancelWaveCommand` | gerekçe 10–500 zorunlu; üyelikler `Removed`; **silinmez** |
| `Releasing` (iç) | → `Released` · → `Draft` (`ReleaseAborted`, hiç görev açılmadıysa) | bakım / istek | §8.6 |
| `Released` | → `Picking` | `ReconcileWaveCommand` | dalga görevlerinden biri `InProgress` / `Completed` ya da onay aldı |
| `Released` \| `Picking` | → `Completed` | `ReconcileWaveCommand` | `Issued` her siparişin dalga görevleri terminal **ve** hiç `Withdrawn` / `Excluded` / `OrderCancelled` üyelik yok |
| `Released` \| `Picking` | → `PartiallyCompleted` | `ReconcileWaveCommand` | `Issued` siparişlerin görevleri terminal **ve** en az bir üyelik `Withdrawn` (D-9) ya da `Excluded` ya da `OrderCancelled` (karar 03 Y-2 — D-9'un genişlemesi). `Excluded` yalnız dalgayı `Released` yapan serbest bırakmadan kalabilir: iptal edilen bir denemenin (`ReleaseAborted`) `Excluded` üyelikleri `Removed`'a çevrilir (§8.6 adım 5, F-7) ve sayılmaz |
| `Released` \| `Picking` | (durum değişmez) | `RecallWaveRemainingCommand` | §8.9; sonraki uzlaştırma `PartiallyCompleted`'a götürebilir |

- İptal **yalnız `Draft`**'tadır; serbest bırakılmış dalga için tek yol "kalan görevleri geri çek"tir (D-9; BRIEF `:19`). `Released` ve sonrası
  `cancel` → 409 `WAVE_INVALID_STATE`.
- Kesim saati geçince **otomatik kapanış yoktur**; `CutoffMissed` istisnası açılır (D-9, §8.10).
- Kısa toplama (0178 kısa kapatma) görevi terminal yapar; dalga `Completed` olabilir, ayrıca `ShortPick` istisnası açılır (Q-10).
- `Completed`, `PartiallyCompleted`, `Cancelled` terminaldir; terminal dalgada her yazma 409 `WAVE_INVALID_STATE` (istisna çözümü hariç —
  istisna kaydının kendisi yazılır, dalga değişmez).
- Serbest bırakılmış dalgada sipariş listesi salt okunurdur; yalnız geri çekme ve istisna eylemleri (BRIEF `:75`).

### 8.4 Kesim saati ve saat dilimi
Karar: D-3.
- Kesim saati **deponun yerel saat diliminde** girilir ve gösterilir (D-3). Saat dilimi kaynağı: `WaveWarehouseSettings.TimeZoneId` (IANA) —
  LOCATION'da alan yoktur (ölçüldü, §7); kalıcı yer kararı Q-3 = A (karar 03; FU-0180-TZ-LOCATION).
- `CutoffAtUtc = ToUtc(CutoffLocal, TimeZoneId)`; **yaz saati boşluğundaki** yerel saat (var olmayan an) → 400 `WAVE_CUTOFF_INVALID`; **çakışan**
  (iki kez yaşanan) yerel saat → erken ofset seçilir ve cevapta `cutoffAmbiguousResolved = true`. Yaratma / düzenlemede `CutoffAtUtc` şimdiden en
  çok 1 saat geride olabilir (geç girişe pay), daha eskiyse 400 `WAVE_CUTOFF_INVALID`.
- Ekranda kesim saati depo saatiyle yazılır; kullanıcının saat dilimi (tarayıcı) depodan farklıysa ikisi de gösterilir ("14:30 depo · 13:30
  sizin") (D-3). Geri sayım `CutoffAtUtc`'den.
- Gün sınırları (KPI günlük kovaları, "bugün serbest bırakılan") deponun yerel gününe göredir.

### 8.5 Sınırlar ve uyarılar
Kararlar: D-7, D-12.
- `MaxOrders` / `MaxLines` **iş sınırıdır, engellemez** (D-7): ekleme sınırı aşsa da yazılır; planlayıcı kartı doluluk çubuğu + "Sipariş sınırı
  aşıldı (n / m)" / "Satır sınırı aşıldı (n / m)" uyarısı gösterir (mockup v2). Serbest bırakmada sınır aşılmışsa istek
  `confirmLimitOverride = true` taşımalıdır, aksi 409 `WAVE_LIMIT_CONFIRMATION_REQUIRED` (cevapta aşımlar); ekran bunu onay modalıyla sorar.
- **Ağırlık / hacim (D-12):** outbound satırı ağırlık / hacim taşımaz (WAREHOUSE-OUTBOUND `OutboundLine` `:86-97`); değer yoksa "—" gösterilir,
  v1'de sınır değildir; toplam hesaplanmaz.
- **Teknik üst sınır (iş sınırından ayrı, engelleyici — Q-8 = A, karar 03):** dalga başına en çok **500 sipariş** ve **2.000 planlı toplama görevi**
  (`Σ PlannedTaskCount`); aşımda ekleme 422 `WAVE_TOO_LARGE`. Gerekçe: rota bütçesi (§8.7) ve tek dalga belgesinin boyutu.
- Uyarı türleri (kart ve ayrıntı): sınır aşımı, kesime < `RiskMinutesBeforeCutoff` kaldı (yalnız Taslak'ta "zaman dar"), farklı saklama koşullu
  Staging yok (bilgi değil — kapsam dışı). Taşıyıcı uyarısı ("n sipariş farklı taşıyıcıda") v1'de **yok** (veri yok, Q-1).

### 8.6 Serbest bırakma ve MOD-0178 seam'i
**Ön koşul — görevlerin ertelenmesi (X-1, X-3, X-4; Q-2):** dalga yönetimli depoda (`WaveManaged = true`) MOD-0178 tahsis bittiğinde pick
görevi **açmaz**: outbound `Allocated` + `PickIssuance.State = Deferred` olur; kalıcı plan (`PlannedBins[]`) zaten işlem A'da yazılıdır
(MOD-0178 §8.17 adım 5 `:1290-1298`). Karar 0178'in portundadır: port **`IPickIssuancePolicy`** (0178; varsayılan uygulama
`ImmediatePickIssuancePolicy` — bugünkü davranış); 0180 kendi uygulaması **`WavePickIssuancePolicy`**'yi `IPickIssuancePolicy` olarak kaydeder
(depo ayarını okur; X-4, X-11 — tek ad, F-12). `WaveManaged = false` ya da ayar yok → `Immediate` → 0178 kod yolu ve çıktısı bugünküyle aynı
(AC-18). **Ayar `true` → `false` (Y-1 = A, karar 03 eki — r3):** reddedilmez; kapatma anında depodaki bekleyen siparişlerin (§4.5 tanımı)
görevleri dalga yokmuş gibi `IssueImmediateAsync` ile açılır; böylece ertelenmiş sipariş dalga yaratılamayan (422 `WAVE_SETTINGS_REQUIRED`) bir
depoda takılı kalmaz. Akış, yarış, kısmi hata ve denetim §8.19'dadır.

**Port (0178'in, X-4) — `IWavePickIssuance`:**
- `IssueAsync(WaveIssueRequest)`: `{ WaveId, WaveNumber, ActorUserId, OutboundId, Items[] { AllocSeq, LocationId, Sequence } }` — **sipariş
  başına bir çağrı**. 0178 kendi işleminde: outbound `Allocated` ve `PickIssuance.State = Deferred` (ya da aynı `WaveId` ile `Issued` → **replay**,
  yazım yok, mevcut görev kimlikleri döner); `Items` kümesi outbound'un `(AllocSeq, LocationId)` plan kümesine **eşit** olmalı (aksi
  `PlanMismatch`); görev numaraları sayaçtan işlem dışında (0178 deseni `OutboundAllocationService.cs:172-185`); görevler 0178'in `PickTask`
  (`:248`) ve `TaskCreated` (`:288`) kuruluşuyla — X-3 (a) ile ortak yardımcıya taşınmış **aynı** kurucular — + `WaveRef { WaveId, WaveNumber,
  Sequence }`; outbound `PickIssuance = Issued(WaveId)`, `Version` koşullu; olaylar
  `PickTasksIssued` (outbound) + görev başına `Created` — **tek 0178 işleminde, hepsi ya da hiçbiri (sipariş düzeyinde)**. Sonuç:
  `Issued { TaskIds, TaskNumbers }` \| `Ineligible { Code: OutboundCancelled | NotDeferred | IssuedToOtherWave | PlanMismatch | NotAllocated }`.
  Teknik hata / zaman aşımı istisna olarak yükselir (sonuç bilinmiyor).
- `WithdrawAsync(WaveWithdrawRequest)`: `{ WaveId, OutboundId, ActorUserId, Reason }` — 0178 kendi işleminde: outbound `PickIssuance =
  Issued(WaveId)` olmalı; outbound'un bu dalgaya ait **bütün** görevleri `Open`, onaysız (`Posted` / `Pending` onay yok, uçuş kilidi yok)
  ise görevler `Cancelled` (`CancelReason` = gerekçe, olay `Data.waveRecalled = true`), outbound `PickIssuance.State = Deferred`, `WaveId = null`,
  olay `PickTasksWithdrawn` → `Withdrawn`; biri başlamışsa yazım yok → `Started`; zaten bu dalgadan çekilmişse → `Withdrawn` (replay).
  `Refused` onay "başlamış" sayılmaz (stok hareketi yok).
- **`IssueImmediateAsync(WaveImmediateIssueRequest)` (r3, Y-1 = A, §8.19):** `{ OutboundId, ActorUserId, Reason }` — sipariş başına bir çağrı;
  dalga yokmuş gibi açılış. 0178 kendi işleminde: outbound `Allocated` ve `PickIssuance.State = Deferred` olmalı; görevler outbound'un kendi
  kalıcı planından (`PlannedBins[]`), tahsisteki bugünkü yolla **aynı** sırada ve X-3 (a) ortak kurucularıyla kurulur — `WaveRef` **yok**; numaralar
  sayaçtan işlem dışında; outbound `PickIssuance = Issued`, `WaveId = null`, `IssuedAtUtc`, `Version` koşullu; olaylar `PickTasksIssued`
  (`Data.reason = WaveManagementDisabled`, `wave = null`) + görev başına `Created` — **tek 0178 işleminde, hepsi ya da hiçbiri**. Zaten `Issued`
  ve `WaveId = null` → **replay** (yazım yok, mevcut görev kimlikleri). Sonuç: `Issued { TaskIds, TaskNumbers }` \| `Ineligible { Code:
  OutboundCancelled | NotAllocated | NotDeferred | IssuedToOtherWave }` (`Issued(WaveId)` → `IssuedToOtherWave`; **r4, F-5:** `PickIssuance = null` —
  görevleri tahsiste açılmış outbound — → `NotDeferred`, `IssueAsync`'teki kodla aynı; ikinci görev seti açılmaz). Eşzamanlı iki çağrıda
  outbound yazımı `PickIssuance.State = Deferred` + `Version` koşulludur: kaybeden çağrı yazım yapmaz, outbound'u yeniden okur ve replay
  (`Issued`, kazananın görev kimlikleri) döner (AC-15 j). **İkinci bekçi (r5, F-5; ölçüm `1aaa0db22`):** 0178 outbound olaylarında tekil
  `(TenantId, OutboundId, Seq)` index'i var (`INV/Persistence/Warehouse/WarehouseIndexConfiguration.cs:147-148`) ve `Seq` işlem dışı
  okunan outbound'dan hesaplanır (`INV/Application/Features/OutboundShipments/Services/OutboundShipmentReads.cs:124`); koşul olmasa da iki
  eşzamanlı çağrının aynı `Seq`'li `PickTasksIssued` olayı çakışır — bu index ikinci set'e karşı ikinci bekçidir, replay'in bekçisi değildir. Teknik hata / zaman aşımı istisna olarak yükselir (sınıflandırması 0180'dedir, §8.19).
- Merkezi denetim: port 0178 deposunun işleminde çalışır ve çağıran **0180 komutunun** merkezi kaydına enlist olur (MOD-0181 §8.11 posting
  emsali — ayrı merkezi kayıt doğmaz).

**0180 serbest bırakma algoritması (`ReleaseWaveCommand`, izin `warehouse.waves.release`, If-Match):**
1. **Ön kontroller (yazımsız):** istek anahtarı; `Draft`; ≥ 1 `Planned` üyelik (aksi 422 `WAVE_EMPTY`); depo ayarı (§8.2 kural 1); her
   üyeliğin outbound'u hâlâ uygun (§8.2 kural 2) — değilse **422 `WAVE_ORDERS_INELIGIBLE`** (`details.orders[] { outboundId, code }`), yazım yok,
   kullanıcı çıkarır; teknik üst sınır (§8.5); sınır onayı (§8.5).
2. **Rota** (§8.7, yazımsız): her üyeliğin `PlannedBins`'inden plan öğeleri → `Sequence` 1..n.
3. **İşlem R1 (0180):** dalga `Releasing`, `ReleaseLeaseUntilUtc = şimdi + Warehouse:Wave:ReleaseLeaseSeconds` (öneri 120), `PickPlan[]`,
   `RouteSummary`, `ReleaseRequest`, olay `ReleaseStarted`, merkezi enlist; **aynı işlemde ayar kaydına koşullu yazım** (§8.19 "Sıralama"; r4,
   F-1) — dalga yönetimi bu arada kapatıldıysa 422 `WAVE_SETTINGS_REQUIRED`, R1 yazılmaz, dalga `Draft` kalır, görev açılmaz.
4. **Görev üretimi:** siparişler en küçük `Sequence`'larına göre sırayla `IssueAsync`. Her `Issued` sonucu üyeliğe **aynı turda** yazılır
   (işlem R2-i: `Issued`, görev kimlikleri; olay yok — toplu olay R3'te).
   **Kısmi hata (iş):** `Ineligible` (ör. outbound ön kontrol ile istek arasında 0178'de iptal edildi) → üyelik `Excluded` (`Active = false`)
   + istisna `OrderNotIssuable` (iptal ise `OrderCancelledInWave`); diğer siparişler sürer. **Teknik hata:** döngü durur, dalga `Releasing`
   kalır, cevap 503 `WAVE_RELEASE_OUTCOME_UNKNOWN`; tamamlama bakım servisinin `CompleteWaveReleaseCommand`'ıdır (kilit dolunca, aynı
   `PickPlan`'dan, aynı isteklerle — port idempotent olduğu için açılmış siparişler replay olur, ikinci görev doğmaz).
5. **İşlem R3:** en az bir `Issued` varsa dalga `Released`, `ReleasedBy` / `ReleasedAt`, olay `Released` (açılan görev ve hariç kalan sipariş
   sayıları), `ReleaseLeaseUntilUtc = null`; **hiç** `Issued` yoksa dalga `Draft`'a döner, olay `ReleaseAborted`, cevap 422
   `WAVE_NOTHING_ISSUED`; **bu denemenin `Excluded` üyelikleri aynı R3 işleminde `Removed`'a çevrilir** (`CloseReason = ReleaseAborted`,
   `Active = false`; olay `Data`'sı sipariş ve sonuç kodlarını taşır) — Taslak dalgada `Excluded` üyelik kalmaz, böylece sonraki tamamen
   başarılı serbest bırakma eski denemenin yüzünden `PartiallyCompleted` olmaz (F-7; §8.3). Denemede açılan `OrderNotIssuable` /
   `OrderCancelledInWave` istisnaları kalır (bilgi; vardiya sorumlusu yok sayar). Sonraki serbest bırakma yeni bir istektir; R1'de
   `PickPlan`'ı kalan üyelik kümesiyle yazar (AC-28'in "bir kez yazılır" kuralı dalgayı `Released` yapan serbest bırakmanındır — çıkarım, r3).
6. **Tekrar:** aynı `Idempotency-Key` + aynı gövde: `Releasing` → 409 `WAVE_RELEASE_IN_PROGRESS`; `Released` ve sonrası → 200 replay; farklı
   gövde → 400 `IDEMPOTENCY_KEY_REUSED`. `PickPlan` yeniden **hesaplanmaz** (kurtarmada aynı sıra).
7. Geri alma: serbest bırakmanın tersi yoktur; açık görevler §8.9 geri çekme ile kapatılır.

### 8.7 Rota optimizasyonu
Karar: D-2.
**Yöntem — S-biçimli (serpantin) koridor geçişi, belirlenimci, v1 (`Algorithm = "S-SHAPE-v1"`):** WMS'lerde yaygın sezgisel yöntem (çıkarım:
en kısa yolun kesin çözümü — gezgin satıcı — dalga boyunda pratik değildir; S-biçimi her koridoru bir kez gezer). Bu, "raf yerleşimine göre
en kısa yol" kararının (D-2) v1 yaklaşımıdır; kesin optimum iddia edilmez. **Sahip teyidi: karar 03 Y-3** — v1'de S-biçimli belirlenimci
yaklaşık yöntem kabul; kabul ölçüsü AC-25'in 39 / 66 karşılaştırmasıdır.

**Girdiler (bölge → koridor → raf):**
- Her plan öğesinin kaynak bin'i (`LocationId`) için LOCATION yolu (`getLocationPath`): **bölge anahtarı** `ZoneKey` = yoldaki `Zone` kodu; `Zone`
  yoksa (Bin → StorageLocation, K-01) `StorageLocation` kodu.
- Bin kodu LOCATION v1.1 toplu kod biçiminde ayrıştırılır: `^(?<aisle>[A-Za-z]|[0-9]{1,3})-(?<rack>[0-9]{2})-(?<level>[0-9]{2})-(?<pos>[0-9]{2})$`
  (`docs/analysis/contracts/location.openapi.yaml:148`, `:284-287`). Koridor: harf → harf sırası, sayı → tam sayı; bir bölgede harfli ve sayılı
  koridor birlikteyse harfliler önce.
- LOCATION okumaları istek başına bin başına bir kez (önbellek istek ömründe); LOCATION erişilemezse 503 `WAVE_INVENTORY_UNAVAILABLE`, yazım yok.

**Sıralama:**
1. Bölgeler `ZoneKey` sıralı (ordinal, artan). (Bölge sırası ayarı FU-0180-ZONEORDER, Q-9.)
2. Bölge içinde koridorlar artan; bölgede gezilen **k.** koridor (0'dan) çiftse raflar **artan**, tekse **azalan** (serpantin).
3. Raf içinde kat artan, sonra pozisyon artan.
4. **Eşitlik bozma (toplam sıra):** bin kodu (ordinal), sonra `OutboundId` (ordinal `D`), sonra `AllocSeq`.
5. **Geri düşüş:** kodu biçime uymayan bin'ler aynı bölgenin ayrıştırılmış bin'lerinden **sonra**, bin kodu sırasıyla; `RouteFallback = true`,
   `RouteSummary.FallbackCount` artar; ekran "n görev için raf sırası bilinmiyor — kod sırası kullanıldı" uyarısı gösterir.

**Çıktı:** `Sequence` 1..n dalga genelinde tek rota (toplayıcıya bölme v1'de yok — FU-0180-PICKER-SPLIT); `WaveRef.Sequence` 0178 görevine
yazılır (X-2); ayrıntı ekranı "Önerilen yol" sırasını ve haritasını gösterir (mockup v2).

**Ölçü (AC için):** `ProxyDistance` = ardışık iki öğe arasında Manhattan uzaklığı toplamı, koordinat `(bölge sırası × 1000 + koridor sırası,
raf)`; aynı koridorda |Δraf|, koridor değişiminde |Δkoridor| + |Δraf|. Geri düşüş öğeleri ölçüye girmez.

**Sınırlar:** dalga başına ≤ 2.000 plan öğesi (§8.5); kat / pozisyon yönü, tek yönlü koridor, asansör, sıcaklık bölgesi geçiş maliyeti v1'de
yok (FU-0180-ROUTE-2).

**Belirlenimcilik:** aynı plan öğeleri ve aynı LOCATION verisi → girdi sırasından bağımsız **aynı** `Sequence` (AC-24).

**Performans bütçesi:** (a) sıralama (LOCATION okumaları hariç) 2.000 öğede **≤ 200 ms p95** (in-process, 20 koşu); (b) serbest bırakma
uçtan uca 200 sipariş / 2.000 görev **≤ 15 s** (entegrasyon ortamı, gerçek Mongo replika seti); (c) öneri sorgusu 5.000 uygun outbound'lu
depoda **≤ 1 s**; (d) **(r4, F-6)** dalga yönetimini kapatma isteği — C1 + ilk tur, bekleyen 200 sipariş / 2.000 görevle (ilk tur bütçesinin
tamamı, §8.19) — istek içinde **≤ 15 s** (aynı ortam; serbest bırakma bütçesiyle aynı, çünkü ikisi de sipariş başına bir 0178 işlemi açar).
**En kötü durum (r5, F-6 e):** ölçüm fikstüründe 200 siparişin **hepsi** bir Taslak dalgada `Planned` üyeliklidir — her sipariş adım 2 (ii)'nin
0180 işlemini (üyelik kapanışı + kapı yazımı) ve (iii)'ün 0178 işlemini açar, yani sipariş başına iki işlem; bütçe bu durum için geçerlidir. CI
gürültüsüne karşı test eşiği bütçenin 3 katıdır; bütçe aşımı kabul kaydına yazılır (AC-27).

### 8.8 İlerleme ve durum uzlaştırması
Karar: D-5.
- **Okuma, yazma yok:** ilerleme okuma anında `wms_tasks` (dalga görevleri, `WaveRef.WaveId`), `wms_task_events` ve `wms_outbound_shipments`
  üzerinde **yalnız `Find`** ile hesaplanır; 0180 bu koleksiyonlara yazmaz (§2.3 kural 2).
- **Tanımlar (sayılar, miktar toplanmaz — farklı SKU / birim toplanmaz, MOD-0178 §4 `:291`):**
  - Toplama % = dalganın terminal (`Completed` \| `Cancelled`) görevleri / dalganın açtığı görevler (geri çekilen siparişlerin görevleri hariç);
    tamamlanan / toplam görev sayısı listede ayrıca (BRIEF `:29`).
  - Paketleme % = outbound'u `Packed` \| `ReadyToShip` \| `Shipped` olan `Issued` siparişler / `Issued` siparişler.
  - Sevk % = outbound'u `Shipped` olan `Issued` siparişler / `Issued` siparişler; `ReadyToShip` ayrıca "sevke hazır" sayısı.
- **Durum uzlaştırması:** `WaveMaintenanceHostedService` her **60 sn** (Q-11 = A, karar 03; `Warehouse:Wave:ReconcileSeconds`) açık dalgalar
  (`Released` \| `Picking`) için `ReconcileWaveCommand` gönderir (kayıt başına `SetScope`). Komut yalnız **yazacak şey varsa** yazar: durum
  geçişi (§8.3), `OrderCancelled` üyelik (outbound 0178'de `Cancelled`), istisna aç / kapat (§8.10). `CompletedAt` = dalga görevlerinin terminal
  olaylarının (`Completed` / `ClosedShort` / `Cancelled`, `wms_task_events.AtUtc`) en büyüğü — uzlaştırma zamanı değil. Aynı servis dalga
  yönetimi kapalı depolar için `ReleasePendingOrdersCommand` gönderir (r3, §8.19; r4: depo başına parti parti, kalan bitene dek —
  §8.19 adım 4; r5: bekleyen sipariş **ya da** boş olmayan takılı liste olan depoda, her outbound turda en çok bir kez — F-2, F-3; r6, F-1:
  **depo başına** turda en çok `PendingReleaseMaxBatchesPerTour` parti, depolar arasında dönüşümlü — her geçişte her depoya bir parti; r7,
  F-1: uygun depo kümesi **her geçişin başında yeniden taranır**, tur sırasında kapanan depo sıradaki geçişe katılır — §8.19 "Depolar arası sıra").
  **İki ayrı döngü (r6, F-1):** dalga uzlaştırması ve bekleyen açılış turu servisin **ayrı** döngüleridir (her biri kendi zamanlayıcısıyla:
  `Warehouse:Wave:ReconcileSeconds` ve öneri `Warehouse:Wave:PendingReleaseSeconds`, ikisi de varsayılan 60 sn); uzun bir açılış turu (§8.19,
  kalabalık depoda en kötü ~12,5 dk) dalga uzlaştırmasını geciktirmez. Her döngünün **tek tur yöntemi** testten doğrudan çağrılabilir (öneri
  `RunReconcileOnceAsync`, `RunPendingReleaseOnceAsync`); AC-15 (i), (j), (n), (p), (q) bakım turunu `RunPendingReleaseOnceAsync` üretim
  yöntemiyle sürer, komutu doğrudan göndermez (r4, F-2). **Bekçi (r7, F-5 — r6'da "tasarım kuralı, bekçi değil" idi):** AC-15 (r) üretim
  servisini iki döngüsüyle koşturur, açılış turu test kancasıyla bir 0178 çağrısında bekletilirken uzlaştırma döngüsünün tur attığını ölçer;
  döngüleri birleştiren sabotaj S-15bb onu kırmızıya çevirir (DL-3 kabulünde — uzlaştırma DL-3'tedir).
- Gecikme: ekran ilerlemesi anlıktır (okuma), dalga **durumu** en çok bir uzlaştırma turu (≤ 60 sn + işlem) gecikir.

### 8.9 Geri çekme ve iptal
Karar: D-9.
- **İptal** (`CancelWaveCommand`, `warehouse.waves.cancel`): yalnız `Draft`; gerekçe 10–500 zorunlu; üyelikler `Removed`; olay `Cancelled`.
- **Kalan görevleri geri çek** (`RecallWaveRemainingCommand`, `warehouse.waves.recall`, If-Match): `Released` \| `Picking`; gerekçe 10–500
  zorunlu. Her `Issued` sipariş için `WithdrawAsync`: `Withdrawn` → üyelik `Withdrawn`, `Active = false` (sipariş yeniden uygun olur — başka
  Taslak dalgaya eklenebilir, D-13); `Started` → sipariş dalgada kalır (sipariş bölünmez, D-8). Sonuç dalganın `Recalls[]`'ına ve olay
  `RemainingRecalled`'a yazılır. **Kısmi hata:** teknik hata alınan sipariş `Issued` kalır, cevap 207 benzeri satır sonuçlu 200 + `details.failed[]`
  (sözleşme taslağında kesinleşir); komut aynı anahtarla tekrar edilebilir (port idempotent).
- Geri çekmeden sonra kalan `Issued` siparişlerin görevleri terminal olunca uzlaştırma dalgayı `PartiallyCompleted` yapar (D-9, karar 02:
  "Kısmen tamamlandı" yalnız kalan görevler geri çekilince). **Genişleme — karar 03 Y-2:** serbest bırakmada `Excluded` olan ya da 0178'de
  iptal edilip `OrderCancelled` olan üyelik de dalgayı `PartiallyCompleted` yapar (§8.3). Kısa toplama bu kurala girmez (Q-10 = A).
- Hiç sipariş kalmazsa (hepsi çekildi) dalga bir sonraki turda `PartiallyCompleted` olur (tamamlanan 0 sipariş).
- **Dalga yönetimi kapalı depoda (r3, §8.19):** geri çekme ve istisna "Siparişi çıkar" çalışır; `Withdrawn` sipariş `Deferred` olur ve bir
  sonraki bakım turunda (`ReleasePendingOrdersCommand`) dalgasız görev alır — kapalı depoda takılı kalmaz. "Sonraki dalgaya taşı" 422
  `WAVE_SETTINGS_REQUIRED`.

### 8.10 İstisnalar
Kararlar: D-9, D-10, D-11, D-13.
**Tespit:** `ReconcileWaveCommand` her turda açık dalgalar için (terminal dalgalarda tespit durur) koşulları ölçer; istisna `SubjectKey` ile
tekildir — aynı koşul ikinci kez kayıt açmaz.

| Tür | Koşul (0178 verisinden) | Kendiliğinden kapanır mı | İzinli eylemler |
|---|---|---|---|
| `ShortPick` | dalga görevi `ClosedShort` olayıyla terminal ya da `Completed` iken `ConfirmedQuantity < PlannedQuantity` | hayır | Yok say (gerekçe); "Görevi aç (MOD-0178)" bağlantısı |
| `StockMissing` | dalga görevinde `Refused` onay, kod `INSUFFICIENT_STOCK` | hayır | Siparişi çıkar · Sonraki dalgaya taşı · Yok say |
| `LotUnderRecall` | dalga görevinde `Refused` onay, kod `LOT_UNDER_RECALL` — **otomatik çıkarma yok**, vardiya sorumlusu karar verir (D-10) | hayır | Siparişi çıkar · Sonraki dalgaya taşı · Yok say |
| `CutoffMissed` | şimdi > `CutoffAtUtc` ve dalga terminal değil — **otomatik kapanış yok** (D-9) | hayır | Yok say (gerekçe) |
| `TaskWaitingLong` | dalga görevi `Open`, onaysız ve `IssuedAtUtc`'den beri ≥ `PendingTaskMinutes` (varsayılan 30, D-11) | **evet** — görev başlayınca ya da terminal olunca `Resolved` + `Action = SystemCleared` (olay `ExceptionCleared`) | Siparişi çıkar · Sonraki dalgaya taşı · Yok say |
| `OrderCancelledInWave` | `Issued` siparişin outbound'u 0178'de `Cancelled` (görevleri 0178 kapattı) ya da serbest bırakmada `OutboundCancelled` | hayır | Yok say (bilgi; üyelik zaten `OrderCancelled`) |
| `OrderNotIssuable` | serbest bırakmada `Ineligible` (iptal dışı kod) | hayır | Sonraki dalgaya taşı · Yok say |

**Eylemler** (`ResolveWaveExceptionCommand`, `warehouse.waves.exceptions.resolve`, If-Match; gerekçe 10–500 her eylemde zorunlu):
- **Siparişi dalgadan çıkar:** `Released` \| `Picking` dalgada `WithdrawAsync` (§8.6) → `Withdrawn`; başlamışsa 409 `WAVE_ORDER_STARTED`.
- **Sonraki dalgaya taşı (D-13):** önce çıkar (aynı kural), sonra hedef dalgaya `AddWaveOrders` kuralıyla ekle — **tek komut**; hedef varsayılanı
  aynı depoda `Draft` ve `CutoffAtUtc` en yakın gelecekteki dalga; kullanıcı başka `Draft` dalga seçebilir; hedef yoksa 422 `WAVE_TARGET_INVALID`
  (kullanıcı önce dalga açar); depo dalga yönetimi kapalıysa 422 `WAVE_SETTINGS_REQUIRED` (§8.2 kural 1, r3; ekleme adımı ayar kaydına koşullu
  yazımla sıralanır — §8.19 "Sıralama", r4). Çıkarma başarılı, ekleme başarısız olursa sipariş serbest kalır (uygun listede) ve cevap bunu söyler; istisna
  `Resolved` olmaz.
- **Yok say:** istisna `Ignored`; dalga ve sipariş değişmez.
- Stok düzeltme bu ekrandan **yapılmaz**; satırdan ilgili ekrana bağlantı (BRIEF `:63-64`).

### 8.11 Riskli dalga
Karar: D-6.
Dalga **riskli** ⇔ durum `Released` \| `Picking` **ve** `CutoffAtUtc − şimdi < RiskMinutesBeforeCutoff` (varsayılan 90 dk) **ve** toplama % <
`RiskProgressPercent` (varsayılan %70); eşikler depo ayarı (D-6). Kesim geçmiş ve terminal olmayan dalga da risklidir (ayrıca `CutoffMissed`).
Risk saklanmaz, okuma anında hesaplanır; pano KPI kartı ve "Riskli olanlar" süzgeci (BRIEF `:24-25`, `:31`). Risk ve durum yalnız renge dayanmaz
(ikon + metin).

### 8.12 İyimser kilit
Karar: D-14.
Her değişebilir kayıt (`Wave`, `WaveOrder`, `WaveException`, `WaveWarehouseSettings`) `Version` taşır; yazma uçları `If-Match: W/"<version>"`
ister; uyuşmazlıkta 409 `WAVE_CONFLICT`, sessiz üzerine yazma yok; ekran "Kayıt değişti, yenileyin." çakışma durumunu gösterir (mockup v2).
Uzlaştırma (sistem) yazımı da `Version` koşulludur; kullanıcı yazımıyla yarışta kaybeden yeniden okur (CAS deseni).

### 8.13 Merkezi denetim ve yerel iz
- BL-501 InventoryService'te kurulu (`tests/architecture/audit-ledger/Diten.InventoryService.md:12`, `RequireEnlistment` açık); her 0180 yazma
  komutu `ICentralAuditDeclaration` taşır; `SourceModule` = manifest kodu `WAVE-PLANNING` (0178 `PUTAWAY-PICKING-PACKING` emsali,
  `dc010b911:INV/Application/Features/Warehouse/WarehouseCentralAudit.cs:15`).
- Merkezi kayıt ad ve gövde taşımaz; değerlerin ve **gerekçelerin** tek yeri `wms_wave_events.Data`'dır (aynı işlem, silinmez). Başarılı ve
  yazan komut 1 kayıt; replay ve 0-kayıt 0; başarısız komut iletilmez.
- 0178 portunun yazımları çağıran 0180 komutunun kaydına enlist olur (§8.6).
- **Gerekçe zorunlu yazmalar (AUD-001 + D-9):** iptal, geri çekme, istisna eylemleri (çıkar / taşı / yok say), depo ayarı değişikliği; gerekçe
  olaya yazılır. Taslak düzenleme / ekleme / çıkarmada gerekçe isteğe bağlıdır.

### 8.14 Kayıt kodu üretimi
`Wave.Number` = `{ŞİRKET}-WAV-{000001}` (RCS-001 biçimi; D-4). `WAV` öneki RCS-001 §3 tablosuna `.antigravity` kapısında yazıldı
(`.antigravity/rules/record-coding-standard.md:72`, commit `0aa802519`, sahip onayı Beste 2026-11-12; r3'te yoktu) ve servis kodunda yok
(`WarehouseRecordCodeRules.TypePrefixes` `dc010b911:…:28`; X-6, DL-1). **Hedef üretici:** MOD-0040-FU02 ortak servisi (D-4). **Bugün:** FU02'nin Inventory
bağlantısı ve WMS türleri DL-8'dedir ve DL-8 yoktur (§7). **Ara üretici (Q-4 = A, karar 03):** MOD-0178 geçici sayacı (`IWarehouseRecordCodeAllocator`)
— `TypePrefixes`'e `WAV` (X-6), MOD-0181 `CNT` emsali (RCS-001 §3 `:71`); FU02 DL-8'de `WAV` kataloğa girer ve geçiş FU-0180-RCS ile yapılır.
0180 kendi sayacını **yazmaz** (RCS-001 §6). Şirket öneki MDM tüzel kişilik `Code`'u (`^[A-Z]{2,5}$`, aksi 422 `WAVE_COMPANY_CODE_INVALID`);
sayaç yazılamazsa 503 `WAVE_RECORD_CODE_UNAVAILABLE`, dalga yaratılmaz.

### 8.15 Ekranlar ve Blueprint soft pages
| Blueprint soft page | Ekran | Sayfa kodu | Veri |
|---|---|---|---|
| (pano) | `/Warehouse/Waves` — KPI kartları (bugün serbest bırakılan, açık, riskli, ort. tamamlanma süresi, açık istisna — BRIEF `:24-25`) + liste (DataTable, server) + oluştur / düzenle yan paneli (8 alan) + hızlı görünüm | `WAVES` (nav) | KPI'lar okuma anında hesaplanır |
| (ayrıntı) | `/Warehouse/Waves/Details/{waveId}` — üst: kod, durum çizgisi, kesim geri sayımı (depo + kullanıcı saati), üç ilerleme; sekmeler **Siparişler** (toplama / paketleme / sevk durumu), **Görevler** (0178 bağlantısı, toplayıcı, raf, sıra), **Yük** (bölge / koridor yoğunluk tablosu, toplayıcı başına görev), **Olay geçmişi**; "Önerilen yol" (§8.7) | `WAVEDETAIL` (nav değil) | 0178'den okuma |
| Wave Planner View | `/Warehouse/WavePlanner` — sol: uygun siparişler (süzgeçli; ağırlık / hacim "—"; Allocating gri); sağ: Taslak dalga kartları (doluluk, kesim, uyarılar); sürükle-bırak **ve** "Ekle" düğmesi / klavye (§11) | `WAVEPLANNER` (nav) | DataTable değil (pano; Q-19) |
| Exception Queue | `/Warehouse/WaveExceptions` — DataTable (server): dalga kodu, sipariş, satır, tür, oluşma, durum, çözen; eylem yan paneli (§8.10) | `WAVEEXCEPTIONS` (nav) | — |
| Performance KPIs | `/Warehouse/WaveKpis` — menüde **ayrı sayfa** (D-16); zaman serileri + CSV (§8.16) | `WAVEKPIS` (nav) | — |
| (depo dalga ayarı) | ekran **yok** (v1) — yalnız API (`GET /waves/settings/{warehouseId}` `read`; `POST …/update` `warehouse.waves.settings.configure`); manifest `ApiPermissions` | — | Q-6 = B (karar 03); Y-1 = A kapatma akışı §8.19 (GET cevabında `deferredOrderCount`, `releasingOrderCount`, `stuckOrders[]` — r4; kapatma cevabında `pendingRelease`) |

Her ekranda durumlar: **yükleniyor, hata ("Stok servisine ulaşılamadı."), boş, çakışma ("Kayıt değişti, yenileyin."), yetkisiz (UAS-001)**
(BRIEF `:70-76`; mockup v2 durum örnekleri). Masaüstü öncelikli; **375 px**'te pano ve istisna kuyruğu yatay sayfa kaydırmasız okunur (BRIEF `:80`);
planlayıcı 375 px'te salt okunur liste görünümüne düşer (sürükle-bırak yok, "Ekle" düğmesi var). RTL (`ar`) her ekranda.

### 8.16 Performans sayfası ve CSV
Karar: D-16.
- Süzgeç: depo (zorunlu), tarih aralığı **en çok 92 gün** (aksi 400 `WAVE_KPI_RANGE_TOO_LARGE`), dalga türü (BRIEF `:68`).
- Seriler (deponun yerel günü):
  - **Günlük dalga sayısı** = o gün `ReleasedAt`'i olan dalgalar.
  - **Ortalama tamamlanma süresi** = `CompletedAt − ReleasedAt` ortalaması (`Completed` + `PartiallyCompleted`, tamamlandığı güne).
  - **Kesime yetişme oranı** = `CompletedAt ≤ CutoffAtUtc` olan / o gün kesimi olan tamamlanmış dalgalar.
  - **Toplayıcı başına satır / saat** = dalga görevlerinin `Posted` onay sayısı / toplayıcıların etkin saatleri (toplayıcı başına gün içinde ilk →
    son onay, en az 0,25 saat); toplayıcı = onayın `ConfirmedBy`'ı.
- **CSV** (`GET /waves/kpis/export`, `warehouse.waves.kpis.export`): aynı seriler aynı süzgeçle, satır başına gün; başlık satırı isteğin
  `Accept-Language`'ında (7 dil), sayılar değişmez kültürde (`.` ondalık); UTF-8. Dosya saklanmaz; okuma işlemidir (denetim §21 notu).

### 8.17 Hata kodları
Öneri (`WAVE_` öneki; kesin liste sözleşme taslağında):

| Kod | HTTP | Ne zaman |
|---|---|---|
| `WAVE_NOT_FOUND` / `WAVE_EXCEPTION_NOT_FOUND` | 404 | yok, başka kiracı / şirket |
| `WAVE_CONFLICT` | 409 | If-Match uyuşmaz (D-14) |
| `WAVE_INVALID_STATE` | 409 | durumda izinli olmayan komut |
| `WAVE_SETTINGS_REQUIRED` | 422 | depo dalga yönetimli değil ya da saat dilimi yok — yaratma, sipariş ekleme, serbest bırakma, "Sonraki dalgaya taşı" (§8.2 kural 1; r3: kapatılmış depo dahil) |
| `WAVE_CUTOFF_INVALID` | 400 | yaz saati boşluğu, geçmiş kesim |
| `WAVE_WAREHOUSE_IMMUTABLE` | 400 | düzenlemede depo değişimi |
| `WAVE_ORDER_NOT_ELIGIBLE` | 422 | eklenen outbound uygun değil (§8.2) |
| `WAVE_ORDER_IN_OTHER_WAVE` | 409 | outbound başka etkin dalgada (D-8) |
| `WAVE_TOO_LARGE` | 422 | teknik üst sınır (§8.5) |
| `WAVE_EMPTY` | 422 | siparişsiz serbest bırakma |
| `WAVE_ORDERS_INELIGIBLE` | 422 | serbest bırakma ön kontrolünde uygun olmayan sipariş |
| `WAVE_LIMIT_CONFIRMATION_REQUIRED` | 409 | sınır aşımı onaysız (D-7) |
| `WAVE_RELEASE_IN_PROGRESS` | 409 | `Releasing` dalgada aynı anahtar ya da yeni istek |
| `WAVE_RELEASE_OUTCOME_UNKNOWN` | 503 | 0178 portu sonuç vermedi; bakım tamamlar |
| `WAVE_NOTHING_ISSUED` | 422 | serbest bırakmada hiç görev açılamadı |
| `WAVE_ORDER_STARTED` | 409 | başlamış siparişi çıkarma / taşıma |
| `WAVE_TARGET_INVALID` | 422 | taşıma hedefi yok / Taslak değil / başka depo |
| `WAVE_REASON_REQUIRED` | 400 | gerekçesiz iptal / geri çekme / istisna eylemi / ayar değişikliği |
| `WAVE_KPI_RANGE_TOO_LARGE` | 400 | > 92 gün |
| `WAVE_COMPANY_CODE_INVALID` | 422 | §8.14 |
| `WAVE_RECORD_CODE_UNAVAILABLE` | 503 | sayaç / ortak servis yazılamadı |
| `WAVE_INVENTORY_UNAVAILABLE` | 503 | 0178 / LOCATION okuması erişilemez; kapatma turunda bağlantı hatası satır kodu (`pendingRelease.failed[]`, §8.19) |
| `WAVE_PENDING_ORDER_ERROR` | — (yalnız satır kodu) | r4, F-3: kapatma turunda siparişe özgü hata — sipariş atlandı, `stuckOrders[]`'a girdi (`pendingRelease.failed[]`, §8.19); HTTP cevabı 200 kalır. **r5 (F-1):** art arda hata sınırını ya da parti başına yeni takılı sınırını tetikleyen siparişler de bu kodu taşır (hepsi takılı listeye yazılır); liste doluyken takılı listeye alınmadan atlanan sipariş de bu kodu taşır (r6, F-2: tur sürer); art arda bağlantı durması kuralıyla takılan sipariş de (r6, F-3) ve eşikte liste dolu olduğu için listeye alınamayan sipariş de (r7, F-3 c). Turun neden durduğu satır kodunda değil `pendingRelease.stopReason`'dadır |
| `IDEMPOTENCY_KEY_REUSED` | 400 | aynı anahtar, farklı gövde |

### 8.18 AI Assist
Blueprint AI Tier `Assist` (Summarize / Extract / Recommend / Route), risk Medium. **Karar (Q-14 = A, karar 03):** v1 kapsamı dışında; hiçbir AI ucu ya da
çağrısı yok; FU-0180-AI. "Route" kullanım durumu §8.7'nin belirlenimci algoritmasıyla karşılanır, AI değildir.

### 8.19 Dalga yönetimini kapatma
Karar: Y-1 = A (kararlar 03 eki, 2026-11-12, Beste "öneriler tamam"): "Depoda dalga yönetimi kapatılırken bekleyen (`Deferred`) siparişler
**reddedilmez ve iptal edilmez**; kapatma anında her bekleyen siparişin toplama görevleri dalga yokmuş gibi hemen açılır (MOD-0178 normal
akışı)." r2'nin 409 `WAVE_SETTINGS_DEFERRED_ORDERS` kuralı ve hata kodu **kaldırıldı** (r2 incelemesi F-2 a ve b bu kararla düşer: iptal yok,
sipariş girişi durdurulmaz). Ekin "A" etiketi, karar 03 tablosunun r2'deki "A: 409" etiketinden farklı içeriktir; bağlayıcı olan ekin metnidir.

**Sahip teyidi — "hemen" (kararlar 03 eki, Beste 2026-11-13, "evet hemen sayılır"; r3 incelemesi (c) 3):** Y-1'in "hemen"i şudur: kapatma
isteğinde **ilk tur istek içinde** açılır (en çok **200 sipariş / 2.000 görev** — Akış adım 2 bütçesi), **kalanı en geç bir bakım turu (60 sn)
içinde** açılır (Akış adım 4: bakım turu koşmuyorsa depo ≤ 60 sn sonra başlayan turun, koşuyorsa o turun **sıradaki geçişinin** kapsamına girer —
r7, F-1, "Depolar arası sıra" — ve kalanı o turda partilerle biter). Teyit hatasız durum içindir; bağlantı hatası (tur
durur, sonraki tur sürer), siparişe özgü hata (sipariş takılır ve görünür — F-3) ve yarış (b) (C1'den sonra `Deferred` yazılan sipariş bir tur
sonra) bu süreyi uzatabilir — "Kısmi başarısızlık" ve "Yarış" başlıkları. AC-15 (g), (q); AC-27 (d). **Tur üst sınırı (r5, F-3; r6,
F-1 — kapsam netleşti):** bir bakım turu **depo başına** en çok `Warehouse:Wave:PendingReleaseMaxBatchesPerTour` (öneri **50**) parti koşar
(bir depoda bir turda en çok 50 parti; sınır tur geneline değil her depoya ayrı uygulanır). Hatasız durumda teyit, ilk tur + bir bakım turuyla
bir depoda **200 + 50 × 200 = 10.200** bekleyen siparişe (görev bütçesi partileri küçültürse daha azına) kadar tutar; üstündeki kalan bir
sonraki turda sürer. Sınır sonsuz döngüye karşı ikinci güvencedir (birincisi "her outbound turda en çok bir kez" kuralıdır). **r5 bu
daraltmayı sahibe sormadan yazmıştı (r5 incelemesi F-1); r6'da sahibe Q-23 olarak soruldu: Q-23 = A — "evet, hemen sayılır"; ilk tur
≤ 200 sipariş / 2.000 görev + depo başına tur başına 50 parti sınırı kabul (Beste 2026-11-13, kararlar 03 eki; §19.4).**
**Depolar arası sıra (r6, F-1; r7, F-1 — depo kümesi her geçişte yeniden taranır):** bakım turu **geçişlerden** oluşur; bir geçişte uygun
her depoya **bir** parti verilir, depolar kiracı, şirket, depo kimliği artan sırayla **dönüşümlü** işlenir. **Uygun depo kümesi turun başında
bir kez sabitlenmez, her geçişin başında yeniden taranır** (r6'nın sabit listesi kaldırıldı — r6 incelemesi F-1): `WaveManaged = false` olan
ayar kayıtlarından, bu turda kalıcı durma koşuluna ulaşmamış (adım 4 koşulları (2), (3), (4), (6); (5)'te yeniden açılan depo
`WaveManaged = false` süzgecinden zaten düşer, tur sürerken yeniden kapatılırsa sıradaki geçişe yeniden katılır ve turdaki parti sayısı
kaldığı yerden sayılır) **ve** bu geçişte denenecek işi olan depolar — işi
olmak = turda denenmemiş bekleyen sipariş (`Find`, `limit 1`, turun denenmiş kümesi dışlanır; `Releasing` üyeleri hariç) **ya da** bu turda
henüz uzlaştırılmamış boş olmayan takılı liste **ya da** yeniden deneme bütçesi dolmamış, bu turda denenmemiş takılı sipariş. Sonuç: tur
sırasında kapatılan depo (C1'i turdan sonra işlenen) **sıradaki geçişe katılır**; turda işi biten (koşul (1)) depo kalıcı çıkmaz, sonraki
geçişin taraması yeni bekleyen (ör. Yarış (b)) bulursa geri döner. **Tarama maliyeti (r8, r7 incelemesi F-4 — r7'nin "r6'nın tur başı
taramasıyla aynı sorgu" cümlesi yanlıştı; çıkarım, ölçülmedi):** r6'nın tur başı sorgusu dışlama taşımıyordu; geçiş sorgusu ise o deponun
**turda denenmiş kümesini** dışlar. Küme depo başınadır ve o depoda bu turda denenen sipariş sayısı kadardır — en çok 50 parti × 200 =
**10.000** kimlik; aynı küme r5'ten beri partinin kendi sorgusunda da vardır ("Bir turda her outbound en çok bir kez", Akış adım 2). Sorgu
sayısı: her geçişte kalıcı durmamış her kapalı depo için bir `limit 1` tarama (koşul (1)'le işi biten depo da taranır, çünkü (1) kalıcı
değildir) + depo başına tur başına en çok 50 parti sorgusu; her biri o deponun ≤ 10.000 kimlikli dışlama süzgecini taşır ve sunucu, ilk
denenmemiş bekleyene varana dek en kötü küme büyüklüğü kadar index girdisi geçebilir. Yani en kötü durumda (kalabalık bir depo turun sonuna
yaklaşırken) geçiş başına depo başına bir sorgu ~10.000 kimliklik süzgeçle koşar — r6'nın tur başına depo başına bir dışlamasız sorgusundan
belirgin biçimde pahalıdır; yük tur süresine (en kötü depo başına ~12,5 dk) yayılır. **Neden mekanizma değişmedi (seçim, r8):** daha ucuz yol
— "işi var" bilgisini son partinin sonucundan ve `CreatedAt` / imleç konumundan türetip denenmiş kümeyi yalnız takılı yeniden denemeler için
tutmak — `CreatedAt`'i konumun gerisinde kalan ama turda sonradan `Deferred` yazılan siparişi (Yarış (b): outbound önce yaratılır, sonra
tahsis edilir) o turda görmez; r7'nin "sıradaki geçişte" sözünü (AC-15 p18b) zayıflatır, şüpheliler ve liste dolu imleci için ayrı kural
ister. En dar düzeltme maliyetin doğru yazılmasıdır; gerçek maliyet DL-2'de bekleyen sipariş taramasının 0178 index'leriyle ölçümüne girer
(§19.1 koşulmayanlar r3 / r4 / r8). Her kapalı depo her geçişte bir parti hakkı alır,
kalabalık bir depo diğerlerini aç bırakmaz. **Süreler (r7, F-1; çıkarım — AC-27 (d)'nin parti başına en kötü 15 s bütçesi):** (1) **geçiş**
en kötü (o geçişte işi olan depo sayısı) × 15 s sürer (tek kalabalık depo yanında küçük bir depo: ≤ 30 s); (2) **tur** depo başına en çok 50
parti koşar, en kötü (turda işi olan depo sayısı) × 50 × 15 s = depo başına ~12,5 dk — tek kalabalık depoda ~12,5 dk, D kalabalık depoda
~D × 12,5 dk; (3) **yeni kapanan deponun beklemesi:** kapatma isteğinin ilk turu istek içinde koşar; kalanı için depo, bakım turu koşuyorsa
**en geç bir geçiş** bekler (o anki geçişin kalanı), bakım turu koşmuyorsa sonraki tur **en geç 60 sn** sonra başlar; ardından kalanı her
geçişte bir partiyle o turda biter. Turun uzunluğu yeni kapanan depoyu artık bekletmez — yalnız kalabalık deponun 10.200'ü aşan fazlasını
bekletir (Q-23 = A'nın kabul ettiği: fazlası sonraki turda). Teyitteki "60 sn" turun başlama gecikmesidir; geçiş ve parti süresi teyidin
"işlem" kısmıdır (Yarış (b) "≤ 60 sn + işlem" ile aynı okuma). Turlar üst üste binmez: **sonraki tur önceki bitince başlar** (bitişten en geç
60 sn sonra). Dalga uzlaştırması bu turun uzunluğundan etkilenmez — ayrı döngüdedir (§8.8; bekçisi AC-15 (r), r7). AC-15 (p14), (p17), (p18).

**Bekleyen sipariş:** §4.5 tanımı — outbound `Status = Allocated` **ve** `PickIssuance.State = Deferred` (F-2 c).

**Etkilenmeyenler (bağlayıcı):**
- **Dalgadaki siparişler** — `Released` / `Picking` dalgada `Issued` üyeliği olan siparişin outbound'u `PickIssuance = Issued(WaveId)` taşır,
  bekleyen tanımına girmez: görevleri, `WaveRef`'i, sırası ve `Version`'ı değişmez; dalga uzlaştırma, geri çekme ve istisnalarla kendi sonuna
  kadar sürer (§8.8–§8.10).
- **`Releasing` dalganın üyeleri** taranmaz (etkin üyeliği `Releasing` dalgada olan outbound atlanır): onları serbest bırakma ya da kurtarması
  (`CompleteWaveReleaseCommand`) dalgalı açar; serbest bırakma onları `Excluded` / `Removed` bırakırsa üyelik etkin değildir ve bir sonraki
  turda bekleyen olarak açılırlar. **Sayım (r4, F-5):** açılana dek `Deferred` oldukları için `deferredOrderCount`'ta sayılırlar, ayrıca
  `releasingOrderCount`'ta gösterilirler; `pendingRelease.remainingCount`'ta **sayılmazlar** (§4.5, Akış adım 3).
- 0178'de iptal edilmiş outbound (rezervasyonu 0178'in iptal yoluyla zaten geri bırakılmıştır), `Allocating` outbound (kapatmadan sonra
  politika `Immediate` döner), başka depolar.

**Seçim — tekrar edilebilir toplu iş (tek işlem değil).** Gerekçe: (1) 0178 portu **sipariş düzeyinde** atomiktir (AC-20) — bütün bekleyenleri
tek işlemde açmak 0178'de çok siparişli yeni bir yazım yolu ister (XMC-001 kapsamını büyütür); (2) depodaki bekleyen sipariş sayısı sınırsızdır
(teknik üst sınır §8.5 dalga başınadır, depo başına değil) — tek Mongo işlemi süre ve boyut sınırına takılır; (3) tek işlem, ayar yazımından
**önce** politikayı okuyup outbound'u **sonra** `Deferred` yazan tahsisi (yarış) yakalayamaz — tekrar eden tur yakalar; (4) tek işlemde bir
siparişin hatası ayar yazımını da geri alır, bu kapatmayı reddetmek olurdu (Y-1 = A'ya aykırı). Toplu iş ayar yazımından ayrıdır, her sipariş
kendi 0178 işlemindedir, port idempotenttir (`IssueImmediateAsync` replay, §8.6); iş tekrarlandıkça kalanı açar, ikinci görev doğmaz.

**Akış:**
1. **İşlem C1 (0180, `UpdateWaveWarehouseSettingsCommand`, izin `warehouse.waves.settings.configure`, If-Match, Idempotency-Key, gerekçe):**
   ayar `WaveManaged = false`, `Version` +1; ayar akışına olay `SettingsUpdated` (önceki / sonraki, gerekçe); merkezi enlist. C1 işlendikten
   sonra başlayan her tahsis politikayı `Immediate` okur ve görevleri 0178'in bugünkü yoluyla açar; C1'den sonra hiçbir Taslak üyelik ve hiçbir
   serbest bırakma R1'i yazılamaz ("Sıralama", aşağıda).
2. **İlk tur — aynı istekte, C1'den sonra (görevler "kapatma anında" açılır):** **(0) tur başı uzlaştırma (r5, F-2 d):** `PendingReleaseStuck[]`'ta
   olup artık bekleyen olmayan siparişler (§4.5 tanımı; ör. 0178'de iptal edildi) listeden çıkarılacaklar deltasına girer (neden
   `NoLongerPending`) ve partinin yazımıyla yazılır. `PendingOrderReleaseEngine` depodaki bekleyen siparişleri
   (etkin üyeliği `Releasing` dalgada olanlar hariç) şu sırayla alır: önce `PendingReleaseStuck[]`'ta **olmayan** ve şüpheli işaretli
   **olmayanlar** outbound `CreatedAt` artan (r7, F-2: liste doluyken depo imlecinden başlayarak dairesel — "Kısmi başarısızlık" (c)); sonra
   **takılı olanlar** (yeniden deneme bütçesiyle, aşağıda) — takılı siparişler bütçeyi tüketip yenileri bekletmez (r4, F-3); **en sonda
   şüpheliler** (r7, F-3: önceki bir turu bağlantı sınıfı hatayla durduran takılı olmayan siparişler — "Kısmi başarısızlık"), aralarında son
   denemesi en eski olan önce. **r8 (r7 incelemesi F-2):** r7'de şüpheliler takılı olanlardan **önce**ydi; depoda takılı olmayan tek bekleyen
   kalıcı bağlantı sınıfı hatalı bir şüpheliyse o her turun ilk çağrısı olup turu durduruyor, başarı şartı gereği sayacı da artmıyordu — takılı
   liste hiç yeniden denenmiyordu. Şüpheliyi takılı yeniden denemelerin arkasına almak yalnız var olan sıralamadaki yerini değiştirir, yeni kural
   açmaz; "bağlantı hatası turu durdurur" kuralı aynen kalır. **Takılı yeniden deneme (r5, F-3):** turda en çok
   `Warehouse:Wave:PendingReleaseStuckRetryPerTour` (öneri **50**) takılı sipariş denenir; sıra **son denemesi en eski olan önce** (son deneme
   zamanı `PendingOrderReleaseEngine`'in süreç içi belleğinde, art arda hata sayacıyla aynı anahtarla; bellekte yoksa `FirstFailedAtUtc` artan) —
   böylece aynı ilk birkaç takılı sipariş her turda denenip geri kalanı hiç denenmeden kalmaz. **Bir turda her outbound en çok bir kez denenir
   (r5, F-3):** tur, denediği outbound kimliklerini tur boyunca tutar; aynı turun sonraki partisinin sorgusu bu kümeyi dışlar (ör. partide yeni
   takılan sipariş aynı turun sonraki partisinde takılı yeniden deneme olarak tekrar denenmez). **Bütçe (r4, F-6):** en çok
   `Warehouse:Wave:PendingReleaseBatch` sipariş (öneri **200** — sipariş ekleme ve öneri listesinin üst sınırıyla aynı, §8.2) **ve**
   `Warehouse:Wave:PendingReleaseTaskBudget` görev (öneri **2.000** — siparişin görev sayısı `PlannedBins` öğe sayısıdır; serbest bırakmanın
   teknik üst sınırıyla aynı, §8.5); sıradaki sipariş görev bütçesini aşacaksa parti orada biter; partinin ilk siparişi tek başına bütçeden büyük
   olsa da işlenir (sipariş bölünmez, D-8). İstek içi süre bütçesi §8.7 (d), AC-27 (d). Her sipariş için sırasıyla:
   (i) ayar yeniden okunur; `WaveManaged = true` olduysa tur durur (yeniden açma kapatmayı geçersiz kılar, kalan siparişler yine dalgaya
   alınabilir);
   (ii) **üyelik kapanışı önce (r4, F-1):** siparişin bir Taslak dalgada `Planned` üyeliği varsa ayrı bir 0180 işleminde üyelik `Removed`
   (`CloseReason = WaveManagementDisabled`, `ClosedBy = system`, `Active = false`; üyelik `Version` koşullu) + dalga akışına sistem olayı
   `OrdersRemoved`; Taslak dalga **`Draft` kalır** (otomatik iptal yok — iptal gerekçe isteyen kullanıcı eylemidir); **kapı yazımı (r5, F-4 —
   seçim):** aynı işlemde ayar kaydına koşullu yazım — filtre `WaveManaged = false`, güncelleme yalnız `GateSeq` +1 (`Version` artmaz; "Sıralama"
   kapısının ayna görüntüsü, `Version` koşulu yok — kapalı depoda eşik değişikliği turu durdurmamalıdır). Eşleşme 0 (depo (i) ile (ii) arasında
   yeniden açıldı) → işlem geri alınır, üyelik `Planned` kalır, sipariş açılmaz, tur durur (`stopReason = Reopened`). Üyeliği olmayan siparişte
   (ii) yoktur, kapı yazımı da yoktur (yazacak üyelik yokken ayar belgesine her siparişte yazım eklemek tur başına yük ve "yalnız bir şey
   değiştiğinde yaz" kuralına aykırı olurdu); o siparişte depo yeniden açılışını yalnız (i)'nin okuması yakalar, bu yüzden kalan pencere
   üyeliksiz siparişte (i)–(iii) aralığıdır (r6, F-4 — aşağıda "Kalan pencere");
   (iii) `IssueImmediateAsync` (sipariş başına bir 0178 işlemi);
   (iv) sonuç: `Issued` → açıldı; `Ineligible` → atlanır; istisna → "Kısmi başarısızlık" (aşağıda).
   Tur sonunda, **yalnız tur bir şey değiştirdiyse** (en az bir `Issued` ya da üyelik kapanışı) ayar akışına olay `PendingOrdersIssued` (açılan
   outbound ve görev numaraları, kapanan üyelikler, `Ineligible` atlananlar ve kodları, r5: `stopReason`); takılan sipariş listesi değiştiyse (giren
   ya da çıkan sipariş — r5: kod değişimi değişiklik sayılmaz, §4.5 e) aynı işlemde `PendingReleaseStuck[]` delta yazımı (§4.5 b: işlem içi okuma,
   `WaveManaged = false` koşullu, `Version` artmaz) + olay `PendingOrdersStuckChanged` (giren siparişler ve kategorileri, çıkan siparişler ve
   nedenleri).
   **Neden kapanış önce — seçim (r4, F-1):** r3 sırası (önce görev açılışı, sonra üyelik kapanışı) iki işlem arasında süreç ölürse outbound
   `Issued` (`WaveId = null`) + Taslak'ta `Planned` üyelik bırakıyordu; bu üyelik hiçbir turun taramasına girmez (bakım turu yalnız `Deferred`
   siparişe, uzlaştırma yalnız `Released` / `Picking` dalgaya bakar) ve depo yeniden açılınca dalgayı 422 `WAVE_ORDERS_INELIGIBLE` ile tıkar.
   Ters sırada aynı ölüm siparişi `Deferred` ve üyeliksiz bırakır — bu **bekleyen sipariş tanımının ta kendisidir** (§4.5); bir sonraki bakım
   turu onu açar; süreç ölümü yetim üyelik bırakmaz (r5, F-4: r4'ün "yetim üyelik hiç doğmaz" cümlesi fazla güçlüydü — kalan pencere aşağıda).
   **Kalan pencere (r5, F-4; r6, F-4 — iki durum):** **üyelikli** siparişte (ii) kapalı depoda yazıldıktan sonra, (iii)'ten önce — pencere
   **(ii)–(iii)**; **üyeliksiz** siparişte (ii) ve kapı yazımı olmadığından (i)'den sonra, (iii)'ten önce — pencere **(i)–(iii)** (siparişlerin
   çoğu bu durumdadır). Bu aralıkta depo yeniden açılır **ve** kullanıcı siparişi bir Taslak dalgaya ekler (üyelikli siparişte yeniden ekler)
   ise (`AddWaveOrders` kapısı `WaveManaged = true` gördüğü için geçer; outbound hâlâ `Deferred` olduğu için uygundur), (iii) görevleri yine
   dalgasız açar: sonuç `Issued` (`WaveId = null`) outbound + Taslak'ta `Planned` üyelik + yeniden açılmış depoda dalgasız görevler. Sonuç ve
   kurtarma yolu iki durumda **aynıdır**. Pencere tur başına en çok **bir** siparişlik (o anda (i) ya da (ii) ile (iii) arasında olan; tur
   sonraki siparişin (i)'inde durur) ve insan eylemi (yeniden açma + ekleme) gerektirir. **Kurtarma yolu:** dalganın serbest bırakma ön kontrolü (§8.6 adım 1) outbound'u
   uygun bulmaz → 422 `WAVE_ORDERS_INELIGIBLE` (`details.orders[]` bu siparişi adlandırır), yazım yok; kullanıcı siparişi dalgadan çıkarır ve
   serbest bırakmayı yeniler; siparişin görevleri 0178 Toplama Görevleri listesinde normal açıktır. Pencereyi tümden kapatmak (iii)'ün 0178
   işleminin 0180 ayar belgesine kapı yazması demektir — 0178 kodunun 0180 belgesine yazması (§2.3 sınırının tersi, yeni XMC kapsamı); bu yüzden
   seçilmedi. (ii)'deki kapı yazımı **üyelikli** siparişte pencereyi (i)–(iii) aralığından (ii)–(iii) aralığına daraltır ve (i) ile (ii)
   arasında yeniden açılan depoda üyelik kapanışını engeller; üyeliksiz siparişte pencere (i)–(iii) kalır (r6, F-4). AC-15 (n5), (n6). **Reddedilen seçenek** — bakım turunda kapalı depodaki Taslak dalgalarda outbound'u artık `Deferred`
   olmayan `Planned` üyelikleri süpürmek: yetim önce doğar ve bir tur (≤ 60 sn) yaşar (bu pencerede yeniden açılan depoda dalga 422 verir) ve
   her tura ayrı bir Taslak tarama sorgusu ekler; kapanış-önce sıralaması aynı sonucu pencere bırakmadan verir. Kapanıştan sonra açılış
   başarısız olursa (bağlantı ya da siparişe özgü hata) sipariş üyeliksiz `Deferred` kalır: kapalı depoda sonraki tur dener; depo bu arada
   yeniden açılırsa sipariş planlayıcıda yeniden uygun görünür (Taslak üyeliğini kaybetmiştir — kapatmanın sonucudur, kullanıcı yeniden ekler).
   0178'de iptal edilmiş outbound'un Taslak'taki `Planned` üyeliği bu kuralın konusu değildir (bekleyen değildir, tura girmez): r1'den beri
   olduğu gibi serbest bırakma ön kontrolü 422 `WAVE_ORDERS_INELIGIBLE` ile söyler, kullanıcı çıkarır (§8.6 adım 1; kapatmaya özgü değil — çıkarım).
3. **Cevap:** 200 + ayar + `pendingRelease { issuedCount, failed[] { outboundId, outboundNumber, code }, remainingCount, stopReason }`.
   **`stopReason` (r5, F-1; r6, F-2 / F-6 b):** `null` (bütçe doldu ya da bekleyen bitti) \| `ConnectionError` \| `RepeatedOrderErrors` (art
   arda hata sınırı) \| `NewStuckLimit` (parti başına yeni takılı sınırı) \| `Reopened` \| `RoundBudgetReached` (yalnız bakım turu: depo bu
   turda `PendingReleaseMaxBatchesPerTour` partiye ulaştı) \| `ListFullSkipLimit` (r7, F-2 — yalnız bakım turu: liste doluyken listeye
   alınamadan atlanan siparişler depo için tur başı tavana ulaştı; ilk turda liste boştur, çünkü yeniden açma onu temizler — §4.5 c) — ilk turun
   neden durduğunu söyler; bakım turlarında aynı değer log'a ve (parti bir şey yazdıysa) `PendingOrdersIssued` olayına yazılır. r5'in
   `StuckListFull` değeri **kaldırıldı**: liste doluyken tur durmaz, yalnız tavanda o depo için durur (r6, F-2; r7, F-2 — "Kısmi başarısızlık"
   (c)). **`remainingCount`
   (r4, F-5)** = ilk turdan sonra bakım turunun açacağı bekleyen sipariş sayısı = `deferredOrderCount − releasingOrderCount` (§4.5) — `Releasing`
   dalganın üyeleri sayılmaz (onları serbest bırakma / kurtarma dalgalı açar); takılı siparişler sayılır. Kapatma bu cevapla tamamdır;
   `remainingCount > 0` ise kalanı bakım turu açar (ekranda yoktur — v1 yalnız API, FU-0180-SETTINGS-UI).
4. **Bakım turu — `ReleasePendingOrdersCommand` (yalnız `WaveMaintenanceHostedService`, her 60 sn, §8.8; HTTP ucu yok):** `WaveManaged = false`
   olan her ayar kaydının deposunda bekleyen sipariş var mı bakar (yalnız `Find`, `limit 1`; `Releasing` üyeleri hariç) **ya da takılı liste
   boş değil mi** bakar (r5, F-2 d — iptal edilen tek takılı sipariş de uzlaştırma için turu tetikler); varsa komutu **parti başına bir kez**
   gönderir — her parti adım 2'nin kurallarıyla ve aynı bütçeyle (200 sipariş / 2.000 görev) koşar, (0) uzlaştırması turun ilk partisindedir —
   ve **aynı turda kalan bitene dek** sürer (r4, "hemen" teyidi); depolar arasında dönüşümlü (her geçişte her depoya bir parti — r6, F-1;
   r7, F-1: bu tarama turun başında bir kez değil **her geçişin başında** yinelenir, yukarıda "Depolar arası sıra"). **Durma koşulları — depo
   başına (r5, F-3 — r4'ün "partide ilerleme yok" koşulu kaldırıldı, çünkü kod değişimi ilerleme sayıldığında sonsuz tekrar mümkündü; r6: bir
   depo durunca turun sırasından çıkar, diğer depolar sürer; r7, F-1: (1) kalıcı değildir, diğerleri o depo için turun sonuna dek kalıcıdır):**
   (1) bu geçişte denenecek iş kalmadı (turda denenmemiş bekleyen yok; takılı yeniden deneme bütçesi dahil — her outbound turda en çok bir kez)
   — sonraki geçişin taraması yeni iş bulursa depo geri döner; (2) depo bu turda `PendingReleaseMaxBatchesPerTour` (öneri 50) partiye ulaştı
   (`RoundBudgetReached`); (3) bağlantı hatası (`ConnectionError`); (4) `RepeatedOrderErrors`, `NewStuckLimit` ("Kısmi başarısızlık"); (5) depo
   yeniden açıldı (`Reopened`; tur sürerken yeniden kapatılırsa sıradaki geçişe katılır); (6) liste doluyken atlanan siparişlerin tur başı
   tavanı (`ListFullSkipLimit`, r7, F-2 — "Kısmi başarısızlık" (c)). (2) ve "her outbound turda en çok bir kez" kuralı birlikte turun sonlu
   olduğunu garanti eder: tur depo başına en çok (bekleyen sayısı) sipariş ve en çok 50 parti dener; depo sayısı sonlu olduğu için tur da
   sonludur (tur sırasında katılan depolar dahil — her depo turda en çok 50 parti). Bu tur beş durumu kapatır: ilk turun bütçesini aşan
   kalan, kısmi hata, yarış (aşağıda), kapatma turunda adım (ii) ile (iii) arasında süreç ölümü ve kapalı depoda geri çekilen sipariş (§8.9).
5. **Tekrar:** aynı `Idempotency-Key` + aynı gövde → 200 replay (ayar ve `Version` değişmez, ilk tur yeniden koşmaz — kalan bakım turunundur);
   farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`.

**Sıralama — C1'e karşı üyelik ve serbest bırakma yazımları (r4, F-1):** ön kontrolde `WaveManaged = true` okuyan bir yazım, yazım anına kadar
C1 ile geçersiz kalabilir (r3'te ayarı C1'den önce okuyup üyeliği ilk turun taramasından sonra yazan `AddWaveOrders` yetim üyelik bırakabiliyor,
ön kontrolü C1'den önce geçen `ReleaseWave` R1'ini C1'den sonra yazıp kapalı depoda dalga serbest bırakabiliyordu). Kural: `AddWaveOrdersCommand`,
"Sonraki dalgaya taşı"nın ekleme adımı (`ResolveWaveExceptionCommand`) ve serbest bırakmanın R1 işlemi (`ReleaseWaveCommand`) **kendi Mongo
işlemlerinde** ayar kaydına koşullu yazım yapar: filtre `WaveManaged = true` **ve** `Version = <ön kontrolde okunan>`, güncelleme yalnız
`GateSeq` +1 (`Version` artmaz — kullanıcının ayar `If-Match`'i bozulmaz, §4.5). C1 aynı kaydı kendi `If-Match`'iyle `Version` koşullu yazar.
İki işlem aynı belgeye yazdığı için Mongo onları sıralar (eşzamanlıysa biri yazım çakışması alır, sürücünün işlem geri çağrısı yeniden dener ve
yeni anlık görüntüde filtreyi yeniden değerlendirir). Eşleşme 0 → işlem geri alınır, ayar yeniden okunur: `WaveManaged = false` → 422
`WAVE_SETTINGS_REQUIRED`, yazım yok; `true` (yalnız eşik değişmiş) → komut bir kez baştan denenir, yine eşleşmezse 409 `WAVE_CONFLICT`.
Sonuç: C1'den sonra hiçbir `Planned` üyelik yazılamaz ve hiçbir dalga `Releasing`'e geçemez; C1'den önce işlenmiş üyelik ilk turun taramasında
(tur C1'den sonra başlar) görülür ve adım 2 (ii) ile kapanır. Yan etki (çıkarım): aynı depodaki eşzamanlı eklemeler ayar belgesinde sıralanır;
planlayıcı işlemleri insan hızında olduğu için kabul edilir. Taslak yaratma, düzenleme, çıkarma, iptal ve kurtarma (`CompleteWaveReleaseCommand`
— R1 zaten yazılı) bu kapıdan geçmez. AC-15 (n).

**Yarış (kapatma sırasında gelen sipariş ya da yazım):**
- (a) Tahsis politikayı C1'den **sonra** okur → görevler tahsiste açılır (0178 normal akışı); 0180 hiçbir şey yapmaz.
- (b) Tahsis politikayı C1'den **önce** okur, outbound'u C1'den sonra `Allocated` + `Deferred` yazar → ilk turun taramasına girerse orada,
  girmezse en geç bir bakım turu sonra (≤ 60 sn + işlem) açılır — r7, F-1: bakım turu koşuyorsa sıradaki geçişte (depo o turda koşul (1) ile
  çıkmış olsa da geçişin taraması onu geri alır; AC-15 p18b), koşmuyorsa ≤ 60 sn sonra başlayan turda. Bu pencerede `deferredOrderCount > 0`
  görünebilir.
- (c) Aynı sipariş için iki tur (ilk tur ile bakım turu, ya da iki bakım örneği) yarışırsa 0178'in `PickIssuance.State = Deferred` + `Version`
  koşullu yazımı tek kazanan bırakır; kaybeden yazım yapmaz ve replay (kazananın görev kimlikleri) alır — ikinci görev doğmaz (AC-15 j; bekçisi
  S-15f; r5, F-5: ikinci set'e karşı ikinci bekçi 0178'in tekil `(TenantId, OutboundId, Seq)` olay index'idir — §8.6, `WarehouseIndexConfiguration.cs:147-148`).
  Takılı liste yazımında iki tur `WaveManaged = false` koşullu işlem içi delta yazımıyla sıralanır, biri diğerinin girdisini silmez (§4.5 b). Aynı siparişin Taslak üyelik kapanışı üyelik `Version`'ıyla tek kez yazılır.
- (d) **Serbest bırakma (r4, F-1 — r3'ün "serbest bırakma ile yarış yoktur" cümlesi fazla güçlüydü ve kaldırıldı):** ön kontrolünü C1'den önce
  geçen bir `ReleaseWave` R1'ini C1'den sonra yazmaya çalışırsa "Sıralama" kuralıyla 422 `WAVE_SETTINGS_REQUIRED` alır — dalga `Draft` kalır,
  üyeleri `Planned`; ilk / bakım turu onları adım 2 (ii) ile kapatıp dalgasız açar. R1'i C1'den **önce** işlenmiş dalga kapatmadan önce serbest
  bırakılmış sayılır: `Releasing` üyeleri taranmaz, serbest bırakma ya da kurtarması onları dalgalı açar (kapalı depoda da `Released` olur);
  `Excluded` / `Removed` kalan üye etkin değildir ve bir sonraki turda bekleyen olarak açılır. Hangi sıra olursa olsun aynı outbound'a iki görev
  seti açılmaz: dalgalı ve dalgasız açılış aynı 0178 koşuluna (`Deferred` + `Version`) yazar. AC-15 (n).
- (e) **Ekleme / taşıma:** ayarı C1'den önce okuyan `AddWaveOrders` ya da taşıma ekleme adımı C1'den sonra işlenirse 422 `WAVE_SETTINGS_REQUIRED`,
  üyelik yazılmaz ("Sıralama"); C1'den önce işlenirse üyelik ilk turda adım 2 (ii) ile kapanır. AC-15 (n).

**Kısmi başarısızlık (r4, F-3 — "zehirli sipariş"):** sipariş başına sonuç.
- `Ineligible` (`OutboundCancelled`, `NotAllocated`, `NotDeferred`, `IssuedToOtherWave` — tarama ile çağrı arasında 0178'de değişti) → sipariş
  atlanır (artık bekleyen değildir), `failed[]`'a kodu yazılır; yazım değildir.
- **Hata sınıflandırması** (`PendingOrderReleaseEngine` içinde; 0178 değişmez): **bağlantı / altyapı hatası** — Mongo bağlantı ya da sunucu
  seçimi hatası, işlem / komut zaman aşımı, yeniden denemeleri tükenmiş geçici işlem hatası (`TransientTransactionError` etiketi), kayıt kodu
  sayacına erişilemez (`WAVE_RECORD_CODE_UNAVAILABLE` sınıfı), iptal edilmiş istek; **siparişe özgü hata** — bunların dışındaki her istisna
  (ör. çözümlenemeyen outbound belgesi, kalıcı plan tutarsızlığı).
- **Bağlantı hatası → tur durur:** sipariş `Deferred` kalır (sonucu bilinmiyorsa sonraki turda replay olur), sonraki siparişler sonraki tura
  kalır (0178 erişilemezken çağrı yığmaz), `failed[]`'a `WAVE_INVENTORY_UNAVAILABLE`. Bakım turunda durma o deponundur (r6, F-1); gerçek
  kesintide her depo turda en çok bir başarısız çağrı yapar. Servis kapanışının iptal belirteci tur iptalidir: tur durur, deneme sayılmaz,
  aşağıdaki sayaç değişmez (r7, F-3).
- **Aynı siparişte art arda bağlantı durması (r6, F-3; r7, F-3 — sayaç başarı şartına bağlandı):** bağlantı sınıfı hata tek bir siparişte
  kalıcıysa (ör. her denemede zaman aşımına uğrayan çok büyük sipariş) r6'da o sipariş her turda ilk denenip turu durduruyordu. r7 kuralı
  üç parçadır. **(a) Şüpheli sıralaması:** bir turu bağlantı sınıfı hatayla durduran outbound süreç içi bellekte **şüpheli** işaretlenir ve
  sonraki turlarda **turun en sonunda** — takılı olmayanlardan ve takılı yeniden denemelerden **sonra** (r8, r7 incelemesi F-2) — denenir
  (şüpheliler arasında son denemesi en eski olan önce — Akış adım 2); böylece sağlam siparişler ve takılı liste ondan önce denenir, şüpheli
  turu durdursa da ikisini aç bırakamaz (AC-15 p16e). **Seçim (r8):** iki yol vardı — şüpheliyi takılı yeniden denemelerin arkasına almak ya
  da başarılı çağrı yokken şüphelinin durdurduğu turda takılı yeniden denemeleri yine koşturmak. İkincisi "bağlantı hatası turu durdurur, 0178
  erişilemezken çağrı yığmaz" kuralına istisna açar ve gerçek kesintide depo başına tur başına 50'ye kadar ek başarısız çağrı ekler; birincisi
  yalnız sıradaki yeri değiştirir. Daha dar olan birincisi seçildi. **(b) Sayaç yalnız başarı kanıtıyla artar:** outbound X turu bağlantı sınıfı
  hatayla durdurduğunda X'in sayacı **yalnız aynı turda X'ten önce en az bir 0178 çağrısı sonuç döndüyse** (`Issued`, `Ineligible` ya da
  replay — 0178 o turda erişilebilirdi) +1 artar; başarılı çağrı yoksa (X turun ilk çağrısıydı ya da önceki çağrılar da bağlantı hatası aldı)
  sayaç **değişmez** — hata kesintiden ayırt edilemez. X'in turun ilk denediği sipariş olup olmaması artık koşul değildir (r6 incelemesi F-3
  b): turu durduran her outbound'un sayacı bu kuralla işler; turda denenmeyen outbound'un sayacı değişmez. Turu durduran her bağlantı sınıfı
  hata bir yapılandırılmış `Warning` log'u yazar (`warehouseId`, `outboundId`, `exceptionType`, `priorSuccessInTour`, `connectionStops` =
  sayacın yeni değeri; mesaj gövdesi ve kişisel veri yok) — depo başına turda en çok bir satır. Sayaç X bir sonuç alınca
  (`Issued` / `Ineligible`), bekleyen olmaktan çıkınca ya da süreç yeniden başlayınca sıfırlanır (şüpheli işareti de kalkar); r6'nın "tur başka
  siparişte durursa ya da bağlantı hatasız biterse sıfırlanır" kuralı **kaldırıldı** (liste doluyken sayacı sıfırlayıp siparişi her 3 turun
  2'sinde depoyu durdurur hale getiriyordu — r6 incelemesi F-3 c). Sayaç `Warehouse:Wave:PendingReleaseConnectionStopsBeforeStuck`'a (öneri
  **3**) ulaşınca X siparişe özgü sayılır: takılı listeye girer (`Code` = kategori `RepeatedConnectionError`, §4.5 e), `failed[]`'ta
  `WAVE_PENDING_ORDER_ERROR`, tur **sürer**. **(c) Eşikten sonra ve liste doluyken:** eşiğe ulaşmış X'in sonraki bağlantı sınıfı hatası, turda
  önceki bir başarılı çağrı varsa siparişe özgü hata gibi işlenir (atlanır, tur sürer); başarılı çağrı yoksa tur yine durur (kesinti olabilir).
  Takılı listedeki siparişin yeniden denemesi de aynı kuralla işler (önceki başarılı çağrı varsa atlanır, liste değişmez, tur sürer — takılı
  bir siparişin her tur sonunda turu durdurup göstergeyi büyütmesi önlenir). Eşikte liste doluysa X listeye **alınamaz**: liste dolu atlaması
  gibi işlenir — `failed[]`'ta `WAVE_PENDING_ORDER_ERROR`, parti özetine ve depo tavanına (`PendingReleaseListFullSkipsPerTour`, aşağıda (c))
  sayılır, tur sürer; sayaç eşikte **kalır** (sıfırlanmaz), böylece sonraki turlarda da atlanır ve depoyu durdurmaz; liste yer açınca X bir
  sonraki hatasında listeye girer. Sayaç ve şüpheli işareti `PendingOrderReleaseEngine`'in süreç içi belleğindedir (anahtar kiracı + şirket +
  depo + outbound). **Kapsam — ne zaman yanlışlıkla takılır (r6 incelemesi F-3 a; r6'nın "gerçek kesintide sipariş yanlışlıkla takılmaz"
  cümlesi daraltıldı):** **tam** kesintide (0178 / Mongo hiçbir çağrıya cevap vermiyor) turda başarılı çağrı olmaz, sayaç artmaz, sipariş
  takılmaz. **Kısmi** kesintide (yük altında 0178 işlem zaman aşımı, yeniden denemesi tükenmiş geçici işlem hatası — bazı çağrılar başarılı,
  bazıları değil) sağlam bir sipariş art arda 3 denemesinin üçünde de bağlantı sınıfı hata alırsa **takılabilir**; şüpheli sıralaması onu tur
  sonuna aldığı için bu, kesintinin üç ardışık turda tam o siparişin denemesine denk gelmesini ister — olasılığı düşürür, sıfırlamaz. Sonuç
  kalıcı kayıp değildir: sipariş `Deferred` kalır, listede görünür, takılı yeniden deneme bütçesiyle her turda yeniden denenir ve ilk başarıda
  listeden çıkar; maliyeti takılmanın `Error` log'u, `PendingOrdersStuckChanged` olayı ve merkezi kaydıdır (giriş ve çıkış birer kez).
  **Metrik ve alarm:** gösterge `inventory_wave_pending_release_connection_stopped_tours` (etiket `warehouse_id`; değer = depoda art arda
  bağlantı hatasıyla duran tur sayısı, bağlantı durması olmadan biten turda 0); sipariş takılı listeye alındığında bir kez `Error` log'u.
  Alarm önerisi: gösterge ≥ 5 (kesin eşik işletim kararıdır). **Kalıcı bağlantı sınıfı hatalı şüpheli, turda ondan önce sonuç dönen 0178
  çağrısı yokken (r8, r7 incelemesi F-2 — r7'nin "depoda başka bekleyen yok" daraltması eksikti):** bu, depoda takılı olmayan başka bekleyen
  yokken **ve** takılı yeniden denemelerin hiçbiri sonuç döndürmezken (liste boş ya da hepsi yine hatalı) olur; o zaman sayaç artmaz ve sipariş
  sayaçla takılmaz — kesintiden ayırt edilemediği için bilinçli; göstergenin alarmıyla görünür. Takılı liste bu durumda **aç kalmaz**:
  şüpheli turun en sonunda denendiği için takılı yeniden denemeler her turda ondan önce koşar; biri sonuç döndürürse şüphelinin sayacı o turda
  artar. AC-15 (p16), (p16e).
- **Siparişe özgü hata → sipariş atlanır, tur sürer:** sipariş `Deferred` kalır, `failed[]`'a `WAVE_PENDING_ORDER_ERROR` (§8.17); ilk hatada
  `PendingReleaseStuck[]`'a girer (`Code` = kararlı kategori — `OutboundUnreadable` \| `PlanInconsistent` \| `Unexpected`, r5, F-2 e; istisna
  türünün adı ve mesaj gövdesi listeye ve API'ye yazılmaz — iç ayrıntı ve kişisel veri yok; `FirstFailedAtUtc`). Takılı sipariş sonraki turlarda
  sıranın **sonunda** (yalnız şüphelilerden önce — r8), yeniden deneme bütçesi içinde (adım 2) yeniden denenir; açılır ya da artık bekleyen değilse listeden çıkar; kategorisi
  değişirse liste **değişmez** (r5 — kayıttaki `Code` ilk hatanınkidir, yeni kategori log ve metriğe gider).
- **Yaygın arıza — tur durur ama tetikleyenler takılır (r5, F-1):** (a) bir partide art arda `Warehouse:Wave:PendingReleaseMaxConsecutiveErrors`
  (öneri 3) siparişe özgü hata → tur durur (`stopReason = RepeatedOrderErrors`); (b) bir partide listeye giren yeni takılı sipariş sayısı
  `Warehouse:Wave:PendingReleaseMaxNewStuckPerBatch`'e (öneri **10**) ulaşırsa → tur durur (`NewStuckLimit`; art arda olmayan ama yoğun hatayı
  yakalar). Her iki durumda **tetikleyen siparişler takılı listeye yazılır** (partinin delta yazımıyla, aynı işlemde; r4'te yazılmıyordu ve en
  eski üç kalıcı hatalı sipariş her turda yine ilk denenip depodaki diğer siparişleri süresiz bekletiyordu — F-1); sonraki tur onları sıranın
  sonuna alır, önce takılı olmayanları açar. Liste büyümesi böylece parti başına en çok 10 siparişle sınırlıdır (yaygın arızada, ör. bağlantı
  sınıfına girmeyen ama bütün siparişlerde görülen toplu hata, her tur en çok bir parti yeni takılı yazar ve durur). (c) **Liste dolu (r6,
  F-2 — r5'in "tur durur" kuralı kaldırıldı; tek bir kalıcı hatalı sipariş her turda ilk denenip depoyu kilitliyordu):** liste
  `PendingReleaseStuckMax` (öneri 1.000, §4.5 f) doluyken yeni siparişe özgü hata alan sipariş listeye **alınmadan atlanır** — `failed[]`'ta
  `WAVE_PENDING_ORDER_ERROR` ve sayaç (her siparişe özgü hata gibi) — ve **tur sürer**; liste doluyken (a) ve (b) işlemez (listeye giremeyen
  hatalar sayılsaydı aynı en eski hatalı siparişler her turda yine turu durdururdu). **Tavan (r7, F-2 — r6 incelemesi F-2: arıza sistemikse
  her tur depo başına bekleyenlerin hepsini, en çok 50 × 200 = 10.000 başarısız 0178 çağrısı ve 10.000 log satırıyla deniyordu):** bir depoda
  bir turda liste doluyken atlanan sipariş sayısı `Warehouse:Wave:PendingReleaseListFullSkipsPerTour`'a (öneri **200**) ulaşınca o depo için
  tur durur (`stopReason = ListFullSkipLimit`, adım 4 koşulu (6)); diğer depolar sürer. **Değer seçimi — 200 sipariş (4 parti değil):** bir
  partinin sipariş bütçesiyle (`PendingReleaseBatch`, sipariş ekleme ve öneri listesinin üst sınırı, §8.2) aynı büyüklüktür, yeni bir
  büyüklük sınıfı açmaz; depo başına tur başına en kötü yük 200 başarısız 0178 çağrısı ve en çok bir parti süresidir (≤ 15 s, AC-27 d) —
  r6'nın en kötüsünün 1/50'si; 4 parti (800) yükü dört katına çıkarır, kazancı yalnız sağlam siparişlerin tavandan önce biraz daha ileri
  gitmesidir ve imleç bunu zaten sağlar (aşağıda). Tavan yalnız liste doluyken atlanan siparişleri sayar (açılanları, `Ineligible`'ları ve
  takılı yeniden denemeleri saymaz); sağlam siparişler tavana kadar her partide açılmayı sürdürür. **İmleç — kaldığı yerden sürer, kilitlenme
  yok:** liste doluyken depo, turda son denediği takılı olmayan siparişin (`CreatedAt`, `OutboundId`) konumunu süreç içi bellekte tutar (depo
  başına); sonraki tur takılı olmayanları bu konumdan **sonrakilerle** başlatır, sona varınca baştan (konuma kadar olanlarla) sürer (dairesel;
  aynı turda bir outbound yine en çok bir kez), sonra takılı olanlar, en sonda şüpheliler (r8). Böylece aynı en eski hatalı siparişler her turun tavanını doldurup
  arkadakileri süresiz bekletemez: liste doluyken her takılı olmayan bekleyen sipariş en geç ⌈(takılı olmayan bekleyen sayısı) / 200⌉ turda bir
  denenir (çıkarım; tavana yalnız atlananlar sayıldığı için daha erken de olabilir). Liste dolu değilken imleç kullanılmaz ve silinir (hatalı
  sipariş zaten listeye girip sıranın sonuna gider); süreç yeniden başlarsa imleç baştan başlar (en kötü bir tur tekrar). **Log:** liste
  doluyken atlanan siparişler için sipariş başına `Warning` log'u **yazılmaz**; **parti başına bir özet** `Warning` log'u yazılır (`warehouseId`,
  atlanan sayısı, kategori başına sayı, ilk ve son atlanan `outboundId`; mesaj gövdesi ve kişisel veri yok) — özet satırı yalnız atlama içeren
  partide yazılır; tavanla birlikte depo başına tur başına atlama en çok 200, özet satırı en çok atlama içeren parti sayısı kadardır (sistemik
  arızada tipik olarak 1). Sayaç (`inventory_wave_pending_release_order_errors_total`) sipariş başına artmayı sürdürür
  (metrik ucuzdur, log seli değildir). `Error` log'u duruma **girişte bir kez** yazılır (liste dolu iken ilk atlama), durumdan **çıkınca**
  (liste `PendingReleaseStuckMax`'ın altına inince) bir `Info` log'u; durum bayrağı süreç içi bellektedir (depo başına; yeniden başlamada giriş
  log'u en çok bir kez daha yazılır) (r6, F-6 c). Bu noktada arıza sistemiktir (gösterge alarmı çoktan çalmıştır, aşağıda). **Kurtarma yolu:**
  nedeni gidermek (liste takılı siparişler açıldıkça ya da tur başı uzlaştırmayla boşalır); ya da depoyu yeniden açıp kapatmak — yeniden açma
  listeyi aynı işlemde temizler (§4.5 c), yeniden kapatma bekleyenleri yeni ilk turla açar (neden sürüyorsa liste yeniden dolar). Art arda
  bağlantı durması eşiğine ulaşıp listeye alınamayan sipariş de bu tavana sayılır (yukarıda (c)). AC-15 (p8), (p19).
  (a) ve (b) yalnız **takılı olmayan** siparişleri sayar: takılı yeniden denemelerin hataları bilinen hatalardır, yaygın arıza işareti değildir
  ve sayılsaydı ilk üç başarısız yeniden deneme bütçenin kalanını kullanılmaz kılardı; onların tur başına yükünü yeniden deneme bütçesi (adım 2)
  sınırlar.
- **Art arda hata sayacı, log, metrik:** sipariş başına art arda hata sayısı ve son deneme zamanı `PendingOrderReleaseEngine`'in süreç içi
  belleğindedir (anahtar kiracı + şirket + depo + outbound; başarı, listeden çıkış ya da süreç yeniden başlaması sıfırlar — her turda değiştiği
  için kalıcı yazılmaz; kalıcı görünürlük `stuckOrders[]`'tır, §4.5). Her siparişe özgü hata yapılandırılmış `Warning` log'u (`warehouseId`,
  `outboundId`, `code` (kategori), `exceptionType` (yalnız log), `consecutiveFailures`; mesaj gövdesi ve kişisel veri yok) ve OTel metriği yazar
  — liste doluyken atlananlar hariç: onlar için parti başına bir özet `Warning` log'u (r7, F-2 — yukarıda (c)).
  **Metrik adları (r5, F-6 f — depo deseni `inventory_` öneki ve sayaçta `_total`, `INV/Application/Common/RefusalAuditMetrics.cs:13`
  `inventory_refusal_audit_failed_total`; ölçüm `1aaa0db22`):** sayaç `inventory_wave_pending_release_order_errors_total` (etiket `code` =
  kategori — düşük kardinalite); gösterge `inventory_wave_pending_release_stuck_orders` (etiket `warehouse_id`; değer = ayar belgesindeki takılı
  liste uzunluğu, her parti yazımından ve yeniden açmadan sonra güncellenir — yeniden açmada 0). Meter adı öneri `Diten.InventoryService.WavePlanning`
  (depodaki `Diten.InventoryService.Audit` emsali, aynı dosya `:11`). Sayaç `Warehouse:Wave:PendingReleaseAlertAfter` (öneri 5) eşiğine ulaştığında
  bir kez `Error` log'u. **Alarm notu (r5, F-6 f):** süreç içi sayaç yeniden başlamada sıfırlandığı için asıl alarm **göstergeye** dayanır —
  öneri: "`inventory_wave_pending_release_stuck_orders` > 0, 15 dakikadan uzun" (kesin eşik ve alarm kuralı işletim kararıdır, paket kuralı
  değildir). GET ayar cevabı takılan siparişleri `stuckOrders[]` ile gösterir.
- **Olay ve merkezi kayıt yalnız bir şey değiştiğinde:** bir bakım partisi hiçbir sipariş açmadı, hiçbir üyelik kapatmadı ve takılı sipariş
  listesi değişmediyse (ör. takılı sipariş aynı ya da farklı kategoriyle yine başarısız — r5: kategori değişimi değişiklik değildir) komut **yazmaz**: ne `PendingOrdersIssued` / `PendingOrdersStuckChanged`
  olayı ne merkezi kayıt doğar — yalnız log + metrik. Bir şey değiştiyse parti başına bir merkezi kayıt (§21).
- **Ayar geri alınmaz** (kapatma işlenmiştir; reddedilmez). Ayrı istisna türü yok (`WaveException` dalgaya bağlıdır); kalıcı hata `stuckOrders[]`,
  `deferredOrderCount` ve ayar akışındaki olaylardan okunur.

**Denetim:** C1 + ilk tur = `UpdateWaveWarehouseSettingsCommand`'ın **bir** merkezi kaydı — ilk turun 0178 port yazımları, üyelik kapanışları
ve takılı liste yazımı ona enlist olur (§8.6 deseni); bakım turu = `ReleasePendingOrdersCommand`'ın parti başına kaydı (yalnız parti bir şey
değiştirdiyse; 0178 yazımları ona enlist). Yerel iz: ayar akışı (`SettingsUpdated` — gerekçe; `PendingOrdersIssued` — sipariş / görev numaraları,
kapanan üyelikler, atlananlar, C1 olayına atıf; `PendingOrdersStuckChanged` — takılan / çözülen siparişler), dalga akışı (`OrdersRemoved`,
`system`, `CloseReason`), 0178 outbound olayı `PickTasksIssued` (`Data.reason = WaveManagementDisabled`) + görev başına `Created`. Gerekçe kapatma
isteğinindir; bakım turu aynı gerekçeyi C1 olayına atıfla taşır. §21.

**Kapalı depoda dalga işlemleri:** yaratma, sipariş ekleme, serbest bırakma ve "Sonraki dalgaya taşı" 422 `WAVE_SETTINGS_REQUIRED` (§8.2 kural 1);
Taslak düzenleme / iptal / çıkarma, geri çekme, istisna "Siparişi çıkar" / "Yok say", okumalar ve uzlaştırma çalışır. Geri çekilen sipariş
bakım turunda dalgasız görev alır (§8.9). Yeniden açma (`false` → `true`) her zaman serbesttir; açıldıktan sonra yeni tahsisler yine ertelenir; takılı liste aynı işlemde temizlenir
(r5, F-2 — §4.5 c; AC-15 p11).

## 9. Layout & Shell Contract
- `shell: tenant` → `Views/Warehouse/{Waves,WavePlanner,WaveExceptions,WaveKpis}/*.cshtml` dosyalarının **her birinde**
  `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır (ayrıntı sayfası `Details.cshtml` dahil); `_ViewStart.cshtml` varsayılanı değişmez.
- View klasörü `Views/Warehouse/{Module}/` (MOD-0178 / MOD-0181 emsali); rota `/Warehouse/{Module}`.
- Yetkisiz kullanıcıya sayfa iskeleti çizilmez, yönlendirme yapılmaz; sol menü ve üst çubuk kalır, içerikte tek açıklama ("Yöneticinizden yetki
  isteyin") (UAS-001; BRIEF `:72-73`).

## 10. Backend File Convention
Golden Reference Slim birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`;
pack `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`):

```text
Features/Waves/
├── Commands/        CreateWaveCommand.cs, UpdateWaveCommand.cs, AddWaveOrdersCommand.cs, RemoveWaveOrdersCommand.cs,
│                    CancelWaveCommand.cs, ReleaseWaveCommand.cs, CompleteWaveReleaseCommand.cs, RecallWaveRemainingCommand.cs,
│                    ReconcileWaveCommand.cs, UpdateWaveWarehouseSettingsCommand.cs, ReleasePendingOrdersCommand.cs (r3)
├── Queries/         GetWaveListQuery.cs, GetWaveByIdQuery.cs, GetWaveDashboardQuery.cs, GetWaveOrderListQuery.cs, GetWaveTaskListQuery.cs,
│                    GetWaveLoadQuery.cs, GetWaveEventListQuery.cs, GetWaveCandidateListQuery.cs, GetWaveSuggestionListQuery.cs,
│                    GetWaveWarehouseSettingsQuery.cs
├── Handlers/
│   ├── CommandHandlers/   CreateWaveHandler.cs … UpdateWaveWarehouseSettingsHandler.cs, ReleasePendingOrdersHandler.cs   (sealed class, suffix YOK)
│   └── QueryHandlers/     GetWaveListHandler.cs … GetWaveWarehouseSettingsHandler.cs
├── Validators/      CreateWaveValidator.cs, UpdateWaveValidator.cs, AddWaveOrdersValidator.cs, ReleaseWaveValidator.cs,
│                    CancelWaveValidator.cs, RecallWaveRemainingValidator.cs, UpdateWaveWarehouseSettingsValidator.cs
└── WaveModels.cs    (tek dosyada bütün DTO'lar)
Features/WaveExceptions/   aynı yapı: ResolveWaveExceptionCommand; GetWaveExceptionList/ById sorguları; WaveExceptionModels.cs
Features/WaveKpis/         aynı yapı: GetWaveKpiSeriesQuery, ExportWaveKpiCsvQuery; WaveKpiModels.cs
Features/WavePlanning/     ortak: WaveContract.cs, WaveErrorCodes.cs, WaveCentralAudit.cs, Ports/, Services/
                           (WaveRoutePlanner, WaveReleaseEngine, WaveReconciler, WaveExceptionDetector, WaveKpiCalculator,
                            PendingOrderReleaseEngine — r3, §8.19; r4: hata sınıflandırması, takılı liste, bütçe)
```

**Golden Slim sapması G-0180-01 (adıyla):** DEV-0000 `Delete{Module}Command` / `BulkDelete{Module}Command` ve PUT / DELETE uçları tanımlar
(`execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`). 0180'de **silme yoktur** (§2.3 kural 6): `Delete` /
`BulkDelete` komutu, `delete` / `bulk-delete` izni ve DataTable "Sil" / toplu sil eylemi yoktur; Taslak dalganın tek "kaldırma" yolu
`CancelWaveCommand`'dır (durum, silme değil). Güncelleme `POST …/update` komut ucudur (InventoryService PUT bekçisi — §8.1). DEV-0000'ın
`data_mode: client`'ı 0180'de `server`'dır (dalga listesi sınırsız büyür). Klasör, adlandırma ve partial seti Golden Slim ile aynı kalır.
DataTable satır eylemi "Sil" yerine "İptal"dir (yalnız Taslak); `verify_datatable_page.py` sapma verirse dokunma protokolüyle sahibine sorulur.

Kurallar: Command / Query `sealed record`; Handler / Validator adında `Command` / `Query` soneki yok; bir dosyada tek `public` tip;
`Handlers/CommandHandlers` ve `Handlers/QueryHandlers` ayrımı zorunlu (`.antigravity/rules/module-pack-standard.md` §4).

## 11. Frontend File Contract
Golden Slim seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`):

| Klasör | Dosyalar |
|---|---|
| `Views/Warehouse/Waves/` | `Index.cshtml` (KPI kartları + liste), `_Filter.cshtml` (depo zorunlu, durum, tarih, kesim aralığı, tür, "riskli olanlar"), `_DataTable.cshtml` (`data-dt-standard="v2"`, `data-dt-data-mode="server"`, skeleton), `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (8 alanlı form + "Siparişleri öner"), `_DetailsQuickView.cshtml`, `WavesIndex.cs`; ek sayfa `Details.cshtml` (liste ekranı değil) |
| `Views/Warehouse/WavePlanner/` | `Index.cshtml` (pano; DataTable değil), `_IndexL10n.cshtml`, `WavePlannerIndex.cs` |
| `Views/Warehouse/WaveExceptions/` | `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (**yalnız eylem formu**: çıkar / taşı + hedef / yok say + gerekçe; yaratma yok), `_DetailsQuickView.cshtml`, `WaveExceptionsIndex.cs` |
| `Views/Warehouse/WaveKpis/` | `Index.cshtml` (grafikler + tablo görünümü + CSV düğmesi), `_Filter.cshtml`, `_IndexL10n.cshtml`, `WaveKpisIndex.cs` |
| `wwwroot/assets/js/Warehouse/{Waves,WavePlanner,WaveExceptions,WaveKpis}/` | `index.js`, `index.l10n.js`; `Waves/details.js`; `WavePlanner/planner.js` (sürükle-bırak + klavye) |
| `Resources/Views/Warehouse/{Module}/` | `{Module}Index.{en,tr,fr,es,zh,ar,ru}.resx` (+ `WavesDetails`) |
| `frontend/Diten.Web/Controllers/` | `WavesController.cs` (`[Route("Warehouse/Waves")]`, `Details/{waveId}` eylemi), `WavePlannerController.cs`, `WaveExceptionsController.cs`, `WaveKpisController.cs` — same-origin MVC proxy → Gateway `/api/warehouse/waves…`; her yazma `[ValidateAntiForgeryToken]`; her proxy isteğine sayfanın `Accept-Language`'ı (CSV başlığı ve hata metinleri); `If-Match` ve `Idempotency-Key` iletilir |
| `frontend/Diten.Web/Resources/` (ortak) | `SharedResource.{7}.resx` nav anahtarları, `PageHelpResource.{7}.resx` sayfa yardımı (§25) — yalnız eklemeli |

**Planlayıcı erişilebilirliği (BRIEF `:55`):** sol listede her satırın "Ekle" düğmesi ve hedef dalga seçimi vardır; klavyeyle: satıra Tab, `Enter`
= seçili dalgaya ekle, `Shift+Enter` = hedef seçici; dalga kartında siparişe Tab + `Delete` tuşu = çıkar (Taslak); sürükle-bırak yalnız fare / dokunma
kolaylığıdır, her işlem düğmeyle de yapılır; işlem sonucu `aria-live` bölgesinde okunur. Kısayol katmanı UXH-001'e uyar (yan panelde Enter).

Compact'a özgü `Create/Edit/Details.cshtml` (liste ekranı formu olarak) ve `_Form.cshtml` **yok** (`Waves/Details.cshtml` bir ayrıntı sayfasıdır,
form değildir — MOD-0181 `CountSheet.cshtml` emsali). Liste ekranına dokunan görev `verify_datatable_page.py --area Warehouse --module {Module}
--format gaps` koşar ve sapmayı sahibine sorar.

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| WarehouseId | Evet | uuid; LOCATION `Warehouse`, `Active`, şirket kapsamı; ayar `WaveManaged = true` + `TimeZoneId` | — | LOCATION + ayar okuması |
| WaveType | Evet | `Cargo` \| `Customer` \| `Region` \| `Manual` | — | — |
| CutoffLocal | Evet | `yyyy-MM-ddTHH:mm`; depo saat diliminde var olan an; ≥ şimdi − 1 saat (§8.4) | — | saat dilimi dönüşümü |
| ShipToFilter | Hayır | trim, 1..100 | — | — |
| MaxOrders | Hayır | tam sayı 1..1000 | — | — |
| MaxLines | Hayır | tam sayı 1..20000 | — | — |
| ResponsibleUserId | Hayır | uuid; kullanıcı dizininde, şirkette etkin | — | dizin okuması |
| Note | Hayır | trim, ≤ 500 | — | — |
| outboundIds (ekle / çıkar) | Evet | 1..200, uuid, tekrarsız; her biri §8.2 uygun | tekil kısmi `(TenantId, LegalEntityId, OutboundId)` `Active` | 0178 outbound `Find` |
| confirmLimitOverride | Hayır (varsayılan false) | bool; sınır aşımında `true` zorunlu | — | §8.5 |
| Gerekçe (iptal / geri çekme / istisna eylemi / ayar) | Evet | trim, 10..500 | — | — |
| Taşıma hedefi | Hayır | uuid; aynı depo, `Draft`; yoksa varsayılan (D-13) | — | dalga okuması |
| TimeZoneId (ayar) | `WaveManaged = true` ise evet | IANA kimliği, çalışma zamanının saat dilimi veritabanında çözülür | — | — |
| RiskMinutesBeforeCutoff / RiskProgressPercent / PendingTaskMinutes (ayar) | Hayır | 1..1440 / 1..100 / 1..1440 | tekil depo kaydı | — |
| WaveManaged `true` → `false` (ayar) | — | her zaman kabul (Y-1 = A, r3); gerekçe 10..500 zorunlu; yazımdan sonra bekleyen siparişlerin görevleri açılır (§8.19) — ön koşul ya da red yok | — | bekleyen sipariş taraması 0178 outbound `Find` (`Status = Allocated` + `PickIssuance.State = Deferred`, §4.5) |
| KPI aralığı | Evet | `from ≤ to`, ≤ 92 gün, depo zorunlu | — | — |
| Idempotency-Key | Evet (her yazma) | 8–100 karakter (MOD-0178 emsali) | tekil kısmi index | anahtar araması |
| If-Match | Evet (yaratma dışı her yazma) | `W/"<version>"` | — | — |
| TenantId / LegalEntityId gövdede | Hayır | gelirse 400 | — | — |
| `GateSeq` / `PendingReleaseStuck` (ayar gövdesinde; r4) | Hayır | sistem alanı — gelirse 400 | — | — |

## 13. Failure Path to Verify
- **Duplicate (aynı Idempotency-Key, aynı gövde)** — Expected: 200 replay, yeni kayıt / olay / numara / görev yok.
- **Aynı anahtar, farklı gövde** — Expected: 400 `IDEMPOTENCY_KEY_REUSED`, yazım yok.
- **Missing alan (depo, tür, kesim)** — Expected: 400 validator mesajı; saat dilimi yoksa 422 `WAVE_SETTINGS_REQUIRED`.
- **Unauthorized actor** — Expected: 403; UI eylem gizli; sayfa izni yoksa iskelet yok (UAS-001).
- **Concurrency (eski If-Match)** — Expected: 409 `WAVE_CONFLICT`; "Kayıt değişti, yenileyin."; sessiz üzerine yazma yok (D-14).
- **Başka kiracı / şirket kaydı** — Expected: 404 (403 değil).
- **Sipariş başka dalgada / eşzamanlı iki ekleme** — Expected: 409 `WAVE_ORDER_IN_OTHER_WAVE`; tam biri başarılı.
- **Sınır aşımı serbest bırakma onaysız** — Expected: 409 `WAVE_LIMIT_CONFIRMATION_REQUIRED`; onayla 200 (D-7).
- **Ön kontrolden sonra outbound 0178'de iptal** — Expected: dalga diğer siparişlerle `Released`; iptal edilen `Excluded` + `OrderCancelledInWave`.
- **0178 portu zaman aşımı** — Expected: 503 `WAVE_RELEASE_OUTCOME_UNKNOWN`, dalga `Releasing`; bakım tamamlar; görev sayısı plan sayısına eşit (ikinci görev yok).
- **Süreç R1'den sonra ölür** — Expected: kilit dolunca bakım `CompleteWaveReleaseCommand` aynı `PickPlan`'la tamamlar.
- **Başlamış siparişi geri çekme / çıkarma** — Expected: sipariş dalgada kalır (`Started`); istisna eyleminde 409 `WAVE_ORDER_STARTED`.
- **Serbest bırakılmış dalgayı iptal** — Expected: 409 `WAVE_INVALID_STATE` (D-9).
- **Kesim geçti** — Expected: dalga kapanmaz; `CutoffMissed` istisnası tek kez (D-9).
- **Lot geri çağırmada** — Expected: `LotUnderRecall` istisnası; sipariş dalgadan **otomatik çıkmaz** (D-10).
- **0178 / LOCATION erişilemez** — Expected: 503 `WAVE_INVENTORY_UNAVAILABLE`; yazım yok.
- **Dalga yönetimi kapalı depo** — Expected: 0178 outbound'u bugünkü gibi görevleriyle doğar; planlayıcıda görünmez.
- **Ertelenmiş sipariş varken dalga yönetimini kapatma (Y-1 = A, r3)** — Expected: 200 (red yok); ayar `false`; bekleyen siparişlerin görevleri
  aynı istekte dalgasız açılır, Taslak üyelikleri `Removed`; dalgadaki (`Released`) siparişler, iptal edilmiş outbound ve başka depo değişmez;
  merkezi kayıt 1 (AC-15 g, h).
- **Kapatma sırasında 0178 portu zaman aşımı** — Expected: 200, ayar `false` kalır, `pendingRelease.failed[]` + `remainingCount`; bakım turu
  kalanı açar; ikinci görev yok (AC-15 j).
- **Kapatmadan önce politikayı okuyan tahsis (yarış)** — Expected: outbound bir bakım turu içinde dalgasız görev alır (AC-15 i).
- **Kapatma turunda üyelik kapanışından sonra, görev açılışından önce süreç ölümü (r4, F-1)** — Expected: sipariş `Deferred` ve üyeliksiz;
  sonraki bakım turu dalgasız açar; Taslak dalgada yetim `Planned` üyelik yok (AC-15 n).
- **Ayarı kapatmadan önce okuyan sipariş ekleme / serbest bırakma, yazımı kapatmadan sonra (r4, F-1)** — Expected: 422 `WAVE_SETTINGS_REQUIRED`,
  üyelik / R1 yazılmaz, görev açılmaz (AC-15 n).
- **Kapalı depoda zehirli sipariş (r4, F-3)** — Expected: siparişe özgü hata siparişi atlatır, diğerleri açılır; sipariş GET ayar cevabının
  `stuckOrders[]`'ında; aynı hata sonraki turda yine olursa olay ve merkezi kayıt yok (yalnız log + metrik); bağlantı hatasında tur durur (AC-15 p).
- **Kapalı depoda en eski siparişlerin hepsi kalıcı hatalı (yaygın arıza, r5, F-1)** — Expected: tur sınırda durur ama tetikleyen siparişler
  `stuckOrders[]`'a yazılır (`failed[]` kodu `WAVE_PENDING_ORDER_ERROR`, `stopReason = RepeatedOrderErrors`); sonraki tur önce yeni siparişleri
  açar; listeye parti başına en çok 10 yeni sipariş girer, liste en çok 1.000; liste doluyken yeni hatalı sipariş listeye alınmadan atlanır ve
  tur sürer, `Error` log'u duruma girişte bir kez (r6, F-2; AC-15 p6, p7, p8); r7 (F-2): liste doluyken atlananlar depo başına tur başına
  en çok 200 — tavanda o depo için tur durur (`ListFullSkipLimit`), sonraki tur imleçten sürer (kilitlenme yok); `Warning` log'u parti başına
  özet (AC-15 p19).
- **Tek siparişte kalıcı bağlantı sınıfı hata (r6, F-3; r7, F-3; r8, r7 incelemesi F-2)** — Expected: turu bağlantı hatasıyla durduran sipariş
  şüpheli olur ve sonraki turlarda turun en sonunda — takılı olmayanlardan ve takılı yeniden denemelerden sonra — denenir (diğer siparişler
  açılır, takılı liste her turda yeniden denenir — AC-15 p16e); sayacı yalnız aynı turda önce başarılı bir 0178 çağrısı varsa artar,
  üçüncüsünde takılı listeye girer (`RepeatedConnectionError`); hiçbir çağrı başarılı değilken (kesinti) sipariş takılmaz; eşikte liste doluysa
  atlanır ve tavana sayılır, sayaç sıfırlanmaz; art arda bağlantı durması göstergesi ve alarmı (AC-15 p16).
- **Birden çok kalabalık kapalı depo (r6, F-1; r7, F-1)** — Expected: her depo her turda en az bir parti alır; parti sınırı depo başınadır
  (AC-15 p17); tur sürerken kapatılan depo sıradaki geçişe katılır, turun sonunu beklemez (AC-15 p18).
- **Uzun açılış turu sırasında dalga uzlaştırması (r7, F-5)** — Expected: uzlaştırma kendi döngüsünde tur atar, açılış turunu beklemez (AC-15 r).
- **Takılı liste yaşam döngüsü (r5, F-2)** — Expected: liste yazımı ayar `Version`'ını değiştirmez (eski `If-Match` geçerli); eşzamanlı iki tur
  birbirinin girdisini silmez; depo yeniden açılınca liste boşalır; 0178'de iptal edilen takılı sipariş sonraki turda listeden çıkar; API'de
  istisna türü adı yok, kategori var (AC-15 p1, p9…p12).
- **Turda sonsuz tekrar (r5, F-3)** — Expected: her outbound turda en çok bir kez denenir; tur depo başına en çok 50 parti koşar
  (`stopReason = RoundBudgetReached`) (AC-15 p13, p14, p15).
- **Kapatma turunda (i) ile (ii) arasında depo yeniden açılır (r5, F-4)** — Expected: (ii)'nin kapı yazımı eşleşmez, üyelik `Planned` kalır,
  görev açılmaz, tur durur; (ii) ile (iii) arasında (üyeliksiz siparişte (i) ile (iii) arasında — r6, F-4) yeniden açılıp sipariş Taslak
  dalgaya eklenirse serbest bırakma 422 `WAVE_ORDERS_INELIGIBLE`, kullanıcı çıkarır (AC-15 n5, n6).
- **Kapalı depoda dalga yaratma / sipariş ekleme / serbest bırakma / taşıma** — Expected: 422 `WAVE_SETTINGS_REQUIRED` (AC-15 k).
- **Ayar izni olmayan planlayıcı** — Expected: `warehouse.waves.update` taşıyan ama `warehouse.waves.settings.configure` taşımayan kullanıcıya ayar
  yazımı 403 (Q-6 = B).
- **Fazladan PUT / DELETE ucu** — Expected: `LedgerImmutabilityArchitectureTests` kırmızı.

## 14. Authorization Convention

**Namespace (D-4, sahip kararı Beste 2026-11-12):** `warehouse.waves.*` — PKS-001 `warehouse.*` ad alanı altında; mockup'taki `wave.*` adları
buna çevrilir. **Not:** PKS-001 §4 `warehouse.*`'ı MOD-0178'e yazar ve "one namespace per WMS module" (Q-08) der; D-4 bunun MOD-0180 için
adlı istisnasıdır → §4'e alt ad alanı satırı ve `:92` istisna cümlesi (Q-7 = A, karar 03) **yazıldı — commit `0aa802519`, sahip onayı Beste
2026-11-12** (`.antigravity/rules/permission-key-standard.md:92`, `:105` "`warehouse.waves.*` hariç — MOD-0180", `:106` `warehouse.waves.*` →
MOD-0180; §6). MOD-0178 tarafındaki not (X-16) **yazıldı — MOD-0178 r29 `adfe1bd7d`, inceleme UYGUN; Ali'nin onay turu (r29 + bu paket)
açık** (r5; not MOD-0178 §14.1 `:2128-2152`) — `:92`'nin "owner decision recorded in both modules' packs" şartının MOD-0178 yarısı odur
(§6 madde (4), §24). **Bekçi (r5, CT kararı — r6: sahip onayladı 2026-11-13, kararlar 03 eki):** notun "MOD-0178 `warehouse.waves.*`
anahtarı tanımlamaz" yasağı AC-5'in alt ad alanı vakasıyla (S-5c, S-5d) DL-1'de ölçülür (r29 incelemesi F-1). Kazara bir eklemeyi 0178'in
sabitleme testleri bugün de dolaylı yakalar; vakanın kapattığı boşluk, sabitlemeleri de güncelleyen bilinçli eklemedir (r7, r6 incelemesi F-4 —
§16 AC-5).
Manifest yalnız `warehouse.waves.*` bildirir; 0178'in `warehouse.*` anahtarları bildirilmez. Depoda `warehouse.waves.` anahtarı yok
(`git grep`, services + frontend: 0 eşleşme — çıkarım: yeni).

**Son ek düzeltmesi (Q-5):** iş paketinin listesindeki `edit` PKS-001 §5'te eski yazımdır, kanonik karşılığı `update`'tir
(`.antigravity/rules/permission-key-standard.md:115-127`) → paket **`warehouse.waves.update`** yazar. `release`, `recall`, `resolve` Tier-3 (bu
pakette kayıtlı), `cancel` Tier-3 (sözlükte örnek), `export` Tier-2, `settings.configure` Tier-3 (Q-6 = B, karar 03 — fiil `configure`
PKS-001 §5'te ve Rol İzinleri köprüsünde var, X-15).

**Ayar izni ayrı (Q-6 = B, karar 03; inceleme F-15):** `WaveManaged` anahtarı **bütün depo** için MOD-0178'in görev açılışını değiştirir
(§8.6); bu yüzden Tier-1 `update`'e bağlanmaz, ayrı Tier-3 anahtar `warehouse.waves.settings.configure` ister. Planlayıcının `update`'i
depo davranışını değiştiremez (AC-15). Ayar okuması `read`'dedir.

| Anahtar | Tier | Dilim | Anlam |
|---|---|---|---|
| `warehouse.waves.read` | 1 | DL-1 | pano, liste, ayrıntı, siparişler, görevler, yük, olaylar, planlayıcı listesi, depo ayarı okuması |
| `warehouse.waves.create` | 1 | DL-1 | dalga oluştur |
| `warehouse.waves.update` | 1 | DL-1 | Taslak düzenle, sipariş ekle / çıkar, öneri (depo dalga ayarı **değil** — Q-6 = B) |
| `warehouse.waves.settings.configure` | 3 (bu pakette kayıtlı) | DL-1 | depo dalga ayarı yazma: `WaveManaged`, `TimeZoneId`, eşikler; gerekçe zorunlu; kapatma bekleyen siparişlerin görevlerini açar (Y-1 = A, §8.19) |
| `warehouse.waves.cancel` | 3 | DL-1 | Taslak iptal (gerekçe) |
| `warehouse.waves.release` | 3 (bu pakette kayıtlı) | DL-2 | serbest bırak (rota + görev üretimi) |
| `warehouse.waves.recall` | 3 (bu pakette kayıtlı) | DL-3 | kalan görevleri geri çek (gerekçe) |
| `warehouse.waves.exceptions.read` | 1 | DL-3 | istisna kuyruğu |
| `warehouse.waves.exceptions.resolve` | 3 (bu pakette kayıtlı) | DL-3 | çıkar / taşı / yok say |
| `warehouse.waves.kpis.read` | 1 | DL-5 | performans sayfası |
| `warehouse.waves.kpis.export` | 2 | DL-5 | CSV |

**Politika ve aktör:** `[Authorize]` (tenant shell) + her eylemde `[HasPermission(WavePermissions.X)]`; `AuthorizationGuardTests` yeni
controller'ları da ölçer. Aktör `tenant_user`; sistem komutları (`CompleteWaveRelease`, `Reconcile`, `ReleasePendingOrders`) bakım servisinin kapsamıyla (`SetScope`)
çalışır, HTTP ucu yoktur. Rol şablonu önerisi (yalnız yeni kiracılar; X-13, Ali (Auth)): **Dalga planlayıcı** = `read` + `create` + `update` +
`cancel` + `release`; **Vardiya sorumlusu** = `read` + `recall` + `exceptions.*`; **Performans izleyici** = `read` + `kpis.*` (rol adları öneri).
`settings.configure` **hiçbir şablon role girmez** — depo yöneticisine elle verilir (öneri; bütün depoyu etkiler, F-15). Rol şablonu kararı
Ali'nindir (Q-20, karar 03).
`read` anahtarlarında kişisel veri: kullanıcı kimliği ve adı (toplayıcı) — KVKK açısından görev ataması 0178'de zaten görünürdür (çıkarım).

**Manifest:** ModuleCode `WAVE-PLANNING`, SortOrder 180 (0178 `PUTAWAY-PICKING-PACKING` / 178 emsali,
`dc010b911:INV/Api/Warehouse/PutawayPickingPackingManifestProvider.cs:29`, `:40`), Pages `WAVES` (nav, `read`), `WAVEDETAIL` (nav değil, `read`),
`WAVEPLANNER` (nav, `read`; eylemler `update`), `WAVEEXCEPTIONS` (nav, `exceptions.read`), `WAVEKPIS` (nav, `kpis.read`; DL-5); `ApiPermissions` =
`warehouse.waves.settings.configure` (DL-1; ekranı olmayan ayar ucu — Q-6 = B; FU-0180-SETTINGS-UI gelince sayfa eylemine taşınır); nav anahtarları `Nav.Module.WAVEPLANNING`, `Nav.Page.WAVES`,
`Nav.Page.WAVEPLANNER`, `Nav.Page.WAVEEXCEPTIONS`, `Nav.Page.WAVEKPIS`.

## 15. Gateway / API Routing Decision
- **Karar:** uçlar `/api/warehouse/waves…` — mevcut `/api/warehouse/{everything}` → 5067 rotasının altında
  (`dc010b911:gateway/Diten.ApiGateway/ocelot.json:1153-1161`; taban `/api/warehouse` `:1134-1142`). **`ocelot.json` değişmez (doğrulandı:
  rota ailesi zaten kapsar).** Scope middleware `/api/warehouse` tabanını kapsar (MOD-0181 §15 ölçümü) — değişmez.
- Gerekli: integration-agent'ın 0180 uç kapsama testi — ayrı dosya `gateway/Diten.ApiGateway.Tests/Mod0180WaveRouteTests.cs` (emsal
  `Mod0178WarehouseRouteTests.cs`; ona yalnız X-14'ün `:97` sayı satırı ve yorumu için dokunulur — aşağıda) ve `/api/warehouse` rota ailesinin sahibi MOD-0178'in onayı (XMC-001; aile tek-yazıcı
  seam'dir — `domain-config.md:56`).
- **Testin ölçtüğü şey üretimdir (ölçüldü, F-5):** Gateway testleri üretim `gateway/Diten.ApiGateway/ocelot.json`'u okur — test projesi dosyayı
  bağlı kopya olarak çıktı dizinine alır (`gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj:26`) ve 0178 testi onu
  `AppContext.BaseDirectory`'den okur (`Mod0178WarehouseRouteTests.cs:23`); uç listesini de üretim controller kaynağından tarar (`:219-236`).
  0180 testi aynı yolu izler: üretim `ocelot.json` + `INV/Api/Controllers/WavePlanning/` kaynağı + WMS-WAVE-BUNDLE taslağının yolları; uç listesi
  testte elle yazılmaz. 0178 testi bütün `api/warehouse` controller'larını özyineli taradığı için 0180 uçları onun ölçümüne de girer.
- **0178 Gateway testinin uç sayısı (r8, r7 incelemesi F-1 — r7'nin "0178 testi değişmeden yeşil kalır" cümlesi yanlıştı ve kaldırıldı):**
  `Mod0178WarehouseRouteTests.cs` rota iddiaları (`:98-108` — her uç tek rotaya, `{everything}`'e, 5067'ye; DELETE yalnız `location-roles`;
  PATCH yok) 0180 uçlarıyla değişmeden yeşil kalır; ama `:97` yönlendirilen uç sayısını sabitler (bugün **73**, ölçüm HEAD `0b5062383`) ve
  ilk 0180 ucuyla kırmızı olur. Bu yüzden uç ekleyen her dilim (DL-1, DL-2, DL-3, DL-5) `:97`'yi **dilim başında ölçülen sayı + dilimin
  eklediği 0180 uç sayısı** yapar, yorumuna dilimin uçlarını adıyla yazar ve bunu dilimin controller uçlarıyla **aynı commit'te** yapar
  (integration-agent; XMC-001 X-14, sahip onayı Beste 2026-11-13 — kararlar 03 eki, `c7dc4ea6a` + kapsam teyidi `6235794ab`, r9: uç ekleyen
  her dilim, ayrı sahip onayı yok; MOD-0181 emsali). Dilim kabul kaydı önce → sonra değerini yazar ve `Assert.Equal(<n>, endpoints.Count)`
  satırına atıf yapar (satır numarasına değil). Dilim kabulünde Gateway test projesi koşar; 0178 testinde arada kırmızı pencere kalmaz
  (AC-55 0178 sayı vakası, S-55c). **DL-6 DL-5 kabulünden sonra** başlar (r9, r8 incelemesi F-1): `Mod0180WaveRouteTests`'in klasör /
  WMS-WAVE-BUNDLE taslak eşitliği (AC-55) taslak DL-1'de 23 işlemle yazıldığı için ancak bütün 0180 uçları teslim edilince sağlanır. Bütün dilimlerden
  sonra beklenen sayı 73 + 23 = 96'dır (çıkarım; arada başka modül uç eklemezse — X-14 dağılımı).
- İletilen başlıklar: `Authorization`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match`, `Accept-Language`. Dev dilimi
  `ocelot.json`'a yazmaz.

## 16. Acceptance Criteria

Her guard maddesi **üretim koduna karşı** ölçülür ve sabotaj kanıtı ister: ilgili üretim satırı geçici bozulur, test kırmızıya döner, bozma geri
alınır (kendi kopyasıyla ölçen test guard sayılmaz). Sabotaj kimliği `S-n` biçimindedir; kabul kaydı her `S-n` için komut + kırmızı çıktı + geri
almayı yazar.

**Bekçi olmayan maddeler (F-13):** AC-40 (`verify_datatable_page.py`), AC-43 (durumlar), AC-44 (375 px), AC-46 (kesim gösterimi) ve
AC-47 (ağırlık / hacim, ikon + metin) **araç ya da tarayıcı kontrolüdür, bekçi değildir** — sabotaj istenmez; kanıtları araç çıktısı / ekran
ölçümüdür. AC-27 bir performans ölçümüdür, S-27 eşiğin çalıştığını gösterir. Diğer her maddenin adlı sabotajı vardır.

**DL-1 — sözleşme taslağı + dalga çekirdeği (arka uç)**

- [ ] AC-1 (zorunlu) — `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` yazıldı: §3'teki her uç (operationId, istek / cevap şeması,
  hata kodları §8.17, `If-Match` / `Idempotency-Key`), §8.6 port sözleşmesinin 0178'e bakan yüzü (bilgi), sürüm `1.0.0-draft`; sözleşme testi
  (`WmsDraftContract.cs` emsali) controller cevaplarını taslağa karşı doğrular. S-1: bir cevaptan zorunlu alanı kaldır → sözleşme testi kırmızı.
- [ ] AC-2 (zorunlu) — `POST /waves` geçerli 8 alanla 201; `Status = Draft`; `Number` `{ŞİRKET}-WAV-` + 6 hane, kiracı genelinde tekil;
  `CutoffAtUtc` ve `TimeZoneId` sunucudan; `wms_wave_events`'te 1 `Created` olayı aynı işlemde. S-2: olay yazımını işlemden çıkar → kırmızı.
- [ ] AC-3 (zorunlu) — Kiracı / şirket / depo izolasyonu: başka kiracıdan her uç (GET, update, orders, release, cancel, recall, exceptions,
  resolve, kpis, settings) 404; aynı kiracıda başka şirketten 404; listeler yalnız kendi şirketi + istenen depo. S-3a: depodan `TenantId`
  yüklemini düşür → kırmızı; S-3b: `LegalEntityId` yüklemini düşür → kırmızı.
- [ ] AC-4 (zorunlu) — Gövde ya da sorguda `tenantId` / `legalEntityId` → 400. S-4: denetimi kaldır → kırmızı.
- [ ] AC-5 (zorunlu) — Her eylem `[HasPermission(WavePermissions.*)]`; manifest yalnız uygulanan `warehouse.waves.*` anahtarlarını bildirir;
  controller'lar `Controllers.WavePlanning` namespace'inde; `WarehouseRegistrationTests` (`dc010b911:INVT/Warehouse/WarehouseRegistrationTests.cs:49-56`)
  **değişmeden** yeşil. **Alt ad alanı sahipliği vakası (r5 — CT kararı; r6: sahip onayladı 2026-11-13, kararlar 03 eki; X-16 bekçisi,
  MOD-0178 r29 incelemesi F-1):** 0180 test ağacındaki (`INVT/WavePlanning/`) test üretim manifestlerini okur — 0178'in `PutawayPickingPackingManifestProvider`
  (`INV/Api/Warehouse/PutawayPickingPackingManifestProvider.cs:25`) manifestinin sayfa, eylem ve `ApiPermissions` anahtarlarının **hiçbiri**
  `warehouse.waves.` önekli değildir; `WavePlanningManifestProvider` manifestinin **her** anahtarı `warehouse.waves.` öneklidir. **Kapsam (r7,
  r6 incelemesi F-4 — r6'nın "bugün bunu ölçen test yok" cümlesi daraltıldı):** önek sahipliğini **adıyla** ölçen test yok
  (`WarehouseRegistrationTests.cs:37` yalnız `StartsWith("warehouse.")` denetler); ama 0178 manifestine **kazara** eklenen bir anahtarı 0178'in
  sabitleme testleri bugün dolaylı yakalar — 8 sayfanın her birinin eylem listesi sabitlidir (AC20 `:80-97`, AC34 `:130-149`, AC36 `:174`
  "eylem yok"), sayfa sayısı `:122`'de 8, `ApiPermissions` `:100`'de boş, `Controllers.Warehouse`'un her HTTP rotası
  `WmsBundleContractTests.cs:243-244`'te dondurulmuş sözleşmeye eşit (satırlar HEAD `90f6a1da8`'de ölçüldü). **Gerçek boşluk** bu
  sabitlemeleri de **bilinçli güncelleyen** bir 0178 eklemesidir (kabul edilmiş bir 0178 değişikliği `warehouse.waves.` önekli anahtar ekleyip
  sabitlemeleri ona göre günceller); onu yalnız bu vaka yakalar. 0178 test dosyaları değişmez (§5.1).
  S-5a: bir eylemden özniteliği kaldır → `AuthorizationGuardTests` kırmızı; S-5b: `WavesController`'ı
  `Controllers.Warehouse` namespace'ine taşı → `WarehouseRegistrationTests` kırmızı (0178 manifestinde olmayan anahtar); **S-5c** (r5; r6,
  F-5 düzeltildi — sahip onayladı 2026-11-13; r7, r6 incelemesi F-4: beklenen ek kırmızılar adıyla; r8, r7 incelemesi F-3 / F-1: (3)'ün sabit
  şablonu, (iii) ve (iv) bu rotayla, (iv)'te sabit 73 yok): 0178'in kayıt testi
  `The_manifest_declares_only_the_warehouse_keys_the_controller_enforces` (`WarehouseRegistrationTests.cs:25-57`) yeşil kalacak biçimde üç geçici
  dokunuş birlikte: (1) 0178 manifestinin **`PICKTASKS`** sayfasına `warehouse.waves.x` anahtarlı bir sayfa eylemi (`ApiPermissions`'a değil —
  `:100` onu boş sabitler), (2) aynı anahtar `WarehousePermissions.Implemented`'e (`:39` eşitliği), (3) **`PickTasksController`**'a
  (`Controllers.Warehouse`; `WarehouseLocationRolesController` değil — `:41-47` eylem sayısını 6'ya ve `:195-209` rotalarını sabitler;
  `WarehouseMonitoringController` değil — AC36 `:179-188` rotalarını sabitler) `[HasPermission("warehouse.waves.x")]` taşıyan geçici bir
  **`GET`** eylemi (PUT / DELETE değil — `LedgerImmutabilityArchitectureTests` yalnız adlı PUT / DELETE'lere izin verir) (`:56` eşitliği:
  manifest anahtarları = `Controllers.Warehouse`'un zorladığı anahtarlar). **Eylemin şablonu sabittir (r8, r7 incelemesi F-3):**
  `[HttpGet("wave-sabotage")]` → rota `GET /api/warehouse/pick-tasks/wave-sabotage` (sınıf `[Route("api/warehouse/pick-tasks")]`,
  `PickTasksController.cs:23`). Şablonsuz `[HttpGet]` **kullanılmaz**: controller'da şablonsuz `GET` zaten var (`:31`, liste) — aynı anahtara
  düşen eylem `WmsBundleContractTests`'in `served` sözlüğünde (`:239`, dizinleyiciyle yazım) mevcut girdinin üzerine yazar, rota sayısı
  değişmez ve (iii) kesin kırmızı olmaz; host kullanan testlerde de aynı rotada iki eylem belirsiz eşleşme (AmbiguousMatch) verir. Sabit harf
  dizili şablon `{taskId}` parametreli `GET`'lerle (`:37`, `:42`) çakışmaz — ASP.NET Core yönlendirmesinde harf dizili kesim parametreli kesimden
  önce eşleşir (belgelenmiş davranış; çıkarım, koşulmadı). **Beklenen sonuç (kesin):** kırmızı — (i) alt ad alanı sahipliği
  vakası; (ii) `WarehouseRegistrationTests.AC34_the_manifest_declares_the_three_S2_pages_and_one_action_per_S2_button` (`:130-149` S2 eylem
  sözlüğü — (1)'in eylemi); (iii) `WmsBundleContractTests.Every_served_warehouse_route_is_an_operation_with_its_permission_key` — `:243`'te
  kırmızı: `served`'de sözleşmede olmayan anahtar `GET /api/warehouse/pick-tasks/wave-sabotage` ((3)'ün eylemi; `:244`'ün sayısı 49 olurdu,
  ama `:243` önce düşer); Gateway test projesi koşulursa (iv)
  `Mod0178WarehouseRouteTests.Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route` — `:97`'de
  kırmızı: taranan uçlara `GET /api/warehouse/pick-tasks/wave-sabotage` eklenir, sayı **dilim başında ölçülen değer + DL-1'in uçları + 1**
  olur (r8, F-1: sabit 73 yazılmaz — DL-1 `:97`'yi kendi uçlarıyla güncellemiştir, X-14; rota iddiaları `:98-108` yeşil kalır, çünkü yeni yol
  `{everything}` ile tek rotaya eşleşir). Yeşil — `The_manifest_declares_only_the_warehouse_keys_the_controller_enforces`
  (`:37`, `:39`, `:45`, `:56` dahil), AC20, AC36. (ii)–(iv) kazara eklemeyi yakalayan sabitlemelerdir; bilinçli eklemede onlar da güncellenir
  ve geriye yalnız (i) kalır — S-5c'nin kanıtladığı budur. **Geçme koşulu:** (i)–(iii) kırmızı (Gateway koşulduysa (iv) de), listelenen
  yeşiller yeşil; listede olmayan bir kırmızı çıkarsa kabul kaydı onu adıyla yazar ve sabotaj yeniden incelenir. Geri alınır, commit'e girmez
  (0178 üretim dosyalarına geçici dokunuş — sabotaj, XMC değişikliği değil). **S-5d** (r6, F-5): 0180 manifestine (`WavePlanningManifestProvider`)
  geçici `warehouse.x` anahtarı ekle → vakanın 0180 yarısı ("her anahtar `warehouse.waves.` önekli") kırmızı; geri alınır.
  **INVENTORY-LEDGER bütünlük vakası (r10 — XMC-001 X-17, sahip onayı `ad1100158`):** DL-1 kabulünde iki bütünlük testi birlikte yeşil —
  (a) 0180'in kendi manifest bütünlük testi (`INVT/WavePlanning/`; `Controllers.WavePlanning`'deki controller'ların zorladığı anahtarlar =
  `WavePlanningManifestProvider` manifestinin anahtarları — bu AC'nin ilk cümlesi) ve (b) MOD-0173'ün
  `InventoryLedgerManifestCompletenessTests.Every_enforced_permission_is_reachable_from_the_manifest` (dışlama listesinde `.Controllers.WavePlanning`
  satırıyla, X-17); kabul kaydı (b)'nin dosya farkının yalnız o satır + yorumu olduğunu yazar (`git diff` çıktısı). **S-5e** (r10): X-17
  dışlama satırını kaldır → `InventoryLedgerManifestCompletenessTests.Every_enforced_permission_is_reachable_from_the_manifest` kırmızı
  (`Assert.All` iddiası; ileti `[HasPermission(warehouse.waves.…)] is enforced but the manifest never declares it` — `warehouse.waves.*`
  anahtarları INVENTORY-LEDGER manifestinde yok); (a) yeşil kalır; geri alınır, commit'e girmez.
- [ ] AC-6 (zorunlu) — İdempotent yazma: her yazma komutu için aynı anahtar + aynı gövde → 200 replay (olay / `Version` değişmez); farklı gövde → 400
  `IDEMPOTENCY_KEY_REUSED`. Ayrı testler: yarat, düzenle, ekle, çıkar, iptal, serbest bırak, geri çek, istisna eylemi, ayar. S-6a: parmak izi
  karşılaştırmasını kaldır → kırmızı; S-6b: anahtar aramasını kaldır → ikinci olay, kırmızı.
- [ ] AC-7 (zorunlu) — İyimser kilit (D-14): eski `If-Match` ile her yazma → 409 `WAVE_CONFLICT`; eşzamanlı iki düzenlemeden tam biri. S-7: depoda
  `Version` koşullu filtreyi kaldır → kırmızı.
- [ ] AC-8 (zorunlu) — Silme yok: DELETE / PUT ucu yok, `LedgerImmutabilityArchitectureTests` **değişmeden** yeşil; `cancel` yalnız `Draft`'ta,
  gerekçe zorunlu (400 `WAVE_REASON_REQUIRED`), durum `Cancelled`, `IsDeleted = false`, GET 200; `Released`'da 409 `WAVE_INVALID_STATE`. S-8a:
  `DELETE /waves/{waveId}` ekle → bekçi kırmızı; S-8b: `cancel`'da durum denetimini kaldır → `Released` iptal olur, kırmızı.
- [ ] AC-9 (zorunlu) — Merkezi denetim: her 0180 yazma komutu `ICentralAuditDeclaration` taşır; `CentralAuditDeclarationTests` sayısı =
  **dilim başındaki ölçülen sayı + dilimin 0180 yazma komutları** (taban dilimin başlangıç commit'inde ölçülür ve kabul kaydına yazılır; r2
  ölçümü `dc010b911:INVT/CentralAudit/CentralAuditDeclarationTests.cs:53` = 117, bilgi — paket sabit taban yazmaz) ve sağlayıcı listesinde
  `WavePlanningManifestProvider` (`:60-69`, X-10); başarılı yazma → 1 kayıt, replay → 0, başarısız → 0. S-9: bir geri çağrıdan
  `CentralAuditTransaction.EnlistAsync`'i kaldır → kırmızı.
- [ ] AC-10 (zorunlu) — Kayıt kodu: `Number` değiştirilemez (gövdede gelirse 400); eşzamanlı iki yaratma farklı numara; sayaç yazılamazsa 503
  `WAVE_RECORD_CODE_UNAVAILABLE` ve dalga yok; 0180 kendi sayacını yazmaz (Q-4 = A: 0178 sayacı `WAV`). S-10: `TypePrefixes`'ten `WAV`'ı çıkar →
  yaratma 503, AC-2 kırmızı.
- [ ] AC-11 (zorunlu) — Uygunluk ve D-8: yalnız `Allocated` + `PickIssuance = Deferred` outbound eklenir (diğerleri 422 `WAVE_ORDER_NOT_ELIGIBLE`
  satır sonuçlu); bir outbound iki etkin dalgada olamaz (409 `WAVE_ORDER_IN_OTHER_WAVE`); **eşzamanlı** iki eklemede tam biri; sipariş bütünüyle
  eklenir (üyelik satır / tahsis düzeyinde değil). S-11: tekil kısmi index'i kaldır → eşzamanlı testte ikisi de başarılı, kırmızı.
- [ ] AC-12 (zorunlu) — Öner (D-1): `GET /waves/{waveId}/suggestions` yalnız uygun ve `ShipToFilter`'a uyan siparişleri, `CreatedAt` artan, kalan
  kapasite kadar döner; **yazmaz** (olay ve üyelik sayısı değişmez); periyodik kurma yok (hosted service dalga **yaratmaz** — mimari test:
  `WaveMaintenanceHostedService` yalnız `CompleteWaveReleaseCommand` / `ReconcileWaveCommand` / `ReleasePendingOrdersCommand` gönderir — üçüncüsü
  r3, DL-2'de listeye girer). S-12: uygunluk süzgecinden `Active`
  üyelik koşulunu kaldır → başka dalgadaki sipariş önerilir, kırmızı; S-12b: `WaveMaintenanceHostedService`'e `CreateWaveCommand` gönderimi
  ekle → mimari test kırmızı.
- [ ] AC-13 (zorunlu) — Sınırlar (D-7): `MaxOrders` / `MaxLines` aşan ekleme **yazılır**, cevap `limitWarnings[]` taşır; teknik üst sınır (500 /
  2.000) aşan ekleme 422 `WAVE_TOO_LARGE`. S-13: sınır aşımında ekleme reddi koy → kırmızı (D-7 engellemez).
- [ ] AC-14 (zorunlu) — Saat dilimi (D-3): `Europe/Istanbul` deposunda `CutoffLocal = 2026-11-20T14:30` → `CutoffAtUtc = 11:30Z`; ABD deposunda yaz
  saati boşluğundaki yerel saat 400 `WAVE_CUTOFF_INVALID`; çakışan saat erken ofset + `cutoffAmbiguousResolved = true`; ayar yoksa 422
  `WAVE_SETTINGS_REQUIRED`. S-14: dönüşümde sunucu saat dilimini kullan → kırmızı.
- [ ] AC-15 (zorunlu; **(a)…(e) DL-1'de, kapatma vakaları (f)…(q) DL-2 kabulünde, (r) DL-3 kabulünde** — görev ertelemesi ve 0178 portu
  DL-2'dedir, DL-1'de ertelenmiş outbound oluşamaz; (r) dalga uzlaştırma döngüsünü ister, o DL-3'tedir — r7) — Depo dalga ayarı ve dalga
  yönetimini kapatma (Y-1 = A, karar 03 eki, §8.19):
  (a) okuma `read`; yazma **`warehouse.waves.settings.configure`** (Q-6 = B) + gerekçe; yalnız `warehouse.waves.update` taşıyan kullanıcıya
  yazma 403; (b) aralık dışı eşik 400, kayıt değişmez; (c) kayıt yokken varsayılanlar (false / — / 90 / 70 / 30) döner; cevaptaki
  `deferredOrderCount = 0`, `releasingOrderCount = 0`, `stuckOrders = []` **bilgidir, bekçi değildir** (r4, F-7: DL-1'de ertelenmiş outbound
  oluşamadığı için sayım hep sıfırdır; sayımın bekçisi DL-2'de (f) ve S-15b'dir); (d) başka şirketin ayarı 404; (e) `false` → `true` ve eşik
  değişikliği her zaman 200.
  **Bakım turu nasıl sürülür (r4, F-2):** (i), (j), (n), (p) ve (q)'daki her "bakım turu" testin **üretim `WaveMaintenanceHostedService`'in
  bekleyen açılış döngüsünün tek tur yöntemini** (§8.8, öneri `RunPendingReleaseOnceAsync` — r6: uzlaştırma ayrı döngüdedir) çağırmasıdır; test `ReleasePendingOrdersCommand`'ı doğrudan göndermez — böylece servisin komutu
  göndermemesi (S-15e) bu vakaları kırmızıya çevirir.
  **Kapatma fikstürü (DL-2, gerçek MOD-0178 koduyla):** depo D1'de O1 `Allocated` + `Deferred` (dalgasız), O2 `Allocated` + `Deferred` (Taslak
  dalga W1'de `Planned`), O3 0178'de iptal edilmiş ama `PickIssuance.State = Deferred` taşıyan, O4 `Released` dalga W2'de `Issued` (görevleri
  `WaveRef`'li), O5 `Allocating`; depo D2'de O6 `Allocated` + `Deferred`.
  (f) **`deferredOrderCount`** (r2 F-2 d): kapatmadan önce D1'de **2** (O1, O2 — O3 iptal, O4 `Issued`, O5 `Allocating` olduğu için sayılmaz;
  süzgeç yalnız `Status = Allocated` + `Deferred`, r2 F-2 c), D2'de 1; kapatma ve ilk turdan sonra D1'de 0.
  (g) D1'de `WaveManaged` `true` → `false` → **200** (409 yok); ayar `false`, `Version` +1; O1 ve O2'nin görevleri **aynı istekte** açılır: görev
  sayısı = plan öğesi sayısı, görevler 0178'in dalgasız tahsis çıktısıyla alan alan aynı (AC-18 betiği, kimlik / zaman normalize), `WaveRef`
  yok, outbound `PickIssuance = Issued` + `WaveId = null`, 0178 olayları `PickTasksIssued` (`reason = WaveManagementDisabled`) + görev başına
  `Created`; W1'deki O2 üyeliği `Removed` (`CloseReason = WaveManagementDisabled`) ve bu kapanışın olayı (`OrdersRemoved`, `At`) O2'nin `PickTasksIssued`
  olayından **önce** (r4, F-1; r5, F-6 c: bu sıra **bilgidir** — iki koleksiyonun `At` zamanları eşit olabildiği için karşılaştırma kararsızdır;
  kapanış-önce sıralamasının asıl bekçisi (n1)'dir); W1 `Draft`; cevap `pendingRelease { issuedCount: 2, failed: [], remainingCount: 0, stopReason: null }`; merkezi kayıt **1** (0178 yazımları
  ona enlist); ayar akışında `SettingsUpdated` + `PendingOrdersIssued`, `PendingOrdersStuckChanged` **yok** (takılı liste değişmedi).
  (h) **Etkilenmeyenler:** O3 (iptal edilmiş — görev açılmaz, iptal edilmez, rezervasyonu değişmez), O4 ve görevleri (`Version`, `WaveRef`,
  `Sequence` aynı), W2 (durumu ve sonraki uzlaştırması), D2'deki O6 **değişmez**; hiçbir sipariş iptal edilmez.
  (i) **Yarış — tahsis:** O5'in tahsisi politikayı C1'den önce okuyup outbound'u C1'den sonra `Deferred` yazar (test kancası) → bir sonraki
  bakım turunda (üretim tur yöntemi) O5'in görevleri açılır (merkezi kayıt 1); kapatmadan sonra başlayan yeni tahsis görevleriyle doğar (0178
  normal akışı, 0180 yazımı yok); bekleyen yokken bakım turu komut göndermez, yazmaz (kayıt 0).
  (j) **Kısmi hata ve tur yarışı:** (j1) `IssueImmediateAsync` ikinci siparişte zaman aşımı (sahte port yalnız hata enjeksiyonu için; zaman aşımı
  bağlantı sınıfıdır) → 200, ayar `false` kalır, `issuedCount 1`, `failed[]` 1 (`WAVE_INVENTORY_UNAVAILABLE`), `remainingCount 1`; bakım turu
  (üretim tur yöntemi) kalanı açar; sonuç bilinmeyen sipariş için görev sayısı = plan öğesi sayısı (ikinci görev yok). (j2) **Tur yarışı (r4,
  F-2):** ayrı fikstürde bekleyen O8 için ilk tur ile üretim bakım tur yöntemi test kancasıyla **aynı anda** çağrılır ve ikisi de O8'i `Deferred`
  okuduktan sonra 0178 işlemine girer → tek görev seti (görev sayısı = plan öğesi sayısı), 0178 `PickTasksIssued` olayı **1**, kaybeden çağrı
  replay alır (kazananın `TaskIds`).
  (k) **Kapalı depo:** `POST /waves`, sipariş ekleme, serbest bırakma ve "Sonraki dalgaya taşı" 422 `WAVE_SETTINGS_REQUIRED`; (h) ölçüldükten sonra
  ayrı vaka olarak W2'de geri çekme 200 ve çekilen O4 bir sonraki bakım turunda dalgasız görev alır.
  (l) **`Releasing` üyeleri (r4, F-5 ile genişledi):** ayrı fikstürde D3'te `Releasing` dalga W3'ün üyesi O9 (R1 C1'den **önce** işlenmiş; port
  çağrısı test kancasıyla bekletilir) ve dalgasız bekleyen O10 → kapatma: ilk tur yalnız O10'u açar; cevap `issuedCount 1`, `remainingCount 0`;
  GET ayar `deferredOrderCount 1`, `releasingOrderCount 1`; bakım turu O9'u açmaz; kanca bırakılınca serbest bırakma O9'u **dalgalı** açar
  (`WaveRef` dolu), W3 `Released`, ardından `deferredOrderCount 0`.
  (m) Aynı `Idempotency-Key` + aynı gövdeyle kapatma tekrarı → 200 replay, yeni görev / olay / kayıt yok; ilk tur sürerken `false` → `true`
  yapılırsa tur bir sonraki siparişte durur.
  (n) **Yetim üyelik yok ve C1'e karşı sıralama (r4, F-1):** (n1) **süreç ölümü:** kapatmanın ilk turu O2'de, turun o siparişteki iki yazımı
  (üyelik kapanışı ve görev açılışı) **arasında** test kancasıyla kesilir (istek iptal belirteci tetiklenir — bağlantı sınıfı, tur durur) →
  O2 `Deferred`, etkin üyeliği yok, W1'de `Planned` üyelik **yok**; bakım turu O2'yi dalgasız açar; ardından depo yeniden açılır, W1'e yeni bir
  sipariş eklenip serbest bırakılır → 200 (`WAVE_ORDERS_INELIGIBLE` yok). (n2) **ekleme yarışı:** `AddWaveOrders` (O7 → W1) ön kontrolü geçtikten
  sonra test kancasıyla bekletilir, bu arada C1 + ilk tur tamamlanır (O7 dalgasız açılır), sonra ekleme işlemi sürer → 422
  `WAVE_SETTINGS_REQUIRED`; O7'nin etkin üyeliği yok, W1'de yetim `Planned` üyelik yok. (n3) **serbest bırakma yarışı:** `ReleaseWave(W4)` ön
  kontrolü geçtikten sonra kancayla bekletilir, bu arada C1 işlenir, sonra R1 → 422 `WAVE_SETTINGS_REQUIRED`; W4 `Draft`, `PickPlan` yok, görev
  yok; W4'ün üyeleri **C1 isteğinin ilk turunda** (R1 kancada beklerken) üyelikleri `Removed` olarak dalgasız açılır — cevabın `issuedCount`'u
  onları sayar (r5, F-6 b: r4 "sonraki turda" diyordu; ilk tur R1'i beklemez). (n4) **`Version` korunur:** dalga yönetimli depoda `AddWaveOrders`
  200 sonrası ayarın `GateSeq`'i +1, `Version`'ı aynı; eklemeden önce okunan `If-Match` ile eşik değişikliği 200.
  (n5) **(i) ile (ii) arasında yeniden açma (r5, F-4):** kapatmanın ilk turu O2'de (i)'den sonra, (ii)'den önce kancayla bekletilir; bu arada
  depo yeniden açılır (`false` → `true`, 200); kanca bırakılınca (ii)'nin kapı yazımı eşleşmez → W1'de O2 üyeliği `Planned` kalır, O2 `Deferred`,
  O2'ye görev yok, cevap `stopReason = Reopened`; `GateSeq` kapı yazımından artmamıştır. (n6) **(ii) ile (iii) arasında kalan pencere ve kurtarma
  (r5, F-4; kanıttır, bekçi değildir — ön kontrolün bekçisi AC-22 (a) / S-22'dir):** kapatmanın ilk turu O2'de (ii)'den sonra, (iii)'ten önce
  kancayla bekletilir; bu arada depo yeniden açılır ve O2 W1'e yeniden eklenir (200); kanca bırakılınca O2 dalgasız açılır, tur sonraki
  siparişin (i)'inde durur → W1'in serbest bırakması 422 `WAVE_ORDERS_INELIGIBLE` (`details.orders[]` O2'yi adlandırır), yazım yok; O2
  çıkarılıp W1'e uygun bir sipariş eklenince serbest bırakma 200.
  (o) **Port düzeyi (r4, F-2, F-5; gerçek 0178 `WavePickIssuanceService`):** (o1) **replay:** aynı outbound için `IssueImmediateAsync` art arda
  iki kez → ikinci çağrı `Issued` + aynı `TaskIds`; ikinci çağrıda görev sayısı, 0178 olay sayısı ve outbound `Version` değişmez. (o2)
  **`NotDeferred`:** `PickIssuance = null` outbound (görevleri tahsiste açılmış) → `Ineligible { NotDeferred }`, görev sayısı değişmez.
  (p) **Zehirli sipariş (r4, F-3):** ayrı fikstürde kapalı D4'te bekleyen P1 (en eski), P2, P3; gerçek portun üstündeki sarmalayıcı P1 için
  siparişe özgü (bağlantı sınıfı dışı) istisna atar. (p1) bakım turu → P2 ve P3 açılır; P1 `Deferred`; GET ayar `stuckOrders = [P1]` (kod,
  `firstFailedAtUtc`); **r5 (F-2 e):** `code = Unexpected` (sarmalayıcının istisna türü eşleme tablosunda yok) ve GET cevabında, olay verisinde
  istisna türünün adı **geçmez** (log'da `exceptionType` var); olaylar `PendingOrdersIssued` 1 + `PendingOrdersStuckChanged` 1; merkezi kayıt 1;
  `Warning` log'u (`consecutiveFailures = 1`); **r5 (F-6 f):** sayaç `inventory_wave_pending_release_order_errors_total{code="Unexpected"}` 1,
  gösterge `inventory_wave_pending_release_stuck_orders{warehouse_id=D4}` 1. (p2) ikinci turdan önce fikstür yeni bekleyen P4 yazar → tur önce P4'ü, **sonra** P1'i dener (sarmalayıcının çağrı
  sırası); P4 açılır (olay + kayıt 1). (p3) yalnız P1 kalmışken üçüncü tur (aynı kodla yine başarısız) → **yazım yok:** olay 0, merkezi kayıt 0,
  `stuckOrders` aynı; log `consecutiveFailures = 3`, sayaç 3; **r5 (F-2 e):** dördüncü turda sarmalayıcı P1 için `PlanInconsistent`'e eşlenen
  istisna atar → yine yazım yok (olay 0, kayıt 0), `stuckOrders[0].code` `Unexpected` kalır (kategori değişimi liste değişikliği değildir),
  sayaçta `code="PlanInconsistent"` 1. (p4) enjeksiyon kaldırılınca → P1 açılır, `stuckOrders = []`,
  `PendingOrdersStuckChanged` (çıkan P1) + kayıt 1. (p5) yeni fikstürde P1 için **bağlantı sınıfı** istisna (zaman aşımı) → tur durur, P2 ve P3
  açılmaz, `stuckOrders = []`; enjeksiyon kalkınca sonraki tur üçünü açar. (p6) **yaygın arıza (r5, F-1 — r4 beklentisi değişti):** yeni
  fikstürde dalga yönetimli D6'da bekleyen P1, P2, P3 (en eski üç) ve P5 (dördüncü); sarmalayıcı P1, P2, P3 için siparişe özgü istisna atar.
  D6 kapatılır → ilk tur P1, P2, P3'te art arda üç hatayla durur, P5 denenmez; cevap `pendingRelease { issuedCount: 0, failed: [P1, P2, P3 —
  hepsi WAVE_PENDING_ORDER_ERROR], remainingCount: 4, stopReason: RepeatedOrderErrors }`; GET ayar `stuckOrders = [P1, P2, P3]`;
  `PendingOrdersStuckChanged` 1 (giren üç; C1'in kaydına enlist). Ardından bakım turu → P5 **önce** denenir ve açılır; P1, P2, P3 sonda yeniden
  denenir, yine başarısız; `stuckOrders` aynı; `PendingOrdersIssued` 1 (P5), merkezi kayıt 1.
  (p7) **Parti başına yeni takılı sınırı (r5, F-1):** test yapılandırması `PendingReleaseMaxNewStuckPerBatch = 2`; kapalı D4'te bekleyen R1
  (hatalı), R2, R3 (hatalı), R4, R5 (`CreatedAt` sırasıyla) → bakım turu: R1 takılır, R2 açılır, R3 takılır → yeni takılı 2 → tur durur
  (`stopReason = NewStuckLimit`, log ve `PendingOrdersIssued` olayında); `stuckOrders = [R1, R3]`; R4 ve R5 denenmez; sonraki tur R4 ve R5'i
  açar. (p8) **Liste üst sınırı (r5, F-1; r6, F-2 / F-6 c — beklenti değişti):** `PendingReleaseStuckMax = 2`; liste [P1, P2] (ikisi de
  hatalı) doluyken yeni bekleyen T1 (hatalı) ve T2 → bakım turu: T1 listeye **alınmadan atlanır** (r7: partinin özet `Warning` log'u 1 —
  atlanan 1; sayaç +1), **T2 açılır**,
  tur durmaz (`stopReason = null`); `stuckOrders = [P1, P2]`; `Error` log'u 1 (duruma giriş). İkinci tur (T1 yine hatalı) →
  `Error` log'u yeni yazılmaz (toplam 1). Ardından enjeksiyon P1 için kaldırılır → üçüncü tur P1'i açar, liste [P2] (sınırın altında) → `Info`
  log'u 1 (durumdan çıkış); dördüncü turda T1 yine hatalıysa listeye girer.
  (p9) **`Version` korunur (r5, F-2 a):** (p1)'den önce okunan ayar `Version`'ı v; (p1)'in takılı yazımından sonra `Version` hâlâ v ve
  `If-Match: W/"v"` ile eşik değişikliği 200. (p10) **Atomik ve koşullu yazım (r5, F-2 b):** (p10a) kapalı D7'de bekleyen hatalı Pa ve Pb; test
  kancası iki bakım tur örneğini aynı anda koşturur (biri yalnız Pa'yı, diğeri yalnız Pb'yi dener) ve ikisini takılı yazımına aynı anda sokar →
  `stuckOrders` Pa'yı **ve** Pb'yi taşır (son yazan kazanmaz), `PendingOrdersStuckChanged` 2; (p10b) bakım partisi Pc'yi denedikten sonra, takılı
  yazımından önce (kanca) depo yeniden açılır → yazım eşleşmez, `stuckOrders = []`; **r6 (F-6 a):** partide açılan sipariş ve kapanan
  üyelik olmadığından olay **0** (`PendingOrdersStuckChanged` ve `PendingOrdersIssued` yok), merkezi kayıt **0**; log'da `stopReason = Reopened`. (p11) **Yeniden açmada temizlik (r5, F-2 c):** kapalı D4'te
  `stuckOrders = [P1]` iken `false` → `true` → 200; GET `stuckOrders = []`; `SettingsUpdated` olayında `clearedStuckOrders = [P1]`; gösterge D4
  için 0; depo yeniden kapatılınca P1 takılı olmayan sipariş olarak `CreatedAt` sırasıyla denenir. (p12) **Tur başı uzlaştırma (r5, F-2 d):**
  kapalı D4'te `stuckOrders = [P1]` ve başka bekleyen yokken P1 0178'de iptal edilir → bakım turu yine koşar (takılı liste boş değil), P1 için
  port **çağrılmaz**, P1 listeden çıkar (neden `NoLongerPending`); `PendingOrdersStuckChanged` 1, merkezi kayıt 1; gösterge 0.
  (p13) **Turda en çok bir kez (r5, F-3):** `PendingReleaseBatch = 2`; kapalı D4'te bekleyen P1 (hatalı), P2, P3 → bir bakım turu: parti 1
  [P1, P2] (P1 takılır, P2 açılır), parti 2 [P3] — P1 aynı turda takılı yeniden deneme olarak **tekrar denenmez** (sarmalayıcının P1 çağrı sayısı
  turda 1); merkezi kayıt 2; sonraki turda P1 bir kez daha denenir. (p14) **Tur başına parti sınırı (r5, F-3):** `PendingReleaseBatch = 1`,
  `PendingReleaseMaxBatchesPerTour = 2`; hatasız bekleyen R1, R2, R3 → bir bakım turu R1 ve R2'yi açar, R3 bekler (log'da `stopReason =
  RoundBudgetReached` — r6, F-6 b); sonraki tur R3'ü açar.
  (p15) **Takılı yeniden deneme bütçesi ve dönüşüm (r5, F-3):** liste [S1, S2, S3, S4] (`FirstFailedAtUtc` sırasıyla), dördü de yine başarısız,
  başka bekleyen yok. (p15a) `PendingReleaseStuckRetryPerTour = 4` → bir tur dördünü de dener (takılı yeniden denemeler art arda sayaca girmez,
  üç başarısız yeniden deneme turu durdurmaz). (p15b) `PendingReleaseStuckRetryPerTour = 2` → tur A S1 ve S2'yi, tur B S3 ve S4'ü, tur C yine
  S1 ve S2'yi dener (sarmalayıcının çağrı sırası).
  (p16) **Aynı siparişte art arda bağlantı durması (r6, F-3; r7, F-3 yeniden yazıldı — şüpheli sıralaması, başarı şartı, liste dolu; r8:
  (p16e) — şüpheli takılı yeniden denemelerden sonra):**
  sarmalayıcı adı geçen siparişin her çağrısında zaman aşımı atar (bağlantı sınıfı); sayaç turu durduran bağlantı hatasının `Warning`
  log'undaki `connectionStops` alanından okunur. (p16a) Kapalı D4'te bekleyen P1 (en eski; zaman aşımı), P2, P3 hatasız. Tur 1: P1 turun ilk
  çağrısı → tur durur (`ConnectionError`), önceki başarılı çağrı yok → `connectionStops = 0`; P2, P3 açılmaz; `stuckOrders = []`; gösterge
  `inventory_wave_pending_release_connection_stopped_tours{warehouse_id=D4}` 1. Tur 2: sarmalayıcının çağrı sırası **P2, P3, P1** (şüpheli
  sonda); P2 ve P3 açılır; P1 → `connectionStops = 1`, tur durur; gösterge 2. Tur 3: fikstür önce hatasız P4 yazar → P4 açılır, P1 →
  `connectionStops = 2`; gösterge 3. Tur 4: fikstür P5 yazar → P5 açılır, P1 eşiğe varır → `stuckOrders = [P1]` (`code =
  RepeatedConnectionError`), `failed[]`'ta `WAVE_PENDING_ORDER_ERROR`, tur bağlantı durması olmadan biter; `PendingOrdersIssued` 1 +
  `PendingOrdersStuckChanged` 1, merkezi kayıt 1; `Error` log'u 1; gösterge 0. Tur 5: fikstür P6 yazar → P6 açılır; P1'in takılı yeniden
  denemesi yine zaman aşımı, önceki başarılı çağrı var → atlanır, `stuckOrders` aynı, tur durmaz; gösterge **0**; olay yalnız
  `PendingOrdersIssued` (P6). (p16b) **Başarı kanıtı yokken sayaç artmaz (kesinti benzetimi):** ayrı fikstürde kapalı D4'te yalnız P1 (her
  çağrıda zaman aşımı) → üç tur: her tur P1'de (turun ilk ve tek çağrısı) durur, `connectionStops` 0 kalır, `stuckOrders = []`; gösterge 1,
  2, 3 (alarmın ölçtüğü). (p16c) **Turu durduran ama ilk denenen olmayan sipariş (r6 incelemesi F-3 b):** ayrı fikstürde P1 hatasız, P2
  (zaman aşımı), P3 hatasız, `CreatedAt` sırasıyla → tur 1: P1 açılır, P2 → önceki başarılı çağrı (P1) var → P2 `connectionStops = 1`, tur
  durur, P3 denenmez; tur 2: sarmalayıcının çağrı sırası P3, P2 → P3 açılır, P2 `connectionStops = 2`. (p16d) **Eşikte liste dolu (r6
  incelemesi F-3 c):** `PendingReleaseStuckMax = 1`; liste [Q1] (Q1 siparişe özgü hatalı); P1 (en eski; zaman aşımı); her turdan önce fikstür
  yeni hatasız bir sipariş yazar (N1…N5) → tur 1 P1'de durur (`connectionStops = 0`; N1 denenmez); tur 2–3'te N'ler açılır, P1
  `connectionStops` 1, 2; tur 4'te P1 eşiğe varır ama liste dolu → listeye alınmadan atlanır (`failed[]`'ta `WAVE_PENDING_ORDER_ERROR`,
  partinin özet `Warning` log'unda atlanan 1), `stopReason = null`, gösterge 0, `stuckOrders = [Q1]`; tur 5: N5 açılır, P1 yine atlanır
  (sayaç eşikte kalır), `stopReason = null`, gösterge 0. (p16e) **Şüpheli + takılı liste, başka bekleyen yok (r8, r7 incelemesi F-2):** ayrı
  fikstürde kapalı D4'te liste [S1, S2] (ikisi de siparişe özgü hata enjeksiyonuyla listeye girdi; S1 için enjeksiyon kaldırılır, S2 için
  sürer); takılı olmayan tek bekleyen X (her çağrıda zaman aşımı, bağlantı sınıfı). Tur 1: sarmalayıcının
  çağrı sırası **X** — X henüz şüpheli değildir ve takılı olmayan tek sipariştir, takılı listeden önce gelir; turun ilk çağrısı → tur durur
  (`ConnectionError`), `connectionStops = 0`, X şüpheli işaretlenir; S1 ve S2 bu turda denenmez. Tur 2: sarmalayıcının çağrı sırası **S1, S2,
  X** (takılı yeniden denemeler şüpheliden önce): S1 açılır (`Issued`), S2 yine hatalı (atlanır, liste S2 için değişmez), X → turda önce sonuç
  dönen çağrı (S1) var → `connectionStops = 1`, tur durur. Tur 3: S1 artık bekleyen değildir ve listede kalmaz (tur 2'nin yazımı ya da tur başı
  uzlaştırma çıkarır; S1 için port çağrılmaz), sarmalayıcının çağrı sırası **S2, X** — S2 yine denenir (takılı liste X yüzünden aç kalmaz); S2 sonuç döndürmediği için X'in
  sayacı **değişmez** (`connectionStops = 1`), tur durur; `stuckOrders = [S2]`.
  (p17) **Depolar arası sıra ve depo başına sınır (r6, F-1):** `PendingReleaseBatch = 1`, `PendingReleaseMaxBatchesPerTour = 2`; kapalı D8'de
  hatasız bekleyen E1, E2, E3, kapalı D9'da F1 (D8 sırada önce) → bir bakım turu: sarmalayıcının çağrı sırası **E1, F1, E2**; E3 bekler
  (D8 `RoundBudgetReached`); sonraki tur E3'ü açar.
  (p18) **Depo kümesi her geçişte yeniden taranır (r7, F-1):** `PendingReleaseBatch = 1`, `PendingReleaseMaxBatchesPerTour = 3`; sarmalayıcı
  çağrıları depo etiketiyle kaydeder; bakım turu geçişler arasında test kancasıyla bekletilebilir. (p18a) **Tur sürerken kapanan depo:** kapalı
  D8'de hatasız E1, E2, E3; dalga yönetimli D10'da bekleyen G1, G2 (D8 sırada önce). Bakım turu ilk geçişten (E1) sonra kancada beklerken D10
  kapatılır (C1 + ilk tur G1'i açar, `remainingCount 1`); kanca bırakılınca **aynı tur** sürer → çağrı sırası **E1, G1 (kapatma isteği), E2,
  G2, E3** — G2 turun sonunu beklemeden **ikinci geçişte** açılır. (p18b) **Turda işi biten depo geri döner:** kapalı D8'de E1, E2; kapalı
  D9'da F1 (D8 önce) → ilk geçiş E1, F1 (D9'un işi biter — koşul (1)); kanca ikinci geçişten önce D9'a yeni bekleyen F2 yazar (Yarış (b)
  benzetimi) → ikinci geçiş **E2, F2** — F2 aynı turda açılır.
  (p19) **Liste dolu tavanı, imleç ve özet log (r7, F-2):** `PendingReleaseStuckMax = 1`, `PendingReleaseBatch = 2`,
  `PendingReleaseListFullSkipsPerTour = 2`; kapalı D4'te liste [P1] dolu (P1 hatalı); takılı olmayan bekleyen U1, U2, U3 (siparişe özgü
  hatalı) ve U4 (hatasız), `CreatedAt` sırasıyla. Tur 1: parti 1 [U1, U2] → ikisi de listeye alınmadan atlanır → tavan → D4 için tur durur
  (log'da `stopReason = ListFullSkipLimit`); U3, U4 denenmez; partinin özet `Warning` log'u **1** (atlanan 2); sayaç
  `inventory_wave_pending_release_order_errors_total` +2; olay 0, merkezi kayıt 0. Tur 2: imleç U2'den sonra → sarmalayıcının çağrı sırası
  **U3, U4, U1** (U3 atlanır, U4 açılır, dairesel U1 atlanır → tavan → durur); `PendingOrdersIssued` 1 (U4), merkezi kayıt 1;
  `stuckOrders = [P1]`.
  (q) **Bütçe ve "hemen" (r4, F-6, sahip teyidi):** test yapılandırması `PendingReleaseBatch = 3`, `PendingReleaseTaskBudget = 5`; kapatılacak
  D5'te bekleyen Q1 (2 görev), Q2 (2), Q3 (4), Q4 (1), Q5 (1), `CreatedAt` sırasıyla → kapatma: ilk tur yalnız Q1 ve Q2'yi açar (Q3
  görev bütçesini aşar), cevap `issuedCount 2`, `remainingCount 3`; **tek** bakım turu çağrısı Q3, Q4 (bir parti) ve Q5'i (ikinci parti) açar —
  merkezi kayıt 2, ardından `deferredOrderCount 0`.
  (r) **İki ayrı döngü (r7, F-5; DL-3 kabulünde):** üretim `WaveMaintenanceHostedService` test host'unda **iki döngüsüyle** başlatılır (tek tur
  yöntemleri doğrudan çağrılmaz; `ReconcileSeconds` ve açılış döngüsünün aralığı test yapılandırmasıyla 1 sn); kapalı D11'de bekleyen H1;
  açılış turunun H1 için 0178 çağrısı test kancasıyla **bekletilir** (kanca tutuldukça açılış turu bitmez); dalga yönetimli D12'de `Picking`
  dalga W5'in bütün görevleri 0178'de terminal. Beklenen: kanca **tutulurken** en çok 10 sn içinde W5'in durumu uzlaştırmayla `Completed`'a
  geçer (§8.3; uzlaştırma döngüsü açılış turunu beklemeden tur attı); ardından kanca bırakılır, H1 açılır.
  Sabotajlar (DL-1): S-15: yüzde aralık denetimini kaldır → 150 kabul, (b) kırmızı; S-15c: ayar ucunun `[HasPermission]`'ını
  `WavePermissions.Update`'e çevir → (a)'nın 403 vakası kırmızı. Sabotajlar (DL-2): **S-15b** (r4 düzeltmesi, F-2): bekleyen süzgecinden
  `Status = Allocated`'ı kaldır → O3 sayılır (`deferredOrderCount` 3) ve tura girer, 0178 portu `OutboundCancelled` ile reddeder (O3 görev
  **almaz**) → (f) sayımı ve (g)'nin `failed: []` vakası kırmızı; **S-15d:** ilk turu ayar komutundan kaldır (yalnız bakım turu kalsın) → (g)'nin
  "aynı istekte" vakası kırmızı; **S-15e** (r4 düzeltmesi, F-2): `ReleasePendingOrdersCommand` gönderimini `WaveMaintenanceHostedService`'ten
  kaldır → üretim tur yöntemini çağıran (i), (j1) ve (q) kırmızı; **S-15f** (r4, F-2 — r3'teki "replay dalını kaldır" yerine; r5, F-5 beklenen kırmızı
  düzeltildi): 0178 `IssueImmediateAsync` işlemindeki outbound yazımından `PickIssuance.State = Deferred` / `Version` koşulunu kaldır → (j2)'nin
  **"kaybeden replay alır (kazananın `TaskIds`)"** iddiası kırmızı: kaybeden çağrı replay döndürmez — 0178'in tekil `(TenantId, OutboundId, Seq)`
  olay index'i (`WarehouseIndexConfiguration.cs:147-148`; `Seq` işlem dışı okunan outbound'dan, `OutboundShipmentReads.cs:124`) kaybeden işlemin
  `PickTasksIssued` olayını çakıştırır ve çağrı hata alır, görev seti bu **ikinci bekçi** sayesinde tek kalabilir (işlem geri çağrısı outbound'u
  yeniden okuyup ikinci seti yazarsa "tek görev seti" iddiası da kırmızıdır); kabul kaydı kırmızıyı replay iddiasından yazar — "iki görev seti"
  bu sabotajın beklenen kırmızısı değildir; **S-15g** (r4 düzeltmesi, F-2): süzgeçten `PickIssuance.State = Deferred`'i kaldır → O4 sayılır ve tura girer, 0178 portu
  `IssuedToOtherWave` ile reddeder (O4'ün görevleri değişmez) → (f) sayımı ve (g)'nin `failed: []` vakası kırmızı; **S-15h:** `Releasing` üyeliği
  atlamasını kaldır → (l)'de O9 dalgasız açılır (`WaveRef` boş), kırmızı; **S-15i** (F-1): üyelik kapanışını görev açılışından sonraya al (r3
  sırası) → (n1)'de O2 `Issued` iken W1'de `Planned` üyelik kalır, kırmızı ((g)'nin olay sırası bilgidir, bu sabotajın kırmızısı sayılmaz —
  r5, F-6 c); **S-15j** (F-1): `AddWaveOrders` işleminden
  ayar kaydına koşullu yazımı kaldır → (n2)'de üyelik yazılır (yetim `Planned`, O7 `Issued`), kırmızı; **S-15k** (F-1): R1'den ayar kaydına koşullu
  yazımı kaldır → (n3)'te W4 kapalı depoda serbest bırakılır, kırmızı; **S-15l** (F-2): `IssueImmediateAsync`'in replay dalını kaldır → (o1)'de
  ikinci çağrı `Ineligible` döner, kırmızı; **S-15m** (F-3): siparişe özgü hatada turu durdur (r3 davranışı) → (p1)'de P2 ve P3 açılmaz, kırmızı;
  **S-15n** (F-3): bağlantı sınıfı hatayı siparişe özgü say → (p5)'te P2 ve P3 kesinti sırasında açılır, kırmızı; **S-15o** (F-3): tur sonu olayını
  ve merkezi kaydı koşulsuz yaz → (p3)'te olay / kayıt 1, kırmızı; **S-15p** (F-3): takılı siparişi sıranın başına koy → (p2)'nin çağrı sırası
  kırmızı; **S-15q** (F-6): bakım turunda depo başına tek parti gönder → (q)'da Q5 açılmaz, kırmızı; **S-15r** (F-5): `IssueImmediateAsync`'te
  `NotDeferred` denetimini kaldır (`null`'ı `Deferred` gibi ele al) → (o2)'de ikinci görev seti, kırmızı; **S-15s** (F-5): `remainingCount`'a
  `Releasing` üyelerini kat → (l)'de `remainingCount 1`, kırmızı; **S-15t** (F-6): görev bütçesini kaldır → (q)'da ilk tur Q3'ü de açar
  (`issuedCount 3`), kırmızı; **S-15u** (F-1): kapı yazımında `Version`'ı da artır → (n4)'te eski `If-Match`'li eşik değişikliği 409, kırmızı;
  **S-15v** (F-3; r5, F-1 yeniden yazıldı): art arda hata sınırını kaldır → (p6)'nın ilk turu P5'i de dener ve açar (`issuedCount 1`,
  `stopReason` boş), kırmızı; **S-15w** (r5, F-1): sınır durdurmasında tetikleyen siparişleri takılı listeye yazma (r4 davranışı) → (p6)'da
  `stuckOrders = []` ve bakım turu yine P1'den başlayıp P5'e varmadan durur, kırmızı; **S-15x** (r5, F-1): parti başına yeni takılı sınırını
  kaldır → (p7)'de R4 aynı turda açılır, kırmızı; **S-15y** (r5, F-1; r6, F-2 yeniden yazıldı): liste üst sınırını kaldır → (p8)'de T1
  listeye girer (`stuckOrders = [P1, P2, T1]`), kırmızı; **S-15z** (r5, F-2): takılı liste yazımında `Version`'ı +1 yap → (p9)'da eşik değişikliği 409, kırmızı; **S-15aa** (r5, F-2): takılı
  yazımından `WaveManaged = false` filtresini kaldır → (p10b)'de yeniden açılmış depoda `stuckOrders = [Pc]`, kırmızı; **S-15ab** (r5, F-2):
  deltayı işlem içi okumaya değil tur başı (işlem dışı) okumasına uygulayıp bütün diziyi yaz → (p10a)'da Pa ya da Pb kaybolur, kırmızı;
  **S-15ac** (r5, F-2): yeniden açmada liste temizliğini kaldır → (p11)'de `stuckOrders = [P1]`, kırmızı; **S-15ad** (r5, F-2): tur başı
  uzlaştırmayı kaldır (ya da bakım tetikleyicisini yalnız bekleyen siparişe bağla) → (p12)'de P1 listede kalır, kırmızı; **S-15ae** (r5, F-2):
  `Code`'a istisna türünün adını yaz → (p1)'in kategori vakası kırmızı; **S-15af** (r5, F-2): kategori değişimini liste değişikliği say →
  (p3)'ün dördüncü turunda olay / kayıt 1, kırmızı; **S-15ag** (r5, F-3): turun denenmiş kümesini kaldır → (p13)'te P1 turda iki kez denenir,
  kırmızı; **S-15ah** (r5, F-3): tur başına parti sınırını kaldır → (p14)'te R3 aynı turda açılır, kırmızı; **S-15ai** (r5, F-3): takılı yeniden
  deneme bütçesini kaldır → (p15b)'de tur A dördünü dener, kırmızı; **S-15aj** (r5, F-3): dönüşümü kaldır (yalnız `FirstFailedAtUtc` sırası) →
  (p15b)'de tur B yine S1 ve S2'yi dener, kırmızı; **S-15ak** (r5, F-3): takılı yeniden denemeleri art arda hata sayacına kat → (p15a)'da S4
  denenmez, kırmızı; **S-15al** (r5, F-4): adım (ii)'nin kapı yazımını kaldır → (n5)'te yeniden açılmış depoda O2 üyeliği `Removed` olur ve O2
  dalgasız açılır, kırmızı; **S-15am** (r6, F-2): liste doluyken turu durdur (r5 davranışı) → (p8)'de T2 açılmaz, kırmızı; **S-15an** (r6,
  F-6 c): liste-dolu durum bayrağını kaldır (her atlamada `Error` log'u) → (p8)'in ikinci turunda `Error` log'u 2, kırmızı; **S-15ao**
  (r6, F-3; r7 beklenen kırmızı güncellendi): art arda bağlantı durması kuralını kaldır (sayaç ve eşik yok) → (p16a)'nın dördüncü turu yine
  P1'de durur, `stuckOrders = []`, gösterge 4, kırmızı; **S-15ap** (r6, F-3; r7 güncellendi): art arda bağlantı durması göstergesini yazma →
  (p16a)'da gösterge 1 / 2 / 3 ve (p16b)'de 1 / 2 / 3 iddiası kırmızı; **S-15aq**
  (r6, F-1): depolar arası dönüşümü kaldır (depoyu bitirip sonrakine geç) → (p17)'de çağrı sırası E1, E2, F1, kırmızı; **S-15ar** (r6, F-1):
  parti sınırını depo başına değil tur geneline uygula → (p17)'de E2 açılmaz, kırmızı; **S-15as** (r7, F-1): uygun depo kümesini turun başında
  bir kez sabitle (r6 davranışı) → (p18a)'da G2 bu turda açılmaz (çağrı sırası E1, G1, E2, E3), kırmızı; **S-15at** (r7, F-1): koşul (1) ile
  sıradan çıkan depoyu turun sonuna dek dışarıda tut → (p18b)'de F2 bu turda açılmaz, kırmızı; **S-15au** (r7, F-2): liste dolu atlama tavanını
  kaldır → (p19)'un ilk turunda U3 ve U4 de denenir, U4 tur 1'de açılır, `stopReason` boş, kırmızı; **S-15av** (r7, F-2): imleci kaldır (her tur
  takılı olmayanları `CreatedAt` başından al) → (p19)'un ikinci turu yine U1, U2 ile tavana varır, U4 açılmaz, kırmızı; **S-15aw** (r7, F-2):
  liste doluyken sipariş başına `Warning` log'u yaz (r6 davranışı) → (p19)'un ilk turunda `Warning` log'u 2, kırmızı; **S-15ax** (r7, F-3):
  başarı şartını kaldır (her bağlantı durması sayacı artırır) → (p16b)'nin üçüncü turunda P1 takılır (`stuckOrders = [P1]`), kırmızı;
  **S-15ay** (r7, F-3): şüpheli sıralamasını kaldır (turu durduran sipariş sonraki turda yine `CreatedAt` sırasında) → (p16a)'nın ikinci turu
  yine P1'de durur, P2 ve P3 açılmaz, kırmızı; **S-15az** (r7, F-3): eşikte liste doluyken sayacı sıfırla (r6'nın "bağlantı hatasız biten turda
  sıfırla" kuralı) → (p16d)'nin beşinci turunda P1 turu durdurur (`stopReason = ConnectionError`, gösterge 1), kırmızı; **S-15ba** (r7, F-3):
  takılı yeniden denemede bağlantı sınıfı hata turu her zaman durdursun → (p16a)'nın beşinci turunda gösterge 1, kırmızı; **S-15bc** (r7, F-3):
  r6'nın "yalnız turun ilk denediği sipariş sayılır" koşulunu geri getir → (p16c)'de P2'nin `connectionStops` değeri 0 ve (p16a)'da P1 hiç
  takılmaz, kırmızı; **S-15bd** (r8, r7 incelemesi F-2): şüphelileri takılı yeniden denemelerden **önce** sırala (r7 sırası) → (p16e)'nin
  ikinci turunda çağrı sırası yalnız **X** (X turun ilk çağrısı, tur durur), S1 açılmaz, `connectionStops` 0 kalır, kırmızı. Sabotaj (DL-3): **S-15bb** (r7, F-5): iki döngüyü tek döngüde birleştir (uzlaştırma açılış turunun ardından aynı döngüde
  koşar) → (r)'de kanca tutulurken W5 `Picking` kalır, kırmızı.
- [ ] AC-16 (zorunlu) — 0178 verisine yazılmaz (§2.3 kural 2): mimari test 0180 üretim kodunda (`WavePlanning` / `Waves` / `WaveExceptions` /
  `WaveKpis` klasörleri) `IOutboundShipmentRepository`, `IWarehouseTaskRepository` referansı ve `WarehouseCollections.*` üzerinde `Find` dışı
  koleksiyon çağrısı bulmaz; 0180 hiçbir `OnHand` / `Available` / `Reserved` özelliği taşımaz. S-16a: bir 0180 deposuna `wms_tasks` üzerinde
  `UpdateOneAsync` ekle → kırmızı; S-16b: handler'a `IWarehouseTaskRepository` enjekte et → kırmızı.
- [ ] AC-17 (zorunlu) — `SingleSystemOfRecordArchitectureTests` `wms_wave_*` koleksiyonlarını açıkça bildirir (X-9). S-17: bildirilmemiş
  `wms_wave_x` koleksiyonu aç → kırmızı.
- [ ] AC-56 (zorunlu; DL-1'de açılır, DL-3 ve DL-5 kabulünde tekrarlanır — X-15) — Rol İzinleri: manifestin bildirdiği her
  `warehouse.waves.*` anahtarının fiili ve satır adı 7 dilde söylenir; `frontend/Diten.Web/tests/role-assignments-label-ledger.test.js` yeşil,
  defter tavanı (`docs/roadmap/backlog/role-permission-label-ledger.md` "fiil 0 · satır 96") **değişmez** (0180 deftere satır eklemez); DL-3'te
  `recall` köprüde, 7 resx'te ve bir `ACTION_FAMILIES` ailesinde. S-56: `RoleAssignmentsIndex.ar.resx`'ten `ActionVerb_Recall`'ı sil (DL-3) ya da
  `SharedResource.ru.resx`'ten `Nav.Page.*` karşılığı olmayan bir 0180 `Perm.Entity.*` satır adını (beklenen `WAVESSETTINGS`, çıkarım) sil
  (DL-1) → vitest kırmızı.

**DL-2 — serbest bırakma + rota + MOD-0178 seam'i**

- [ ] AC-18 (zorunlu) — 0178 varsayılan davranışı değişmez: `WaveManaged = false` (ya da ayar yok) depoda outbound yaratma çıktısı (outbound
  belgesi, pick görevleri, olaylar) X-1…X-5 öncesiyle **alan alan aynı**. **Karşılaştırma referansı (F-14):** X-1…X-5 öncesi commit'te
  (DL-2 dilim başı) aynı fikstür betiğiyle üretilen çıktı — kimlik / zaman alanları normalize edilmiş JSON + sha256 — **kabul kaydına
  yazılır**; dilim sonu çıktısı aynı betikle üretilir ve sha256 eşitliği kayıtta gösterilir. Bu karşılaştırma **kanıttır, bekçi değildir**:
  0180 test ağacında saklanan bir kopya referans sayılmaz (kendi kopyasıyla ölçme). Bekçi, 0178'in mevcut testleridir: 0178 test dosyaları
  değişmez (`git diff --stat … -- INVT/Warehouse` boş) ve tam paket yeşil. `WaveManaged = true` depoda outbound `Allocated` + `PickIssuance.State = Deferred`,
  `wms_tasks`'ta o outbound'un görevi **yok**. S-18: politika kontrolünü ters çevir (her depoda ertele) → 0178'in mevcut S2 testleri kırmızı.
- [ ] AC-19 (zorunlu) — Görev üretimi: serbest bırakma sonrası her `Issued` siparişin `(AllocSeq, LocationId)` plan öğesi için tam **bir** pick
  görevi; `PlannedQuantity` = `PlannedBins.quantity`, `FromLocationId`, `ToLocationId` (Staging), `ReservationId` 0178 `PickTask` kuruluşuyla aynı (X-3 (a): `OutboundAllocationService` ile `WavePickIssuanceService` **aynı** ortak
  kurucuyu çağırır — mimari test: `WavePickIssuanceService` kendi `WarehouseTask` nesne başlatıcısını içermez);
  `WaveRef { WaveId, WaveNumber, Sequence }` dolu; görevler 0178 Toplama Görevleri listesinde görünür ve 0178 onay makinesiyle onaylanır (bir
  onay `Posted`). S-19: portta `Items` küme eşitliği denetimini kaldır, eksik öğeyle çağır → eksik görev, kırmızı; S-19b: `WavePickIssuanceService`'e
  kendi `new WarehouseTask { … }` kuruluşunu yaz → mimari test kırmızı.
- [ ] AC-20 (zorunlu) — Sipariş düzeyinde atomiklik: 0178 işleminde outbound `PickIssuance` güncellemesi, görevler ve olaylar birlikte yazılır;
  görev yazımı başarısızsa outbound `Deferred` kalır. S-20: görev eklemeyi işlem dışına al → enjekte hatada outbound `Issued`, görev yok, kırmızı.
- [ ] AC-21 (zorunlu) — İdempotent serbest bırakma ve kurtarma: (a) aynı istek iki kez → tek görev seti; (b) port 2. siparişte zaman aşımı → 503
  `WAVE_RELEASE_OUTCOME_UNKNOWN`, dalga `Releasing`; bakım kilit dolunca tamamlar → görev sayısı = plan öğesi sayısı, ikinci görev yok;
  (c) R1'den sonra süreç ölümü benzetimi → bakım aynı `PickPlan`'la tamamlar, `Sequence` değişmez. S-21a: port replay dalını kaldır → (b)'de çift
  görev, kırmızı; S-21b: kurtarmada rotayı yeniden hesapla (LOCATION verisi değiştirilmiş kurguda) → `Sequence` farkı, kırmızı.
- [ ] AC-22 (zorunlu) — Ön kontrol ve kısmi hata: (a) ön kontrolde uygun olmayan sipariş → 422 `WAVE_ORDERS_INELIGIBLE`, yazım yok; (b) ön kontrol ile
  port arasında 0178'de iptal edilen sipariş → `Excluded` + `OrderCancelledInWave` istisnası, diğerleri `Issued`, dalga `Released`; (c) hiçbiri
  açılamazsa dalga `Draft`, 422 `WAVE_NOTHING_ISSUED`, bu denemenin `Excluded` üyelikleri `Removed` (`CloseReason = ReleaseAborted`) — Taslakta
  `Excluded` üyelik kalmaz (F-7, §8.6 adım 5). S-22: `Ineligible` sonucunu teknik hata gibi ele al → (b)'de dalga `Releasing` kalır, kırmızı.
- [ ] AC-23 (zorunlu) — Sınır onayı (D-7): sınır aşımlı dalgada `confirmLimitOverride` yok → 409 `WAVE_LIMIT_CONFIRMATION_REQUIRED`, yazım yok;
  `true` → 200. S-23: onay denetimini kaldır → kırmızı.
- [ ] AC-24 (zorunlu) — Rota belirlenimciliği: aynı plan öğeleri 100 rastgele karışık sırada verildiğinde `Sequence` her seferinde aynı; eşit bin
  kodlu öğelerde eşitlik bozma `OutboundId` → `AllocSeq`. S-24: sıralamadan eşitlik bozmayı kaldır → kırmızı.
- [ ] AC-25 (zorunlu) — Rota yöntemi (D-2): fikstür F1 — tek bölge, koridor A…D, her koridorda raf 01…10, her rafta bir görev (40 öğe) →
  (a) her `(bölge, koridor)` çifti tek ardışık blok; (b) blok sırası A, B, C, D; (c) raf yönü A artan, B azalan, C artan, D azalan;
  (d) `ProxyDistance = 39` (koridor içi 4 × 9 + koridor geçişi 3 × 1); aynı fikstürde kod sırası (koridor artan, raf artan) **66**'dır
  (4 × 9 + 3 × (1 + 9)) — rota ondan kısa. Fikstür F2 — iki bölge (`A`, `B` kodlu Zone) → önce `A`'nın bütün öğeleri. S-25: yön değiştirmeyi kaldır →
  (c) ve (d) kırmızı (66).
- [ ] AC-26 (zorunlu) — Geri düşüş: biçime uymayan kodlu bin'ler (`DOCK-1`, `COLD/7`) bölgenin ayrıştırılmış öğelerinden sonra kod sırasıyla;
  `RouteFallback = true`, `FallbackCount` doğru; Zone'suz yol `StorageLocation` koduyla gruplanır. S-26: geri düşüş öğelerini başa koy → kırmızı.
- [ ] AC-27 (zorunlu) — Performans bütçesi (§8.7): (a) 2.000 öğe sıralaması p95 ≤ 200 ms (test eşiği 600 ms); (b) 200 sipariş / 2.000 görev serbest
  bırakma ≤ 15 s (eşik 45 s); (c) 5.000 uygun outbound'da öneri ≤ 1 s (eşik 3 s); (d) **(r4, F-6)** dalga yönetimini kapatma isteğinin istek
  içi süresi — C1 + ilk tur, depoda 200 sipariş / 2.000 görev bekleyen (ilk tur bütçesinin tamamı; 201. sipariş `remainingCount`'ta);
  **en kötü durum (r5, F-6 e):** fikstürde 200 siparişin **hepsi** bir Taslak dalgada `Planned` üyeliklidir (her sipariş için adım (ii)'nin 0180
  işlemi + kapı yazımı ve (iii)'ün 0178 işlemi — sipariş başına iki işlem); Taslak üyeliksiz fikstür ölçümü bu vakanın yerine geçmez — ≤ 15 s
  (eşik 45 s; uç düzeyinde ölçülür, HTTP isteği başından cevaba); ölçümler kabul kaydına sayıyla yazılır. S-27: sıralayıcıya karşılaştırma
  başına LOCATION okuması ekle → (a) eşiği aşar, kırmızı. (Bütçe sınırlarının kendisi — 200 sipariş / 2.000 görev — AC-15 (q) ve S-15t ile
  bekçilenir; (d) süre ölçümüdür.)
- [ ] AC-28 (zorunlu) — Kesin saklama: `PickPlan` ve `RouteSummary` serbest bırakmada bir kez yazılır, sonra hiçbir yol değiştirmez; ayrıntı ekranı
  "Önerilen yol" bunu gösterir. S-28: ayrıntı okumasında rotayı yeniden hesapla → LOCATION değiştirilmiş kurguda fark, kırmızı.

**DL-3 — ilerleme + istisnalar + geri çekme**

- [ ] AC-29 (zorunlu) — İlerleme (D-5): fikstür — 3 sipariş, 6 görev; 4 görev terminal, 1 outbound `Packed`, 1 `Shipped` → toplama %67 (4/6),
  paketleme %67 (2/3), sevk %33 (1/3); 0180 bu okumada hiçbir koleksiyona yazmaz. S-29: paketleme sayımından `Shipped`'i çıkar → %33, kırmızı.
- [ ] AC-30 (zorunlu) — Durum uzlaştırması: ilk onayla `Picking`; bütün görevler terminal + geri çekme yok → `Completed`, `CompletedAt` = son
  görevin terminal olay zamanı (uzlaştırma zamanı değil); yazacak şey yoksa komut yazmaz (merkezi kayıt 0). **Y-2 (karar 03):** bir üyelik
  `Excluded` (serbest bırakmada) ya da `OrderCancelled` (0178 iptali) iken kalan `Issued` siparişlerin görevleri terminal → `PartiallyCompleted`
  (`Completed` değil); yalnız kısa toplama varken `Completed` (Q-10 = A). **F-7 vakası:** serbest bırakma denemesi hiç görev açamaz (tek
  üyesi `Excluded` → `Removed`, dalga `Draft`), kullanıcı yeni sipariş ekleyip yeniden serbest bırakır, bütün görevler terminal → dalga
  **`Completed`** (eski denemenin üyeliği `PartiallyCompleted` yapmaz). S-30: `CompletedAt`'i şimdiye bağla → kırmızı; S-30b: kısmi koşuldan
  `Excluded` / `OrderCancelled`'ı çıkar → dalga `Completed` olur, Y-2 vakası kırmızı; S-30c: `ReleaseAborted`'da `Excluded` → `Removed`
  çevirisini kaldır → F-7 vakasında dalga `PartiallyCompleted`, kırmızı.
- [ ] AC-31 (zorunlu) — Geri çekme (D-9): 3 `Issued` sipariş, biri başlamış → geri çekme 200; iki sipariş `Withdrawn`, görevleri 0178'de
  `Cancelled` (`Data.waveRecalled = true`), outbound'ları `Deferred` ve planlayıcıda yeniden uygun; başlamış sipariş dalgada; gerekçe olayda; gerekçesiz
  400. Başlamışın görevleri bitince dalga `PartiallyCompleted`. S-31a: `Started` denetimini kaldır → başlamış siparişin açık görevi iptal olur,
  kırmızı; S-31b: `Refused` onayı "başlamış" say → `StockMissing`'li sipariş çekilemez, kırmızı.
- [ ] AC-32 (zorunlu) — Kesim (D-9): kesim geçen ve bitmemiş dalga `Picking` kalır (otomatik kapanış yok), tek `CutoffMissed`; ikinci tur ikinci
  istisna açmaz. S-32: `SubjectKey` tekilliğini kaldır → iki istisna, kırmızı.
- [ ] AC-33 (zorunlu) — Riskli dalga (D-6): kesime 60 dk, toplama %50 → riskli; kesime 60 dk, %80 → değil; kesime 120 dk, %50 → değil; depo ayarı 150 dk
  → 120 dk / %50 riskli. S-33: eşiği sabite bağla → ayar vakası kırmızı.
- [ ] AC-34 (zorunlu) — Bekleyen görev (D-11): `IssuedAtUtc + 31 dk`'da `Open` görev → `TaskWaitingLong`; görev başlayınca bir sonraki turda
  `Resolved` + `SystemCleared` (olay `ExceptionCleared`); ayar 45 dk iken 31 dk'da istisna yok. S-34: kendiliğinden kapanmayı kaldır → kırmızı.
- [ ] AC-35 (zorunlu) — Geri çağırma (D-10): `LOT_UNDER_RECALL` ile `Refused` onay → `LotUnderRecall` istisnası; sipariş dalgada **kalır**, görev
  değişmez; vardiya sorumlusu "Siparişi çıkar" ile çıkarır. S-35: tespitte otomatik geri çekme çağır → kırmızı.
- [ ] AC-36 (zorunlu) — İstisna eylemleri: çıkar (başlamamış → `Withdrawn`; başlamış → 409 `WAVE_ORDER_STARTED`); taşı (D-13) — hedef verilmezse aynı
  depoda kesimi en yakın gelecek `Draft` dalga, verilirse o; hedef yok → 422 `WAVE_TARGET_INVALID`; başka depo hedefi 422; yok say (`Ignored`,
  gerekçe); her eylem `exceptions.resolve` ister. S-36: varsayılan hedef seçiminde kesim sıralamasını ters çevir → kırmızı.
- [ ] AC-37 (zorunlu) — 0178'de outbound iptali: `Issued` siparişin outbound'u 0178'de iptal edilir → sonraki turda üyelik `OrderCancelled`,
  istisna `OrderCancelledInWave`, toplama ilerlemesi iptal edilen görevleri terminal sayar. S-37: uzlaştırmada outbound durum okumasını kaldır →
  kırmızı.
- [ ] AC-38 (zorunlu) — Kısa toplama: 0178 kısa kapatması → `ShortPick` istisnası; dalga diğer koşullar tamamsa `Completed` (Q-10). S-38: kısa
  kapatmayı terminal saymayan koşul → dalga `Picking` kalır, kırmızı.

**DL-4 — ekranlar (7 dil)**

- [ ] AC-39 (zorunlu) — `Views/Warehouse/{Waves,WavePlanner,WaveExceptions}/*.cshtml` (ve `Waves/Details.cshtml`) dosyalarının her birinde
  `Layout = "_LayoutTenantShell"` açık (`WavePlanningL10nAndHelpTests`; emsal `CycleCountL10nAndHelpTests.cs:235`). `WaveKpis` performans sayfası
  DL-5'tir; Layout vakası AC-54'tedir (F-3). S-39: `Waves/Index.cshtml`'den `Layout` satırını kaldır → kırmızı.
- [ ] AC-40 (zorunlu; araç kontrolü — bekçi değil) — `verify_datatable_page.py . --area Warehouse --module Waves` ve `--module WaveExceptions` PASS; `data_mode: server` beyanla
  aynı; satır eylemi "İptal" (Taslak), "Sil" yok (G-0180-01).
- [ ] AC-41 (zorunlu) — 7 dil (D-15): her resx anahtarı en, tr, fr, es, zh, ar, ru'da (parite testi); `ar` sağdan sola; kullanıcıya görünen
  hardcoded metin yok; hata kodu ve istisna türü metinleri 7 dilde. S-41: bir anahtarı `ru`'dan sil → parite kırmızı.
- [ ] AC-42 (zorunlu) — UAS-001: izni olmayan kullanıcı pano, ayrıntı, planlayıcı ve istisna kuyruğu sayfalarında başlık / kart / boş
  tablo / düğme görmez; tek açıklama; yönlendirme yok (performans sayfası DL-5, AC-54 — F-3). S-42: izin denetimini kaldırıp iskeleti çiz → kırmızı.
- [ ] AC-43 (zorunlu; tarayıcı kontrolü — bekçi değil) — Durumlar: her sayfada yükleniyor, hata ("Stok servisine ulaşılamadı."), boş ("Bu depoda henüz dalga yok", "Dalgaya girmemiş
  sipariş yok", "Açık istisna yok"), çakışma ("Kayıt değişti, yenileyin.") gerçek uç cevaplarıyla tetiklenir (503 / boş liste / 409) ve doğru
  metni gösterir (tarayıcı testi).
- [ ] AC-44 (zorunlu; canlı ölçüm — bekçi değil) — 375 px: pano ve istisna kuyruğu yatay sayfa kaydırmasız (canlı ölçüm `document.scrollingElement.scrollWidth ≤ 375`);
  planlayıcı salt okunur liste + "Ekle" düğmesiyle çalışır.
- [ ] AC-45 (zorunlu) — Planlayıcı klavye ve sürükle-bırak: yalnız klavyeyle (Tab / Enter / Shift+Enter / Delete) bir sipariş eklenir ve çıkarılır;
  sürükle-bırak aynı uca gider; sonuç `aria-live`'da okunur; Allocating satırı gri, eklenemez, nedeni yazılı. S-45: "Ekle" düğmesini kaldır →
  klavye testi kırmızı.
- [ ] AC-46 (zorunlu; tarayıcı kontrolü — bekçi değil) — Kesim gösterimi (D-3): tarayıcı saat dilimi depodan farklıyken iki saat birden; aynıyken tek saat.
- [ ] AC-47 (zorunlu; tarayıcı kontrolü — bekçi değil) — Ağırlık / hacim (D-12) değer yokken "—"; risk / durum / istisna türü ikon + metinle (yalnız renk değil).
- [ ] AC-48 (zorunlu) — Manifest sayfaları (`WAVES`, `WAVEDETAIL`, `WAVEPLANNER`, `WAVEEXCEPTIONS`) ve her gerçek düğme için bir eylem;
  `ApiPermissions` = `warehouse.waves.settings.configure` (Q-6 = B; DL-1'den beri);
  `python3 scripts/check_pack_nav_keys.py` PASS; `delete` / `bulk-delete` eylemi yok. S-48: manifestte `warehouse.waves.delete` bildir → AC-5
  uyum testi kırmızı.
- [ ] AC-49 (zorunlu) — Sayfa yardımı (UXH-001): `Help.{KOD}.Purpose`, `.Audience`, `.Step{n}Title` / `.Step{n}` (§25) 7 dilde; tur yok
  (`WavePlanningL10nAndHelpTests`, `PageHelpResource.{7}.resx`'i okur). S-49: `PageHelpResource.zh.resx`'ten `Help.WAVES.Step2`'yi sil → kırmızı.
- [ ] AC-50 (zorunlu — Q-12 = A, karar 03; XMC X-7) — 0178 Toplama Görevleri listesinde "Dalga" sütunu ve `waveId` süzgeci (API + Web proxy
  beyaz listesi `PickTasksController.cs:118`); **`orderBy=waveSequence`** (0178'in parametresi `orderBy` — r2'deki `sort` yanlıştı, F-1) dalga
  sırasını verir, sözleşme enum'u (`wms-bundle.openapi.yaml:406`) değeri içerir; Web proxy üzerinden `waveId` ile açılan liste dalga sırasıyla
  gelir; Web liste satırı (`Row`, `PickTasksController.cs:332`) `wave` alanını `WmsPickTaskDto.Wave`'den (`OutboundDtos.cs:169`, eklemeli — r4,
  F-4) taşır; **CSV (r4, F-4):** `waveId` ile alınan dışa aktarım (`PickTasksController.cs:79`, aynı `ListQuery` `:109`) satırları dalga
  sırasındadır, sütunları (`:97`) değişmez; `waveId` olmadan CSV sırası `-createdAt` kalır; varsayılan sıralama (`-createdAt`) ve 0178 testleri
  değişmez. S-50: sıralamayı `Number`'a bağla → dalga sırası testi kırmızı; S-50b: Web proxy beyaz listesinden `waveId`'yi çıkar → proxy vakası
  süzgeçsiz liste döner, kırmızı; S-50c (r4): `ListQuery`'de `waveId` verilince de `orderBy=-createdAt` yaz → liste ve CSV dalga sırası vakası
  kırmızı.

**DL-5 — performans + CSV**

- [ ] AC-51 (zorunlu) — KPI formülleri (§8.16) fikstürle: 3 günde 5 dalga (bilinen `ReleasedAt` / `CompletedAt` / `CutoffAtUtc`, iki toplayıcı) →
  günlük sayı, ortalama süre, kesime yetişme oranı ve satır / saat beklenen değerlerle aynı; günler deponun yerel gününe göre (gece yarısı sınırı
  vakası). S-51: günü UTC'ye göre kovala → sınır vakası kırmızı.
- [ ] AC-52 (zorunlu) — Aralık: 92 gün 200, 93 gün 400 `WAVE_KPI_RANGE_TOO_LARGE`; depo zorunlu. S-52: üst sınırı kaldır → kırmızı.
- [ ] AC-53 (zorunlu) — CSV: `kpis.export` ister (yoksa 403); içerik JSON serisiyle satır satır aynı; `Accept-Language: tr` başlığı Türkçe, `ar`
  Arapça; sayılar değişmez kültürde; başka kiracının verisi yok. S-53a: CSV yolundan `[HasPermission]` kaldır → `AuthorizationGuardTests` kırmızı;
  S-53b: sayıları istek kültürüyle yaz (`tr` → virgül) → kırmızı.
- [ ] AC-54 (zorunlu) — Performans menüde **ayrı sayfa** (`WAVEKPIS`, nav, D-16); `Views/Warehouse/WaveKpis/*.cshtml`'in her birinde
  `Layout = "_LayoutTenantShell"` açık (`WavePlanningL10nAndHelpTests`'in DL-5 vakası — AC-39'dan taşındı, F-3); UAS-001 (izinsiz kullanıcı
  başlık / grafik / tablo / CSV düğmesi görmez, tek açıklama, yönlendirme yok); `check_pack_nav_keys.py` PASS. S-54: Web `WaveKpisController`'ın
  sayfa eyleminden izin denetimini kaldır → izinsiz kullanıcı iskeleti görür, bu AC'nin UAS-001 vakası kırmızı; S-54b: `WaveKpis/Index.cshtml`'den
  `Layout` satırını kaldır → Layout vakası kırmızı.

**DL-6 — Gateway kapsama testi**

- [ ] AC-55 (zorunlu; **kapsama testi DL-6'da, DL-6 DL-5 kabulünden sonra** — r9; **0178 sayı vakası DL-1, DL-2, DL-3 ve DL-5 kabulünde** —
  X-14, r8) —
  `gateway/Diten.ApiGateway.Tests/Mod0180WaveRouteTests.cs` 0180'in her ucunu (uç listesi üretim
  `INV/Api/Controllers/WavePlanning/` kaynağından ve WMS-WAVE-BUNDLE taslağından okunur, testte elle yazılmaz) **üretim** `ocelot.json`'un
  (`Diten.ApiGateway.Tests.csproj:26` bağlı kopyası) `/api/warehouse/{everything}` rotasıyla 5067'ye tam bir rotada yönlendirildiği haliyle
  doğrular; `ocelot.json` değişmez. **0178 sayı vakası (r8, r7 incelemesi F-1 — r7'nin "`Mod0178WarehouseRouteTests.cs` değişmez, 0178 testi
  değişmeden yeşil" yazımı yanlıştı; X-14, XMC-001, sahip onayı `c7dc4ea6a` + kapsam teyidi `6235794ab`, r9):** 0178 testi 0180 controller'larını da tarar ve `:97`'de uç
  sayısını sabitler. Uç ekleyen her dilimin (DL-1, DL-2, DL-3, DL-5) kabulünde Gateway test projesi koşar ve
  `Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route` **dilim başında ölçülen sayı +
  dilimin eklediği 0180 uç sayısı** ile yeşildir (taban dilimin başlangıç commit'inde ölçülür, önce → sonra kabul kaydına yazılır ve kayıt `Assert.Equal(<n>, endpoints.Count)` satırına
  atıf yapar, satır numarasına değil — r9; paket sabit
  taban yazmaz — X-10 deseni; beklenen dağılım X-14'te, çıkarım); `:97` güncellemesi dilimin controller uçlarıyla **aynı commit'tedir** (arada
  kırmızı pencere yok); `git diff <dilim-başı>..<dilim-sonu> -- gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` yalnız `:97` ve
  üstündeki uç yorumudur (integration-agent). **S-55c** (r8; her uç ekleyen dilimde): bir 0180 controller'ına
  `[HasPermission(WavePermissions.Read)]` taşıyan geçici `[HttpGet("waves/count-sabotage")]` eylemi ekle → 0178 testi `:97`'de kırmızı (taranan
  uç sayısı güncellenmiş değerden bir fazla; beklenen kırmızı yalnız budur); `Mod0180WaveRouteTests` uç ekleyen dilimlerde henüz yoktur (DL-6
  DL-5'ten sonra — r9) — listede olmayan her kırmızı kabul kaydında adıyla yazılır; geri alınır, commit'e girmez.
  **Uç listesinin toplanması (F-8):**
  `INV/Api/Controllers/WavePlanning/` klasöründeki **her** controller sınıf `[Route]` önekinden **bağımsız** alınır — 0178 testinin
  `api/warehouse` önek süzgeci (`Mod0178WarehouseRouteTests.cs:228`) kopyalanmaz; klasörden okunan uç sayısı WMS-WAVE-BUNDLE taslağındaki işlem
  sayısına eşit olmalıdır (eşit değilse kırmızı). **Sıra (r9, r8 incelemesi F-1):** AC-1 taslağı DL-1'de §3'ün 23 ucunu yazar, klasörde ise
  DL-1 / DL-2 / DL-3 sonrası 14 / 16 / 21 uç vardır; eşitlik ancak DL-5 sonrası sağlanır, bu yüzden DL-6 **DL-5 kabulünden sonra** başlar
  (§23, §24; taslağı dilim etiketiyle sınırlama yolu seçilmedi). Böylece rotası değişen controller listeden düşmez. Sabotajlar **üretimi** bozar
  (integration-agent, geri alınır; F-5): S-55a: `gateway/Diten.ApiGateway/ocelot.json`'da `/api/warehouse/{everything}` bloğunun
  `Port`'unu (`:1158`) geçici 5068 yap → kırmızı; S-55b: bir 0180 controller'ının sınıf `[Route]`'unu geçici `api/waves` yap → o uçlar
  hiçbir rotaya eşleşmez, kırmızı.

**AC sayısı: 56** (hepsi zorunlu; r1'deki koşullu AC-50 Q-12 = A ile zorunlu oldu; AC-56 r2'de eklendi — X-15). r3'te AC eklenmedi ve
silinmedi; değişen: AC-12, AC-15 (yeniden yazıldı), AC-22, AC-30, AC-39, AC-42, AC-50, AC-54, AC-55. r4'te de AC eklenmedi ve silinmedi
(AC-1…AC-56 boşluksuz); değişen: AC-15 ((c) bilgi işareti, bakım turunun üretim tur yöntemiyle sürülmesi, (g), (j), (l) genişledi; yeni (n),
(o), (p), (q); S-15b / S-15e / S-15f / S-15g düzeltildi; yeni S-15i…S-15v), AC-27 ((d)), AC-50 (Web DTO, CSV sırası, S-50c). r5'te de AC
eklenmedi ve silinmedi (AC-1…AC-56 boşluksuz); değişen: AC-15 ((g) olay sırası bilgi, (n3) metni, (p1) kategori ve metrik adları, (p3)
kategori değişimi, (p6) yeniden yazıldı; yeni (n5), (n6), (p7)…(p15); S-15f beklenen kırmızısı, S-15i daraldı, S-15v yeniden yazıldı; yeni
S-15w…S-15z, S-15aa…S-15al), AC-27 ((d) en kötü durum fikstürü), AC-5 (alt ad alanı sahipliği vakası + S-5c — CT eki, CT kararı, sahip
onayı bekliyor). r6'da da AC eklenmedi ve silinmedi (AC-1…AC-56 boşluksuz); değişen: AC-5 (S-5c yeniden yazıldı, yeni S-5d; vaka sahip
onaylı 2026-11-13), AC-15 (bakım turu `RunPendingReleaseOnceAsync` ile sürülür; (p8) yeniden yazıldı, (p10b) olay / kayıt, (p14)
`RoundBudgetReached`; yeni (p16), (p17); S-15y yeniden yazıldı; yeni S-15am…S-15ar). r7'de de AC eklenmedi ve silinmedi (AC-1…AC-56
boşluksuz); değişen: AC-5 (kapsam cümlesi daraltıldı; S-5c'nin eylem yeri `PICKTASKS` / `PickTasksController` `GET` ve beklenen ek kırmızıları
adıyla), AC-15 (başlıkta (r) DL-3; (p8) özet log; (p16) yeniden yazıldı — (p16a)…(p16d); yeni (p18), (p19), (r); S-15ao, S-15ap güncellendi;
yeni S-15as…S-15az, S-15ba, S-15bb, S-15bc). r8'de de AC eklenmedi ve silinmedi (AC-1…AC-56 boşluksuz); değişen: AC-5 (S-5c'nin geçici
`GET` eylemine sabit şablon `wave-sabotage`, (iii) ve (iv) bu rotayla, (iv)'te sabit 73 yok), AC-15 (şüpheliler takılı yeniden denemelerden
sonra; yeni (p16e); yeni S-15bd), AC-55 (0178 sayı vakası DL-1, DL-2, DL-3, DL-5 kabulünde — X-14; yeni S-55c). r9'da da AC eklenmedi ve silinmedi (AC-1…AC-56
boşluksuz); değişen yalnız AC-55 metni (DL-6 DL-5 kabulünden sonra; X-14 kapsam teyidi `6235794ab`; kabul kaydı `Assert.Equal` satırına
atıf); S adı eklenmedi. r10'da da AC eklenmedi ve silinmedi (AC-1…AC-56 boşluksuz); değişen yalnız AC-5 (INVENTORY-LEDGER bütünlük vakası —
X-17; yeni S-5e).

## 17. Test Expectations
- **Birim (Domain, saf):** durum makinesi (§8.3 tablosu tam), rota (S-biçimi, ayrıştırma, geri düşüş, eşitlik bozma, ölçü), saat dilimi dönüşümü
  (boşluk / çakışma), risk, sınır uyarıları, istisna tespit koşulları, KPI formülleri.
- **Entegrasyon (Mongo replika seti):** InventoryService test paketi `INVENTORY_TEST_MONGO` yoksa kendi replika setini başlatır (MVP-1 ortam notu);
  AC-2…AC-38 gerçek MOD-0178 koduyla (outbound yaratma → tahsis → serbest bırakma → 0178 onayı; r3: dalga yönetimini kapatma → dalgasız
  görev açılışı, AC-15 f…q); sahte port ya da gerçek portun üstünde hata sarmalayıcısı yalnız zaman aşımı / hata enjeksiyonunda (AC-15 j, p;
  AC-21, AC-22); AC-15 (i) yarışı için tahsisin politika okuması ile yazımı arasına test kancası. **r4 test kancaları ve sürüş (F-1…F-3):**
  bakım turu her vakada üretim `WaveMaintenanceHostedService`'in tek tur yöntemiyle sürülür (komut doğrudan gönderilmez); kapatma turunun bir
  siparişteki iki yazımı arasına kesme kancası (AC-15 n1); `AddWaveOrders` ve `ReleaseWave` ön kontrolü ile işlemi arasına bekletme kancası (n2,
  n3); iki turu aynı siparişte aynı anda 0178 işlemine sokan kanca (j2); port düzeyi vakaları gerçek 0178 `WavePickIssuanceService`'e karşı (o);
  bütçe vakası test yapılandırmasıyla küçük değerlerle (q). Log ve metrik iddiaları test log toplayıcısı ve OTel test dinleyicisiyle ölçülür (p).
  **r5 kancaları (F-1…F-4):** adım (i) ile (ii) ve (ii) ile (iii) arasına bekletme kancası (n5, n6); iki bakım tur örneğini aynı anda koşturup
  takılı yazımına birlikte sokan kanca ve parti ile takılı yazımı arasına bekletme kancası (p10); sınır, bütçe ve tur değerleri test
  yapılandırmasıyla küçültülür (`PendingReleaseMaxNewStuckPerBatch`, `PendingReleaseStuckMax`, `PendingReleaseMaxBatchesPerTour`,
  `PendingReleaseStuckRetryPerTour` — p7, p8, p13…p15); sarmalayıcı sipariş başına çağrı sayısını ve sırasını kaydeder (p13, p15). Metrik
  adları üretim sabitlerinden okunur, testte elle yazılmaz. **r6 (F-1…F-3):** `PendingReleaseConnectionStopsBeforeStuck` varsayılanıyla
  ardışık turlar (p16); iki kapalı depoda çağrı sırasını depo etiketiyle kaydeden sarmalayıcı (p17); log seviyeleri (`Error` / `Info`) test log
  toplayıcısıyla (p8). **r7 (F-1…F-3, F-5):** bakım turunu geçişler arasında bekleten kanca ve kanca beklerken başka bir depoyu kapatan ya da
  fikstür yazan adım (p18); `PendingReleaseListFullSkipsPerTour` test yapılandırmasıyla küçültülür, özet `Warning` log'unun satır sayısı ve
  atlanan sayısı test log toplayıcısıyla (p19); bağlantı durması log'unun `connectionStops` / `priorSuccessInTour` alanları ve turlar arası
  fikstür yazımı (p16); üretim `WaveMaintenanceHostedService` test host'unda iki döngüsüyle (kısa aralıklar) ve 0178 çağrısında bekletme kancası
  (r, DL-3). **r8 (F-2):** takılı bir siparişin hata enjeksiyonunu turlar arasında kaldırma ve sarmalayıcının tur başına çağrı sırası (p16e).
- **MOD-0178 davranışı:** X-1…X-5'in ölçümü 0180 test ağacında; 0178 test dosyaları değişmez (AC-18). Servise dokunulduğunda **bütün**
  InventoryService paketi koşar (filtre yok — MVP-1 CT kuralı).
- **Mimari:** AC-5 (`AuthorizationGuardTests`, `WarehouseRegistrationTests` değişmeden; r10: 0180 manifest bütünlüğü +
  `InventoryLedgerManifestCompletenessTests` yalnız X-17 dışlama satırıyla, S-5e), AC-8 (`LedgerImmutabilityArchitectureTests` değişmeden),
  AC-9 (`CentralAuditDeclarationTests`), AC-16 (0180 yazma yasağı taraması), AC-17 (`SingleSystemOfRecordArchitectureTests`).
- **Sabotaj kanıtı:** §16'daki her `S-n` kabul kaydında komut + kırmızı çıktı + geri alma ile.
- **Frontend:** `verify_datatable_page.py` (AC-40), RESX parite 7 dil (AC-41), UAS-001 (AC-42), durumlar (AC-43), 375 px canlı (AC-44), klavye
  (AC-45), tarayıcı smoke (E3): dalga oluştur → öner → ekle → serbest bırak → 0178'de toplama onayı → ilerleme → geri çek → istisna çöz → KPI + CSV.
- **Rol İzinleri (X-15, AC-56):** `npx vitest run tests/role-assignments-label-ledger.test.js` (Web, `frontend/Diten.Web`) manifest anahtarı
  bildiren her dilimde (DL-1, DL-3, DL-5) koşar; Web `vitest` paketinin tamamı DL-4 / DL-5'te.
- **Build:** `Diten.InventoryService`, `Diten.Web` PASS; Gateway test projesi (AC-55) PASS — üretim `ocelot.json`'u okur (§15); r8: Gateway
  test projesi uç ekleyen her dilimin kabulünde (DL-1, DL-2, DL-3, DL-5 — 0178 sayı vakası, X-14) ve DL-6'da (DL-5 kabulünden sonra — r9)
  koşar.
- **Sözleşme:** WMS-WAVE-BUNDLE taslağına karşı sözleşme testi (AC-1).

## 18. Ready-for-dev Checklist
- [x] Golden Reference Slim referans alındı; form alanı 8 → slim (§4.1; Q-1 = A, karar 03); sapma G-0180-01 adıyla §10'da
- [x] Frontmatter zorunlu alanları dolu (service, shell, golden_reference, data_mode, screens, entity_base)
- [x] Layout & Shell Contract'ta `_LayoutTenantShell` açıkça yazılı (§9, AC-39 + S-39)
- [x] Backend File Convention Golden Reference ile birebir (§10)
- [x] Frontend File Contract Slim dosya listesi tam; Compact dosyası yok (§11)
- [x] Validation Rules her alan için yazılı (§12)
- [x] Failure Path en az 4 senaryo (duplicate, missing, unauthorized, concurrency) (§13)
- [x] Authorization Convention: namespace (D-4), anahtarlar (Q-6 = B ayar izni dahil), politika, aktör (§14)
- [x] Gateway kararı: `ocelot.json` değişmez; integration-agent işi tanımlı; sabotaj üretimi bozar (AC-55); r8: 0178 Gateway testinin uç
  sayısı (`Mod0178WarehouseRouteTests.cs:97`) uç ekleyen her dilimde, uçlarla aynı commit'te güncellenir — X-14, sahip onayı `c7dc4ea6a` + kapsam teyidi `6235794ab` (r9) (AC-55, S-55c); DL-6 DL-5
  kabulünden sonra (r9)
- [x] Acceptance criteria test edilebilir; her bekçi sabotajlı, bekçi olmayanlar adıyla işaretli (§16)
- [x] Audited Events tablosu her yazma komutunu tam bir kez içeriyor (§21)
- [x] Sayfa yardım metni her sayfa için yazılı (§25); tur yok
- [x] Sahip kararları: Q-1…Q-16, Q-18, Q-19, Q-21, Q-22 ve Y-1…Y-4 karar 03 ile kapandı (§19.4); Y-1 karar 03 ekiyle A (kapatma reddedilmez,
  bekleyenlerin görevleri açılır — §8.19); Q-17 karar 03 ekiyle (r4); D-1…D-17 (kararlar 01, 02)
- [x] Q-17 hedef tarih — **24 Kasım 2026** (kararlar 03 eki, Beste 2026-11-12); künye `target: 2026-11-24`
- [x] "Hemen" teyidi (Y-1) — ilk tur istek içinde en çok 200 sipariş / 2.000 görev, kalanı en geç bir bakım turu (kararlar 03 eki, Beste
  2026-11-13; §8.19, AC-15 q, AC-27 d)
- [x] **Q-23 (r6, r5 incelemesi F-1)** — teyidin tur sınırıyla daraltılması (bir depoda 10.200'den fazla bekleyenin fazlası sonraki turda):
  **A — "hemen" sayılır**; ilk tur ≤ 200 / 2.000 + depo başına tur başına 50 parti kabul (kararlar 03 eki, Beste 2026-11-13; §8.19, §19.4)
- [ ] Q-20 rol şablonu — karar Ali (Auth); X-13 ayrı WP, DL-4 kabul kapısı (§24)
- [ ] CT kararı CT-1 — WMS-BUNDLE eklemeli sürüm numarası (1.2.0 ya da 1.3.0, MOD-0178 CT ile eşgüdüm) (§19.4)
- [ ] XMC-001 satırları X-1…X-17 sahiplerinden onay aldı (§5.1); X-15'in sahibi TBD (öneri Ali); X-14 sahip onaylı (kararlar 03 eki,
  `c7dc4ea6a`, r8; kapsam teyidi — uç ekleyen her dilim — `6235794ab`, r9); X-17 sahip onaylı (kararlar 03 eki, `ad1100158`, r10)
- [x] **`.antigravity/**` kapısı — paket onayından ÖNCE (Y-4, karar 03):** PKS-001 §4 `warehouse.waves.*` satırı, PKS-001 :92 adlı alt ad
  alanı istisna cümlesi (r3, F-4), RCS-001 §3 `WAV` satırı ve aynı adımda X-12 — **yazıldı — commit `0aa802519`, sahip onayı Beste 2026-11-12**
  (§6 maddeleri (1), (1b), (2), (3)-X-12)
- [x] **X-16 — MOD-0178 §14.1 notu (kapının kalan parçası):** yazıldı — MOD-0178 r29 `adfe1bd7d`, inceleme UYGUN
  (`docs/records/reviews/MOD-0178/2026-11-13-2e4036b6.md`; not `:2128-2152`); Ali'nin onay turu (r29 + bu paket) açık — aşağıdaki madde (§6 madde (3), §24)
- [x] **X-16 bekçisi (r5, CT kararı; r29 incelemesi F-1):** AC-5 alt ad alanı sahipliği vakası + S-5c, S-5d (DL-1) — **sahip onayladı
  2026-11-13** (kararlar 03 eki)
- [ ] **MOD-0178 r29 (X-16, F-5):** r29 bağımsız incelemesi (CT-APR-02) **UYGUN — tamam** (`2e4036b6`) + Ali onayı (**açık**) — sahip kararıyla MOD-0178 r28 ile **aynı onay turunda**;
  bu paketin onay satırından önce (§6 madde (4), §24)
- [x] Kararlar 02 dosyası depoya alındı (Q-15) — MVP-1 hattında (`ct/line-ff-2026-11-12i` ve sonrası)
- [ ] WMS-WAVE-BUNDLE taslağı yazıldı (DL-1 ilk adımı, AC-1); dondurma ayrı CT adımı
- [ ] WMS-BUNDLE eklemeli sürümü (X-8: `OutboundEventKind` değerleri + `wave` / `pickIssuance` + X-7 üyeleri) MOD-0178 CT adımıyla yazıldı ve
  donduruldu — **DL-2 başlamadan önce** (F-3)
- [ ] r9 bağımsız inceleme (CT-APR-02, çok kısa kontrol — r8 incelemesi `e2666ea4` KOŞULLU idi, F-1, F-2 r9'da kapandı, yalnız metin
  değişti — §19.12; r7 incelemesi `f357b120` KOŞULLU idi, F-1…F-4 r8'de kapandı — §19.11; r3 incelemesi `311bcfa3` KOŞULLU idi, F-1…F-7 r4'te kapandı — §19.7; r4 incelemesi `7e92bce7`
  KOŞULLU idi, F-1…F-6 r5'te kapandı — §19.8; r5 incelemesi `0517db83` KOŞULLU idi, F-1…F-6 r6'da kapandı — §19.9; r6 incelemesi `127005fa`
  KOŞULLU idi, F-1…F-5 r7'de kapandı — §19.10) ve onay satırı (CT-APR-01)
  — **onaylayan Ali (GitHub `GMG-ERP`), D-17 (karar 02)**; yazan ≠ onaylayan

## 19. Implementation Notes

### 19.1 Okunan kaynaklar
- Brief `execution/domains/supply-chain-execution/work-packs/mockups/wave-planning/BRIEF-MOD-0180-wave-planning.md` (tamamı, `:1-93`).
- Kararlar 01 `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-01.md` (tamamı).
- Kararlar 02 — `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-02.md` (MVP-1 hattında; r1 yazımında dalsız commit
  `58e3e73b4`'ten `git show` ile okunmuştu; içerik aynı) (Q-15).
- Mockup v2 (`C:\Users\Ali\Downloads\Dalga Planlama (standalone) v2.html`, 2.223.236 bayt, paketlenmiş JS sayfası) — yalnız metin dizgeleri çıkarıldı
  (form etiketleri, durumlar, KPI, istisna eylemleri, durum örnekleri); çalıştırılmadı, görsel olarak incelenmedi (kısmi inceleme).
- `execution/domains/supply-chain-execution/domain-config.md` (tamamı); MOD-0181 paketi — r1 yazımında **r7** okundu (yapı, §2–§8, §16, §18,
  §20–§25; §19 kısmen); geçerli onaylı sürüm **r8** (`execution/approvals/approvals.tsv:83`, LF sha256 `f943ab48…` = `dc010b911` dosyası) —
  r2'de atıflar r8 konumlarına taşındı (`:219`, `:235-239`, `:299-302`, `:573-576`, `:1395`); MOD-0178 paketi — r1'de r27 okundu (§1–§4.6,
  §8.17–§8.21 başı), r2'de atıflar **r28** konumlarına taşındı (r28 farkı yalnız künye, §19.1.27 ve Item adımı satırları; atıf yapılan bölümler
  +8 / +9 kaydı, içerik aynı) ve §14.1 (`:2117-2120`) okundu; diğer bölümler okunmadı — kısmi inceleme.
- r2 girdileri: bağımsız inceleme r1 `docs/records/reviews/MOD-0180/2026-11-12-c7c774c6.md` (tamamı); sahip kararları 03
  `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md` (tamamı); kararlar 02 yeniden (D-9 metni, D-17).
- r2 ölçümleri (`dc010b911`, salt okuma): `OutboundAllocationService.cs:165-300`; `CentralAuditDeclarationTests.cs:40-132`;
  `SingleSystemOfRecordArchitectureTests.cs:86-120`; `ContractSchemas.cs:15-24`, `:404-407`; `wms-bundle.openapi.yaml:64`, `:1352`, `:1694`,
  `:1757`, `:1805-1835`; `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:1-60`, `:215-250` ve `.csproj:24-26`;
  `ocelot.json:1130-1162`; Governance `_IndexL10n.cshtml` fiil satırları, `perm-label.js:52-54`, `:165-295`,
  `role-assignments-label-ledger.test.js:1-40`, `docs/roadmap/backlog/role-permission-label-ledger.md:1-30`; bu dosyaların `git log`'u;
  `.antigravity/rules/permission-key-standard.md:98-110`, `record-coding-standard.md:45-75`; `RecordCodeTypeCatalogTests.cs:24-29`;
  `git diff a3f329ece dc010b911` (servis, sözleşme, kural ve paket atıflarının kayması).
- r3 girdileri: bağımsız inceleme r2 `docs/records/reviews/MOD-0180/2026-11-12-05180e86.md` (tamamı); kararlar 03 ve eki "Y-1
  netleştirmesi" (tamamı, commit `33bb4343a`).
- r3 ölçümleri (HEAD `33bb4343a`, salt okuma): `PickTaskQueries.cs:1-30`, `:68`; API `PickTasksController.cs:31-44`;
  `OutboundShipmentModels.cs:60-95` (`:70`, `:87`); `OutboundShipmentReads.cs:61`, `:80-100`; `PutawayTaskModels.cs:60-90` (`:74`);
  `PutawayTaskReads.cs:22`; Web `frontend/Diten.Web/Controllers/PickTasksController.cs:95-135` (`:97`, `:111`, `:118`);
  `wms-bundle.openapi.yaml` `operationId` listesi ve `:396-406`; `PageHelpCatalog.cs:15-36`, `:98-112`; `perm-label.js:52-55`;
  `Mod0178WarehouseRouteTests.cs:215-240` (`:228`); `.antigravity/rules/permission-key-standard.md:88-106` (`:92`); MOD-0178 paketi künyesi
  (r28, `approval_status: pending`) ve `execution/approvals/reviews.tsv` MOD-0178 satırları; `git diff --stat dc010b911 33bb4343a -- services
  gateway docs/analysis` (boş) ve aynı aralıkta `PickTasksController.cs`, `PageHelpCatalog.cs`, Governance ve `Views/Warehouse/PickTasks` (boş).
- r4 girdileri: bağımsız inceleme r3 `docs/records/reviews/MOD-0180/2026-11-12-311bcfa3.md` (tamamı); kararlar 03 ve eki (tamamı, commit
  `c30c47e90` — Q-17 ve "hemen" teyidi); `.antigravity` kapısı commit'i `0aa802519` (`git show`: PKS-001 `:92`, `:105-106`; RCS-001 `:72`;
  `RecordCodeTypeCatalogTests.cs:29-30`; commit iletisindeki sabotaj notu).
- r4 ölçümleri (HEAD `c30c47e90`, salt okuma): `frontend/Diten.Web/Models/Warehouse/OutboundDtos.cs:160-180` (`WmsPickTaskDto` `:169`,
  sayfa DTO'su `:201`); Web `frontend/Diten.Web/Controllers/PickTasksController.cs:54`, `:76-130` (`Export` `:76`, `ListQuery` çağrısı `:79`,
  CSV başlığı `:97`, `ListQuery` `:109-129`, `orderBy=-createdAt` `:111`, süzgeç adları `:118`), `:325-345` (`Row` `:332`);
  `git diff --stat 33bb4343a c30c47e90 -- services gateway docs/analysis` (yalnız `RecordCodeTypeCatalogTests.cs` +1); AUD-001 istisna
  sınıfları `.antigravity/rules/audit-trail-standard.md:233-237` (r4 yeni komut açmadığı için yalnız okundu).
- r5 girdisi: bağımsız inceleme r4 `docs/records/reviews/MOD-0180/2026-11-13-7e92bce7.md` (tamamı).
- r5 ölçümleri (HEAD `1aaa0db22`, salt okuma): `INV/Persistence/Warehouse/WarehouseIndexConfiguration.cs:140-152` (tekil
  `(TenantId, OutboundId, Seq)` olay index'i `:147-148`); `INV/Application/Features/OutboundShipments/Services/OutboundShipmentReads.cs:118-128`
  (`Seq = ++outbound.EventSeq` `:124`); `INV/Application/Common/RefusalAuditMetrics.cs:1-14` (Meter `Diten.InventoryService.Audit` `:11`,
  sayaç `inventory_refusal_audit_failed_total` `:13`).
- r10 girdileri: r9 bağımsız kontrolü `docs/records/reviews/MOD-0180/2026-11-13-21e14601.md` (UYGUN); kararlar 03 eki X-17
  (`docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md` son madde, commit `ad1100158`). r10 ölçümü (HEAD
  `ad1100158`, salt okuma): `InventoryLedgerManifestCompletenessTests.cs:209` yöntem, `:213-223` dışlama listesi (6 namespace; `:222` 0181
  yorumu); MOD-0181 emsal commit `446cb58db` (dışlama satırı + Gateway sayısı tek commit'te); `EndsWith(".Controllers` deseni
  InventoryService testlerinde yalnız bu dosyada ve `CycleCountArchitectureTests.cs:175`'te (0181'in kendi namespace seçimi); `git diff
  --name-only 6235794ab ad1100158 -- services gateway docs/analysis frontend` yalnız Platform `TrustedDocumentRepositoryConsumerMongoTests.cs`
  (0180 atıflarını etkilemez); `Mod0178WarehouseRouteTests.cs:97` hâlâ `Assert.Equal(73, endpoints.Count)`. Koşulmayanlar (r10): test, build,
  sabotaj (S-5e DL-1'de).
- r9 girdileri: bağımsız inceleme r8 `docs/records/reviews/MOD-0180/2026-11-13-e2666ea4.md` (tamamı); kararlar 03 eki X-14 kapsam teyidi
  (`docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md` son madde, commit `6235794ab`). r9 ölçümü (HEAD
  `6235794ab`, salt okuma): `git diff --stat 0b5062383 6235794ab -- services gateway docs/analysis frontend` boş;
  `Mod0178WarehouseRouteTests.cs:97` `Assert.Equal(73, endpoints.Count)`. Koşulmayanlar (r9): test, build, sabotaj.
- r8 girdileri: bağımsız inceleme r7 `docs/records/reviews/MOD-0180/2026-11-13-f357b120.md` (tamamı); kararlar 03 eki X-14 maddesi
  (`docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md` son madde, commit `c7dc4ea6a`).
- r8 ölçümleri (HEAD `0b5062383`, salt okuma): `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:77-109` (`:80-96` yorum, `:97`
  `Assert.Equal(73, endpoints.Count)`, `:98-108` rota iddiaları), `:219-236` (`:228`); `git log` aynı dosya (`a55087094` 70 → 73,
  `5ab424fc0`, `96e25bf9b`); MOD-0181 paketi `:1024-1036` (r8 Gateway sayısı paragrafı); `PickTasksController.cs:15-60` (`:23` sınıf rotası,
  `:31` şablonsuz `GET`, `:37`, `:42` `{taskId}` `GET`'leri); `WmsBundleContractTests.cs:215-250` (`:239` `served` dizinleyici yazımı, `:243`,
  `:244`); `git diff --stat 90f6a1da8 0b5062383 -- services gateway docs/analysis frontend` (boş).
- **Koşulmayanlar (r8):** test, build, sabotaj koşulmadı; S-5c'nin `wave-sabotage` şablonunun `{taskId}` ile belirsiz eşleşme vermediği
  ASP.NET Core rota önceliğinden çıkarımdır (koşulmadı); X-14'ün dilim başına uç dağılımı (14 / 2 / 5 / 2) §3 / §14 / §23'ten çıkarımdır;
  denenmiş küme dışlamalı `limit 1` sorgusunun gerçek maliyeti ölçülmedi (DL-2); karar 03 X-14 ekinin dayanağı olan sahip sohbet cevabı
  depoda değil (yalnız karar kaydı).
- **Koşulmayanlar (r5):** test, build, vitest, sabotaj koşulmadı; S-15f'in iki olası kırmızısından hangisinin oluşacağı (kaybeden çağrının
  `Seq` çakışmasıyla hata alması ya da işlem geri çağrısının yeniden okuyup ikinci seti yazması) 0178 `InTransactionAsync` yeniden deneme
  davranışına bağlıdır ve ölçülmedi — iki durumda da (j2) kırmızıdır (DL-2'de ölçülür); takılı liste delta yazımının Mongo yazım çakışmasında
  yeniden deneme davranışı ölçülmedi (DL-2'de AC-15 p10 ölçer); takılı liste öğe boyutu ve 1.000 öğelik belge boyutu ölçülmedi (çıkarım);
  gösterge alarm eşiği (15 dk) işletim önerisidir, depoda alarm kuralı dosyası açılmadı; mockup v2, Blueprint xlsx, MOD-0178 r28 ve MOD-0181 r8
  gövdeleri r5'te yeniden açılmadı.
- **Koşulmayanlar (r4):** test, build, vitest, sabotaj koşulmadı; `GateSeq` koşullu yazımının Mongo yazım çakışması davranışı ve sürücünün işlem
  geri çağrısının yeniden denemesi bu depoda ölçülmedi (Mongo belgelenmiş davranışı — çıkarım; DL-2'de AC-15 n2 / n3 ölçer); `IssueImmediateAsync`
  ve bekleyen sipariş taramasının 0178 index'leriyle maliyeti ile ilk turun istek içi süresi ölçülmedi (AC-27 d DL-2'de ölçer); InventoryService'te
  OTel metrik adlandırma deseni açılmadı (metrik adları öneridir); MOD-0178 r28 gövdesi yeniden okunmadı.
- **Koşulmayanlar (r3):** test, build, vitest, sabotaj koşulmadı; MOD-0178 paketinin r28 gövdesi r3'te yeniden okunmadı (yalnız künye);
  `IssueImmediateAsync` ve bekleyen sipariş taramasının 0178 `wms_outbound_shipments` index'leriyle maliyeti ölçülmedi (DL-2'de ölçülür;
  index gerekirse XMC-001 — §4.6).
- **Koşulmayanlar (r2):** test, build, vitest, sabotaj koşulmadı; mockup v2 ve Blueprint xlsx r2'de yeniden açılmadı; X-15'in beklenen
  satır kodları (`WAVES`, `WAVESSETTINGS`, …) üretim `PermLabel.split` ile ölçülmedi — çıkarımdır, DL-1'de vitest ile ölçülür.
- Blueprint 8.8 satır 181 (xlsx); registry `:318`; MVP-5 planı `:78`, `:124` (W-4 satırı; plan yetenek kimliği vermez).
- `.claude/agents/module-pack-author.md`, `.antigravity/rules/module-pack-standard.md` (`:1-110`), PKS-001 §4 tablosu ve §5, RCS-001 §3 başlıkları
  ve `CNT` / `PCK` satırları, AGENTS.md §6.1.
- MOD-0040-FU02 paket künyesi ve DL-8 satırları.
- DEV-0000 metni r1'de **açılmadı**; Golden Slim yapısı module-pack-author ajan tanımındaki kopyadan ve MOD-0181 §10 ölçümünden alındı.
  r1 bağımsız incelemesi DEV-0000 Golden Slim kod ağacını (Application `Features/GoldenReferenceSlim`, Web
  `Views/DevEnablement/GoldenReferenceSlim`) §10 / §11 ile karşılaştırdı ve uyumlu buldu (inceleme kaydı "Dayanaklar").

### 19.2 Ölçülen gerçekler (r1 `a3f329ece`; r2'de `dc010b911`'de yeniden ölçüldü, salt okuma)
- 0178 pick görevleri tahsisin işlem B'sinde, outbound'la aynı işlemde açılır (`OutboundAllocationService.cs:172-185` numaralar işlem dışı,
  `:200-213` kurulum, `:227-233` tek `InTransactionAsync`); kurucular `PickTask` `private static` `:248`, `TaskCreated` `private` `:288`;
  dalga için erteleme zorunlu bir 0178 değişikliğidir (X-3).
- 0178 kayıt testi `Controllers.Warehouse` namespace'indeki bütün controller anahtarlarını 0178 manifestiyle eşitler (`WarehouseRegistrationTests.cs:49-56`).
- `CentralAuditDeclarationTests`: `dc010b911`'de **117** yazma komutu (`:53`; r1'in ölçtüğü 116 `a3f329ece`'deydi, 117'ye MOD-0181 DL-5
  `c76c02944` çıkardı), 14 işlem geri çağrısı (`:130`), sağlayıcı listesi `:60-69`.
- `SingleSystemOfRecordArchitectureTests`: 0181 bildirimleri `:88-89`, `:92-93`; `declared` `:100-115`; `wms_` önek süzgeci `:120`.
- WMS-BUNDLE 1.1.0 (`:64`); `OutboundEventKind` kapalı enum 15 değer (`:1812-1814`); servis sözleşme testleri `additionalProperties`'siz
  her nesneyi kapalı okur (`ContractSchemas.cs:19-21`, `:404-407`).
- PUT / DELETE bekçisi `LedgerImmutabilityArchitectureTests.cs:131-160`.
- Gateway `/api/warehouse` + `/api/warehouse/{everything}` → 5067 (`ocelot.json:1134-1161`; `{everything}` portu `:1158`); Gateway testleri
  üretim `ocelot.json`'u bağlı kopya olarak okur (`Diten.ApiGateway.Tests.csproj:26`); 0178 testi `api/warehouse` controller'larını özyineli
  tarar (`Mod0178WarehouseRouteTests.cs:219-236`) ve yönlendirilen uç sayısını `:97`'de **73**'e sabitler (r8, ölçüm HEAD `0b5062383`; son
  artış MOD-0181 DL-5 `a55087094`) — X-14.
- Governance Rol İzinleri: `recall` fiili köprüde ve ailelerde yok; `configure`, `release`, `resolve`, `read`, `create`, `update`, `export`,
  `cancel` var (`_IndexL10n.cshtml:165-276`, `perm-label.js:52-54`); defter "fiil 0 · satır 96".
- LOCATION: koridor / raf / saat dilimi alanı yok; yalnız v1.1 toplu bin kodu biçimi (`location.openapi.yaml:148`, `:205-252`, `:280-287`).
- Outbound: taşıyıcı, müşteri kimliği, kesim saati, ağırlık / hacim alanı yok (`warehouse-outbound.openapi.yaml:76-97`; MOD-0178 §4.5).
- RCS-001 §3'te `WAV` yok (r3; r4'te `:72`'de var — aşağıda); Platform `RecordCodeTypeCatalogTests` RCS satırı ile kataloğu eşitler (`2f7b6145e`; sözlük `:29` `CNT`).
- PKS-001 §4: `warehouse.*` → MOD-0178 (`:105`); §5: `edit` → `update`. §4 başlığı "(locked)", kural cümlesi `:92` "first segment … exactly
  one module/service" — alt ad alanı istisnası yok (r3 ölçümü, F-4). **r4 (`c30c47e90`, commit `0aa802519` sonrası):** `:92` adlı alt ad alanı
  istisnasını taşır, `:105` "`warehouse.waves.*` hariç — MOD-0180", `:106` `warehouse.waves.*` → MOD-0180; RCS-001 §3 `:72` `WAV`;
  `RecordCodeTypeCatalogTests.cs:30` `["WAV"] = "MOD-0040-FU02 DL-8"`.
- (r4, `c30c47e90`) Web Toplama Görevleri satırı `WmsPickTaskDto`'dan (`OutboundDtos.cs:169`) `Row` (`PickTasksController.cs:332`) ile kurulur;
  `ListQuery` (`:109`) liste (`:54`) ve CSV dışa aktarımı (`:79`) için ortaktır — sıralama parametresi ikisine birden gider.
- (r3, `33bb4343a`) 0178 pick listesinin sıralama parametresi `orderBy`'dır; beyaz liste `createdAt` / `-createdAt` (`PickTaskQueries.cs:68`,
  sözleşme `:406`); Web proxy süzgeç adlarını beyaz listeyle geçirir (`PickTasksController.cs:118`). 0178 görev ve outbound DTO'ları
  (`WarehouseTaskDto`, `OutboundPickTaskDto`, `OutboundShipmentViewDto`) dalga / `pickIssuance` üyesi taşımaz.
- `a3f329ece` → `dc010b911` arası: sözleşmeler, kurallar, domain-config, registry, plan, brief ve 0180'in atıf yaptığı 0178 kod dosyaları
  **değişmedi** (`git diff --stat` boş); MOD-0178 paketi r27 → r28, MOD-0181 paketi r7 → r8 (atıf kaymaları §19.1).
- **Mockup v2 ile brief / karar farkları:** (1) yaratma panelinde 9 alan (taşıyıcı ayrı, "En çok sipariş" ve "En çok satır" ayrı) — paket 8
  (Q-1); (2) mockup "önerilen ad alanı `wave.`" diyor — karar `warehouse.waves.*` (D-4); (3) mockup risk eşiğini "kesime < 90 dk, ilerleme < %70"
  gösterir — D-6 ile aynı; (4) mockup sipariş satırında "taşıyıcı" ve "Taşıyıcı süzgeci yok" uyarısı gösterir — v1'de veri yok (Q-1).

### 19.3 Çıkarımlar
- S-biçimli rota "en kısa yol"un yaklaşığıdır; kesin optimum değildir (§8.7).
- Uzlaştırma servisinin 60 sn turu durum gecikmesi yaratır; olay yolu (bus) gelince FU-0180-EVENTS.
- `TaskWaitingLong` görev atanmamış ama başlamamış görevi de sayar; atanmış / atanmamış ayrımı v1'de yok.

### 19.4 Kararlar (Q-n, Y-n) — karar 03 sonrası durum

Kaynak: sahip kararları 03 (`docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md`, Beste, 2026-11-12): "Y-1…Y-4 için
önerdiğin gibi, Q'lar için Q-6 ayar izni, diğerleri öneri gibi". Karar 03 eki (r4): **Q-17 = 24 Kasım 2026** ve "hemen" teyidi (§8.19).
Karar 03 eki (r6, Beste 2026-11-13, sahip sohbet cevabı): **Q-23 = A** ve X-16 bekçisinin (AC-5 vakası, DL-1) sahip onayı.
Açık kalan: **Q-20** (Ali), **CT-1** (CT).

| Q | Konu | Seçenekler | Karar (r3 itibarıyla) | Karar sahibi |
|---|---|---|---|---|
| Q-1 | Taşıyıcı / sevk yöntemi süzgeci (mockup v2 alanı) — outbound'da veri yok | A: v1'de alan yok, 8 alan → slim · B: alan kalır, kaynak kararı + 9 alan → compact | **A** (karar 03); FU-0180-CARRIER — form 8 alan, slim değişmedi | Beste |
| Q-2 | Görev ertelemesinin anahtarı | A: depo ayarı `WaveManaged` + 0178 `IPickIssuancePolicy` portu · B: outbound başına istek bayrağı | **A** (karar 03) | Beste (0180 + MOD-0178 sahibi) |
| Q-3 | Depo saat dilimi kaynağı (D-3) — LOCATION'da yok | A: 0180 depo ayarı `TimeZoneId` · B: LOCATION `Warehouse` alanı | **A** (karar 03); FU-0180-TZ-LOCATION | Beste |
| Q-4 | `WAV` ara üreticisi — FU02 DL-8 yok | A: 0178 geçici sayacı + `WAV` (X-6) · B: FU02 DL-8'i bekle | **A** (karar 03); FU-0180-RCS | Beste (K2) |
| Q-5 | İzin son eki `edit` ↔ PKS-001 §5 `update` | A: `warehouse.waves.update` · B: `edit` | **A** (karar 03) | Beste |
| Q-6 | Depo dalga ayarının izni | A: `warehouse.waves.update` · B: yeni `warehouse.waves.settings.configure` (Tier-3) | **B** (karar 03; inceleme F-15) — §14, AC-15, AC-48 | Beste |
| Q-7 | PKS-001 §4 satır metni ve MOD-0178 Q-08 ile fark kaydı | A: §4'e alt ad alanı satırı + MOD-0178 §14.1'e not (X-16) · B: yalnız §4 satırı | **A** (karar 03); metin önerisi §6 | Beste |
| Q-8 | Teknik üst sınır (500 sipariş / 2.000 görev) | A: kabul · B: başka değer | **A** (karar 03) | Beste / CT |
| Q-9 | Bölge sırası | A: bölge kodu artan · B: depo ayarında liste | **A** (karar 03); FU-0180-ZONEORDER | Beste |
| Q-10 | Kısa toplama ve "Tamamlandı" | A: kısa kapatılan görev terminal → `Completed` + `ShortPick` · B: kısa toplama `PartiallyCompleted` | **A** (karar 03). Gerekçe (r2, F-2): "Kısmen tamamlandı" geri çekme (D-9) ve `Excluded` / `OrderCancelled` üyeliğe (karar 03 Y-2) bağlıdır; kısa toplama bu kümeye girmez | Beste |
| Q-11 | Uzlaştırma turu | A: 60 sn · B: başka | **A** (karar 03) | CT |
| Q-12 | Toplayıcı 0178 listesinde dalga sırası (X-7) | A: 0178 listesine dalga sütunu + sıralama · B: yalnız 0180 ayrıntısı | **A** (karar 03) — AC-50 zorunlu | Beste (MOD-0178 sahibi) |
| Q-13 | Build / Buy (W-0180-01) | A: Build · B: Buy/Partner | **A** (karar 03); FU-0180-EA | Beste |
| Q-14 | AI Assist | A: v1 dışı · B: v1'de | **A** (karar 03); FU-0180-AI | Beste |
| Q-15 | Kararlar 02 dosyası | A: CT dosyayı çalışma dalına alır | **KAPANDI — A** (r1; dosya depoda) | CT |
| Q-16 | Dal | A: tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` · B: ayrı dal | **A** (karar 03) | Beste |
| Q-17 | Hedef tarih (künye `target`) | plan satırı tarih vermez | **KAPANDI (r4) — 24 Kasım 2026** (karar 03 eki, Beste 2026-11-12); künye `target: 2026-11-24` | Beste |
| Q-18 | KPI CSV okuma denetimi (AUD-001 §7) | A: v1'de yok · B: okuma kaydı | **A** (karar 03); FU-0180-READAUDIT | Beste |
| Q-19 | Planlayıcı DataTable değil | A: kabul · B: sol listeyi DataTable yap | **A** (karar 03) | Beste / CT |
| Q-20 | Rol şablonu (X-13) | A: §14 önerisi · B: başka | **Açık — karar Ali (Auth)**; öneri §14 (`settings.configure` şablonsuz) | Ali (Auth) |
| Q-21 | "Müşteri ya da teslim bölgesi" süzgeci | A: `ShipTo` metin eşleşmesi · B: v1'de süzgeç yok | **A** (karar 03); FU-0180-CUSTOMER | Beste |
| Q-22 | Sipariş başına kesim saati yok | A: planlayıcı sütunu "—" · B: outbound'a kesim alanı | **A** (karar 03) | Beste |
| Q-23 (r6; r5 incelemesi F-1) | "Hemen" teyidinin tur sınırı: "Bir depoda aynı anda 10.200'den fazla bekleyen sipariş varsa fazlası sonraki turda (en geç 60 sn sonra başlayan) açılır; kalabalık depoda tek tur ~12,5 dk sürebilir. Bu, Y-1'in 'hemen'ine uyar mı?" | A: evet — ilk tur ≤ 200 sipariş / 2.000 görev + depo başına tur başına 50 parti kalır · B: hayır — sınır kaldırılır ya da büyütülür (tur uzar; sonsuz döngüye karşı yalnız "turda bir kez" kuralı kalır) | **KARARLAŞTI (r6) — A** (CT önerisi A; karar 03 eki, Beste 2026-11-13, sahip sohbet cevabı: "evet"); §8.19 "Tur üst sınırı", AC-15 (p14), (p17). **r7 (r6 incelemesi F-1, CT kararı — sahibe yeni soru gerekmedi):** soru tek depo durumunu sormuştu; r6'nın sabit depo listesi tur sırasında kapanan başka bir deponun (başka kiracınınki de olabilir) kalanını turun sonuna bırakıyordu. r7'de depo kümesi her geçişte yeniden taranır — yeni kapanan depo sıradaki geçişe katılır, kalabalık deponun turu onu bekletmez; böylece teyidin "kalanı en geç bir bakım turu (60 sn)" sözü başka depo ve kiracılar için de geçerli kalır ve Q-23 = A'nın kapsamı (yalnız 10.200'ü aşan fazla sonraki turda) değişmez — §8.19 "Depolar arası sıra", AC-15 (p18) | Beste |
| Y-1 (F-1; r2 incelemesi F-2) | Ertelenmiş sipariş varken dalga yönetimini kapatma | r2 tablosu: A: 409 · B: kapatınca 0178'e hepsini aç isteği | **r3: karar 03 eki "Y-1 = A" — kapatma reddedilmez, bekleyenler iptal edilmez, görevleri dalga yokmuş gibi hemen açılır** (içerik r2 tablosundaki B'dir; ekin etiketi bağlayıcı metindir). r2'nin 409 `WAVE_SETTINGS_DEFERRED_ORDERS` kuralı ve kodu kaldırıldı — §4.5, §8.6, §8.19, AC-15, S-15b…S-15h. **r4: "hemen" teyidi** (karar 03 eki, Beste 2026-11-13) — ilk tur istek içinde ≤ 200 sipariş / 2.000 görev, kalanı en geç bir bakım turu; §8.19, AC-15 (q), AC-27 (d), S-15i…S-15v. **r5:** kapatma turunun kısmi başarısızlık ve takılı liste kuralları (F-1…F-4, §19.8); r5 teyidi tur sınırıyla daraltmıştı (bir depoda 200 + 50 × 200 = 10.200 bekleyen sipariş, §8.19) — bu daraltma sahibe sorulmamıştı (r5 incelemesi F-1). **r6:** daraltma sahibe Q-23 olarak soruldu → **Q-23 = A** (Beste 2026-11-13, karar 03 eki): 10.200'ü aşan fazla sonraki turda açılır ve bu "hemen" sayılır; sınır depo başınadır, depolar dönüşümlü işlenir (§8.19) | Beste |
| Y-2 (F-2) | Dalgadan çıkarılan / 0178'de iptal edilen sipariş | A: `PartiallyCompleted` · B: `Completed` | **A** (karar 03) — D-9 genişler; §8.3, §8.9, AC-30, S-30b | Beste |
| Y-3 (F-11) | "En kısa yol" (D-2) | A: S-biçimli v1 yaklaşığı | **A** (karar 03) — §8.7, AC-25 | Beste |
| Y-4 (F-10) | `.antigravity` satırlarının zamanı | A: paket onayından önce · B: modül PR'ından önce | **A** (karar 03) — §6, §18, §24. **r4:** satırlar ve X-12 yazıldı (commit `0aa802519`, sahip onayı Beste 2026-11-12); X-16 açık. **r5:** X-16 yazıldı — MOD-0178 r29 `adfe1bd7d`, inceleme UYGUN; Ali'nin onay turu (r29 + bu paket) açık | Beste |
| CT-1 (F-3) | WMS-BUNDLE eklemeli sürüm numarası (X-8) | 1.2.0 (0178 Item adımıyla birleşik) · 1.3.0 (Item 1.2.0'dan sonra) | **Açık — CT kararı**; adım DL-2 başlamadan önce | CT (MOD-0178 CT ile) |

### 19.5 r1 → r2 kapanış tablosu

| # | Bulgu / karar | r2 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | Dalga yönetimi kapatılınca ertelenmiş siparişler takılır (ORTA) | Y-1: `Deferred` / `Allocating` outbound varken kapatma 409 `WAVE_SETTINGS_DEFERRED_ORDERS`; kalan yarış ve çıkış yolu yazıldı; AC-15 (a)…(e) + S-15b. **r3'te değişti** (karar 03 eki Y-1 = A — 409 kaldırıldı; §19.6 F-2, §8.19) | §4.5, §8.6, §8.17, §12, §13, §16 AC-15 |
| F-2 | D-9 genişletilmiş ve yanlış atfedilmiş (ORTA) | Y-2: `Excluded` / `OrderCancelled` → `PartiallyCompleted` "karar 03 Y-2" diye atfedildi; D-9'un karar 02 metni ayrı yazıldı; Q-10 gerekçesi düzeltildi; AC-30 + S-30b | §8.3, §8.9, §16 AC-30, §19.4 Q-10 |
| F-3 | X-8 eksik ve sırası geç (ORTA) | X-8'e `OutboundEventKind` `PickTasksIssued` / `PickTasksWithdrawn`; kapalı şema ölçümü; eklemeli sürüm **DL-2 başlamadan önce** CT adımı; numara sabitlenmedi — "0178 CT ile eşgüdümlenen bir sonraki eklemeli sürüm (1.2.0 ya da 1.3.0)", CT-1 | §5.1 X-8, §7, §18, §19.4 CT-1, §24 |
| F-4 | XMC listesinde rol köprüsü yok (ORTA) | X-15 (Governance `_IndexL10n.cshtml`, `RoleAssignmentsIndex` 7 resx, `perm-label.js`; `recall` yeni fiil, `configure` var); sahip depoda ölçüldü (sahiplenen pack yok → TBD, öneri Ali), XMC-001 açık; §5 ortak kaynak tanımı `Perm.Entity.*` ile genişledi; AC-56 + S-56 | §5, §5.1 X-15, §7, §16 AC-56, §17 |
| F-5 | S-55 sabotajı testi bozuyor (ORTA) | Gateway testinin üretim `ocelot.json`'u okuduğu ölçüldü (`.csproj:26`, `Mod0178WarehouseRouteTests.cs:23`, `:219-236`); S-55a üretim `ocelot.json` portu, S-55b üretim controller `[Route]`'u (integration-agent, geri alınır) | §5.1 X-14, §15, §16 AC-55 |
| F-6 | AC-9 ve X-10 bayat sayıya bağlı (ORTA) | AC-9 "dilim başındaki ölçülen sayı + dilimin 0180 komutları"; X-10 `:53` (117), `:60-69`, `:130`; X-9 `:88-89`, `:92-93`, `:100-115`, `:120` | §5.1 X-9 / X-10, §16 AC-9, §19.2 |
| F-7 | Belge atıfları eski konumda; 0181 r7 (DÜŞÜK) | Kod atıfları `dc010b911:` ile sabitlendi; MOD-0178 atıfları r28 (+8 / +9), MOD-0181 atıfları r8 (+38); 0181 onaylı sürüm r8 yazıldı | giriş notu, §0, §2, §3, §4, §6, §7, §8, §19.1 |
| F-8 | D-17 pakette yok (DÜŞÜK) | Onaylayan Ali (GitHub `GMG-ERP`), D-17 — künye `revision`, giriş notu, §18 | künye, giriş, §18 |
| F-9 | `status_note` Q-15'i açık sayıyor (DÜŞÜK) | `status_note` güncellendi (Q-15 kapalı; r2 inceleme + Ali onayı bekler) | künye |
| F-10 | `.antigravity` satırlarının zamanı (DÜŞÜK) | Y-4: paket onayından ÖNCE, sahip onayıyla; CT hazırlar; satır metinleri önerildi | §6, §18, §24 |
| F-11 | D-2 yorumu sahip teyitsiz (DÜŞÜK) | Y-3: S-biçimli v1 yaklaşığı sahipçe kabul | §8.7, §19.4 |
| F-12 | Ad ve atıf tutarsızlığı (DÜŞÜK) | Tek ad: port `IPickIssuancePolicy`, 0180 uygulaması `WavePickIssuancePolicy`; X-3 aralığı `:227-233` | §5.1 X-3 / X-4, §8.6 |
| F-13 | Adlı sabotajı olmayan bekçiler (DÜŞÜK) | S-12b, S-39, S-49, S-54 eklendi; AC-40, AC-43, AC-44, AC-46, AC-47 "bekçi değil" diye işaretlendi | §16, §24 |
| F-14 | AC-18 karşılaştırma referansı tanımsız (DÜŞÜK) | Referans: X öncesi commit'te aynı fikstür betiğiyle üretilip kabul kaydına yazılan normalize JSON + sha256; kanıttır, bekçi değil; bekçi 0178 testleri | §16 AC-18 |
| F-15 | Q-6 = A riski (DÜŞÜK) | Q-6 = B (karar 03): `warehouse.waves.settings.configure` Tier-3; izin tablosu, manifest `ApiPermissions`, rol önerisi, AC-15 / AC-48, S-15c | §8.15, §13, §14, §16 |
| F-16 | Kurucuların paylaşımı (DÜŞÜK) | X-3 (a): `PickTask` / `TaskCreated` davranış değişmeden 0178 içinde ortak yardımcıya (`OutboundPickTaskFactory`); X-4 dosya listesine eklendi; AC-19 + S-19b | §5.1 X-3 / X-4, §8.6, §16 AC-19 |
| Y-1…Y-4 | Sahip kararları 03 | F-1, F-2, F-11, F-10 satırları | §19.4 |
| Q-6 | Ayar izni B | F-15 satırı | §14, §16 |
| Q-1…Q-5, Q-7…Q-16, Q-18, Q-19, Q-21, Q-22 | Öneri gibi (A) | §19.4 "Karar (r2)" sütunu; koşullu ifadeler ("Q-4 = A ise", "Q-12 = A ise") kesinleştirildi; AC-50 zorunlu | §5.1 X-6 / X-7, §16, §19.4 |
| Q-17 | Hedef tarih | künye `target` "TBD (sahip)" | künye, §18 |
| Q-20 | Rol şablonu | karar Ali (Auth); öneri §14 | §14, §18 |
| D-17 | Onaylayan Ali (karar 02) | F-8 satırı | künye, §18 |

### 19.6 r2 → r3 kapanış tablosu

Girdi: bağımsız inceleme r2 `docs/records/reviews/MOD-0180/2026-11-12-05180e86.md` (KOŞULLU; 2 orta, 7 düşük) ve kararlar 03 eki (Y-1 = A).

| # | Bulgu / karar | r3 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | X-7 / X-8 dosya listesi eksik; `sort=waveSequence` (ORTA) | HEAD `33bb4343a`'da ölçüldü; X-7'ye sorgu `:12` + `orderBy` beyaz listesi `:68`, API `PickTasksController.cs:33-35`, Web `PickTasksController.cs:111`, `:118` (+ view / JS / resx); X-8'e "üretim karşılığı" — `PutawayTaskModels.cs:74` `WarehouseTaskDto` (+ `PutawayTaskReads.cs:22`), `OutboundShipmentModels.cs:70`, `:87` (+ `OutboundShipmentReads.cs:61`, `:91`) ve sözleşme `listPickTasks` `orderBy` enum'u `:406`; parametre adı **`orderBy=waveSequence`**; §6'daki "başka 0178 dosyası yok" cümlesi "yalnız adıyla sayılan dosyalar; listede olmayana dokunmak = dur + XMC-001" oldu; AC-50 + S-50b | §5.1 X-7 / X-8, §6, §7, §16 AC-50 |
| F-2 | Y-1 kapanışı (a) iptal okuması, (b) 409'un canlı depoda çözülmemesi, (c) sayım süzgeci, (d) `deferredOrderCount` AC'siz (ORTA) | **Kararlar 03 eki Y-1 = A:** 409 ve `WAVE_SETTINGS_DEFERRED_ORDERS` kaldırıldı → (a) iptal yok, (b) red yok; kapatma akışı §8.19 (ayar işlemi C1 + aynı istekte ilk tur + tekrar edilebilir bakım turu `ReleasePendingOrdersCommand`; seçim gerekçesi; yarış; kısmi hata; denetim; dalgadaki siparişler etkilenmez); 0178 portuna `IssueImmediateAsync` (X-4, §8.6); X-1'e "iptal `PickIssuance`'a dokunmaz"; (c) bekleyen = `Status = Allocated` + `Deferred` (§4.5); (d) AC-15 (f); AC-15 yeniden yazıldı (a…m) + S-15b…S-15h; yazma komutu 11 → 12 | §3, §4.3, §4.5, §5.1 X-1 / X-4 / X-10, §8.2, §8.6, §8.8, §8.9, §8.10, §8.17, §8.19, §10, §12, §13, §14, §16 AC-12 / AC-15, §19.4, §21 |
| F-3 | AC-39 / AC-42 / S-39 DL-4'te DL-5 sayfasını ölçüyor (DÜŞÜK) | AC-39 `{Waves,WavePlanner,WaveExceptions}`; S-39 `Waves/Index.cshtml`; AC-42'den performans çıktı; `WaveKpis` Layout vakası + S-54b AC-54'e (DL-5) taşındı; S-54 AC-54'ün kendi UAS-001 vakasını kırar | §16 AC-39 / AC-42 / AC-54, §24 |
| F-4 | PKS-001 :92 "locked" cümlesi değişmezse standart çelişir (DÜŞÜK) | §6 madde (1b): :92'nin bugünkü metni ve sonuna eklenecek adlı alt ad alanı istisnası önerisi; sahip kararıyla `.antigravity` kapısı onayına dahil | §6, §18 |
| F-5 | X-16 MOD-0178'i r29'a taşır, inceleme + onay yazılı değil (DÜŞÜK) | §5.1 X-16 ve §6 madde (4): r29 bağımsız inceleme + Ali onayı; sahip kararıyla MOD-0178 r28 ile aynı onay turunda; sıra yazıldı; §18 ve §24 kapısı | §5.1 X-16, §6, §18, §24 |
| F-6 | `recall` ailesi "danger / geri alma" karışık (DÜŞÜK) | öneri **`secondary`** (`perm-label.js:55`; `danger` `:53` silme ailesi) | §5.1 X-15 |
| F-7 | Hiç görev açılamayan denemenin `Excluded` üyelikleri sonraki başarılı serbest bırakmayı `PartiallyCompleted` yapar (DÜŞÜK) | `ReleaseAborted`'da bu denemenin `Excluded` üyelikleri aynı R3 işleminde `Removed` (`CloseReason = ReleaseAborted`); `PartiallyCompleted` koşulu yalnız dalgayı `Released` yapan serbest bırakmanın `Excluded`'larını sayar; AC-22 (c), AC-30 F-7 vakası + S-30c | §4.2, §8.3, §8.6, §16 AC-22 / AC-30 |
| F-8 | S-55b, 0178 önek süzgeci kopyalanırsa boşa çıkar (DÜŞÜK) | AC-55: klasördeki her controller önekten bağımsız alınır (`Mod0178WarehouseRouteTests.cs:228` süzgeci kopyalanmaz); uç sayısı taslaktaki işlem sayısına eşit | §16 AC-55 |
| F-9 | `PageHelpCatalog.cs` §5'te yok (DÜŞÜK) | §5'e yalnız eklemeli satır: `Related` `:17-34`, `StepIcons` `:36-108`; 0181 emsali `e0bf26918`, `9f8696dd4`; DL-4, DL-5 | §5 |
| Y-1 (ek) | Kararlar 03 eki: Y-1 = A | F-2 satırı; §19.4 Y-1 satırı | §8.19, §19.4 |
| AC-15 bölünmesi (r3 yan bulgusu) | r2'de AC-15'in kapatma vakaları DL-1'deydi, oysa erteleme ve port DL-2'dedir — DL-1'de ertelenmiş outbound oluşamaz | AC-15 (a)…(e) DL-1, (f)…(m) DL-2 kabulünde; §24 güncellendi | §16 AC-15, §24 |

### 19.7 r3 → r4 kapanış tablosu

Girdi: bağımsız inceleme r3 `docs/records/reviews/MOD-0180/2026-11-12-311bcfa3.md` (KOŞULLU; 3 orta, 4 düşük), kararlar 03 eki (Q-17, "hemen"
teyidi; commit `c30c47e90`) ve `.antigravity` kapısı commit'i `0aa802519`.

| # | Bulgu / karar | r4 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | Yetim Taslak üyelik (süreç ölümü, `AddWaveOrders` yarışı) ve "serbest bırakma ile yarış yoktur" cümlesi (ORTA) | **Seçim: üyelik kapanışı görev açılışından ÖNCE** (ayrı 0180 işlemi, sonra `IssueImmediateAsync`) — aynı ölüm siparişi üyeliksiz `Deferred` bırakır, bu bekleyen tanımıdır ve sonraki tur açar; yetim hiç doğmaz (r5'te yumuşatıldı — dar pencere kalır, §19.8 F-4). Reddedilen süpürme seçeneği gerekçesiyle yazıldı (yetim önce doğar, bir tur yaşar, ek tarama). **Sıralama:** `AddWaveOrders`, taşıma ekleme adımı ve R1 kendi işleminde ayar kaydına `Version` + `WaveManaged = true` koşullu yazım (`GateSeq` +1, `Version` artmaz); C1'den sonra işlenen 422 `WAVE_SETTINGS_REQUIRED`. §8.19 (d) düzeltildi (R1 C1'den önce → dalga kapatmadan önce serbest bırakılmış; sonra → 422); yeni (e). AC-15 (g) olay sırası, yeni (n1)…(n4); S-15i, S-15j, S-15k, S-15u | §4.5, §8.2, §8.6, §8.10, §8.19, §12, §13, §16 AC-15 |
| F-2 | AC-15 DL-2 sabotajlarının bir kısmı yazıldığı yoldan kırmızı olmaz (ORTA) | S-15f = 0178 işlemindeki `Deferred` / `Version` koşulunu kaldır → (j2) tur yarışında iki görev seti; replay ayrıca port düzeyinde iki ardışık çağrıyla (o1) ve S-15l; S-15b ve S-15g beklenen kırmızısı düzeltildi (port reddeder; kırmızı (f) sayımı ve (g) `failed: []`'den); (i), (j), (n), (p), (q) bakım turunu **üretim `WaveMaintenanceHostedService` tur yöntemiyle** sürer (§8.8) — S-15e bu yüzden kırmızı | §8.6, §8.8, §16 AC-15, §17 |
| F-3 | Zehirli sipariş turu süresiz tıkar; olay / kayıt sıklığı tanımsız (ORTA) | Hata sınıflandırması (bağlantı / altyapı ↔ siparişe özgü); bağlantıda tur durur, siparişe özgü hatada sipariş atlanır ve tur sürer; takılı sipariş sıranın sonunda; art arda 3 siparişe özgü hata turu durdurur; süreç içi art arda hata sayacı + `Warning` / `Error` log + OTel metriği; `PendingReleaseStuck[]` → GET ayar `stuckOrders[]`; olay `PendingOrdersStuckChanged`; satır kodu `WAVE_PENDING_ORDER_ERROR`; olay ve merkezi kayıt **yalnız bir şey değiştiğinde**. AC-15 (p1)…(p6); S-15m, S-15n, S-15o, S-15p, S-15v | §4.3, §4.5, §8.17, §8.19, §13, §16 AC-15, §21 |
| F-4 | `WmsPickTaskDto` listede yok; CSV sırası yazılı değil (DÜŞÜK) | X-7 ve §6 listesine `frontend/Diten.Web/Models/Warehouse/OutboundDtos.cs:169` `WmsPickTaskDto` (eklemeli `Wave` üyesi) ve `Row` `:332`; `ListQuery` `:109` liste ve dışa aktarımda (`:79`) ortak → `waveId` verilince CSV de dalga sırasında (bilinçli), sütunlar (`:97`) değişmez; AC-50 + S-50c | §5.1 X-7, §6, §16 AC-50, §19.2 |
| F-5 | `IssueImmediateAsync` `NotDeferred` vermiyor; `Releasing` üyelerinin sayımı tanımsız (DÜŞÜK) | Sonuç kümesine `NotDeferred` (`PickIssuance = null`); `Releasing` üyeleri `deferredOrderCount`'ta sayılır, `releasingOrderCount`'ta gösterilir, `remainingCount`'ta sayılmaz; AC-15 (l), (o2); S-15r, S-15s | §4.5, §5.1 X-4, §8.6, §8.19, §16 AC-15 |
| F-6 | İlk tur bütçesi yalnız sipariş sayısı; süre ölçülmüyor (DÜŞÜK) | Bütçe **200 sipariş ve 2.000 görev** (`PendingReleaseBatch`, `PendingReleaseTaskBudget`; serbest bırakma bütçesiyle aynı); bakım turu kalanı aynı turda partilerle bitirir; §8.7 (d) ve AC-27 (d) istek içi süre ≤ 15 s; AC-15 (q) + S-15q, S-15t | §8.7, §8.8, §8.19, §16 AC-15 / AC-27 |
| F-7 | §24 DL-1 sabotaj aralığı DL-2 sabotajlarını kapsıyormuş gibi; AC-15 (c) sayımı bekçi sanılabilir (DÜŞÜK) | §24 DL-1 ve DL-2 sabotajları **adıyla** (DL-1'de AC-15 için yalnız S-15, S-15c); AC-15 (c) sayımları "bilgi, bekçi değil" | §16 AC-15, §24 |
| Q-17 | Hedef tarih | **24 Kasım 2026** — künye `target: 2026-11-24`; §18, §19.4 | künye, §18, §19.4 |
| "Hemen" teyidi | Y-1'in "hemen"i (r3 incelemesi (c) 3) | §8.19'a sahip teyidi: ilk tur istek içinde ≤ 200 sipariş / 2.000 görev, kalanı en geç bir bakım turu (60 sn); hatalı durumların istisnası yazıldı | §8.19, §18, §19.4 |
| `.antigravity` kapısı | Y-4 kapısı (§6 (1), (1b), (2), (3)) | (1), (1b), (2), (3)-X-12 **yazıldı — commit `0aa802519`, sahip onayı Beste 2026-11-12**; X-16 (MOD-0178 §14.1 notu → r29) ve (4) **açık** | künye, §5.1 X-12 / X-16, §6, §8.14, §14, §18, §19.2, §22, §24 |

### 19.8 r4 → r5 kapanış tablosu

Girdi: bağımsız inceleme r4 `docs/records/reviews/MOD-0180/2026-11-13-7e92bce7.md` (KOŞULLU; 1 orta, 5 düşük; hedef r4 LF sha256 `7e92bce7…`).
Her bulgu incelemenin önerisiyle kapandı; seçim gereken yerde seçim ve gerekçesi bölümündedir.

| # | Bulgu | r5 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | Art arda hata sınırı zehirli siparişi geri getiriyor — en eski üç kalıcı hatalı sipariş her turda yine ilk denenip turu durduruyor, `stuckOrders` boş (ORTA) | Sınır turu durdursa da **tetikleyen siparişler `PendingReleaseStuck[]`'a yazılır** (partinin delta yazımıyla) ve sonraki tur onları sıranın sonuna alıp önce takılı olmayanları açar; yaygın arızada listeye **parti başına en çok 10** yeni sipariş (`PendingReleaseMaxNewStuckPerBatch`, `NewStuckLimit`); liste **en çok 1.000** (`PendingReleaseStuckMax`, `StuckListFull`); sınırlar yalnız takılı olmayanları sayar; tetikleyenlerin `failed[]` kodu **`WAVE_PENDING_ORDER_ERROR`**, turun durma nedeni yeni `pendingRelease.stopReason`'da. AC-15 (p6) yeniden yazıldı — `stuckOrders = [P1, P2, P3]`, sonraki turda P5 açılır; yeni (p7), (p8); S-15v yeniden yazıldı; yeni S-15w, S-15x, S-15y | §4.5 (f), §8.17, §8.19 adım 3 ve "Kısmi başarısızlık", §13, §16 AC-15 |
| F-2 | `PendingReleaseStuck[]` yaşam döngüsü eksik (DÜŞÜK) | (a) yazım `Version`'ı **artırmaz**; (b) yazım işlem içi okunan son belgeye delta olarak, **`WaveManaged = false` koşullu** — eşzamanlı yazanlar yazım çakışmasıyla sıralanır, son yazan kazanmaz; (c) **seçim: depo yeniden açılınca liste temizlenir** (`SettingsUpdated.Data.clearedStuckOrders[]`, gösterge 0) — "yalnız kapalı depoda göster" bayat listeyi saklayıp yeniden kapatmada diriltirdiği için reddedildi; (d) her tur başında listedeki ama artık bekleyen olmayanlar çıkar (`NoLongerPending`), bakım turu boş olmayan takılı listeyle de tetiklenir; (e) `Code` kararlı kategori `OutboundUnreadable` \| `PlanInconsistent` \| `Unexpected`, istisna türü adı yalnız log'da; `Code` ilk hatanınkidir, kategori değişimi liste değişikliği sayılmaz. AC-15 (p1) kategori, (p3) kategori değişimi; yeni (p9)…(p12); S-15z, S-15aa, S-15ab, S-15ac, S-15ad, S-15ae, S-15af | §4.3, §4.5, §8.8, §8.19, §13, §16 AC-15, §21 |
| F-3 | Bakım turunun bitişi yalnız "partide ilerleme yok"a bağlı; kod değişimi ilerleme sayılıyor (DÜŞÜK) | **Bir turda her outbound en çok bir kez denenir** (turun denenmiş kümesi); **tur başına en çok 50 parti** (`PendingReleaseMaxBatchesPerTour`); "partide ilerleme yok" koşulu kaldırıldı, durma koşulları yeniden yazıldı (tur sonludur); kod değişimi artık değişiklik değil (F-2 e); takılı yeniden deneme tur başına en çok 50 (`PendingReleaseStuckRetryPerTour`), son denemesi en eski olan önce — aynı ilk takılılar her turda denenip geri kalanı aç kalmasın diye. "Hemen" teyidinin kapsamı tur sınırıyla yazıldı (200 + 50 × 200 sipariş). AC-15 yeni (p13), (p14), (p15); S-15ag, S-15ah, S-15ai, S-15aj, S-15ak | §8.8, §8.19 ("hemen", adım 2, adım 4), §13, §16 AC-15 |
| F-4 | "Yetim üyelik hiç doğmaz" cümlesi (ii)–(iii) penceresini kapsamıyor (DÜŞÜK) | Cümle yumuşatıldı (künye r4 metni, §8.19, §19.7 F-1 satırı); kalan pencere (tur başına en çok bir sipariş, yeniden açma + yeniden ekleme gerekir) ve **kurtarma yolu** (serbest bırakma ön kontrolü 422 `WAVE_ORDERS_INELIGIBLE`, kullanıcı çıkarır) §8.19'a yazıldı. **Seçim:** (ii) işlemi ayar kaydına **`WaveManaged = false` koşullu kapı yazımı** yapar (`GateSeq` +1, `Version` artmaz) — (i) ile (ii) arasında yeniden açılan depoda üyelik kapanışını engeller ve pencereyi (ii)–(iii)'e daraltır; pencereyi tümden kapatmak 0178 işleminin 0180 belgesine yazmasını gerektirdiği için seçilmedi. AC-15 yeni (n5) (bekçi, S-15al) ve (n6) (kurtarma yolunun kanıtı, bekçi değil); AC-27 (d) bu yazımı en kötü durumda sayar | künye, §8.19 adım 2 (ii), §13, §16 AC-15 / AC-27, §19.7 |
| F-5 | S-15f'in yazılan kırmızısı (iki görev seti) oluşmaz (DÜŞÜK) | Beklenen kırmızı (j2)'nin **"kaybeden replay alır"** iddiasından; 0178 tekil `(TenantId, OutboundId, Seq)` olay index'i (`WarehouseIndexConfiguration.cs:147-148`; `Seq` işlem dışı okumadan, `OutboundShipmentReads.cs:124`) **ikinci bekçi** olarak §8.6 ve §8.19 (c)'de anıldı | §8.6, §8.19 Yarış (c), §16 AC-15 S-15f, §19.1 |
| F-6 | Metin ve ölçüm ayrıntıları (DÜŞÜK) | (a) künye r4 metni `S-15i…S-15v`; (b) AC-15 (n3): W4'ün üyeleri **C1 isteğinin ilk turunda** (R1 beklerken) açılır; (c) (g)'nin olay sırası **bilgi**, asıl bekçi (n1); S-15i'nin kırmızısı yalnız (n1); (d) §24 DL-5 `S-53a`, `S-53b`; (e) AC-27 (d) ve §8.7 (d) fikstürü en kötü durum — 200 siparişin hepsi Taslak üyelikli; (f) metrik adları depo deseninde: `inventory_wave_pending_release_order_errors_total` (etiket `code` = kategori), `inventory_wave_pending_release_stuck_orders` (etiket `warehouse_id`) — `RefusalAuditMetrics.cs:13` emsali; asıl alarm gösterge > 0 süresine dayanır (öneri 15 dk; süreç içi sayaç yeniden başlamada sıfırlanır) | künye, §8.7, §8.19, §16 AC-15 / AC-27, §24 |
| CT eki — X-16 | MOD-0178 §14.1 notu (r29) yazıldı mı | **Yazıldı** — MOD-0178 r29 `adfe1bd7d`, bağımsız inceleme UYGUN (`docs/records/reviews/MOD-0178/2026-11-13-2e4036b6.md`); not §14.1 `:2128-2152` (r4'teki `:2117-2120` atfı r29'da kaydı); **Ali'nin onay turu (r29 + bu paket) açık** | künye, giriş, §5.1 X-16, §6 (3)-(4), §14, §18, §19.4 Y-4, §24 |
| CT eki — r29 incelemesi F-1 (DÜŞÜK) | X-16'nın "MOD-0178 `warehouse.waves.*` anahtarı tanımlamaz" yasağını ölçen bekçi yok (`WarehouseRegistrationTests.cs:37` yalnız `StartsWith("warehouse.")`) | **CT kararı (sahip onayı bekliyor):** bekçi MOD-0180 DL-1'de, AC-5'e vaka olarak (AC numarası eklenmedi) — 0178 üretim manifestinde `warehouse.waves.` önekli anahtar yok, 0180 manifestindeki her anahtar `warehouse.waves.` önekli; test 0180 test ağacında, 0178 testleri değişmez; **S-5c:** 0178 manifestine geçici `warehouse.waves.x` → kırmızı | §5.1 X-16, §14, §16 AC-5, §18, §24 DL-1 |

### 19.9 r5 → r6 kapanış tablosu

Girdi: bağımsız inceleme r5 `docs/records/reviews/MOD-0180/2026-11-13-0517db83.md` (KOŞULLU; 1 orta, 5 düşük; hedef r5 LF sha256 `0517db83…`)
ve sahip cevapları (Beste 2026-11-13, sahip sohbet cevabı; kararlar 03 eki): Q-23 = A, X-16 bekçisi onaylı. Bu tur yeni mekanizma eklemez;
her bulgu en dar düzeltmeyle kapandı.

| # | Bulgu | r6 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | "Hemen" teyidi sahibe sorulmadan daraltılmış; 50 parti sınırının kapsamı (depo / tur) belirsiz; tur süresi yazılı değil (ORTA) | Sahibe **Q-23** soruldu → **A** (Beste 2026-11-13; karar 03 eki; Y-1 satırı Q-23'e atıfla düzeltildi). Sınır **depo başına** (bir depoda bir turda en çok 50 parti); depolar sabit sırayla **dönüşümlü** (her geçişte her depoya bir parti — aç kalma yok); bakım turunun süresi en kötü ~12,5 dk (çıkarım), sonraki tur önceki bitince başlar; dalga uzlaştırması **ayrı döngüde** (`RunReconcileOnceAsync` / `RunPendingReleaseOnceAsync`), açılış turunun uzunluğundan etkilenmez. Yeni AC-15 (p17); S-15aq, S-15ar | künye, §8.8, §8.19 ("Tur üst sınırı", adım 4), §13, §16 AC-15, §18, §19.4, §24 |
| F-2 | Liste doluyken tek kalıcı hatalı sipariş depoyu kilitliyor (DÜŞÜK) | Liste doluyken yeni hatalı sipariş **listeye alınmadan atlanır** (`failed[]` `WAVE_PENDING_ORDER_ERROR`, log, sayaç) ve **tur sürer**; liste doluyken (a) / (b) sınırları işlemez; `stopReason` `StuckListFull` kaldırıldı; **kurtarma yolu** (nedeni gidermek ya da depoyu yeniden açıp kapatmak — yeniden açma listeyi temizler) yazıldı. (p8) yeniden yazıldı; S-15y yeniden yazıldı; yeni S-15am | §8.17, §8.19 adım 3, adım 4, "Kısmi başarısızlık" (c), §13, §16 AC-15 |
| F-3 | Bağlantı sınıfı hatada görünmeyen aç bırakma (DÜŞÜK) | Aynı outbound art arda **3** turda turun ilk denediği sipariş olarak turu bağlantı sınıfı hatayla durdurursa siparişe özgü sayılır, takılı listeye girer (`Code` = `RepeatedConnectionError`), tur sürer (`PendingReleaseConnectionStopsBeforeStuck`); gösterge `inventory_wave_pending_release_connection_stopped_tours` + `Error` log'u + alarm önerisi (≥ 5). Bakım turunda bağlantı durması o deponundur. Yeni AC-15 (p16); S-15ao, S-15ap | §4.5 (e), §8.19 "Kısmi başarısızlık", §13, §16 AC-15, §17 |
| F-4 | Üyeliksiz siparişte pencere daha geniş (DÜŞÜK) | Kalan pencere: üyelikli siparişte **(ii)–(iii)**, üyeliksiz siparişte **(i)–(iii)**; sonuç ve kurtarma yolu aynı | §8.19 adım 2 (ii), "Kalan pencere", §13 |
| F-5 | S-5c'nin beklenen sonucu yanlış (DÜŞÜK) | S-5c: 0178 manifestine sayfa eylemi + `WarehousePermissions.Implemented` + bir `Controllers.Warehouse` eylemine `[HasPermission("warehouse.waves.x")]` → yeni vaka kırmızı, `WarehouseRegistrationTests.cs:37` / `:39` / `:56` yeşil (satırlar `d834454ad`'de ölçüldü); yeni **S-5d** (0180 manifestine `warehouse.x` → vaka kırmızı). AC-5 vakası **sahip onaylı 2026-11-13** | §14, §16 AC-5, §18, §24 DL-1 |
| F-6 | Metin ayrıntıları (DÜŞÜK) | (a) (p10b): delta eşleşmezse `PendingOrdersStuckChanged` yazılmaz; açılan / kapanan yoksa olay 0, merkezi kayıt 0; (b) 50 parti sınırında `stopReason = RoundBudgetReached`; (c) liste dolu `Error` log'u duruma **girişte bir kez**, çıkışta bir `Info` (S-15an) | §4.5 (b), §8.19 adım 3, "Kısmi başarısızlık" (c), §16 AC-15 (p8), (p10b), (p14) |

### 19.10 r6 → r7 kapanış tablosu

Girdi: bağımsız inceleme r6 `docs/records/reviews/MOD-0180/2026-11-13-127005fa.md` (KOŞULLU; 1 orta, 4 düşük; hedef r6 LF sha256 `127005fa…`).
Seçimler CT kararıdır (sahibe yeni soru açılmadı): F-1'de incelemenin önerdiği en dar yol (her geçişte depo kümesini yeniden tarama) seçildi,
böylece sahibin teyidi (Q-23 = A, karar 03 eki) değişmeden kalır. Yeni test atıfları HEAD **`90f6a1da8`**'de ölçüldü
(`WarehouseRegistrationTests.cs:25-57`, `:41-47`, `:80-97`, `:100`, `:122`, `:130-149`, `:174`, `:179-188`, `:195-209`;
`WmsBundleContractTests.cs:243-244`; `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:97`; `PickTasksController.cs:23`).

| # | Bulgu | r7 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | Depolar arası gecikme sahibin teyidiyle çelişiyor — sabit depo listesi ve "sonraki tur önceki bitince" tur sırasında kapanan depoyu turun sonuna bırakıyor (ORTA) | **Uygun depo kümesi her geçişin başında yeniden taranır** (`WaveManaged = false` + kalıcı durma koşuluna ulaşmamış + işi olan; `limit 1` sorgusu, tur denenmiş kümesi dışlanır); tur sırasında kapanan depo **sıradaki geçişe katılır**; koşul (1) kalıcı değildir — işi biten depo yeni bekleyenle (Yarış (b)) aynı turda geri döner; yeniden kapatılan depo da katılır. Süreler net yazıldı: geçiş en kötü (depo sayısı) × 15 s, tur en kötü depo başına ~12,5 dk (D kalabalık depoda ~D × 12,5 dk), yeni kapanan depo tur koşuyorsa **en geç bir geçiş**, koşmuyorsa **en geç 60 sn** bekler; "60 sn" başlama gecikmesi, geçiş / parti süresi teyidin "işlem" kısmıdır. 8.19 "hemen" teyidi metni, Yarış (b), §8.8 ve Q-23 satırı (r7 notu) buna göre; yeni AC-15 (p18a), (p18b); S-15as, S-15at | §8.8, §8.19 ("hemen", "Depolar arası sıra", adım 4, Yarış (b)), §13, §16 AC-15, §19.4 Q-23 |
| F-2 | Liste doluyken log ve yük seli (DÜŞÜK) | Depo başına tur başına **tavan** `PendingReleaseListFullSkipsPerTour` = **200** (gerekçe: bir parti bütçesiyle aynı büyüklük, en kötü yük 200 çağrı / ≤ 15 s — r6'nın 1/50'si; 4 parti yükü dört katına çıkarırdı, imleç zaten ilerlemeyi sağlar); tavanda o depo için tur durur, yeni `stopReason = ListFullSkipLimit` (adım 4 koşul (6), yalnız bakım turu); **imleç** — liste doluyken sonraki tur takılı olmayanları kaldığı konumdan dairesel sürer, kilitlenme yok (her bekleyen en geç ⌈N / 200⌉ turda bir denenir); liste doluyken sipariş başına `Warning` yerine **parti başına özet** `Warning`; sayaç sipariş başına sürer. (p8) özet log; yeni AC-15 (p19); S-15au, S-15av, S-15aw | §8.17, §8.19 adım 2, adım 3, adım 4, "Kısmi başarısızlık" (c) ve log paragrafı, §13, §16 AC-15, §17 |
| F-3 | `RepeatedConnectionError` kuralının açıkları (DÜŞÜK) | (a) **Başarı şartı:** sayaç yalnız aynı turda önce sonuç dönen bir 0178 çağrısı varsa artar; yoksa değişmez — bunun ölçülebilmesi için turu bağlantı hatasıyla durduran sipariş **şüpheli** işaretlenip sonraki turlarda takılı olmayanların sonunda denenir (aksi halde en eski sipariş hep ilk çağrı olur ve sayaç hiç artmazdı); "gerçek kesintide yanlışlıkla takılmaz" cümlesi doğru kapsamla yeniden yazıldı (tam kesintide takılmaz; kısmi kesintide sağlam sipariş üç ardışık denemesi de denk gelirse takılabilir, kayıp yok, ilk başarıda çıkar); servis kapanışı iptali sayılmaz. (b) "İlk denenen" koşulu kaldırıldı — turu durduran her siparişin sayacı başarı şartıyla işler, denenmeyenin sayacı değişmez; bağlantı durması `Warning` log'u (`connectionStops`, `priorSuccessInTour`). (c) Sayaç yalnız sonuç, bekleyenden çıkış ya da süreç yeniden başlamasıyla sıfırlanır (r6'nın "bağlantı hatasız turda sıfırla" kuralı kalktı); eşikte liste doluysa sipariş atlanır, F-2 tavanına sayılır, sayaç eşikte kalır; takılı yeniden denemede başarı kanıtlı bağlantı hatası turu durdurmaz. (p16) yeniden yazıldı — (p16a)…(p16d); S-15ao, S-15ap güncellendi; yeni S-15ax, S-15ay, S-15az, S-15ba, S-15bc | §8.17, §8.19 adım 2, "Kısmi başarısızlık", §13, §16 AC-15, §17 |
| F-4 | S-5c metni ve X-16 dayanağı kısmen yanlış (DÜŞÜK) | S-5c'nin dokunuşları sabitlendi: eylem **`PICKTASKS`** sayfasına, HTTP eylemi **`PickTasksController`**'a **`GET`** (WarehouseLocationRolesController — `:41-47`, `:195-209` — ve WarehouseMonitoringController — AC36 `:179-188` — dışarıda; PUT / DELETE değil); **beklenen ek kırmızılar adıyla:** AC34 (`:130-149`), `WmsBundleContractTests.Every_served_warehouse_route_is_an_operation_with_its_permission_key` (`:243-244`), Gateway koşulursa `Mod0178WarehouseRouteTests` (`:97`); yeşil kalanlar `:25-57` kayıt testi, AC20, AC36. X-16, §14 ve AC-5'teki "bugün ölçen test yok" / "boşluğu yalnız yeni vaka yakalar" cümleleri daraltıldı: kazara eklemeyi 0178 sabitlemeleri dolaylı yakalar, gerçek boşluk sabitlemeleri de güncelleyen bilinçli eklemedir | §5.1 X-16, §14, §16 AC-5, §24 DL-1 |
| F-5 | İki döngü ayrımının bekçisi yok (DÜŞÜK) | Yeni AC-15 **(r)** (DL-3 kabulünde — uzlaştırma DL-3'tedir): üretim servisi iki döngüsüyle koşar, açılış turu bir 0178 çağrısında kancayla bekletilirken uzlaştırma döngüsü tur atar (W5 `Completed`); **S-15bb** döngüleri birleştirir → kırmızı. §8.8'deki "tasarım kuralı, bekçi değil" cümlesi kaldırıldı; açılış döngüsü aralığı öneri `Warehouse:Wave:PendingReleaseSeconds` | §8.8, §13, §16 AC-15, §17, §24 DL-3 |

### 19.11 r7 → r8 kapanış tablosu

Girdi: bağımsız inceleme r7 `docs/records/reviews/MOD-0180/2026-11-13-f357b120.md` (KOŞULLU; 1 orta, 3 düşük; hedef r7 LF sha256 `f357b120…`)
ve kararlar 03 eki X-14 (sahip onayı Beste 2026-11-13, "tamam", commit `c7dc4ea6a`). Seçimler CT kararıdır (sahibe yeni soru açılmadı); her
bulguda incelemenin önerdiği en dar yol seçildi. Yeni atıflar HEAD **`0b5062383`**'te ölçüldü (`git diff --stat 90f6a1da8 0b5062383 --
services gateway docs/analysis frontend` boş — r7 atıfları geçerli).

| # | Bulgu | r8 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | X-14, §15, AC-55 ve §24 kapanış cümlesi 0178 Gateway testinin değişmeden yeşil kalacağını söylüyor; `:97` uç sayısını 73'e sabitliyor, DL-1 ile kırmızı (ORTA) | X-14 **XMC-001 satırı** oldu: dosya `Mod0178WarehouseRouteTests.cs` `:97` (+ `:80-96` yorumu), sahip onayı `c7dc4ea6a`, sahibi integration-agent. Uç ekleyen her dilim (DL-1, DL-2, DL-3, DL-5 — §23 / §24'ten doğrulandı: DL-4 yeni servis ucu açmaz, X-7 var olan `GET /pick-tasks`'a sorgu parametresidir; DL-6 yalnız test) `:97`'yi **dilim başında ölçülen sayı + dilimin eklediği 0180 uç sayısı** yapar, uçlarıyla **aynı commit'te** (MOD-0181 emsali) — arada kırmızı pencere yok. Beklenen dağılım çıkarım olarak yazıldı (14 / 2 / 5 / 2 = 23; 73 → 96). AC-55'e **0178 sayı vakası** (DL-1, DL-2, DL-3, DL-5 kabulünde) + **S-55c**; §15 r7 cümlesi kaldırıldı, yerine sayı paragrafı; §24 kapanış cümlesi "tek istisna X-14" ile; S-5c (iv)'te sabit 73 yerine "dilim başında ölçülen + DL-1 uçları + 1"; §5 Repo Scope satırı, §5.1 son paragraf istisnası, §6, §7 Gateway satırı, §17 Build, §18 iki madde, §19.2 | §5, §5.1 X-14, §6, §7, §15, §16 AC-5 / AC-55, §17, §18, §19.2, §24 |
| F-2 | Şüpheli sıralaması takılı yeniden denemeyi aç bırakabilir; :1084-1085 "depoda başka bekleyen yok" daraltması eksik (DÜŞÜK) | **Şüpheliler takılı yeniden denemelerden sonra** (turun en sonu) sıralanır. Gerekçe: iki seçenekten daha dar olanı — yalnız var olan sıralamadaki yer değişir; öteki ("başarılı çağrı yokken şüphelinin durdurduğu turda takılı yeniden denemeler yine koşsun") "bağlantı hatası turu durdurur" kuralına istisna açar ve kesintide depo başına tur başına 50'ye kadar ek başarısız çağrı ekler. Akış adım 2 sırası, "(a) Şüpheli sıralaması" (seçim gerekçesiyle), takılı sipariş bullet'ı, imleç cümlesi, §13 satırı; :1084-1085 cümlesi doğru koşulla yeniden yazıldı (takılı olmayan başka bekleyen yok **ve** takılı yeniden denemelerin hiçbiri sonuç döndürmüyor; takılı liste aç kalmaz). Yeni AC-15 **(p16e)** ("X + takılı liste, başka bekleyen yok") ve **S-15bd** (r7 sırası → (p16e) tur 2 yalnız X, S1 açılmaz) | §8.19 adım 2, "Kısmi başarısızlık", §13, §16 AC-15, §17, §24 DL-2 |
| F-3 | S-5c (3)'teki geçici GET'in şablonu yok; şablonsuz GET mevcut liste GET'inin anahtarına düşer, (iii) kesin değil (DÜŞÜK) | Sabit şablon **`[HttpGet("wave-sabotage")]`** → `GET /api/warehouse/pick-tasks/wave-sabotage`; şablonsuz `[HttpGet]` yasak (gerekçe: `:31` ile aynı anahtar, `served` dizinleyicisi `:239` üzerine yazar; host testlerinde AmbiguousMatch). Beklenen sonuç bu rotayla: (iii) `:243`'te kırmızı (sözleşmede olmayan anahtar; `:244` 49 olurdu ama önce `:243` düşer), (iv) `:97`'de kırmızı (sayı + 1), rota iddiaları `:98-108` yeşil; harf dizili kesim `{taskId}` ile çakışmaz (çıkarım) | §16 AC-5 (S-5c) |
| F-4 | Tarama maliyeti cümlesi yanlış — r7 sorgusu turun denenmiş kümesini dışlar, küme depo başına 10.000 kimliğe çıkabilir (DÜŞÜK) | **Mekanizma değişmedi, maliyet doğru yazıldı:** dışlama kümesi depo başına tur içinde ≤ 50 × 200 = 10.000 kimlik; aynı küme r5'ten beri parti sorgusunda da var; her geçişte kalıcı durmamış her kapalı depo için bir tarama + depo başına tur başına ≤ 50 parti sorgusu, her biri bu süzgeçle; r6'dan pahalı, yük tur süresine yayılır (çıkarım, DL-2'de ölçülür). Öteki yol ("işi var"ı son parti sonucu + `CreatedAt` / imleçten türetmek) Yarış (b)'deki geç `Deferred` yazılan, `CreatedAt`'i konumun gerisindeki siparişi o turda kaçırır (AC-15 p18b sözünü zayıflatır) ve şüpheli / liste dolu imleci için ayrı kural ister — daha geniş olduğu için seçilmedi | §8.19 "Depolar arası sıra", §19.1 koşulmayanlar (r8) |

### 19.12 r8 → r9 kapanış tablosu

Girdi: bağımsız inceleme r8 `docs/records/reviews/MOD-0180/2026-11-13-e2666ea4.md` (KOŞULLU; 1 orta, 1 düşük; hedef r8 LF sha256
`e2666ea4…`) ve kararlar 03 eki X-14 kapsam teyidi (Beste 2026-11-13, "evet", commit `6235794ab`). F-1'de incelemenin önerdiği en dar yol
seçildi; F-2 sahip teyidiyle kapandı. Sahibe açık soru yok. Yalnız metin değişti.

| # | Bulgu | r9 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | AC-55'in klasör / WMS-WAVE-BUNDLE taslak eşitliği DL-6 DL-5'ten önce koşarsa kırmızı (taslak DL-1'de 23 işlem; klasörde DL-1 / DL-2 / DL-3 sonrası 14 / 16 / 21 uç); paket "DL-6 DL-1'den sonra her an" diyordu (ORTA) | **DL-6 DL-5 kabulünden sonraya sabitlendi** (incelemenin en dar yolu; taslağı dilim etiketiyle sınırlama seçilmedi — taslak ve sözleşme testinde ikinci bir kural açardı). §24 sıra cümlesi `DL-1 → DL-2 → DL-3 → DL-4 → DL-5 → DL-6`, "her an" ve "ne zaman koşarsa koşsun" cümleleri kaldırıldı; §24 DL-6 satırına "DL-5 kabulü" kapısı; §23 DL-6 satırı; §15 paragrafı; AC-55 başlığı ve "Sıra (r9)" cümlesi; S-55c'deki "`Mod0180WaveRouteTests` o dilimde varsa" ifadesi "uç ekleyen dilimlerde henüz yoktur" oldu; §17 Build; §18 Gateway maddesi | §15, §16 AC-55, §17, §18, §23, §24 |
| F-2 | Karar 03 X-14 eki tek güncelleme onaylıyor, paket dört dilimi `c7dc4ea6a`'ya bağlıyor; kabul kayıtlarında satır numarası kayar (DÜŞÜK) | Sahip teyidi alındı ve karar 03'e eklendi (`6235794ab`: onay uç ekleyen **her** dilimi — DL-1, DL-2, DL-3, DL-5 — kapsar, her biri dilim başında ölçülen sayı + kendi uçlarıyla; ayrı sahip onayı gerekmez). X-14 atıfları `c7dc4ea6a` + `6235794ab` oldu (§5.1 X-14, §6, §15, AC-55, §18 iki madde, §24); dilim kabul kayıtları önce → sonra değerini yazar ve satır numarasına değil `Assert.Equal(<n>, endpoints.Count)` satırına atıf yapar (`:97` yalnız bugünkü HEAD ölçümü) | §5.1 X-14, §6, §15, §16 AC-55, §18, §19.1, §24 |

### 19.13 r9 → r10 kapanış tablosu

Girdi: r9 bağımsız kontrolü `docs/records/reviews/MOD-0180/2026-11-13-21e14601.md` (UYGUN; hedef r9 LF sha256 `21e14601…`), CT'nin kod
bulgusu (HEAD `ad1100158`'de doğrulandı) ve kararlar 03 eki X-17 (Beste 2026-11-13, "evet", commit `ad1100158`). Sahibe açık soru yok.
Yalnız metin değişti.

| # | Bulgu | r10 değişikliği | Bölüm |
|---|---|---|---|
| K-1 | MOD-0173 `InventoryLedgerManifestCompletenessTests.Every_enforced_permission_is_reachable_from_the_manifest` servisin bütün controller'larını tarar, yalnız adlı namespace'leri dışlar (`:213-223`); `Controllers.WavePlanning` dışlanmazsa DL-1 testi kırmızı — paket anmıyordu (CT bulgusu) | **X-17** XMC-001 satırı: DL-1 dışlama listesine `.Controllers.WavePlanning` satırı yorumuyla, ilk 0180 controller'ıyla aynı commit'te (MOD-0181 emsali `446cb58db`); sahip onayı `ad1100158`. §5 Repo Scope satırı; §5.1 son paragraf (MOD-0173 bekçileri X-9, X-10, X-17); §6 korunan yol maddesi ve "MOD-0173 dosyası yok" cümlesi düzeltildi (X-9, X-10, X-11, X-17); AC-5'e INVENTORY-LEDGER bütünlük vakası ((a) 0180 manifest bütünlüğü, (b) MOD-0173 testi) + **S-5e** (dışlama satırı kaldırılırsa `InventoryLedgerManifestCompletenessTests` kırmızı); §17 Mimari; §18 XMC maddesi (X-1…X-17); §24 DL-1 satırı (seam X-17, sabotaj S-5e) ve kapanış cümlesi (istisnalar X-14 ve X-17) | §5, §5.1 X-17, §6, §16 AC-5, §17, §18, §19.1, §24 |

## 20. Follow-up Items
- **FU-0180-RCS** — `WAV`'ın MOD-0040-FU02 ortak servisine geçişi (DL-8 + FU-0178-RCS-01 ile; Q-4).
- **FU-0180-CARRIER** — taşıyıcı süzgeci ve "farklı taşıyıcı" uyarısı (MVP-6 taşıyıcı verisi; Q-1).
- **FU-0180-CUSTOMER** — müşteri kimliğiyle süzgeç (O2C; Q-21).
- **FU-0180-TZ-LOCATION** — saat diliminin LOCATION `Warehouse`'a taşınması (Q-3).
- **FU-0180-AUTO** — kurala göre periyodik otomatik dalga (D-1 v1 dışı).
- **FU-0180-ZONEORDER** — bölge sırası ayarı (Q-9). **FU-0180-ROUTE-2** — kat / tek yön / sıcaklık bölgesi maliyetleri.
- **FU-0180-PICKER-SPLIT** — toplayıcıya rota bölme ve yük dengeleme.
- **FU-0180-WEIGHT** — ağırlık / hacim (MOD-0290 P9 ambalaj verisi; D-12).
- **FU-0180-EVENTS** — uzlaştırma turu yerine olay yolu.
- **FU-0180-AI** — Blueprint AI Assist (Q-14). **FU-0180-EA** — Blueprint Build/Buy düzeltmesi (Q-13).
- **FU-0180-READAUDIT** — KPI / CSV okuma denetimi (Q-18).
- **FU-0180-SETTINGS-UI** — depo dalga ayarı ekranı (v1 yalnız API; izin `warehouse.waves.settings.configure`, Q-6 = B).

## 21. Audited Events

Servisin denetim altyapısı **bugün var**: `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (`inventory-merkezi-iletim | b | işaret |
ICentralAuditDeclaration`). Kayıt ad ve gövde taşımaz; "olay adı" = `RequestType` (komut sınıfı) + `Operation` (Platform enum adı); kategori
`MasterData` (servisin); `SourceModule` = `WAVE-PLANNING`. Önceki / sonraki değerler ve gerekçeler yalnız `wms_wave_events.Data`'dadır (aynı işlem,
silinmez). Başarılı ve yazan komut 1 kayıt; replay / 0 kayıt 0; başarısız 0 (§8.13). 0178 portunun yazımları çağıran 0180 komutunun kaydına enlist
olur.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateWaveCommand | CreateWaveCommand + Create | Wave | b | Sonraki: Number, WarehouseId, WaveType, CutoffLocal / CutoffAtUtc, TimeZoneId, ShipToFilter, MaxOrders, MaxLines, ResponsibleUserId, Note | — |
| UpdateWaveCommand | UpdateWaveCommand + Update | Wave | b | Önceki / sonraki: değişen form alanları | — |
| AddWaveOrdersCommand | AddWaveOrdersCommand + Update | Wave (+ WaveOrder) | b | eklenen OutboundId / numara, `AddedVia`, sınır uyarıları | — |
| RemoveWaveOrdersCommand | RemoveWaveOrdersCommand + Update | Wave (+ WaveOrder) | b | çıkarılan siparişler, gerekçe (varsa) | — |
| CancelWaveCommand | CancelWaveCommand + LifecycleTransition | Wave | b | önceki durum, **gerekçe**, kapanan üyelikler | — |
| ReleaseWaveCommand | ReleaseWaveCommand + LifecycleTransition | Wave | b | sipariş / görev sayısı, `RouteSummary`, sınır onayı, hariç kalanlar, açılan görev numaraları (0178 yazımları bu kayda enlist) | — |
| CompleteWaveReleaseCommand | CompleteWaveReleaseCommand + Execute | Wave | b | kurtarma sonucu (açılan / hariç / replay sayıları); yalnız yazacak şey varsa | — |
| RecallWaveRemainingCommand | RecallWaveRemainingCommand + LifecycleTransition | Wave | b | **gerekçe**, çekilen ve başlamış siparişler, iptal edilen görev numaraları | — |
| ResolveWaveExceptionCommand | ResolveWaveExceptionCommand + Execute | WaveException | b | eylem (çıkar / taşı / yok say), hedef dalga, **gerekçe** | — |
| ReconcileWaveCommand | ReconcileWaveCommand + Execute | Wave | b | durum geçişi, `CompletedAt`, açılan / kapanan istisnalar, `OrderCancelled` üyelikler; yalnız yazacak şey varsa gönderilir (0178 sweeper deseni) | — |
| UpdateWaveWarehouseSettingsCommand | UpdateWaveWarehouseSettingsCommand + Update | WaveWarehouseSettings (+ kapatmada 0178 outbound / görev, WaveOrder) | b | önceki / sonraki: WaveManaged, TimeZoneId, eşikler, **gerekçe**; izin `warehouse.waves.settings.configure` (Q-6 = B); kapatmada (Y-1 = A, §8.19) ilk turun açtığı outbound / görev numaraları, `Removed` üyelikler ve (r4) ilk turda takılan siparişler, (r5) `stopReason` — 0178 port yazımları bu kayda enlist; kapatma reddedilmez (r2'nin 409'u kaldırıldı); (r5, F-2 c) yeniden açmada (`false` → `true`) temizlenen takılı liste (`clearedStuckOrders[]`) aynı kayıtta | — |
| ReleasePendingOrdersCommand | ReleasePendingOrdersCommand + Execute | WaveWarehouseSettings (+ 0178 outbound / görev, WaveOrder) | b | r3, §8.19: açılan outbound / görev numaraları, `Removed` üyelikler, atlanan / hatalı siparişler ve kodları, C1 olayına atıf (gerekçe kapatma isteğinin); r4: takılan / çözülen siparişler (`PendingReleaseStuck[]`, olay `PendingOrdersStuckChanged`); yalnız bakım servisi gönderir (depoda bekleyen varken, parti başına; r5: boş olmayan takılı listeyle de — tur başı uzlaştırma; r6: depo başına turda en çok 50 parti; r7: depo kümesi her geçişte yeniden taranır, liste doluyken atlama tavanı — kayıt kuralı değişmez); **parti başına 1 kayıt yalnız parti bir şey değiştirdiyse** (sipariş açıldı, üyelik kapandı ya da takılı listeye sipariş girdi / çıktı) — takılı sipariş aynı ya da (r5) farklı kategoriyle yine başarısızsa yazım yok, kayıt 0 (F-3); takılı liste yazımı `Version`'ı artırmaz, `WaveManaged = false` koşullu (r5, F-2); 0178 port yazımları bu kayda enlist | — |

**Yazma olmayan:** KPI okuması ve CSV (`ExportWaveKpiCsvQuery`) okumadır, merkezi kayıt doğurmaz (AUD-001 §7 okuma denetimi v1 dışı, Q-18).
Kayda yazılmayacak alan: yok (parola / token / özel nitelikli veri taşınmaz; `ShipToFilter` serbest metindir — KVKK için olay `Data`'sına yazılır,
log / OTel'e yazılmaz).

## 22. Record Codes

| Kayıt türü | Sınıf | TÜR öneki | Üretici | Gerekçe |
|---|---|---|---|---|
| Dalga (`Wave.Number`) | OTOMATİK | `WAV` (D-4) | hedef MOD-0040-FU02; ara MOD-0178 geçici sayacı (Q-4) | yüksek hacimli, kullanıcı değiştirmez |
| Dalga üyeliği | — | kod yok | — | sipariş numarası (`OUT`) gösterilir |
| İstisna | — | kod yok | — | dalga kodu + sipariş + tür ile anılır |

Biçim `{ŞİRKET}-WAV-{000001}`, sayaç (kiracı, şirket öneki, tür) başına, sıfırlanmaz, ASCII; kiracı genelinde tekil (RCS-001 §6). `WAV`'ın RCS-001
§3 tablosuna yazılması korunan yol değişikliğidir (kapı §6, + X-12 aynı adımda) — **yazıldı: commit `0aa802519`, sahip onayı Beste 2026-11-12**
(`.antigravity/rules/record-coding-standard.md:72`).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | WMS-WAVE-BUNDLE sözleşme taslağı + dalga çekirdeği: yarat / düzenle / iptal, sipariş ekle / çıkar, öner, sınır uyarıları, kesim saat dilimi, depo ayarı, izin, kiracı, denetim, kayıt kodu (arka uç) | Owned Objects, Entity Fields, Indexes, Repo Scope, Protected Paths, Dependencies, Servis ve kapsam, Dalga kurma ve uygunluk, Durum makinesi, Kesim saati ve saat dilimi, Sınırlar ve uyarılar, İyimser kilit, Merkezi denetim ve yerel iz, Kayıt kodu üretimi, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events, Record Codes | E4 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |
| DL-2 | serbest bırakma + rota optimizasyonu + MOD-0178 seam'i (görev erteleme, port, kurtarma) + dalga yönetimini kapatma (bekleyenlerin dalgasız görev açılışı, bakım turu — r3) | Serbest bırakma ve MOD-0178 seam'i, Rota optimizasyonu, Dalga yönetimini kapatma, Durum makinesi, Hata kodları, Failure Path to Verify, Audited Events | L3 + E5 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |
| DL-3 | ilerleme + durum uzlaştırması + istisnalar + riskli dalga + geri çekme | İlerleme ve durum uzlaştırması, Geri çekme ve iptal, İstisnalar, Riskli dalga, Hata kodları, Audited Events | E4 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |
| DL-4 | ekranlar: pano + ayrıntı, planlayıcı (sürükle-bırak + klavye), istisna kuyruğu; durumlar; 375 px; RTL; 7 dil; yardım | Layout & Shell Contract, Frontend File Contract, Ekranlar ve Blueprint soft pages, Page Help (UXH-001) | E4 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |
| DL-5 | performans sayfası + CSV | Performans sayfası ve CSV, Frontend File Contract, Audited Events | E4 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |
| DL-6 | yalnız Gateway kapsama testi (integration-agent; DL-5 kabulünden sonra — r9, AC-55 eşitliği; `ocelot.json` değişmez) | Gateway / API Routing Decision | E3 | yok (plan `:78` yetenek kimliği vermez) | MVP-5 |

## 24. Dilim kapıları ve bağlı kararlar

Paket onaylanmadan (künye `approved` / `ready-for-dev` + CT-APR-02 inceleme kaydı + CT-APR-01 onay satırı, onaylayan Ali — D-17) hiçbir dilim
başlamaz. **Onaydan önce kapanan kapı (Y-4):** `.antigravity/**` satırları (PKS-001 §4 tablo satırı + :92 adlı alt ad alanı istisna cümlesi,
RCS-001 §3) + aynı adımda X-12 — **yazıldı, commit `0aa802519`, sahip onayı Beste 2026-11-12** — ve X-16 — **yazıldı — MOD-0178 r29 `adfe1bd7d`, inceleme UYGUN; Ali'nin onay turu (r29 + bu paket) açık** (r5; §6). **X-16'nın
sonucu (F-5):** MOD-0178 r29 — bağımsız inceleme (CT-APR-02, **UYGUN**, `docs/records/reviews/MOD-0178/2026-11-13-2e4036b6.md`) + Ali onayı
(**açık**); sahip kararıyla MOD-0178 r28 ile **aynı onay turunda**; bu paketin onay satırından önce (§6 madde (4)).
Sahip kararları (karar 03) bütün dilimler için kapalıdır; aşağıdaki "kararlar" sütunu yalnız açık kalanları ve CT adımlarını sayar.

| Dilim | Başlamadan kapanması gereken kararlar | Seam'ler (XMC-001) | AC | Sabotajlar |
|---|---|---|---|---|
| DL-1 | Ali'nin onay turu — MOD-0178 r29 + bu paket (onaydan önce; `.antigravity` satırları ve X-12 `0aa802519`'da, X-16 MOD-0178 r29 `adfe1bd7d`'de yazıldı, r29 incelemesi UYGUN); X-16 bekçisi (AC-5 alt ad alanı vakası, S-5c, S-5d) — sahip onayladı 2026-11-13; ilk adım: WMS-WAVE-BUNDLE taslağı; X-15 sahip teyidi (satır adları) | X-6, X-9, X-10, X-11, X-14 (r8 — `Mod0178WarehouseRouteTests.cs:97`, integration-agent), X-15, X-17 (r10 — `InventoryLedgerManifestCompletenessTests` dışlama satırı) | AC-1…AC-17 (AC-15 yalnız a…e; AC-5 alt ad alanı vakası ve INVENTORY-LEDGER bütünlük vakası — r10 — dahil), AC-55 0178 sayı vakası (r8), AC-56 | **adıyla (r4, F-7):** S-1, S-2, S-3a, S-3b, S-4, S-5a, S-5b, S-5c (r5; r6 yeniden yazıldı; r7 beklenen ek kırmızılar adıyla), S-5d (r6), S-5e (r10), S-6a, S-6b, S-7, S-8a, S-8b, S-9, S-10, S-11, S-12, S-12b, S-13, S-14, **S-15, S-15c** (AC-15'in DL-1 sabotajları yalnız bu ikisidir), S-16a, S-16b, S-17, S-55c (r8), S-56 |
| DL-2 | **CT adımı: WMS-BUNDLE eklemeli sürümü (X-8; X-8 üretim karşılığı DTO'ları ve `listPickTasks` `orderBy` / `waveId` dahil) DL-2 başlamadan önce yazılır ve dondurulur; sürüm numarası CT-1** | X-1, X-2, X-3, X-4 (`IssueImmediateAsync` + `NotDeferred` dahil), X-5, X-8, X-10 (`ReleasePendingOrdersCommand` +1), X-11, X-14 (r8) | AC-18…AC-28 (AC-27 d dahil); AC-15 f…q (kapatma, §8.19); AC-12 bakım servisi listesi; AC-55 0178 sayı vakası (r8) | **adıyla (r4, F-7):** S-18, S-19, S-19b, S-20, S-21a, S-21b, S-22, S-23, S-24, S-25, S-26, S-27, S-28; AC-15: S-15b, S-15d, S-15e, S-15f, S-15g, S-15h, S-15i, S-15j, S-15k, S-15l, S-15m, S-15n, S-15o, S-15p, S-15q, S-15r, S-15s, S-15t, S-15u, S-15v; **r5:** S-15w, S-15x, S-15y, S-15z, S-15aa, S-15ab, S-15ac, S-15ad, S-15ae, S-15af, S-15ag, S-15ah, S-15ai, S-15aj, S-15ak, S-15al; **r6:** S-15am, S-15an, S-15ao, S-15ap, S-15aq, S-15ar; **r7:** S-15as, S-15at, S-15au, S-15av, S-15aw, S-15ax, S-15ay, S-15az, S-15ba, S-15bc; **r8:** S-15bd, S-55c |
| DL-3 | X-15 sahip teyidi (`recall` fiili) | X-9, X-10, X-14 (r8), X-15 | AC-29…AC-38; AC-15 (r) (r7 — iki döngü ayrımı, uzlaştırma DL-3'te); AC-55 0178 sayı vakası (r8); AC-56 tekrar | S-29…S-38 (S-30b, S-30c dahil), **S-15bb** (r7), S-55c (r8), S-56 |
| DL-4 | AuthService rol şablonu WP'si (Q-20 Ali, X-13) kabul kapısı; X-7 üyeleri X-8 sürümünde değilse DL-4 öncesi CT adımı | X-7, X-8, `PageHelpCatalog.cs` (§5, eklemeli) | AC-39…AC-50 | S-39 (`Waves/Index.cshtml`), S-41, S-42, S-45, S-48, S-49, S-50, S-50b, S-50c |
| DL-5 | — | X-10 (KPI komutu yok — sayı değişmez), X-14 (r8), X-15 (satır adı), `PageHelpCatalog.cs` (`WAVEKPIS`) | AC-51…AC-54; AC-55 0178 sayı vakası (r8); AC-56 tekrar | S-51, S-52, S-53a, S-53b, S-54, S-54b, S-55c (r8), S-56 |
| DL-6 | DL-5 kabulü (r9, r8 incelemesi F-1 — AC-55 klasör / taslak eşitliği bütün 0180 uçlarını ister) | gateway test projesi (integration-agent); `ocelot.json` değişmez, sabotajda geçici bozulur ve geri alınır | AC-55 (kapsama testi) | S-55a, S-55b |

Sıra: DL-1 → DL-2 → DL-3 → DL-4 → DL-5 → DL-6; DL-4 geliştirmesi DL-2'den sonra DL-3 ile paralel yürüyebilir ama **DL-4 kabulü DL-3 kabulünden
sonradır** (istisna kuyruğu ve ilerleme ekranları DL-3 uçlarına dayanır); **DL-6 DL-5 kabulünden sonra** başlar (r9, r8 incelemesi F-1:
AC-55'in klasör / WMS-WAVE-BUNDLE taslak eşitliği — taslak DL-1'de 23 işlemle yazılır — ancak bütün 0180 uçları teslim edilince sağlanır). MOD-0178'in servis bekçi testleri
(`INVT/Warehouse/**`) her dilimde değişmeden yeşil (AC-18'in `git diff` kanıtı her dilim kabulünde tekrarlanır). **İki istisna (r10 — X-14 ve X-17; "değişmeden yeşil" sözünün dışında kalan, var olan bir bekçinin sabitini ya da
dışlama listesini güncelleyen başka modül testleri; X-9, X-10 eklemeli MOD-0173 bildirimleri §5.1'de ayrıca sayılır):** **(1) X-14 (r8, r7
incelemesi F-1):** Gateway'deki `Mod0178WarehouseRouteTests.cs:97` uç sayısı uç ekleyen her dilimde (DL-1, DL-2, DL-3, DL-5) dilim
başında ölçülen sayı + dilimin 0180 uçlarına, uçlarla aynı commit'te güncellenir (integration-agent; sahip onayı `c7dc4ea6a` + kapsam teyidi `6235794ab`, r9 — ayrı sahip onayı yok);
dilim kabul kaydı önce → sonra değerini yazar ve `Assert.Equal(<n>, endpoints.Count)` satırına atıf yapar (satır numarasına değil); dilim
kabulünde Gateway test projesi koşar, bu yüzden 0178 testinde kırmızı pencere kalmaz (AC-55 0178 sayı vakası, S-55c). **(2) X-17 (r10, sahip
onayı `ad1100158`):** MOD-0173'ün `InventoryLedgerManifestCompletenessTests.cs` dosyasına yalnız DL-1'de, ilk 0180 controller'ıyla aynı commit'te
`Every_enforced_permission_is_reachable_from_the_manifest` dışlama listesine `.Controllers.WavePlanning` satırı + yorumu eklenir (MOD-0181 emsali
`446cb58db`); başka satır değişmez, sonraki dilimlerde dosyaya dokunulmaz; DL-1 kabulünde InventoryService paketi bütünüyle koşar, kırmızı
pencere kalmaz (AC-5 INVENTORY-LEDGER bütünlük vakası, S-5e).

## 25. Page Help (UXH-001)
- `WAVES` → `Help.WAVES.Purpose`, `.Audience`, `.Step1Title`/`.Step1` … `.Step4` (1: depoyu seç, 2: dalga oluştur — kesim saati depo saatiyle,
  3: siparişleri ekle ya da öner, 4: serbest bırak ve ilerlemeyi izle); `.Immediate` ("Serbest bırakılan dalga iptal edilmez; kalan görevler
  gerekçeyle geri çekilir"); `Related.WAVEPLANNER`, `Related.WAVEEXCEPTIONS`.
- `WAVEDETAIL` → `Purpose`, `Audience`, `Step1..3` (ilerlemeyi oku, görevleri ve önerilen yolu incele, olay geçmişine bak); "İlerleme Toplama
  Görevleri'nden okunur" notu.
- `WAVEPLANNER` → `Purpose`, `Audience`, `Step1..3` (soldan siparişi seç, "Ekle" ya da sürükle-bırak — klavyeyle Enter, sınır uyarılarını kontrol
  et); "Gri siparişlerin rezervasyonu bitmedi" notu.
- `WAVEEXCEPTIONS` → `Purpose`, `Audience`, `Step1..3` (istisnayı incele, çıkar / sonraki dalgaya taşı / yok say — gerekçe zorunlu, stok düzeltmesi
  için bağlantıyı kullan); `.Immediate` ("Siparişi çıkarmak açık görevlerini iptal eder").
- `WAVEKPIS` (DL-5) → `Purpose`, `Audience`, `Step1..2` (depo ve tarih aralığını seç — en çok 92 gün, CSV indir).
- Kiracı modülü → 7 dil (D-15). **Tur yok.**
