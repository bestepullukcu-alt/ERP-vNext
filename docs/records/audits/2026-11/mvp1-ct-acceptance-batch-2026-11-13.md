# MVP-1 / MVP-5 — CT kabul kayıtları (toplu, 2026-11-13)

CT: merkez (Beste adına), oturum ca7382cf. Kaynak: geliştirici oturumlarının raporları (oturum dökümleri), CT'nin kendi koşuları ve PR #424
CI'ı. Her kalemde "Ölçülmeyen" ayrıca yazılı.

## 1. MOD-0181 DL-6 — Gateway kapsama testi · VERIFIED
- Commit `a7ec33562` (`gateway/Diten.ApiGateway.Tests/Mod0181CycleCountRouteTests.cs`, +186; yalnız test). Etiket başı `ct/line-ff-2026-11-13f`.
- Ölçüm (geliştirici): uç listesi koddan (`Controllers.CycleCounting`: CycleCounts 13, CountVariances 9, CountEvidenceExports 3 = 25),
  WMS-COUNT-BUNDLE 1.1.0 FROZEN ile iki yönde eşit; her uç `/api/warehouse/{everything}` → `localhost:5067`; gerçek Gateway üzerinden 25 uç.
- Gateway takımı: önce 371/0, sonra 375/0. Sabotaj (a) bin kopyasında port 5068 → 4 testten 3'ü kırmızı; (b) mutlak şablon → 3'ü kırmızı;
  bayt-eşit geri alındı. `ocelot.json` sha256 öncesi = sonrası.
- CT: Gateway 375/375 MOD-0180 DL-1 kabulünde de ölçüldü (bu test dahil). PR #424 CI.
- Ölçülmeyen: CT sabotajları yeniden koşmadı. Kalan MOD-0181 kapısı: DL-4 ACCEPTED için Auth rol şablonu (Ali, bekletmez).

## 2. MOD-0178-FU01 DL-1 — çıkış formu v2 (Web) · KOŞULLU → canlı tur sonrası VERIFIED
- Commit `32ef19ca7` (feat, 20 Web dosyası), `f4877003c` (test, 4 dosya). Rapor: 14 paket + 7 ek sabotaj tuttu.
- CT tam Web.Tests koşusu (`3e31bfde9`, bu DL-1 dahil): 4543/4546; 3 kırmızı main'de de kırmızı. vitest: 13 kırmızı, tabanla aynı ve main'de de
  aynı (Windows satır sonu / yol; CI Linux'ta yeşil).
- CT kararları: 375 px'te `table-responsive` yatay kayma kabul; Edit'te ürün adı DL-2'de (proxy değişikliği).
- **Canlı turda bulunan hata (CT, 2026-11-13):** `/Warehouse/OutboundShipments/Create` yarıda kesiliyordu (ERR_INCOMPLETE_CHUNKED_ENCODING).
  Neden: `_LineOffcanvas.cshtml:48` `@Localizer["LinePanel.Unit"]` — değer `{0}` içeriyor, argümansız basılınca HTML localizer biçimlendirip
  hata fırlatıyor. Düzeltme `1a9ee5333` (`.Value`, emsal CRM LegalProfiles `_Form.cshtml:103`) + bekçi
  `WarehouseLocalizerPlaceholderGuardTests` (yeşil; `.Value` silinince kırmızı, geri alındı). Birim testler Razor çizmediği için yakalamamıştı.
- Geliştiricinin süreç hatası: dala girmeyen geçici commit'te bir kez `core.hooksPath=/dev/null`; geri alındı, kayda geçti.
- Kalan: canlı tur (iki lotla önizleme; 375 px `scrollWidth` tr / en / ar). Ana checkout Web'i düzeltmeyi alınca yapılır; sonuç bu kayda eklenir.

## 3. MOD-0177-FU01 AC-17 — SHIPMENT_BUNDLE_350 atlamasının kaldırılması · VERIFIED
- Commit `64c3ef26a` (cherry-pick -x 016c892b7, 4 dosya), `481bbea29` (AC-17 r9 testi). Adım 0 `e22a28eb4` (--no-ff CT etiketi).
- `CustomerRef_field_matches` allOf / $ref çözümü, `[string, null]` tam küme, pattern harfiyen. Sabotajlar: pattern sil, a-f → A-F,
  tür tekil, alan adı, RepoPaths filtresi — hepsi kırmızı, geri alındı.
- Inventory filtresiz: taban 2130 geçti / 5 atlandı; dal 2131 / 4 atlandı (yalnız INVENTORY_PERF); kırmızı yok. CI skip denetimi dalda 0.
- CT: `ShipmentBundle350AlignmentTests` CT'de ayrıca yeşil (1/1, 2026-11-13). PR #424 CI inventory-tests.
- Ölçülmeyen: `scripts/tests` 240 failure / 6 error (taban ve dalda aynı sayı; adları karşılaştırılmadı).

## 4. MOD-0262-FU08 DL-1 — yükleme denetim kaydı nesneyi adlandırır · VERIFIED (errata ile)
- Commit `02a1a41cd` (9 dosya, hepsi paket §5 içinde). AC-1…AC-16 test adları raporda; S-1…S-20 kırmızı, geri alındı. AC-16 gerçek
  Mongo: aynı correlation ile 503 sonra 201 → iki kayıt (Failed boş EntityId; Succeeded EntityId = ContentId).
- Platform.Application filtresiz: önce 9304/9323 (19 kırmızı), main 9140/9159 (19), sonra 9319/9339 (20). 19 kırmızı adı adına aynı
  (Mongo ortam); 20. `InternalRecordCodesHttpTests.AC26_…3_s…` yük altında süre testi, tek başına 46/46 yeşil.
- CT kararı: DL-1 yükleme cevabı **200** kalır (FU01 §19, `DocumentRepositoryService.cs:300`); paket AC-2 / S-12'deki "201" errata,
  sonraki revizyonda düzelir. AC-15 geliştirme kaydı: K-7 yolu commit iletisinde (`mod0262-fu01-mod0181-owner-decisions-2026-11-12.md:33`).
- Kalan: AC-29 Auth audience (paket kapısı). PR #424 CI platform-app.

## Karar
MOD-0181 DL-6, MOD-0177-FU01 AC-17, MOD-0262-FU08 DL-1: VERIFIED. MOD-0178-FU01 DL-1: render hatası düzeltildi; canlı tur sonrası VERIFIED.

## Ek — MOD-0178-FU01 DL-1 canlı tur (CT, 2026-11-13 akşam) · VERIFIED

Ortam: ana checkout Web 5001 (dotnet watch), hat `73b632263` (`ct/line-ff-2026-11-13m`, düzeltme `1a9ee5333` dahil); kiracı GMG, depo D01,
ürün GP-000123 / LSKU-TR-000045. Kayıt yapılmadı; yalnız önizleme (sunucuda yazım yok).
- Create sayfası tam çiziliyor (`</html>` dahil); `#lineUom` "Temel birim: EA" (düzeltilen şablon doldu).
- Tek satır önizleme, iki lot: AMX-2604 (FEFO 1, SKT 2028-03-31, 346) ve G1-20261020 (FEFO 2, SKT 2028-12-31, 58). FEFO dışı lot seçilince
  gerekçe alanı açılıyor ("10–1000 karakter"), şirket FEFO kipi "gerekçe zorunlu (RequireReason)" gösteriliyor.
- Toplu "Tahsisi Önizle", iki satır: (1) G1-20261020, 2 EA, "FEFO sapması — gerekçe var"; (2) FEFO (önerilen AMX-2604), 3 EA, "Karşılanır".
- 375 px (öykünülen görünüm), iki satırla:

| Dil | dir | document scrollWidth | tablo scrollWidth | kutu clientWidth |
|---|---|---|---|---|
| tr | ltr | 375 | 1003 | 311 |
| en | ltr | 375 | 956 | 311 |
| ar | rtl | 375 | 801 | 311 |

  Sayfa taşmıyor; tablo `table-responsive` içinde kayıyor (CT kararı). Başlıklar ve düğmeler üç dilde çevrili.
- Ölçülmeyen: gerçek telefon; Edit / Details canlı turu; kaydetme ve tahsis yazımı (DL-1 kapsamı önizleme ve gövde; yazım servis testleriyle).
  Dil, tur sonunda tr'ye geri alındı.

## Ek — MOD-0177-FU01 DL-2 yeniden ölçüm (WP-0177-FU01-DL2) · VERIFIED (canlı tur ve D-4 hariç)

- DL-2 kodu `a872e8c56` (2026-11-06 VERIFIED, main'de) paket r9'a (`c12df7b7…`) göre yeniden ölçüldü: AC-21…AC-24, AC-27, AC-31 hepsi
  "vardı" (dosya:satır ve test adlarıyla geliştirici raporunda); üretim kodu değişmedi.
- F-1 kapandı: `182f5b88e` (recall-fu01.test.mjs, GUID biçimli Legacy / Manual etiket vakası; CT-4 sabotajı kırmızı) ve `6f8481efe`
  (RecallFu01NodeTests sarmalayıcısında Node test sayısı 7 → 8).
- 25 sabotaj (S-21, S-22a–d, S-23a–e, S-24a–b, S-27a–g, S-31a–d, CT-4) kırmızı → geri al → yeşil.
- Web.Tests filtresiz: dal 4554 / 5 kırmızı, taban 4547 / 6; beş kırmızı tabanda da var: main'in 3'ü + MOD-0182'nin
  `NavManifestL10nGuardTests` 2'si (MOD-0182 FIX-01 madde 7'de düzeltiliyor). vitest: 14 Windows kırmızısı (dal = taban; pvg süre testi
  bir kez daha, tek başına 34/34).
- Not: geliştiricinin ilk vitest denemesinde npx npm'den vitest 5.1.0 indirdi (repo dışı önbellek); o koşu sayılmadı. CT kuralı: vitest
  yalnız repodaki node_modules ile koşulur.
- Kalan: CT canlı turu (en / tr / ar, 375 px) ve DL-1 kabul kapısı D-4 (Ali, `crm.account.lookup-validation` rol eşlemesi) — modül ACCEPTED
  bu ikisinden sonra.

## Ek — MOD-0182 DL-1 (3395f5cd7 + FIX-01 b19765ef2) · VERIFIED (2026-11-14)

- İlk CT ölçümü (3395f5cd7): Inventory 2283 / 0 kırmızı, Gateway 375/375; Web.Tests koşulmamıştı (CT eksiği) — sonradan 2 kırmızı
  (`NavManifestL10nGuardTests`, Nav.Module.BARCODERFID) görüldü. Bağımsız güvenlik incelemesi: engel yok, O-1 orta (PIN deneme yarışı).
- FIX-01 (`b19765ef2`, WP-0182-DL1-FIX-01, 7 madde): O-1 atomik hak ayırma + işlem içi kilit doğrulaması; sayımda süresi geçmiş /
  geri çağrılmış lot kabul + uyarı (sahip kararı); `no-store`; `IsDeleted` süzgeci; sırlı record'ların `ToString` maskesi; `skuName` =
  getSku name (sahip kararı); Nav.Module.BARCODERFID 7 dil + PagelessUntil. Geliştirici: 12 sabotaj kırmızı, DL-1 sabotajları yeniden
  koşuldu (S-4a/b, S-18b/f/g).
- CT ölçümü (ayrık worktree, `b19765ef2`, 2026-11-14): Web.Tests 4554 = 4551 / 3 kırmızı (yalnız main'de de kırmızı olan üçü; nav
  kırmızıları kapandı). Inventory 2363 = 2357 / 2 kırmızı / 4 atlandı; iki kırmızı (`BarcodeScanTests.Two_hundred_consecutive_scans_stay_under_
  the_p95_bound`, `CancelReleaseTests.AC32_…`) tek başına iki kez yeşil — yük altında süre testleri. Bir önceki deneme ortam yüzünden
  geçersiz (Mongo bağlantı zaman aşımı, 1620 kırmızı) — sayılmadı.
- Risk notu: Barkod p95 testi yük altında üç kez düştü (geliştirici iki, CT bir). CI'da tek iş koşarken yeşil beklenir; tekrarlarsa
  bütçe ya da ölçüm yöntemi FU olarak ele alınır.
- Paket r8'e gidenler: PIN okuma ucu, sayım kararı, D-3 (engelli cihazdan aşım), O-2 (başarısız PIN merkezi denetimde değil), PIN canlılığı.

## Ek — MOD-0178-FU01 DL-2 (9922f2b8f + 321b9a40d) · VERIFIED (sözleşme adımı ve kapılar hariç)

- Geliştirici: AC-14…AC-23 testli ve yeşil; 23 sabotaj kırmızı → geri al → yeşil; Inventory 2328 / 1 (barkod p95, yük).
- CT ölçümü (`b19765ef2`, DL-2'yi içerir): yukarıdaki Inventory ve Web koşuları — DL-2'ye ait kırmızı yok.
- Açık: CT sözleşme adımı (WMS-BUNDLE / WAREHOUSE-OUTBOUND eklemeli `customerRef`, `customerAddressId`, üç kod; o gün
  `WmsContractPendingDl2` ve `OutboundCustomerRefBodyTests.Pending` listeleri silinir; `updateOutboundShipment`'a 422 / 503); kabul kapıları
  XMC-CRM-01 (CRM §5a satırı, Beste) ve Q-5 (Ali).

## Ek — MOD-0049-FU01 DL-1 (iş ortağı ana verisi) · KOŞULLU (2026-11-14)

- Commit'ler: `4cba4d69e` sözleşme taslağı (AC-1), `ec1cbd139` Platform X-4 / X-5 / X-6, `54e24a94c` MDM kodu, `f7a2a7d46` testler,
  `916f207a8` Web anahtarları (Nav.Module / Nav.Page / Perm.Entity 7 dil; CT onayı).
- Geliştirici: AC-1…AC-14, AC-16, AC-26 yeşil (birim 123, HTTP / gerçek Mongo 47); 37 sabotaj kırmızı → geri al → yeşil; F-1…F-5 kapandı.
  Filtresiz: MDM App 72 / 3056 (tabanın 125'inin alt kümesi, yeni kırmızı 0), MDM Api 0 / 1268, Platform 19 / 9420 (taban aynı; 16 fazla
  kırmızı replica init zaman aşımı, sınıf tek başına tabanla aynı), TenantArchitecture 5 / 229 (DocsPathGuardTests, taban), Web 3 / 4554,
  vitest label-ledger 28/28.
- CT kararları (açık noktalar): (1) `ExportTooLarge` sabit + `ListExportContract.TooLargeCode` eşitlik testi kabul (csproj değişmez);
  (2) `MDM_BP_STORE_UNAVAILABLE` (503) ve `MDM_BP_UNEXPECTED_ERROR` (500) kabul, paket §8.5'e sonraki revizyonda; DL-2'de 19 kodun hepsi
  için `Error.{KOD}`; (3) `UpdatedAtTicks` ve `ResultVersion` sistem alanları kabul; (4) kişilerin tek tek yazımı kabul; (5) tokensız
  istek 400 (MDM'nin mevcut kiracı çözümü) — bilgi; (6)–(9) kabul. **Menü:** modül kiracı planına (A-3, Ali) DL-2 ekranından önce
  eklenmez; plan girişi açılmadıkça menü satırı görünmez.
- Kalan (kabul kapıları): X-7 RECORD-CODE-ALLOCATE 1.1.0 (CT), BUSINESS-PARTNER-READ 1.0.0 dondurma (CT), X-8 Gateway rotası
  (integration-agent), A-5 ortam (Platform `TrustedRecordCode:Mdm` + `AllowedRecordTypes` "BPT", MDM `RecordCodeAllocator__*`),
  ardından AC-15 + S-15 canlı kanıt. CT kendi tam koşusunu makine boşalınca yapar.

## Ek — MOD-0012-FU01 DL-1 (kiracı sır kasası) · KOŞULLU (2026-11-14)

- Commit'ler: `c85d3b35f` (kod, testler, X-3, X-2 defter + AcceptedTrails aynı commit), `b7096e8e6` (sızıntı taraması ek vakası),
  `80b54d844` (eşzamanlı ikiz istekte kaybeden ilk cevabı alır), `e6b4d487e` (hattı alma).
- Geliştirici: AC-1…AC-15 yeşil (AC-3 (b)/(d), AC-7 ve AC-9 resolve bacakları DL-2); 26 sabotaj kırmızı → geri al → yeşil (S-11a iki maske
  notuyla); test vektörleri üretim kodundan bağımsız (Node.js crypto). Filtresiz: Platform 9509 / 22 (20 taban + 2 guard), BackgroundJobs
  93 / 1 (taban), CompositionGuard 12 / 0, Eventing 68 / 0, TenantArchitecture 229 / 5 (taban), Gateway 384 / 0. KEK'siz dev başlangıcı
  ölçüldü; appsettings değişmedi.
- Kalan kırmızı: `LoginOnlyEndpointGuardTests` 2 vaka — yeni iç denetleyici `TrustedServiceSchemeControllers`'a eklenmeli; CT onayı verildi
  (tek satır + sabotaj), FIX bekleniyor.
- CT kararları (sapmalar 1–8 kabul): SourceService = Diten.Platform (çağıran metadata'da), denetim anahtarı `Version`, özel host + kompozisyon
  sabitleme, iki ek dosya, 503 `SECRET_AUDIT_UNAVAILABLE`, 401 gövdesi bir kez ölçülecek, `Open` DL-2'de, 404 / 409 ret kayıtları. Paket metni
  sonraki revizyonda hizalanır.
- Kabul kapıları: Ali Q-1, Q-2, Q-3, Q-4, Q-13 (kod Q-1'i ISecretsProvider + IKeyEncryptionKeyProvider dikişiyle karşılıyor); CT Q-12
  domain-config Vault satırı.

### MOD-0049-FU01 DL-1 — kapılar (2026-11-14)

- CT kapıları kapandı (`28e2e9f2e`): RECORD-CODE-ALLOCATE 1.1.0 (BPT), BUSINESS-PARTNER-READ 1.0.0 FROZEN, Gateway iki MDM rotası
  (Gateway 384/384, MDM AC01 12/12 donmuş dosyayı okuyor, Platform RecordCode 400/400).
- A-5 (dev, bu bilgisayar): yalnız bu PC'de geçerli test kimlik bilgisi üretildi (oturum scratchpad'i, repoda ve sohbette yok); Platform
  `TrustedRecordCodeCredential:Mdm` (DITENMDMSERVICE, TRUSTED_RECORD_CODE_ALLOCATE, BNK/BAC/AGR/BPT) ve MDM `RecordCodeAllocator:*` ortam
  değişkeniyle verildi; Platform / MDM / Gateway yeniden başlatıldı (2026-11-14 08:25–08:28). Canlı ortam için sahip (Beste) yapacak.
- **CT kararı — AC-15 canlı kanıt DL-2 kabulüne taşındı:** Web jetonu tarayıcıya vermiyor (HttpOnly) ve modülün DL-1'de ekranı / Web
  vekili yok; kullanıcı adına Gateway yolu DL-2 ile açılır. Ayrıca A-2 (rol izni) ve A-3 (kiracı planı, DL-2 sonrası) gerekir. DL-1 kodu
  ve sabotajları tam; DL-1 DL-2 ile birlikte ACCEPTED olur.

## Ek — MOD-0040-FU02 DL-3 (İKİSİ DE motoru) · VERIFIED (2026-11-14)

- Commit: `07ad98464` ("İKİSİ DE engine: preview, claim, reasoned change, usage lock, history (AC-62..AC-75)"), MOD-0182 DL-1
  `3395f5cd7` üstünde; 37 dosya; A-20 defter satırı aynı commit'te (`Diten.Platform.md`, `CT-DECISIONS.md`). Paket r15 (c4499f37, Ali onaylı),
  WP-0040-FU02-DL3-both-engine.
- Geliştirici (WP §5 raporu, DL-1 oturumu): AC-62…AC-75 yeşil; 44 üretim kodu sabotajı kırmızı → geri al → yeşil; 45. kontrol Skip
  denemesi "atlandı" sayıldı ve kabulün "atlanan test sıfır" kuralına takılıyor (beklenen davranış). Bildirilen sapmalar: tarih alanları
  DateTime (UTC, DL-1 emsali), AC-68 örneği eşdeğer ölçümle, kodla uyuşmayan birkaç sabotaj tarifi eşdeğeriyle — CT kabul; paket metni
  sonraki revizyonda hizalanır.
- CT ölçümü (filtresiz, ayrı worktree, `07ad98464`): Diten.Platform.Application.Tests 9420 / 9401 geçti / 19 kırmızı / 0 atlandı
  (14 dk 32 sn). 19 kırmızının adları DL-2 CT tabanıyla (`ctv-0040dl2`) birebir aynı — yeni kırmızı 0, kaybolan 0; hepsi Mongo ortam /
  süreç kırmızısı (main tabanı).
- Sonuç: DL-3 VERIFIED. Uç / Gateway / ekran / yeni izin anahtarı yok (paket §3). Canlıya alma kapısı ayrı: MDM kısa kod kalıcı kilidi
  (Ali) ve onay satırının main'e girmesi.

### MOD-0012-FU01 DL-1 — VERIFIED (2026-11-14)

- Guard düzeltmesi `15fcfe67c` (`LoginOnlyEndpointGuardTests` sözlüğüne `InternalTenantSecretsController → TrustedSecretVaultConsumerService`;
  geliştirici 7/7, satır silinince 2 kırmızı).
- CT ölçümü (filtresiz, ayrı worktree, `15fcfe67c`): Diten.Platform.Application.Tests 9509 / 9490 geçti / 19 kırmızı / 0 atlandı (32 dk;
  makine yüklü). 19 kırmızının adları DL-2 / DL-3 CT tabanıyla birebir aynı — yeni kırmızı 0; geliştiricinin 2 guard kırmızısı kapandı.
- Kabul kapıları: Q-1, Q-2, Q-4, Q-13 sahip (Beste) kararıyla kapandı (`a974b20d5`; "Ali'den bekleme"); Q-12 `23f25bca5`. Q-3 DL-5 kapısı.
- Sonuç: DL-1 VERIFIED. 401 gövdesi ölçümü (dev'de servis jetonu yapılandırması yok → 400) DL-2'de şemanın yapılandırıldığı ortamda yapılır.

## Ek — MOD-0360-FU01 DL-1 (tedarikçi kullanıcı bağı) · VERIFIED, kabul kapıları açık (2026-11-14)

- Commit'ler: `44c8121ef` (Web `Perm.Entity.SUPPLIERBINDINGS` 7 dil — kanonik anahtar, karar metnindeki SUPPLIER_BINDINGS normalize
  edilmiş hali), `7a390c319` (Auth DL-1 + A-1 defter satırları aynı commit'te). Hat `ct/line-ff-2026-11-14g` ile CT zincirine alındı.
- Geliştirici (WP §6 raporu): 142 SupplierBindings testi yeşil; 24 sabotaj kırmızı → bayt-eşit geri al (19'u S-3 / S-6…S-9 öncesi
  anlık görüntüde, hedef dosyalar aynı; 5'i son kodda); XMC dosyaları karar kayıtlarıyla (84421c2d7, 6b8566366, 7e5a347c8).
- CT ölçümü (filtresiz, ayrı worktree, `7a390c319`): Diten.AuthService.Application.Tests 3074 / 3001 geçti / 54 kırmızı / 19 atlandı
  (12 dk); ServiceClientRegistry 160 / 0. 52 kırmızı DL-1 öncesi taban (ProductIdentityEntitlementReconciliationMongoTests 38,
  ServiceClientOperationalProvisioningMongoTests 12, OwnedMongoResolutionTests 1, ServiceClientOperationalBootstrapTests 1 — geliştirici
  tabanıyla aynı). İki ek kırmızı tek başına yeşil (34 / 34): `AccountKindCreationPathsGuardTests` IOException "İşleyici geçersiz" (ortam,
  yük altında dosya okuma) ve `CreateEligibilityFailClosedTests…(supplierId: "")` — yük altında kararsız; geliştiriciye not (FU: vaka
  ikizin istek kaydını beklemeden okuyor olabilir). Atlanan 19 Unix-özel, önce / sonra aynı (Auth için kabul edilmiş istisna).
- Kabul kapıları (ACCEPTED için): MOD-0140 DL-3 main'de + Gateway canlı kanıt; DL-1 canlı kanıtı (bir başarılı kur, bir 422);
  `SupplierEligibility:GatewayBaseUrl` ortam değişkeni (CT); mevcut kiracı Admin rollerine iki anahtarın geriye dönük verilmesi ölçülecek;
  dev Mongo boş koleksiyon olayı (sahip kararı: sil — geliştirici oturumunda sahip onayıyla).
- Açık noktalar paket sonraki revizyonda: CT önerisi eşleme satırları (404 kodsuz / 401 / relationshipExists false → 503; 403 gövdesiz →
  403) ve paket dışı ret kodları (CONCURRENT_WRITE 409, COMMIT_UNCONFIRMED 503, ACCOUNT_NAME_REQUIRED 400, HISTORY_AUDIT_UNAVAILABLE
  503, NOT_FOUND 404, ACTOR_FORBIDDEN 403) — CT kabul; `UpdateUserCommandValidator.cs` ileti listesi kozmetik (dokunulmadı).

## Ek — MOD-0180 DL-2 (dalga serbest bırakma + seam) · VERIFIED (2026-11-14)

- Paket r11 (226ab7f9…), WP-0180-DL2-release-route-seam. Commit'ler a5500a23a … 1309b27e3 (13; her biri filtresiz ölçüldü, geliştirici
  WP §5 raporu). Hat dalda; CT zincirinde.
- Geliştirici: Inventory 2330 → 2430 / 2426 / 0 / 4; WavePlanning + Warehouse 574/574; Gateway 384/384; AC-18 son sha referansla aynı
  (57491f99…, çıktılar bayt bayt aynı); AC-27 a p95 32,9 ms, b 7,0 s, c 59 ms, d 6,9 s (bütçe içinde); 74 sabotaj — 72 doğrudan kırmızı,
  S-15f / S-15at güçlendirilmiş varyantlarla (S-15f2, S-15at2) kırmızı; 0178 Warehouse test klasöründe diff yok; X-14 103→105, X-10
  133→136.
- CT ölçümü (filtresiz, ayrı worktree, `1309b27e3`, Mongo 27017 yok): Diten.InventoryService.Tests 2430 / 2426 / 0 kırmızı / 4 atlandı
  (6 dk); Diten.ApiGateway.Tests 384 / 384.
- CT kararları: S1 (X-7 → DL-4; waveId'siz orderBy=waveSequence 400), S2, S3, S4 (0178 InsertPickTasksAsync erken dönüş — sahip onaylı),
  B1 (taslak failed[].code), B4 kabul. B2 (planlayıcı Deferred outbound PlannedBins) sahip kararı bekliyor (ayrı).
- Açık 1 (DL-3 başında geliştirici inceler): merkezi denetim satırı durumunun cevaptan hemen sonra okunduğu iki vaka (WaveReleaseTests
  AC22_c, 0178 PutawayAccessAuditTests AC15_AC48) birer kez kırmızı, tek başına 5/5 yeşil. 0178 dosyası gerekirse XMC-001 sorusu.
- Bekleyen DL-3 parçaları: AC-22 (b)/(c) WaveException kayıtları, AC-15 (k) "sonraki dalgaya taşı" 422 + W2 geri çekme; X-7 → DL-4.
- vitest (geliştirici, uç): 14 kırmızı / 11 dosya, hiçbiri DL-2 dosyası değil (page-help-ledger BUSINESSPARTNERS → MOD-0049-FU01 DL-2
  ile kapanır; diğerleri wcn / governance / gsku — main karşılaştırması ölçülmedi; CRLF olabilir).

## Ek — 2026-11-17 CT doğrulaması (hat ucu `1a4475569` + `68d86449c`)

Ortak ölçüm (filtresiz, ayrı worktree `erp-vnext-wt-ct-0180-verify`, kendi mongod'u, Mongo 27017 yok, `INVENTORY_TEST_MONGO` boş):

| Koşu | Commit | Toplam / geçti / kırmızı / atlandı | Değerlendirme |
|---|---|---|---|
| Diten.Platform.Application.Tests | `1a4475569` | 9530 / 9511 / 19 / 0 | 19 = bilinen Mongo ortam sınıfları (AuditOutboxTemporalStorageMigration 8, BusinessReferenceDataVerifiedMarketOperational 7, PlatformMongoTestLock 1, VerifiedMarketOperationalConfiguredReferenceTenant 1, PpmAuditRetentionPolicySeed 1, PlatformTestMongo 1); yeni kırmızı 0 |
| Diten.Web.Tests | `1a4475569` | 4750 / 4747 / 3 / 0 | taban: CapacityPlans / SandopPlans first-load, ReturnDetails DetailsJs; yeni 0 |
| Diten.ApiGateway.Tests | `1a4475569` | 384 / 384 / 0 / 0 | temiz |
| TenantArchitecture.ArchitectureTests | `1a4475569` | 229 / 224 / 5 / 0 | 5 = DocsPathGuardTests (main'de de kırmızı; sabitlenmiş özet tutmuyor → bekçi fiilen taramıyor; sahibine bildirilecek) |
| vitest (tam) | `1a4475569` | 5671 / 5658 / 13 (10 dosya) | 10 dosyanın hepsi taban listesinde (dialog-one-implementation, global-products-register, governance-users-error-codes(-ct), gsku-register, platform-administrators-own-record, wcn-*); page-help-ledger artık yeşil |
| Diten.InventoryService.Tests | `68d86449c` | 2574 / 2570 / 0 / 4 | 4 atlanan = performans testleri (Expiry*Performance vb.); temiz |

`68d86449c` (test-only, sahip onayı 2026-11-17 XMC-001 kaydı `b73aca717`): `OutboundTestScenario.cs:162` varsayılan satır kimliği
`"SO-1/" + tam GUID` — AC27_b / AC27_d'deki ~%13 çakışma (`lines[].orderLineId must be unique`) kapandı; geliştirici 10 yalıtılmış koşu 2/2.

### MOD-0182 DL-2 (barkod ekranları) · VERIFIED (2026-11-17)

- Commit'ler `a38e97c28` (SCE: supervisor-pin GET, dört manifest sayfası, Gateway 106), `f91aaf6aa` (Web 7 dil). Hatta.
- CT: yukarıdaki tablo — Inventory 0 kırmızı, Gateway 384/384, Web / vitest yalnız taban. Canlı tur gerekmiyor (DL-2 kapsamı).

### MOD-0179 DL-1 (3PL) · VERIFIED (2026-11-17)

- Commit'ler `37fd11c46` … `04b8fefbc`, `66f7bdd59` (Rol İzinleri satır adları 7 dil); XMC-001 onayları `d8b017faa`, `424f9562b`.
  Hatta (`3bc3a6746`).
- CT: Inventory 2570 / 0 kırmızı (3PL test sınıfları ayrıca sayılmadı), Gateway 384/384, Web / vitest taban. Açık: Natig'in 3PL ücret mutabakatı sınırları `mod0179-3pl-owner-decisions-01.md`'ye yazılacak;
  `ed61055e0` (Perm.Entity kararı) hatta alınacak.

### MOD-0012-FU01 DL-2 (kiracı sır kasası resolve) · VERIFIED (2026-11-17), sözleşme dondurma açık

- Commit'ler `6de190340` (TENANT-SECRET-VAULT 1.0.0 DRAFT), `ed9fc24bf` (resolve, doğrulama süresi okuma tarafı, sayaç, sızıntı
  taraması, sözleşme testi). Sözleşme açık noktaları a…i "uygulandığı gibi" (sahip, `64b11b7e2`).
- CT: Platform 9511 / 19 taban, yeni kırmızı 0; Gateway rotası yok (384/384). Kalan: sözleşmenin DRAFT → FROZEN kaydı (sahip onayı).

### MOD-0049-FU01 DL-2 (iş ortağı ekranı) · KOŞULLU (2026-11-17)

- Commit'ler `7279a0923`, `1a4475569`; XMC-001 MDM SHOW_INPUT_CALLERS satırı (`b73aca717`). Hatta.
- CT: Web / vitest yalnız taban (page-help-ledger BUSINESSPARTNERS kapandı), Gateway 384/384.
- VERIFIED için: canlı tur — AC-15 (kiracılar arası 404) + S-15, AC-24 tarayıcı turu; izinli test kullanıcısı (A-2/A-3), parolayı
  kullanıcı girer. Küçük UX notları (salt-okur kullanıcıya ülke filtresi, GUID aktör için "—") sonraki revizyona.

### MOD-0178-FU01 DL-3 (CRM'den alıcı) · KOŞULLU (2026-11-17)

- Commit'ler `365dbe851`, `68d86449c` (test kimliği), `ec7c5c08e` (eylem metinleri). Ülke alanı kararı A (`5e4e62733`).
- CT: Inventory 0 kırmızı, Web / vitest taban (`1a4475569`; `ec7c5c08e` henüz ölçülmedi).
- `ec7c5c08e` geri alınacak: CT'nin "eylem metninde Title Case yasak" yönlendirmesi yanlıştı — `localization-standard` varsayılanı
  Title Case; doğru düzeltme yalnız `Customer.Refetch` en / fr değerlerini Title Case'e çekmek. Revert + düzeltme sonrası Web.Tests
  ve vitest tekrar.
- VERIFIED için: AC-31 (a–c) canlı ölçüm, §4.3 Reference Data ilk ölçümü, ülke serbest metin uyarısı kontrolü.

## Ek — 2026-11-18 CT doğrulaması

Hat `feature/sce/mod-0173-inventory-ledger-valuation`: origin/main `f21ab5891` hatta alındı (`027e29185`; çakışma yalnız
`approvals.tsv` / `reviews.tsv`, main satırları önce, satır değişmedi). CT koşuları ayrık worktree'de, `INVENTORY_TEST_MONGO` boş,
27017 yok. Kırmızılar adlarıyla 2026-11-17 tabanıyla karşılaştırıldı: yeni 0, kaybolan 0.

| Takım | Ref | Toplam / geçen / kırmızı / atlanan | Not |
|---|---|---|---|
| Diten.Web.Tests | `5ae87dab7` | 4750 / 4747 / 3 / 0 | taban 3 (CapacityPlans, SandopPlans, ReturnDetails) |
| vitest (tam) | `5ae87dab7` | 5671 / 5658 / 13 / 0 | taban 10 dosya |
| Diten.InventoryService.Tests | `47c0d4f78` | 2652 / 2648 / 0 / 4 | 4 = performans |
| Diten.Platform.Application.Tests | `2a9a21759` | 9603 / 9584 / 19 / 0 | taban 19 (Mongo ortamı) |
| Diten.Web.Tests | `2a9a21759` | 4768 / 4765 / 3 / 0 | taban 3 |
| TenantArchitecture.ArchitectureTests | `2a9a21759` | 231 / 226 / 5 / 0 | taban 5 (DocsPathGuardTests) |
| vitest (tam) | `2a9a21759` | 5757 / 5744 / 13 / 0 | taban 10 dosya |
| Diten.InventoryService.Tests | `2a9a21759` | 2660 / 2656 / 0 / 4 | 4 = performans |
| Diten.ApiGateway.Tests | `2a9a21759` | 384 / 384 / 0 / 0 | X-14 128 rota |

### MOD-0178-FU01 DL-3 (CRM'den alıcı) · KOŞULLU (güncelleme 2026-11-18)

- `ec7c5c08e` geri alındı (`d9fa1c9c8`, revert; `ec7c5c08e` öncesiyle fark 0); `5ae87dab7` yalnız `Customer.Refetch` en "Reload From
  CRM", fr "Recharger Depuis le CRM". es "Volver a Cargar desde CRM" olduğu gibi kalır (sahip, 2026-11-18: edat küçük, fr "le" ile aynı
  çizgi). Web.Tests / vitest tam: yalnız taban.
- VERIFIED için kalan: canlı tur (AC-31 a–c, §4.3 ilk ölçüm, ülke serbest metin uyarısı).

### MOD-0181-FU01 DL-1 (sayım satırı görünen kodları, atanan süzgeci) · VERIFIED kod (2026-11-18), canlı ölçüm açık

- Commit `47c0d4f78` (20 dosya, yalnız InventoryService; frontend / gateway / sözleşme / execution / .antigravity farkı 0). F-11
  (ScopeSkusAsync kalmadı) kapandı. Geliştirici sabotajı 18 / 18 kırmızı.
- CT: Inventory 2648 / 0 / 4 (`47c0d4f78`), hat ucunda 2656 / 0 / 4. Atanan süzgeci şirket + soft delete süzgecine AND (S-FU01-10
  kanıtı); yeni indeks `ix_wms_count_assignee_status` TenantId ile başlar.
- Açık: AC-15 E4 canlı ölçümü (Gateway üzerinden dört üye; canlı PRODUCT-MASTER getSku `name`, B-4). Mobil bilinmeyen üye toleransı
  teyidi mobil yayın öncesi (karar kaydı :107-108).
- DÜŞÜK not: AC-7 ("skuName dilden bağımsız") için paketin tek satırlık sabotajı kırmızı vermiyor — Application katmanı isteğin diline
  erişmediği için kural yapısal olarak sağlanıyor, test bunu kanıtlamıyor. Düzeltme gerekmiyor; dil taşıma eklenirse test güçlendirilir.

### MOD-0040-FU02 DL-4 (TTY + PLN kayıt kodu) · VERIFIED kod (2026-11-18), CT-2 canlı sayım açık

- Commit `2a9a21759` (73 dosya; Platform + Web; gateway / sözleşme / .antigravity / tests/architecture / execution farkı 0; yeni izin
  anahtarı yok). Paket r18 `1dbd7180` onaylı. Geliştirici sabotajı S-117…S-139-WP kırmızı.
- CT: Platform 9584 / 19, Web 4765 / 3, Architecture 226 / 5, vitest 5744 / 13 — hepsi taban adlarıyla aynı; yeni testler yeşil.
- Sapmalar: (b) `TaskTypeCodeHistoryEntryDto` (MOD-0024 TaskJsonContractTests gereği; JSON aynı) kabul; (g) üç deneme dolunca sayaç
  artışının commit olması kabul; (h) tek aktif varsayılan plan denetiminin koddan önceye alınması kabul; (d) `code: null` cevabı
  çerçevenin zorunlu üye metni (DL-4 öncesi davranış) not. (e) plan işlemi commit olamazsa 500 (503 değil; GlobalExceptionHandler,
  DL-4 öncesi) — açık, sahip kararı bekliyor.
- Açık: CT-2 canlı sayımları (`^[A-Z]{2,5}-TTY-[0-9]{6,}$`, `^PLN-[0-9]{6,}$`, CreatedAt'siz plan sayısı) canlı turda.

### MOD-0180 DL-3 (dalga ilerleme / risk / istisna / recall) · VERIFIED kod (2026-11-18)

- Commit'ler `924620695`, `9793aadbe`, `a4f2d49db`, `88e0a465f`, `4f580660b`, `887df7c07`, `bf70a9995`, `d52110e14`, `f419641ac`
  (B2). Paket r11 `226ab7f9`. Gateway X-14 123 → 128 (yalnız test sayısı; `ocelot.json` farkı 0), X-10 148 → 151, X-9 4 → 5.
  Geliştirici sabotajı 21 / 21 kırmızı.
- CT: Inventory 2656 / 0 / 4, Gateway 384 / 384 (`2a9a21759`); Web / Architecture / vitest aynı uçta yalnız taban.
- B2: `OutboundShipmentRepository.DeferredPlannedBinsAsync` şirket süzgeciyle okur; `DeferredPickPlanRegistration` opsiyonel
  bağımlılık — kabul (sahip, 2026-11-18).
- Sahip kararları (Beste, 2026-11-18; CT önerileri): (1) Açık 1 — BL-501 merkezi denetim sayacı kırmızıları test altyapısı kusuru
  (paylaşılan test DB'sinde kiracısız `ReleaseOrphansAsync` süpürmesi); MOD-0178 ortak test host'unda ayrı test-only commit ile
  düzeltilir (XMC-001 izni Beste), üretim koduna dokunulmaz; (2) taslak sözleşmede WaveException `outboundId` / `outboundNumber`
  opsiyonel ve `progress` görev sayıları DL-4 sözleşme dondurmasında; (3) WaveOrder pick/pack/shipStatus anlamı DL-4 ekran işinde;
  (4) "Riskli olanlar" süzgeci DL-4; (5) perm-label ailesi `secondary`, Perm.Entity `WAVESEXCEPTIONS` kabul; (6) tasarım notları
  (anahtarda onay sıra numarası, başarısız recall anahtar saklamaz, istisna eylemi dalga Version'ını artırır, yeniden Ineligible ikinci
  kayıt açmaz) kabul; (7) geliştirme worktree'leri bu kayıttan sonra kaldırılır.
- Açık 1 kapandı (2026-11-18): `6cdb92e43` (test-only, XMC-001 Beste) — ortak `InventoryApiFactory` her test host'unda
  `CentralAudit:DispatcherEnabled=false` (üretim varsayılanı `true`, `CentralAuditOptions.cs:49`, değişmedi); iki açık-sweep testi
  zaten `PutawayCentralAuditAloneCollection`'da. Yeni `SharedTestHostCentralAuditTests` (2 test); sabotaj (ayar kaldırıldı) 2 / 2 kırmızı
  ("Expected AwaitingCommand, Actual Pending" — kök neden doğrudan). CT: Inventory 2662 / 2658 / 0 / 4 (`6cdb92e43`).

## Ek — 2026-11-19 / 2026-11-20 CT doğrulaması

CT koşuları satır sonu LF ayrık worktree'lerde, süzgeçsiz; `INVENTORY_TEST_MONGO` boş, 27017 yok. Kırmızılar adlarıyla aynı günün
tabanıyla karşılaştırıldı.

| Takım | Ref | Kırmızı / toplam | Taban | Not |
|---|---|---|---|---|
| Platform.Application | `ea8554906` | 19 / 9619 | 19 / 9603 (`a39a9c644`) | aynı adlar; 16 yeni test yeşil |
| Platform.BackgroundJobs | `ea8554906` | 1 / 93 | 1 / 93 | aynı |
| Platform.CompositionGuard | `ea8554906` | 0 / 12 | — | |
| Platform.Eventing | `ea8554906` | 0 / 68 | — | |
| MDM.Application | `ea8554906` | 75 / 3057 | 73 / 3056 | +2 zamanlama testi (Mongo başlatma, zaman aşımı iptali); tek başına 2 / 2 yeşil, modül kaydıyla ilgisiz |
| MDM.Api | `ea8554906` | 0 / 1283 | 1 / 1283 | tabandaki kırmızı kararsız yarış testi |
| TenantArchitecture.ArchitectureTests | `ea8554906` | 0 / 231 | — | |
| Diten.Web.Tests | `a39a9c644` | 1 / 4911 | taban | ReturnDetailsBehaviorTests (MOD-0186) |
| vitest (tam) | `a39a9c644` | 2 / 5802 | taban | iki diyalog dosyası, Windows yol ayracı |
| SupplyChain (MOD-0183) | `af4a3a9a9` | 0 / 1050 geçen, 1 atlanan | — | test Mongo'su ayrı replica set (27023); DL-8'in 14 yeni testi dahil |
| TenantArchitecture.ArchitectureTests | `af4a3a9a9` | 231 / 231 geçen | — | DocsPathGuard repin `9b89fd8c3` sonrası |
| Diten.Web.Tests | `af4a3a9a9` | 1 kırmızı, 4884 geçen | taban | MOD-0186 konsol kodlaması |
| vitest (tam) | `af4a3a9a9` | 2 kırmızı, 5788 geçen | taban | Windows yol ayracı |

### MOD-0049-FU01 FIX-01 (kayıt sahipliği 403 / 409) · VERIFIED kod + canlı kayıt (2026-11-19)

- Commit `ea8554906` (hat ucu): `InternalModuleRegistrationController` sahiplik denetimi (403) ve
  `RegisterModuleManifestCommandHandler.ValidateCatalogServiceOwnershipAsync` (409). CT sayıları yukarıdaki tabloda.
- Canlı: MDM yeniden başlayınca `business-partner-master` ve `brand-product-master` kataloğa kaydoldu (ürün modülünün isteği 100 sn'de
  zaman aşımı; o modül zaten katalogdaydı). GMG kiracısının STARTER planına Business Partner Master eklendi (sahip, 2026-11-19).
- Açık: Auth izin kataloğunda (`DataSeeder.cs`) altı `mdm.business-partners.read|export|create|update|deactivate|activate` anahtarı
  yok, bu yüzden RoleAssignments'ta görünmüyorlar → **FIX-02** (Auth; XMC-001, Ali onayı bekleniyor). Ardından canlı AC-15
  (kiracılar arası 404) + S-15 ve AC-24 tarayıcı turu; DL-2 bundan sonra VERIFIED olur.

### MOD-0180 DL-4 canlı tur düzeltmeleri · VERIFIED (2026-11-19)

- Commit `a39a9c644`. CT: Web 1 / 4911, vitest 2 / 5802 — yalnız taban.
- Canlı ölçüm: 375 / 800 / 1280 px'te üç sayfada yatay taşma yok; süzgeçteki iki tarih ayrı kutularda ve adları doğru; üç sayfanın
  "depo seçin" metinleri doğru.
- Açık: dalga akışının uçtan uca canlı turu (geri sayım, 1 / 1 görev) — CT yapacak; X-13 rol şablonu Ali (FU32).

### MOD-0178-FU01 DL-3 (CRM'den alıcı) · AC-31 canlı kapandı (2026-11-19)

- AC-31 (a) CRM müşterisiyle, (b) elle, (c) CRM kapalıyken kayıt — üçü de canlıda geçti ((c) negatif deneme dahil).
- Sahip turu (Beste, 2026-11-19): Enter tuşu, "?" yardım metni, tur yok — üçüne "tamam". Yeni çıkış sevkiyatı formu 375 px'te tr / en / ar
  görüntülerinde yatay kaydırma yok, ar sağdan sola.
- Bu kayıtta ayrıca ölçülmüş olarak yazılamayanlar: §4.3 Reference Data ilk ölçümü ve ülke serbest metin uyarısı kontrolü (2026-11-18
  ekinde VERIFIED koşulu olarak sayılmıştı). DL-3 VERIFIED ilanı bu iki maddenin ölçümüne bağlı kalır.

### MOD-0183 DL-8 + DL-9 · VERIFIED kod (2026-11-19)

- DL-8 commit'leri `20aaffc78`, `43e880b67`, `c0636fa99`; DL-9 (detay ekranında GUID yok, metinler 7 dil) aynı hat ucunda. CT
  `af4a3a9a9` ucunda: SupplyChain 1050 / 0 (1 atlanan), Architecture 231 / 231 (CT repin `9b89fd8c3`), Web / vitest yalnız taban.
- Açık: DL-8 CR-5 canlı Gateway ölçümü (MVP-6 `gateway_tour_probe.py`, #456 main'de); DL-9'un depo girişi vakaları DL-5'ten sonra;
  DL-5.

### MOD-0181-FU01 DL-1 AC-15 E4 · canlı ölçüm (2026-11-20, CT)

- Sayım GMG-CNT-000002 (countId `c008125e-8ec1-44e0-b7ab-fd1a469a9901`, D01 · İstanbul Merkez Depo, Released
  2026-11-19T07:52Z, 2 satır). Web proxy `GET /Warehouse/CycleCounts/lines/{id}` → 200.
- İki satırda da dört görünen üye dolu: `locationCode` A-01-01-02, `skuCode` LSKU-TR-000045, `skuName` "Amoksisilin 500mg Kapsül · TR",
  `lotNumber` AMX-2604 / G1-20261020; `namesAvailable` true.
- Sonuç: canlı PRODUCT-MASTER `getSku` `name` gönderiyor — paketin :538 belirsizliği kapandı. AC-15 kanıt maddesinin canlı kısmı tamam.
- Açık kalan: mobil bilinmeyen üye toleransı teyidi mobil yayın öncesi (karar kaydı :107-108).

### MOD-0177-FU01 DL-2 canlı tur (CT, 2026-11-20) · kısmi

- SupplyChain (5066) filoda yeniden başlatıldı (sahip onayı, 2026-11-20). Sahip onayıyla test vakası açıldı: **RC-2026-0002** (Draft,
  sınıf 3, Toptan, kök lot AMX-2604, ileri iz; gerekçe "CT-TEST — … gerçek geri çağırma değildir", referans CT-TEST).
- İz: tek sevkiyat (`1bf316a3-…`, Delivered, `shipToReference` CT-TUR-MUSTERI-01, `customer.state` NoCrm). Dev verisinde CRM `customerRef`
  taşıyan sevkiyat yok.
- Müşteriler ve sevkiyatlar sekmesi 375 px'te en / tr / ar ölçüldü: yatay kaydırma yok (scrollWidth 375); ar `dir=rtl`; CRM'siz satır üç dilde
  doğru rozetle ("No CRM identity" / "CRM kimliği yok" / "بلا هوية CRM"); tarih biçimleri dile göre.
- Açık: CRM adlı satırın (ad + kod) ekranı — CRM hesabına bağlı (`customerRef` = CRM `Account.Id`) bir sevkiyat ve o lotun mal çıkışı
  gerekiyor; D-4 (`crm.account.lookup-validation` rol eşlemesi) Ali'de.

### MOD-0178-FU01 DL-3 — §4.3 / Q-11 Reference Data ölçümü ve eski serbest metin ülke (CT, 2026-11-20)

- Q-11 ölçümü: ülke listesi Web `GET /Warehouse/OutboundShipments/countries` → Platform consumable-sets (`country`), 15 değer. en / tr / ar /
  ru / zh / fr / es isteklerinin hepsinde adlar **Türkçe** ("Almanya", "Türkiye"): dev `country` kümesinde `label_<dil>` öznitelikleri yok,
  `ReferenceValueDto.TextFor` kümenin tek etiketine düşüyor. Kod paketle uyumlu; paketin Q-11 önerisi ("7 dilde ad yoksa en'e düşülür")
  karşılanmıyor — veri eksiği. Açık: `country` (ve `city`, `district`) kümelerine 7 dilli etiket yüklenmesi ya da Q-11 kararının "kümenin
  etiketi" olarak yazılması (sahip kararı).
- Eski serbest metin ülke (AC-26, F-11): FU01 öncesi GMG-OUT-000001 / 000003 (`country` "TR", `countryName` null). GMG-OUT-000003 Edit'inde
  ülke seçicisi "TR (kayıtlı değer)" önceden seçili ek seçenek olarak geliyor; sevke hazır olduğu için form "artık değiştirilemez" uyarısını
  gösteriyor. Davranış pakete uygun.
- Sonuç: AC-31 (a–c) kapalı (2026-11-19); §4.3 / Q-11 ölçüldü (veri eksiği açık); eski ülke davranışı doğru. DL-3 VERIFIED için kalan tek
  nokta Q-11 sahip kararı.

- Q-11 sahip kararı (Beste, 2026-11-20, karar (b) — kümenin kendi etiketi): `mod0178-fu01-outbound-form-v2-owner-decisions-01.md` son ek.
  **MOD-0178-FU01 DL-3 · VERIFIED (2026-11-20).**

### MOD-0183 CR-5 canlı (CT, 2026-11-20) · PASS — ekran yolu, betiksiz

- Sahip kararı (Beste, 2026-11-20): CR-5 canlıda, normal filo portlarında, ekran yolu (Web → Gateway 5000 → SupplyChain 5066) ile ölçülür;
  betikte parola / tüzel kişi / rol adımları kaldırıldı (`36326dcdf`, `6679516ae`).
- Sevkiyat `db807cf3-090c-4eed-9149-fdf40348eb97` (CT-TEST-SO-0177FU01-01; GMG D01; CRM `customerRef` 5M MİGROS PH-192739; AMX-2604,
  A-01-01-01, 1 EA): oluşturma 201 (gönderilen `X-Correlation-Id` e7353849-… yerine Gateway 4a863050-… üretti; saklı
  `LifecycleCorrelationId` = 4a863050-…) → Planned 200 → Dispatched 200 (Inventory mal çıkışı **INV-2026-0000023**) → InTransit 200 → POD 201,
  Delivered. C yolu (r11) canlıda doğrulandı.
- Merkezi denetim: `central_audit_outbox` 5 satır (Create, 3 × LifecycleTransition, Update), hepsi Delivered; Platform
  `diten_personalization_dev.audit_events` 5 olay (EntityId ikili GUID, STANDARD).
- Bulgular → MOD-0183 düzeltme işi (Beste, 2026-11-20):
  - L-1 Sevkiyat numarası `SHP-{guid:N}` (`CreateShipmentHandler.cs:33`, `WarehouseShipmentMapper.cs:48`) — RCS-001 görünen kodu değil;
    kayıt kodu türü + paket revizyonu gerekir.
  - L-2 Detay satırında ürün adı "Ad alınamadı" (Web `FillDetailAsync` SKU etiketi çözülmüyor; MOD-0181-FU01 aynı SKU için adı alıyor).
  - L-3 Durum değiştirme ve POD panelleri başarılı işlemden sonra kapanmıyor, durum yenilenmiyor (istek 200 / 201).
  - L-4 Betiğin Platform denetim kontrolü EntityId'yi metin arıyor; Platform ikili GUID saklar → yanlış kırmızı.
- Ek gözlem: 0187 / 0185 incelemesi canlı Gateway'in `Observability:Correlation:SigningKey` olmadan çalıştığını ölçtü (kendi kökünü de
  benimsemez); C yolu kararı bu durumdan bağımsız geçerli.

### MOD-0177-FU01 DL-2 CRM satırı (CT, 2026-11-20) · açık bulgu

- Recall izi (AMX-2604 ileri) yeni sevkiyatı `customer.source` Crm, `state` Resolved, "5M MİGROS ALIŞVERİŞ MAĞAZASI · PH-192739" olarak
  gösteriyor (ikinci okumada; ilk okumada `shipment.read` Unavailable).
- RC-2026-0002 sahip onayıyla iptal edildi (kapsam yeni sevkiyattan önceydi); **RC-2026-0003** açıldı (CT-TEST). Kapsamda yeni sevkiyat
  `customerRef` = `DOC:db807cf3-…`, `source` Document, NoCrm; ekranda "TR, TR-35-IZMIR, ÇİĞLİ · No CRM identity".
- Çıkarım: vaka açılırken sevkiyat okuması geçici olarak Unavailable döndü ve anahtar `DOC:` olarak saklandı; paket kuralı gereği sonradan
  CRM'e yükseltilmiyor. Açık: (1) Inventory → SupplyChain sevkiyat okumasının aralıklı Unavailable nedeni (ilk çağrı / zaman aşımı) ölçülmeli;
  (2) CRM adlı satırın vaka ekranında ölçümü bu düzeltmeden sonra.

### MOD-0186 / 0187 / 0185 bağımsız CT incelemesi (2026-11-20, main `2de950edc`) · KOŞULLU

- SupplyChain tam koşu 1037 / 1036 / 0 / 1 (atlanan taban). Kiracı ve izin sabotajları kırmızı (0186 25 / 2; 0187 11 / 1; 0185 23 / 5);
  korelasyon kök kontrollerinin koruyucusu yok (0186 S3, 0187 C3 / C4, 0185 L3 → 0 kırmızı).
- 0186: C yolu sahip kararı (`mod-0186-correlation-c-path-owner-decision-01.md`) + paket r11 (`b1573204b`, DL-6, AC RC-1…RC-9) — CT-APR-02 /
  CT-APR-01 bekliyor. 0187 ve 0185 için aynı revizyon gerekir; ayrıca GUID gösteren liste sütunları (0186 F-13, 0187 Claims/index.js:1051,
  0185 Loads/index.js:527-528), 0187 adlı kiracılar arası 404 testi, canlı turlar.

### MOD-0180 dalga akışı canlı turu (CT, 2026-11-20) · tamam — DL-4 VERIFIED

- GMG-WAV-000001 (D01, Kargo, kesim 20.11.2026 16:00 depo saati Europe/Istanbul, Released): detay sayfası 10:14:55Z "Kesime 2 sa 45 dk kaldı",
  sayfa yenilenmeden 10:16:15Z "Kesime 2 sa 44 dk kaldı" (AC-46 geri sayım canlı).
- Toplama görevi GMG-PCK-000007 (GMG-OUT-000007, AMX-2604, A-01-01-01 → A-01-01-02, 1 EA) Web `confirm` → 200, defter cevabıyla Completed
  (confirmed 1.000). Dalga detayı: "Toplama %100 · 1 görevden 1 tamam"; sipariş satırı Toplama "Tamam", Paketleme / Sevk "Bekliyor" (K-3, K-5).
- MOD-0180 için açık yalnız X-13 rol şablonu (Ali, FU32). D01'de dalga yönetimi açık kalıyor (sahip bilgisi).

### MOD-0183 FIX-LIVE-01 sonucu (2026-11-20)

- L-2 kök nedeni geliştirme ortamı: Web `Inventory__ProductMasterBaseUrl` = CT dev köprüsü 4010; köprü `POST /skus/bulk-read` sözleşmenin
  `skuIds` alanını okumuyordu (`{"items":[]}`). Denetleyici ve MDM sözleşmeye uygun. CT köprüyü düzeltti (skuIds + notFound) ve yeniden başlattı;
  canlı detay "LSKU-TR-000045 · Amoksisilin 500mg Kapsül · TR". Repo değişikliği yok.
- L-3 geliştirici oturumunda gerçek arayüz akışıyla yeniden üretilemedi (başarıda panel kapanıyor, toast, yeniden okuma); gözlem CT'nin betikle
  tetiklediği gönderimden — kapatıldı.
- L-4 CT zincirinde düzeltildi (`3aea5924c`, Platform Guid'leri STANDARD UUID olarak okunur; canlı sevkiyat için 5 / 5 olay).
- L-1 (`SHP-{guid}` numara → RCS-001) ayrı FU; paket revizyonu gerekir.
- Sevkiyat okumasının aralıklı Unavailable'ı (MOD-0177-FU01): 4 dk boşta bekleme sonrası ilk okuma 133 ms, Ok — "soğuk ilk çağrı" varsayımı
  doğrulanmadı; yeniden üretilemedi, izlemede.

### MOD-0049-FU01 FIX-02 (Auth kataloğu, `e478ea496`, XMC-001 Ali 2026-11-20) · VERIFIED kod (2026-11-20)

- Kapsam 3 dosya: `DataSeeder.cs` (6 anahtar `mdm.business-partners.read|export|create|update|deactivate|activate`, moduleOverride
  `business-partner-master`), `permission-scope-baseline.csv` (6 satır, Tenant), yeni `BusinessPartnerPermissionCatalogTests` (anahtar kümesi,
  modül, Tenant, silme anahtarı yok, Admin şablonu vermez). Rol şablonu / kiracıya otomatik atama yok; MDM / Platform / gateway / execution
  farkı 0. Etiket gerekmedi (`Perm.Entity.BUSINESSPARTNERS` 7 dilde, fiiller `perm-label.js`'te var).
- CT: `Diten.AuthService.Application.Tests` (ayrık LF worktree) 3083 / 3012 / 52 / 19; 52 kırmızı dört ortam sınıfında, `ea8554906`'da aynı
  52 ad — yeni kırmızı 0. Sabotaj: `activate` satırı silindi → 3 kırmızı; `update` moduleOverride değişti → 1 kırmızı.
- Canlı: `SeedPermissionsAsync` her açılışta eksik anahtarları ekler (`DataSeeder.cs:235-245`) → Auth yeniden başlatılınca 6 izin katalogda,
  hiçbir role atanmadan. Ardından canlı AC-15 / AC-24.

### AUTH-HDR-01 (Auth başlık tavanı 64 → 128 KB, `f85d819a5`, XMC-001 Ali 2026-11-20) · VERIFIED kod (2026-11-20)

- Canlı bulgu (CT): `/RoleAssignments` (direct-gateway-profile) `GET :5000/api/roles` ve `/api/permissions/tenant-assignable` → 431. Kiracı 97c59330
  Admin rolü 681 izin → access token ≈ 42 KB; gateway çerezi `Authorization`'a kopyalar (`gateway/Diten.ApiGateway/Program.cs:173-183`), Ocelot
  çerezle birlikte Auth'a iletir → ≈ 85 KB > 64 KB. Tavan ölçümü: 5000 / 5056 60 KB X-Pad → 401, 5000 70 KB → 431; aynı çerezlerle 5001 → 200.
  Kalıcı çözüm backlog'da (yetkiler token'dan çıkar; Ali). Seçenek kararı Beste: Auth tavanı (2026-11-20).
- Kapsam 2 dosya (+53 / −1): `Diten.AuthService.Api/Program.cs` `MaxRequestHeadersTotalSize = 128 * 1024` + ölçüm yorumu;
  `CompositionRootGuardTests.Auth_kestrel_header_limit_takes_a_100KB_header_over_the_wire` (gerçek host'un Kestrel tavanı ≥ 128 KB; gerçek dinleyicide
  ~100 KB başlık: Auth tavanı 200, 64 KB 431). Token / izin modeli / başka servis farkı 0.
- CT (ayrık LF worktree `erp-vnext-wt-ct-lf`, `f85d819a5`): `Diten.AuthService.Application.Tests` 3084 / 3013 geçen / 52 kırmızı / 19 atlanan;
  52 ad geliştirici tabanıyla (`e478ea496`) birebir — yeni kırmızı 0; yeni test yeşil. Sabotaj: tavan 64 KB → 1 kırmızı (yeni test); geri alındı, ağaç temiz.
- Risk notu: direct-gateway sayfalarından Platform / MDM / CRM gibi diğer servislere giden istekler aynı ≈ 85 KB ile o servislerin 64 KB tavanına
  çarpabilir — ölçülmedi.

### GW-HDR-01 (Gateway: token çerezi Authorization'a taşınınca arkaya iletilmez, `114ef3df1`, Beste 2026-11-23) · VERIFIED kod (2026-11-23)

- Canlı bulgu (CT, 2026-11-23): Platform yöneticisi oturumuyla `POST :5000/api/admin/tenants/97c59330…/record-code-group-prefix` → 431;
  aynı çerezlerle Gateway `/health` 200 → 431 arkadaki Platform'dan (token çerez + Authorization ikilemesi, 64 KB tavan). AUTH-HDR-01'in genel hali.
- Kapsam 3 dosya (+308): `Authentication/AuthTokenCookies.cs` (`RemoveAccessTokenCookiesWhenAuthorized`, `StripAccessTokenCookies`:
  `access_token` ve `access_tokenC<n>` büyük/küçük harf duyarsız; benzer adlar kalır; diğer çerezler sırasıyla kalır; Authorization yoksa
  değişiklik yok), `Program.cs` (ara katman `UseAuthorization` ile `UseOcelot` arasında; kimlik doğrulama ve TenantResolutionMiddleware çerezi
  okuduktan sonra), yeni `AccessTokenCookieForwardingTests` (5 birim + 1 uçtan uca gerçek Program/Ocelot/ocelot.json, 42 KB token 12 parça).
  Tavanlar, `ocelot.json`, servisler, Auth/Web değişmedi.
- Geliştirici ölçümü: arkadaki hiçbir servis `access_token` / `refresh_token` / `auth_ticket` çerezini okumuyor (Auth refresh / logout gövdeden).
- CT (ayrık LF worktree `erp-vnext-wt-ct-lf`, `114ef3df1`): `Diten.ApiGateway.Tests` 390 / 390 (taban `b4116e3d2` 384 / 384; +6 yeni, kırmızı 0).
  Sabotaj: çıkarma satırı kaldırıldı → 1 kırmızı (`A_42_KB_cookie_token_reaches_downstream_once_in_Authorization_and_under_the_64_KB_ceiling`);
  geri alındı, ağaç temiz.
- Not: 64 KB tavan değişmedi; tarayıcı → gateway yönünde token bir kez gidiyor. Kalıcı çözüm (izinleri token'dan çıkarmak) backlog'da (Ali).

### Dev ortamı: RCS-001 kayıt kodu bağlantısı + GMG grup öneki (CT, Beste onayı 2026-11-23)

- Bulgu: dev'de MDM `RecordCodeAllocator` ve Platform `TrustedRecordCodeCredential:Mdm` boştu → MDM her ortak kayıt kodu isteğini
  "port not configured" ile reddediyordu (ekran: "Kod servisine ulaşılamadı"; 503, kayıt yok). Ortak kayıt kodu servisini dev'de kullanan ilk modül.
- Bağlama (repo değişikliği yok): sır repo dışında `%LOCALAPPDATA%\Diten\dev-secrets` (kullanıcı profili), iki pencereye yalnız süreç ortamıyla
  (komut satırında yok, basılmadı). Betik: CT scratchpad `start-platform-mdm-recordcode.ps1`. Bağlama: kimlik `mdm-dev-recordcode`, tüketici
  `DITENMDMSERVICE`, hedef `TRUSTED_RECORD_CODE_ALLOCATE`, türler BPT/BNK/BAC/AGR.
- Sonra Platform 409 `GROUP_PREFIX_MISSING`: `record_code_group_prefixes` boştu. Platform yöneticisi oturumuyla
  `PUT /api/admin/tenants/97c59330…/record-code-group-prefix` (gövdesiz) → 200 `GMG`, 08:47:13Z; ilk kodla kilitlendi 08:48:19Z.
  (İlk deneme GW-HDR-01 öncesi 431 idi.)

### MOD-0049-FU01 canlı tur (CT, 2026-11-23) · AC-24 PASS, AC-15 kısmi

- İzinler: Beste Admin'e 6 `mdm.business-partners.*` iznini rol ekranından verdi (Admin 687 izin); yeniden girişten önce sayfa 403 (beklenen).
- AC-24 (tr): oluştur `CT-TEST Lojistik A.Ş.` (3PL, TR, İstanbul) → ayrıntı → düzenle (İlçe Kadıköy, PUT 204) → pasifleştir (gerekçe; Düzenle düğmesi
  kayboldu) → etkinleştir (Düzenle geri geldi); Geçmiş sekmesi Oluşturuldu / Güncellendi olaylarını gösteriyor. (en): form / ayrıntı / düzenle
  (posta kodu 34710) / Deactivate / Activate İngilizce metinlerle. Ağ: tüm istekler 5001 same-origin vekil (`/MasterData/BusinessPartners/api…`);
  tarayıcıdan 5059 ya da 5000'e istek 0.
- AC-15: kod **GMG-BPT-000001** (id 1403f89f-cb79-4df1-a15f-8d9efd98f65f); `role=ThirdPartyLogistics&status=Active` → 1, `status=Inactive` → 0;
  kod servisi kullanılamazken oluşturma 503 ve kayıt yok (bağlama öncesi iki deneme — Platform durdurularak ayrıca ölçülmedi); başka kiracı 404
  açık (ikinci kiracı kullanıcısı gerekir). `run_record.py` kaydı alınmadı — bu kayıt canlı kanıttır.
- Bilinen: ülke Türkçe ekranda "TR — Turkey" / ayrıntıda "TR" (referans veri Türkçe etiketi yok, önceki bulgu).
- **Beste tasarım bulguları (2026-11-23, açık — düzeltme işi):** (1) ayrıntı sayfası sekmeleri standart değil → Work Center sekme düzeni;
  (2) Geçmiş sekmesi tablo yerine Work Center "Etkinlik & Yorum" benzeri zaman çizelgesi; (3) oluşturma / düzenleme formundaki iletişim kişileri
  standart DataTable olmalı, kişi ekleme Golden Reference'taki gibi offcanvas.

### MOD-0040-FU02 CT-2 canlı sayım (CT, 2026-11-23) · PASS

- Abonelik planı: Platform yöneticisi ekranından `CT-TEST Plan` → **PLN-000001**; 8 planın 1'i `^PLN-[0-9]{6,}$`, 7 eski plan kodunu korudu
  (FREE, STARTER, PROFESSIONAL, ENTERPRISE, 3 deneme), `CreatedAt`'siz plan 0.
- Görev türü: kiracı ekranından `CT-TEST Görev Türü` → **GMG-TTY-000001** (önerilen kod formda göründü); 1 / 1 `^[A-Z]{2,5}-TTY-[0-9]{6,}$`,
  `CreatedAt`'siz 0. Admin'e `platform.tasks.task-types.manage` Beste tarafından verildi.
- Gözlem: kiracı kullanıcısına dönüşte bir giriş denemesi `/account/login?reason=tenant-unavailable`'a düştü (Platform kiracıyı Aktif gösteriyor);
  sonraki giriş çalıştı. Neden ölçülmedi (Web liveness önbelleği şüphesi) — izlemede.

### MOD-0177-FU01 CRM müşteri satırı canlı ölçümü (CT, Beste onayı 2026-11-23) · DL-2 canlı PASS + kök neden bulundu

- RC-2026-0003 (DOC anahtarlı) iptal edildi (gerekçe kayıtlı); aynı lot AMX-2604 (GMG şirketi) için **RC-2026-0004** açıldı
  (id 2c2740d2-6cf0-4c42-a85d-e3fc51c65954, 3. sınıf, Taslak, neden "CT-TEST").
- Vaka "Müşteriler ve sevkiyatlar": `5M MİGROS ALIŞVERİŞ MAĞAZASI · PH-192739` (CRM, INV-2026-0000023) ve `CT-TUR-MUSTERI-01` "CRM kimliği yok"
  (shipToReference, INV-2026-0000014) — anahtar sırası customerRef → shipToReference → DOC canlıda doğrulandı.
- **Kök neden (RC-2026-0003'ün DOC anahtarı):** günün ilk izleme sorgusu `durationMs` 2168 — iki sevkiyat okuması da `Unavailable`
  (`ShipmentBundleHttpReader` istek başına 2 s), müşteri `DOC:` düştü. Hemen ardından aynı sorgu 76 ms, iki okuma `Ok`, CRM Resolved. Yani
  "aralıklı Unavailable" = soğuk ilk çağrının 2 s'yi aşması (2026-11-20 "4 dk boşta" denemesi soğuk durumu üretmemişti).
- Yan bulgu: izleme ekranı `read != Ok` için "Sevkiyat ayrıntıları okunamadı (eksik alan)" diyor (`trace-view.js:65`, `Recall.Shipment.Unreadable`);
  gerçek durum "erişilemedi" — metin yanıltıcı.
- **Önerilen FU (Beste kararı bekliyor):** (a) vaka açılışında herhangi bir sevkiyat okuması `Unavailable` ise vakayı DOC anahtarıyla dondurmak yerine
  açmayı reddet / uyar ve yeniden denet (kapsam açılışta donuyor); (b) okuma için tek yeniden deneme ya da soğuk başlangıçta daha geniş bütçe;
  (c) ekran metnini `Unavailable` / `Forbidden` / `NotFound` için ayrı yaz (7 dil).

### MOD-0049-FU01 FIX-03 (İş Ortakları ekranları standarda, `e5e953edf` + `6c5e128c7`, Beste 2026-11-23) · VERIFIED kod (2026-11-23)

- Kapsam 15 dosya (+710 / −165), yalnız İş Ortakları Web ekranları; ortak dosya / başka modül / MDM değişmedi. Ayrıntı sekmeleri `/WorkCenterNext/Details`
  sekme şeridi sınıfları (`wcn-detail-tabcard`, `wcn-tabs`, `wc-tab-compact`, sayaçlar); Geçmiş → `ul.wcn-audit` zaman çizelgesi (yorum kutusu yok,
  veri `/api/{id}/history`); kişiler → `_ListShell` + `DitenDataTable.createList` (istemci modu), ekle / düzenle `_ContactOffcanvas` (Golden Slim deseni,
  `data-dt-primary="enter"`); gönderim gövdesi aynı. Yeni anahtarlar 7 dilde. (Golden Compact'ta offcanvas yok — DataTable Compact'tan, offcanvas Slim'den.)
- CT (ayrık LF worktree, `6c5e128c7`): `Diten.Web.Tests` 4916 / 4915 / 1 (taban kırmızı `ReturnDetailsBehaviorTests.DetailsJs_shows_names_not_ids…`;
  geliştirici tabanında da var) — yeni kırmızı 0; vitest 332 dosya, 2 kırmızı dosya (`dialog-one-implementation`, `wcn-dialog-one-language` —
  Windows yol ayırıcısı ortam kırmızıları, geliştirici taban listesinde) — İş Ortakları ile ilgisiz. Sabotaj: `_Form.cshtml`'den `_ContactOffcanvas`
  partial'ı kaldırıldı → 2 kırmızı (`FIX03_the_contacts_are_a_standard_client_datatable_edited_in_a_side_panel` Create / Edit); geri alındı, ağaç temiz.
- Geliştirici: UIC seçici 61 PASS, verify_datatable tek boşluk tabanla aynı (`#offcanvasDetailsPreview`, Index). Dev'de test kaydı `GMG-BPT-000002`.
- Açık (Beste'ye soruldu): zaman çizelgesinde kişi adı yerine "Bilinmeyen kullanıcı" — geçmiş verisi yalnız kullanıcı kimliği taşıyor, ad çözülmüyor.

### MOD-0177 FIX-UI-01 (`2534436c2`) + WEB-ACTOR-NAME-01 (`bc693707c`) (Beste 2026-11-23) · VERIFIED kod (2026-11-23)

- FIX-UI-01: `/Recall/Cases/Details` sekmeleri WorkCenterNext şeridi (`wcn-detail-tabcard`, `wcn-tabs`, `wc-tab-compact`), altı sekme tablosu `_ListShell`
  (client) + `DitenDataTable.createList`, zaman çizelgesi `ul.wcn-audit`; sunucu çağrıları / gövdeler aynı ("Dahil / hariç kaydet" işaretleri sayfa
  durumundan — sayfalamalı liste). 1 yeni anahtar 7 dilde. Sekme tablolarında dışa aktarma / görünüm kaydı YOK (karar Beste'ye soruldu); boş tablo metni
  ortak `dt-defaults`. Vakalar listesi zaten standarttı (değişmedi).
- WEB-ACTOR-NAME-01: yalnız İş Ortakları — `BusinessPartnersController.History` benzersiz aktör kimliklerini (≤ 40) Web'in mevcut çözümleyicisiyle
  (`QuarantinePeople.ResolveAsync` → AuthService `GET /api/users/{id}/display-label`, çağıranın token'ı, kiracı kapsamlı) `actorName` olarak ekler;
  çözülemeyen → "Bilinmeyen kullanıcı", kimlik çizilmez. Recall zaman çizelgesi DEĞİŞMEDİ: sahip kararı 1-A / WP-MVP1-NAMES1 (§19.23) — ad servisin
  işlem anında sakladığı addır (RC-2026-0004'te "Admin User" görünüyor); commit mesajındaki "recall case" ifadesi kapsamı aşar, kod yalnız İş Ortakları.
  Ad yalnız `auth.users.lookup` taşıyan okuyucuya görünür (açık karar, Auth / Ali).
- CT (ayrık LF worktree, `bc693707c`): `Diten.Web.Tests` 4924 / 4923 / 1 (taban kırmızı `ReturnDetailsBehaviorTests…`) — yeni kırmızı 0; vitest 5810 / 5808 / 2
  (taban ortam kırmızıları `dialog-one-implementation`, `wcn-dialog-one-language`). Sabotaj: `actorName` ataması kaldırıldı → 1 kırmızı
  (`ACTORNAME_the_history_door_names_each_distinct_actor_id_once_with_the_callers_token`); fr resx'ten `Recall.CaseTab.Label` silindi → 2 kırmızı
  (`RecallCaseFixUi01WebTests.The_tab_strip_name_is_in_the_seven_tenant_languages`, `RecallWebTests.Seven_languages_carry_exactly_the_Recall_keys_with_real_text`);
  geri alındı, ağaç temiz. Geliştirici: UIC seçici 176 PASS, tarayıcı (geçici 5102) tr / en, 375 px.

### INT-MAIN-SYNC-01 (origin/main `58eb6fa90` → MVP-1 hattı, merge `938785198`, Beste 2026-11-23) · VERIFIED (2026-11-23)

- `--no-ff` birleştirme (ebeveynler `96fd55f6d` + `58eb6fa90`); 16 çakışma çözüldü (MDM kayıt kodu portu 4 dosya, Platform katalog testi, approvals.tsv,
  reviews.tsv, CT-DECISIONS.md, 7 PageHelpResource, treasury-intake-owner-decisions-07.md). `gateway_tour_probe.py` çakışmadı (main sürümü eklendi;
  CT zincirindeki düzeltilmiş sürüm CT zinciri hatta birleşince gelir).
- Sahip kararları: **1 = C** — MDM `PlatformRecordCodeAllocatorClient` main imzası (`RecordCodeAllocationResult`), yalnız taşıma hatası (zaman aşımı /
  `HttpRequestException` / `IOException`) aynı gövde ve idempotency anahtarıyla 1 kez yeniden denenir (en çok 2 istek, 200 ms), HTTP cevapları (429 / 503
  dahil) yeniden denenmez; tek kayıt `AddInfrastructure`, `PlatformRegistration:BaseUrl` geri düşüşü `PostConfigure`. İş Ortakları davranışı aynen
  (yalnız `RECORD_CODE_PERMISSION_MISSING` → 403, diğerleri 503). **2 = (a)** — hattın D148'i **D149** (main D148 = AGR aynen, numara notu bölümde).
- Plan dışı (Beste onayına sunuldu): İş Ortakları manifest `SortOrder` 336 → **337** (main Banka 336 kullanıyor; hattın koruma testi).
- Defterler önce main, sonra hat; `pack_approval.py history --ref HEAD --base 58eb6fa90` "yalnız ekleme — bozulma yok". PageHelpResource 7 dilde 899 anahtar.
- Geliştirici (10 suite, taban = hat ucu ve origin/main): yeni kırmızı 0 (MDM Api `LskuFix3HttpTests.Fix4_Item01_Fifty_racing…` yük altında bir kez,
  tek başına 3 / 3 yeşil).
- CT (ayrık LF worktree, `938785198`; kırmızılar geliştiricinin hat + main taban birleşimiyle karşılaştırıldı): MDM Application 3172 / 73 kırmızı (yeni 0),
  MDM Api 1294 / 0, Diten.Web.Tests 4920 / 1 (yeni 0), architecture 231 / 0, Platform Application 9619 / 19 (yeni 0). Sabotaj: taşıma hatası yeniden
  denemesi kaldırıldı → 1 kırmızı (`RecordCodeAllocatorClientTests.IntMainSync01_A_transport_failure_is_retried_once_with_the_same_idempotency_key_and_never_more`);
  geri alındı, ağaç temiz.

### Sahip kararları — MOD-0177 FIX-UI-01 / WEB-ACTOR-NAME-01 / INT-MAIN-SYNC-01 açıkları (Beste 2026-11-23)

- Recall vaka sekmesi tablolarına dışa aktarma **eklenmez** (sekme tabloları salt görüntüleme; vakalar listesindeki dışa aktarma aynen).
- Boş tablo metni ortak `dt-defaults` metni olarak **kalır**.
- İş Ortakları menü `SortOrder` 336 → **337** onaylandı (main'de Banka 336).
- Aktör adları: kullanıcı görme iznine (`auth.users.lookup`) sahip herkes görür — bugünkü davranış aynen kalır; izni olmayana
  "Bilinmeyen kullanıcı". Değişiklik yok.

### Sahip kararı — MOD-0177-FU01 okuma nedeni metni (Beste 2026-11-23)

- "c" (erişilemedi / yetki yok / bulunamadı ayrı metin) kabul; ölçüm (MOD-0177-FU01-READTEXT-01, kod yazılmadan durdu): neden vakada saklanmıyor,
  `RecallScopeItem` / RECALL `CustomerDisplay` (`additionalProperties: false`) paket dışı alan istiyor. Karar **seçenek 1**: MOD-0177-FU01 paket
  revizyonu + RECALL sözleşmesi 1.11.0 (yalnız ekleme: `RecallScopeItem.CustomerSourceReason` ve ScopeItem / CustomerRow'da isteğe bağlı neden
  üyesi); vaka ve iz ekranında üç metin, 7 dilde YENİ anahtarlar (var olan `Recall.Read.*` teslim sütununda kalır). Eski vakalarda metin yok.
  Revizyon → CT-APR-02 → CT-APR-01 (onaylayan MOD-0177-FU01 sahibi) → WP.

## Ek — MOD-0177-FU01 r10 açık kararları K-1…K-5 ve K-7 (Beste, 2026-11-24)

Pakette (r10, sha256 `6181b276…`, §19.14) yazar önerisiyle yazılan kararlar sahip tarafından **olduğu gibi kabul edildi** ("K'lar uygun"):
K-1 metnin yeri (müşteriler sekmesinde müşteri hücresi altı, kapsam sekmesinde yer hücresi; `title` ipucu değil), K-2 iz ekranında
`customer.source = Document` düğümünde rozet nedenin metniyle değişir, K-3 zaman eki yok (neden kalemin yazıldığı anın okuması),
K-4 yalnız müşteriler sekmesi, kapsam sekmesi ve iz ekranı, K-5 açılış + `Draft` yeniden izleme + revizyon deltası, K-7 r10 onayına
kadar DL-1 / DL-2 kabul kayıtları r9'u (`c12df7b7`) anar. K-6 daha önce kapandı (CT, inceleme F-1@329d81c0c). Paket metni değişmez;
sıradaki adım onaylayanın (Ali, MOD-0177-FU01 sahibi) r10 onay satırı, ardından DL-3 iş paketi.
