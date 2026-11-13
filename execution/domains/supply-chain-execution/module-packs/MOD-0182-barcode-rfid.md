---
id: MOD-0182
name: Barcode/RFID
domain: supply-chain-execution
service: Diten.InventoryService
shell: tenant
golden_reference: slim
data_mode: server
screens:
  - module: ScanEvents
    data_mode: server
  - module: ScanDevices
    data_mode: server
  - module: LabelTemplates
    data_mode: server
  - module: LabelPrints
    data_mode: server
entity_base: EntityBase
status: draft
approval_status: pending
revision: "r4 (2026-11-13) — bağımsız inceleme r3 (`docs/records/reviews/MOD-0182/2026-11-13-11585181.md`, KOŞULLU, F-1…F-3 orta, F-4…F-9 düşük) ve kararlar 03'ün iki son eki (XMC-001 onayları X-15 / X-12 / X-1 `ad1100158`; r3 incelemesi sonrası X-6 genişletmesi + QR ile anahtar teslimi `4168e7a90`) işlendi; F-1…F-9 kapandı (kapanış tablosu §19.7). Başlıca değişiklikler: X-6'ya `CentralAuditDeclarationTests.cs` işlem çağrı yeri sayısı (`Assert.Equal(<n>, seen)`, bugün `:130`, yorum `:127-129`) — dilim başında ölçülen sayı + dilimin yeni `WithTransactionAsync(` çağrı yerleri, aynı commit, yorumda dilim adı; öneri tek ortak işlem yardımcısı (beklenen DL-1 +1, sonra 0); AC-23'e vaka + S-23b / S-23c (F-1); cihaz anahtarı Web'den kayıt ve yeniden etkinleştirmede anahtar yerine tek kullanımlık, 10 dakikalık eşleştirme kodu + QR; telefon `POST /barcode/devices/pair` ile (çalışanın JWT'si + `devices.create` + kod + aynı `InstallationId`) anahtarı bir kez alır; kod ve anahtar sunucuda yalnız özet; yeni komut `PairScanDeviceCommand` → yazma komutu **17** (DL-1 10), DL-1 servis ucu **16** (toplam 33); ZXing.Net DL-1'e çekildi (QR çizimi); AC-3'e vaka + S-3b / S-3c / S-3d (F-2); PIN taşıyan gövdelerde idempotency parmak izi PIN'i dışlar, idempotency deposunda PIN ya da türevi yok; AC-19'a vaka + S-19c / S-19d (F-3); komut idempotency deposu `wms_barcode_command_requests` (§3, §4.10, §4.11, X-5) ve replay araması If-Match / durum / tekillik denetimlerinden önce; AC-9'a S-9e / S-9f (F-4); aşım denetim sırası 404 → 422 aşılamaz → 422 öz-aşım → 409 → 422 PIN yok → 423 kilit → PIN; yanlış PIN ve kilit yerel izi `wms_barcode_override_attempts`, ScanEvents ve Device Health'te görünür; AC-18'e S-18h / S-18i (F-5); ilçe FU01 DL-2'den önce basılmaz, DL-5 beklemez, ilçe vakası ilçe okumasını ekleyen dilimde (S-56c); posta kodu ifadesi FU01 kararları 01 son ekiyle hizalandı (`cb612efc9`) (F-6); (00)+(02)+(37)+(10) lojistik etiket Product adımında `Rejected` `PRODUCT_NOT_VERIFIABLE`, X-1'in `:40` kuralına etkisi açık; AC-58'e S-58c (F-7); şirket başına cihaz kaydı / anahtar / Web onayı kuralı §8.2'de, SCAN-BUNDLE ve mobil sözleşme notu CT işi olarak §18'de (F-8); X-1 / X-6 / X-12 / X-15 onay atıfları, §8.12 nabız atfı `CentralAuditRecorder.cs:160-162`, §5.1 ölçüm HEAD `db70c9f37` (F-9). Çağıran CT'nin ek kararları (mobil ekip soruları 2026-11-13, inceleme dışı): önerisi olmayan yerleştirme görevinde 0178 hedef kurallarından geçen bin `Accepted`, geçmeyen aşılamaz `Rejected`, `WRONG_LOCATION` `Warning` yalnız öneri varken (AC-11, S-11c); 423 gövdesi `details.lockedUntil`, kalan hak yok (AC-18, S-18j); SCAN-BUNDLE'da `DIFFERENT_LOT` = `Rejected` aşılamaz, PIN aşımı sonrası 0178 `deviationReason`'ı sunucu yazar, bağ `scanRef = scan-session:{sessionId}` (kapı bu önekte depo ayarından bağımsız da çalışır) (§8.7, §8.16, AC-41). AC numaraları değişmedi (AC-1…AC-65). Kimlik ön kontrolü yeniden koşulmadı (kimlik ve ad değişmedi). r3 (2026-11-13) — bağımsız inceleme r2 (`docs/records/reviews/MOD-0182/2026-11-13-7ce2f1cb.md`, KOŞULLU, F-1…F-5 orta, F-6…F-12 düşük) ve kararlar 03'ün iki eki (Q-3 genişletme teyidi `7f584f546`; Q-6 = ZXing.Net `bea6662d9`) işlendi; F-1…F-12 kapandı (kapanış tablosu §19.6). Başlıca değişiklikler: X-12 'değişmez' satırından XMC-001 satırına çevrildi — `Mod0178WarehouseRouteTests.cs` `Assert.Equal(<n>, endpoints.Count)` uç ekleyen her dilimde (DL-1 15, DL-4 11, DL-6 2, DL-7 4; DL-5 0) dilim başında ölçülen sayı + dilimin uçlarıyla, aynı commit'te, integration-agent; sahip onayı bekliyordu — r4: onaylandı `ad1100158` (F-1); yeni X-15 `InventoryLedgerManifestCompletenessTests.cs` `Controllers.Barcode` dışlaması (DL-1, MOD-0173; sahip onayı bekliyordu — r4: onaylandı `ad1100158`) ve karşılığında 0182'nin kendi manifest bütünlük vakası (F-2); aşım ucu `warehouse.barcode.scans.create` ister, olay çağıranın olmalı (F-3); cihaz anahtarı replay'de dönmez, `deviceKeyNotice = BARCODE_DEVICE_KEY_NOT_REPLAYED`, replay anahtar üretmez (F-4); açık kör sayımın bin'i bir toplama ya da iz adımına katılıyorsa o toplamın / adımın miktarı gizlenir (F-5); yazma komutu 18 değil **16** (F-6); §8.4 Outcome değerleri + S-11b (F-7); mobil not / brief CT tarafından düzeltildi `bf7bd7852` (F-8); Q-6 = ZXing.Net kapandı, Q-3 teyidi işlendi (F-9); nabız ve PIN sayacı If-Match muafiyeti + atomik yazım (F-10); başka kullanıcının oturumuna olay reddedilir, replay araması cihaz durumundan önce (F-11); X-1'e kapalı listeli AI toleransı (37, 310n, 240) (F-12); Q-11 güncellendi — SSCC etiketinde alıcıdan şehir + ilçe, posta kodu yalnız sevkiyat kopyasında varsa (MOD-0178-FU01 kararları 01 eki Q-10, `a7c1d32b8`; AC-56, S-56b). AC numaraları değişmedi (AC-1…AC-65); yeni sabotajlar S-2c, S-6d, S-9b, S-9c, S-9d, S-11b, S-16b, S-18d, S-18e, S-18f, S-18g, S-20b, S-56b, S-58b, S-62b, S-65c. Kimlik ön kontrolü yeniden koşulmadı (kimlik ve ad değişmedi). r2 (2026-11-13) — sahip kararları 03 (`docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-03.md`; Beste 2026-11-13 sohbet cevabı, 'hepsi öneri gibi') işlendi: Q-1, Q-2, Q-3, Q-4, Q-5, Q-7, Q-8, Q-9, Q-10, Q-11, Q-12, Q-15, Q-17 kapandı (kapanış tablosu §19.5). Davranış daralması yalnız Q-3: S-7'nin PIN aşımı toplamada geçmez — v1'de aşılabilir tek durum yerleştirmedeki `WRONG_LOCATION`; `DIFFERENT_LOT` hiçbir bağlamda aşılamaz (§2.1, §4.2, §8.4, §8.5, §13, AC-12, §17). Q-7'nin donanım seçimi açık (TBD, Beste — DL-6 kapısı). Q-2'ye bağlı CT işi: mobil not `:106` düzeltilir (paket dokunmaz). `.antigravity` kapısı (Q-1 = A satırları) onaydan önce açık. AC 65, yazma komutu 18 (r3'te 16 olarak düzeltildi — F-6), sabotaj adları — değişmedi. Kimlik ön kontrolü yeniden koşulmadı (kimlik ve ad değişmedi). r1 (2026-11-13) — ilk taslak. Girdiler: sahip kararları 01 (S-2, S-3, S-5, S-6) ve 02 (S-7…S-12, E-1…E-7; mockup v2 kabul), Web ve mobil mockup brief'leri, mobil ekip notu (§3A dondurulmuş uçlar, §3B menü ve Görev Merkezi), Görev Merkezi analizi ve sahip kararı (2026-11-13: Görev Merkezi bağlantısı MVP-5 sonrası), MOD-0290-FU07 (GTIN) paketi, MOD-0178 / MOD-0180 / MOD-0181 paketleri. Kimlik ön kontrolü bu oturumda yeniden koşuldu (§0). Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01) yok."
status_note: "draft r4 — kod izni yok. r3 bağımsız incelemesi (11585181, KOŞULLU) F-1…F-9 r4'te kapandı (§19.7). XMC-001 sahip onayları kayıtlı: X-15, X-12 (her artış dahil), X-1 (AI toleransı + gs1-check yan etkisi) — ad1100158; X-6 genişletmesi (CentralAuditDeclarationTests işlem çağrı yeri sayısı) ve QR ile anahtar teslimi — 4168e7a90. r4 ile yazma komutu 17 (PairScanDeviceCommand), DL-1 servis ucu 16. r4 için kısa bağımsız yeniden inceleme (CT-APR-02) önerilir. r2 bağımsız incelemesi (7ce2f1cb, KOŞULLU) F-1…F-12 r3'te kapandı (§19.6); Q-6 = ZXing.Net (karar 03 eki bea6662d9, Ali'ye gitmez) ve Q-3 genişletme teyidi (7f584f546) işlendi; Q-11 (şehir + ilçe, posta kodu varsa — MOD-0178-FU01 kararları 01 ekleri a7c1d32b8, cb612efc9); mobil not / brief düzeltmesi CT tarafından yapıldı (bf7bd7852). Onaylayan Ali (GitHub GMG-ERP); satırları yazan Beste'nin hesabı (yazan ≠ onaylayan). Sahip kararları 03 (Beste 2026-11-13, 'hepsi öneri gibi') ile Q-1, Q-2, Q-3, Q-4, Q-5, Q-7, Q-8, Q-9, Q-10, Q-11, Q-12, Q-15, Q-17 kapandı (§19.5). Onaydan önce kapanacak: .antigravity kapısı (Q-1 = A: PKS-001 §4 warehouse.barcode.* satırı + :92 / :105, RCS-001 §3 DVC / LBL, aynı adımda X-8 RecordCodeTypeCatalogTests ve X-9 MOD-0178 §14.1 notu — CT hazırlar, Beste onaylar; bu paket .antigravity'ye yazmaz); r4 bağımsız yeniden inceleme (CT-APR-02) ve onay satırı (CT-APR-01, Ali). Beste'ye kalan: RFID okuyucu donanım seçimi (Q-7'nin kalanı, TBD — DL-6 kapısı, onayı bekletmez); X-2…X-5, X-7…X-11, X-13, X-14 ve X-6'nın :53 / :60-69 kısmı için XMC-001 sahip onayları (dilim başlamadan). CT işi (r4, F-8): SCAN-BUNDLE taslağına (DL-1) ve mobil sözleşme notuna şirket başına cihaz kaydı / anahtar / Web onayı kuralı ve QR eşleştirme akışı. Ali'ye: Q-13 şirket bağlamı (legal_entities claim + X-Legal-Entity-Id), Q-14 rol şablonları. CT'ye: Q-16 okutma olayının merkezi denetim yolu. Başka ekip: MDM (MOD-0290-FU07 DL-1'in main tarihi — DL-5 kapısı), mobil ekipler (D-1…D-5 cevapları; SCAN-BUNDLE sözleşme notu paket onayından sonra). target: TBD — sahip tarihi yok, plan satırı verilmedi."
owner: supply-chain-execution / merkez (Beste) — MVP-5 WMS CT
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-13
target: TBD
form_field_count: 8
dependencies: [MOD-0178 (görev bağlamı okuması; DL-3 scanRef kapısı — XMC-001), MOD-0181 (sayım bağlamı okuması, kör sayım; DL-3 scanRef kapısı — XMC-001), MOD-0174 (GS1 ayrıştırıcı, lot ve seri kaydı), MOD-0175 (stok statüsü), MOD-0176 (son kullanma kuralı), MOD-0177 (geri çağırma kapısı), MOD-0173 / MOD-0173-FU02 (LOCATION, bin kodu, defter okuması), MOD-0290 (PRODUCT-MASTER, FG satış kutusu gtin'i), MOD-0290-FU07 (GTIN çözümleme + GS1 şirket önekleri — DL-5 kapısı; onaylı, kodu main'de yok), MOD-0365 (dış sağlayıcı — v1'de yalnız sınır; Q-5 = A ile yazdırma ajanı yok, RFID okuyucu ara yazılımı donanım seçimine bağlı — Q-7), MOD-0040-FU02 (RCS-001 ortak servis; ara üretici MOD-0178 geçici sayacı), MOD-0018 (RBAC), BL-501 (merkezi denetim), SCAN-BUNDLE (taslak yok — bu paketin sözleşmesi), WMS-BUNDLE 1.1.0, WMS-COUNT-BUNDLE 1.0.0, LOCATION, TRACE, DEV-0000]
---

# MOD-0182 — Barcode/RFID (Barkod / RFID)

> **draft (r4, 2026-11-13).** `status: draft`, `approval_status: pending`. **Paket onaylanmadan geliştirme başlamaz:** kod yalnız
> `approved` / `ready-for-dev` künye + bağımsız inceleme kaydı (CT-APR-02) + onay satırı (CT-APR-01, **onaylayan Ali / GitHub `GMG-ERP`**)
> ile yazılır (AGENTS.md Çekirdek madde 7). Bu sürüm kod, commit, push, PR ya da onay izni değildir.
>
> **r4 revizyon notu (2026-11-13).** Girdi: bağımsız inceleme r3 `docs/records/reviews/MOD-0182/2026-11-13-11585181.md` (KOŞULLU; 3 orta,
> 6 düşük; hedef r3 LF sha256 `11585181…`) ve kararlar 03'ün iki son eki: XMC-001 onayları (`ad1100158` — X-15, X-12 her artış dahil, X-1 AI
> toleransı + gs1-check yan etkisi) ve r3 incelemesi sonrası sahip kararları (`4168e7a90` — X-6 genişletmesi: `CentralAuditDeclarationTests`
> işlem sayısı satırı; cihaz anahtarının telefona QR ile teslimi). Her bulgunun kapanışı §19.7 "r3 → r4 kapanış tablosu"ndadır; seçimler CT
> önerisidir, sahibe yeni soru açılmadı. **Sayı değişiklikleri:** QR teslimi bir eşleştirme komutu ve ucu ekler — yazma komutu **17** (DL-1 10,
> DL-4 5, DL-6 1, DL-7 1), DL-1 servis ucu **16** (toplam 33; X-12 beklenen dağılımı 73 → 89 → 100 → 102 → 106, çıkarım). **Yeni koleksiyonlar:**
> komut idempotency deposu `wms_barcode_command_requests` (F-4) ve aşım denemesi yerel izi `wms_barcode_override_attempts` (F-5); ikisi de
> `BarcodeCollections.All`'da (X-5). **ZXing.Net** QR çizimi için DL-1'in ilk adımına çekildi (Q-6 kararı değişmedi). AC numaraları (AC-1…AC-65)
> değişmedi; yeni vakalar mevcut AC'lere eklendi, yeni sabotajlar (S-3b, S-3c, S-3d, S-9e, S-9f, S-11c, S-18h, S-18i, S-18j, S-19c, S-19d,
> S-23b, S-23c, S-56c, S-58c) §16 ile §24'te iki yönde adlı. **Çağıran CT'nin ek kararları (mobil ekip soruları 2026-11-13, inceleme dışı;
> §19.7 "CT ek 1–3"):** önerisi olmayan yerleştirme görevi (§8.4, AC-11); 423 `details.lockedUntil`, kalan hak yok (§8.15, AC-18); SCAN-BUNDLE
> açıklamaları — `DIFFERENT_LOT`, sunucunun yazdığı `deviationReason`, `scanRef = scan-session:{sessionId}` bağı (§8.7, §8.16).
>
> **r3 revizyon notu (2026-11-13).** Girdi: bağımsız inceleme r2 `docs/records/reviews/MOD-0182/2026-11-13-7ce2f1cb.md` (KOŞULLU; 5 orta,
> 7 düşük; hedef r2 LF sha256 `7ce2f1cb…`) ve kararlar 03'ün iki eki: Q-3 genişletme teyidi (`7f584f546` — farklı lot hiçbir bağlamda, yerleştirmede
> de, PIN'le aşılamaz) ve Q-6 = ZXing.Net (`bea6662d9` — Ali'ye gitmez). Her bulgunun kapanışı §19.6 "r2 → r3 kapanış tablosu"ndadır. Seçimler CT
> önerisidir; sahibe yeni soru açılmadı. **Sahip onayı bekleyen XMC-001 satırları:** X-12 (0178 Gateway testinin uç sayısı, uç ekleyen her dilim —
> F-1) ve X-15 (yeni; MOD-0173 manifest bütünlük testinde `Controllers.Barcode` dışlaması — F-2); X-1'in kapsamı genişledi (kapalı listeli AI
> toleransı — F-12), MOD-0174 sahibinin X-1 onayı bunu da kapsamalıdır (r4: üçü de onaylandı — `ad1100158`). **Düzeltilen sayı:** yazma komutu **16**'dır (DL-1 9, DL-4 5, DL-6 1, DL-7 1);
> r1 / r2'nin "18" yazımı yanlıştı (F-6; r4'te eşleştirme komutuyla 17). **Q-11 güncellemesi (sahip kararı, inceleme dışı):** posta kodu CRM'den boş gelebilir; SSCC etiketinde
> alıcıdan **şehir + ilçe** yazılır, posta kodu yalnız sevkiyat kopyasında varsa (MOD-0178-FU01 kararları 01 eki Q-10, `a7c1d32b8`; §8.9, AC-56,
> S-56b). AC numaraları (AC-1…AC-65) değişmedi; yeni vakalar mevcut AC'lere eklendi, yeni sabotajlar §16 ile §24'te
> iki yönde adlı.
>
> **r2 revizyon notu (2026-11-13).** Girdi: sahip kararları 03 `docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-03.md` (Beste
> 2026-11-13 sohbet cevabı, "hepsi öneri gibi"; hedef r1 commit `dbbb8dd04`, LF sha256 `26b4b4a2…`). Q-1, Q-2, Q-3, Q-4, Q-5, Q-7, Q-8, Q-9,
> Q-10, Q-11, Q-12, Q-15, Q-17 r1'deki önerileriyle kapandı; her kararın değişikliği ve bölümü §19.5 "r1 → r2 kapanış tablosu"ndadır.
> **Tek davranış daralması Q-3'tür:** kararlar 02'nin S-7'si "farklı lot ve yanlış raf PIN'le aşılabilir" diyordu; Q-3 = A bunu toplama için
> kapatır — v1'de PIN aşımı yalnız **yerleştirmedeki `WRONG_LOCATION`** için geçerlidir, toplamada `WRONG_LOCATION` ve `DIFFERENT_LOT` geçerli
> PIN'le bile aşılamaz (422 `BARCODE_OVERRIDE_NOT_ALLOWED`), `DIFFERENT_LOT` hiçbir bağlamda aşılamaz; lot / bin değişimi FU-0178-LOTSWAP
> (§2.1, §4.2, §8.4, §8.5, §13, AC-12, §17). Diğer kararlar r1'in yazdığı varsayılanları teyit eder (metinde "öneri" → "karar 03"). **Açık
> kalanlar:** Ali'ye Q-6, Q-13, Q-14; CT'ye Q-16; Q-7'nin donanım seçimi (TBD, Beste — DL-6 kapısı); Q-1'e bağlı `.antigravity` kapısı **paket
> onayından önce** (§6; bu paket `.antigravity/**`'a yazmaz); Q-2'ye bağlı CT işi: mobil not `:106` düzeltilir (bu paket mobil nota dokunmaz).
> AC numaraları (AC-1…AC-65), yazma komutu sayısı (18 — r3'te 16 olarak düzeltildi, F-6) ve sabotaj adları **değişmedi**.
>
> **Kısa özet.** MOD-0182, depoda okutmayı (barkod, GS1 DataMatrix karekod, RFID) **sunucuda doğrulanan, değişmez bir okutma olayı**na
> çevirir; okutmayı MOD-0178 / MOD-0181 işlemlerine opak `scanRef` kapısından bağlar; cihazları kaydeder ve sağlığını izler; lokasyon,
> lot / koli ve SSCC etiketlerini şablondan **önizlemeyle** basar; RFID ile raf taramasında beklenen / okunan farkını gösterir (stok
> değişmez); lot / seri / SSCC'nin depo yolculuğunu kanıt paketiyle verir. Okutma ekranları **mobil uygulamadadır** (mobil ekipler, E-3);
> bu paket yalnız sözleşmeyi (SCAN-BUNDLE) ve Web yönetim ekranlarını yazar.

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed) — bu oturumda koşuldu (HEAD `90f6a1da8`, araç sha256 `ae87a56615f5…`):**

| Komut | Sonuç |
|---|---|
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --check-id MOD-0182 --name "Barcode/RFID"` | çıkış 0 — `OK  MOD-0182: proven against Blueprint/registry.` |
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "Barcode RFID"` | çıkış 3 ("inceleme gerekli"); `ÖZET bulgular: blueprint 1 · sor 0 · registry 1 · paket 1 · gateway 0 · görev 1`; 606 satır, rapor sha256 `b5e9eec60d1f20eddd52b7733b51a88ef8224e5a95c53562bfd8cd5c10832bd6` |

CT ön kontrolü (2026-11-13) aynı sonucu vermişti: registry satırı reserved / planned, domain supply-chain-execution, DCP-002 kapısı OK 2026-09-11.

**Keşif bulgularının her birine karar:**

| Bulgu | Karar |
|---|---|
| Blueprint 1 — `MOD-0182 'Barcode/RFID'` | **Bu paketin kaynağı.** Blueprint 8.8 `Blueprint_Data` satır 183 (aşağıda) |
| Registry 1 — `MOD-0182` reserved / planned | **Bu paketin kaynağı.** `execution/registries/module-id-registry.md:320` |
| Paket 1 — `execution/domains/master-data-management/module-packs/MDM-061-barcode-rfid.md` (8 eski uzak dal: `origin/feature/dev-enablement`, `origin/feature/module-catalog`, …; blob `7ceafdb5a`) | **Mükerrer keşfi — eski takma ad / otomatik iskelet; devralınmadı.** Yalnız eski uzak dallarda, 2026-04-24 "Initial commit"; künye `status draft`, `owner ai-agent`, yanlış domain (master-data-management); 82 satırlık şablon iskeleti, kapsamı boş; main'de yok. CT kararı (2026-11-13): MOD-0182 bu paketle supply-chain-execution'da yazılır. Aynı karar MOD-0178 (`MDM-057`), MOD-0180 (`MDM-059`) ve MOD-0181 (`MDM-060`) için verildi |
| Görev 1 — `execution/domains/supply-chain-execution/work-packs/mvp5-wms/CT-QUEUE.tsv:19` `WMS-MVP5-MOD0182` | **Bu paketin iş satırı**; çakışma değil |
| Gateway 0 | Karar §15 — mevcut `/api/warehouse/{everything}` ailesi |

**MOD-0365 (bağımlılık, bu pakette KAPSAM DIŞI):** `execution/registries/module-id-registry.md:201` — `Device / Edge Integration (Scanner & RFID
Middleware) [External Provider]`, domain platform-shared-services, "needed by MOD-0182". Sahibi merkez (S-5). Bu paket MOD-0365'in hiçbir nesnesini
yazmaz; sınır §2.2'de, bağımlılık §7'de.

**Blueprint 8.8 satır 183 (bu oturumda xlsx'ten okundu):** Module ID `MOD-0182` · Suite `Supply Chain Execution Suite` · Capability Group
`Warehouse (WMS)` · Module Name `Barcode/RFID` · Wave `W-4` · Dependency Gate `Putaway / Picking / Packing; Device integration` · Outcome
`Scan-driven traceability` · Soft Pages `Scan Event Monitor; Compliance Trace; Device Health` · Placement `Edge + Domain App (WMS)` · Min
Integration Contract `SCAN-BUNDLE (scan event schema, device identity, offline buffer/replay contract)` · SoR `scan events (if stored), device
registrations (if included)` · SoR Primary Object `scan events` · Build/Buy `Buy/Partner` · SLO `Tier 2` · AI Tier `Assist` · AI Use Cases
`Summarize; Extract; Recommend; Route` · AI Risk `Medium`. **MOD-0365 satır 364:** Placement `External Provider`, Build/Buy `Partner`, SoR
`Device scan / read events`, Soft Pages `External System Profile; Integration/Contract Profile`.

**Blueprint sapmaları (adıyla):**
- **W-0182-01 — Build/Buy.** Blueprint `Buy/Partner` der. **Karar: Build** (Q-15, sahip — karar 03, 2026-11-13). Gerekçe: okutma doğrulaması MOD-0178 görevine,
  MOD-0181 sayımına, MOD-0174 lotuna ve MOD-0177 geri çağırma kapısına süreç içi bağlıdır; 2026-11-06 kararı "v1 sağlayıcıdan bağımsız".
  Emsal: MOD-0178, MOD-0180, MOD-0181 Blueprint'te `Buy/Partner`, sahip kararıyla Build. Blueprint düzeltmesi EA'ya açık iş (FU-0182-EA).
- **W-0182-02 — çevrimdışı tampon / yeniden oynatma.** Blueprint SCAN-BUNDLE "offline buffer/replay" içerir. **Sahip kararı S-1 (2026-11-06,
  kararlar 01 `:5`): v1 yalnız çevrimiçi**, çevrimdışı kuyruk FU (FU-0182-OFFLINE). Sözleşme bu yüzden idempotency anahtarı ve istemci
  zamanını (`capturedAt`) şimdiden taşır; yeniden oynatma sonradan eklemeli gelir (§8.6).

**Diğer kimlik kaynakları:** `execution/domains/supply-chain-execution/domain-config.md:27` (WMS listesi `MOD-0182 Barcode/RFID`);
`execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md:16` (`canonical_modules` MOD-0182'yi içerir); plan
`docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md:78` ("MOD-0182 Barkod / RFID (W-4, MOD-0365'e bağlı)") ve `:124`
("MOD-0182 barkod (en son)").

## 1. Module Summary

MOD-0182 **depoda okutmanın** sahibidir. Bir okutma (el terminali tarayıcısı, telefon kamerası, RFID okuyucu ya da gerekçeli elle giriş)
sunucuya gelir; sunucu ham değeri **değiştirmeden** saklar, GS1 alanlarını (GTIN, lot, son kullanma, seri, SSCC) çözer, bağlamdaki işin
beklediğiyle (doğru raf, doğru ürün, doğru lot, son kullanma, geri çağırma, seri tekrarı) karşılaştırır ve sonucu **Kabul / Uyarı / Ret +
neden** olarak döner. Her okutma değişmez bir **okutma olayı** (Blueprint SoR "scan events") olarak kalır; görev onayına `scanRef` ile bağlanır.

Blueprint SoR nesneleri: **scan events** (okutma olayı) ve **device registrations** (cihaz kaydı — S-3: telefon kameraları da cihaz olarak
kaydedilir). Ek nesneler: sorumlu aşımı ve PIN'i, şirket ayarı, etiket şablonu ve basım işi, SSCC ataması, RFID taraması, uyum izi dışa
aktarımı (§3).

**Temel sınırlar:**
- **Stok yazılmaz.** MOD-0182 bakiye, hareket, rezervasyon, görev, sayım ya da koli yazmaz; RFID farkı bile stoğu değiştirmez (S-8). Stok
  gerçeği MOD-0173'tedir.
- **Bütün doğrulama sunucudadır** (mobil not §4): istemci yalnız ham değeri, bağlamı ve cihaz kimliğini gönderir.
- **v1 yalnız çevrimiçi** (S-1): bağlantı yoksa okutma yapılmaz.
- **Okutma ekranları mobil uygulamadadır** (E-3, E-4); Web'de yönetim ekranları: okutma olayları, cihaz sağlığı, ayarlar, sorumlu PIN'i,
  etiket şablonları, etiket basımı, uyum izi.
- **Görevlerin kaynağı** sahip kararıyla Görev Merkezi'dir (E-6) ama bağlantı **MVP-5 bittikten sonra** yapılır (sahip kararı 2026-11-13,
  `docs/records/decisions/2026-11/workcenter-integration-analysis-2026-11-13.md:6`); bu paket Görev Merkezi sağlayıcısı yazmaz.

Hedef kullanıcı: **depo çalışanı** (mobilde okutur, etiket basar), **vardiya sorumlusu** (PIN ile aşım verir, olayları izler), **kalite /
denetçi** (uyum izi, kanıt paketi), **BT / cihaz sorumlusu** (cihaz onayı, engelleme, sağlık), **etiket sorumlusu** (şablonlar). Kiracı
modülüdür (`_LayoutTenantShell`, 7 dil).

## 2. Ownership and Boundaries

### 2.1 Kapsam
- Okutma olayı (`ScanEvent`) ve sunucu doğrulaması; elle giriş (gerekçeli, S-10); vardiya sorumlusu PIN aşımı (`ScanOverride`, S-7 —
  toplamada Q-3 = A ile daraltıldı: v1'de yalnız yerleştirmedeki yanlış raf aşılır, §8.5); sorumlu PIN'i (`SupervisorPin`, Q-4).
- Cihaz kaydı ve yaşam döngüsü (`ScanDevice`: kayıt, onay, engelleme, yeniden etkinleştirme, nabız; sağlık görünümü — S-3, S-11; sahibi
  MOD-0182 — Q-2 = A, karar 03; yeni kayıt onay bekler — Q-8 = A).
- Şirket ayarı (`BarcodeSettings`: sessiz cihaz 24 saat, düşük pil %20 — S-11; PIN deneme sınırı; okutma zorunluluğu; SSCC öneki).
- WMS işlemlerine bağlama: MOD-0178 yerleştirme / toplama onayı ve MOD-0181 sayım girişi için `scanRef` kapısı (DL-3, `ScanSessionBinding`).
- Etiket şablonu ve basımı: lokasyon (raf), lot / koli (GTIN + lot + SKT), SSCC (koli / palet) etiketleri; basmadan önce önizleme (E-1);
  yeniden basım gerekçesi (S-10); barkod üretimi (`LabelTemplate`, `LabelPrintJob`, `SsccAssignment`).
- GTIN'den ürün çözümleme ve "Bu nedir?" sorgusu (DL-5, MOD-0290-FU07'ye bağlı).
- RFID raf taraması: beklenen / okunan farkı, yeniden tarama, sayım başlatma bağlantısı (S-8; `RfidSweep`).
- Uyum izi ve kanıt paketi (PDF / CSV) (`ScanTraceExport`).
- SCAN-BUNDLE sözleşmesi (taslak bu paketin DL-1 işi; bugün yok) ve mobil ekiplere sözleşme notu (paket onayından sonra, E-3).

### 2.2 Kapsam dışı
| Şey | Sahibi | 0182 nasıl dokunur |
|---|---|---|
| Mobil okutma ekranları, ses / titreşim ayarı, kamera, tarayıcı entegrasyonu | Mobil ekipler (iOS / Android) | yalnız SCAN-BUNDLE sözleşmesi ve sözleşme notu; ses / titreşim ayarı cihazda yereldir, sunucuya yazılmaz |
| Dış cihaz / RFID ara yazılımı sağlayıcı profili, sahada yazdırma ajanı | MOD-0365 (platform-shared-services, merkez — S-5) | yazmaz; Q-5 = A (karar 03) ile v1'de sahada yazdırma ajanı yok; seçilecek RFID okuyucu (Q-7, donanım TBD) bir ara yazılım gerektirirse ayrı paket |
| Stok bakiyesi, hareket, rezervasyon | MOD-0173 / FU03 | yalnız **okur** (`Find`); yazmaz |
| Görev, onay, koli, outbound | MOD-0178 | görev / koli okur; onaya yalnız DL-3 kapısıyla (XMC-001) dokunur |
| Sayım, sayım satırı, kör sayım | MOD-0181 | sayım / satır / bin talebi okur; DL-3 kapısı (XMC-001); RFID "Sayım başlat" **0181'in kendi ucunu** çağırır |
| Lot, seri kaydı, GS1 ayrıştırıcı | MOD-0174 | okur; ayrıştırıcıyı yeniden yazmaz (X-1 eklemeli SSCC üyesi) |
| Karantina / statü, son kullanma, geri çağırma kararı | MOD-0175 / MOD-0176 / MOD-0177 | karar vermez; kararı okur ve okutmayı reddeder |
| GTIN defteri, GS1 şirket öneki | MOD-0290-FU07 (MDM) | yalnız okuma sözleşmesi (`identifiers/{gtin}`, `gs1-company-prefixes`) |
| Paletleme / birleştirme (koli → palet içeriği), seri doğrulama | MOD-0246 Aggregation / Verification (Blueprint; registry'de yok) | yok; SSCC palet etiketi içeriksiz basılır (Q-10 = A, karar 03) |
| Türkiye İTS bildirimi | ayrı modül (S-6; MOD-0247 çıkarımı, araştırılacak) | yok; 0182 yalnız okutulan serileri tutar |
| Mal kabul akışı | MVP-6 / tedarik (mobil not soru 6) | v1'de mal kabul bağlamı yok; palet etiketi serbest SSCC basımıyla (Q-10 = A) |
| Görev Merkezi sağlayıcısı, mobil "benim menüm" ucu | merkez, MVP-5 sonrası (2026-11-13 kararı) / CT (E-5) | yok |
| Çevrimdışı okutma kuyruğu | — | v1 yok (S-1); FU-0182-OFFLINE |
| Kapı / portal RFID okuyucuları, RFID etikete yazma (kodlama) | — | v1 yok (Q-12: yalnız el okuyucu; Q-5 = A: RFID etikete yazma yok — karar 03) |
| AI Assist | Blueprint AI kapıları | v1 yok; FU-0182-AI |

### 2.3 Sınır kuralları
1. **İkinci bakiye yok.** 0182 varlıklarında `OnHand` / `Available` / `Reserved` adlı alan yoktur; RFID taramasının "beklenen" değeri tarama
   anında defterden okunur ve yalnız o taramanın kanıtıdır (MOD-0181 `ExpectedQuantity` deseni).
2. **Başka modülün koleksiyonuna yazılmaz.** 0182 üretim kodu MOD-0173 / 0174 / 0178 / 0181 depolarında `Insert*` / `Update*` / `Replace*` /
   `Delete*` / `BulkWrite` çağırmaz; okumalar 0182 Persistence'ta **yalnız `Find`**'dır. Tek istisna DL-3 kapısıdır: 0178 / 0181 onay işlemi
   **kendi işleminin içinde** 0182'nin portunu çağırır ve `ScanSessionBinding` satırı o işlemde yazılır (§8.7).
3. **Okutma sonucu HTTP hatası değildir.** Yanlış ürün, yanlış raf, SKT geçmiş … okutmanın **sonucudur**: olay yazılır, HTTP 201 döner, sonuç
   gövdededir. HTTP hata kodları yalnız geçersiz istek, cihaz kimliği, yetki, kapsam ve erişilemeyen bağımlılık içindir (§8.15).
4. **Kör sayım sızdırılmaz.** Açık kör sayımın bin'inde hiçbir 0182 cevabı (okutma, "Bu nedir?", RFID) beklenen miktarı ya da o bin'in stok
   miktarını vermez (AC-14, AC-16, AC-59). **r3 (F-5) — toplamlar ve iz:** "açık kör sayım bin'i" = MOD-0181 bin talebi açık + `Blind = true`
   (AC-16 tanımı). (a) Lot, SKU ya da GTIN düzeyindeki her **toplam** (ör. "Bu nedir?"in depo statü kovaları) hesabına açık kör sayım bin'inin bakiyesi
   katılıyorsa o toplamın miktarı **bütünüyle** gizlenir (`quantitiesHidden: true`, `hiddenReason: BLIND_COUNT_OPEN`) — bin'i dışlayıp kalanı
   göstermek **yapılmaz**, çünkü sayım öncesi toplamla fark kör bin'in miktarını verir. (b) DL-7 uyum izinde (`GET /barcode/trace` ve kanıt
   paketi) bin'i açık kör sayımda olan her defter hareketi adımı **gösterilir ama miktarı yazılmaz** (`quantity: null`, `quantityHidden: true`);
   kanıt paketi aynı kuralı dışa aktarma anında uygular (insert-only olduğu için o pakette miktar gizli kalır). (c) Kural sayım kapanana
   (bin talebi kapanana) kadar sürer; kullanıcıya göre ayrılmaz — miktarı görmesi gereken yetkili MOD-0173 / MOD-0181 ekranlarını kullanır.
   Okutma olayı listesi ve okutma cevabı miktar taşımaz; kural onları değiştirmez (AC-16, AC-54, AC-62).
5. Her okuma ve yazma kiracı + şirket (`TenantId` + `LegalEntityId`, sunucuda çözülür) kapsamındadır; kiracılar / şirketler arası erişim **404**.
6. **Silme yok.** Okutma olayı, aşım, cihaz, şablon, basım işi, SSCC, tarama ve dışa aktarım silinmez; DELETE ucu yoktur. Cihaz engellenir,
   şablon pasifleştirilir.

## 3. Owned Objects

| Nesne | Dilim | Koleksiyon (öneri) | Not |
|---|---|---|---|
| `ScanDevice` | DL-1 | `wms_barcode_devices` | Değişebilir, `Version` (If-Match); nabız alanları `Version`'ı artırmaz (§8.2) |
| `ScanEvent` | DL-1 | `wms_barcode_scan_events` | Insert-only; idempotent |
| `ScanOverride` | DL-1 | `wms_barcode_scan_overrides` | Insert-only; olay başına en çok bir |
| `SupervisorPin` | DL-1 | `wms_barcode_supervisor_pins` | Kullanıcı + şirket başına bir; yalnız özet (hash) |
| `BarcodeSettings` | DL-1 | `wms_barcode_settings` | Şirket başına bir; `Version` |
| `BarcodeCommandRequest` | DL-1 | `wms_barcode_command_requests` | **r4 (F-4):** okutma dışındaki her 0182 yazma komutunun idempotency deposu; insert-only, komutun işleminde yazılır; sır taşımaz (§4.11) |
| `ScanOverrideAttempt` | DL-1 | `wms_barcode_override_attempts` | **r4 (F-5):** başarısız aşım denemesi (yanlış PIN, kilit kuruldu, kilitliyken deneme) yerel izi; insert-only, işlem dışı; PIN taşımaz (§4.3, §8.5) |
| `ScanSessionBinding` | DL-3 | `wms_barcode_session_bindings` | Insert-only; `SessionId` tekil — bir oturum bir onayda harcanır |
| `LabelTemplate` | DL-4 | `wms_barcode_label_templates` | Değişebilir, `Version`; pasifleştirilir, silinmez |
| `LabelPrintJob` | DL-4 | `wms_barcode_label_prints` + `wms_barcode_label_files` | Insert-only; PDF dosyası ve sha256 |
| `SsccAssignment` | DL-5 | `wms_barcode_sscc` | Insert-only; SSCC kiracıda tekil |
| `SsccCounter` | DL-5 | `wms_barcode_sscc_counters` | (kiracı, önek, uzantı hanesi) başına atomik sayaç; sıfırlanmaz |
| `RfidSweep` | DL-6 | `wms_barcode_rfid_sweeps` | Insert-only |
| `ScanTraceExport` | DL-7 | `wms_barcode_trace_exports` + `wms_barcode_trace_files` | Insert-only |
| Koleksiyon sabitleri | DL-1 | `BarcodeCollections` (0182 Persistence) | MOD-0178 `WarehouseCollections` ile paylaşılmaz (MOD-0181 `CycleCountCollections`, MOD-0180 `WaveCollections` emsali) |

Koleksiyon öneki `wms_barcode_*` önerisidir; kesin ad DL-1'in ilk ölçümüyle sabitlenir.

**Komutlar (yazma, 17 — DL-1 10, DL-4 5, DL-6 1, DL-7 1; r1 / r2'nin "18" yazımı yanlıştı, r3 F-6; r4 F-2 ile `PairScanDeviceCommand` eklendi):**
- DL-1: `RegisterScanDeviceCommand`, `UpdateScanDeviceCommand`, `ActivateScanDeviceCommand` (onay ve yeniden etkinleştirme),
  `DeactivateScanDeviceCommand` (engelleme, gerekçe), `PairScanDeviceCommand` (r4, F-2: eşleştirme koduyla anahtarın telefona teslimi — §8.2),
  `RecordDeviceHeartbeatCommand`, `RecordScanCommand`, `OverrideScanCommand`, `SetSupervisorPinCommand`, `UpdateBarcodeSettingsCommand`.
- DL-4: `CreateLabelTemplateCommand`, `UpdateLabelTemplateCommand`, `ActivateLabelTemplateCommand`, `DeactivateLabelTemplateCommand`,
  `CreateLabelPrintJobCommand` (yeniden basım dahil; DL-5'te SSCC ve lot / koli türleri eklenir).
- DL-6: `RecordRfidSweepCommand`.
- DL-7: `CreateScanTraceExportCommand`.
- DL-3: `ScanSessionBinding` yazımı ayrı komut **değildir** — 0178 / 0181 onay komutunun işleminde port çağrısıdır (§8.7, §21).
- Sayım başlatma (S-8) 0182 komutu **değildir**: istemci MOD-0181'in `POST /api/warehouse/cycle-counts` ucunu çağırır (§8.10).

**Sorgular:** `GetScanEventListQuery`, `GetScanEventByIdQuery`, `GetScanEventDashboardQuery`, `GetScanDeviceListQuery`,
`GetScanDeviceByIdQuery`, `GetBarcodeSettingsQuery`, `GetLabelTemplateListQuery`, `GetLabelTemplateByIdQuery`, `PreviewLabelQuery`,
`GetLabelPrintJobListQuery`, `GetLabelPrintJobByIdQuery`, `GetLabelPrintJobFileQuery`, `GetRfidSweepByIdQuery`, `GetScanTraceQuery`,
`GetScanTraceExportListQuery`, `GetScanTraceExportFileQuery`.

**API (taban `/api/warehouse`, aile `/api/warehouse/barcode/…` — §15; yol adları SCAN-BUNDLE taslağında kesinleşir). PUT / DELETE yok, komut uçları:**
`POST /barcode/scans` (okut — sonuç gövdede) · `POST /barcode/scans/{scanId}/override` (PIN aşımı; izin `scans.create` — §8.5, r3) · `GET /barcode/scan-events` ·
`GET /barcode/scan-events/{scanId}` · `GET /barcode/scan-events/dashboard` · `GET/POST /barcode/devices` · `GET /barcode/devices/{deviceId}` ·
`POST /barcode/devices/{deviceId}/update` · `POST /barcode/devices/{deviceId}/activate` · `POST /barcode/devices/{deviceId}/deactivate` ·
`POST /barcode/devices/pair` (r4, F-2: eşleştirme kodu → anahtar, bir kez; izin `devices.create` — §8.2) ·
`POST /barcode/devices/{deviceId}/heartbeat` · `GET /barcode/settings` · `POST /barcode/settings/update` · `POST /barcode/supervisor-pin` ·
DL-4: `GET/POST /barcode/label-templates` · `GET /barcode/label-templates/{templateId}` · `POST /barcode/label-templates/{templateId}/update` ·
`…/activate` · `…/deactivate` · `POST /barcode/label-previews` (salt okuma; gövde GS karakteri taşıyabildiği için POST) ·
`GET/POST /barcode/label-prints` · `GET /barcode/label-prints/{jobId}` · `GET /barcode/label-prints/{jobId}/file` (PDF) · DL-6:
`POST /barcode/rfid-sweeps` · `GET /barcode/rfid-sweeps/{sweepId}` · DL-7: `GET /barcode/trace` (`lot` + `skuId` \| `serial` + `skuId` \| `sscc`) ·
`GET/POST /barcode/trace/exports` · `GET /barcode/trace/exports/{exportId}/file`. Sabit kesimler (`dashboard`, `settings`, `trace`) parametreli
yoldan önce eşleşir (`devices/pair` sabit kesimi `devices/{deviceId:guid}`'den önce); `{…Id}` parametreleri `:guid` kısıtlıdır. **Dilim başına servis
ucu (r3, F-1 — X-12'nin sayısı; §3 listesinden; r4, F-2: DL-1'e `devices/pair`):** DL-1 16, DL-4 11, DL-6 2, DL-7 4 = 33; DL-2 (yalnız Web), DL-3 (0178 / 0181 içinde port), DL-5 (SSCC ve lot / koli türleri var olan `label-prints`'e,
GTIN çözümü `scans`'e, önek `settings/update`'e gelir) ve DL-8 (yalnız test) servis ucu eklemez.

**Controller tabanı:** `BarcodeScansController`, `ScanDevicesController`, `BarcodeSettingsController`, `LabelTemplatesController`,
`LabelPrintsController`, `RfidSweepsController`, `ScanTraceController` — hepsi `[Route("api/warehouse")]`, eylem şablonu tam yol parçası
(`[HttpPost("barcode/scans")]` …); dosyalar `INV/Api/Controllers/Barcode/` altında, namespace `Diten.InventoryService.Api.Controllers.Barcode`.
**Neden ayrı namespace:** MOD-0178'in kayıt testi `Controllers.Warehouse` namespace'indeki controller'ların zorladığı anahtarları 0178
manifestiyle eşit sayar (MOD-0180 §3 ölçümü); 0182 controller'ı oraya konursa o test kırmızı olur (AC-6 sabotajı). Ayrı namespace'in
bedeli (r3, F-2): MOD-0173'ün `InventoryLedgerManifestCompletenessTests`'i dışlamadığı her controller'ı INVENTORY-LEDGER manifestinde arar —
`Controllers.Barcode` dışlaması X-15'tir, 0182 anahtarlarının kendi manifestinden ulaşılabilirliği AC-6'da ölçülür.

**Frontend rotaları:** `/Warehouse/ScanEvents` (Scan Event Monitor), `/Warehouse/ScanDevices` (Device Health), `/Warehouse/BarcodeSettings`
(ayarlar), `/Warehouse/SupervisorPin` (kendi PIN'im), `/Warehouse/LabelTemplates`, `/Warehouse/LabelPrints`, `/Warehouse/ScanTrace`
(Compliance Trace). Brief'teki `/Warehouse/Devices` adı `ScanDevices` olarak yazıldı (Warehouse alanında genel "Devices" adının başka
cihaz türleriyle karışmaması için — CT önerisi, davranış farkı yok).

**İzinler:** §14.

## 4. Entity Fields

Tüm varlıklar InventoryService `EntityBase`'inden türer: `Id`, `TenantId`, `LegalEntityId`, `CreatedAt`, `UpdatedAt`, `Version` (int),
`IsDeleted`, `DeletedAt` (MOD-0178 / MOD-0180 §4 ölçümü). `TenantId` / `LegalEntityId` sunucuda çözülür; gövde ya da sorguda gelirse 400.
Kullanıcı alanı `EntityBase`'de yoktur; gereken varlık kendi alanını taşır. Zamanlar UTC. Insert-only varlıklarda `Version` 0 kalır.
**Ekranda kimlik (GUID) gösterilmez** (mobil not §3A kuralı): raf kodu, lot numarası, SKU kodu / adı, kişi adı, cihaz adı / kodu.

### 4.1 ScanDevice
| Alan | Tip | Kural |
|---|---|---|
| `Number` | string | `{ŞİRKET}-DVC-{000001}` (§8.13); bir kez yazılır |
| `Name` | string | trim, 2..80; şirkette etkin cihazlar arasında tekil |
| `Type` | enum | `PhoneCamera` \| `HandheldScanner` \| `RfidReader` (E-7: v1'de el terminali alınmaz; tür sağlayıcıdan bağımsızlık için durur; `RfidReader` = Android telefona bağlı el okuyucu — Q-7 = A, Q-12, karar 03; model TBD) |
| `Platform` | enum | `Android` \| `iOS` \| `Other` |
| `InstallationId` | string | istemcinin kalıcı kurulum kimliği, 8..100; şirkette tekil (aynı telefon iki kez kaydolmaz) |
| `WarehouseId` | uuid \| null | doluysa okutma yalnız o depoda; boşsa şirketin her deposu (çok depolu çalışan — mobil brief §1) |
| `Status` | enum | `Pending` → `Active` → `Blocked` → `Active` (§8.2); silinmez |
| `KeyHash` | string \| null | cihaz anahtarının SHA-256 özeti; anahtarın kendisi yalnız mobilden doğrudan kaydın ya da eşleştirmenin (`POST /barcode/devices/pair`) cevabında bir kez döner, hiçbir yerde saklanmaz / loglanmaz — idempotency kaydı da anahtarsız cevabı saklar; replay anahtarı döndürmez (§8.2, r3 F-4). **r4 (F-2):** Web'den kayıt ve yeniden etkinleştirme anahtar üretmez; `KeyHash` null olur (eski anahtar o anda 403) ve eşleştirme kodu üretilir — null iken her anahtar 403 |
| `PairingCodeHash`, `PairingExpiresAt`, `PairingIssuedBy`, `PairedAt` | string \| null / UTC \| null / uuid \| null / UTC \| null | **r4 (F-2):** tek kullanımlık eşleştirme kodunun SHA-256 özeti (kodun kendisi saklanmaz), son geçerlilik (üretim + 10 dk), kodu üreten Web kullanıcısı, son başarılı eşleştirme zamanı; kod kullanılınca `PairingCodeHash` / `PairingExpiresAt` null (§8.2) |
| `LastSeenAt`, `LastUserId`, `LastUserName`, `AppVersion`, `BatteryPercent`, `BatteryReportedAt` | nabız alanları | `Version`'ı artırmaz; yalnız nabız ve okutma yazar |
| `BlockedReason`, `BlockedAt`, `BlockedBy` | string / UTC / uuid | engellemede zorunlu gerekçe 10..500 |
| `Note` | string \| null | ≤ 500 |
| `CreatedBy`, `CreatedByName` | uuid / string | kaydı başlatan kullanıcı |

### 4.2 ScanEvent
| Alan | Tip | Kural |
|---|---|---|
| `SessionId` | uuid \| null | istemci üretir: bir görev satırı onayına giden okutmaların ortak kimliği; `Query` bağlamında yok. Oturumun sahibi ilk olayının kullanıcısıdır; başka kullanıcının oturumuna olay yazılmaz (422 `BARCODE_SESSION_INVALID`, §8.3 — r3 F-11) |
| `DeviceId`, `DeviceNumber`, `DeviceName` | uuid / string | başlıktaki cihaz; ad ve kod okutma anında kopyalanır |
| `WarehouseId` | uuid | LOCATION `Warehouse`, şirket kapsamı |
| `UserId`, `UserName` | uuid / string | JWT'den |
| `Source` | enum | `Scanner` \| `Camera` \| `Rfid` \| `Manual` |
| `RawValue` | string ≤ 500 | **değiştirilmeden** saklanır (FNC1 / GS U+001D ve sembol tanıtıcısı dahil); başka denetim karakteri gelirse 400 |
| `Parsed` | nesne | `{ kind: Gs1 \| Plain, gtin?, lot?, expiryRaw?, expiry?, serial?, sscc? }` — MOD-0174 ayrıştırıcısından (§8.3) |
| `Resolved` | nesne | `{ locationId?, locationCode?, skuId?, itemId?, skuLevel?, skuCode?, skuName?, lotId?, lotNumber?, packageId?, packageNumber? }` |
| `Context` | nesne | `{ kind: Putaway \| Pick \| Count \| Pack \| Query, refId?, lineRef?, step: Location \| Product \| Lot \| Serial \| Package }` |
| `Outcome` | enum | `Accepted` \| `Warning` \| `Rejected` |
| `ReasonCode` | string \| null | §8.15 okutma sonucu kodları; `Accepted`'da null |
| `Overridable` | bool | v1'de yalnız `Putaway` bağlamında `WRONG_LOCATION` için `true` (S-7, toplama için Q-3 = A ile daraltıldı — §8.5); başka her nedende ve bağlamda `false` |
| `Manual` | nesne \| null | `{ reasonCode, note? }` — `Source = Manual` iken zorunlu (S-10) |
| `ReceivedAt` | UTC | sunucu zamanı; tek geçerli zaman |
| `CapturedAt` | UTC \| null | istemci zamanı, yalnız bilgi (çevrimdışı yeniden oynatma FU'su için yer) |
| `IdempotencyKey`, `RequestFingerprint`, `CorrelationId` | string / string / uuid | §8.3 |

Okutma sonucunun "Aşıldı" (Overridden) hali `ScanEvent`'e yazılmaz; okumada `ScanOverride` ile birleştirilir (olay değişmez).

### 4.3 ScanOverride ve SupervisorPin
`ScanOverride`: `ScanId` (tekil), `OverriddenReasonCode`, `SupervisorUserId`, `SupervisorName`, `DeviceId`, `ApprovedAt`, `Note?` (≤ 500).
`SupervisorPin`: `UserId` (şirkette tekil), `PinHash` + `Salt` + `Iterations` (PBKDF2-SHA256, ≥ 100.000 tur, .NET `Rfc2898DeriveBytes.Pbkdf2`),
`SetAt`, `ExpiresAt` (`SetAt` + `PinValidityDays`), `FailedAttempts`, `LockedUntil`, `Version`. `FailedAttempts` / `LockedUntil` yalnız aşım
denemesinin tek belgelik atomik yazımlarıyla değişir, `Version`'ı artırmaz ve `If-Match` istemez (§8.5, r3 F-10). PIN 4..8 hane; PIN ve özeti hiçbir cevapta,
logda, OTel'de, denetim kaydında **ya da idempotency deposunda / parmak izinde** yer almaz (Q-4 = A, karar 03; r4 F-3 — §8.1).
`ScanOverrideAttempt` (r4, F-5): `ScanId`, `DeviceId`, `DeviceNumber`, `AttemptedBy` + `AttemptedByName` (JWT kullanıcısı), `SupervisorUserId` +
`SupervisorName`, `Result` (`PinInvalid` \| `LockSet` — bu deneme kilidi kurdu \| `Locked` — kilitliyken deneme), `FailedAttemptsAfter` (int),
`LockedUntil?`, `At` (UTC). Insert-only; PIN, PIN özeti ve girilen değerin hiçbir türevi yoktur.

### 4.4 BarcodeSettings
| Alan | Tip | Varsayılan | Kural |
|---|---|---|---|
| `SilentDeviceHours` | int | 24 (S-11) | 1..720 |
| `LowBatteryPercent` | int | 20 (S-11) | 1..90 |
| `PinMaxAttempts` | int | 3 (MS-4; Q-4 = A, karar 03) | 1..10 |
| `PinLockMinutes` | int | 5 (Q-4 = A, karar 03) | 1..1440 |
| `PinValidityDays` | int | 90 (Q-4 = A, karar 03) | 1..365 |
| `ScanSessionMaxMinutes` | int | 30 | 1..480 — oturumun DL-3 onayında geçerli kaldığı süre |
| `ScanRequiredWarehouseIds` | uuid[] | boş (Q-9 = A, karar 03: depo başına, varsayılan kapalı) | DL-3; her biri şirketin deposu |
| `SsccCompanyPrefix` | string \| null | null | DL-5; MOD-0290-FU07 etkin önek listesinde olmalı |
| `SsccExtensionDigit` | int | 0 (Q-10 = A, karar 03) | DL-5; 0..9 |

### 4.5 ScanSessionBinding
`SessionId` (tekil), `Module` (`MOD-0178` \| `MOD-0181`), `TargetKind` (`PutawayConfirmation` \| `PickConfirmation` \| `CountEntry` \|
`CountFoundLine`), `TargetId`, `LineRef`, `ScanIds[]` (oturumdan sayılan olaylar), `ConsumedAt`, `ConsumedBy`. Onay işleminin içinde yazılır (§8.7).

### 4.6 LabelTemplate ve LabelPrintJob
`LabelTemplate`: `Name` (2..80, şirkette tekil), `LabelType` (`Location` \| `LotCase` \| `Sscc`), `Size` (sabit liste — Q-17, karar 03: `100x150`,
`100x50`, `50x25` mm), `HumanReadableFields[]` (türün izinli alan kümesinden, §8.9), `IsDefault` (tür başına tek etkin varsayılan),
`Status` (`Active` \| `Inactive`), `Note?`, `Version`.
`LabelPrintJob`: `Number` (`{ŞİRKET}-LBL-{000001}`), `LabelType`, `TemplateId` + `TemplateVersion` (anlık), `Sources[]` (lokasyon kimlikleri
\| `{skuId, lotId}` \| koli kimlikleri \| serbest palet adedi), `Copies` (1..20), `LabelCount` (≤ 500), `Language` (7 dilden biri),
`IsReprint`, `ReprintOfJobId?`, `ReprintReasonCode?`, `ReprintNote?`, `Ssccs[]` (DL-5), `FileId`, `FileSha256`, `CreatedBy`, `CreatedByName`.

### 4.7 SsccAssignment ve SsccCounter
`SsccAssignment`: `Sscc` (18 hane, kiracıda tekil), `CompanyPrefix`, `ExtensionDigit`, `SerialReference`, `UnitKind` (`Package` \| `Pallet`),
`PackageId?` / `PackageNumber?` / `OutboundId?` (MOD-0178 kolisi), `PrintJobId`, `CreatedBy`. `SsccCounter`: (`TenantId`, `CompanyPrefix`,
`ExtensionDigit`) anahtarı, `Next` (long). Atomik artırma (`findOneAndUpdate` + `$inc`), geri alınmaz, yeniden kullanılmaz.

### 4.8 RfidSweep
`WarehouseId`, `LocationId`, `DeviceId`, `Epcs[]` (≤ 5.000, büyük harf hex, tekrarsız), `Decoded[]` (`{epc, scheme: Sgtin96 \| Sscc96 \| Unknown,
gtin?, serial?, sscc?}`), `Expected[]` (tarama anındaki defter okuması: GTIN / SKU / lot başına miktar ve seri listesi — kör sayım bin'inde
yalnız sayıyla değil, hiç yazılmaz, §2.3 kural 4), `Differences[]`, `RepeatOfSweepId?`, `CreatedBy`.

### 4.9 ScanTraceExport
`Query` (`lot` + `skuId` \| `serial` + `skuId` \| `sscc`), `Format` (`Pdf` \| `Csv`), `Language`, `RowCount`, `FileId`, `FileSha256`, `CreatedBy`.

### 4.10 Indexes
| Koleksiyon | Index | Amaç |
|---|---|---|
| `wms_barcode_scan_events` | `(TenantId, LegalEntityId, WarehouseId, ReceivedAt desc)` | liste (depo + 31 gün) |
| `wms_barcode_scan_events` | `(TenantId, LegalEntityId, UserId, IdempotencyKey)` tekil | idempotency |
| `wms_barcode_scan_events` | `(TenantId, LegalEntityId, SessionId)` kısmi (`SessionId` dolu) | DL-3 oturum okuması |
| `wms_barcode_scan_events` | `(TenantId, LegalEntityId, Parsed.lot)`, `(…, Parsed.serial)`, `(…, Parsed.sscc)` kısmi | DL-7 uyum izi |
| `wms_barcode_devices` | `(TenantId, LegalEntityId, InstallationId)` tekil; `(TenantId, LegalEntityId, Name)` tekil kısmi (`Status ≠ Blocked`) | kayıt |
| `wms_barcode_scan_overrides` | `(TenantId, LegalEntityId, ScanId)` tekil | olay başına bir aşım |
| `wms_barcode_supervisor_pins` | `(TenantId, LegalEntityId, UserId)` tekil | |
| `wms_barcode_session_bindings` | `(TenantId, LegalEntityId, SessionId)` tekil | bir oturum bir onay |
| `wms_barcode_label_templates` | `(TenantId, LegalEntityId, Name)` tekil; `(…, LabelType, IsDefault)` tekil kısmi (`IsDefault = true`, `Status = Active`) | |
| `wms_barcode_sscc` | `(TenantId, Sscc)` tekil; `(TenantId, LegalEntityId, PackageId)` tekil kısmi | SSCC tekilliği, koli başına bir SSCC |
| `wms_barcode_sscc_counters` | `(TenantId, CompanyPrefix, ExtensionDigit)` tekil | |
| `wms_barcode_devices` | `(TenantId, LegalEntityId, PairingCodeHash)` tekil kısmi (`PairingCodeHash` dolu) — r4, F-2 | eşleştirme kodu araması |
| `wms_barcode_command_requests` | `(TenantId, LegalEntityId, ActorUserId, RequestOp, IdempotencyKey)` tekil — r4, F-4 | komut idempotency'si (MOD-0181 `CycleCountEvent` `EventKeyIndex` deseni) |
| `wms_barcode_override_attempts` | `(TenantId, LegalEntityId, ScanId, At)`; `(TenantId, LegalEntityId, DeviceId, At desc)`; `(TenantId, LegalEntityId, SupervisorUserId, At desc)` — r4, F-5 | olay ayrıntısı, Device Health, sorumlu başına deneme görünümü |

### 4.11 BarcodeCommandRequest (komut idempotency deposu — r4, F-4)
`RequestOp` (komut adı, ör. `RegisterScanDevice`, `OverrideScan`), `ActorUserId`, `IdempotencyKey`, `RequestFingerprint` (§8.1 kuralı — PIN
dışlanır), `TargetId`, `ResponseStatus` (int), `ResponseBody` (sırsız cevap DTO'su — §8.1), `CreatedAt`. Okutma olayının (`RecordScanCommand`)
anahtarı `ScanEvent` üzerindedir (§4.2, §4.10) ve buraya yazılmaz; cihaz nabzı idempotency kaydı yazmaz (§8.2, §12 muafiyeti). Belge komutun
yazımıyla **aynı işlemde** eklenir: başarısız komut kayıt bırakmaz, aynı anahtarla yeniden deneme yeniden çalışır.

## 5. Repo Scope

`INV` = `services/Diten.InventoryService/src/Diten.InventoryService.`, `INVT` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`.

| Yol | Ne | Dilim |
|---|---|---|
| `docs/analysis/contracts/drafts/scan-bundle-v1-proposal.md` (yazılacak) | SCAN-BUNDLE taslağı (contract-first; **DL-1'in ilk adımı**; dondurma ayrı CT adımı; mobil sözleşme notunun kaynağı) | DL-1 |
| `INV/Domain/Barcode/{Entities,Enums,Rules,Repositories}/` | varlıklar; SSCC kontrol hanesi, EPC çözümü (SGTIN-96 / SSCC-96), bağlam karşılaştırma kuralları (saf) | DL-1…DL-7 |
| `INV/Application/Features/Barcode/` | ortak: `BarcodeContract.cs`, `BarcodeErrorCodes.cs`, `BarcodeCentralAudit.cs`, `Ports/` (`IScanSessionGate` — DL-3), `Services/` (okutma doğrulayıcı, bağlam okuyucuları, PIN hasher, etiket çizici, PDF üretici, SSCC ayırıcı, EPC çözücü, iz derleyici), `Resources/BarcodeText.{en,tr,fr,es,zh,ar,ru}.resx` | DL-1…DL-7 |
| `INV/Application/Features/BarcodeScans/`, `ScanDevices/`, `BarcodeSettings/`, `LabelTemplates/`, `LabelPrints/`, `RfidSweeps/`, `ScanTrace/` | Golden Slim: Commands, Queries, Handlers/CommandHandlers, Handlers/QueryHandlers, Validators, `{Module}Models.cs` | dilimine göre |
| `INV/Persistence/Barcode/` | depolar, `BarcodeCollections`, index yapılandırması, başka modül okumaları (**yalnız `Find`**) | DL-1…DL-7 |
| `INV/Api/Controllers/Barcode/` | §3'teki controller'lar | DL-1…DL-7 |
| `INV/Api/Barcode/` | `BarcodeRfidManifestProvider`, `BarcodeServiceCollectionExtensions`, cihaz başlığı okuyucusu | DL-1 |
| `INV/Infrastructure/Authorization/BarcodePermissions.cs` | izin sabitleri | DL-1 |
| `INVT/Barcode/` | birim, entegrasyon, mimari testler (0182'nin kendi test ağacı; DL-3'ün 0178 / 0181 davranış testleri **burada**) | her dilim |
| `frontend/Diten.Web/Views/Warehouse/{ScanEvents,ScanDevices,BarcodeSettings,SupervisorPin,LabelTemplates,LabelPrints,ScanTrace}/`, `frontend/Diten.Web/Controllers/`, `frontend/Diten.Web/wwwroot/assets/js/Warehouse/…`, `frontend/Diten.Web/Resources/Views/Warehouse/…` | ekranlar, proxy controller'lar, JS, resx (7 dil) | DL-2, DL-4, DL-7 |
| `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx`, `frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx`, `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs` | ortak Web kaynakları — **yalnız eklemeli**: nav anahtarları, sayfa yardımı (§25), Rol İzinleri satır adları (`Perm.Entity.*`) | DL-2, DL-4, DL-7 |
| `frontend/Diten.Web.Tests/Warehouse/BarcodeL10nAndHelpTests.cs` (yeni) | 0182'nin L10n / Layout / yardım testi | DL-2 |
| `gateway/Diten.ApiGateway.Tests/Mod0182BarcodeRouteTests.cs` (yeni, integration-agent) | uç kapsama testi + cihaz başlıklarının iletimi; üretim `ocelot.json`'u okur | DL-8 |

### 5.1 XMC-001 — başka modülün dosyalarına dokunuşlar
Her satır dokunmadan önce sahibine **tek tek** sorulur; yalnız eklemeli. Ölçüm HEAD'leri: r1 `90f6a1da8`; X-12, X-15 ve X-6'nın `:53` / `:54` /
`:60-69` atıfları r3'te `bf7bd7852`; **r4 (F-9):** X-6 (`:53`, `:127-130`), X-12 (`:97`), X-15 (`:209-225`) ve X-1 (`Gs1CodeParser.cs:24`, `:40`)
`db70c9f37`'de yeniden ölçüldü — satır numaraları r3 ölçümüyle aynı (arada yalnız inceleme kayıtları değişti). **Sahip onayları (XMC-001):**
X-1, X-12 (her artış dahil), X-15 — kararlar 03 eki `ad1100158`; X-6 genişletmesi (`:130` işlem sayısı) — kararlar 03 eki `4168e7a90`. Diğer
satırlar (X-2…X-5, X-7…X-11, X-13, X-14 ve X-6'nın `:53` / `:60-69` kısmı) dilim başlamadan sahibinden onay ister.

| # | Dosya | Bugün | Gereken değişiklik | Sahip | Dilim |
|---|---|---|---|---|---|
| X-1 | `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Traceability/Rules/Gs1Code.cs` ve `Gs1CodeParser.cs` | ayrıştırıcı (00) uzunluğunu bilir (`Gs1CodeParser.cs:24`) ama `Gs1Code` (00)'ı taşımaz (`Gs1Code.cs:8`: GTIN, seri, SKT, lot) | `Gs1Code`'a eklemeli `Sscc` üyesi (init özelliği — konumlu kurucuyu bozmaz); (00) tek başına gelen dizge de okunur. **r3 (F-12) — kapalı listeli AI toleransı:** ham (FNC1 / GS) biçimde ayrıştırıcı tablosunda olmayan her AI bütün dizgeyi reddeder (`Gs1CodeParser.cs:133-136`; köşeli biçimde bilinmeyen AI zaten okunup yok sayılır, `:92`), gerçek koli / palet etiketlerindeki (37), (3103), (240) okutmayı `UNREADABLE` yapar. Ekleme: ayrıştırıcıya **kapalı bir "okunur, kullanılmaz" listesi** — (37) değişken ≤ 8 rakam, (310n) n = 0..5 sabit 6 rakam (4 haneli AI), (240) değişken ≤ 30; bu alanlar uzunluklarıyla atlanır, `Gs1Code`'a üye eklenmez; listede olmayan AI bugünkü gibi reddedilir (`TraceDomainRulesTests.cs:147` `"99ABC"` vakası değişmeden yeşil); yalnız toleranslı AI'lardan oluşan dizge okunmaz (`:40` kuralı aynı). Liste genişletmesi FU-0182-GS1AI. MOD-0174'ün `gs1-check` ucu da bu listeyi kabul etmeye başlar — davranış farkı MOD-0174 sahibinin onayındadır. MOD-0174 testleri değişmeden yeşil. **r4 (F-7) — `:40` kuralına etkisi açıkça:** bugün `:40`
"en az biri (01), (21), (10), (17)" ister; X-1 bu kümeye **yalnız (00)**'ı ekler — (00) tek başına gelen dizge okunur (`Gs1Code` dört alanı null,
`Sscc` dolu); toleranslı AI'lar ((37), (310n), (240)) ve (02) kümeye **eklenmez**, yalnız onlardan oluşan dizge okunmaz. (02) bugün de okunur
(`:24` sabit 14) ama `Gs1Code`'a girmez; X-1 (02) için üye eklemez (FU-0182-GS1AI). Sonuç: (00) + (02) + (37) + (10) taşıyan lojistik etiket
okunur (SSCC + lot), GTIN null'dır — ürün adımı davranışı §8.4 (Rejected `PRODUCT_NOT_VERIFIABLE`). MOD-0174 `gs1-check` ucu için etki: yalnız
(00) taşıyan dizge artık ayrıştırılır (alanlar null). **Sahip onayı: `ad1100158`** (X-1 satırı r3 metniyle; onay metni AI listesini ve gs1-check
yan etkisini adıyla anar) | MOD-0174 (merkez, Beste) | DL-5 |
| X-2 | MOD-0178 yerleştirme / toplama onay işleyicileri (`PutawayTasks`, `PickTasks` onay komutları) | `scanRef` opak, ≤ 200, doğrulanmaz (`WarehouseRequestReader.cs:285`, `:305`) | depo `ScanRequired` ise onay işleminin içinde `IScanSessionGate.ConsumeAsync` çağrısı (§8.7); kapalıysa davranış aynı | MOD-0178 (merkez) | DL-3 |
| X-3 | MOD-0181 sayım girişi ve bulunan satır işleyicileri (`RecordCountLineHandler`, `AddCountLineHandler`) | `scanRef` opak, ≤ 200 (`CycleCountContract.cs:62`) | X-2 ile aynı kapı; kör sayım cevabı değişmez | MOD-0181 (merkez) | DL-3 |
| X-4 | `INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs` `TypePrefixes` | PUT / PCK / OUT / PKG / CNT (+ WAV — MOD-0180) | `DVC`, `LBL` sabitleri ve listeye ekleme (MOD-0181 `CNT`, MOD-0180 `WAV` emsali) | MOD-0178 | DL-1 (`DVC`), DL-4 (`LBL`) |
| X-5 | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` | 0181 / 0180 koleksiyon bildirimleri | `BarcodeCollections.All` bildirimi (r4: `wms_barcode_command_requests` ve `wms_barcode_override_attempts` dahil — §3); önek süzgeci değişmez | MOD-0173 (merkez) | DL-1 |
| X-6 | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` | yazma komutu sayısı sabit (`:53` `Assert.Equal(117, WriteCommands.Length)`); `:54` **her** `IRequest` yazma komutunun `ICentralAuditDeclaration` taşımasını ister; `:60-69` kayıtlı modül kodları listesi. **r4 (F-1):** aynı dosyada `Every_transaction_a_command_can_run_in_enlists_the_central_audit_event` Persistence'taki her `WithTransactionAsync(` **çağrı yerini** sayar (`:130` `Assert.Equal(14, seen)`, yorum `:127-129`; süpürücüler hariç) ve her çağrı yerinin geri çağrısında (ilk 1.200 karakter) `CentralAuditTransaction.EnlistAsync(s` ister; ölçüm HEAD `db70c9f37` | (a) **komut sayısı (`:53`):** dilim başındaki ölçülen sayı + dilimin 0182 komutları (§21): **DL-1 +10, DL-4 +5, DL-6 +1, DL-7 +1** (DL-2, DL-3, DL-5 0; toplam 17 — r3 F-6, r4 F-2 `PairScanDeviceCommand`); yorum satırına dilimin komutları adıyla; DL-1'de `:60-69` listesine `BarcodeRfidManifestProvider` (MOD-0181 `:68` emsali). `RecordDeviceHeartbeatCommand` da sayılır ve işaret taşır (İ2 — kayıt doğurmaması §21'de). (b) **işlem çağrı yeri sayısı (`:130`, r4 F-1):** işlem açan her MOD-0182 dilimi sayıyı **dilim başında ölçülen sayı + dilimin 0182 Persistence'a eklediği yeni `WithTransactionAsync(` çağrı yerleri** yapar; değişiklik o çağrı yerleriyle **aynı commit'tedir**; `:127-129` yorumuna dilimin adı ve çağrı yeri yazılır (ör. "+ BarcodeTransactions.InTransactionAsync (MOD-0182 DL-1, XMC-001)" — MOD-0181 DL-1 emsali); kabul kaydı önce → sonra değerini yazar ve satır numarasına değil `Assert.Equal(<n>, seen)` satırına atıf yapar. **İşlem içinde yazan dilimler** (§3 / §21'den doğrulandı): DL-1 (nabız hariç 9 komut — nabız ve PIN sayacı / aşım denemesi izi işlem açmaz, §8.2, §8.5), DL-4 (5 komut), DL-5 (SSCC ataması ve sayaç — DL-4'ün basım komutunun işleminde), DL-6 (1), DL-7 (1); DL-2, DL-3 (bağlama 0178 / 0181'in **kendi** işlemine katılır — §8.7), DL-8 işlem açmaz. **Öneri (en dar):** 0182 Persistence'ta tek ortak işlem yardımcısı (`BarcodeTransactions.InTransactionAsync`; MOD-0181'in 16 komutunun tek `CycleCountRepository.InTransactionAsync` çağrı yeri emsali, `:51-52` yorumu) — beklenen dağılım (çıkarım) DL-1 **+1** (14 → 15, arada başka modül eklemezse), DL-4 / DL-5 / DL-6 / DL-7 **+0** (satıra dokunulmaz); bir dilim yeni bir çağrı yeri açarsa (ör. ayrı SSCC deposu işlemi) kural ona uygulanır. MOD-0180 (X-10) ile sıra: hangisi önce gelirse dilim başında ÖLÇÜLEN sayıdan devam; paralel dallarda sonra birleşen dal birleştirmeden sonra yeniden ölçer. **Sahip onayı: (b) `4168e7a90`** (kararlar 03, "r3 incelemesi sonrası sahip kararları"); (a) dilim başlamadan sahibinden | MOD-0173 (merkez, Beste) | DL-1, DL-4, DL-6, DL-7 (a); işlem çağrı yeri ekleyen her dilim — beklenen yalnız DL-1 (b) |
| X-7 | `INV/Api/Program.cs` | manifest kayıtları | `BarcodeRfidManifestProvider` + `AddBarcode()` (kompozisyon kökü) | MOD-0173 (merkez) | DL-1 |
| X-8 | `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs` `RuleAheadOfCatalogue` | `CNT`, `WAV` satırları | RCS-001 §3'e `DVC` / `LBL` yazıldığı **aynı adımda** `["DVC"]`, `["LBL"]` = "MOD-0040-FU02 DL-8" | Platform (merkez) | `.antigravity` kapısı (§6) |
| X-9 | `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md` §14.1 | Q-08 notu: "0182 kendi namespace'ini açar (önerilen `barcode.*`)" (`:2131-2132`); `warehouse.waves.*` istisna notu | Q-1 = A (karar 03) olduğu için not: "`warehouse.barcode.*` adlı alt ad alanı MOD-0182'nindir; MOD-0178 bu alt ad alanında anahtar tanımlamaz" — 0178 paket revizyonu (yalnız not), ayrı inceleme + Ali onayı | MOD-0178 (merkez) | `.antigravity` kapısı (§6) |
| X-10 | Governance Rol İzinleri köprüsü (`frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml`, `SharedResource.*.resx` `Perm.Entity.*`) | fiiller `activate` `:188`, `approve` `:192`, `configure` `:239`, `deactivate` `:243`, `export` `:179` var | **yeni fiil yok**; yalnız varlık satır adları (`Perm.Entity.SCANS`, `.SCANEVENTS`, `.SCANOVERRIDES`, `.DEVICES`, `.SETTINGS`, `.LABELTEMPLATES`, `.LABELPRINTS`, `.TRACE` — kesin adlar DL-2 ölçümü) 7 dilde | Web ortak (merkez) | DL-2, DL-4, DL-7 |
| X-11 | `services/Diten.AuthService/**` rol şablonu | — | §14 rol önerisi (Q-14) | Ali (Auth) | ayrı WP |
| X-12 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — **yalnız `Assert.Equal(<n>, endpoints.Count)` satırı** (bugün `:97`, HEAD `bf7bd7852`) ve üstündeki uç yorumu (`:80-96`) (r3, F-1) | 0178 uç kapsama testi; üretim `ocelot.json`'u okur (`:23`; bağlı kopya `Diten.ApiGateway.Tests.csproj:26`) ve **bütün** InventoryService controller'larını özyineli tarar — sınıf `[Route]`'u `api/warehouse` ile başlayan her controller (`:219-237`, önek süzgeci `:228`, eylem regex'i `:229`); `:97` `Assert.Equal(73, endpoints.Count)` yönlendirilen uç sayısını sabitler, yorum her artışı adıyla sayar (son artış MOD-0181 DL-5, 70 → 73) | **XMC-001 değişikliği (r2'nin "değişmez" yazımı yanlıştı):** 0182 controller'ları (`[Route("api/warehouse")]`, `Controllers/Barcode/`) taramaya kendiliğinden girer; her 0182 ucu `/api/warehouse/{everything}` ile tek rotaya eşleşir, PUT / DELETE / PATCH yoktur — rota iddiaları (`:98-108`) değişmeden yeşil kalır, ama sayı satırı ilk 0182 ucuyla kırmızı olur. **Kural:** uç ekleyen her dilim (**DL-1 16, DL-4 11, DL-6 2, DL-7 4** — §3 / §23 / §24'ten; r4: DL-1'e `POST /barcode/devices/pair`, F-2; DL-2, DL-3, DL-5, DL-8 servis ucu eklemez, DL-5 bir uç eklerse kural ona da uygulanır) sayıyı **dilim başında ölçülen sayı + dilimin eklediği 0182 uç sayısı** yapar ve yorumuna dilimin uçlarını adıyla ekler (MOD-0181 emsali; MOD-0180 X-14 deseni); değişiklik dilimin controller uçlarıyla **aynı commit'tedir**, dilim kabulünde Gateway test projesi koşar — arada kırmızı pencere kalmaz (AC-65 0178 sayı vakası, S-65c). Kabul kaydı önce → sonra değerini yazar ve satır numarasına değil `Assert.Equal(<n>, endpoints.Count)` satırına atıf yapar. Eylem öznitelikleri `[HttpX("…")]` biçimindedir (ek argüman yok) — aksi halde `:229` regex'i ucu saymaz. Paket sabit taban yazmaz; beklenen dağılım **çıkarım**: arada başka modül uç eklemezse 73 → 89 → 100 → 102 → 106 (§3 listesine göre; r4 — r3'te 73 → 88 → 99 → 101 → 105; PIN sayfasının son kullanma tarihi okuması §3'te ayrı uç yazılı değil — DL-1 bir GET eklerse sayı ölçümden gelir). **MOD-0180 ile sıra:** MOD-0180 (X-14) aynı satırı DL-1 / DL-2 / DL-3 / DL-5'te 23 uçla artırır; iki paketin dilimleri hangi sırayla gelirse gelsin her dilim **dilim başında ÖLÇÜLEN** sayıdan devam eder, paralel dallarda sonra birleşen dal sayıyı birleştirmeden sonra yeniden ölçer ve yalnız kendi uçlarını ekler; yorumdaki iki paketin satırları adıyla kalır. `ocelot.json` ve bu iki satır dışındaki satırlar değişmez. **Durum: sahip onaylı (XMC-001) — kararlar 03 eki `ad1100158`, her artış dahil** (r4, F-9; MOD-0180'in X-14 onayı `c7dc4ea6a` / `6235794ab` 0182'yi kapsamaz; r4'teki DL-1 +1 uç aynı kuralla — "her artış dahil") | integration-agent (test dosyası); rota ailesi MOD-0178 (Beste) | DL-1, DL-4, DL-6, DL-7 |
| X-15 | `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Api/InventoryLedgerManifestCompletenessTests.cs` — `Every_enforced_permission_is_reachable_from_the_manifest` dışlama listesi (bugün `:209-225`, HEAD `bf7bd7852`; r3, F-2) | MOD-0173 testi; `:213-223` controller'ları namespace ile dışlar (Traceability, Quarantine, Expiry, Recall, Warehouse, CycleCounting); dışlanmayan her controller'ın `[HasPermission]` anahtarı INVENTORY-LEDGER manifestinde aranır | dışlama satırı + yorum: `.Where(t => t.Namespace?.EndsWith(".Controllers.Barcode", StringComparison.Ordinal) != true)` — "MOD-0182: the barcode controllers belong to BARCODE-RFID (own completeness test, AC-6)". Eklenmezse ilk `warehouse.barcode.*` anahtarı bu testi kırmızı yapar. Dışlamanın karşılığı 0182'nin kendi bütünlük vakasıdır (AC-6, S-6d). Emsal: MOD-0181'in `:223` dışlaması (sahip onaylı, MOD-0181 paketi `:1392`). MOD-0180 (`Controllers.WavePlanning`, MOD-0180 r10 X-17) önce gelirse satır kayar — dilim başında ölçülür. **Durum: sahip onaylı (XMC-001) — kararlar 03 eki `ad1100158`** (r4, F-9) | MOD-0173 (merkez, Beste) | DL-1 |
| X-13 | Inventory'nin PRODUCT-MASTER istemcisi (TRACE `ITraceSkuDirectory` ve onun uygulaması) | SKU `gtin` (FG satış kutusu) okunur | iki okuma eklenir: `GET /product-master/identifiers/{gtin}`, `GET /product-master/gs1-company-prefixes` (MOD-0290-FU07 sözleşmesi) — ayrı port arkasında, MOD-0174 davranışı değişmez | MOD-0174 / MOD-0173 (merkez) | DL-5 |
| X-14 | `docs/analysis/contracts/wms-bundle.openapi.yaml` (1.1.0) ve `wms-count-bundle.openapi.yaml` (1.0.0) | `scanRef` opak; `BARCODE_SESSION_*` kodları yok | eklemeli hata kodları (§8.15) yeni sözleşme sürümünde; servis sözleşme testleri şemayı kapalı okur — CT adımı, **DL-3 başlamadan önce** | MOD-0178 / MOD-0181 + CT | DL-3 |

**0178 / 0181 bekçi testleri değişmez (bağlayıcı):** `INVT/Warehouse/**` ve `INVT/CycleCount/**` dosyaları **hiçbir satırı değişmeden** yeşil
kalır (X-12'nin Gateway testi ve X-15'in MOD-0173 testi bu dizinlerde değildir; yalnız adlı satırları değişir); DL-3'ün 0178 / 0181 davranışı `INVT/Barcode/` altında ölçülür. Kanıt: dilim kabulünde
`git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount`
boş (AC-41).

## 6. Protected Paths
- `.antigravity/**` — kullanıcı onayı ister (Çekirdek md. 6). **Kapı — paket onayından ÖNCE, sahip onayıyla (MOD-0180 emsali):**
  (1) PKS-001 §4 `warehouse.barcode.*` satırı ve `:105` `warehouse.*` satırına "`warehouse.barcode.*` hariç — MOD-0182" eki; `:92` istisna
  cümlesindeki "today only `warehouse.waves.*`" ifadesinin iki alt ad alanını sayacak biçimde güncellenmesi (Q-1 = A, karar 03 — r2; B
  seçeneği `barcode.*` seçilmedi); (2) RCS-001 §3 `DVC` (cihaz) ve `LBL` (etiket basım işi) satırları; (3) aynı adımda X-8 ve X-9. **Hazırlayan:** CT; **onaylayan:**
  Beste. Bu paket ve geliştirme dilimleri `.antigravity/**`'a **yazmaz**. Kapı kapanmadan paket `approved` / `ready-for-dev` olmaz (§18, §24).
  Önerilen PKS-001 satırı: `` | `warehouse.barcode.*` | `Diten.InventoryService` — MOD-0182 Barcode/RFID (okutma olayları, cihazlar, sorumlu
  aşımı, etiket şablonları ve basımı, RFID taraması, uyum izi; owner decision Q-1 (MOD-0182 owner decisions 03, 2026-11-13) — `warehouse.*` altında MOD-0182'ye ayrılmış adlı alt ad
  alanı; MOD-0182 pack §14; base path `/api/warehouse/barcode`) | MOD-0182 (Barcode/RFID) | ``
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız integration-agent; bu paket değişiklik **istemez** (§15). `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs`
  yalnız X-12'nin iki satırı için, uç ekleyen dilimde, integration-agent eliyle (XMC-001, sahip onaylı — `ad1100158`; r4).
- `services/Diten.Building.Blocks/**` (BL-501 olduğu gibi).
- MOD-0173 / 0174 / 0175 / 0176 / 0177 / 0178 / 0180 / 0181 kodu ve testleri — yalnız §5.1'de **adıyla** sayılan dosyalar; listede olmayan bir
  dosyaya dokunmak gerekirse dilim durur ve XMC-001 ile sahibine sorulur.
- Donmuş sözleşmeler: `wms-bundle.openapi.yaml`, `wms-count-bundle.openapi.yaml` (yalnız X-14, CT adımıyla), `location.openapi.yaml`,
  `inventory-bundle.openapi.yaml`, PRODUCT-MASTER (MDM'nin; 0182 yalnız tüketir).
- MOD-0365'in dosyaları (platform-shared-services); `services/Diten.Platform/**` (X-8 dışında); `services/Diten.AuthService/**` (X-11, ayrı WP);
  `services/Diten.MdmService/**`; başka domain servisleri; Archive; `_Layout.cshtml`.

## 7. Dependencies

| Bağımlılık | Tür | Durum (ölçüm) | Kullanım | Değişiklik | Sahip |
|---|---|---|---|---|---|
| MOD-0178 görevler ve koliler | HARD (okuma); DL-3 XMC | kod var; onay gövdesi opak `scanRef` alır (`services/Diten.InventoryService/src/Diten.InventoryService.Api/Controllers/Warehouse/PickTasksController.cs:83`, `services/Diten.InventoryService/src/Diten.InventoryService.Api/Controllers/Warehouse/PutawayTasksController.cs:121`); koli `HandlingUnit` SSCC taşımaz (`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/Entities/HandlingUnitEntities.cs:24-27`) | bağlam (beklenen bin / SKU / lot), SSCC'nin koliye bağlanması 0182 tarafında | X-2, X-4, X-14 | merkez (Beste) |
| MOD-0178-FU01 outbound formu v2 | SOFT (DL-5, okuma) — **DL-5 FU01'i beklemez** (r4, F-6) | taslak r2, onaysız, kodu yok (HEAD'de `District` yok); FU01 DL-2 `ShipTo`'ya `District` / `DistrictRef` ekler (`execution/domains/supply-chain-execution/module-packs/MOD-0178-FU01-outbound-form-v2.md:156-172`, `District` `:171`, `PostalCode` `:172`); posta kodu CRM ana adresinde CRM alanı ekleyene kadar boş gelir, ek teslimat adreslerinde dolu gelir; elle girişte şehir / ilçe serbest metin (`:339`) | SSCC etiketinde alıcı ilçesi (Q-11): FU01 DL-2 `main`'de değilken 0182 `District`'e kod düzeyinde başvurmaz, ilçe basılmaz; ilçe okuması FU01 DL-2 geldikten sonraki ilk 0182 dilimine eklenir (§8.9) | yok | merkez (Beste) |
| MOD-0181 sayım | HARD (okuma); DL-3 XMC | kod var; `scanRef` opak (`services/Diten.InventoryService/src/Diten.InventoryService.Api/CycleCount/CycleCountRequestReader.cs:70`, `:86`); kör sayım bayrağı ve bin talebi var (`CycleCountEntities.cs`) | sayım bağlamı; kör sayım gizliliği (§2.3 kural 4) | X-3, X-14 | merkez |
| MOD-0174 GS1 ayrıştırıcı, lot, seri | HARD | `Gs1CodeParser` ham ve köşeli biçimi okur (01, 21, 17, 10); `POST /api/trace/serials/gs1-check` var (`services/Diten.InventoryService/src/Diten.InventoryService.Api/Controllers/Traceability/TraceController.cs:143-144`) | ayrıştırma (ikinci ayrıştırıcı yazılmaz), lot / seri çözümü, alan karşılaştırması | X-1 (DL-5) | merkez |
| MOD-0175 / MOD-0176 / MOD-0177 | HARD (okuma) | geri çağırma kapısı `LOT_UNDER_RECALL` (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Common/InventoryErrorCodes.cs:80`); 0178 yerleştirme ve rezervasyon aynı kuralı okur | statü, SKT, geri çağırma reddi — kaynak 0178 denetimleriyle aynı (DL-1 ilk ölçümünde adıyla sabitlenir) | yok | merkez |
| MOD-0173-FU02 LOCATION | HARD (okuma) | bin kodu; 0178 okutulan raf kodunu tam kod eşleşmesiyle bulur | lokasyon çözümü, lokasyon etiketi | yok | merkez |
| MOD-0290 PRODUCT-MASTER | HARD (okuma) | SKU `gtin` yalnız FG satış kutusunda dolu; LSKU / kalem null (MOD-0290-FU07 §16) | DL-1 ürün karşılaştırması (FG), SKU adı / kodu | yok | MDM |
| MOD-0290-FU07 GTIN defteri + GS1 önekleri | **HARD — DL-5 kapısı** | paket `ready-for-dev`, onaylı (`execution/approvals/approvals.tsv:82`, 2026-11-11); `identifiers/{gtin}` ve `gs1-company-prefixes` okumaları tanımlı (`execution/domains/master-data-management/module-packs/MOD-0290-FU07-product-identifiers-gtin.md:316`, `:318`); **kodu HEAD'de ve `origin/main`'de yok** (`git grep ProductIdentifier` MdmService: 0) | koli / palet / LSKU GTIN'inden ürün, "Bu nedir?", SSCC öneki, lot / koli etiketindeki GTIN | X-13 | MDM (Ali) |
| MOD-0365 Device / Edge Integration | SOFT (v1'de kullanılmaz) | registry'de planned, paket yok | Q-5 = A (karar 03) ile sahada yazdırma ajanı yok; yalnız seçilecek RFID okuyucu (Q-7, donanım TBD) bir ara yazılım gerektirirse | yok | merkez (S-5) |
| MOD-0040-FU02 RCS-001 | SOFT (hedef üretici) | Inventory bağlantısı DL-8'de; ara üretici MOD-0178 geçici sayacı (`IWarehouseRecordCodeAllocator`) | `DVC`, `LBL` | X-4 | merkez |
| MOD-0018 RBAC + Auth rol şablonu | HARD | `[HasPermission]` + manifest | izinler | rol şablonu ayrı WP (X-11, Q-14) | Ali (Auth) |
| Auth şirket bağlamı | HARD (mobil) | `legal_entities` claim + `X-Legal-Entity-Id` (E-7; teyit Ali) | mobil şirket seçimi | yok (teyit — Q-13) | Ali (Auth) |
| BL-501 merkezi denetim | HARD | InventoryService'te var — `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (`inventory-merkezi-iletim`, yol b) | §21 | yok | korunan yol |
| PDF üretimi | HARD (DL-4, DL-7) | `PDFsharp-MigraDoc` 6.1.1 Inventory API'de var (`services/Diten.InventoryService/src/Diten.InventoryService.Api/Diten.InventoryService.Api.csproj:19`) | etiket PDF'i, kanıt paketi | yok | — |
| Barkod çizim kütüphanesi | HARD (DL-4) | depoda **yok** (csproj taraması: barkod / DataMatrix kütüphanesi 0); **karar: ZXing.Net** (Q-6, karar 03 eki `bea6662d9` — Apache-2.0, ücretsiz, ticari kullanıma açık; r3) | Code 128, GS1-128, GS1 DataMatrix çizimi; AC-49 / AC-57'de PDF'teki barkodun aynı kütüphanenin okuyucusuyla çözülmesi; **r4 (F-2): cihaz eşleştirme QR'ı** (QR_CODE, DL-1 — §8.2) | yeni NuGet (`ZXing.Net`, **DL-1'in ilk adımı** — r4: QR DL-1'de gerektiği için DL-4'ten çekildi; sürüm ve bilinen güvenlik açığı taraması — `dotnet list package --vulnerable` — DL-1 kabul kaydında) | Beste (karar 03) |
| SCAN-BUNDLE | HARD (contract-first) | yok | sözleşme taslağı; mobil sözleşme notu | bu paket (DL-1) | supply-chain-execution (Beste) |
| Mobil ekipler | tüketici | mock veriyle başladılar (E-7); §3A uçları dondurulmuş | SCAN-BUNDLE'ı okur | yok | mobil ekipler |
| Görev Merkezi | bağımlılık DEĞİL (v1) | depo görevleri bağlı değil; bağlantı MVP-5 sonrası | — | — | merkez |
| Gateway | — | `/api/warehouse/{everything}` → 5067 (`gateway/Diten.ApiGateway/ocelot.json:1153-1161`) | §15 | `ocelot.json` değişmez; `Mod0178WarehouseRouteTests` uç sayısı uç ekleyen her dilimde (X-12, r3 — sahip onaylı `ad1100158`) | integration-agent |

## 8. Runtime Constraints

### 8.1 Servis ve kapsam
- Servis `Diten.InventoryService` (port §3 tablosunda 5067), taban `/api/warehouse/barcode` (MOD-0178'in `/api/warehouse` ailesi; Gateway
  rotası zaten kapsar). Web (5001) servise doğrudan değil, same-origin MVC proxy → Gateway (5000) üzerinden gider.
- Her istek: `Authorization: Bearer`, `X-Legal-Entity-Id`, `X-Correlation-Id`; yazmalarda `Idempotency-Key`; değişebilir kayıtta `If-Match`
  (`W/"<version>"`). Mobil okutma ve nabız ayrıca `X-Scan-Device-Id` + `X-Scan-Device-Key` taşır (§8.2).
- InventoryService PUT / DELETE bekçisi (`LedgerImmutabilityArchitectureTests`) değişmez; 0182 yalnız GET / POST yazar.
- Kiracı + şirket her sorguda yüklemdir; başka modülün koleksiyonunda okuma da aynı yüklemle yapılır.
- **Idempotency deposu (r4, F-4):** okutma (`RecordScanCommand`) anahtarını `ScanEvent`'te tutar (§4.2, §4.10); diğer her 0182 yazma komutu —
  cihaz (kayıt, düzenleme, onay / yeniden etkinleştirme, engelleme, eşleştirme), aşım, PIN, ayar, şablon, basım, RFID taraması, dışa aktarım —
  anahtarını `wms_barcode_command_requests`'te (`BarcodeCommandRequest`, §4.11) tutar; koleksiyon `BarcodeCollections.All`'dadır (X-5). Kayıt
  komutun yazımıyla **aynı işlemde** eklenir; başarısız komut kayıt bırakmaz. Cihaz nabzı idempotency kaydı yazmaz (adlı muafiyet — yazımı
  doğası gereği tekrarlanabilir `$set`'tir; `Idempotency-Key` gelirse yok sayılır, §8.2, §12). **Replay araması sırası:** izin (403) → cihaz
  kimliği (cihaz başlığı isteyen uçlarda, 403) → biçim (400) → **idempotency araması** (aynı kullanıcı + şirket + `RequestOp` + anahtar; aynı
  parmak izi → ilk cevabın durum kodu ve sırsız gövdesi, yeni yazım / merkezi kayıt / sayaç değişimi yok; farklı parmak izi → 400
  `IDEMPOTENCY_KEY_REUSED`) → ancak sonra varlık (404), `If-Match` (409), durum (409) ve **tekillik** (409, ör. `BARCODE_ALREADY_OVERRIDDEN`)
  denetimleri. Böylece başarılı bir aşımın ya da düzenlemenin replay'i 409 değil ilk cevabı alır.
- **Parmak izi ve sırlar (r4, F-3):** parmak izi MOD-0181 deseniyle (`CycleCountSupport.Fingerprint`, `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCount/Services/CycleCountSupport.cs:132-136`
  — adlı alanlar üzerinde düz SHA-256) hesaplanır; **PIN taşıyan gövdelerde (`OverrideScanCommand`: `supervisorUserId`, `pin`, `note`;
  `SetSupervisorPinCommand`: `pin`) PIN alanı parmak izine katılmaz** — alan listesinde hiç yoktur (yer tutucu da yazılmaz); 4..8 haneli PIN
  düz SHA-256 izinden saniyeler içinde geri bulunabileceği için. Sonuç: aynı anahtarla yalnız PIN'i farklı gövde **replay'dir** (ilk cevap;
  PIN değerlendirilmez, sayaç değişmez) — Web her gönderimde yeni anahtar üretir, mobil yalnız aynı isteği tekrarlarken anahtarı korur.
  Idempotency deposunda (`RequestFingerprint`, `ResponseBody`, başka hiçbir alan) PIN, PIN özeti, tuz ya da PIN'in herhangi bir türevi
  **saklanmaz**. Aynı kural cihaz anahtarı ve eşleştirme kodu için de geçerlidir: saklanan `ResponseBody` sır alanı taşımayan ayrı bir DTO
  tipidir (`deviceKey`, `pairing.code`, `pairing.qrPng` o tipte yoktur — derleme düzeyinde). Eşleştirme kodu 256 bitlik rastgele değer olduğu
  için parmak izine yalnız özeti (`PairingCodeHash` ile aynı değer) olarak girer; PIN'e bu istisna uygulanmaz.

### 8.2 Cihaz kimliği ve yaşam döngüsü
- **Kayıt (S-3):** mobil uygulama ilk açılışta `POST /barcode/devices` (`Name`, `Type`, `Platform`, `InstallationId`, `WarehouseId?`,
  `keyDelivery?`) çağırır; `keyDelivery = Direct` (varsayılan — çağıran telefonun kendisi) iken cevap **bir kez** rastgele 32 baytlık cihaz
  anahtarını döner (base64url). Sunucu yalnız SHA-256 özetini saklar. Aynı `InstallationId` ikinci kez → 409 `BARCODE_DEVICE_ALREADY_REGISTERED`
  (anahtar yenileme yalnız Web'den "yeniden etkinleştir" ile — eski anahtar o anda geçersiz, yenisi QR eşleştirmesiyle; aşağıda).
- **Anahtarın telefona QR ile teslimi (r4, F-2 — sahip kararı, kararlar 03 eki `4168e7a90`):** Web'den kayıt (Web proxy her zaman
  `keyDelivery = Pairing` gönderir) ve yeniden etkinleştirme (`Blocked` → `Active`) **anahtar üretmez ve döndürmez**: `KeyHash` null olur (eski
  anahtar hemen 403) ve tek kullanımlık bir **eşleştirme kodu** üretilir — 32 bayt rastgele (base64url), ömrü **10 dakika** (sabit
  `BarcodeContract.PairingCodeLifetimeMinutes = 10`; ayar değildir). Cevap `pairing: { code, qrPng, expiresAt }` taşır (`qrPng` = ZXing.Net ile
  çizilmiş QR, içerik `diten-scan-pair:1:{code}`; kod QR'ın altında kopyalanabilir metin olarak da gösterilir). Sunucu yalnız
  `PairingCodeHash` (SHA-256) + `PairingExpiresAt` + `PairingIssuedBy` saklar; kod hiçbir kayıtta, logda, OTel'de, merkezi kayıtta ya da
  idempotency deposunda yoktur. Telefon QR'ı okutur ve **`POST /barcode/devices/pair`** `{ pairingCode, installationId }` çağırır; sunucu
  (kiracı + şirket yüklemiyle) kod özetini arar ve **tek belgelik atomik** `findOneAndUpdate` ile (filtre: kiracı + şirket + `PairingCodeHash` +
  `PairingExpiresAt > şimdi` + `InstallationId` = gövde + `Status ≠ Blocked`) yeni 32 baytlık anahtarı üretip `KeyHash`'e yazar,
  `PairingCodeHash` / `PairingExpiresAt`'i null, `PairedAt`'i şimdi yapar; anahtar cevapta **bir kez** döner. Kurallar: kod **tek kullanımlıktır**
  — kullanılmış kod artık bulunmaz (404), eşzamanlı iki kullanımdan tam biri başarılıdır; süresi dolmuş kod 422 `BARCODE_PAIRING_EXPIRED` (Web'de
  yeni kod gerekir); bilinmeyen kod, başka kiracının ya da şirketin kodu 404 (varlık açılmaz); `InstallationId` uyuşmazsa ya da cihaz `Blocked`
  ise 422 `BARCODE_PAIRING_INVALID` (kod geçerli kalır, süresi işler). Yeni kod (yeniden etkinleştirme) önceki kullanılmamış kodu geçersiz kılar
  (aynı alanın üzerine yazılır). Eşleştirme cihaz durumunu değiştirmez: Web'den kaydedilen cihaz `Pending` kalır ve Web onayı yine gerekir (Q-8);
  `Pending` cihaz eşleşebilir, okutması `DEVICE_PENDING` olur. Eşleştirme replay'i (aynı anahtar + gövde) anahtarı **döndürmez**
  (`deviceKeyNotice`, yukarıdaki kural); Web kayıt / yeniden etkinleştirme replay'i kodu döndürmez, gövde `pairingCodeNotice:
  "BARCODE_PAIRING_CODE_NOT_REPLAYED"` taşır, yeni kod üretmez. **Süresi dolan ya da kaybolan kod:** v1'de ayrı "yeni kod" ucu yoktur (en dar
  yol); yeni kod Web'de engelle (gerekçe) → yeniden etkinleştir ile alınır (bilinen sınır, FU-0182-PAIR-REISSUE).
  **İzin ve kimlik (en dar yol):** eşleştirme ucu **çalışanın JWT'si + `[HasPermission(BarcodePermissions.DevicesCreate)]` + kod** ister; cihaz
  başlığı istemez (anahtar henüz yoktur). Gerekçe: `devices.create` telefonun kendi kaydı için depo çalışanında zaten bulunan anahtardır
  (§14 rol önerisi) ve eşleştirme kaydın cihaz tarafındaki yarısıdır; yetki kararı Web'de `devices.create` / `devices.activate` taşıyan
  kullanıcının kodu üretmesiyle verilmiştir, kod bu kararın tek kullanımlık taşıyıcısıdır. `devices.activate` istenseydi çalışan 403 alırdı
  (r3 F-2'nin sorunu); `scans.create` okutma yetkisidir, cihaz yaşam döngüsüne karışmamalıdır; anonim uç (yalnız kod) her eylemde
  `[HasPermission]` kuralına (Çekirdek madde 3) aykırıdır; yeni anahtar (`devices.pair`) güvenlik kazancı olmadan manifest / Rol İzinleri satırı
  açar. Kod kiracı + şirket kapsamında aranır: başka kiracının ya da şirketin kodu 404'tür. Atomik tüketim komutun işleminin içindedir
(idempotency kaydı ve merkezi kayıt aynı işlemde — §8.1, §21).
- **Şirket başına kayıt (r4, F-8):** cihaz kaydı, `InstallationId` tekilliği, anahtar ve Web onayı **şirket (`LegalEntityId`) kapsamındadır.**
  Aynı telefon iki şirkette okutacaksa her şirkette **ayrı kayıt** (ayrı `Number`, ayrı `DeviceId`, ayrı anahtar) ve her şirkette **ayrı Web
  onayı** gerekir; mobil uygulama cihaz kimliği + anahtarı (kiracı, şirket) başına saklar ve şirket değiştirdiğinde o şirketin kimliğini ve
  anahtarını gönderir. Başka şirketin cihaz kimliği / anahtarıyla gelen okutma → 403 `BARCODE_DEVICE_KEY_INVALID` (o şirkette cihaz yok
  sayılır; varlık açılmaz). Şirket seçiminin kendisi Q-13'e bağlıdır (`legal_entities` + `X-Legal-Entity-Id`). SCAN-BUNDLE taslağı (DL-1, AC-1)
  bu kuralı ve QR eşleştirme akışını yazar; mobil sözleşme notu CT işidir (§18).
- **Anahtar ve idempotency replay (r3, F-4 — anahtar saklanmaz ilkesi korunur; r4: anahtarı yalnız `Direct` kayıt ve eşleştirme üretir):**
  `RegisterScanDeviceCommand` (`Direct`) ve `PairScanDeviceCommand` için idempotency kaydı cevabı **anahtarsız** saklar (Web kaydı ve
  yeniden etkinleştirme için **kodsuz** — yukarıda). Aynı
  `Idempotency-Key` + aynı gövde → ilk cevabın durum kodu ve gövdesi döner, **yalnız `deviceKey` alanı yoktur** ve gövde
  `deviceKeyNotice: "BARCODE_DEVICE_KEY_NOT_REPLAYED"` taşır (mesajı 7 dilde: "Anahtar yalnız ilk cevapta verildi; kaybolduysa cihazı Web'den
  yeniden etkinleştirin"). Replay **yeni anahtar üretmez**, `KeyHash`'i değiştirmez, cihaz / merkezi kayıt yazmaz — ilk cevaptaki anahtar
  geçerli kalır. Farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED` (§8.3). Kurtarma (ilk cevap hiç ulaşmadıysa): `Pending` cihaz için Web'de onayla →
  engelle (gerekçe) → yeniden etkinleştir → telefon QR'ı okutup eşleşir (yeni anahtar); `Blocked` → `Active` için yeniden etkinleştirme ve QR
  eşleştirmesi tekrarlanır. Yeni durum geçişi açılmaz (r4: kurtarma yolu artık telefona ulaşır — çalışanın `devices.activate` izni gerekmez). `Pending` → `Active` onayı anahtar üretmediği için replay'i ilk cevabın aynısıdır.
- **Durum makinesi:** `Pending` —(activate, `devices.activate`)→ `Active` —(deactivate + gerekçe, `devices.deactivate`)→ `Blocked`
  —(activate + gerekçe)→ `Active` (eski anahtar geçersiz, yeni anahtar QR eşleştirmesiyle — r4). Q-8 = A (karar 03): yeni kayıt **her zaman** `Pending` doğar, Web'den onaylanır; kendiliğinden
  etkin kayıt yoktur. Geçersiz geçiş 409 `BARCODE_INVALID_STATE`.
- **Okutma kapısı:** başlıktaki cihaz yoksa ya da anahtar özeti uyuşmazsa → **403 `BARCODE_DEVICE_KEY_INVALID`, hiçbir şey yazılmaz.** Cihaz
  geçerli ama `Pending` / `Blocked` ise okutma **olay olarak yazılır**, `Outcome = Rejected`, `ReasonCode = DEVICE_PENDING` / `DEVICE_BLOCKED`
  (iz için — brief §6 "engellenmiş cihazdan gelen okutma reddedilir ve nedeni gösterilir"). Cihazın deposu doluysa ve okutma başka depodaysa
  `Rejected` `DEVICE_WRONG_WAREHOUSE`.
- **Nabız:** `POST /barcode/devices/{id}/heartbeat` (`appVersion`, `batteryPercent?`) ve her okutma `LastSeenAt` / `LastUser*` / `AppVersion`'ı yazar;
  bu alanlar **`Version`'ı artırmaz** (Web'deki düzenleme If-Match'i nabızla bozulmasın). Nabız merkezi kayıt doğurmaz (§21, İ2).
  **r3 (F-10):** nabız `If-Match` **istemez** (mobil sürüm bilmez; gelirse yok sayılır) — §12'deki If-Match kuralının adlı muafiyetidir. Nabız
  yazımı tek belgelik atomik `findOneAndUpdate` (`$set` yalnız nabız alanları; filtre kiracı + şirket + `Id`; `Version` dokunulmaz) ve işlem
  açmaz; eşzamanlı Web düzenlemesi (`Version` koşullu yazım) nabız alanlarına dokunmaz, iki yazım birbirini ezmez. r4 (F-4): nabız
  idempotency kaydı yazmaz ve `Idempotency-Key` istemez (§8.1 adlı muafiyeti; tekrar aynı `$set`'tir).
- **Sağlık (okuma anında hesaplanır, saklanmaz):** `Silent` = `LastSeenAt` + `SilentDeviceHours` < şimdi; `LowBattery` = son pil
  < `LowBatteryPercent` ve rapor `SilentDeviceHours` içinde. Eşikler ayar sayfasından değişir (S-11).

### 8.3 Okutma olayı ve sunucu doğrulaması
- `POST /barcode/scans` gövdesi: `{ sessionId?, warehouseId, source, rawValue, context: { kind, refId?, lineRef?, step }, manual?: { reasonCode,
  note? }, capturedAt? }`. Cevap 201: `{ scanId, outcome, reasonCode, overridable, message, parsed, resolved }` — `message` `Accept-Language`'a
  göre 7 dilde (§8.16). Kör sayım bağlamında `expected*` alanı **hiç yoktur** (§8.4).
- **Sıra (r3, F-11 ile netleşti):** (1) cihaz **kimlik** kapısı — cihaz yok ya da anahtar özeti uyuşmuyor → 403 `BARCODE_DEVICE_KEY_INVALID`
  (§8.2); (2) idempotency araması (aynı kullanıcı + şirket + anahtar; aynı gövde → ilk cevap aynen, yeni olay / kayıt yok; farklı gövde → 400
  `IDEMPOTENCY_KEY_REUSED`) — **403 kontrolünden sonra, cihaz durumu (`Pending` / `Blocked` / depo) değerlendirmesinden önce**: cihaz ilk
  istekten sonra engellendiyse replay ilk cevabı döner ve **yeni `DEVICE_BLOCKED` olayı yazmaz**; (3) cihaz durumu (`DEVICE_PENDING` /
  `DEVICE_BLOCKED` / `DEVICE_WRONG_WAREHOUSE` olayları); (4) **oturum sahipliği:** `sessionId` doluysa ve oturumda başka kullanıcının olayı
  varsa → 422 `BARCODE_SESSION_INVALID`, olay yazılmaz (oturum ilk olayını yazan kullanıcınındır); (5) ayrıştırma → çözümleme → bağlam
  karşılaştırması (§8.4) → olay + merkezi kayıt aynı işlemde. Aynı `SessionId`'ye iki kullanıcının eşzamanlı ilk yazımı (dar yarış) DL-3 kapısında
  zararsızdır: kapı yalnız onaylayan kullanıcının olaylarını sayar (§8.7).
- **Ayrıştırma (AI sınırı r3, F-12):** ham biçimde ayrıştırıcı tablosunda olmayan AI taşıyan dizge DL-5'e kadar `UNREADABLE`'dır (bilinen
  sınır; MOD-0174 ayrıştırıcısının bugünkü davranışı); DL-5'te X-1 ile (37), (310n), (240) okunur ve yok sayılır, liste dışı AI'lar `UNREADABLE`
  kalır (FU-0182-GS1AI). MOD-0174 `Gs1CodeParser.TryParse` kullanılır (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Traceability/Handlers/QueryHandlers/CheckGs1CodeHandler.cs:56`
  aynı çağrıyı yapar); 0182 ikinci bir GS1 ayrıştırıcı **yazmaz** (AC-10). GS1 değilse dizge `Plain`'dir: adım `Location` ise bin kodu tam
  eşleşmesi (depo içinde), adım `Lot` ise beklenen SKU içinde lot numarası, `Query` ise sırayla SSCC (DL-5) → bin kodu → lot numarası →
  `UNKNOWN_BARCODE`.
- **Okutma sonucu kodları** (HTTP değil, gövde): `UNREADABLE`, `UNKNOWN_BARCODE`, `WRONG_PRODUCT`, `PRODUCT_NOT_VERIFIABLE`, `WRONG_LOCATION`,
  `DIFFERENT_LOT`, `LOT_EXPIRED`, `LOT_RECALLED`, `LOT_DATA_MISMATCH` (karekoddaki SKT kayıttakiyle uyuşmuyor), `SERIAL_DUPLICATE`,
  `DEVICE_PENDING`, `DEVICE_BLOCKED`, `DEVICE_WRONG_WAREHOUSE`, `CONTEXT_CLOSED` (görev / sayım kapanmış).
- **Ürün karşılaştırması DL-5'ten önce:** beklenen SKU'nun PRODUCT-MASTER `gtin`'i doluysa (FG satış kutusu) okunan (01) ile eşitlik
  (MOD-0174 `gs1-check` kuralı); `gtin` boşsa (LSKU, kalem, koli GTIN'i) `Warning` `PRODUCT_NOT_VERIFIABLE` (aşılamaz, bilgi). DL-5'ten sonra
  çözümleme `identifiers/{gtin}` ile yapılır ve bu uyarı yalnız MDM'de kaydı olmayan GTIN'de kalır (§8.8).
- **Son kullanma ve geri çağırma:** lotun kayıttaki son kullanma tarihi MOD-0176'nın gün sınırı kuralına göre geçmişse `LOT_EXPIRED`; lot geri
  çağırmadaysa `LOT_RECALLED` — **ikisi de hiçbir zaman aşılamaz** (S-7), `Overridable = false`.
- **Hız:** p95 ≤ 500 ms (yerel ölçüm, AC-29). Erişilemeyen bağımlılık (Mongo, LOCATION, PRODUCT-MASTER) → 503 `BARCODE_INVENTORY_UNAVAILABLE`,
  olay yazılmaz.

### 8.4 Bağlam kuralları
| Bağlam | Adımlar | Beklenen (kaynak) | Karşılaştırma ve sonuç |
|---|---|---|---|
| `Putaway` (MOD-0178 görevi) | `Product` → `Location` | görevin SKU + lotu, önerilen hedef bin | ürün / lot: §8.3; görevin lotundan farklı lot → **`Rejected`** `DIFFERENT_LOT`, **aşılamaz** (Q-3 = A: v1'de farklı lot aşımı yok; sahip teyidi karar 03 eki `7f584f546` — yerleştirmede de; S-11b); hedef bin ≠ okunan → `Warning` `WRONG_LOCATION`, **aşılabilir** — v1'in aşılabilir tek durumu (0178 sapma gerekçesi yolu var); kilitli / karantina rolündeki bin 0178 kuralıyla `Rejected`. **r4 (CT kararı, mobil ekip sorusu 2026-11-13) — önerisi olmayan görev:** görevde planlı / önerilen hedef bin yoksa (`ToLocationId` null — 0178'in sapma kuralı yalnız plan varken gerekçe ister, `ConfirmPutawayTaskHandler.cs:64`) okunan bin 0178'in hedef kurallarından geçiyorsa — aynı depo, etkin, stok tutabilen (bin / saklama yeri), kaynakla aynı olmayan, mal kabul / sevkiyat hazırlık (`Receiving` / `Staging`) rolünde olmayan (`PutawayChecks.TargetAsync`, `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/PutawayTasks/Services/PutawayChecks.cs:51-70`), saklama koşulu uyumlu (`StorageFitAsync`, `:78`) ve 0178'in kilit / karantina kuralına takılmayan — `Accepted`; geçmiyorsa 0178 kuralıyla **`Rejected`** (neden `WRONG_LOCATION`, o bin'in reddi **aşılamaz**, `overridable = false`). **`WRONG_LOCATION` `Warning` (aşılabilir) yalnız öneri varken doğar**; öneri varken okunan bin ≠ öneri ve okunan bin 0178 kurallarından geçmiyorsa sonuç aşılamaz `Rejected`'dır (aşım 0178'in reddedeceği bir hedefe izin veremez). Bin kuralı 0182'de yeniden yazılmaz; 0178'in denetimleri salt okuma olarak çağrılır (yazım yok) |
| `Pick` (MOD-0178 görevi) | `Location` → `Product` (→ `Serial`) | tahsisin kaynak bin'i ve lotu | bin ≠ → **`Rejected`** `WRONG_LOCATION`; lot ≠ → **`Rejected`** `DIFFERENT_LOT` (r3, F-7: aşılamayan uyuşmazlık `Rejected`'dır — `Warning` yalnız aşılabilir ya da bilgi niteliğinde sonuçtadır). **İkisi de aşılamaz — geçerli PIN'le bile** (Q-3 = A, karar 03; S-7 toplama için daraltıldı): 0178 toplama onayı kaynak / lot almaz; cevap `overridable = false`, kullanıcıya "Sorumluya başvurun — tahsis değişmeli" (7 dil, §8.16); lot / bin değişimi FU-0178-LOTSWAP |
| `Count` (MOD-0181 sayımı) | `Location` → `Product` | sayımın bin'leri ve satır anahtarları | bin sayım kapsamında değil → `Rejected` `WRONG_LOCATION` (aşılamaz); satırda olmayan SKU / lot → `Accepted` + `foundStock: true` (istemci 0181 "bulunan stok" yoluna gider); **kör sayımda beklenen miktar ve bin stoğu cevapta yok** |
| `Pack` (MOD-0178 outbound) | `Product` (→ `Package`) | outbound'un toplanmış tahsisleri | outbound'da olmayan SKU / lot → `Rejected` `WRONG_PRODUCT` / `DIFFERENT_LOT` (aşılamaz); koli bağlaması v1'de yalnız kayıt (0178 koli uçları `scanRef` almaz) |
| `Query` ("Bu nedir?") | tek okutma | — | çözülen nesnenin özeti; lot için SKT, geri çağırma uyarısı, depodaki statü kovaları (kullanılabilir / karantina / bloke) ve miktarlar; **açık kör sayımın bin'inde miktarlar gizlenir** (`quantitiesHidden: true`); **r3 (F-5):** lot / SKU / GTIN düzeyindeki depo toplamına açık kör sayım bin'inin bakiyesi katılıyorsa o toplamın miktarı bütünüyle gizlenir (`hiddenReason: BLIND_COUNT_OPEN`; bin dışlanıp kalan gösterilmez — §2.3 kural 4) |

**Ürün kimliği olmayan GS1 dizgesi (r4, F-7):** `Product` adımında okunan GS1 dizgesinde (01) yoksa — tipik örnek (00) SSCC + (02) içerik GTIN'i +
(37) adet + (10) lot taşıyan lojistik (koli / palet) etiketi; ayrıştırıcı (02)'yi okur ama `Gs1Code` taşımaz (§5.1 X-1) — sonuç her bağlamda
**`Rejected` `PRODUCT_NOT_VERIFIABLE`**, `overridable = false`; mesaj "Bu bir koli / palet etiketi — ürünün kendi barkodunu okutun" (7 dil).
**Asla `Accepted` değildir:** dizgedeki (10) lot beklenen lotla aynı olsa bile ürün doğrulanmış sayılmaz (lot numarası SKU'lar arasında tekil
değildir). Aynı kod iki sonuçla gelebilir ve sonucu `Outcome` belirler: `Warning` `PRODUCT_NOT_VERIFIABLE` = (01) okundu ama SKU'nun ana veride
GTIN'i yok (bilgi, §8.3); `Rejected` `PRODUCT_NOT_VERIFIABLE` = okutmada ürün kimliği yok. Aynı etiket `Pack` bağlamının `Package` adımında ve
`Query`'de (00) ile koliyi / paleti çözer (§8.8, DL-5). DL-5'ten (X-1) önce: ham biçimde (37) bütün dizgeyi reddettiği için etiket
`UNREADABLE`'dır (`Gs1CodeParser.cs:133-136`); köşeli biçimde bilinmeyen AI yok sayıldığından (`:92`) (10) okunur ve kural yine `Rejected`
`PRODUCT_NOT_VERIFIABLE`'dır. Kural DL-1'in bağlam karşılaştırmasındadır (AC-58 vakası DL-5'te ham biçimle de koşar).

`SERIAL_DUPLICATE`: aynı oturumda (ya da `Query` dışında aynı görev satırında) aynı seri ikinci kez → `Warning` (aşılamaz; ikinci okutma sayılmaz).
Kapanmış görev / sayım → `Rejected` `CONTEXT_CLOSED`. Bağlam kaydı yoksa ya da başka şirketinse → **404** (olay yazılmaz).

### 8.5 Elle giriş ve sorumlu PIN aşımı
- **Elle giriş (S-10):** `source = Manual` iken `manual.reasonCode` zorunlu, sabit liste: `ScannerFault` (okuyucu arızalı), `DamagedBarcode`
  (barkod hasarlı), `NoLabel` (etiket yok), `NoCameraPermission` (kamera izni yok), `Other` (açıklama 10..500 zorunlu). Olay "elle girildi"
  olarak işaretlenir ve listede rozetle görünür; doğrulama aynıdır.
- **Aşım (S-7, Q-3 = A ile daraltılmış — karar 03):** S-7 farklı lot ve yanlış rafı aşılabilir sayar; Q-3 = A toplamada ikisini de kapatır ve
  farklı lot aşımını tahsis değişimi (FU-0178-LOTSWAP) gelene kadar hiçbir bağlamda açmaz. **v1'de aşılabilir tek durum: `Putaway` bağlamında
  `WRONG_LOCATION`** (§8.4). `Pick`'te `WRONG_LOCATION` / `DIFFERENT_LOT`, her bağlamda `DIFFERENT_LOT`, `LOT_EXPIRED` ve `LOT_RECALLED` geçerli
  PIN'le bile 422 `BARCODE_OVERRIDE_NOT_ALLOWED` alır. `POST /barcode/scans/{scanId}/override`
  gövdesi `{ supervisorUserId, pin, note? }`; cihaz başlıkları zorunlu. **İzin (r3, F-3): `[HasPermission(BarcodePermissions.ScansCreate)]`
  — `warehouse.barcode.scans.create`.** Çağrı okutmanın yapıldığı cihazdan, **çalışanın JWT'siyle** gelir (cihaz sorumluya verilir, sorumlu PIN'ini
  girer — mobil brief `:75`); sorumlunun yetkisi JWT'den değil **PIN kaydından** gelir (PIN yalnız `scan-overrides.approve` taşıyan kullanıcı
  tarafından belirlenebilir — aşağıda). Gerekçe: `scan-overrides.approve` istenseydi çalışan cihazından her aşım 403 alırdı; ayrı bir anahtar
  (`scan-overrides.create`) yeni manifest / Rol İzinleri satırı açar, güvenlik kazancı yoktur (yetki kararı zaten PIN'dedir). Ek kural: olay
  çağıran kullanıcınındır (`ScanEvent.UserId` = JWT kullanıcısı); değilse **404** (başkasının okutmasının varlığı açılmaz). Kurallar: sorumlu ≠ okutan kullanıcı (422 `BARCODE_OVERRIDE_SELF`);
  sorumlunun geçerli PIN'i var (yoksa / süresi dolduysa 422 `BARCODE_PIN_NOT_SET`); yanlış PIN 422 `BARCODE_PIN_INVALID` ve deneme sayacı
  artar; `PinMaxAttempts`'e ulaşınca `PinLockMinutes` kilit, 423 `BARCODE_PIN_LOCKED`; aşılamaz neden 422 `BARCODE_OVERRIDE_NOT_ALLOWED`
  (geçerli PIN'le bile); olay başına tek aşım (ikinci 409 `BARCODE_ALREADY_OVERRIDDEN`). Başarılı aşım `ScanOverride` + merkezi kayıt;
  olayın görünen sonucu "Aşıldı — {sorumlu adı}".
- **Aşım denetim sırası (r4, F-5 — bağlayıcı):** (1) izin `scans.create` (403) ve cihaz kimliği (403 `BARCODE_DEVICE_KEY_INVALID`);
  (2) biçim (400); (3) **idempotency replay araması** (§8.1 — tekillikten önce: başarılı bir aşımın replay'i 409 değil ilk cevabı alır; PIN
  değerlendirilmez, sayaç ve iz yazılmaz); (4) **olay sahipliği / varlığı** — olay yok, başka kiracı / şirket ya da başka kullanıcının → 404;
  (5) **aşılamaz neden** → 422 `BARCODE_OVERRIDE_NOT_ALLOWED`; (6) **öz-aşım** (sorumlu = okutan) → 422 `BARCODE_OVERRIDE_SELF`; (7) olay zaten
  aşılmış → 409 `BARCODE_ALREADY_OVERRIDDEN`; (8) sorumlunun geçerli PIN'i yok / süresi dolmuş → 422 `BARCODE_PIN_NOT_SET`; (9) **kilit**
  (`LockedUntil` > şimdi) → 423 `BARCODE_PIN_LOCKED`; (10) **PIN doğrulaması** → yanlışsa sayaç (aşağıda) ve 422 `BARCODE_PIN_INVALID` (sınıra
  ulaşınca kilit kurulur, o deneme 423); doğruysa (11) `ScanOverride` + idempotency kaydı + merkezi kayıt tek işlemde (tekil index son
  bekçidir — eşzamanlı ikinci aşım 409). **Gerekçe:** PIN gerektirmeyen her denetim PIN'den önce gelir; böylece PIN denemesi (ve sayaç
  artışı) yalnız çağıranın kendi, aşılabilir, aşılmamış olayında mümkündür — başkasının olayı, aşılamaz neden ya da aşılmış olay üzerinden PIN
  denenemez, sorumlu kilitlenemez.
- **Yanlış PIN ve kilit izi (r4, F-5):** (10)'daki her yanlış PIN ve (9)'daki her kilitliyken deneme `ScanOverrideAttempt` olarak **yerel iz**
  bırakır (`wms_barcode_override_attempts`, §4.3; `Result` = `PinInvalid` \| `LockSet` \| `Locked`; çağıran kullanıcı, cihaz, olay, sorumlu,
  zaman; PIN ve türevi yok). İz, sayaç yazımı gibi **aşım işleminin dışında** tek belgelik ekleme ile yazılır (422 / 423 cevabına rağmen kalır);
  yazılamazsa cevap 503 `BARCODE_INVENTORY_UNAVAILABLE` olur (iz bırakmadan deneme yok). **Merkezi kayıt doğurmaz:** başarısız komut BL-501'de
  0 kayıttır (§8.12) ve aşım denemesi komut değildir; kilit olayının merkezi denetime taşınması Q-16 ile birlikte CT kararıdır
  (FU-0182-PIN-AUDIT). **Görünürlük:** `/Warehouse/ScanEvents` satır ayrıntısında olayın aşım denemeleri (zaman, deneyen, sorumlu, sonuç);
  `/Warehouse/ScanDevices` listesinde son `SilentDeviceHours` içinde kilit kurulmuşsa "PIN kilidi" rozeti (ikon + metin) ve cihaz ayrıntısında
  son denemeler — veri mevcut `GET /barcode/scan-events/{scanId}` ve `GET /barcode/devices[/{deviceId}]` cevaplarına eklenir (yeni uç yok;
  X-12 sayısı değişmez). Görünürlük `scan-events.read` / `devices.read` ile; sorumlunun kendi `/Warehouse/SupervisorPin` sayfasında son
  kilit zamanı gösterilir.
- **PIN deneme sayacı (r3, F-10):** kilit denetimi (`LockedUntil` > şimdi → 423) PIN doğrulamasından önce (r4: tam sıra yukarıda). Yanlış PIN'de sayaç **aşım işleminin
  dışında**, tek belgelik atomik yazımla artar: `findOneAndUpdate` (filtre kiracı + şirket + `UserId`, `$inc: { FailedAttempts: 1 }`, yeni değer
  döner); dönen değer ≥ `PinMaxAttempts` ise aynı belgeye koşullu ikinci atomik yazım (`FailedAttempts >= PinMaxAttempts` filtresiyle
  `LockedUntil = şimdi + PinLockMinutes`, `FailedAttempts = 0`). Eşzamanlı N yanlış deneme sayacı tam N artırır (oku-yaz yok, kayıp artış yok);
  aşım komutu 422 ile başarısız olsa da sayaç kalır (işlem geri alınması sayacı geri almaz); doğru PIN sayacı atomik sıfırlar (`$set:
  { FailedAttempts: 0 }`). Bu yazımlar `Version`'ı artırmaz, `If-Match` istemez (§12 muafiyeti) ve merkezi kayıt doğurmaz (başarısız komut 0;
  sayaç komut değildir). Sayaç ve aşım denemesi izi işlem açmaz — X-6'nın işlem çağrı yeri sayısına girmez (r4).
- **PIN (Q-4 = A, karar 03 — 90 gün geçerli, 3 yanlışta 5 dk kilit; §4.4 varsayılanları):** kullanıcı kendi PIN'ini Web'de `/Warehouse/SupervisorPin` sayfasından belirler; ucu çağırmak için JWT'sinde
  `warehouse.barcode.scan-overrides.approve` olmalıdır. Yetki PIN belirlenirken ölçülür; sonradan geri alınan yetki PIN süresi
  (`PinValidityDays`) dolana kadar geçerli kalır — **bilinen sınır**: canlı yetki denetimi AuthService'ten başka kullanıcının izinlerini okumayı
  ister (FU-0182-PIN-LIVE). Yönetici kullanıcının PIN'ini sıfırlayamaz (yalnız kendisi).

### 8.6 Çevrimiçi kuralı
- **S-1: v1 yalnız çevrimiçi.** Sunucu yalnız `ReceivedAt`'i geçerli zaman sayar; `capturedAt` saklanır, karar vermez. Yerel kuyruk, toplu
  yeniden oynatma ucu ve "çevrimdışı bekleyen" KPI'ı **yoktur** (mockup v2'de "Bağlantı yok" uyarısı).
- Sözleşme yeniden oynatmaya hazırdır: idempotency anahtarı okutma başınadır; FU-0182-OFFLINE geldiğinde toplu uç ve `capturedAt` kuralları
  eklemeli gelir (W-0182-02).

### 8.7 WMS işlemlerine bağlama
- **Bugün:** 0178 yerleştirme / toplama onayı ve 0181 sayım girişi opak `scanRef` (≤ 200) alır, doğrulamaz (MOD-0178 `:360`, MOD-0181 `:469`,
  AC-46). MOD-0182 bu kapıyı doldurur: `scanRef` = `scan-session:{SessionId}`.
- **Bağ (r4, CT kararı):** okutma ile 0178 / 0181 onayı arasındaki bağ **`scanRef` = `scan-session:{SessionId}`** üzerinden kurulur; `SessionId`
  okutmalarda istemcinin gönderdiği `sessionId`'dir (§4.2). Başka bağ (görev kimliği ile olay eşlemesi, olay listesi) yoktur.
- **DL-3 (XMC X-2, X-3):** depo `ScanRequiredWarehouseIds`'te ise (Q-9 = A, karar 03: depo başına, varsayılan kapalı) **ya da** (r4) onay
  `scan-session:` önekli bir `scanRef` taşıyorsa (depo ayarından bağımsız — aşılmış okutmanın onaya bağlanabilmesi için; bugün depoda bu önek
  kullanılmıyor, 0178 testleri `"SCAN-0001"` gibi öneksiz değer gönderir — DL-3 ilk ölçümünde teyit), 0178 / 0181 onay işlemi **kendi işleminin içinde**
  `IScanSessionGate.ConsumeAsync(sessionId, target, user)` çağırır. Kapı şunları ister: oturumda **onaylayan kullanıcının** olayları aynı kiracı +
  şirket + bağlam (`refId`, `lineRef`) — başka kullanıcının olayı (§8.3 sıra 4'ün dar yarış artığı) ne doğrular ne geçersiz kılar, sayılmaz
  (r3, F-11); onaylayanın oturumda olayı yoksa 422 `BARCODE_SESSION_INVALID`; gerekli adımların her biri için son olay `Accepted` ya da aşılmış (`Putaway`: `Product` + `Location`; `Pick`:
  `Location` + `Product`; `Count`: `Location` + `Product`); son olay `ScanSessionMaxMinutes` içinde; oturum harcanmamış. Uygunsa
  `ScanSessionBinding` aynı işlemde yazılır (tekil `SessionId` — eşzamanlı iki onaydan biri kazanır). Uymazsa 422 `BARCODE_SESSION_REQUIRED` /
  `BARCODE_SESSION_INVALID` / `BARCODE_SESSION_CONSUMED`; onay yazılmaz.
- **Putaway'de aşılmış `WRONG_LOCATION` → sapma gerekçesini sunucu yazar (r4, CT kararı):** oturumun `Location` adımındaki son olay aşılmış
  `WRONG_LOCATION` ise ve onayın `toLocationId`'si o olayda okunan bin ise, 0178 onay işleyicisi kapıdan dönen aşım bilgisiyle 0178'in mevcut
  `deviationReason` alanını **sunucu tarafında** doldurur: "Sorumlu onayı — {sorumlu adı}, {ApprovedAt, UTC}" (onaylayan isteğin dilinde,
  `Accept-Language`, 7 dil; 0178'in 10..1000 kuralını karşılar). **Kullanıcıdan ayrıca gerekçe istenmez** — mobil gerekçe alanı göstermez;
  istemci `deviationReason` gönderirse yok sayılır ve sunucu metni yazılır. 0178'in "plan ≠ hedef ise gerekçe" denetimi
  (`ConfirmPutawayTaskHandler.cs:64`) kapı çözümlemesinden sonra çalışır (X-2'nin parçası). Aşım yoksa ve hedef plandan farklıysa 0178'in
  bugünkü kuralı (kullanıcı gerekçesi, yoksa 400 `REASON_REQUIRED`) aynen geçerlidir.
- Depo listede değilse **ve** `scanRef` `scan-session:` önekli değilse 0178 / 0181 davranışı **bugünkü gibidir** (opak `scanRef`); 0178 / 0181
  testleri değişmez (AC-41).
- Seri kontrollü SKU'da kısmi yerleştirme (FU-0178-SERIAL-01) bu pakette yoktur; MOD-0178 paketinin FU'su olarak kalır.

### 8.8 GTIN çözümleme ve SSCC
- **Kapı:** MOD-0290-FU07 DL-1 `main`'de ve `GET /product-master/identifiers/{gtin}` + `GET /product-master/gs1-company-prefixes` canlıda
  ölçülmeden DL-5 başlamaz (FU07 §16, AK-7).
- **Çözümleme:** (01) GTIN-14 → `identifiers/{gtin}` → `{targetKind, targetId, skuLevel, packagingLevel, status}`; bilinmeyen / geri çekilmiş →
  `UNKNOWN_BARCODE`; emekli GTIN'in hedefi yine çözülür ama `Warning` `PRODUCT_RETIRED_IDENTIFIER` (bilgi). FU07 erişilemezse 503
  `BARCODE_GTIN_RESOLUTION_UNAVAILABLE` — **sessiz kabul yok**.
- **SSCC (S-3 FU07: SSCC depoda üretilir, MDM yalnız öneki verir):** `ExtensionDigit` + `CompanyPrefix` + seri referansı (toplam 17 hane) +
  GS1 mod-10 kontrol hanesi = 18 hane. Seri referansı `SsccCounter`'dan atomik; kapasite (`10^(16 − önek uzunluğu)`) dolarsa 422
  `SSCC_CAPACITY_EXHAUSTED`. SSCC RCS-001 biçiminde değildir — GS1 tanımlayıcısıdır (FU07 §3'teki GTIN gerekçesiyle aynı). Önek ayarda yoksa
  ya da FU07 etkin listesinde değilse 422 `SSCC_PREFIX_NOT_SET`.
- **SSCC kapsamı (Q-10 = A, karar 03):** MOD-0178 kolisine (`HandlingUnit`) bir SSCC (kolide tekil — yeniden basım aynı SSCC'yi kullanır) ve içeriksiz
  "serbest palet" SSCC'si (mal kabul / paletleme için). Koli → palet birleştirmesi MOD-0246'nındır.
- (00) okutması (DL-5, X-1) koli / paleti çözer; `Pack` ve `Query` bağlamında kullanılır.

### 8.9 Etiket şablonu ve basımı
| Etiket türü | Barkod | İnsan okunur alanlar (izinli küme) | Kaynak | Dilim |
|---|---|---|---|---|
| `Location` (raf) | Code 128 — bin kodu (0178'in tam kod eşleşmesiyle aynı dizge) | bin kodu, depo / bölge yolu, rol | LOCATION bin'leri (tek tek ya da bölge altındaki hepsi, ≤ 500) | DL-4 |
| `LotCase` (lot / koli) | GS1 DataMatrix + GS1-128: (01) GTIN + (17) SKT + (10) lot | ürün adı, SKU kodu, GTIN, lot, SKT | `{skuId, lotId}` + ambalaj düzeyi (GTIN FU07'den) | DL-5 |
| `Sscc` (koli / palet) | GS1-128: (00) SSCC | SSCC, gönderen şirket, koli no (PKG), outbound no (OUT), alıcı **şehri + ilçesi**, posta kodu yalnız sevkiyat kopyasında varsa (Q-11 — r3 güncellemesi: MOD-0178-FU01 kararları 01 eki Q-10, `a7c1d32b8`; **r4 (F-6) hizalaması — aynı kaydın "Q-10 düzeltmesi" eki, `cb612efc9`:** CRM cevabı ilk günden adres listesidir (`addresses[]` + `defaultAddressId`, ilk sürümde `addressId = "primary"`); posta kodu biçimi ilk günden vardır — ek teslimat adreslerinde dolu gelir, account'un ana adresine alanı **CRM ekliyor** (tarih CRM'de) ve eklenene kadar ana adreste **boş gelir**; sözleşme biçimi değişmez; WMS `customerRef` + `customerAddressId` + adres kopyası tutar; elle girişte posta kodu isteğe bağlıdır; r2'nin "şehir ve posta kodu" yazımı bunun yerine geçer). Kaynak: MOD-0178 outbound `ShipTo` kopyası — `City`, `District` (MOD-0178-FU01 DL-2'nin yeni alanı, FU01 paketi `:171`), `PostalCode` (sevkiyat kopyasında doluysa basılır, boşsa basılmaz; CRM'den dolu gelmeye başladığında kural değişmez). **İlçe kaynağının zamanlaması (r4, F-6):** DL-5 FU01'i **beklemez**. DL-5 başladığında FU01 DL-2 `main`'de değilse 0182 `District`'e kod düzeyinde başvurmaz ve ilçe satırı basılmaz (yer tutucu yok); ilçe okuması FU01 DL-2 `main`'e geldikten sonraki **ilk 0182 dilimine** (DL-5 sürüyorsa DL-5, değilse DL-6 ya da DL-7; hiçbiri kalmadıysa FU-0182-DISTRICT) eklenir ve AC-56'nın ilçe vakası (S-56c) o dilimin kabulünde koşar. FU01 geldikten sonra da elle yazılmış adreste ya da CRM'de ilçesiz adreste `District` boş olabilir — boşsa basılmaz. Kişi / müşteri adı (`ShipTo.Name`) ve açık adres (`Line1`, `Line2`) izinli kümede **yok** | 0178 kolisi ya da serbest palet adedi | DL-5 |

- **Önizleme (E-1):** `POST /barcode/label-previews` seçilen şablon + kaynakla ilk etiketin PNG görüntüsünü ve alan listesini döner; **hiçbir
  şey yazmaz** (iş, SSCC sayacı, merkezi kayıt yok). SSCC önizlemesi örnek "000000000000000000" değil, `(00)` alanını "basımda atanır" diye
  gösterir.
- **Basım:** `POST /barcode/label-prints` işi yazar, etiketleri PDF olarak (etiket boyutunda sayfa) üretir, dosyayı ve sha256'sını saklar,
  `Number` (LBL) döner. **Yazıcıya ulaşma — Q-5 = A (karar 03):** sunucu yazıcıya bağlanmaz; Web PDF'i tarayıcının yazdırma penceresinde,
  mobil Android yazdırma hizmetiyle açar (yazıcı her basımda işletim sistemi penceresinden seçilir). Sunucuda ağ / soket çağrısı yoktur (AC-51).
- **Yeniden basım (S-10):** `isReprint = true` + `reprintOfJobId` + gerekçe sabit listesi: `Damaged` (etiket hasarlı), `Lost` (kayboldu),
  `PrinterError` (yazıcı hatası / eksik basım), `Relabel` (yeniden etiketleme), `Other` (açıklama 10..500). SSCC yeniden basımı **aynı**
  SSCC'yi kullanır; yeni SSCC yalnız yeni basımda.
- Etiket dili işin `Language`'ıdır (varsayılan isteğin dili); GS1 alanları AI numarasıyla da yazılır.

### 8.10 RFID toplu okuma
- **Kapı:** DL-5 (GTIN çözümleme) ve RFID okuyucu donanımı. Q-7 = A (karar 03): RFID **Android telefona bağlı el okuyucuyla** (ör. Bluetooth)
  okunur (E-7 "el terminali alınmayacak"; Q-12: kapı / portal okuyucu yok). **Hangi okuyucu — açık, TBD, sahibi Beste** (mobil ekiple); donanım
  kararı gelene kadar DL-6 başlamaz.
- `POST /barcode/rfid-sweeps` `{ warehouseId, locationId, epcs[] (≤ 5.000), repeatOfSweepId? }`: EPC'ler SGTIN-96 / SSCC-96 olarak çözülür
  (GS1 EPC Tag Data Standard); çözülemeyen `Unknown` listelenir, taramayı düşürmez. Beklenen tarama anında defterden okunur (GTIN / SKU / lot
  başına miktar; seri kontrollü SKU'da MOD-0174 seri listesi). Cevap: beklenen / okunan sayıları ve fark listesi (`Missing`, `Unexpected`,
  `QuantityDiff`). **Stok değişmez** (S-8).
- "Yeniden tara" yeni tarama (`repeatOfSweepId`); fark sürerse "Sayım başlat" istemcide MOD-0181'in `POST /api/warehouse/cycle-counts` ucunu
  o bin kapsamıyla çağırır — yetki 0181'in (`cycle-count.counts.create`), 0182 sayım yazmaz.
- **Kör sayım:** bin açık kör sayımdaysa beklenen yazılmaz ve dönmez; cevap yalnız okunanları verir (`expectedHidden: true`).

### 8.11 Uyum izi ve kanıt paketi
- `GET /barcode/trace?lot=&skuId=` (ya da `serial` + `skuId`, ya da `sscc`): lotun / serinin / SSCC'nin depo yolculuğu — 0182 okutma olayları
  + 0178 görev onayları (kim, ne zaman, `scanRef` var mı) + defter hareketleri; her adımda "okutuldu / elle / okutmasız" ayrımı, cihaz ve kullanıcı.
  Mal kabul ve sevk adımları defterden okunur (0182 o modüllerin ekranı değildir).
- Kanıt paketi: `POST /barcode/trace/exports` PDF ya da CSV, **isteyenin dilinde** (7 dil; MOD-0181 Evidence Pack deseni), insert-only, sha256'lı.
- Okuma sınırı: tek sorgu ≤ 5.000 adım; fazlası 422 `BARCODE_TRACE_TOO_LARGE` (süzgeç daraltılır).
- **Kör sayım (r3, F-5):** bin'i açık kör sayımda olan defter hareketi adımı izde ve kanıt paketinde görünür ama miktarı yazılmaz (`quantity:
  null`, `quantityHidden: true`, `hiddenReason: BLIND_COUNT_OPEN`); izde lot / seri / SSCC toplamı gösteriliyorsa §2.3 kural 4 (a) uygulanır.
  Kural iz derleyicisinde tektir; kanıt paketi aynı derleyiciyi kullanır (AC-62, S-62b).

### 8.12 Merkezi denetim ve yerel iz
- BL-501 InventoryService'te kurulu (yol b, `RequireEnlistment` kapalı-başarısız — MOD-0181 §8.11 ölçümü); her yazma komutu
  `ICentralAuditDeclaration` taşır; `SourceModule` = manifest kodu `BARCODE-RFID`.
- Merkezi kayıt yalnız **başarılı ve yazan** komutta 1; replay ve başarısız 0. Değerlerin yeri 0182'nin kendi kayıtlarıdır (okutma olayı,
  aşım, cihaz alanları, iş).
- **Nabız ve işaret (r3, F-6 ölçümü):** `CentralAuditDeclarationTests.cs:54` her `IRequest` yazma komutunun `ICentralAuditDeclaration` taşımasını
  ister; bu yüzden `RecordDeviceHeartbeatCommand` da işaret taşır ve 17'ye dahildir (r4). Kayıt doğurmaması (İ2) işaretten değil yazımın biçiminden
  gelir: nabız işlem açmaz, olayı enlist etmez — `RequireEnlistment` altında başarılı ama hiçbir işlemi enlist etmemiş komut kayıt bırakmaz.
  **Doğrudan atıf (r4, F-9):** `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditRecorder.cs:160-162`
  (`if (_requireEnlistment && !pending.OutsideTransaction)` → "a success that enlisted nothing wrote nothing … no central record") ve seçeneğin
  tanımı `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditOptions.cs:39-46` (`RequireEnlistment`); r3'ün
  `ICentralAuditDeclaration.cs:32-36` atfı dolaylıdır. Davranış DL-1'in ilk ölçümünde AC-20 / AC-23 ile doğrulanır. `ICentralAuditOutsideTransaction` **taşımaz** (o işaret kaydı işlem dışında yazdırır).
- **Hacim notu (Q-16, CT):** `RecordScanCommand` her okutmada 1 merkezi kayıt doğurur (yol b). Depo başına günde binlerce okutma beklenir
  (ölçülmedi). Alternatif: `ScanEvent` defteri yol c eşdeğer izi (insert-only, kullanıcıya gösterilir) — CT kabulü ister. r1 varsayılanı **b**.
- PIN, PIN özeti, cihaz anahtarı ve anahtar özeti, eşleştirme kodu ve özeti **hiçbir kayda, loga ya da OTel'e** yazılmaz (AC-19, AC-2, AC-3);
  idempotency deposu ve parmak izi için §8.1 (r4, F-3).
- **Yanlış PIN ve kilit (r4, F-5):** merkezi kayıt doğurmaz (başarısız komut 0); yerel iz `ScanOverrideAttempt` (§8.5) kullanıcıya gösterilir.

### 8.13 Kayıt kodu üretimi
- `ScanDevice.Number` = `{ŞİRKET}-DVC-{000001}`; `LabelPrintJob.Number` = `{ŞİRKET}-LBL-{000001}` (RCS-001 biçimi). Ara üretici MOD-0178'in
  geçici sayacı (`IWarehouseRecordCodeAllocator`) — `TypePrefixes`'e ekleme X-4; ortak servise geçiş MOD-0040-FU02 DL-8 ile (FU-0182-RCS).
- Okutma olayı, aşım, şablon, tarama ve dışa aktarım görünen kod **taşımaz** (§22). SSCC GS1 tanımlayıcısıdır (§8.8).
- `DVC` / `LBL` RCS-001 §3'te yoktur (ölçüldü: tabloda CNT, OUT, PCK, PKG, PUT, WAV var) — satırlar korunan yol değişikliğidir (§6 kapısı,
  paket onayından önce; Q-1 = A ile aynı adım).

### 8.14 Ekranlar ve Blueprint soft pages
| Blueprint soft page | Ekran | Sayfa kodu | Veri |
|---|---|---|---|
| Scan Event Monitor | `/Warehouse/ScanEvents` — üst kartlar (bugünkü okutma, ret oranı %, en sık ret nedeni, elle giriş oranı %) + liste: zaman, kullanıcı, cihaz, depo, işlem türü, okunan değer (çözümlenmiş GTIN / lot / SKT / seri / SSCC), sonuç (+ neden, "Aşıldı — sorumlu", "elle" rozeti), bağlı görev (bağlantı); süzgeç depo (zorunlu), tarih (≤ 31 gün), kullanıcı, cihaz, sonuç, işlem türü; satır ayrıntısı yan panelde (r4, F-5: ayrıntıda olayın aşım denemeleri — zaman, deneyen, sorumlu, sonuç: yanlış PIN / kilit kuruldu / kilitliyken deneme) | `SCANEVENTS` (nav) | KPI'lar okuma anında |
| Device Health | `/Warehouse/ScanDevices` — liste: ad / kod, tür, depo, son görülme, son kullanıcı, sürüm, pil, durum + sağlık rozetleri (Sessiz, Düşük pil; r4, F-5: "PIN kilidi" — son `SilentDeviceHours` içinde bu cihazdan kilit kuruldu; ayrıntıda son aşım denemeleri); eylemler Kaydet, Onayla / Etkinleştir, Engelle (gerekçe), düzenle; **Sil yok**. **r4 (F-2):** Web kaydı ve yeniden etkinleştirme sonrası panelde anahtar **yoktur**; eşleştirme QR'ı + kopyalanabilir kod + kalan süre (10 dk) gösterilir, panel kapanınca kod bir daha gösterilmez; süresi dolarsa "Engelle → yeniden etkinleştir ile yeni kod" açıklaması | `SCANDEVICES` (nav) | sağlık okuma anında |
| (ayarlar) | `/Warehouse/BarcodeSettings` — §4.4 alanları; DataTable değil | `BARCODESETTINGS` (nav) | |
| (sorumlu PIN'i) | `/Warehouse/SupervisorPin` — kendi PIN'ini belirle / değiştir; son kullanma tarihi; son kilit zamanı (r4, F-5) | `SUPERVISORPIN` (nav) | |
| (etiket şablonları) | `/Warehouse/LabelTemplates` — liste + form + önizleme | `LABELTEMPLATES` (nav) | DL-4 |
| (etiket basımı) | `/Warehouse/LabelPrints` — basım işleri listesi; "Yeni basım" formu (tür, şablon, kaynak, adet, yeniden basım + gerekçe) + **önizleme** + PDF'i aç | `LABELPRINTS` (nav) | DL-4 |
| Compliance Trace | `/Warehouse/ScanTrace` — lot / seri / SSCC ile ara, zaman çizelgesi, "Kanıt paketi indir" (PDF / CSV) | `SCANTRACE` (nav) | DL-7 |
| (MOD-0365 profil kartı) | yok — Platform'un dış sistem ekranı, MOD-0365'in | — | kapsam dışı |

Durumlar her ekranda: boş, yükleniyor, hata ("Depo servisine ulaşılamadı" + yeniden dene), yetkisiz (UAS-001: menü ve üst çubuk kalır, içerikte
tek açıklama — S-9). Masaüstü öncelikli, 375 px'te okunur, yatay sayfa kaydırması yok.

### 8.15 Hata kodları
| HTTP | Kod | Ne zaman |
|---|---|---|
| 400 | `VALIDATION_FAILED`, `IDEMPOTENCY_KEY_REUSED` | biçim; aynı anahtar farklı gövde; gövdede `tenantId` / `legalEntityId` |
| 403 | `BARCODE_DEVICE_KEY_INVALID`; izin yok | cihaz başlığı / anahtarı; `[HasPermission]` |
| 404 | — | kayıt ya da bağlam yok / başka kiracı / şirket; r4: bilinmeyen, kullanılmış ya da başka kiracının / şirketin eşleştirme kodu (§8.2) |
| 409 | `BARCODE_CONFLICT`, `BARCODE_INVALID_STATE`, `BARCODE_DEVICE_ALREADY_REGISTERED`, `BARCODE_ALREADY_OVERRIDDEN`, `LABEL_TEMPLATE_NAME_TAKEN` | eski `If-Match`; geçersiz geçiş; tekillik |
| 422 | `BARCODE_OVERRIDE_NOT_ALLOWED`, `BARCODE_OVERRIDE_SELF`, `BARCODE_PIN_NOT_SET`, `BARCODE_PIN_INVALID`, `LABEL_SOURCE_INVALID`, `LABEL_REPRINT_REASON_REQUIRED`, `SSCC_PREFIX_NOT_SET`, `SSCC_CAPACITY_EXHAUSTED`, `BARCODE_TRACE_TOO_LARGE`, DL-3: `BARCODE_SESSION_REQUIRED`, `BARCODE_SESSION_INVALID`, `BARCODE_SESSION_CONSUMED`; DL-1 (r3, F-11): `BARCODE_SESSION_INVALID` okutmada da — başka kullanıcının oturumu; DL-1 (r4, F-2): `BARCODE_PAIRING_EXPIRED` (süresi dolmuş eşleştirme kodu), `BARCODE_PAIRING_INVALID` (`InstallationId` uyuşmuyor ya da cihaz `Blocked`) | iş kuralı |
| 423 | `BARCODE_PIN_LOCKED` | deneme sınırı; **r4 (CT kararı):** gövde `details.lockedUntil` (UTC, ISO-8601) taşır — istemci kalan süreyi buradan gösterir; deneme sayısı / kalan hak gövdede **yoktur** (422 `BARCODE_PIN_INVALID` gövdesinde de yok — PIN zorlamaya ipucu verilmez) |
| 503 | `BARCODE_INVENTORY_UNAVAILABLE`, `BARCODE_GTIN_RESOLUTION_UNAVAILABLE` | bağımlılık erişilemez |

Okutma **sonucu** kodları HTTP değildir (§8.3). Kullanıcı metni kodun 7 dildeki karşılığıdır; istemcide sabit metin yok. Gövde bildirimleri
(HTTP hatası değil — r3): `deviceKeyNotice: BARCODE_DEVICE_KEY_NOT_REPLAYED` (§8.2, F-4), `hiddenReason: BLIND_COUNT_OPEN` (§2.3, F-5); r4 (F-2):
`pairingCodeNotice: BARCODE_PAIRING_CODE_NOT_REPLAYED` (§8.2). Okutma sonucu `PRODUCT_NOT_VERIFIABLE` hem `Warning` hem `Rejected` olabilir — §8.4 (r4, F-7).

### 8.16 Mobil sözleşme ve mobil ekip sınırı
- Mobil ekipler sözleşmeyi SCAN-BUNDLE taslağından okur; paket onaylanınca CT **sözleşme notu** yazar (E-3). Mobil şimdilik mock veriyle
  ilerler (E-7); sözleşmeler MVP-1 hattından okunur, main'e hat PR'ıyla gelir.
- Okutma metinleri (`message`) sunucuda 7 dilde üretilir (`Accept-Language`); istemci kod tablosunu kendi çevirisiyle de gösterebilir
  (çeviriler merkezden — E-7).
- §3A uçları (WMS-BUNDLE 1.1.0, WMS-COUNT-BUNDLE 1.0.0) değişmez; DL-3 eklemeli hata kodları yeni sürümde (X-14).
- v1 platform: Android kamera ile okutma (`CAMERA` izni, uygulama içi sürekli okutma); iOS yalnız kamera ile "Bu nedir?" (E-7). Sunucu
  platforma göre davranış değiştirmez; `Platform` yalnız kayıt bilgisidir.
- Tam ekran geri bildirim (S-12: kabul 1 sn'de kendiliğinden, ret dokunuşla kapanır) ve ses / titreşim mobilin işidir; sunucu yalnız
  `outcome` + `reasonCode` + `overridable` verir. `overridable = false` iken mobil PIN düğmesi göstermez (S-7; toplamada hiç gösterilmez — Q-3 = A).
- **SCAN-BUNDLE'da açıkça yazılacaklar (r4, CT kararı — mobil ekip soruları 2026-11-13):** (a) farklı lot sonucu `reasonCode = DIFFERENT_LOT`,
  `outcome = Rejected`, `overridable = false` — her bağlamda; (b) önerisi olmayan yerleştirme görevinde okunan bin 0178 kurallarından geçerse
  `Accepted`, geçmezse aşılamaz `Rejected` `WRONG_LOCATION`; aşılabilir `Warning` `WRONG_LOCATION` yalnız öneri varken (§8.4); (c) yerleştirmede
  PIN aşımından sonra 0178 onayında `deviationReason`'ı **sunucu** "Sorumlu onayı" olarak yazar, istemci ayrıca gerekçe istemez (§8.7);
  (d) okutma ile onay arasındaki bağ `scanRef = scan-session:{sessionId}`'dir (§8.7); (e) 423 `BARCODE_PIN_LOCKED` gövdesi
  `details.lockedUntil` taşır, kalan hak sayısı hiçbir cevapta yoktur (§8.15).

### 8.17 AI Assist
Blueprint AI Tier `Assist` (Summarize; Extract; Recommend; Route). v1'de **yok** (FU-0182-AI); AI kapıları (Prompt Registry, HITL, …) yok.

## 9. Layout & Shell Contract
- `shell: tenant` → `Views/Warehouse/{ScanEvents,ScanDevices,BarcodeSettings,SupervisorPin,LabelTemplates,LabelPrints,ScanTrace}/` altındaki
  **her sayfa görünümünde** (partial `_*.cshtml` hariç) `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır; `_ViewStart.cshtml` varsayılanı
  değişmez (AC-31).
- View klasörü `Views/Warehouse/{Module}/` (MOD-0178 / 0180 / 0181 emsali); rota `/Warehouse/{Module}`.
- Yetkisiz kullanıcıya sayfa iskeleti çizilmez, yönlendirme yapılmaz; sol menü ve üst çubuk kalır, içerikte tek açıklama (UAS-001, S-9).

## 10. Backend File Convention
Golden Reference Slim birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`;
pack `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`):

```text
Features/ScanDevices/
├── Commands/        RegisterScanDeviceCommand.cs, UpdateScanDeviceCommand.cs, ActivateScanDeviceCommand.cs,
│                    DeactivateScanDeviceCommand.cs, RecordDeviceHeartbeatCommand.cs
├── Queries/         GetScanDeviceListQuery.cs, GetScanDeviceByIdQuery.cs
├── Handlers/
│   ├── CommandHandlers/   RegisterScanDeviceHandler.cs … RecordDeviceHeartbeatHandler.cs   (sealed class, suffix YOK)
│   └── QueryHandlers/     GetScanDeviceListHandler.cs, GetScanDeviceByIdHandler.cs
├── Validators/      RegisterScanDeviceValidator.cs, UpdateScanDeviceValidator.cs, DeactivateScanDeviceValidator.cs,
│                    RecordDeviceHeartbeatValidator.cs
└── ScanDeviceModels.cs    (tek dosyada bütün DTO'lar)
Features/BarcodeScans/     aynı yapı: RecordScanCommand, OverrideScanCommand, SetSupervisorPinCommand; GetScanEventList / ById /
                           Dashboard sorguları; RecordScanValidator, OverrideScanValidator, SetSupervisorPinValidator; BarcodeScanModels.cs
Features/BarcodeSettings/  UpdateBarcodeSettingsCommand; GetBarcodeSettingsQuery; UpdateBarcodeSettingsValidator; BarcodeSettingsModels.cs
Features/LabelTemplates/   Create / Update / Activate / DeactivateLabelTemplateCommand; GetLabelTemplateList / ById; Validators; LabelTemplateModels.cs
Features/LabelPrints/      CreateLabelPrintJobCommand; GetLabelPrintJobList / ById / File, PreviewLabelQuery; Validators; LabelPrintModels.cs
Features/RfidSweeps/       RecordRfidSweepCommand; GetRfidSweepByIdQuery; RecordRfidSweepValidator; RfidSweepModels.cs
Features/ScanTrace/        CreateScanTraceExportCommand; GetScanTrace / ExportList / ExportFile sorguları; Validators; ScanTraceModels.cs
Features/Barcode/          ortak: BarcodeContract.cs, BarcodeErrorCodes.cs, BarcodeCentralAudit.cs, Ports/, Services/, Resources/
```

**Golden Slim sapması G-0182-01 (adıyla):** DEV-0000 `Delete{Module}Command` / `BulkDelete{Module}Command` ve PUT / DELETE uçları tanımlar. 0182'de
**silme yoktur** (§2.3 kural 6): `Delete` / `BulkDelete` komutu, `delete` / `bulk-delete` izni ve DataTable "Sil" / toplu sil eylemi yoktur;
cihaz engellenir, şablon pasifleştirilir; güncelleme `POST …/update` komut ucudur (InventoryService PUT bekçisi). `ScanEvents` ve `LabelPrints`
listelerinde düzenleme formu yoktur (insert-only kayıtlar): `ScanEvents`'te `_CreateEditOffcanvas.cshtml` **yok**, `LabelPrints`'te offcanvas
yalnız "Yeni basım" formudur. DEV-0000'ın `data_mode: client`'ı 0182'de `server`'dır (okutma olayı listesi sınırsız büyür). Klasör, adlandırma
ve partial seti diğer her yerde Golden Slim ile aynıdır; `verify_datatable_page.py` sapma verirse dokunma protokolüyle sahibine sorulur.

Kurallar: Command / Query `sealed record`; Handler / Validator adında `Command` / `Query` soneki yok; bir dosyada tek `public` tip;
`Handlers/CommandHandlers` ve `Handlers/QueryHandlers` ayrımı zorunlu (`.antigravity/rules/module-pack-standard.md` §4).

## 11. Frontend File Contract
Golden Slim seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`):

| Klasör | Dosyalar |
|---|---|
| `Views/Warehouse/ScanEvents/` | `Index.cshtml` (KPI kartları + liste), `_Filter.cshtml` (depo zorunlu, tarih ≤ 31 gün, kullanıcı, cihaz, sonuç, işlem türü), `_DataTable.cshtml` (`data-dt-standard="v2"`, `data-dt-data-mode="server"`, skeleton), `_IndexL10n.cshtml`, `_DetailsQuickView.cshtml`, `ScanEventsIndex.cs` — `_CreateEditOffcanvas.cshtml` **yok** (G-0182-01) |
| `Views/Warehouse/ScanDevices/` | `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (6 alan: ad, tür, platform, depo, kurulum kimliği — yalnız Web kaydında, not; r4, F-2: kayıt ve yeniden etkinleştirme sonrası aynı panelde eşleştirme QR'ı + kod + kalan süre, anahtar yok), `_DetailsQuickView.cshtml` (sağlık + engelleme geçmişi + r4 F-5 son aşım denemeleri), `ScanDevicesIndex.cs` |
| `Views/Warehouse/BarcodeSettings/` | `Index.cshtml` (form; DataTable değil), `_IndexL10n.cshtml`, `BarcodeSettingsIndex.cs` |
| `Views/Warehouse/SupervisorPin/` | `Index.cshtml` (PIN formu; DataTable değil), `_IndexL10n.cshtml`, `SupervisorPinIndex.cs` |
| `Views/Warehouse/LabelTemplates/` | `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (6 alan + canlı önizleme), `_DetailsQuickView.cshtml`, `LabelTemplatesIndex.cs` |
| `Views/Warehouse/LabelPrints/` | `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (yalnız "Yeni basım": 8 alan + önizleme; "Bas" önizleme yüklenmeden etkin değil — E-1), `_DetailsQuickView.cshtml` (iş ayrıntısı, PDF'i aç, "Yeniden bas"), `LabelPrintsIndex.cs` |
| `Views/Warehouse/ScanTrace/` | `Index.cshtml` (arama + zaman çizelgesi; DataTable değil), `_IndexL10n.cshtml`, `ScanTraceIndex.cs` |
| `wwwroot/assets/js/Warehouse/{Module}/` | `index.js`, `index.l10n.js`; `LabelPrints/preview.js` |
| `Resources/Views/Warehouse/{Module}/` | `{Module}Index.{en,tr,fr,es,zh,ar,ru}.resx` |
| `frontend/Diten.Web/Controllers/` | `ScanEventsController.cs`, `ScanDevicesController.cs`, `BarcodeSettingsController.cs`, `SupervisorPinController.cs`, `LabelTemplatesController.cs`, `LabelPrintsController.cs`, `ScanTraceController.cs` — same-origin MVC proxy → Gateway `/api/warehouse/barcode…`; her yazma `[ValidateAntiForgeryToken]`; her proxy isteğine sayfanın `Accept-Language`'ı; `If-Match` ve `Idempotency-Key` iletilir; PIN gövdesi proxy'de loglanmaz |
| `frontend/Diten.Web/Resources/` (ortak) | `SharedResource.{7}.resx` nav anahtarları ve `Perm.Entity.*` satır adları, `PageHelpResource.{7}.resx` sayfa yardımı (§25) — yalnız eklemeli |

Compact'a özgü `Create/Edit/Details.cshtml` ve `_Form.cshtml` **yok**. Liste ekranına dokunan görev
`verify_datatable_page.py --area Warehouse --module {Module} --format gaps` koşar ve sapmayı sahibine sorar (dokunma protokolü).
Nav anahtarları: `Nav.Module.BARCODERFID`, `Nav.Page.SCANEVENTS`, `Nav.Page.SCANDEVICES`, `Nav.Page.BARCODESETTINGS`, `Nav.Page.SUPERVISORPIN`,
`Nav.Page.LABELTEMPLATES`, `Nav.Page.LABELPRINTS`, `Nav.Page.SCANTRACE`.

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| Cihaz `Name` | Evet | trim, 2..80 | tekil kısmi (şirket, `Status ≠ Blocked`) | — |
| Cihaz `Type` / `Platform` | Evet | §4.1 enum | — | — |
| Cihaz `InstallationId` | Evet | 8..100, `^[A-Za-z0-9._:-]+$` | tekil (şirket) | — |
| Cihaz `WarehouseId` | Hayır | uuid; LOCATION `Warehouse`, `Active`, şirket kapsamı | — | LOCATION okuması |
| Engelleme / yeniden etkinleştirme gerekçesi | Evet | trim, 10..500 | — | — |
| Nabız `batteryPercent` / `appVersion` | Hayır / Evet | 0..100 / 1..40 | — | — |
| Okutma `rawValue` | Evet | 1..500; yazdırılabilir karakter + U+001D (+ baştaki `]xx` sembol tanıtıcısı); başka denetim karakteri 400 | — | — |
| Okutma `source` / `context.kind` / `context.step` | Evet | §4.2 enum; adım bağlamda geçerli olmalı (§8.4) | — | — |
| Okutma `context.refId` / `lineRef` | `Query` dışında evet | uuid / 1..100 | — | bağlam okuması (404) |
| Okutma `sessionId` | `Query` dışında evet | uuid | — | — |
| Okutma `warehouseId` | Evet | uuid; bağlamın deposuyla aynı | — | — |
| `manual.reasonCode` / `manual.note` | `source = Manual` ise evet / `Other` ise evet | §8.5 listesi / 10..500 | — | — |
| `capturedAt` | Hayır | ISO-8601 UTC | — | — |
| Aşım `supervisorUserId` / `pin` | Evet | uuid / 4..8 hane; **`pin` parmak izine katılmaz** (r4, F-3 — §8.1) | — | PIN kaydı |
| Cihaz kaydı `keyDelivery` (r4, F-2) | Hayır | `Direct` (varsayılan) \| `Pairing`; Web proxy her zaman `Pairing` | — | — |
| Eşleştirme `pairingCode` / `installationId` (r4, F-2) | Evet / Evet | 43 karakter base64url (32 bayt) / §4.1 `InstallationId` kuralı | `PairingCodeHash` tekil kısmi | kod özeti araması (kiracı + şirket) |
| PIN belirleme | Evet | 4..8 hane; aynı rakamın tekrarı (`1111`) ve ardışık dizi (`1234`) reddedilir | tekil (kullanıcı) | — |
| Ayar alanları | Evet | §4.4 aralıkları; `ScanRequiredWarehouseIds` şirketin depoları; `SsccCompanyPrefix` FU07 etkin listesinde | tekil (şirket) | LOCATION / FU07 okuması |
| Şablon `Name` / `LabelType` / `Size` / `HumanReadableFields` | Evet | 2..80 / enum / sabit liste / türün izinli kümesinden 1..8, tekrarsız | tekil (şirket, ad) | — |
| Basım `labelType` / `templateId` | Evet | şablon `Active` ve aynı tür | — | şablon okuması |
| Basım `sources` | Evet | türe göre (§8.9); toplam etiket ≤ 500 | — | kaynak okuması (`LABEL_SOURCE_INVALID`) |
| Basım `copies` | Evet | 1..20 | — | — |
| Yeniden basım `reprintOfJobId` / gerekçe / not | `isReprint` ise evet / evet / `Other` ise evet | uuid aynı şirket / §8.9 listesi / 10..500 | — | iş okuması |
| RFID `epcs` | Evet | 1..5.000, 24 hane hex (96 bit), tekrarsız | — | — |
| İz sorgusu | Evet | tam olarak bir anahtar: `lot` + `skuId` \| `serial` + `skuId` \| `sscc` (18 hane) | — | — |
| Idempotency-Key | Evet (her yazma) — **adlı muafiyet (r4, F-4):** cihaz nabzı (tekrarlanabilir `$set`, kayıt yazmaz) | 8–100 karakter (MOD-0178 emsali) | tekil index (`ScanEvent` ya da `wms_barcode_command_requests`, §4.10) | anahtar araması If-Match / durum / tekillikten önce (§8.1) |
| If-Match | Evet (değişebilir kayıtta yaratma dışı yazma) — **adlı muafiyetler (r3, F-10):** cihaz nabzı (§8.2) ve aşım denemesinin `SupervisorPin.FailedAttempts` / `LockedUntil` yazımları (§8.5); ikisi de `Version`'ı artırmaz, tek belgelik atomik `findOneAndUpdate`'tir | `W/"<version>"` | — | — |
| TenantId / LegalEntityId gövdede | Hayır | gelirse 400 | — | — |

## 13. Failure Path to Verify
- **Duplicate (aynı Idempotency-Key, aynı gövde)** — Expected: ilk cevap aynen; yeni olay / kayıt / merkezi kayıt / numara / SSCC yok.
- **Cihaz kaydı / yeniden etkinleştirme replay'i (r3, F-4)** — Expected: ilk durum kodu ve gövde, `deviceKey` yok, `deviceKeyNotice =
  BARCODE_DEVICE_KEY_NOT_REPLAYED`; yeni anahtar üretilmez, `KeyHash` değişmez, ilk anahtar geçerli.
- **Eşleştirme kodu ikinci kez / eşzamanlı iki kez (r4, F-2)** — Expected: ilk kullanım 200 + anahtar; ikinci 404, yeni anahtar yok, `KeyHash`
  ilk eşleştirmedeki; eşzamanlı ikisinden tam biri 200.
- **Süresi dolmuş eşleştirme kodu (r4, F-2)** — Expected: 422 `BARCODE_PAIRING_EXPIRED`; anahtar üretilmez; `KeyHash` null kalır.
- **Başka şirketin / kiracının eşleştirme kodu; başka `InstallationId` (r4, F-2)** — Expected: 404 / 422 `BARCODE_PAIRING_INVALID`; yazım yok.
- **Başka şirkette kayıtlı cihazın kimliği ve anahtarıyla okutma (r4, F-8)** — Expected: 403 `BARCODE_DEVICE_KEY_INVALID`; olay yok.
- **Başarılı aşımın replay'i (r4, F-4)** — Expected: ilk cevap (201) aynen; 409 `BARCODE_ALREADY_OVERRIDDEN` değil; PIN değerlendirilmez,
  sayaç ve deneme izi değişmez.
- **Aynı anahtar, yalnız PIN'i farklı aşım / PIN belirleme gövdesi (r4, F-3)** — Expected: replay (ilk cevap); 400 değil; idempotency
  deposunda PIN ya da türevi yok.
- **Cihaz engellendikten sonra eski okutmanın replay'i (r3, F-11)** — Expected: ilk cevap aynen; yeni `DEVICE_BLOCKED` olayı yok.
- **Başka kullanıcının `SessionId`'siyle okutma (r3, F-11)** — Expected: 422 `BARCODE_SESSION_INVALID`; olay yazılmaz.
- **Başkasının okutmasına aşım (r3, F-3)** — Expected: 404; aşım ve sayaç yazımı yok.
- **Eşzamanlı yanlış PIN denemeleri (r3, F-10)** — Expected: sayaç deneme sayısı kadar artar; sınırda kilit; 422'lere rağmen sayaç kalıcı.
- **Aynı anahtar, farklı gövde** — Expected: 400 `IDEMPOTENCY_KEY_REUSED`, yazım yok.
- **Missing alan** (ham değer, bağlam, gerekçe) — Expected: 400 validator mesajı; elle girişte gerekçesiz 400.
- **Unauthorized actor** — Expected: 403; UI eylemi gizli; sayfa izni yoksa iskelet yok (UAS-001).
- **Concurrency (eski If-Match)** — Expected: 409 `BARCODE_CONFLICT`; "Kayıt değişti, yenileyin."; sessiz üzerine yazma yok. Nabız `Version`'ı
  değiştirmediği için nabız sonrası düzenleme 409 **vermez**.
- **Başka kiracı / şirket kaydı ya da bağlamı** — Expected: 404 (403 değil); olay yazılmaz.
- **Bilinmeyen cihaz / yanlış anahtar** — Expected: 403 `BARCODE_DEVICE_KEY_INVALID`, hiçbir şey yazılmaz.
- **Engelli / onay bekleyen cihaz** — Expected: 201, `Rejected` `DEVICE_BLOCKED` / `DEVICE_PENDING`, olay yazılır.
- **Geri çağrılmış lot ya da SKT geçmiş + geçerli PIN'le aşım** — Expected: 422 `BARCODE_OVERRIDE_NOT_ALLOWED`; aşım yazılmaz.
- **Toplamada yanlış raf ya da farklı lot + geçerli PIN'le aşım (Q-3 = A)** — Expected: 422 `BARCODE_OVERRIDE_NOT_ALLOWED`; aşım yazılmaz; olay
  `overridable = false` kalır.
- **Sorumlu kendi okutmasını aşar** — Expected: 422 `BARCODE_OVERRIDE_SELF`.
- **Yanlış PIN art arda** — Expected: `PinMaxAttempts`'e kadar 422 `BARCODE_PIN_INVALID`, sonra 423 `BARCODE_PIN_LOCKED` `PinLockMinutes` boyunca;
  423 gövdesi `details.lockedUntil` (UTC) taşır, deneme sayısı / kalan hak hiçbir gövdede yok (r4, CT kararı).
  r4 (F-5): her yanlış deneme ve kilitliyken deneme `ScanOverrideAttempt` yerel izi bırakır; iz `ScanEvents` ayrıntısında ve Device Health'te görünür.
- **Aşılamaz nedenli, başkasının ya da aşılmış olayda yanlış PIN (r4, F-5)** — Expected: 422 `BARCODE_OVERRIDE_NOT_ALLOWED` / 404 / 409; PIN
  değerlendirilmez, sayaç artmaz, sorumlu kilitlenmez.
- **Kör sayım bin'inde "Bu nedir?" / sayım okutması / RFID** — Expected: miktar ve beklenen yok (`quantitiesHidden` / `expectedHidden`).
- **Lot / SKU / GTIN toplamına ya da iz adımına açık kör sayım bin'i katılıyor (r3, F-5)** — Expected: toplamın miktarı bütünüyle gizli; iz
  adımı görünür, miktarı `null` + `quantityHidden`; kanıt paketinde de.
- **DL-3: okutma zorunlu depoda okutmasız onay** — Expected: 422 `BARCODE_SESSION_REQUIRED`; onay yazılmaz.
- **DL-3: aynı oturumla iki onay (eşzamanlı)** — Expected: tam biri başarılı; diğeri 422 `BARCODE_SESSION_CONSUMED`.
- **DL-3: okutma zorunlu olmayan depo** — Expected: 0178 / 0181 bugünkü gibi; 0178 / 0181 testleri değişmeden yeşil.
- **FU07 erişilemez (DL-5)** — Expected: 503 `BARCODE_GTIN_RESOLUTION_UNAVAILABLE`; olay yazılmaz; ürün kabul edilmez.
- **İki eşzamanlı SSCC basımı** — Expected: farklı SSCC'ler; sayaç atlamaz ya da tekrar etmez (boşluk yalnız başarısız işlemde, yeniden kullanım yok).
- **Aynı koliye ikinci yeni basım** — Expected: mevcut SSCC yeniden kullanılır; yeni SSCC yok.
- **Önizleme** — Expected: hiçbir yazım (iş, sayaç, merkezi kayıt) yok.
- **RFID'de çözülemeyen EPC** — Expected: `Unknown` listede; tarama düşmez; stok değişmez.
- **Inventory / LOCATION erişilemez** — Expected: 503 `BARCODE_INVENTORY_UNAVAILABLE`; yazım yok.
- **Fazladan PUT / DELETE ucu** — Expected: `LedgerImmutabilityArchitectureTests` kırmızı.

## 14. Authorization Convention

**Namespace (Q-1 = A — sahip kararı, karar 03, 2026-11-13):** `warehouse.barcode.*` — PKS-001 `warehouse.*` ad alanı altında MOD-0182'ye ayrılmış
**adlı alt ad alanı** (`.antigravity/rules/permission-key-standard.md:92` istisna kuralı; bugün tek örnek `warehouse.waves.*` → MOD-0180, `:106`).
Kural gereği alt ad alanı ancak **iki paketin de kaydettiği** bir sahip kararıyla açılır: bu paket (§14, karar 03) + MOD-0178 §14.1 notu (X-9 —
henüz yazılmadı). B seçeneği (`barcode.*`, MOD-0178 Q-08'deki önerilen ad) **seçilmedi**. PKS-001 / RCS-001 satırlarının yazımı (`.antigravity`
kapısı) **paket onayından önce** kapanır ve hâlâ açıktır (§6). Depoda `warehouse.barcode.` anahtarı yok (MOD-0178'in `warehouse.*` anahtarları ölçüldü: `activity`,
`exceptions`, `integration-monitor`, `location-roles`, `outbound-shipments`, `packages`, `pick-tasks`, `putaway-tasks` — çakışma yok).

**Fiiller:** yalnız Tier-1 (`read`, `create`, `update`) ve Tier-2 (`activate`, `deactivate`, `approve`, `export`) ile Tier-3 `configure` (Rol
İzinleri köprüsünde var — X-10); **yeni fiil yok**. "Engelle" UI etiketidir, anahtar `deactivate`'tir.

| Anahtar | Tier | Dilim | Anlam |
|---|---|---|---|
| `warehouse.barcode.scans.create` | 1 | DL-1 | okutma gönder (her bağlam, sorgu dahil), cihaz nabzı, **aşım ucu** `POST /barcode/scans/{scanId}/override` (çalışanın JWT'siyle, kendi okutmasında; sorumlunun yetkisi PIN kaydından — r3, F-3); DL-6'da RFID taraması |
| `warehouse.barcode.scan-events.read` | 1 | DL-1 | okutma olayları listesi, ayrıntı, KPI |
| `warehouse.barcode.scan-overrides.approve` | 2 | DL-1 | sorumlu olma yetkisi: kendi PIN'ini belirleme (`POST /barcode/supervisor-pin`, `SUPERVISORPIN` sayfası); PIN'i aşımda kabul edilir. Aşım ucunu **korumaz** (r3, F-3) |
| `warehouse.barcode.devices.read` | 1 | DL-1 | cihaz listesi, sağlık |
| `warehouse.barcode.devices.create` | 1 | DL-1 | cihaz kaydı (mobil ilk açılış — depo çalışanında olmalı); **r4 (F-2): eşleştirme ucu** `POST /barcode/devices/pair` (çalışanın JWT'si + tek kullanımlık kod; gerekçe §8.2) |
| `warehouse.barcode.devices.update` | 1 | DL-1 | ad / depo / not düzenle |
| `warehouse.barcode.devices.activate` | 2 | DL-1 | onayla (Pending → Active), yeniden etkinleştir (yeni anahtar) |
| `warehouse.barcode.devices.deactivate` | 2 | DL-1 | engelle (gerekçe) |
| `warehouse.barcode.settings.read` | 1 | DL-1 | şirket ayarı okuma |
| `warehouse.barcode.settings.configure` | 3 (bu pakette kayıtlı) | DL-1 | şirket ayarı yazma (eşikler, PIN kuralları, okutma zorunluluğu — DL-3, SSCC öneki — DL-5) |
| `warehouse.barcode.label-templates.read` / `.create` / `.update` / `.activate` / `.deactivate` | 1 / 1 / 1 / 2 / 2 | DL-4 | şablonlar |
| `warehouse.barcode.label-prints.read` / `.create` | 1 / 1 | DL-4 | basım işleri; yeni basım ve yeniden basım; önizleme `create` ister |
| `warehouse.barcode.trace.read` / `.export` | 1 / 2 | DL-7 | uyum izi; kanıt paketi |

**Politika ve aktör:** `[Authorize]` (tenant shell) + her eylemde `[HasPermission(BarcodePermissions.X)]`; `AuthorizationGuardTests` yeni
controller'ları da ölçer. Aktör `tenant_user`. Cihaz başlıkları yetki değildir — **ek** bir kimlik kapısıdır (JWT + izin + cihaz).
Aşımda sorumlunun izni JWT'den değil PIN kaydından gelir (§8.5, Q-4 = A); aşım ucunun `[HasPermission]`'ı `ScansCreate`'tir (r3, F-3 — gerekçe §8.5).
**Rol şablonu önerisi** (yalnız yeni kiracılar; X-11, Q-14 — Ali): **Depo çalışanı** = `scans.create` + `devices.create` + `label-prints.read` /
`.create`; **Vardiya sorumlusu** = çalışan + `scan-events.read` + `scan-overrides.approve` + `devices.read`; **Cihaz sorumlusu** = `devices.*` +
`settings.*`; **Etiket sorumlusu** = `label-templates.*` + `label-prints.*`; **Kalite / denetçi** = `scan-events.read` + `trace.*`. Rol adları öneri.
`read` anahtarlarında kişisel veri: kullanıcı adı (okutan, sorumlu) — görev atamasında zaten görünür (çıkarım).

**Manifest:** ModuleCode `BARCODE-RFID`, SortOrder 182; Pages `SCANEVENTS` (`scan-events.read`), `SCANDEVICES` (`devices.read`; eylemler
create / update / activate / deactivate), `BARCODESETTINGS` (`settings.read`; eylem `configure`), `SUPERVISORPIN` (`scan-overrides.approve`),
`LABELTEMPLATES` (DL-4), `LABELPRINTS` (DL-4), `SCANTRACE` (DL-7); `ApiPermissions` = `warehouse.barcode.scans.create` (ekranı mobilde olan uç).
Manifest yalnız `warehouse.barcode.*` bildirir; 0178'in `warehouse.*` anahtarları bildirilmez (`ManifestNamespaceOwnershipTests` deseni).

## 15. Gateway / API Routing Decision
- **Karar:** uçlar `/api/warehouse/barcode/…` — mevcut `/api/warehouse/{everything}` → 5067 rotasının altında
  (`gateway/Diten.ApiGateway/ocelot.json:1153-1161`; taban `/api/warehouse` `:1134-1142`). **`ocelot.json` değişmez.**
- Gerekli: integration-agent'ın 0182 uç kapsama testi — ayrı dosya `gateway/Diten.ApiGateway.Tests/Mod0182BarcodeRouteTests.cs` (emsal
  `Mod0178WarehouseRouteTests.cs`; ona yalnız X-12'nin uç sayısı satırı ve yorumu için dokunulur — aşağıda); test üretim `ocelot.json`'u ve üretim controller kaynağını okur (uç listesi testte elle
  yazılmaz) ve **`X-Scan-Device-Id` / `X-Scan-Device-Key` başlıklarının Gateway'den servise iletildiğini** ölçer (ölçülmedi — Ocelot başlık
  iletimi DL-8'in ilk ölçümü). `/api/warehouse` ailesinin sahibi MOD-0178'in onayı (XMC-001).
- **0178 Gateway testinin uç sayısı (r3, F-1 — r2'nin "değişmez" yazımı yanlıştı):** `Mod0178WarehouseRouteTests` bütün `api/warehouse`
  controller'larını tarar ve `Assert.Equal(<n>, endpoints.Count)` ile sayıyı sabitler; 0182 uçları ilk dilimde (DL-1) bu satırı kırmızı yapar.
  Uç ekleyen her dilim (DL-1, DL-4, DL-6, DL-7) satırı dilim başında ölçülen sayı + kendi uçlarıyla, uçlarıyla aynı commit'te günceller
  (integration-agent) — X-12, **sahip onaylı (XMC-001, `ad1100158`, her artış dahil — r4)**; AC-65 0178 sayı vakası, S-65c. `ocelot.json` yine
  değişmez. r4 (F-2): `POST /barcode/devices/pair` aynı `/api/warehouse/{everything}` rotasına eşleşir; DL-1 sayısı 16.
- İletilen başlıklar: `Authorization`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match`, `Accept-Language`,
  `X-Scan-Device-Id`, `X-Scan-Device-Key`. Dev dilimi `ocelot.json`'a yazmaz.

## 16. Acceptance Criteria

Her guard maddesi **üretim koduna karşı** ölçülür ve sabotaj kanıtı ister: ilgili üretim satırı geçici bozulur, test kırmızıya döner, bozma geri
alınır (kendi kopyasıyla ölçen test guard sayılmaz). Sabotaj kimliği `S-n` biçimindedir; kabul kaydı her `S-n` için komut + kırmızı çıktı + geri
almayı yazar.

**Bekçi olmayan maddeler:** AC-29 (performans ölçümü; S-29 yalnız eşiğin çalıştığını gösterir), AC-32 (`verify_datatable_page.py`), AC-38
(375 px ve durumlar, tarayıcı ölçümü) — kanıtları araç çıktısı / ekran ölçümüdür. Diğer her maddenin adlı sabotajı vardır.

**DL-1 — SCAN-BUNDLE taslağı + cihaz + okutma çekirdeği (arka uç)**

- [ ] AC-1 (zorunlu) — `docs/analysis/contracts/drafts/scan-bundle-v1-proposal.md` yazıldı: §3'teki DL-1 uçları (operationId, istek / cevap şeması,
  §8.3 okutma sonucu kodları, §8.15 hata kodları, başlıklar), sürüm `1.0.0-draft`; sözleşme testi controller cevaplarını taslağa karşı kapalı
  şemayla doğrular; r4: taslak QR eşleştirme akışını (`POST /barcode/devices/pair`, `keyDelivery`, `pairing`, `pairingCodeNotice`) ve şirket
  başına cihaz kaydı / anahtar / Web onayı kuralını (§8.2, F-2, F-8) yazar. S-1: bir cevaptan zorunlu alanı kaldır → sözleşme testi kırmızı.
- [ ] AC-2 (zorunlu) — `POST /barcode/devices` 201; `Status = Pending` (Q-8 = A, karar 03 — kendiliğinden `Active` doğan kayıt yok), `Number` `{ŞİRKET}-DVC-` + 6 hane; cevap (`keyDelivery = Direct`, mobil) cihaz anahtarını **bir
  kez** taşır (r4: `Pairing` için AC-3); `GET` uçları, liste, merkezi kayıt, olay verisi ve log anahtarı ya da özetini taşımaz; depoda yalnız SHA-256 özeti var. S-2a: GET
  DTO'suna `KeyHash` ekle → kırmızı; S-2b: anahtarı düz metin sakla → kırmızı. **r3 (F-4) — replay vakası:** aynı `Idempotency-Key` + aynı
  gövdeyle ikinci `POST /barcode/devices` ve ikinci yeniden etkinleştirme → ilk durum kodu ve gövde, `deviceKey` **yok**, `deviceKeyNotice =
  BARCODE_DEVICE_KEY_NOT_REPLAYED`; idempotency deposundaki belge anahtarı ve özetini taşımaz (depo taraması). S-2c: idempotency kaydına ilk
  cevabı anahtarıyla yaz (replay anahtarı döndürsün) → replay vakası ve depo taraması kırmızı.
- [ ] AC-3 (zorunlu) — Cihaz durum makinesi §8.2 tam: geçerli geçişler 200, geçersizler 409 `BARCODE_INVALID_STATE`; engelleme ve yeniden
  etkinleştirme gerekçesiz 400; yeniden etkinleştirme eski anahtarı geçersiz kılar (eski anahtar 403) ve yeni anahtar yalnız QR eşleştirmesiyle
  gelir; silme ucu yok. S-3: yeniden etkinleştirmede `KeyHash`'i geçersiz kılmayı kaldır → eski anahtar vakası kırmızı. **r4 (F-2) — QR ile
  anahtar teslimi (sahip kararı `4168e7a90`):** Web kaydı (`keyDelivery = Pairing`) ve yeniden etkinleştirme cevabında `deviceKey` **yok**,
  `pairing.code` + `pairing.qrPng` + `pairing.expiresAt` (= şimdi + 10 dk, sahte saatle) var; depoda yalnız `PairingCodeHash` (kodun kendisi hiçbir
  koleksiyonda, logda ve merkezi kayıtta yok — depo taraması); yalnız `devices.create` taşıyan çalışanın JWT'siyle `POST /barcode/devices/pair`
  (doğru kod + aynı `InstallationId`) → 200, anahtar bir kez; bu anahtarla okutma kabul edilir (cihaz `Active` ise); **aynı kod ikinci kez →
  404**, yeni anahtar yok, `KeyHash` ilk eşleştirmedeki; eşzamanlı iki eşleştirmeden (görevli kanca) tam biri 200; sahte saatle 10 dk + 1 sn
  sonra → 422 `BARCODE_PAIRING_EXPIRED`, anahtar üretilmez; başka şirketin / kiracının kodu 404; farklı `InstallationId` 422
  `BARCODE_PAIRING_INVALID`; `devices.create` olmadan 403; eşleştirme replay'i anahtarsız (`deviceKeyNotice`); yeniden etkinleştirme replay'i
  kodsuz (`pairingCodeNotice`), yeni kod üretmez. S-3b: tüketim filtresinden tek kullanım koşulunu (`PairingCodeHash` null'lama ya da
  filtredeki özet eşleşmesi) kaldır → ikinci kullanım vakası (ve eşzamanlılık vakası) kırmızı; S-3c: `PairingExpiresAt > şimdi` koşulunu
  kaldır → süresi dolmuş kod vakası kırmızı; S-3d: kodu düz metin sakla (ya da idempotency `ResponseBody`'sine kodlu DTO'yu yaz) → depo
  taraması kırmızı.
- [ ] AC-4 (zorunlu) — Kiracı / şirket izolasyonu: başka kiracıdan her uç (cihaz, okutma, aşım, olaylar, ayar, PIN) ve başka kiracının bağlamına
  okutma 404; aynı kiracıda başka şirketten 404; listeler yalnız kendi şirketi. **r4 (F-8):** aynı `InstallationId` iki şirkette iki ayrı
  kayıt (ayrı `Number`, ayrı anahtar, her biri `Pending` ve ayrı onay); A şirketinin cihaz kimliği + anahtarıyla B şirketi bağlamında okutma →
  403 `BARCODE_DEVICE_KEY_INVALID`, 0 olay. S-4a: `TenantId` yüklemini düşür → kırmızı; S-4b: `LegalEntityId` yüklemini düşür → kırmızı
  (şirketler arası anahtar vakası dahil).
- [ ] AC-5 (zorunlu) — Gövde ya da sorguda `tenantId` / `legalEntityId` → 400. S-5: denetimi kaldır → kırmızı.
- [ ] AC-6 (zorunlu) — Her eylem `[HasPermission(BarcodePermissions.*)]`; manifest yalnız uygulanan `warehouse.barcode.*` anahtarlarını bildirir;
  0178'in manifestinde `warehouse.barcode.` önekli anahtar yok ve 0182 manifestinde `warehouse.barcode.` dışı anahtar yok; `AuthorizationGuardTests`
  ve `WarehouseRegistrationTests` değişmeden yeşil. S-6a: bir eylemden `[HasPermission]`'ı kaldır → kırmızı; S-6b: manifeste `warehouse.pick-tasks.read`
  ekle → kırmızı; S-6c: bir 0182 controller'ını `Controllers.Warehouse` namespace'ine taşı → `WarehouseRegistrationTests` kırmızı.
  **r3 (F-2) — kendi bütünlük vakası:** `Controllers.Barcode` controller'larının `[HasPermission]` ile zorladığı her anahtar BARCODE-RFID
  manifestinden (sayfa, eylem ya da `ApiPermissions`) ulaşılabilir — MOD-0173 `InventoryLedgerManifestCompletenessTests`'in `Controllers.Barcode`
  dışlamasının (X-15) karşılığı; o test X-15 satırıyla yeşil. S-6d: manifestin `ApiPermissions`'ından `warehouse.barcode.scans.create`'i çıkar →
  0182 bütünlük vakası kırmızı.
- [ ] AC-7 (zorunlu) — Cihaz kapısı §8.2: bilinmeyen cihaz ya da yanlış anahtar → 403 `BARCODE_DEVICE_KEY_INVALID`, 0 olay, 0 merkezi kayıt;
  `Pending` / `Blocked` → 201 `Rejected` `DEVICE_PENDING` / `DEVICE_BLOCKED`, olay yazılır; depolu cihaz başka depoda → `DEVICE_WRONG_WAREHOUSE`.
  S-7a: anahtar karşılaştırmasını kaldır → kırmızı; S-7b: `Blocked` denetimini kaldır → kırmızı.
- [ ] AC-8 (zorunlu) — Okutma olayı insert-only: `ScanEvent` üzerinde güncelleme / silme yolu yok (depo arayüzünde yalnız ekleme ve okuma);
  aşım olayı değiştirmez, ayrı `ScanOverride` yazar. S-8: depoya `UpdateAsync` ekleyip aşımda olaya yaz → mimari test kırmızı.
- [ ] AC-9 (zorunlu) — Idempotency: aynı anahtar + aynı gövde → ilk cevap aynen, ikinci olay / merkezi kayıt yok; aynı anahtar + farklı gövde →
  400 `IDEMPOTENCY_KEY_REUSED`; anahtar kullanıcı + şirket başına. S-9: anahtar aramasını kaldır → kırmızı. **r3 vakaları:** (F-4) cihaz kaydı ve
  yeniden etkinleştirme replay'i yeni anahtar üretmez — `KeyHash` replay öncesi ve sonrası aynı, ilk cevaptaki anahtar okutmada geçerli (403
  değil); S-9b: replay yolunda anahtar üretimini yeniden çalıştır → `KeyHash` değişir, ilk anahtar 403 alır, kırmızı. (F-11) başka kullanıcının
  olayı bulunan `SessionId`'yle okutma → 422 `BARCODE_SESSION_INVALID`, olay yazılmaz; S-9c: oturum sahipliği denetimini kaldır → kırmızı.
  (F-11) okutma A `Accepted` yazılır, cihaz engellenir, A aynı anahtar + gövdeyle tekrarlanır → ilk cevap aynen, yeni olay (ve
  `DEVICE_BLOCKED` olayı) yok; S-9d: idempotency aramasını cihaz durumu değerlendirmesinden sonraya taşı → yeni `DEVICE_BLOCKED` olayı yazılır,
  kırmızı. **r4 (F-4) — komut idempotency deposu:** okutma dışındaki her yazma komutu (en az: cihaz kaydı, düzenleme, engelleme, eşleştirme,
  aşım, PIN, ayar; DL-4 / DL-6 / DL-7'de şablon, basım, tarama, dışa aktarım) aynı anahtar + aynı gövdeyle ikinci kez → ilk durum kodu ve gövde,
  ikinci yazım / merkezi kayıt / numara yok; kayıt `wms_barcode_command_requests`'te, komutun işleminde (başarısız komut sonrası aynı anahtarla
  yeniden deneme yeniden çalışır); başarılı düzenlemenin replay'i eski `If-Match`'le 409 değil ilk cevap; **başarılı aşımın replay'i 409
  `BARCODE_ALREADY_OVERRIDDEN` değil ilk cevap** (201), PIN değerlendirilmez, sayaç ve deneme izi değişmez. S-9e: aşımda replay aramasını
  tekillik denetiminden sonraya taşı → aşım replay vakası 409 alır, kırmızı; S-9f: `UpdateScanDeviceCommand`'da idempotency kaydını yazmayı
  kaldır → düzenleme replay vakası (409 `BARCODE_CONFLICT` ya da ikinci merkezi kayıt) kırmızı.
- [ ] AC-10 (zorunlu) — Ayrıştırma MOD-0174 `Gs1CodeParser`'ıyla: ham FNC1 / GS biçimi, sembol tanıtıcılı biçim ve köşeli biçim aynı alanları verir;
  ham değer değiştirilmeden saklanır (GS dahil); okunamayan → 201 `Rejected` `UNREADABLE`; 0182 kaynak ağacında GS1 AI tablosu yok (mimari
  test: `"01"` / `"17"` / `"10"` AI sabitleri 0182 dosyalarında bulunmaz). S-10a: ham değeri `Trim` + GS silerek sakla → kırmızı; S-10b: 0182'ye
  kendi AI sözlüğünü ekle → mimari test kırmızı.
- [ ] AC-11 (zorunlu) — `Putaway` bağlamı: doğru ürün / lot / hedef bin `Accepted`; hedef ≠ okunan bin → `Warning` `WRONG_LOCATION`
  `overridable = true` (v1'in aşılabilir tek durumu — Q-3 = A); görevin lotundan farklı lot → `Rejected` `DIFFERENT_LOT` `overridable = false`
  ve geçerli PIN'le aşım 422 `BARCODE_OVERRIDE_NOT_ALLOWED`, `ScanOverride` yazılmaz (Q-3 teyidi `7f584f546`, r3 F-7); FG satış kutusu GTIN'i uyuşmaz → `Rejected` `WRONG_PRODUCT`; SKU `gtin` boş → `Warning` `PRODUCT_NOT_VERIFIABLE`
  `overridable = false`. S-11: bin karşılaştırmasını kaldır → kırmızı; S-11b (r3, F-7): aşılabilirlik matrisinde `Putaway` × `DIFFERENT_LOT`'u
  `true` yap → farklı lot vakası (`overridable` ve 422) kırmızı. **r4 (CT kararı) — önerisi olmayan görev:** `ToLocationId` null görevde 0178
  kurallarından geçen bin → `Accepted`; kilitli, karantina, mal kabul / sevkiyat hazırlık rolündeki, başka depodaki ya da saklama koşulu
  uyumsuz bin → `Rejected` `WRONG_LOCATION`, `overridable = false`, geçerli PIN'le aşım 422 `BARCODE_OVERRIDE_NOT_ALLOWED`; öneri varken
  0178 kurallarından geçmeyen bin de aşılamaz `Rejected`. S-11c: öneri yokken 0178 hedef denetimini atlayıp her bin'i `Accepted` dön → kilitli
  bin vakası kırmızı.
- [ ] AC-12 (zorunlu) — `Pick` bağlamı (Q-3 = A, karar 03 — kararlar 02'nin PIN aşımı kuralı toplama için daraltıldı, §8.5): kaynak bin ≠ → `Rejected` `WRONG_LOCATION`, lot ≠ →
  `Rejected` `DIFFERENT_LOT` (Outcome r3, F-7), ikisi de `overridable = false`; ikisinde de geçerli PIN'le aşım 422 `BARCODE_OVERRIDE_NOT_ALLOWED`, `ScanOverride` ve merkezi
  kayıt yazılmaz. S-12: `Pick`'te `Overridable`'ı `true` yap → kırmızı.
- [ ] AC-13 (zorunlu) — Geri çağrılmış lot → `Rejected` `LOT_RECALLED`; SKT geçmiş → `Rejected` `LOT_EXPIRED`; ikisinde de `overridable = false`
  ve geçerli PIN'le aşım 422 `BARCODE_OVERRIDE_NOT_ALLOWED`, `ScanOverride` yazılmaz. S-13a: geri çağırma denetimini kaldır → kırmızı; S-13b:
  aşılabilir neden listesine `LOT_EXPIRED` ekle → kırmızı.
- [ ] AC-14 (zorunlu) — Kör sayım: `Count` bağlamında kör sayımın her adımında cevap ve olay `expected*` ya da bin stoğu taşımaz; satırda olmayan
  SKU / lot `Accepted` + `foundStock: true`. S-14: cevaba satırın `ExpectedQuantity`'sini ekle → kırmızı.
- [ ] AC-15 (zorunlu) — Aynı oturumda aynı seri ikinci kez → `Warning` `SERIAL_DUPLICATE`, `overridable = false`. S-15: tekrar denetimini kaldır → kırmızı.
- [ ] AC-16 (zorunlu) — `Query` ("Bu nedir?") lokasyon ve lot için çözülen özeti, SKT'yi, geri çağırma uyarısını ve depo statü kovalarını
  döner; bin açık kör sayımdaysa (MOD-0181 bin talebi açık + `Blind = true`) miktarlar gizli, `quantitiesHidden: true`. S-16: kör sayım
  denetimini kaldır → kırmızı. **r3 (F-5) — toplam vakası:** lot iki bin'de (biri açık kör sayımda, öteki değil) → lot sorgusunun depo statü
  kovalarının miktarları **bütünüyle** gizli (`quantitiesHidden: true`, `hiddenReason: BLIND_COUNT_OPEN`; yalnız kör olmayan bin'in miktarı da
  gösterilmez); sayım kapanınca aynı sorgu miktarları verir. S-16b: toplam yolunda kör sayım denetimini kaldır (ya da kör bin'i dışlayıp kalanı
  göster) → toplam vakası kırmızı.
- [ ] AC-17 (zorunlu) — Elle giriş: `source = Manual` gerekçesiz 400; `Other` açıklamasız 400; olay `Manual` alanıyla yazılır ve listede "elle"
  görünür; doğrulama okutmayla aynı. S-17: gerekçe denetimini kaldır → kırmızı.
- [ ] AC-18 (zorunlu) — Aşım §8.5: geçerli PIN → `ScanOverride` + 1 merkezi kayıt, olay görünümü "Aşıldı — {sorumlu}"; sorumlu = okutan 422
  `BARCODE_OVERRIDE_SELF`; PIN yok / süresi dolmuş 422 `BARCODE_PIN_NOT_SET`; yanlış PIN 422, `PinMaxAttempts`'te 423 `BARCODE_PIN_LOCKED` ve
  kilit süresince doğru PIN de 423; ikinci aşım 409. S-18a: öz-aşım denetimini kaldır → kırmızı; S-18b: deneme sayacını kaldır → kırmızı;
  S-18c: kilit süresi denetimini kaldır → kırmızı. **r3 vakaları:** (F-3) yalnız `warehouse.barcode.scans.create` taşıyan çalışanın JWT'siyle
  ve geçerli sorumlu PIN'iyle aşım 201 (`scan-overrides.approve` gerekmez); `scans.create` olmadan 403; başka kullanıcının okutmasına aşım
  404, `ScanOverride` ve sayaç yazımı yok. S-18d: aşım ucunun `[HasPermission]`'ını `ScanOverridesApprove` yap → çalışan vakası 403 alır,
  kırmızı; S-18e: olay sahipliği denetimini kaldır → başkasının okutması vakası kırmızı. (F-10) `PinMaxAttempts = 10` ayarıyla 9 eşzamanlı yanlış PIN
  denemesi (görevli kanca) → `FailedAttempts = 9` (kayıp artış yok), 10'uncu yanlış deneme kilidi kurar (423); 422'den sonra sayaç kalıcı. S-18f:
  atomik `$inc`'i oku-yaz ile değiştir → eşzamanlılık vakası kırmızı; S-18g: sayaç yazımını aşım işleminin içine taşı → 422 sonrası sayaç 0,
  kırmızı. **r4 (F-5) — denetim sırası ve iz:** §8.5 sırası tablo vakasıyla ölçülür — (i) aşılamaz nedenli olayda (ör. `LOT_EXPIRED`) **yanlış**
  PIN → 422 `BARCODE_OVERRIDE_NOT_ALLOWED`, `FailedAttempts` değişmez, iz yok; (ii) öz-aşımda yanlış PIN → 422 `BARCODE_OVERRIDE_SELF`, sayaç
  değişmez; (iii) aşılmış olayda yanlış PIN → 409, sayaç değişmez; (iv) başkasının olayında → 404, sayaç değişmez; (v) kilitliyken doğru PIN →
  423. Her yanlış PIN bir `ScanOverrideAttempt` (`PinInvalid`; sınırı aşan denemede `LockSet`), kilitliyken her deneme `Locked` izi yazar
  (422 / 423'e rağmen kalıcı; PIN ve türevi yok); iz `GET /barcode/scan-events/{scanId}` ayrıntısında ve `GET /barcode/devices` ("PIN kilidi"
  rozeti alanı) cevabında görünür; merkezi kayıt 0. S-18h: PIN doğrulamasını aşılamaz neden denetiminden önceye taşı → (i) vakasında sayaç
  artar, kırmızı; S-18i: yanlış PIN izinin yazımını kaldır → iz vakası kırmızı. **r4 (CT kararı) — 423 gövdesi:** 423 `BARCODE_PIN_LOCKED`
  gövdesi `details.lockedUntil` (UTC) taşır ve değer `SupervisorPin.LockedUntil`'e eşittir; 422 `BARCODE_PIN_INVALID` ve 423 gövdelerinde
  deneme sayısı / kalan hak alanı (`failedAttempts`, `remainingAttempts` ya da eşdeğeri) yoktur (gövde anahtarları kapalı listeyle ölçülür).
  S-18j: 422 / 423 gövdesine `remainingAttempts` ekle → kapalı anahtar listesi vakası kırmızı.
- [ ] AC-19 (zorunlu) — PIN saklama: PBKDF2-SHA256 ≥ 100.000 tur, tuzlu; PIN ve özeti hiçbir cevapta, logda (test log toplayıcısı), OTel'de ve
  merkezi kayıtta yok; PIN'i yalnız kişi kendisi belirler; `scan-overrides.approve` olmadan 403. S-19a: tur sayısını 1 yap → kırmızı; S-19b: PIN'i
  log satırına yaz → kırmızı. **r4 (F-3) — idempotency ve PIN:** (a) aynı `Idempotency-Key`, yalnız `pin`'i farklı iki aşım gövdesi (ilk başarılı)
  → ikincisi replay (ilk cevap), 400 `IDEMPOTENCY_KEY_REUSED` **değil**; aynısı `POST /barcode/supervisor-pin` için; (b) **depo taraması:** aşım
  (doğru ve yanlış PIN) ve PIN belirleme sonrası `wms_barcode_command_requests`, `wms_barcode_override_attempts`, `wms_barcode_scan_overrides`,
  `wms_barcode_scan_events` belgelerinin hiçbir alanında PIN düz metni, SHA-256'sı (hex / base64) ya da PBKDF2 özeti yok (PBKDF2 özeti yalnız
  `wms_barcode_supervisor_pins`'te); (c) parmak izi alan listesinde `pin` adlı alan yok (üretim parmak izi kurucusunun alan adları okunarak).
  S-19c: `pin`'i aşım parmak izi alanlarına ekle → (a) vakası 400 alır, (c) kırmızı; S-19d: idempotency `ResponseBody`'si yerine istek gövdesini
  (PIN dahil) sakla → (b) depo taraması kırmızı.
- [ ] AC-20 (zorunlu) — Nabız ve okutma `LastSeenAt` / `LastUser*` / `AppVersion` / pil alanlarını yazar, `Version`'ı artırmaz; nabız merkezi kayıt
  doğurmaz; nabızdan sonra eski `If-Match` ile düzenleme 200. S-20: nabızda `Version`'ı artır → kırmızı. **r3 (F-10):** nabız `If-Match`
  olmadan 200 (§12 muafiyeti); nabız ile eşzamanlı Web düzenlemesi ikisini de korur (düzenlenen ad ve nabzın `LastSeenAt`'i birlikte kalır).
  S-20b: nabız işleyicisine `If-Match` zorunluluğu ekle → `If-Match`'siz nabız vakası kırmızı.
- [ ] AC-21 (zorunlu) — Sağlık okuma anında: `Silent` (`SilentDeviceHours`), `LowBattery` (`LowBatteryPercent`); eşik değişince sonraki okuma yeni
  eşikle; sağlık alanı saklanmaz. S-21: sessizlik eşiğini sabit 24'e bağla → eşik değişikliği vakası kırmızı.
- [ ] AC-22 (zorunlu) — Ayar: `GET` / `POST /barcode/settings/update` If-Match ile; §4.4 aralıkları; `settings.configure` olmadan 403 (`read` ve
  `devices.*` taşıyan kullanıcıda dahi). S-22: aralık denetimini kaldır → kırmızı.
- [ ] AC-23 (zorunlu) — Merkezi denetim §21: her yazma komutu başarıda tam 1 kayıt (`SourceModule = BARCODE-RFID`), replay 0, başarısız 0,
  nabız 0; `CentralAuditDeclarationTests` sayısı dilim başı ölçümü + dilimin komutları — **DL-1 +10, DL-4 +5, DL-6 +1, DL-7 +1** (toplam 17;
  nabız dahil, çünkü testin `:54` satırı her yazma komutunda işaret ister — r3, F-6; r4, F-2 eşleştirme komutu; X-6); DL-1'de kayıtlı modül
  listesine `BARCODE-RFID`. S-23: bir komuttan `ICentralAuditDeclaration`'ı kaldır → kırmızı. **r4 (F-1) — işlem çağrı yeri vakası (X-6 (b),
  sahip onayı `4168e7a90`):** `Every_transaction_a_command_can_run_in_enlists_the_central_audit_event` dilim başında ölçülen sayı + dilimin
  0182 Persistence'a eklediği `WithTransactionAsync(` çağrı yerleriyle yeşil (öneri tek ortak yardımcı: DL-1 +1, sonraki dilimler +0); 0182'nin
  her çağrı yeri geri çağrısında `CentralAuditTransaction.EnlistAsync(s…)` taşır; satır güncellemesi çağrı yeriyle aynı commit'te, yorumda dilim
  adıyla; kabul kaydı önce → sonra ve `Assert.Equal(<n>, seen)` satırına atıf. Vaka, işlem çağrı yeri ekleyen sonraki dilimde de koşar.
  S-23b: 0182 işlem yardımcısının geri çağrısından `CentralAuditTransaction.EnlistAsync(s…)` çağrısını kaldır (enlist etmeyen 0182 işlemi) →
  test `failures` ile kırmızı; S-23c: 0182 Persistence'a enlist eden ikinci bir geçici `WithTransactionAsync(` çağrı yeri ekle → sayı satırı
  kırmızı (güncellenmiş değerden bir fazla); ikisi de geri alınır, commit'e girmez.
- [ ] AC-24 (zorunlu) — `DVC` kodu MOD-0178 geçici sayacından (`IWarehouseRecordCodeAllocator`), biçim `{ŞİRKET}-DVC-{000001}`, kiracıda tekil,
  0182 kendi sayacını yazmaz. S-24: `TypePrefixes`'ten `DVC`'yi çıkar → kırmızı.
- [ ] AC-25 (zorunlu) — 0182'de PUT / DELETE ucu yok; `LedgerImmutabilityArchitectureTests` değişmeden yeşil. S-25: bir `[HttpPut]` ekle → kırmızı.
- [ ] AC-26 (zorunlu) — Tek SoR: 0182 varlıklarında `OnHand` / `Available` / `Reserved` yok; 0182 üretim kodunda başka modül deposuna / koleksiyonuna
  yazma çağrısı yok (mimari tarama); `SingleSystemOfRecordArchitectureTests` `BarcodeCollections.All` bildirimiyle yeşil (X-5). S-26a: bir
  0182 servisine `IWarehouseTaskRepository.UpdateAsync` çağrısı ekle → kırmızı; S-26b: `BarcodeCollections.All`'dan bir koleksiyonu çıkar →
  kırmızı.
- [ ] AC-27 (zorunlu) — Değişebilir kayıtta (cihaz, ayar) eski `If-Match` → 409 `BARCODE_CONFLICT`; eksik `If-Match` → 428 değil 400 (MOD-0178
  emsali). S-27: sürüm karşılaştırmasını kaldır → kırmızı.
- [ ] AC-28 (zorunlu) — Okutma `message` ve hata metinleri `Accept-Language`'a göre 7 dilde; `BarcodeText` 7 resx'in anahtar kümesi eşit;
  bilinmeyen dil `en`. S-28: `ar` resx'ten bir anahtarı sil → parite testi kırmızı.
- [ ] AC-29 (zorunlu) — Performans: 200 ardışık okutma (yerel Mongo replika seti) `POST /barcode/scans` p95 ≤ 500 ms; ölçüm kabul kaydına yazılır.
  S-29: eşiği 1 ms yap → ölçüm testi kırmızı (eşiğin çalıştığını gösterir).
- [ ] AC-30 (zorunlu) — Mongo / LOCATION / PRODUCT-MASTER okuması başarısız → 503 `BARCODE_INVENTORY_UNAVAILABLE`, 0 olay, 0 merkezi kayıt.
  S-30: hatayı yutan `catch` ekle → kırmızı.

**DL-2 — Web yönetim ekranları (okutma olayları, cihazlar, ayarlar, PIN)**

- [ ] AC-31 (zorunlu) — Modülün her sayfa görünümünde (partial hariç) `Layout = "_LayoutTenantShell";` açıkça yazılı; test 0182 sayfa
  görünümlerinin tamamını tarar (DL-4 ve DL-7'nin sayfaları dahil). S-31: `ScanEvents/Index.cshtml`'den satırı sil → kırmızı.
- [ ] AC-32 (zorunlu) — `verify_datatable_page.py --area Warehouse --module ScanEvents` ve `--module ScanDevices` (DL-4'te `LabelTemplates`,
  `LabelPrints`) çıktısı kabul kaydında; sapmalar G-0182-01 dışında sıfır ya da sahibine sorulmuş (araç kontrolü).
- [ ] AC-33 (zorunlu) — Her sayfanın resx'i 7 dilde aynı anahtar kümesi; nav anahtarları `Nav.Module.BARCODERFID` / `Nav.Page.*` biçiminde
  (`check_pack_nav_keys.py`); Arapça sağdan sola. S-33: bir resx'ten anahtar sil → kırmızı.
- [ ] AC-34 (zorunlu) — UAS-001: sayfa izni olmayan kullanıcıya iskelet çizilmez, yönlendirme yapılmaz, içerikte tek açıklama; eylem düğmeleri
  izne göre (ör. `devices.deactivate` yoksa "Engelle" yok). S-34: izin denetimini görünümden kaldır → kırmızı.
- [ ] AC-35 (zorunlu) — Okutma olayları: depo zorunlu, tarih aralığı ≤ 31 gün (fazlası 400), süzgeçler §8.14; KPI kartları okuma anında; ekranda
  GUID yok (raf kodu, lot no, SKU, kişi ve cihaz adı); "çevrimdışı bekleyen" kartı yok (S-1). S-35: 31 gün sınırını kaldır → kırmızı.
- [ ] AC-36 (zorunlu) — Cihazlar ekranı: Sil yok; Engelle gerekçe ister; sağlık rozetleri ikon + metin (yalnız renk değil); anahtar ekranda
  görünmez — r4 (F-2): Web kaydında ve yeniden etkinleştirmede anahtar **hiç** gösterilmez; yerine eşleştirme QR'ı + kod + kalan süre bir kez
  gösterilir (panel kapanınca yok; Web proxy her zaman `keyDelivery = Pairing` gönderir). S-36: anahtarı liste DTO'suna ekle → kırmızı.
- [ ] AC-37 (zorunlu) — Ayar ve PIN sayfaları: ayar formu `If-Match` ile; PIN sayfası yalnız kendi PIN'i, PIN alanı `autocomplete="off"` ve
  proxy PIN gövdesini loglamaz. S-37: proxy'de istek gövdesi loglamasını aç → Web testi kırmızı.
- [ ] AC-38 (zorunlu) — 375 px'te her sayfa okunur, yatay sayfa kaydırması yok; boş / yükleniyor / hata / yetkisiz durumları çizilir (tarayıcı ölçümü).
- [ ] AC-39 (zorunlu) — Her sayfanın yardımı (§25) 7 dilde var; tur yok. S-39: bir sayfanın `Help.*.Purpose` anahtarını sil → kırmızı.
- [ ] AC-40 (zorunlu) — Rol İzinleri: `npx vitest run tests/role-assignments-label-ledger.test.js` (Web) manifest anahtarı bildiren her dilimde
  yeşil; 0182 satır adları 7 dilde (X-10). S-40: bir `Perm.Entity.*` satırını sil → vitest kırmızı.

**DL-3 — WMS işlemlerine bağlama (`scanRef` kapısı)**

- [ ] AC-41 (zorunlu) — Depo `ScanRequiredWarehouseIds`'te değilse ve `scanRef` `scan-session:` önekli değilse (r4) 0178 yerleştirme / toplama
  onayı ve 0181 sayım girişi bugünkü gibidir; dilim
  kabulünde `git diff --stat` 0178 / 0181 test dizinlerinde boş ve o testler yeşil. S-41: kapıyı ayardan bağımsız her depoda çalıştır →
  0178 testleri kırmızı.
- [ ] AC-42 (zorunlu) — Okutma zorunlu depoda: `scanRef`'siz onay 422 `BARCODE_SESSION_REQUIRED`; gerekli adımları `Accepted` / aşılmış oturumla
  onay 200 ve `ScanSessionBinding` **aynı işlemde** (onay geri alınırsa bağlama da yok). S-42a: adım denetiminden `Location`'ı çıkar → kırmızı;
  S-42b: bağlamayı işlem dışında yaz + işlemi düşür → kırmızı.
- [ ] AC-43 (zorunlu) — Aynı oturumla ikinci onay 422 `BARCODE_SESSION_CONSUMED`; eşzamanlı iki onaydan tam biri başarılı. S-43: `SessionId`
  tekil indexini kaldır → eşzamanlı vaka kırmızı.
- [ ] AC-44 (zorunlu) — Oturumda onaylayan kullanıcının olayı yok (başka kullanıcının oturumu), başka görev / satır, `ScanSessionMaxMinutes`'i
  aşmış ya da `Rejected` / aşılmamış `Warning` son olay → 422 `BARCODE_SESSION_INVALID`. **r3 (F-11):** onaylayanın oturumunda (dar yarışla)
  başka kullanıcının `Rejected` olayı bulunsa da onaylayanın kendi olayları geçerliyse onay 200 — başka kullanıcının olayı sayılmaz. S-44:
  kapının kullanıcı süzgecini kaldır → başka kullanıcı vakası ve yarış artığı vakası kırmızı.
- [ ] AC-45 (zorunlu) — 0181 sayım girişi ve bulunan satır aynı kurallarla; kör sayım cevabı değişmez. S-45: 0181 kapı çağrısını kaldır → kırmızı.
- [ ] AC-46 (zorunlu) — WMS-BUNDLE ve WMS-COUNT-BUNDLE'ın eklemeli sürümleri (X-14) `BARCODE_SESSION_*` kodlarını taşır; servis sözleşme
  testleri yeşil. S-46: bir kodu sözleşmeden çıkar → sözleşme testi kırmızı.

**DL-4 — etiket şablonu + basım çekirdeği + lokasyon etiketi + Web ekranları**

- [ ] AC-47 (zorunlu) — Şablon yaratma / düzenleme / pasifleştirme / etkinleştirme; ad tekil (409); tür başına tek etkin varsayılan (ikincisi
  öncekini varsayılandan düşürür, aynı işlemde); silme yok; `HumanReadableFields` türün izinli kümesinden. S-47: varsayılan tekilliğini kaldır →
  kırmızı.
- [ ] AC-48 (zorunlu) — Önizleme PNG + alan listesi döner ve **hiçbir şey yazmaz** (iş, sayaç, merkezi kayıt, dosya sayısı değişmez). S-48:
  önizlemede iş yaz → kırmızı.
- [ ] AC-49 (zorunlu) — Lokasyon etiketi basımı: `LBL` numaralı iş, PDF (etiket boyutunda sayfalar), sha256 saklı; PDF'teki barkod aynı
  kütüphanenin okuyucusuyla çözülünce bin kodunu verir. S-49: barkod içeriğine bin adını yaz → çözüm testi kırmızı.
- [ ] AC-50 (zorunlu) — Yeniden basım: gerekçesiz 422 `LABEL_REPRINT_REASON_REQUIRED`; `Other` açıklamasız 400; `reprintOfJobId` aynı şirket ve aynı
  tür; iş listesinde "yeniden basım" rozeti. S-50: gerekçe denetimini kaldır → kırmızı.
- [ ] AC-51 (zorunlu) — Sunucu yazıcıya bağlanmaz (Q-5 = A, karar 03): 0182 üretim kodunda `Socket`, `TcpClient` ya da yazıcıya `HttpClient` çağrısı yok
  (mimari tarama). S-51: etiket servisine `TcpClient` ekle → kırmızı.
- [ ] AC-52 (zorunlu) — Web "Yeni basım" formunda "Bas" düğmesi önizleme yüklenmeden etkin değildir (E-1); önizleme hatasında basılamaz.
  S-52: düğmeyi her zaman etkin yap → JS testi (vitest) kırmızı.

**DL-5 — GTIN çözümleme + "Bu nedir?" + SSCC + lot / koli etiketleri (kapı: MOD-0290-FU07)**

- [ ] AC-53 (zorunlu) — Ürün çözümleme `identifiers/{gtin}` ile: koli / palet / LSKU GTIN'i doğru SKU'ya; bilinmeyen `UNKNOWN_BARCODE`; FU07
  erişilemez → 503 `BARCODE_GTIN_RESOLUTION_UNAVAILABLE`, olay yok, ürün kabul edilmez. S-53a: 503'ü `Accepted`'a düşür → kırmızı; S-53b:
  çözümlemeyi atlayıp FG `gtin` karşılaştırmasına dön → koli GTIN vakası kırmızı.
- [ ] AC-54 (zorunlu) — "Bu nedir?" GTIN karekoduyla ürün adı, SKU kodu, GTIN, lot, SKT, raf, statü ve geri çağırma uyarısını döner; AC-16'nın kör
  sayım gizliliği bu yolda da geçerli. S-54: GTIN yolunda kör sayım denetimini atla → kırmızı.
- [ ] AC-55 (zorunlu) — SSCC 18 hane, GS1 mod-10 kontrol hanesi doğru; önek ayardan ve FU07 etkin listesinde (değilse 422 `SSCC_PREFIX_NOT_SET`);
  eşzamanlı 20 basım farklı SSCC'ler; sayaç geri gitmez; kapasite dolunca 422 `SSCC_CAPACITY_EXHAUSTED`. S-55a: kontrol hanesini sabit 0 yap →
  kırmızı; S-55b: atomik artırmayı oku-yaz ile değiştir → eşzamanlılık vakası kırmızı; S-55c: önek listesi denetimini kaldır → kırmızı.
- [ ] AC-56 (zorunlu) — 0178 kolisi için SSCC: koli başına bir (tekil kısmi index); aynı koliye yeni basım ve yeniden basım aynı SSCC'yi kullanır;
  0178 `HandlingUnit` değişmez. S-56: koli tekilliğini kaldır → kırmızı. **Q-11 alıcı alanları (r3, MOD-0178-FU01 kararları 01 eki Q-10,
  `a7c1d32b8`):** SSCC etiketinin insan okunur alanlarında alıcıdan yalnız şehir + ilçe; posta kodu yalnız outbound `ShipTo.PostalCode` doluysa;
  `ShipTo.Name`, `Line1`, `Line2` etikette ve PDF metninde yok (PDF metni çıkarılarak ölçülür); ilçe boşsa boş satır / yer tutucu basılmaz.
  S-56b: izinli kümeye `ShipTo.Name`'i ekle → alıcı alanları vakası kırmızı. **r4 (F-6) — ilçe zamanlaması:** DL-5 MOD-0178-FU01'i
  beklemez; FU01 DL-2 `main`'de değilken DL-5 vakası ilçesizdir (etikette ilçe satırı yok; 0182 kodunda `District` başvurusu yok) ve posta
  kodu boş `ShipTo`'da posta kodu satırı yoktur (CRM ana adresi bugün boş gelir). **İlçe vakası** — `ShipTo.District` dolu → etikette basılı,
  boş → basılmaz — ilçe okumasını ekleyen dilimin kabulünde koşar (FU01 DL-2 `main`'e geldikten sonraki ilk 0182 dilimi: DL-5, DL-6 ya da
  DL-7; yoksa FU-0182-DISTRICT). S-56c (ilçe okumasını ekleyen dilimde): ilçe okumasını kaldır → `District` dolu vakası kırmızı.
- [ ] AC-57 (zorunlu) — Lot / koli etiketi: GS1 DataMatrix ve GS1-128 içeriği (01)(17)(10); PDF'ten çözülen değer `Gs1CodeParser`'la ayrıştırılınca
  kaynak GTIN / SKT / lotu verir. S-57: (17) alanını atla → çözüm testi kırmızı.
- [ ] AC-58 (zorunlu) — (00) SSCC okutması koliyi / paleti çözer (X-1; MOD-0174 testleri değişmeden yeşil). S-58: `Gs1Code.Sscc` atamasını kaldır →
  kırmızı. **r3 (F-12):** ham biçimde (37), (3103), (240) taşıyan koli / palet etiketi okunur (bu alanlar yok sayılır, GTIN / lot / SSCC
  çözülür); liste dışı AI (ör. `99`) taşıyan dizge `UNREADABLE` kalır; yalnız toleranslı AI'lardan oluşan dizge okunmaz. S-58b: kapalı listeyi
  "bilinmeyen her AI'yı atla" ile değiştir → liste dışı AI vakası (ve MOD-0174 `TraceDomainRulesTests.cs:147` `"99ABC"`) kırmızı. **r4 (F-7):**
  ham biçimde (00) + (02) + (37) + (10) taşıyan lojistik etiket `Putaway` / `Pick` / `Pack` bağlamlarının `Product` adımında → 201 `Rejected`
  `PRODUCT_NOT_VERIFIABLE`, `overridable = false` — (10) lotu beklenen lotla aynı olsa da **asla `Accepted` değil**; aynı etiket `Pack`'in
  `Package` adımında ve `Query`'de (00) ile koliyi çözer; yalnız (00) taşıyan dizge okunur (X-1'in `:40` değişikliği), yalnız (02) + (37) taşıyan
  dizge `UNREADABLE`. S-58c: `Product` adımında (01) yokken lot eşleşmesiyle `Accepted` dön → lojistik etiket vakası kırmızı.

**DL-6 — RFID toplu okuma (kapı: DL-5 + RFID okuyucu donanım seçimi — Q-7 = A, model TBD)**

- [ ] AC-59 (zorunlu) — RFID taraması: SGTIN-96 / SSCC-96 çözülür; beklenen tarama anında defterden; fark listesi; tarama öncesi ve sonrası defter
  hareket sayısı ve bakiyeler aynı; kör sayım bin'inde beklenen yok (`expectedHidden`). S-59a: tarama içinde bir hareket yaz → kırmızı; S-59b:
  kör sayım denetimini kaldır → kırmızı.
- [ ] AC-60 (zorunlu) — Yeniden tarama `repeatOfSweepId` ile; "Sayım başlat" 0182 yazımı doğurmaz (yalnız 0181 ucu, 0181 izniyle). S-60: 0182'ye
  0181 komutunu doğrudan gönderen yol ekle → mimari tarama kırmızı.
- [ ] AC-61 (zorunlu) — Çözülemeyen EPC `Unknown`, tarama 201; 5.000'den fazla EPC 400. S-61: çözülemeyen EPC'de 400 dön → kırmızı.

**DL-7 — uyum izi + kanıt paketi**

- [ ] AC-62 (zorunlu) — İz lot / seri / SSCC ile: 0182 olayları + 0178 onayları + defter hareketleri zaman sırasıyla; her adımda okutuldu / elle /
  okutmasız; kiracı ve şirket izolasyonu (404); 5.000 adım sınırı 422. S-62: "okutmasız" ayrımını kaldır → kırmızı. **r3 (F-5):** lotun
  bir hareketi açık kör sayımdaki bin'e ise o adım izde ve kanıt paketinde (PDF / CSV) görünür, miktarı `null` + `quantityHidden: true`;
  izdeki lot / seri / SSCC toplamına kör bin katılıyorsa toplam gizli; sayım kapanınca miktarlar görünür. S-62b: iz derleyicisinde kör sayım
  denetimini kaldır → iz ve kanıt paketi vakaları kırmızı.
- [ ] AC-63 (zorunlu) — Kanıt paketi PDF / CSV isteyenin dilinde, insert-only, sha256, 1 merkezi kayıt; dosya indirme `trace.export` ister.
  S-63: dil parametresini yok say → `ar` vakası kırmızı.
- [ ] AC-64 (zorunlu) — `ScanTrace` sayfası AC-31 / AC-33 / AC-34 / AC-39 kapsamında; ekranda GUID yok. S-64: sonuç satırında lot kimliğini göster → kırmızı.

**DL-8 — Gateway kapsama testi (integration-agent)**

- [ ] AC-65 (zorunlu; **kapsama testi DL-8'de; 0178 sayı vakası DL-1, DL-4, DL-6 ve DL-7 kabulünde** — X-12, r3) — `Mod0182BarcodeRouteTests`
  üretim `ocelot.json`'u ve üretim controller kaynağını okur; her 0182 ucu Gateway rotasıyla kapsanır; cihaz başlıkları iletilir; `ocelot.json`
  değişmez. S-65a: `ocelot.json`'da `/api/warehouse/{everything}` rotasını geçici boz → kırmızı (geri alınır); S-65b: başlık iletimini engelleyen
  ayar ekle → kırmızı (geri alınır). **0178 sayı vakası (r3, F-1 — r2'nin "`Mod0178WarehouseRouteTests` değişmeden yeşil" yazımı yanlıştı; X-12,
  XMC-001, sahip onaylı `ad1100158` — r4; r4'te DL-1 16 uç):** 0178 testi 0182 controller'larını da tarar ve `Assert.Equal(<n>, endpoints.Count)` ile sayıyı sabitler. Uç
  ekleyen her dilimin (DL-1, DL-4, DL-6, DL-7) kabulünde Gateway test projesi koşar ve
  `Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route` **dilim başında ölçülen sayı + dilimin
  eklediği 0182 uç sayısı** ile yeşildir; taban dilimin başlangıç commit'inde ölçülür, önce → sonra kabul kaydına yazılır ve kayıt satır
  numarasına değil `Assert.Equal(<n>, endpoints.Count)` satırına atıf yapar; güncelleme dilimin controller uçlarıyla **aynı commit'tedir**;
  `git diff <dilim-başı>..<dilim-sonu> -- gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` yalnız sayı satırı ve üstündeki uç
  yorumudur (integration-agent). **S-65c** (her uç ekleyen dilimde): bir 0182 controller'ına `[HasPermission(BarcodePermissions.ScanEventsRead)]`
  taşıyan geçici `[HttpGet("barcode/count-sabotage")]` eylemi ekle → 0178 testi sayı satırında kırmızı (taranan uç sayısı güncellenmiş değerden
  bir fazla); listede olmayan her kırmızı kabul kaydında adıyla yazılır; geri alınır, commit'e girmez.

## 17. Test Expectations
- **Birim (Domain, saf):** cihaz durum makinesi; bağlam karşılaştırma tablosu (§8.4 her satır); aşılabilirlik matrisi (S-7, Q-3 = A ile daraltılmış: yalnız `Putaway` × `WRONG_LOCATION` `true`, matrisin geri kalanı `false`); SSCC kontrol
  hanesi ve kapasite; EPC çözümü (SGTIN-96 / SSCC-96, GS1 örnek vektörleri); PIN zayıflık kuralları; sağlık hesabı.
- **Entegrasyon (Mongo replika seti):** InventoryService test paketi `INVENTORY_TEST_MONGO` yoksa kendi replika setini başlatır; AC-1…AC-30 gerçek
  MOD-0178 / 0181 / 0174 koduyla (görev, sayım, lot, geri çağırma kurulur, sonra okutulur); DL-3 vakaları gerçek 0178 / 0181 onay işleyicileriyle;
  eşzamanlılık (AC-43, AC-55) iki görevli kancayla; FU07 okuması DL-5'te sahte HTTP işleyiciyle (erişilemezlik vakası) ve pilotta gerçek uçla.
- **Mimari:** AC-6 (`AuthorizationGuardTests`, `WarehouseRegistrationTests` değişmeden; `InventoryLedgerManifestCompletenessTests` yalnız X-15 satırıyla), AC-8 (insert-only), AC-10 (ikinci ayrıştırıcı yok),
  AC-23 (`CentralAuditDeclarationTests`), AC-25 (`LedgerImmutabilityArchitectureTests`), AC-26 (`SingleSystemOfRecordArchitectureTests` + yazma
  taraması), AC-51 (ağ çağrısı yok), AC-60 (0181'e yazma yok). Servise dokunulduğunda **bütün** InventoryService paketi koşar (filtre yok).
- **MOD-0178 / 0181 davranışı:** X-2 / X-3'ün ölçümü 0182 test ağacında; 0178 / 0181 test dosyaları değişmez (AC-41).
- **Sabotaj kanıtı:** AC-1…AC-65'in her bekçisi (bekçi olmayan AC-29, AC-32, AC-38 dışında) §16'daki adlı `S-n` sabotajıyla; kabul kaydında
  komut + kırmızı çıktı + geri alma. Dilim başına sabotaj listesi §24'tedir.
- **Frontend:** `verify_datatable_page.py` (AC-32), RESX parite 7 dil (AC-33), UAS-001 (AC-34), Layout (AC-31), yardım (AC-39), vitest (AC-40,
  AC-52), 375 px ve durumlar (AC-38), tarayıcı smoke (E3): cihaz kaydı (API) → Web'de onay → okutma (API) → olay listesinde görünür → engelle →
  okutma reddi görünür; DL-4'te şablon → önizleme → basım → PDF.
- **Build:** `Diten.InventoryService`, `Diten.Web` PASS; Gateway test projesi uç ekleyen her dilimin kabulünde (DL-1, DL-4, DL-6, DL-7 — AC-65
  0178 sayı vakası, X-12; r3, F-1: `Mod0178WarehouseRouteTests` değişmeden yeşil **kalmaz**, sayı satırı dilimin uçlarıyla aynı commit'te
  güncellenir) ve DL-8'de (AC-65 kapsama testi) PASS.
- **r3 ek mimari / entegrasyon:** X-15 dışlamasıyla `InventoryLedgerManifestCompletenessTests` yeşil + 0182 kendi bütünlük vakası (AC-6, S-6d);
  replay anahtar vakaları (AC-2, AC-9); oturum sahipliği ve replay sırası (AC-9, AC-44); PIN sayacı eşzamanlılığı (AC-18, görevli kanca); kör
  sayım toplamı ve izi (AC-16, AC-62); kapalı AI listesi (AC-58).
- **r4 ek mimari / entegrasyon:** `CentralAuditDeclarationTests` işlem çağrı yeri sayısı ve enlist denetimi (AC-23, S-23b, S-23c); QR
  eşleştirme — tek kullanım, süre (sahte saat), eşzamanlı iki kullanım (görevli kanca), şirket / kiracı 404, depo taraması (AC-3); şirketler
  arası cihaz anahtarı (AC-4); komut idempotency deposu ve aşım replay sırası (AC-9); aşım denetim sırası ve yanlış PIN / kilit yerel izi
  (AC-18); PIN'siz parmak izi ve idempotency depo taraması (AC-19); lojistik etiket ürün adımı (AC-58); ilçe vakası ilçe okumasını ekleyen
  dilimde (AC-56). Yeni koleksiyonlar `BarcodeCollections.All`'da (AC-26, X-5).
- **Sözleşme:** SCAN-BUNDLE taslağına karşı sözleşme testi (AC-1); DL-3'te WMS-BUNDLE / WMS-COUNT-BUNDLE eklemeli sürümleri (AC-46).

## 18. Ready-for-dev Checklist
- [x] Golden Reference Slim referans alındı; en büyük form 8 alan (yeni basım) → slim; sapma G-0182-01 adıyla §10'da
- [x] Frontmatter zorunlu alanları dolu (service, shell, golden_reference, data_mode, screens, entity_base)
- [x] Layout & Shell Contract'ta `_LayoutTenantShell` açıkça yazılı (§9, AC-31 + S-31)
- [x] Backend File Convention Golden Reference ile birebir (§10)
- [x] Frontend File Contract Slim dosya listesi tam; Compact dosyası yok (§11)
- [x] Validation Rules her alan için yazılı (§12)
- [x] Failure Path en az 4 senaryo (duplicate, missing, unauthorized, concurrency) (§13)
- [x] Authorization Convention: namespace önerisi, anahtarlar, politika, aktör (§14)
- [x] Gateway kararı: `ocelot.json` değişmez; integration-agent işi tanımlı (§15, AC-65); 0178 Gateway testinin uç sayısı X-12 ile uç ekleyen her dilimde (r3)
- [x] Acceptance criteria test edilebilir; her bekçi sabotajlı, bekçi olmayanlar adıyla işaretli (§16)
- [x] Audited Events tablosu her yazma komutunu tam bir kez içeriyor (§21)
- [x] Sayfa yardım metni her sayfa için yazılı (§25); tur yok
- [x] Sahip kararları: Q-1, Q-2, Q-3, Q-4, Q-5, Q-7, Q-8, Q-9, Q-10, Q-11, Q-12, Q-15, Q-17 — kararlar 03
  (`docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-03.md`, Beste 2026-11-13, "hepsi öneri gibi"; r2, §19.5); Q-3 genişletme
  teyidi (ek `7f584f546`) ve **Q-6 = ZXing.Net** (ek `bea6662d9`, Ali'ye gitmez) — r3, §19.6
- [ ] RFID okuyucu donanım seçimi (Q-7 = A'nın kalanı; TBD, Beste — **DL-6 kapısı**, paket onayını bekletmez)
- [ ] Ali kararları: Q-13 (şirket bağlamı teyidi), Q-14 (rol şablonu — DL-2 kabul kapısı)
- [ ] CT kararı: Q-16 (okutma olayının merkezi denetim yolu)
- [x] **CT işi (Q-2 = A, Q-3 = A; r2 incelemesi F-8):** mobil not
  `execution/domains/supply-chain-execution/work-packs/mobile/2026-11-12-barcode-scanning/MOBILE-NOTE-2026-11-12-barcode-scanning.md` `:27`, `:40`,
  `:46` (S-7 PIN aşımı yalnız yerleştirmede yanlış raf; farklı lot hiçbir işlemde aşılamaz — Q-3) ve `:106` (cihaz kaydı MOD-0182 — Q-2) ile
  mobil brief `execution/domains/supply-chain-execution/work-packs/mockups/barcode-scanning/BRIEF-MOD-0182-mobile-scanning.md:67` (PIN düğmesi yalnız
  yerleştirmedeki yanlış raf uyarısında) — **CT düzeltti, commit `bf7bd7852`** (r3 HEAD'de satırlar ölçüldü); bu paket mobil nota ve brief'e dokunmaz
- [ ] **`.antigravity/**` kapısı — paket onayından ÖNCE:** PKS-001 §4 `warehouse.barcode.*` satırı + `:92` cümlesi + `:105` eki (Q-1 = A,
  karar 03), RCS-001 §3 `DVC` / `LBL`, aynı adımda X-8 (`RecordCodeTypeCatalogTests`) ve X-9 (MOD-0178 §14.1 notu — 0178'in yeni sürümü ayrıca
  incelenir ve onaylanır) (§6). CT hazırlar, Beste onaylar; bu paket `.antigravity/**`'a yazmaz
- [x] **XMC-001 sahip onayları (r4, F-9):** X-15 (`InventoryLedgerManifestCompletenessTests` `Controllers.Barcode` dışlaması), X-12
  (`Mod0178WarehouseRouteTests` uç sayısı, uç ekleyen her dilim, her artış dahil), X-1 (Sscc + kapalı AI listesi + gs1-check yan etkisi) —
  kararlar 03 eki `ad1100158`; X-6 genişletmesi (`CentralAuditDeclarationTests` işlem çağrı yeri sayısı `:130`) — kararlar 03 eki `4168e7a90`
- [ ] Diğer XMC-001 satırları (X-2…X-5, X-7…X-11, X-13, X-14 ve X-6'nın `:53` / `:60-69` kısmı) sahiplerinden onay aldı (§5.1) — ilgili
  dilim başlamadan
- [ ] **CT işi (r4, F-8 + F-2):** SCAN-BUNDLE taslağına (DL-1, AC-1) ve paket onayından sonra yazılacak mobil sözleşme notuna — şirket başına
  bir cihaz kaydı (ayrı `DeviceId`, ayrı anahtar, `InstallationId` tekilliği şirket kapsamında), Web onayının şirket başına ayrı olduğu, mobilin
  anahtarı (kiracı, şirket) başına saklaması, başka şirketin anahtarıyla 403; QR eşleştirme akışı (`POST /barcode/devices/pair`, 10 dk, tek
  kullanım, `devices.create`). Q-13 ile bağlantılı. Bu paket mobil nota dokunmaz
- [ ] SCAN-BUNDLE taslağı yazıldı (DL-1 ilk adımı, AC-1); dondurma ayrı CT adımı; mobil sözleşme notu paket onayından sonra
- [ ] MOD-0290-FU07 DL-1 `main`'de ve pilotta ölçüldü — **DL-5 başlamadan önce** (§24)
- [x] r2 bağımsız inceleme (CT-APR-02) — `docs/records/reviews/MOD-0182/2026-11-13-7ce2f1cb.md`, KOŞULLU; F-1…F-12 r3'te kapandı (§19.6)
- [x] r3 bağımsız inceleme (CT-APR-02) — `docs/records/reviews/MOD-0182/2026-11-13-11585181.md`, KOŞULLU; F-1…F-9 r4'te kapandı (§19.7)
- [ ] r4 bağımsız yeniden inceleme (CT-APR-02) ve onay satırı (CT-APR-01) — **onaylayan Ali (GitHub `GMG-ERP`)**; yazan ≠ onaylayan

## 19. Implementation Notes

### 19.1 Okunan kaynaklar
- Sahip kararları: `docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-01.md` (tamamı; S-2 `:10`, S-3 `:11`, S-5 `:12`, S-6 `:13`; S-1'in
  2026-11-06 kapanışı `:5`) ve `docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-02.md` (tamamı; S-7 `:8` … S-12 `:13`, E-1 `:14`,
  E-2 `:15`, mockup v2 kabulü `:24`, E-3 `:21`, E-4 `:22`, E-5 `:25`, E-6 `:26`, E-7 `:27`). r2: kararlar 03
  `docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-03.md` (tamamı; Beste 2026-11-13 sohbet cevabı "hepsi öneri gibi").
- Brief'ler: `execution/domains/supply-chain-execution/work-packs/mockups/barcode-scanning/BRIEF-MOD-0182-MOD-0365-barcode-devices.md` ve
  `execution/domains/supply-chain-execution/work-packs/mockups/barcode-scanning/BRIEF-MOD-0182-mobile-scanning.md` (tamamı).
- Mobil not: `execution/domains/supply-chain-execution/work-packs/mobile/2026-11-12-barcode-scanning/MOBILE-NOTE-2026-11-12-barcode-scanning.md`
  (tamamı; §3A `:51`, §3B `:77`, cihaz kaydı cümlesi `:106`).
- Görev Merkezi analizi ve sahip kararı: `docs/records/decisions/2026-11/workcenter-integration-analysis-2026-11-13.md` (tamamı).
- `execution/domains/master-data-management/module-packs/MOD-0290-FU07-product-identifiers-gtin.md` (künye, §0, §1–§3, §14, §16, §23–§25; S-3 `:54`,
  AK-7 `:95`, MOD-0182 tüketici satırı `:328`).
- MOD-0178 (`:211`, `:243`, `:360`, `:1706-1707`, §14.1 `:2128-2152`, FU-0178-SERIAL-01 `:3217-3218`), MOD-0180 (künye, §0–§3, §5–§7, §9–§18,
  §20–§25), MOD-0181 (§0–§4, §8.11–§8.13, `:286`, `:469`, `:1120`, `:1412`) — aynı klasör.
- Kurallar: `.antigravity/rules/module-pack-standard.md` (§6, §7, §10.1, §16), `.antigravity/rules/permission-key-standard.md` (§1, §4, §5),
  `.antigravity/rules/record-coding-standard.md` (§3), `.antigravity/rules/audit-trail-standard.md` (§5, §6), AGENTS.md Çekirdek.
- Blueprint 8.8 `Blueprint_Data` satır 183 ve 364 (xlsx, bu oturumda okundu).
- **r3 girdileri:** bağımsız inceleme r2 `docs/records/reviews/MOD-0182/2026-11-13-7ce2f1cb.md` (tamamı); kararlar 03'ün iki eki (Q-3 genişletme
  teyidi `7f584f546`, Q-6 = ZXing.Net `bea6662d9`); emsal MOD-0180 X-14 satırı ve AC-55 (`execution/domains/supply-chain-execution/module-packs/MOD-0180-wave-planning.md`)
  ile `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md` X-14 ekleri; MOD-0181 paketi `:1376`, `:1392`; mobil not `:27`,
  `:40`, `:46`, `:106` ve mobil brief `:67`, `:75` (commit `bf7bd7852` sonrası); Q-11 güncellemesi:
  `docs/records/decisions/2026-11/mod0178-fu01-outbound-form-v2-owner-decisions-01.md` son ek (Q-10, commit `a7c1d32b8`) ve MOD-0178-FU01 paketi `:139-151`.
- **r4 girdileri:** bağımsız inceleme r3 `docs/records/reviews/MOD-0182/2026-11-13-11585181.md` (tamamı); kararlar 03'ün son iki eki (XMC-001
  onayları `ad1100158`; r3 incelemesi sonrası sahip kararları `4168e7a90`); `docs/records/decisions/2026-11/mod0178-fu01-outbound-form-v2-owner-decisions-01.md`
  ekleri ("sahip cevapları", CRM sorusu 5, "Q-10 düzeltmesi" — `cb612efc9`); MOD-0178-FU01 paketi r2 §4.1a `:156-172` ve `:336-341`;
  çağıran CT'nin iki ek kararı (mobil ekip soruları 2026-11-13: önerisi olmayan yerleştirme görevi, 423 `details.lockedUntil`; SCAN-BUNDLE
  açıklamaları — `DIFFERENT_LOT`, sunucu `deviationReason`, `scanRef` / `sessionId` bağı). Kod (salt okuma, HEAD `db70c9f37`):
  `CentralAuditDeclarationTests.cs:45-69`, `:96-132`; `CentralAuditRecorder.cs:150-170`; `CentralAuditOptions.cs:37-46`;
  `CycleCountSupport.cs:115-150`; `CycleCountRepository.cs` (tek `WithTransactionAsync(` çağrı yeri) ve `CycleCountIndexConfiguration.cs:36-95`;
  `Gs1CodeParser.cs:18-45`, `Gs1Code.cs:1-8`; `ConfirmPutawayTaskHandler.cs:25-95`; `PutawayChecks.cs:51-100`; `InventoryLedgerManifestCompletenessTests.cs:209-226`;
  `Mod0178WarehouseRouteTests.cs:93-97`, `:226-230`.
- **Okunmayan (kısmi inceleme):** mockup v2 HTML dosyaları (`Barkod RFID Prototip v2`, `Mobil Okutma Prototip`) depoda değil; içerikleri yalnız
  kararlar 02'deki CT incelemesi özetinden biliniyor. Mobil ekiplerin D-1…D-5 cevapları (cihaz, kütüphane, RFID SDK) okunmadı — E-7 dışında
  kayıtlı cevap bulunamadı. AuthService `legal_entities` claim'i ölçülmedi (Q-13). Ocelot'un özel başlık iletimi ölçülmedi (§15).

### 19.2 Ölçülen gerçekler
HEAD `90f6a1da8`, salt okuma.
- GS1 ayrıştırıcı MOD-0174'te var; (00) sabit uzunluğu bilinir (`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Traceability/Rules/Gs1CodeParser.cs:24`)
  ama `Gs1Code` (00) taşımaz (`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Traceability/Rules/Gs1Code.cs:8`).
- 0178 onay kanalı `Ui` / `Api` / `External` (`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/Enums/WarehouseTaskEnums.cs:80-85`);
  `scanRef` onay gövdesi üyesi (`services/Diten.InventoryService/src/Diten.InventoryService.Api/Warehouse/WarehouseRequestReader.cs:285`, `:305`);
  0181 `MaxScanRefLength = 200` (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCount/CycleCountContract.cs:62`).
- 0178 toplama onayı kaynak ya da hedef almaz ("a target or a source is NOT a member", `WarehouseRequestReader.cs:304`) — Q-3'ün gerekçesi.
- MOD-0178 `warehouse.*` anahtarları (servis kodundan, 23 anahtar): `activity`, `exceptions`, `integration-monitor`, `location-roles`,
  `outbound-shipments`, `packages`, `pick-tasks`, `putaway-tasks` kaynakları — `warehouse.barcode.` 0 eşleşme.
- RCS-001 §3 TÜR önekleri: APD, ARI, BAC, BNK, CNT, CPS, CTP, CTR, INI, ORG, OUT, PAY, PCK, PFL, PKG, POS, PUT, TTY, WAV — `DVC` / `LBL` yok.
- Barkod / DataMatrix çizim kütüphanesi depoda yok; PDF kütüphanesi var (`services/Diten.InventoryService/src/Diten.InventoryService.Api/Diten.InventoryService.Api.csproj:19`).
- MOD-0290-FU07 kodu HEAD'de ve `origin/main`'de (`5a099cadc`) yok; paket onaylı (`execution/approvals/approvals.tsv:82`).
- Görev Merkezi'nde depo görevi sağlayıcısı yok; uzak sağlayıcı deseni Platform `WorkItemsController` + `WorkAggregation:RemoteProviders`
  (analiz kaydından) — 0182 kullanmaz.
- Gateway `/api/warehouse` ve `/api/warehouse/{everything}` rotaları (`gateway/Diten.ApiGateway/ocelot.json:1134-1142`, `:1153-1161`).
- **r3 ölçümleri (HEAD `bf7bd7852`, salt okuma):** `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:97` `Assert.Equal(73, endpoints.Count)`,
  yorum `:80-96`, tarama `:219-237` (önek `:228`, eylem regex'i `:229`), rota iddiaları `:98-108`, `ocelot.json` bağlı kopya
  `gateway/Diten.ApiGateway.Tests/Diten.ApiGateway.Tests.csproj:26`; `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Api/InventoryLedgerManifestCompletenessTests.cs:209-225`
  (dışlamalar `:213`, `:215`, `:217`, `:219`, `:221`, `:223`; `:229` boş olmayan tarama denetimi); `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CentralAudit/CentralAuditDeclarationTests.cs:53`
  (117), `:54` (her yazma komutunda işaret), `:60-69` (modül listesi); `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit.Abstractions/ICentralAuditDeclaration.cs:32-36`
  (enlist edilmeyen komut kayıt bırakmaz); `Gs1CodeParser.cs:22-27` (bilinen AI'lar), `:40`, `:92`, `:131`, `:133-136`; `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Trace/TraceDomainRulesTests.cs:143-148`
  (okunamayan vakalar, `"99ABC"`); §3 API listesinde dilim başına servis ucu 15 / 11 / 2 / 4.
- **r4 ölçümleri (HEAD `db70c9f37`, salt okuma; `4168e7a90` → `db70c9f37` arasında yalnız inceleme kayıtları değişti):**
  `CentralAuditDeclarationTests.cs:130` `Assert.Equal(14, seen)`, yorum `:127-129` ("9 + RecallDrillRepository … + CycleCountRepository.InTransactionAsync
  (MOD-0181 DL-1, XMC-001)"); sayım Persistence'taki `WithTransactionAsync(` çağrı yerlerini sayar, süpürücü dosyaları (`:96-99`) hariç, her çağrı
  yerinin ilk 1.200 karakterinde `CentralAuditTransaction.EnlistAsync(s` ister; `:51-52` MOD-0181'in 16. komutunun aynı çağrı yerini kullandığını
  yazar ("the callback count below is unchanged"); `CycleCountRepository.cs`'te tek çağrı yeri. `CentralAuditRecorder.cs:160-162`
  (`_requireEnlistment && !pending.OutsideTransaction` → kayıt yok). `CycleCountSupport.cs:132-136` düz SHA-256 parmak izi (adlı alanlar).
  Inventory idempotency deseni: anahtar, işin kendi belgesinde ya da olay belgesinde (`RequestOp` + `IdempotencyKey` tekil index —
  `CycleCountIndexConfiguration.cs:74-77`). `Gs1CodeParser.cs:24` (00) 18, (02) 14 sabit; `:40` en az biri (01) / (21) / (10) / (17);
  `Gs1Code.cs:8` dört alan. `ConfirmPutawayTaskHandler.cs:64` plan ≠ hedef ise gerekçe; `PutawayChecks.cs:51-70` hedef kuralları (etkin, stok
  tutabilen bin / saklama yeri, aynı depo, kaynak değil, `Receiving` / `Staging` değil), `:78` saklama koşulu. Depoda QR / barkod kütüphanesi
  yok (csproj taraması: 0). `scan-session:` öneki depoda kullanılmıyor (0 eşleşme; 0178 testleri `"SCAN-0001"`).

### 19.3 Çıkarımlar
- "RFID tek uç" (2026-11-06) yalnız el okuyucu, kapı / portal okuyucu yok anlamında okundu (brief "RFID kapı okuyucuları ilk sürümde yok") — Q-12;
  **r2: sahip teyit etti (karar 03).**
- E-7 (el terminali alınmayacak) ile S-2 (RFID v1'de) birlikte: RFID okumak için telefona bağlı bir okuyucu (ör. Bluetooth) gerekir — Q-7;
  **r2: yön sahip kararı (karar 03, Q-7 = A); donanım seçimi hâlâ yok (TBD, Beste).** Bu yüzden RFID ayrı dilim (DL-6) ve kapılı.
- Mobil notun "Cihaz kaydı ve engelleme MOD-0365" cümlesi (`:106`) Blueprint'in MOD-0182 SoR'u ("device registrations (if included)") ve brief §4
  (`/Warehouse/Devices` eylemleri) ile çelişir; paket cihaz kaydını MOD-0182'ye yazar — Q-2; **r2: sahip kararı (karar 03, Q-2 = A); mobil not
  CT işiyle düzeltilir (§18), bu paket dokunmaz.**
- İTS'nin MOD-0247'ye girmesi kararlar 01'de çıkarım olarak yazılı; bu paket İTS'ye dokunmaz.

### 19.4 Kararlar ve açık sorular (karar 03 sonrası durum, r2)
"Öneri" sütunu r1'in metnidir; kapanan satırlarda karar bu öneridir (kararlar 03, Beste 2026-11-13, "hepsi öneri gibi").

| # | Soru (sade) | Öneri | Seçenekler | Kim | Durum |
|---|---|---|---|---|---|
| Q-1 | İzin anahtarları hangi ad alanında olsun? | **A** `warehouse.barcode.*` — `warehouse.*` altında MOD-0182'ye ayrılmış alt ad alanı (MOD-0180'in `warehouse.waves.*` deseni; taban yol `/api/warehouse/barcode`) | A · B `barcode.*` (MOD-0181'in `cycle-count.*` deseni, MOD-0178 Q-08 önerisi) | Beste | **kararlaştı — A** (karar 03, 2026-11-13); `.antigravity` kapısı (§6) paket onayından önce **açık** |
| Q-2 | Cihaz kaydı (kayıt, onay, engelleme, sağlık) hangi modülün? | **MOD-0182** (Blueprint SoR "device registrations"); MOD-0365 yalnız dış sağlayıcı profili; mobil not `:106` düzeltilir | A 0182 · B 0365 (Platform'da cihaz kaydı, 0182 okur) | Beste | **kararlaştı — A, MOD-0182** (karar 03, 2026-11-13); mobil not `:106` düzeltmesi CT işi (§18) |
| Q-3 | Toplamada "farklı lot" ya da "yanlış raf" PIN'le aşılınca ne olsun? 0178 toplama onayı bugün yalnız tahsis edilen bin ve lotu kabul eder | **A** v1'de toplamada bu ikisi aşılamaz ("Sorumluya başvurun — tahsis değişmeli"); PIN aşımı yerleştirmedeki yanlış raf için geçerli (0178'in sapma yolu var); farklı lot aşımı tahsis değişimi gelene kadar yok | A · B 0178'e lot / bin değiştirme eklenir (XMC, büyük iş, FU-0178) | Beste | **kararlaştı — A** (karar 03, 2026-11-13); S-7 toplama için daraltıldı (§8.4, §8.5, AC-12); FU-0178-LOTSWAP; **genişletme teyidi** (karar 03 eki `7f584f546`, r3): farklı lot yerleştirmede de aşılamaz (AC-11, S-11b) |
| Q-4 | Sorumlu PIN'i nasıl çalışsın? | PIN 0182'de özetiyle; kişi kendisi belirler (izni varken); 90 gün geçerli; 3 yanlışta 5 dk kilit; yetki geri alınırsa PIN süresi dolana kadar geçerli kalır (bilinen sınır) | A öneri · B sorumlu kendi cihazından / oturumundan onaylar (PIN yok) | Beste | **kararlaştı — A** (karar 03, 2026-11-13) |
| Q-5 | Etiket yazıcıya nasıl ulaşsın? | **A** sunucu PDF üretir; Web tarayıcının, mobil Android'in yazdırma penceresiyle basar (yazıcı her basımda seçilir); RFID etikete yazma v1'de yok | A · B sahada küçük yazdırma ajanı (MOD-0365) — ağ yazıcısına ZPL · C sunucudan doğrudan ağ yazıcısına (bulutta çalışmaz) | Beste | **kararlaştı — A** (karar 03, 2026-11-13) |
| Q-6 | Barkod çizim kütüphanesi | ZXing.Net (Apache-2.0) — Code 128, GS1-128, DataMatrix; lisans ve güvenlik taraması DL-4 öncesi | ZXing.Net · başka kütüphane · kendi kodlayıcımız | Beste (r2'de Ali yazılıydı; sahip "Ali'ye sorma, biz karar veririz") | **kararlaştı — ZXing.Net** (karar 03 eki `bea6662d9`, 2026-11-13; Apache-2.0, ücretsiz); güvenlik açığı taraması DL-4 kabul kaydında (§7) |
| Q-7 | RFID'yi hangi cihazla okuyacağız? (el terminali alınmayacak) | Android telefona bağlı RFID okuyucu (ör. Bluetooth) — cihaz seçimi mobil ekiple; karar gelene kadar DL-6 başlamaz | A telefon + bağlı okuyucu · B RFID v1'den çıkar (FU) | Beste + mobil ekip | **yön kararlaştı — A** (karar 03, 2026-11-13); **donanım seçimi açık — TBD, sahibi Beste** (DL-6 kapısı) |
| Q-8 | Yeni cihaz kaydı onay beklesin mi? (MS-2) | **A** evet — `Pending`, Web'den onay | A · B kendiliğinden etkin | Beste | **kararlaştı — A** (karar 03, 2026-11-13) |
| Q-9 | "Okutmadan onay verilemez" kuralı nasıl açılsın? | depo başına ayar, varsayılan kapalı; pilot depoda açılır | A öneri · B şirket geneli · C her zaman açık | Beste | **kararlaştı — A** (karar 03, 2026-11-13) |
| Q-10 | SSCC nelere verilsin? | 0178 kolisine bir SSCC + içeriksiz serbest palet etiketi; koli → palet birleştirmesi MOD-0246'ya; uzantı hanesi 0 | A öneri · B palet içeriği de 0182'de (kapsam büyür) | Beste | **kararlaştı — A** (karar 03, 2026-11-13) |
| Q-11 | SSCC etiketinde alıcı bilgisi | yalnız şehir ve posta kodu; kişi / müşteri adı yok (mobil brief öneri) | A öneri · B tam sevk adresi | Beste | **kararlaştı — A** (karar 03, 2026-11-13); **r3 güncellemesi** (MOD-0178-FU01 kararları 01 eki Q-10, `a7c1d32b8`; Beste 2026-11-13): posta kodu CRM'den boş gelebilir → alıcıdan **şehir + ilçe**, posta kodu yalnız sevkiyat kopyasında varsa; kişi / müşteri adı ve açık adres yine yok (§8.9, AC-56, S-56b); **r4 hizalaması** (`cb612efc9`): CRM ana adrese posta kodu ekliyor, eklenene kadar boş; adres listesi + `customerAddressId`; DL-5 FU01'i beklemez, ilçe FU01 DL-2'den sonra (S-56c) |
| Q-12 | "RFID tek uç" ne demek? | yalnız el okuyucu; kapı / portal okuyucu yok | teyit | Beste | **kararlaştı — teyit** (karar 03, 2026-11-13) |
| Q-13 | Mobilde şirket bağlamı `legal_entities` claim + `X-Legal-Entity-Id` ile mi? | evet (E-7); Auth teyidi | teyit | Ali | açık |
| Q-14 | Rol şablonları (§14) | Depo çalışanı, Vardiya sorumlusu, Cihaz sorumlusu, Etiket sorumlusu, Kalite / denetçi | ayrı WP | Ali | açık |
| Q-15 | Build mi, Buy mı? (Blueprint `Buy/Partner`) | **Build** (2026-11-06 "sağlayıcıdan bağımsız"; 0178 / 0180 / 0181 emsali) | Build · Buy | Beste | **kararlaştı — Build** (karar 03, 2026-11-13) |
| Q-16 | Her okutma merkezi denetime gitsin mi? | r1: evet (yol b); hacim ölçülünce yol c (okutma olayı defteri) değerlendirilir | b · c | CT (+ Ali) | açık |
| Q-17 | Etiket boyutları | 100×150, 100×50, 50×25 mm sabit liste | liste | Beste | **kararlaştı — liste** (karar 03, 2026-11-13) |

### 19.5 r1 → r2 kapanış tablosu

Girdi: kararlar 03 (`docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-03.md`). Hedef: r1, commit `dbbb8dd04`, LF sha256 `26b4b4a2…`.

| # | Karar | r2 değişikliği | Bölüm |
|---|---|---|---|
| Q-1 | A — `warehouse.barcode.*` | "öneri A" → karar; B seçeneği "seçilmedi"; PKS-001 önerilen satırına karar kaydı; `.antigravity` kapısı ve X-8 / X-9 **onaydan önce açık** kaldı | künye, §5.1 X-9, §6, §8.13, §14, §18, §22, §24 |
| Q-2 | A — cihaz kaydı MOD-0182'de | kapsam satırına karar; mobil not `:106` düzeltmesi CT işi olarak §18'e (paket mobil nota dokunmaz) | künye, §2.1, §18, §19.3 |
| Q-3 | A — toplamada farklı lot / yanlış raf PIN'le de aşılamaz; lot değişimi FU | **S-7 daraltıldı:** v1'de aşılabilir tek durum `Putaway` × `WRONG_LOCATION`; `Pick`'te iki neden ve her bağlamda `DIFFERENT_LOT` geçerli PIN'le 422 `BARCODE_OVERRIDE_NOT_ALLOWED`; `Putaway` farklı lot satırı açıkça yazıldı; AC-12'ye 422 vakası, AC-11'e `Putaway` farklı lot vakası (sabotaj adları aynı: S-11, S-12); yeni hata yolu satırı; FU-0178-LOTSWAP koşulsuz | künye, §2.1, §4.2, §8.4, §8.5, §8.16, §13, §16 AC-11 / AC-12, §17, §20 |
| Q-4 | A — 90 gün, 3 yanlışta 5 dk kilit | varsayılanlar karar olarak işaretlendi; bilinen sınır ve FU-0182-PIN-LIVE değişmedi | §4.3, §4.4, §8.5, §14 |
| Q-5 | A — sunucu PDF, Web tarayıcı / Android yazdırma penceresi | "öneri A" → karar; MOD-0365 sınırında yazdırma ajanı v1 dışı; FU-0182-PRINT-AGENT yeniden yazıldı | künye `dependencies`, §2.2, §7, §8.9, AC-51, §20 |
| Q-7 | A — telefona bağlı okuyucu; donanım kararı gelene kadar DL-6 yok | DL-6 kapısı "donanım seçimi (TBD, Beste)" olarak yazıldı; donanım seçimi **açık** kaldı | künye, §4.1, §8.10, §16 DL-6 başlığı, §18, §19.3, §23, §24 |
| Q-8 | A — yeni cihaz onay bekler | "Q-8 = B seçilirse" cümlesi kaldırıldı; kayıt her zaman `Pending` | §2.1, §8.2, AC-2 |
| Q-9 | A — depo başına, varsayılan kapalı | karar olarak işaretlendi | §4.4, §8.7, §24 |
| Q-10 | A — 0178 kolisi + içeriksiz palet etiketi; birleştirme MOD-0246 | karar olarak işaretlendi | §2.2, §4.4, §8.8, §24 |
| Q-11 | A — alıcıdan yalnız şehir ve posta kodu | izinli kümede kişi / müşteri adı ve açık adres olmadığı yazıldı | §8.9 |
| Q-12 | teyit — yalnız el okuyucu | çıkarım teyitli olarak işaretlendi | §2.2, §4.1, §8.10, §19.3 |
| Q-15 | Build | W-0182-01 "Öneri" → "Karar"; FU-0182-EA (Blueprint düzeltmesi) kaldı | §0, §20 |
| Q-17 | 100×150, 100×50, 50×25 mm | "Q-17 önerisi" → karar | §4.6 |

Açık kalan (r2 kapatmaz): Q-6, Q-13, Q-14 (Ali); Q-16 (CT); Q-7 donanım seçimi (Beste); `.antigravity` kapısı (onaydan önce); mobil not `:106`
(CT işi); XMC-001 X-1…X-14 onayları; MDM MOD-0290-FU07 (DL-5 kapısı). AC sayısı 65, yazma komutu 18 (r3: yanlış sayım, doğrusu 16 — F-6), sabotaj listesi değişmedi.

### 19.6 r2 → r3 kapanış tablosu

Girdi: bağımsız inceleme r2 `docs/records/reviews/MOD-0182/2026-11-13-7ce2f1cb.md` (KOŞULLU; hedef r2 LF sha256 `7ce2f1cb…`) ve kararlar 03'ün iki eki
(`7f584f546`, `bea6662d9`). Seçimler CT önerisidir; sahibe yeni soru açılmadı. Sahip onayı bekleyen: X-12, X-15 (XMC-001) ve X-1'in genişleyen kapsamı (r4: üçü de onaylandı — `ad1100158`).

| # | Bulgu (özet) | r3 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | X-12 / AC-65 0178 Gateway testinin değişmediğini söylüyor; `:97` sayıyı 73'e sabitliyor, DL-1 ile kırmızı (ORTA) | X-12 **XMC-001 satırı** oldu (MOD-0180 X-14 deseni): yalnız `Assert.Equal(<n>, endpoints.Count)` satırı ve yorumu; uç ekleyen her dilim (§3'ten doğrulandı: DL-1 15, DL-4 11, DL-6 2, DL-7 4; DL-2, DL-3, DL-5, DL-8 0) dilim başında ölçülen sayı + kendi uçlarıyla, uçlarıyla aynı commit'te; kabul kaydı önce → sonra, `Assert.Equal` satırına atıf; integration-agent; MOD-0180 ile sıra: hangisi önce gelirse dilim başında ölçülen sayıdan devam; **sahip onayı bekliyordu — r4: onaylandı `ad1100158`du — r4: onaylandı `ad1100158`**. AC-65'e 0178 sayı vakası + S-65c; §15 paragrafı; §17 Build | §3, §5.1 X-12, §6, §7, §15, §16 AC-65, §17, §18, §24 |
| F-2 | `InventoryLedgerManifestCompletenessTests` dışlama listesi X listesinde yok (ORTA) | Yeni **X-15** (MOD-0173, DL-1; ölçüm `:209-225`, dışlamalar `:213-223`; MOD-0181 `:223` emsali, 0181 paketi `:1392`); karşılığında AC-6'ya 0182 kendi bütünlük vakası + S-6d; **sahip onayı bekliyordu — r4: onaylandı `ad1100158`du — r4: onaylandı `ad1100158`** | §3, §5.1 X-15, §16 AC-6, §17, §18, §24 |
| F-3 | Aşım ucunun `HasPermission` anahtarı yazılmamış (ORTA) | Uç `warehouse.barcode.scans.create` ister (çalışanın JWT'si; sorumlunun yetkisi PIN kaydından; `approve` seçilseydi çalışan 403 alırdı; ayrı anahtar güvenlik kazancı olmadan manifest / köprü işi açardı); olay çağıranın değilse 404. AC-18'e vaka + S-18d, S-18e | §3, §8.5, §13, §14, §16 AC-18 |
| F-4 | Cihaz anahtarı ile idempotency replay çelişkisi (ORTA) | Idempotency kaydı anahtarsız cevabı saklar; replay ilk cevabı `deviceKey`'siz + `deviceKeyNotice = BARCODE_DEVICE_KEY_NOT_REPLAYED` döner, yeni anahtar üretmez; kurtarma var olan geçişlerle (yeni geçiş yok). AC-2 (S-2c) ve AC-9 (S-9b) | §4.1, §8.2, §8.15, §13, §16 AC-2 / AC-9 |
| F-5 | Kör sayım lot / SKU toplamlarına ve ize uygulanmıyor (ORTA) | §2.3 kural 4'e (a) toplam **bütünüyle** gizlenir (kör bin dışlanıp kalan gösterilmez — fark sızdırır), (b) iz adımı görünür, miktarı gizli; kanıt paketi aynı derleyiciyle; (c) sayım kapanana kadar, kullanıcıya göre ayrılmaz. AC-16 (S-16b), AC-62 (S-62b) | §2.3, §8.4, §8.11, §8.15, §13, §16 AC-16 / AC-62 |
| F-6 | Yazma komutu sayısı 18 değil 16 (DÜŞÜK) | Künye, r2 notu, §3, §19.5 düzeltildi; X-6'ya dilim başı artışlar (DL-1 +9, DL-4 +5, DL-6 +1, DL-7 +1), testin `:54` işaret kuralı ve `:60-69` modül listesi; nabız işaret taşır ama enlist etmediği için kayıt doğurmaz (§8.12, §21); AC-23 aritmetiği | künye, §3, §5.1 X-6, §8.12, §16 AC-23, §19.5, §21 |
| F-7 | Yerleştirmede farklı lotun sabotajı ve üç Outcome yok (DÜŞÜK) | §8.4: `Pick` `WRONG_LOCATION` / `DIFFERENT_LOT` ve `Putaway` `DIFFERENT_LOT` **`Rejected`** (ayrıca `Count` / `Pack` uyuşmazlıkları `Rejected`); AC-11'e farklı lot + 422 ve **S-11b**; AC-12 Outcome | §8.4, §16 AC-11 / AC-12 |
| F-8 | Mobil not `:27`, `:40`, `:46` ve brief `:67` Q-3 öncesi (DÜŞÜK) | CT düzeltti (`bf7bd7852`; r3'te satırlar ölçüldü); §18 CT maddesi bu satırları sayar ve kapandı; paket mobil nota / brief'e dokunmadı | §18, §19.1 |
| F-9 | Q-3 teyidi ve Q-6 = ZXing.Net işlenmemiş (DÜŞÜK) | Q-6 kapandı (Ali'ye gitmez): künye, §7, §18, §19.4, §24 DL-4; Q-3 teyidi §8.4, AC-11, §19.4 | künye, §7, §8.4, §16 AC-11, §18, §19.4, §24 |
| F-10 | Nabız ve PIN sayacı If-Match / atomiklik (DÜŞÜK) | §12'ye iki adlı muafiyet; nabız ve sayaç tek belgelik `findOneAndUpdate`, `Version`'ı artırmaz; sayaç aşım işleminin dışında (422'de kalır); kilit eşiği koşullu ikinci atomik yazım. AC-18 (S-18f, S-18g), AC-20 (S-20b) | §4.3, §8.2, §8.5, §12, §13, §16 AC-18 / AC-20 |
| F-11 | Oturum ve replay kenar durumları (DÜŞÜK) | Sıra: kimlik 403 → replay araması → cihaz durumu → oturum sahipliği (başka kullanıcının oturumu 422 `BARCODE_SESSION_INVALID`) → doğrulama; DL-3 kapısı yalnız onaylayanın olaylarını sayar (yarış artığı zararsız). AC-9 (S-9c, S-9d), AC-44 (S-44 metni) | §4.2, §8.3, §8.7, §8.15, §13, §16 AC-9 / AC-44 |
| Q-11 (ek) | Sahip kararı, inceleme bulgusu değil: posta kodu CRM'den boş gelebilir (MOD-0178-FU01 kararları 01 eki Q-10, `a7c1d32b8`; Beste 2026-11-13: CRM ana ve ek teslimat adreslerine posta kodu alanı eklenecek, eklenene kadar boş) | SSCC etiketinde alıcıdan şehir + ilçe, posta kodu yalnız sevkiyat kopyasında varsa; kaynak `ShipTo.City` / `District` (FU01) / `PostalCode`; ad ve açık adres yok. AC-56'ya vaka + **S-56b**; §7'ye MOD-0178-FU01 (SOFT, ilçe alanı) | §7, §8.9, §16 AC-56, §19.4, §24 |
| F-12 | Bilinmeyen AI bütün dizgeyi reddediyor (DÜŞÜK) | En dar yol: X-1'e **kapalı** "okunur, kullanılmaz" listesi — (37), (310n), (240); liste dışı AI reddedilir (MOD-0174 `"99ABC"` vakası yeşil); DL-5'e kadar bilinen sınır; FU-0182-GS1AI. MOD-0174 sahibinin X-1 onayı bunu da kapsar. AC-58 + S-58b | §5.1 X-1, §8.3, §16 AC-58, §20 |

AC sayısı 65 (AC-1…AC-65, numaralar değişmedi). Yazma komutu 16. Yeni sabotajlar: S-2c, S-6d, S-9b, S-9c, S-9d, S-11b, S-16b, S-18d, S-18e,
S-18f, S-18g, S-20b, S-56b, S-58b, S-62b, S-65c (§16 ve §24 iki yönde). Açık kalan: Q-13, Q-14 (Ali); Q-16 (CT); Q-7 donanım seçimi (Beste);
`.antigravity` kapısı (onaydan önce); XMC-001 X-1…X-15 onayları; MDM MOD-0290-FU07 (DL-5 kapısı); r3 bağımsız yeniden inceleme.

### 19.7 r3 → r4 kapanış tablosu

Girdi: bağımsız inceleme r3 `docs/records/reviews/MOD-0182/2026-11-13-11585181.md` (KOŞULLU; hedef r3 LF sha256 `11585181…`), kararlar 03'ün
son iki eki (`ad1100158`, `4168e7a90`) ve MOD-0178-FU01 kararları 01 "Q-10 düzeltmesi" eki (`cb612efc9`). Seçimler CT önerisidir; sahibe yeni
soru açılmadı.

| # | Bulgu (özet) | r4 değişikliği | Bölüm |
|---|---|---|---|
| F-1 | `CentralAuditDeclarationTests.cs:130` işlem sayısı X-6'da yok (ORTA) | X-6'ya (b) işlem çağrı yeri sayısı: dilim başında ölçülen + dilimin yeni `WithTransactionAsync(` çağrı yerleri, aynı commit, `:127-129` yorumunda dilim adı, kabul kaydı önce → sonra, `Assert.Equal(<n>, seen)` atfı; işlem içinde yazan dilimler §3 / §21'den doğrulandı (DL-1, DL-4, DL-5, DL-6, DL-7; DL-2 / DL-3 / DL-8 açmaz; nabız, PIN sayacı ve deneme izi işlem açmaz); öneri tek ortak yardımcı (MOD-0181 emsali) → beklenen DL-1 +1, sonra 0; MOD-0180 X-10 ile sıra; sahip onayı `4168e7a90`. AC-23'e vaka + **S-23b** (enlist etmeyen 0182 işlemi) ve **S-23c** (sayı) | §5.1 X-6, §8.5, §16 AC-23, §17, §21, §24 |
| F-2 | Yeniden etkinleştirilen / Web'den kaydedilen cihazın anahtarı telefona ulaşmıyor (ORTA) | Sahip kararı (`4168e7a90`): Web kaydı (`keyDelivery = Pairing`) ve yeniden etkinleştirme anahtar üretmez, `KeyHash` null (eski anahtar 403), 32 baytlık tek kullanımlık eşleştirme kodu (10 dk) + QR (ZXing.Net, DL-1'e çekildi); `POST /barcode/devices/pair` (çalışanın JWT'si + `devices.create` + kod + aynı `InstallationId`) atomik tüketimle anahtarı bir kez döner; kod ve anahtar yalnız özet; kullanılmış / bilinmeyen / başka şirket 404, süresi dolmuş 422 `BARCODE_PAIRING_EXPIRED`, uyuşmazlık 422 `BARCODE_PAIRING_INVALID`; replay anahtarsız / kodsuz; ayrı "yeni kod" ucu yok (FU-0182-PAIR-REISSUE). İzin gerekçesi §8.2. Yeni komut `PairScanDeviceCommand` → yazma komutu 17 (DL-1 10); DL-1 uç 16, toplam 33 (X-12: 73 → 89 → 100 → 102 → 106, çıkarım; onay "her artış dahil"). AC-3'e vaka + **S-3b** (iki kez kullanım), **S-3c** (süre), **S-3d** (düz metin); AC-36 metni | künye, §3, §4.1, §4.10, §5.1 X-6 / X-12, §7, §8.2, §8.12, §8.14, §8.15, §11, §12, §13, §14, §15, §16 AC-1 / AC-3 / AC-23 / AC-36 / AC-65, §21, §24, §25 |
| F-3 | PIN idempotency parmak izine girebiliyor (ORTA) | PIN taşıyan gövdelerde (`OverrideScanCommand`, `SetSupervisorPinCommand`) `pin` parmak izi alan listesinde yok (yer tutucu da yok); idempotency deposunda PIN ya da türevi yok; saklanan cevap sırsız DTO tipi; aynı anahtar + yalnız PIN'i farklı gövde = replay. AC-19'a vaka + depo taraması + **S-19c**, **S-19d** | §4.3, §8.1, §12, §13, §16 AC-19 |
| F-4 | Okutma dışı komutların idempotency deposu yazılı değil; aşım replay'i 409 alabilir (DÜŞÜK) | `wms_barcode_command_requests` (`BarcodeCommandRequest`, §4.11; tekil `(TenantId, LegalEntityId, ActorUserId, RequestOp, IdempotencyKey)`; komutun işleminde; `BarcodeCollections.All` — X-5); replay araması varlık, `If-Match`, durum ve tekillik denetimlerinden önce; nabız adlı muafiyet. AC-9'a **S-9e** (aşım replay sırası), **S-9f** (kayıt yazımı) | §3, §4.10, §4.11, §5.1 X-5, §8.1, §8.2, §8.5, §12, §13, §16 AC-9 |
| F-5 | Aşım denetim sırası yok; yanlış PIN / kilit iz bırakmıyor (DÜŞÜK) | Sıra: 403 → 400 → replay → 404 sahiplik → 422 aşılamaz → 422 öz-aşım → 409 aşılmış → 422 PIN yok → 423 kilit → PIN; PIN yalnız çağıranın aşılabilir, aşılmamış olayında denenir. Yerel iz `ScanOverrideAttempt` (`wms_barcode_override_attempts`; `PinInvalid` / `LockSet` / `Locked`), işlem dışı; ScanEvents ayrıntısı, Device Health "PIN kilidi" rozeti ve ayrıntısı, PIN sayfasında son kilit; merkezi kayıt yok (FU-0182-PIN-AUDIT, Q-16). AC-18'e vaka + **S-18h** (sıra), **S-18i** (iz) | §3, §4.3, §4.10, §8.5, §8.12, §8.14, §11, §13, §16 AC-18, §21 |
| F-6 | İlçe kaynağı FU01'e bağlı; posta kodu ifadesi kayıtla farklı (DÜŞÜK) | DL-5 FU01'i beklemez; FU01 DL-2 `main`'de değilken `District`'e başvurulmaz, ilçe basılmaz; ilçe okuması FU01 DL-2 sonrası ilk 0182 dilimine (DL-5 / DL-6 / DL-7; yoksa FU-0182-DISTRICT); ilçe vakası o dilimde (**S-56c**); posta kodu ifadesi `cb612efc9` ekiyle hizalandı (CRM ana adrese alan ekliyor, eklenene kadar boş; adres listesi, `customerAddressId`); FU01 atıfları r2'ye (`:156-172`) | §7, §8.9, §16 AC-56, §19.4, §20, §24 |
| F-7 | (00)+(02)+(37)+(10) lojistik etiketin ürün adımı tanımsız (DÜŞÜK) | `Product` adımında (01) yoksa her bağlamda `Rejected` `PRODUCT_NOT_VERIFIABLE`, aşılamaz, lot eşleşse de asla `Accepted`; `PRODUCT_NOT_VERIFIABLE` iki sonuçla gelebilir (`Outcome` belirler); X-1'in `:40` etkisi açık: yalnız (00) kümeye eklenir, (02) ve toleranslı AI'lar eklenmez. AC-58'e vaka + **S-58c** | §5.1 X-1, §8.4, §8.15, §16 AC-58, §24 |
| F-8 | Şirket değiştiren mobil kullanıcı tanımsız (DÜŞÜK) | §8.2'ye şirket başına kayıt / anahtar / Web onayı kuralı; başka şirketin anahtarı 403; AC-4'e vaka (S-4b kapsar); SCAN-BUNDLE (AC-1) ve mobil sözleşme notu CT işi olarak §18'de; mobil nota dokunulmadı | §8.2, §13, §16 AC-1 / AC-4, §18 |
| F-9 | Kaydedilen sahip onayları işlenmemiş; nabız atfı dolaylı; 5.1 ölçüm HEAD tutarsız (DÜŞÜK) | X-1 / X-12 / X-15 (`ad1100158`) ve X-6 (b) (`4168e7a90`) onaylı olarak işlendi; "sahip onayı bekliyor" ifadeleri kaldırıldı ya da "bekliyordu — r4: onaylandı" yapıldı; §8.12 atfı `CentralAuditRecorder.cs:160-162` + `CentralAuditOptions.cs:39-46`; §5.1 başlığı ölçüm HEAD'lerini ayrı ayrı yazar (r4 `db70c9f37`) | künye, §5.1, §6, §7, §8.12, §15, §16 AC-65, §18, §19.6, §24 |
| CT ek 1 (inceleme dışı) | Mobil ekip sorusu: önerisi olmayan yerleştirme görevi | Öneri yoksa okunan bin 0178 hedef kurallarından geçerse `Accepted`, geçmezse aşılamaz `Rejected` `WRONG_LOCATION`; aşılabilir `Warning` yalnız öneri varken. AC-11'e vaka + **S-11c** | §8.4, §8.16, §16 AC-11 |
| CT ek 2 (inceleme dışı) | Mobil ekip sorusu: kilit süresi | 423 gövdesi `details.lockedUntil` (UTC); deneme sayısı / kalan hak hiçbir gövdede yok. AC-18'e vaka + **S-18j** | §8.15, §8.16, §13, §16 AC-18 |
| CT ek 3 (inceleme dışı) | SCAN-BUNDLE açıklamaları | `DIFFERENT_LOT` = `Rejected`, aşılamaz; yerleştirmede PIN aşımından sonra 0178 `deviationReason`'ı sunucu "Sorumlu onayı — {sorumlu}, {zaman}" olarak yazar, kullanıcıdan gerekçe istenmez; bağ `scanRef = scan-session:{sessionId}`; kapı `scan-session:` önekli `scanRef`'te depo ayarından bağımsız da çalışır (AC-41 metni buna göre) | §8.7, §8.16, §16 AC-41 |

AC sayısı 65 (AC-1…AC-65, numaralar değişmedi). Yazma komutu 17 (DL-1 10, DL-4 5, DL-6 1, DL-7 1). DL-1 servis ucu 16 (toplam 33). Yeni
sabotajlar: S-3b, S-3c, S-3d, S-9e, S-9f, S-11c, S-18h, S-18i, S-18j, S-19c, S-19d, S-23b, S-23c, S-56c, S-58c (§16 ve §24 iki yönde). Yeni
koleksiyonlar: `wms_barcode_command_requests`, `wms_barcode_override_attempts`. Açık kalan: Q-13, Q-14 (Ali); Q-16 (CT); Q-7 donanım seçimi
(Beste); `.antigravity` kapısı (onaydan önce); XMC-001 X-2…X-5, X-7…X-11, X-13, X-14 ve X-6 (a) onayları; MDM MOD-0290-FU07 (DL-5 kapısı);
MOD-0178-FU01 DL-2 (ilçe — kapı değil); CT işi F-8 (SCAN-BUNDLE + mobil sözleşme notu); r4 bağımsız yeniden inceleme.

## 20. Follow-up Items
- **FU-0182-OFFLINE** — çevrimdışı okutma kuyruğu ve yeniden oynatma (Blueprint SCAN-BUNDLE; S-1 v1 dışı).
- **FU-0182-RCS** — `DVC` / `LBL`'nin MOD-0040-FU02 ortak servisine geçişi (FU-0178-RCS-01 ile).
- **FU-0182-PIN-LIVE** — aşımda sorumlunun yetkisinin AuthService'ten canlı okunması (Q-4 sınırı).
- **FU-0182-PRINT-AGENT** — sahada yazdırma ajanı ve ZPL (Q-5 = A ile v1 dışı; ağ yazıcısına doğrudan basım ihtiyacı doğarsa MOD-0365 ile).
- **FU-0182-RFID-ENCODE** — RFID etikete yazma (kodlama).
- **FU-0182-PORTAL** — kapı / portal RFID okuyucuları.
- **FU-0182-WORKCENTER** — Görev Merkezi bağlantısı (MVP-5 sonrası, 2026-11-13 kararı; 0182 tüketici).
- **FU-0182-RECEIPT** — mal kabul bağlamı (MVP-6 netleşince).
- **FU-0182-AI** — Blueprint AI Assist. **FU-0182-EA** — Blueprint Build/Buy düzeltmesi (Q-15).
- **FU-0182-GS1AI** (r3, F-12) — ayrıştırıcının "okunur, kullanılmaz" AI listesinin (37), (310n), (240) ötesine genişletilmesi (ör. (02), (400),
  (410)-(417)); MOD-0174 sahibiyle.
- **FU-0182-PAIR-REISSUE** (r4, F-2) — süresi dolan / kaybolan eşleştirme kodu için engelle → yeniden etkinleştir yerine doğrudan "yeni kod"
  eylemi (yeni komut ve uç; X-6, X-12 sayılarına etkisiyle).
- **FU-0182-PIN-AUDIT** (r4, F-5) — yanlış PIN kilidinin merkezi denetime taşınması (Q-16 ile birlikte CT kararı).
- **FU-0182-DISTRICT** (r4, F-6) — FU01 DL-2 `main`'e 0182'nin son dilimi bittikten sonra gelirse SSCC etiketine ilçe okumasının eklenmesi
  (AC-56 ilçe vakası, S-56c).
- **FU-0178-SERIAL-01** (MOD-0178'in) ve **FU-0181-BARCODE** (MOD-0181'in) — DL-3 bu iki FU'nun okutma tarafını karşılar; seri kısmi yerleştirme
  0178'de kalır.
- **FU-0178-LOTSWAP** (Q-3 = A, karar 03) — toplamada lot / bin değişimi (tahsis değişimi); gelene kadar toplamada farklı lot / yanlış raf
  PIN'le de aşılamaz. MOD-0178'in FU'su olarak sahibine önerilir; MOD-0178 paketine bu paket yazmaz.

## 21. Audited Events

Servisin denetim altyapısı **bugün var**: `tests/architecture/audit-ledger/Diten.InventoryService.md:12` (`inventory-merkezi-iletim | b | işaret |
ICentralAuditDeclaration`). Kayıt ad ve gövde taşımaz; "olay adı" = `RequestType` (komut sınıfı) + `Operation` (Platform enum adı); kategori
`MasterData` (servisin); `SourceModule` = `BARCODE-RFID`. Başarılı ve yazan komut 1 kayıt; replay 0; başarısız 0 (§8.12). `RequireEnlistment`
kapalı-başarısızdır: kayıt yazılamazsa işlem durur (K2 sınıfı komutlar — cihaz anahtarı, engelleme, aşım, PIN — bu yolla korunur).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| RegisterScanDeviceCommand | RegisterScanDeviceCommand + Create | ScanDevice | b | Sonraki: Number, Name, Type, Platform, WarehouseId, Status, `keyDelivery` (anahtar, eşleştirme kodu ve özetleri **yok**) | — |
| UpdateScanDeviceCommand | UpdateScanDeviceCommand + Update | ScanDevice | b | Önceki / sonraki: Name, WarehouseId, Note | — |
| ActivateScanDeviceCommand | ActivateScanDeviceCommand + LifecycleTransition | ScanDevice | b | önceki / sonraki Status, gerekçe (yeniden etkinleştirmede), "anahtar geçersiz kılındı + eşleştirme kodu üretildi" bayrağı ve kodun son geçerliliği (r4; kod ve özeti **yok**) | — |
| PairScanDeviceCommand | PairScanDeviceCommand + Update | ScanDevice | b | r4 (F-2): "anahtar eşleştirildi" bayrağı, `PairedAt`, eşleştiren kullanıcı (JWT), `InstallationId` (anahtar, kod ve özetleri **yok**) | — |
| DeactivateScanDeviceCommand | DeactivateScanDeviceCommand + LifecycleTransition | ScanDevice | b | önceki / sonraki Status, **gerekçe** | — |
| RecordDeviceHeartbeatCommand | — | — | — | yalnız LastSeenAt, AppVersion, BatteryPercent | İ2 — teknik sinyal: cihazın iş kaydını (ad, depo, durum, anahtar) değiştirmez, `Version`'ı artırmaz; yalnız son görülme / sürüm / pil alanlarını yazar. r3 (F-6): `CentralAuditDeclarationTests.cs:54` gereği `ICentralAuditDeclaration` **taşır** (17'ye dahil — r4); kayıt doğurmaz çünkü işlem açmaz ve enlist etmez (`RequireEnlistment`, §8.12) — AC-20 / AC-23 "nabız 0" |
| RecordScanCommand | RecordScanCommand + Create | ScanEvent | b | Sonraki: DeviceNumber, WarehouseId, Source, Context, Outcome, ReasonCode, Parsed özeti, Manual gerekçesi (ham değer olay kaydında) — hacim Q-16 | — |
| OverrideScanCommand | OverrideScanCommand + Execute | ScanOverride (+ ScanEvent) | b | aşılan neden, sorumlu kullanıcı, not (PIN **yok**) | — |
| SetSupervisorPinCommand | SetSupervisorPinCommand + Update | SupervisorPin | b | SetAt, ExpiresAt (PIN ve özeti **yok**) | — |
| UpdateBarcodeSettingsCommand | UpdateBarcodeSettingsCommand + Update | BarcodeSettings | b | önceki / sonraki: §4.4 alanları | — |
| CreateLabelTemplateCommand | CreateLabelTemplateCommand + Create | LabelTemplate | b | Sonraki: Name, LabelType, Size, HumanReadableFields, IsDefault | — |
| UpdateLabelTemplateCommand | UpdateLabelTemplateCommand + Update | LabelTemplate | b | önceki / sonraki: değişen alanlar (varsayılan devri dahil) | — |
| ActivateLabelTemplateCommand | ActivateLabelTemplateCommand + LifecycleTransition | LabelTemplate | b | önceki / sonraki Status | — |
| DeactivateLabelTemplateCommand | DeactivateLabelTemplateCommand + LifecycleTransition | LabelTemplate | b | önceki / sonraki Status | — |
| CreateLabelPrintJobCommand | CreateLabelPrintJobCommand + Create | LabelPrintJob (+ SsccAssignment) | b | Number, LabelType, TemplateId / Version, kaynak sayısı, LabelCount, IsReprint + gerekçe, atanan SSCC'ler, FileSha256 | — |
| RecordRfidSweepCommand | RecordRfidSweepCommand + Create | RfidSweep | b | WarehouseId, LocationId, EPC sayısı, fark özeti | — |
| CreateScanTraceExportCommand | CreateScanTraceExportCommand + Create | ScanTraceExport | b | sorgu anahtarı, biçim, dil, satır sayısı, FileSha256 | — |

**r4 (F-5) — komut olmayan yerel iz:** yanlış PIN, kilit kuruldu ve kilitliyken deneme `ScanOverrideAttempt` yerel izidir (insert-only,
`/Warehouse/ScanEvents` ve `/Warehouse/ScanDevices`'ta gösterilir); başarısız aşım komutu merkezi kayıt doğurmaz (0); kilidin merkezi kayda
taşınması Q-16 ile CT kararı (FU-0182-PIN-AUDIT). PIN sayacı ve iz yazımı işlem açmaz (X-6 işlem sayısına girmez).

**DL-3 kapısı:** `ScanSessionBinding` 0182 komutu değildir; 0178 / 0181 onay komutunun işleminde yazılır ve o komutun merkezi kaydına enlist olur
(MOD-0180 port deseni). **Yazma olmayan:** önizleme (`PreviewLabelQuery`), iz okuması ve dosya indirmeleri okumadır (AUD-001 §7 okuma denetimi v1
dışı). **Kayda yazılmayacak alanlar:** PIN, PIN özeti, tuz, cihaz anahtarı ve özeti (§8.12).

## 22. Record Codes

| Kayıt türü | Sınıf | TÜR öneki | Üretici | Gerekçe |
|---|---|---|---|---|
| Cihaz (`ScanDevice.Number`) | OTOMATİK | `DVC` (öneri; RCS-001 satırı `.antigravity` kapısında — §6) | hedef MOD-0040-FU02; ara MOD-0178 geçici sayacı | engelleme, iz ve destekte anılır; kullanıcı değiştirmez |
| Etiket basım işi (`LabelPrintJob.Number`) | OTOMATİK | `LBL` (öneri; RCS-001 satırı `.antigravity` kapısında — §6) | aynı | yeniden basım ve iz bu numarayla anılır |
| Okutma olayı | — | kod yok | — | yüksek hacim; zaman + kullanıcı + cihaz ile anılır |
| Etiket şablonu | — | kod yok | — | ad şirkette tekil |
| SSCC | GS1 tanımlayıcısı | — | 0182 SSCC sayacı (§8.8) | RCS-001 dışında: biçimi GS1 tanımlar (MOD-0290-FU07 §3'teki GTIN gerekçesi) |
| RFID taraması, iz dışa aktarımı, aşım | — | kod yok | — | zaman + raf / sorgu ile anılır |

Biçim `{ŞİRKET}-{TÜR}-{000001}`, sayaç (kiracı, şirket öneki, tür) başına, sıfırlanmaz, ASCII; kiracı genelinde tekil (RCS-001). `DVC` ve `LBL`'nin
RCS-001 §3 tablosuna yazılması korunan yol değişikliğidir (§6 kapısı, X-8 aynı adımda).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | SCAN-BUNDLE sözleşme taslağı + cihaz kaydı ve yaşam döngüsü + okutma olayı API'si (ayrıştırma, bağlam doğrulaması, elle giriş, PIN aşımı) + ayar + izin, kiracı, denetim, kayıt kodu (arka uç; çekirdek dilim) | Owned Objects, Entity Fields, ScanDevice, ScanEvent, ScanOverride ve SupervisorPin, BarcodeSettings, Indexes, Repo Scope, Protected Paths, Dependencies, Servis ve kapsam, Cihaz kimliği ve yaşam döngüsü, Okutma olayı ve sunucu doğrulaması, Bağlam kuralları, Elle giriş ve sorumlu PIN aşımı, Çevrimiçi kuralı, Merkezi denetim ve yerel iz, Kayıt kodu üretimi, Hata kodları, Mobil sözleşme ve mobil ekip sınırı, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events, Record Codes | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | Web yönetim ekranları: okutma olayları, cihaz sağlığı, ayarlar, sorumlu PIN'i; durumlar; 375 px; RTL; 7 dil; yardım | Layout & Shell Contract, Frontend File Contract, Ekranlar ve Blueprint soft pages, Page Help (UXH-001) | E4 | plan kimliği yok | plan satırı yok |
| DL-3 | WMS işlemlerine bağlama: MOD-0178 yerleştirme / toplama onayı ve MOD-0181 sayım girişi için `scanRef` kapısı, okutma zorunluluğu ayarı | WMS işlemlerine bağlama, ScanSessionBinding, XMC-001 — başka modülün dosyalarına dokunuşlar, Hata kodları, Failure Path to Verify, Audited Events | L3 + E5 | plan kimliği yok | plan satırı yok |
| DL-4 | etiket şablonu + basım çekirdeği (önizleme, PDF, yeniden basım) + lokasyon etiketi + Web ekranları | Etiket şablonu ve basımı, LabelTemplate ve LabelPrintJob, Frontend File Contract, Ekranlar ve Blueprint soft pages, Audited Events, Record Codes | E4 | plan kimliği yok | plan satırı yok |
| DL-5 | GTIN çözümleme + "Bu nedir?" GTIN yolu + SSCC + lot / koli ve SSCC etiketleri (kapı: MOD-0290-FU07 DL-1 main'de) | GTIN çözümleme ve SSCC, SsccAssignment ve SsccCounter, Etiket şablonu ve basımı, Bağlam kuralları, Hata kodları, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-6 | RFID toplu okuma: beklenen / okunan farkı, yeniden tarama, sayım başlatma bağlantısı (kapı: DL-5 + RFID okuyucu donanım seçimi — Q-7) | RFID toplu okuma, RfidSweep, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-7 | uyum izi + kanıt paketi (PDF / CSV) + Web ekranı | Uyum izi ve kanıt paketi, ScanTraceExport, Frontend File Contract, Ekranlar ve Blueprint soft pages, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-8 | yalnız Gateway kapsama testi (integration-agent; DL-1'den sonra; `ocelot.json` değişmez) | Gateway / API Routing Decision | E3 | plan kimliği yok | plan satırı yok |

## 24. Dilim kapıları ve bağlı kararlar

Paket onaylanmadan (künye `approved` / `ready-for-dev` + CT-APR-02 inceleme kaydı + CT-APR-01 onay satırı, onaylayan Ali) hiçbir dilim başlamaz.
**Onaydan önce kapanan kapı (r3'te hâlâ açık):** `.antigravity/**` satırları (Q-1 = A, karar 03: PKS-001 §4 `warehouse.barcode.*` + `:92` / `:105`;
RCS-001 §3 `DVC` / `LBL`) + aynı adımda X-8 (`RecordCodeTypeCatalogTests`) ve X-9 (MOD-0178 §14.1 notu ve 0178'in yeni sürümünün incelemesi +
onayı) (§6).
**XMC-001 sahip onayları (r4, F-9):** X-12 (DL-1, DL-4, DL-6, DL-7; her artış dahil), X-15 (DL-1), X-1 (DL-5) — `ad1100158`; X-6 (b) işlem
çağrı yeri sayısı — `4168e7a90`. Dilim başlamadan sahibinden onay bekleyen: X-2…X-5, X-7…X-11, X-13, X-14 ve X-6 (a).

| Dilim | Başlamadan kapanması gereken kararlar | Seam'ler (XMC-001) | AC | Sabotajlar |
|---|---|---|---|---|
| DL-1 | Q-16 (CT); Q-1, Q-2, Q-3, Q-4, Q-8, Q-15 kapandı (karar 03); X-12, X-15 (`ad1100158`) ve X-6 (b) (`4168e7a90`) sahip onaylı (r4); ilk adım SCAN-BUNDLE taslağı (QR eşleştirme ve şirket başına kayıt kuralı dahil — r4) + ZXing.Net NuGet ekleme ve güvenlik açığı taraması (r4: QR çizimi için DL-4'ten çekildi) | X-4 (`DVC`), X-5 (r4: iki yeni koleksiyon dahil), X-6 (a) +10 ve modül listesi, X-6 (b) işlem çağrı yerleri (öneri +1), X-7, X-12 (+16), X-15 | AC-1…AC-30; AC-65 0178 sayı vakası (r3) | S-1, S-2a, S-2b, S-2c, S-3, S-3b, S-3c, S-3d, S-4a, S-4b, S-5, S-6a, S-6b, S-6c, S-6d, S-7a, S-7b, S-8, S-9, S-9b, S-9c, S-9d, S-9e, S-9f, S-10a, S-10b, S-11, S-11b, S-11c, S-12, S-13a, S-13b, S-14, S-15, S-16, S-16b, S-17, S-18a, S-18b, S-18c, S-18d, S-18e, S-18f, S-18g, S-18h, S-18i, S-18j, S-19a, S-19b, S-19c, S-19d, S-20, S-20b, S-21, S-22, S-23, S-23b, S-23c, S-24, S-25, S-26a, S-26b, S-27, S-28, S-29, S-30, S-65c |
| DL-2 | Q-14 rol şablonu (Ali) — kabul kapısı | X-10 | AC-31…AC-40 | S-31, S-33, S-34, S-35, S-36, S-37, S-39, S-40 |
| DL-3 | Q-9 kapandı (karar 03); **CT adımı: WMS-BUNDLE ve WMS-COUNT-BUNDLE eklemeli sürümleri (X-14) DL-3 başlamadan önce yazılır ve dondurulur**; öneri: DL-5'ten sonra (ürün doğrulaması GTIN'le anlamlı olsun) | X-2, X-3, X-14 (DL-3 0182 komutu ve servis ucu eklemez — X-6, X-12 sayıları değişmez) | AC-41…AC-46 | S-41, S-42a, S-42b, S-43, S-44, S-45, S-46 |
| DL-4 | Q-5, Q-17 kapandı (karar 03); **Q-6 = ZXing.Net kapandı** (karar 03 eki `bea6662d9`, r3) — NuGet DL-1'de eklendi (r4); DL-4'te yalnız Code 128 / GS1-128 / DataMatrix çizimi | X-4 (`LBL`), X-6 (a) +5 (b: yeni çağrı yeri açarsa), X-10, X-12 (+11) | AC-47…AC-52; AC-31…AC-34, AC-39 yeni sayfalar için; AC-65 0178 sayı vakası (r3) | S-47, S-48, S-49, S-50, S-51, S-52, S-65c |
| DL-5 | **MOD-0290-FU07 DL-1 `main`'de ve pilotta ölçüldü** (`identifiers/{gtin}`, `gs1-company-prefixes`); Q-10, Q-11 kapandı (karar 03; Q-11 r3 güncellemesi — şehir + ilçe, `a7c1d32b8`, `cb612efc9`); X-1 sahip onaylı (`ad1100158`); **MOD-0178-FU01'i beklemez** (r4, F-6 — FU01 DL-2 yoksa ilçe basılmaz) | X-1 (Sscc + kapalı AI listesi + `:40`'a (00)), X-13 (DL-5 0182 komutu ve servis ucu eklemez — X-6 (a), X-12 sayıları değişmez; bir uç eklerse X-12 kuralı, yeni işlem çağrı yeri açarsa X-6 (b) kuralı uygulanır) | AC-53…AC-58 | S-53a, S-53b, S-54, S-55a, S-55b, S-55c, S-56, S-56b, S-56c (ilçe okumasını ekleyen dilimde — DL-5, DL-6 ya da DL-7), S-57, S-58, S-58b, S-58c |
| DL-6 | DL-5 kabulü; **RFID okuyucu donanım seçimi** (Q-7 = A'nın kalanı — TBD, Beste); Q-7 yönü ve Q-12 kapandı (karar 03) | X-6 (a) +1 (b: yeni çağrı yeri açarsa), X-12 (+2) | AC-59…AC-61; AC-65 0178 sayı vakası (r3) | S-59a, S-59b, S-60, S-61, S-65c |
| DL-7 | — | X-6 (a) +1 (b: yeni çağrı yeri açarsa), X-10, X-12 (+4) | AC-62…AC-64; AC-31…AC-34, AC-39 `ScanTrace` için; AC-65 0178 sayı vakası (r3) | S-62, S-62b, S-63, S-64, S-65c |
| DL-8 | DL-1 kabulü | gateway test projesi (integration-agent); `ocelot.json` değişmez, sabotajda geçici bozulur ve geri alınır; `Mod0178WarehouseRouteTests` DL-8'de değişmez (sayı satırı uç ekleyen dilimlerde — X-12) | AC-65 kapsama testi | S-65a, S-65b |

Sıra: DL-1 → DL-2 ve DL-4 (paralel) → DL-5 (FU07 kapısı) → DL-3 → DL-6 → DL-7; DL-8 DL-1'den sonra her an. Mobil ekipler DL-1 taslağıyla mock
veriden gerçek uca geçer. MOD-0178 / 0181 bekçi testleri her dilimde değişmeden yeşil (AC-41'in `git diff` kanıtı her dilim kabulünde tekrarlanır).
**Tek istisnalar (r3; r4):** X-12 (Gateway'deki `Mod0178WarehouseRouteTests` uç sayısı satırı ve yorumu, uç ekleyen dilimde, integration-agent), X-15
(`InventoryLedgerManifestCompletenessTests` `Controllers.Barcode` dışlaması, DL-1) ve X-6 (`CentralAuditDeclarationTests` sayı satırları) — üçü de
`INVT/Warehouse/**` / `INVT/CycleCount/**` dışındadır ve sahip onaylıdır (XMC-001; `ad1100158`, `4168e7a90`; X-6 (a) için ayrıca dilim başında).

## 25. Page Help (UXH-001)
- `SCANEVENTS` → `Help.SCANEVENTS.Purpose`, `.Audience`, `.Step1Title`/`.Step1` … `.Step3` (1: depoyu ve tarihi seç — en çok 31 gün, 2: sonuca ya
  da cihaza göre süz, 3: satırı açıp okunan değeri ve bağlı görevi incele); `.Immediate` ("Okutma kayıtları değiştirilemez; aşımlar sorumlunun
  adıyla görünür"); `Related.SCANDEVICES`, `Related.SCANTRACE`.
- `SCANDEVICES` → `Purpose`, `Audience`, `Step1..3` (bekleyen cihazı onayla, sessiz ya da düşük pilli cihazı kontrol et, kayıp cihazı gerekçeyle
  engelle); `.Immediate` ("Engellenen cihazdan okutma reddedilir; yeniden etkinleştirmede telefon yeni anahtarı ekrandaki QR kodu 10 dakika
  içinde okutarak bir kez alır" — r4).
- `BARCODESETTINGS` → `Purpose`, `Audience`, `Step1..2` (eşikleri ayarla, okutma zorunlu depoları seç).
- `SUPERVISORPIN` → `Purpose`, `Audience`, `Step1..2` (PIN'ini belirle — 4–8 hane, kolay tahmin edilen PIN kabul edilmez; süresi dolunca yenile);
  `.Immediate` ("PIN'inizi kimseyle paylaşmayın; aşımlar sizin adınızla kaydedilir").
- `LABELTEMPLATES` (DL-4) → `Purpose`, `Audience`, `Step1..3` (etiket türünü ve boyutu seç, görünecek alanları seç, önizlemeyi kontrol et).
- `LABELPRINTS` (DL-4) → `Purpose`, `Audience`, `Step1..4` (türü ve şablonu seç, kaynağı seç, önizlemeyi kontrol et, PDF'i yazdır); `.Immediate`
  ("Yeniden basım gerekçe ister; SSCC yeniden basımda değişmez").
- `SCANTRACE` (DL-7) → `Purpose`, `Audience`, `Step1..3` (lot, seri ya da SSCC ile ara, adımları incele — okutuldu / elle / okutmasız, kanıt
  paketini indir).
- Kiracı modülü → 7 dil. **Tur yok.**
