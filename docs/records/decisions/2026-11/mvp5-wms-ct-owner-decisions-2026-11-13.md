# MVP-5 WMS — sahip kararları (2026-11-13, Beste; kaynak: sahip sohbet cevapları)

- **Dilim sırası (aynı sayaçlar ve tek çalışma dalı):** MOD-0180 DL-1 → MOD-0182 DL-1 → MOD-0179 DL-1 ("MOD-0179 sırası … onayladım").
  Her dilim bir öncekinin CT doğrulamasından sonra başlar; Gateway uç sayısı, merkezi denetim komut sayısı ve kayıt kodu önek listesi
  dilim başında ölçülür.
- **K-4 — görevlere raf ve ürün kodu ("evet eklensin"):** toplama ve yerleştirme görevlerine (WMS-BUNDLE `WarehouseTask`) raf kodu ve
  ürün kodu / adı eklemeli olarak eklenir (mobil isteği, MOD-0181 K-2 deseni). WMS-BUNDLE'ın eklemeli yeni sürümü; MOD-0178 için ayrı FU
  (kimlik DCP-002 ile). Dil kuralı `skuName` için K-2 ile aynı.
- **MVP-1 hattının main'e PR'ı ("tamam"):** MOD-0180 DL-1 kapanınca hat dalının tek PR'ı açılır (merge commit; onay defterleri main satırları
  önce). PR'ı açma ve merge her biri yine sahip onayıyla.
- **Ali'ye bağlı işler (geliştirmeyi bekletmez, unutulmaz):** Auth AC-76 (MOD-0018-FU31; MOD-0178 canlıya çıkışı), sayım rol şablonu
  (MOD-0181 DL-4 ACCEPTED), MDM kısa kod kilidi (MOD-0040-FU02 DL-2 canlıya alma), sır kasası teknik soruları (MOD-0012-FU01 Q-1…Q-5,
  Q-13). Geliştirme bunları beklemeden sürer; her biri ilgili kabul ya da canlıya alma kapısında kontrol edilir.
- **MOD-0049-FU01 `.antigravity` kapısı ("yaz", Beste):** RCS-001 §3'e `BPT` (İş Ortağı, OTOMATİK, grup önekli, ortak servisten) satırı
  yazıldı; aynı adımda X-6: Platform `RecordCodeTypeCatalogTests` `RuleAheadOfCatalogue["BPT"] = "MOD-0049-FU01 DL-1"` — 17/17 yeşil,
  satır silinince AC16 kırmızı, geri alındı. Bu onay öneki `BPT` olarak seçer (Q-1). Q-2…Q-7 açık.
- **MOD-0049-FU01 Q-2…Q-7 ("hepsinde öneri gibi", Beste):** Q-2 = A (denetim: `mdm-merkezi-iletim` en iyi çaba + aynı işlemde yerel
  geçmiş, Brand emsali); Q-3 = iletişim kişilerine 90 gün kuralı uygulanmaz, en aza indirme + kişi silinince kalıcı silme, v1'de zamanlanmış
  temizlik yok; Q-4 = vergi numarası kiracıda tekil, listede maskeli, ayrıntıda yetkiliye açık; Q-5 = Golden Compact sapmaları paketteki gibi,
  son rol kaldırılamaz (önce pasifleştirme); Q-6 = pasif kayıt salt okunur, yalnız etkinleştir; Q-7 = domain-config'e MDM ekran / Gateway
  istisna satırı eklenir (merkez yazar). Q-1 = `BPT` (yukarıda).
- **MOD-0040-FU02 r14 Q-8…Q-16 ("hepsinde öneri gibi"):** hepsi paketteki A önerisi — Q-8 TTY kataloğa girer; Q-9 oluşturmada elle kod
  gerekçe istemez, yalnız sonraki değişiklik; Q-10 gerekçe 10–500 karakter; Q-11 kırp + Türkçe→ASCII + büyük harf, diğer işaretler red;
  Q-12 elle kod 2–40; Q-13 tekillik (kiracı, tür); Q-14 emekli koda geri dönüş yok; Q-15 DL-3'te HTTP / Gateway / ekran yok; Q-16 tüketici
  tutarlılığı DL-4'te. A-20 (dört denetim belirteci `platform-islem-ici` iz satırına) CT kararı: kabul, DL-3 merge kapısı.
- **MOD-0182 panel penceresi ("öneri gibi"):** dashboard "son 24 saat" (çağrı anından geriye); takvim günü yok, paket değişikliği yok.
  Paket "bugünkü" ifadesi bir sonraki revizyonda (r8) hizalanır.
- **MOD-0049-FU01 C-5 — rol uçları (Beste, "iki komutu v1'den çıkar"):** v1'de tek rol (ThirdPartyLogistics) olduğundan rol ver / rol kaldır
  komutları, uçları ve `mdm.business-partners.assign-role` izni v1'den çıkar; rol oluşturmada verilir. "Son rol kaldırılamaz" (Q-5) alan
  kuralı olarak kalır; ikinci rol eklenince iki komut eklemeli olarak gelir.
- **Düzeltme — MOD-0040-FU02 Q-9 (Beste, 2026-11-13, "A — oluşturmada da gerekçe"):** yukarıdaki Q-9 özeti («oluşturmada elle kod gerekçe
  istemez») CT'nin sahibe sunduğu tablodaki yanlış özetti (B'yi anlatıyordu). Sahip A'yı teyit etti: elle yazılan kod oluşturmada da gerekçe
  ister (RCS-001 §2), her değişiklikte de. r15 (c4499f37) A ile yazılı; paket değişmez. r15 18.1'deki "Q-9 kayıt metni düzeltmesi" işi bu
  satırla kapandı.
- **CT kararı — MOD-0040-FU02 r15 inceleme F-1 (kilit filtresi):** LockIfUnlockedAsync filtresine ön okunan kod eklenir (Code eşit);
  eşleşmezse satır yeniden okunur ve 409 RECORD_CODE_CONFLICT döner — çağıranın görmediği yeni kod kilitlenmez. DL-3 WP'si bunu adıyla
  taşır; AC-70 / AC-72 metni paketin bir sonraki revizyonunda hizalanır (r15 onayı geçersiz olmasın diye).
- **EES 2.14 eki — onay geçmişi başlangıç noktası (Beste, "Başlangıcı taşı", PR #424):** `scripts/pack_approval.py` `HISTORY_BASELINE`
  7e8aa77d8'den (PR #397 merge'ü) MVP-1 hattının GitHub'daki a1aba4066 commit'ine taşındı; altındaki iki sıra-değiştiren merge (11c37f79d,
  727d99414; CT zinciri ↔ hat birleştirmeleri) `BASELINE_REORDERS`'a eklendi. Ölçüm: `history --base origin/main` yalnız bu ikisini
  adlandırıyordu (reviews.tsv; satır kaybı yok, eklenen yok); `git blame` iki defterde de bu merge'lere 0 satır veriyor; değişiklikten sonra
  aynı komut "yalnız ekleme — bozulma yok", çıkış 0. a1aba4066'dan sonraki her ihlal yine ihlaldir. Neden: append-only zorunlu kontrol;
  ortak dalda geçmiş yeniden yazılmaz, istisna dosyası yalnız kayıp satırı affeder. Ders: CT zinciri ↔ hat birleştirmelerinde defterler
  hattın sırasıyla yazılır (ana satırlar önce, sonra hattın satırları hattın sırasıyla).
- **MOD-0182 — sayımda süresi geçmiş / geri çağrılmış lot (Beste, "Kabul + uyarı"):** Count bağlamında LOT_EXPIRED / LOT_RECALLED okutma
  reddedilmez; okutma kabul edilir ve sonuç uyarı taşır (süresi geçmiş / geri çağrılmış). Toplama, paketleme ve yerleştirmede ret aynen kalır.
  Paket metni r8'de (PIN okuma ucuyla birlikte) hizalanır; DL-1 kodu küçük düzeltme (FIX) ile.
- **MOD-0182 DL-1 — iki mimari test istisnası (Beste, geliştirici oturumunda onaylandı, 2026-11-13):** (1)
  `SingleSystemOfRecordArchitectureTests.No_Model_B_Product_type_is_used` — okutma adımı adı "Product" için üç dosyalık istisna; (2)
  `TraceArchitectureTests.Only_LotIdentityPolicy_builds_a_lot_or_serial_key` — `ScanContextReader.cs` "Bu nedir?" lot numarası okuması istisnası.
  Kapsamları CT kabulünde dar olduğu ölçülür.
- **Sahip kararı (Beste, akşam):** MOD-0180 DL-2, MOD-0012-FU01 DL-1, MOD-0049-FU01 DL-1 başlatılır; MOD-0181 1.2.0 ek işi (K-2 / K-3) için
  paket yazılır. MOD-0175-FU01 ACCEPTED (main, PR #424).
- **CT kararları — MOD-0180 DL-2 (WP-0180-DL2 §2):** (1) AC-22 (b)/(c): DL-2'de `Excluded` üyelik ve olay verisi ölçülür, `WaveException`
  kaydı DL-3 kabulünde tamamlanır; (2) AC-15 (k) "sonraki dalgaya taşı" ve geri çekme parçaları DL-3 kabulüne kalır; (3) yarım kalan
  serbest bırakmayı yalnız uzlaştırma döngüsü (`RunReconcileOnceAsync`) tamamlar. Paket metni sonraki revizyonda hizalanır.
- **CT kararı — WMS-BUNDLE sürüm sırası:** sürümler dondurulma sırasıyla numaralanır. 1.2.0 = MOD-0180 dalga üyeleri (X-8 / CT-1). MOD-0178
  ebeveyninde "1.2.0 Item adımı" diye anılan MVP-6 adımı, K-4 (görevlere raf / ürün kodu) ve MOD-0178-FU01 DL-2 `customerRef` sonraki
  eklemeli sürümlerdir; paket metinleri sonraki revizyonlarda hizalanır.
- **MOD-0181-FU01 (sayım sözleşmesi 1.2, K-2 / K-3) — sahip kararları (Beste):** Q-1 = A (dört görünen üye satır açılırken bir kez
  yazılır — kanıt kopyası); Q-2 = MDM `getSku` `name` aynen (tek değerli, dil yok; yoksa üye yazılmaz; K-4 aynı kuralı kullanır); X-1 = evet
  (XMC-001: MOD-0173 `SkuReference.cs` ve `ProductMasterHttpClient.cs`'e eklemeli SKU `name` okuması). CT kararları: Q-3 = A (önce sözleşme
  dondurma, sonra DL-1); Q-4 = sürüm dondurulma sırasıyla numaralanır (WMS-BUNDLE kararı emsal).
- **`skuName` anlamı (Beste, "İkisi de SKU adı"; MOD-0181-FU01 r2 inceleme F-5):** mobil yüzeylerinde `skuName` her yerde PRODUCT-MASTER
  `getSku` `name`'idir (SKU'ya özgü ad): MOD-0181-FU01 sayım satırı, MOD-0182 `ScanResolved` (bugün `getProduct` `name` — FIX-01'de
  hizalanır; SCAN-BUNDLE henüz dondurulmadı) ve K-4 görevleri. Yoksa üye yazılmaz.
- **MOD-0181-FU01 X-3 (Beste, "Evet"):** XMC-001 onayı ortak test dosyası `StubProductMaster.cs`'i de kapsar (eklemeli: SKU `name` ve
  `GetSkuCalls` sayacı; mevcut testlerin davranışı değişmez).

## Ek — MOD-0182 r8 incelemesi kararları (Beste, 2026-11-14)

Kaynak: bağımsız inceleme `docs/records/reviews/MOD-0182/2026-11-14-8e7b465c.md` (KOŞULLU, `3c7a4782f` / `067b6f523`). Sahip (Beste) bu
oturumda seçti:

- **F-1 — son kilit alanı ekle:** `GET .../barcode/supervisor-pin` cevabına sır olmayan `lastLockedUntil?` üyesi eklenir (en son kurulan
  kilidin bitişi; kilit sürse de bitse de). Kapalı anahtar listesi (AC-37 (c)) `setAt?`, `expiresAt?`, `lockedUntil?`, `lastLockedUntil?`
  olur; PIN, özet, tuz, sayaç, `Version` yine yok. `/Warehouse/SupervisorPin` sayfası "son kilit zamanı"nı bu üyeden gösterir.
- **F-2 — X-12 DL-2 onayı:** DL-2'nin tek uçluk Gateway artışı (PIN durumu okuma; değişikliği integration-agent yapar, XMC-001) sahip
  onaylıdır. Önceki onay (`ad1100158`, DL-1 / DL-4 / DL-6 / DL-7) bu kayıtla DL-2'yi de kapsar.
- **F-4 — sayımda iki denetim çalışır:** Count bağlamında `LOT_EXPIRED` / `LOT_RECALLED` uyarısından sonra `LOT_DATA_MISMATCH` ve
  `SERIAL_DUPLICATE` denetimleri yine yapılır (uyarı erken dönüşü kaldırılır); kod düzeltmesi DL-3'te, AC + sabotajla.
- **F-5 — GTIN'siz ürün uyarıyla geçer:** `PRODUCT_NOT_VERIFIABLE` uyarısı DL-3 kapısında geçerli adım sayılır ve olay kaydına yazılır;
  sorumlu aşımı gerekmez. "İstisna yalnız iki koddur" kuralı üç koda genişler.

## Ek — MOD-0182 DL-2 XMC-001 (Beste, 2026-11-14)

- **X-10 onaylı:** `Views/Governance/RoleAssignments/_IndexL10n.cshtml` ve `SharedResource.*.resx` `Perm.Entity.*` — yalnız MOD-0182 varlık
  satır adları, 7 dil, yalnız ekleme, yeni fiil yok.
- **`NavManifestL10nGuardTests` `PagelessUntil["BARCODE-RFID"]` girdisinin silinmesi onaylı** (yalnız o girdi ve iki satırlık yorumu;
  `NavVisiblePageKeyFloor` ve başka satır değişmez). Emsal MOD-0177 R6, MOD-0181 DL-4. WP-0182-DL2 B-1 kapısı kapandı.

## Ek — MOD-0012-FU01 Q-1, Q-2, Q-4, Q-13 ve MOD-0181-FU01 / MOD-0178-FU01 başlatma (Beste, 2026-11-14)

Sahip (Beste) "Ali'den bekleme, soruları bana sor" dedi ve bu oturumda seçti (paket
`execution/domains/platform-shared-services/module-packs/MOD-0012-FU01-tenant-runtime-secret-vault.md` §19 açık sorular):

- **Q-1 — ana KEK yeri:** v1 Platform sunucusunda ortam değişkeni (`TenantSecretVault__Kek__*`, `ISecretsProvider`); anahtarı sistem
  yöneticisi üretir, yalnız sunucu yöneticisi erişir; `IKeyEncryptionKeyProvider` dikişi ileride KMS / HSM'e açık (paket önerisi).
- **Q-2 — yedek / kurtarma:** KEK'in şifreli çevrimdışı yedeği şirket kasasında / parola yöneticisinde iki yetkiliyle saklanır; buna rağmen
  kayıpta kiracılar sırlarını yeniden girer (kabul).
- **Q-4 — servisler arası TLS:** canlıda zorunlu; geliştirme makinesinde zorunlu değil; mTLS sonraki iş (DL-2 canlı kullanım kapısı bu
  kararla tanımlı).
- **Q-13:** FU01 onayı ana MOD-0012 paketinin `review` durumunu beklemez.
- Açık kalan: Q-3 (Inventory servis istemcisi + kiracı grant'leri, X-1 izni) — DL-5 kapısı, DL-5'ten önce sorulur.
- Bu kararlar DL-1 kabul kapısını (Q-1, Q-2, Q-13) kapatır; Q-12 CT tarafından 23f25bca5 ile kapanmıştı.

Ayrıca: **MOD-0181-FU01** — mobil bilinmeyen üye toleransı teyidi beklenmeden CT sözleşme dondurma + DL-1 başlar; teyit kabul kapısı
olarak açık kalır. **MOD-0178-FU01 DL-3** — Q-5 (Ali), XMC-CRM-01 (Beste), Q-6 (Natig) beklenmeden başlar; kapıya bağlı parçalar
geldiğinde ek adım olarak eklenir.

## Ek — MOD-0049-FU01 DL-2 ve MOD-0178-FU01 DL-3 kapıları (Beste, 2026-11-14)

- **MOD-0049-FU01 DL-2 B-1 (X-9b):** `PageHelpResource.{7}.resx` `Help.BUSINESSPARTNERS.*` anahtarları yalnız eklemeyle yazılır — onaylı.
- **MOD-0049-FU01 DL-2 B-2:** ekran brief / mockup şartından **muaf**; ekran mevcut Compact datatable kalıbıyla (verify_datatable
  `--reference compact --data-mode server --api-profile proxy`) yapılır. Muafiyet bu kayıtla.
- **MOD-0178-FU01 DL-3 B-1:** B parçası (CRM teslimat adresi seçicisi, `customer-ship-to` proxy, Q-12 uyarısı) CRM 1.1 donmasını
  beklemeden **taslak r7'ye göre** kodlanır (`docs/analysis/contracts/drafts/crm-account-lookup-v1.1-ship-to-proposal.md`). Riskler kabul:
  donmada taslak değişirse yeniden iş; CRM U-4 ucu yokken canlıda 404 → paketin "kayıt bulunamadı, bağlantı kurulmaz" yolu. Testler CRM
  ikiziyle (taslağın şemasına karşı); canlı AC-31 (a) ve donma uyumu kabul kapısı (G-1) olarak kalır.

## Ek — MOD-0040-FU02 r16 DL-4 kararları Q-17…Q-26 (Beste, 2026-11-14)

Kaynak: paket r16 (`15bc15c34`, LF sha256 fcb6d1f6…) §24.3. Sahip (Beste) tek tek seçti:

- **Q-17 — kapsam:** DL-4 = görev türü (TTY) + abonelik planı (PLN), merkez (biz) yapar. **PPM'i (MOD-0117) biz yapmayız**: PPM sahibi
  geliştirici ekibe bağlanma metni verilir, onlar kendi servisinde yapar (DL-11 bizim tarafta yalnız sözleşme / tüketici bağlama kapısı).
- **Q-18 — A:** kiracısız kod satırları ayrı küresel koleksiyonda (`GlobalEntity`).
- **Q-19 — A:** platform-global denetim mevcut `AuditTenantIds.PlatformSystemTenantId` + `TargetTenantId = null` yolu; yazıcı değişmez.
- **Q-20 — A:** tüketici yazımı kodla aynı işlemde (oturumlu port).
- **Q-21 — C:** TTY kilidi yalnız kod değişikliği anında görev sayımıyla; `CreateTaskItemHandler`'a dokunulmaz.
- **Q-22 — A:** eski kodlar deftere alınmaz ve değişmez; DL-4 yalnız çakışma savunması + canlı veride sayım; defter alımı DL-6.
- **Q-23 — A:** FU-RCS-04 (kodu kayda bağlama / kullanılmayan kod raporu) DL-4 dışında, DL-10'a.
- **Q-24 — B (öneri A'dan farklı):** PLN'de bugünkü davranış korunur — güncelleme kodu değiştirebilir, bugünkü normalleştirici aynen
  (CodeChange ayrı alanı / boşluk-alt çizgi reddi yok). Paket r17'de hizalanır (AC-88 ve ilgili sabotaj).
- **Q-25 — B (öneri A'dan farklı):** grup öneki tanımlı değilse TTY formu kodu zorunlu tutar (kullanıcı elle girer, RCS-001 elle kod
  kuralları ve gerekçesiyle); canlıya alma kapısı değil. Paket r17'de hizalanır (AC-83 ve ilgili sabotaj).
- **Q-26 — onay (XMC-001):** MOD-0024 ve MOD-0361 / CAND-CAP-0002 dosyalarına yalnız paket r16 §5 "DL-4" listesiyle sınırlı dokunma onaylı;
  liste dışı dokunuş yine sorulur.
- **A-21 (CT):** yeni denetim belirteci açılmaz; DL-3 yöntemleri aşırı yüklemeyle kullanılır (CT kararı, öneri).

## Ek — MOD-0040-FU02 r17 incelemesi kararları (Beste, 2026-11-14)

Kaynak: `docs/records/reviews/MOD-0040-FU02/2026-11-14-f4a52ff2.md` (KOŞULLU). Sahip (Beste) seçti:

- **F-1 — (c) ilk değişiklikte deftere alım:** DL-4'ten önce açılmış (tohumlar dahil) abonelik planlarının kodu güncellemede
  değiştirilirse, değişiklik gerekçeyle (RCS-001 §2, 10–500) kayıt kodu defterine alınır: eski kod o anda deftere "eski kod" sahipliği
  olarak yazılır ve emekliye ayrılır, yeni kod Manual sahiplikle yazılır; aynı koordinatör işleminde. Değiştirilmeyen eski kodlar
  deftere alınmaz (Q-22 A korunur). 409 `RECORD_CODE_IMMUTABLE` eski planlar için kalkar. AC-88 / AC-90 / §8.18 / sürüm notu hizalanır.
- **F-2 — 400 korunur:** plan güncellemesinde boş kod bugünkü gibi 400 "Code is required." (Q-24 B "bugünkü davranış korunur").
- **A-22 — PPM ekibi:** PPM'in kullanacağı Platform tarafı kayıt kodu uçlarını (sözleşme eki + Platform kodu) PPM ekibi yazar; Platform
  koduna dokunma XMC-001 kapsamındadır ve DL-11 revizyonunda dosya listesiyle sahip onayı alınır. Merkez yalnız inceleme / kabul ölçümü.
- **r17 yorumları (1) CodeReason, (3) PLN daralmaları (üst sınır 40, nokta reddi), (4) MISMATCH genişletmesi:** CT kabul (inceleme
  bunları kabul edilebilir buldu); onay satırında adlarıyla anılır. F-3 (atıf `GlobalApplicabilityStateRepository.cs:33`) r18'de düzeltilir.

## Ek — MOD-0040-FU02 r18 XMC ek onayı ve inceleme F-1 (2026-11-14)

- **XMC-001 ek onay (Beste):** `UpdateSubscriptionPlanCommandValidator.cs` (MOD-0361 / CAND-CAP-0002) — boş kod 400 kuralı ortak
  `SubscriptionPlanRequestValidator`'dan bu dosyaya aynen taşınır; davranış değişmez. Q-26 listesine eklenir.
- **r18 incelemesi F-1 (DÜŞÜK, CT kararı):** deftere alınmış desene uyan eski plan kodu (ör. `PLN-000007`) sistem kodu atlamasında
  görülmüyor. Kapanış (a): DL-4 WP'sinde CT ek şartı — platform kolunda atlama koşulu "dış yoklama veya defter `CodeExistsAsync`";
  AC-88'e adım + sabotaj (paket metni sonraki revizyonda hizalanır). Ayrıca kabulde canlı sayım `^PLN-[0-9]{6,}$`.

## Ek — B2 Deferred outbound PlannedBins (Beste, 2026-11-14)

- **Karar: dolu sayılır.** Ertelenmiş (`Deferred`) ya da dalgadan geri çekilip `Deferred`'a dönen çıkış siparişinin `PlannedBins`'i,
  yeni bin dağıtımında (MOD-0178 `OutboundAllocationPlanner.WorldAsync`) açık görevlerin kalanı gibi düşülür; başka sipariş aynı bin
  miktarını planlamaz. İptal edilen ya da kapanan siparişin `PlannedBins`'i düşülmez.
- **XMC-001 onaylı:** MOD-0178 `OutboundAllocationPlanner.cs` (yalnız `WorldAsync` düşüm kuralı) ve gerekirse okuma için
  `OutboundShipmentRepository.cs`'e yalnız eklemeli sorgu. Uygulama MOD-0180 DL-3 içinde (WP-0180-DL3 eki), AC vakası + sabotajla; 0178
  test klasöründe yalnız yeni vaka eklenir, mevcut vakalar değişmez.

## Ek — MOD-0049-FU01 DL-2 UAS bekçisi (Beste, 2026-11-14)

- **XMC-001 onaylı:** `frontend/Diten.Web/tests/unauthorized-surface.test.js` `GATED` haritasına yalnız ekleme dört satır —
  `MasterData/BusinessPartners/{Index,Details}.cshtml` → `mdm.business-partners.read`, `Create.cshtml` → `mdm.business-partners.create`,
  `Edit.cshtml` → `mdm.business-partners.update`. DL-2 commit'ine girer. Ders: yeni kiracı ekranı WP'lerine bu bekçi satırı da yazılır.

## Ek — Ali kararları ve bilgileri (2026-11-16, Beste'ye e-posta)

Kaynak: Ali'nin iki e-postası (Beste aktardı, 2026-11-16).

- **MOD-0182 Q-13 (mobilde şirket bilgisi):** Auth token'a `legal_entities` koymuyor. Karar: mobil okutma depo servisindeki bugünkü
  W-0173-02 kuralıyla başlar — `X-Legal-Entity-Id` başlığı zorunlu, MDM'de kiracının etkin şirketi olarak doğrulanır; kullanıcının şirket
  üyeliği şimdilik denetlenmez. Üyelik denetimi ileride ayrı Auth işi; geldiğinde listede olmayan başlık 403 `CROSS_LE_FORBIDDEN`
  (bugünkü sözleşmeyle uyumlu). MOD-0182 paketi sonraki revizyonda Q-13'ü bu kararla kapatır.
- **MOD-0182 Q-14 + MOD-0178-FU01 Q-5 (rol şablonları):** `warehouse.barcode.*` izinleri ve sevkiyat rollerine iki CRM izni
  (`crm.account.lookup-validation`, `crm.account-ship-to.read`) ayrı, küçük bir Auth paketiyle; FU31'e eklenmez (FU31 donduruldu), FU31'den
  sonra gelir. Açık: paketi kim yazar; D-4 (geri çağırma rolleri), A-2 (3PL Operatörü) ve sayım rol şablonu bu pakete girer mi — Ali'ye
  soruldu.
- **Görev Merkezi'nde MDM bağlantısı:** evet, bağlanır; Platform ayar satırını merkez ekip ekler, MDM tarafı değişikliği Ali'de; acil değil,
  MVP-5 sonrasına (backlog).
- **Auth FU31 (AC-76):** paket donduruldu, Ali onayı bekliyor; onaydan sonra ~44 saat kod; tarih onay günü bildirilecek.
- **MDM kısa kod kilidi (N-12 / A-18):** Ali kararı (a) ilk kayıt kodundan sonra kalıcı kilit; MOD-0220-FU01 rev 3 (origin/chore/ct/mdm-ct-02,
  374732e11) incelemeden geçti, Beste onayını bekliyor.
- **MOD-0290-FU07 (GTIN):** paket onaylı; `GET /product-master/identifiers/{gtin}` pakette; DL-1 başlamadı, tarih bildirilecek (MOD-0182
  GTIN çözümlemesi buna bağlı).
- **Q-7 / Q-8 (MOD-0178-FU01):** kapanmış — Q-8 Ali kabulü 13 Kasım (bc503498c), Q-7 CT kararı + sözleşme adımı (WMS-BUNDLE 1.3.0 /
  WAREHOUSE-OUTBOUND 1.1.0, 2a1ce1c85). Ali'ye bildirildi.
- **#417 ve #428** main'e merge commit ile girdi (Beste); "Require branches to be up to date" Ali kararıyla kapatılacak; şart: main'e her
  birleştirmeden sonra tam kontrol, main kırmızıysa birleştirme durur. Açık: MOD-0288 kararsız testi (gerçek yarış, sahip kararı).

## Ek — MOD-0182 Q-14 rol dağılımı ve Görüntüleyici (Beste, 2026-11-16)

- **Rol dağılımı (MOD-0018-FU32 DL-2) uygun:** yeni rol yok; `WarehouseOperator` = scans.create, devices.create, label-prints.read /
  .create; `WarehouseSupervisor` = operatör + scan-events.read, scan-overrides.approve, devices.*, settings.*, label-templates.*, trace.*;
  yalnız BARCODE-RFID hakkı olan kiracılarda. Paket §14'teki beş rol önerisi bununla karşılanır (MOD-0182 sahibi teyidi).
- **Kalite / denetçi rolü gerekmez.**
- **Görüntüleyici altı okuma anahtarını alır** (scan-events.read, devices.read, settings.read, label-templates.read, label-prints.read,
  trace.read); yazma anahtarları ve trace.export almaz. Okutma olayı ve izdeki okutan / sorumlu çalışan adı kabul — müşteri / alıcı verisi
  değil (FU31'den farkı), görev atamalarında zaten görünür (paket §14 notu). Ayrı Auth işi açılmaz.

## Ek — MOD-0012-FU01 DL-2 sözleşme açık noktaları a…i (Beste, 2026-11-17)

Kaynak: WP-0012-FU01-DL2 raporu (dal `ed9fc24bf`, sözleşme `docs/analysis/contracts/tenant-secret-vault.openapi.yaml` LF sha256
`853ade0e…040a`, x-status DRAFT). Sahip kararı: dokuzu da geliştiricinin bugünkü seçimiyle kalır; sözleşme böyle yazılır. Dondurma CT
doğrulamasından sonra.

- **a)** Hata zarfında `data`: işleyici retlerinde `null`, kapı / gövde retlerinde (400, 403) `{}` — DL-1 davranışı; tekleştirme ayrı iş.
- **b)** `previous` = `{ value, secretVersion, validUntil }`; yokken üye yazılmaz (null değil).
- **c)** Verify + geçiş penceresinde önceki zarf açılamazsa istek bütünüyle 500 / 503; kısmi cevap yok.
- **d)** `"use": null` = Use (DL-1 TryParse deseni); başka geçersiz değer 400.
- **e)** `Cache-Control: no-store` hata cevaplarında da var; sözleşmede yalnız 200 için bağlayıcı.
- **f)** 401 gövdesiz; yazma uçlarında temsilî kullanıcı eksikliği gövdesiz 401, `WWW-Authenticate` yok (DL-1 davranışı).
- **g)** Resolve gövde üst sınırı 1024 bayt.
- **h)** Sözleşme testi x-status DRAFT ya da FROZEN kabul eder; dondurmada FROZEN'a çekilir.
- **i)** Hata kodu → HTTP durumu eşlemesi testte kaynak taramasıyla ölçülür; tek eşleme tablosu ayrı küçük iş.

## Ek — MOD-0178-FU01 DL-3 ülke alanı geri dönüşü (Beste, 2026-11-17)

Kaynak: WP-0178-FU01-DL3 raporu (commit `365dbe851`). Paket, Reference Data `country` kümesi okunamaz ya da boş olduğunda ne olacağını
söylemiyor. Sahip kararı **A**: ülke normalde yalnız listeden seçilir; küme okunamaz ya da boşsa alan elle yazılabilir olur ve formda uyarı
görünür. Gerekçe: Reference Data kesintisi çıkış sevkiyatı açılmasını durdurmasın. Paket sonraki revizyonda bu davranışı yazar.

## Ek — İki XMC-001 onayı (Beste, 2026-11-17)

- **MOD-0049-FU01 DL-2:** `frontend/Diten.Web/tests/global-confirm-input-type.test.js` SHOW_INPUT_CALLERS tablosuna yalnız ekleme tek
  satır `"MasterData/BusinessPartners/index.js": { inputs: 1, typed: 0 }` (Pasifleştir / Etkinleştir gerekçe kutusu yeni çağıran). Ayrı
  commit, satır kaldırılınca kırmızı sabotajı. Barkod X-10 ve 3PL Perm.Entity emsali.
- **Inventory test kimliği (MOD-0178 test yardımcısı):** `OutboundTestScenario.cs:162` sipariş satır kimliği 16 bitlik GUID öneki
  (`[..4]`) yüzünden 200 × 10 satırda koşu başına ≈ %13 çakışıyor → `WavePerformanceTests.AC27_d` / `AC27_b` ara sıra 400
  (`lines[].orderLineId must be unique`). Bulan: MOD-0179 DL-1 oturumu. Düzeltme yalnız test kodunda (sıra numarası ya da tam GUID),
  çıkış formu DL-3 oturumunda ayrı commit; ürün kodu değişmez. Main'deki belge PR'larının ara sıra `inventory-tests` kırmızısının muhtemel
  nedeni.
- **Sıra:** CT doğrulaması (MOD-0012-FU01 DL-2, MOD-0179 DL-1, MOD-0182 DL-2) MDM DL-2 dala girdikten sonra tek seferde.

## Ek — MVP-4 sahipliği Beste'ye geçti (Beste, 2026-11-17)

- MVP-4 geliştirici hattı (MOD-0188, MOD-0189, MOD-0191) 2026-11-17'de ElikPenco'dan devralındı; sahip **Beste** (merkez). Devir
  envanteri: PR #445 (`docs/records/audits/2026-11/mvp4-developer-handoff-01/README.md`, main `cd76894cf`). Süreli R8 istisnası:
  PR #411 (`docs/records/decisions/2026-11/mvp4-r8-time-limited-exception-owner-decision-01.md`, 2026-11-11 … 2026-12-31).
- Sonuç: MVP-4 pack'lerinde `owner` Beste; pack onaylayıcısı Beste (`bestepullukcu-alt`), yazan ElikPenco (K5) — yazan ≠ onaylayan.
  ElikPenco devralma süresince yalnız Beste'nin verdiği düzeltme işlerini yapar; merge yetkisi sahipte.
- İlk uygulama: MOD-0188 #256 (AC-D07 birim modu revizyonu; kararlar D1–D6, PR #450).
