# WP-0183-DL8 — MOD-0183 Shipment Tracking & POD DL-8: korelasyon kökü sunucuda (Gateway arkasında geçiş, POD, dispatch, tekrar) + SHIPMENT-BUNDLE 3.5.1

Tarih: 2026-11-18 · Veren: CT (MVP-6 CT / merkez, Beste adına) · Durum: **VERİLDİ — §2 kararları kapandı (Beste, 2026-11-18)**

- Modül: **MOD-0183 — Shipment Tracking & POD**, servis `Diten.SupplyChainService` (5066, AGENTS.md §3), kiracı modülü. Gateway 5000; `ocelot.json`
  bu WP'nin kapsamında **değildir** (integration-agent; paket §6, §15).
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md` **r11**, LF sha256
  `6c8c5c9544189b6810457bf180bf1cd8ae6bf0e854ff4da978c717785dd5505c` (CT ölçümü 2026-11-18: `origin/main` @ `bd5a2c851` ve
  `feature/sce/mod-0173-inventory-ledger-valuation` @ `6cdb92e43` aynı bayt; 1605 satır, CR yok). Son paket commit'i `4089f79c5`.
- Onay: `execution/approvals/approvals.tsv` **satır 100** — sha `6c8c5c95…505c`, `approve`, risk **HIGH**, yazan `gmg-10`, onaylayan
  `bestepullukcu-alt`, 2026-11-17, not **"only DL-8, DL-9"**. İnceleme: `execution/approvals/reviews.tsv` satır 391 →
  `docs/records/reviews/MOD-0183/2026-11-16-6c8c5c95.md` (**UYGUN**, açık engel 0). Karar kaydı:
  `docs/records/decisions/2026-11/mod0183-r11-owner-decisions-2026-11-16.md` (R11-C Natig 2026-11-15, R11-8 Natig 2026-11-16).
- Paket künyesi `status_note` r11'i hâlâ "draft, not approved" yazar (satır 11); onaylı sürümü belirleyen approvals.tsv'deki son satırdır
  (satır 100). Künye `branch: feature/mvp6-logistics` yazar; bu WP tek çalışma dalı kuralını uygular (§0 madde 1).
- Dilim: **DL-8** (paket "Teslim dilimleri" satır 1579), kanıt seviyesi **E4**. Kapsar: **CR-1…CR-5, CR-7, CR-8, CR-9** (satır 1602);
  SHIPMENT-BUNDLE 3.5.0 → 3.5.1 aynı değişiklikte (§5 "§26 DL-8 exception", satır 175-181). Aciliyet: §26.6 (satır 1526) — MOD-0177-FU01 dev
  turu bu dilimi bekliyor.
- Yetki: paket > `domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse **paket geçerlidir**; çelişkiyi CT'ye yaz, kendin
  çözme. Bu WP push, PR, `.antigravity/**` ya da `ocelot.json` değişikliği izni değildir.

## 0. Başlamadan

1. **Dal:** tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation`, ana checkout `C:\Users\Ali\erp-vnext`; **yeni dal yok**. CT
   etiketi **`<CT belirler>`** (öneri `ct/line-ff-2026-11-18a`; WP yazıldığında yok — son etiket `ct/line-ff-2026-11-14i`): etiket dalın atasıysa
   devam; değilse `git merge --ff-only <etiket>`; olmazsa `git merge --no-ff <etiket>`; çakışmada `git merge --abort` ve CT'ye yaz (çözme).
   Etiket yoksa **dur**. Ölçüm notu: `origin/feature/sce/mod-0173-inventory-ledger-valuation` (`902383037`) r11'i **içermiyor** (yerel dal
   `6cdb92e43` içeriyor) — uzak dala bakarak karar verme.
2. **Paket sha'sı (fail-closed):** `git show HEAD:execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md | tr -d '\r' | sha256sum`
   → `6c8c5c95…505c` olmalı; raporun ilk satırına yaz. Farklıysa **dur**, kod yok.
3. **Onay satırı:** approvals.tsv'de bu paket yolu için yazılmış **son** satırın 2. sütunu `6c8c5c95…505c`, 3. sütunu `approve`, son sütunu
   `only DL-8, DL-9` olmalı (bugün satır 100). Sonradan aynı yol için başka satır yazılmışsa **dur**.
4. **Sözleşme ön kontrolü:** `docs/analysis/contracts/shipment-bundle.openapi.yaml` LF sha256 = `42ccf361a08c13f8c080a0c95dfcd98a91bc9f5d7da4295041b9e9c17d25f8b7`
   (`info.version: 3.5.0`, 4433 satır) ve `git apply --check docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.5.1.patch` geçmeli
   (CT 2026-11-18'de ölçtü: geçiyor). Değilse **dur** — sözleşme başka bir dilimle değişmiş demektir (ör. DL-5'in 3.6.0'ı DL-8'den önce
   girmemeli, §25.7 / satır 1328).
5. **Ortam:**
   - Ana checkout'ta derleme / test **yok** (Inventory 5067 oradan `dotnet watch` ile koşabilir); derleme, test ve sabotaj **ayrık (detached)
     worktree**'de.
   - `INVENTORY_TEST_MONGO` **boş**, 27017 **kullanılmaz**; `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`.
   - **SupplyChain takımı kendi mongod'unu açmaz (ölçüm):** sekiz değişken fail-closed okunur — `MOD0183_TEST_MONGO`, `MOD0184_TEST_MONGO`,
     `MOD0185_TEST_MONGO`, `MVP6_MOD0190_MONGO_URI`, `MVP6_MOD0192_MONGO_URI`, `RETURNS_MONGO_URI`, `CLAIMS_TEST_MONGO`,
     `SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI` (`scripts/test-env/supplychain-ci-mongo.sh:30-39`; ör. `ShipmentTests.cs:32-33`). İstenen biçim
     (aynı betik `:15-19`): tek üyeli **replica set**, `enableTestCommands=1`, yalnız loopback, **27017-27021 bandı dışında** bir port. Betik
     Linux/CI içindir (`/proc`, `RUNNER_TEMP`); Windows'ta `DITEN_TEST_MONGOD` ikilisiyle aynı biçimi elle aç (tarif CT'ye soru, §2 madde 4).
   - `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşulmaz**.
6. **Dilim başı taban koşusu** (§4) koşulmadan kod yazılmaz; taban kırmızıları adıyla rapora, aynı gün `main`'de ölçülerek ayrılır.
7. **Paketi oku (r11 satır numaralarıyla):** §5 Repo Scope (123-181; özellikle "§26 DL-8 exception" 175-181), §6 (183-191), §8 (217-221),
   §12 `X-Correlation-Id` satırı (407), §13 (420-430), §13.1 `CONFLICTING_PERSISTED_ROOT` satırı (453) ve "Two keys" (460-470), §16 korelasyon
   satırı (500), §26 tamamı (1431-1566: Cause tablosu 1442-1447, R11-1…R11-8 1459-1468, 26.2 1470-1477, 26.4 1504-1507, 26.5 1509-1522, 26.6
   1524-1527, 26.7 1529-1546), dilim tablosu 1579-1580, dilim notu 1602-1605. Sözleşme önerisi:
   `docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.5.1-PROPOSAL.md`, yama: aynı klasörde `SHIPMENT-BUNDLE-3.5.1.patch`.

## 1. Kapsam

### 1.1 Dilim satırı (paket "Teslim dilimleri", satır 1579, 1602-1605)

"korelasyon kökü sunucuda (Gateway arkasında geçiş, POD, tekrar)" — kapsadığı bölümler: Repo Scope, Runtime Constraints, Correlation root
behind the Gateway (r11), Rules, Contract: SHIPMENT-BUNDLE 3.5.1, Acceptance and sabotage. CR-1…CR-5, CR-7, CR-8, CR-9; R11-8 mevcut
koordinatörde (DL-5 devralır). **CR-6 DL-9'dadır** (§6).

Kurallar (paket §26.1; bağlayıcı metin pakettedir, burada yalnız uygulama yeri):

| Kural | Uygulama yeri (CT ölçümü, `origin/main` @ `bd5a2c851`; servis dosyaları MVP-1 dalıyla bayt-eşit) |
|---|---|
| R11-1 kök = `createShipment`'ın aldığı `X-Correlation-Id` | **zaten böyle**: `CreateShipmentHandler.cs:43-44` (`CorrelationId` ve `LifecycleCorrelationId` = `context.CorrelationId`). Değişmez; CR-1 ölçer |
| R11-2 karşılaştırmalar kalkar | `Application/Features/Shipments/Handlers/CommandHandlers/TransitionShipmentHandler.cs:31`, `.../CapturePodHandler.cs:17`, `Application/Features/Shipments/Dispatch/ShipmentDispatchCoordinator.cs:33` — üç satır silinir; **başka kontrol yer değiştirmez** |
| R11-3 olay / lifecycle kökü taşır | **zaten böyle**: `Persistence/Features/Shipments/ShipmentRepository.cs:221` (history), `:235` (event zarfı), `:244` (outbox); `causationId` = komut kimliği (`:236`). Değişmez; CR-3 ölçer |
| R11-4 isteğin korelasyonu = komut izi | **zaten böyle**: yanıt başlığı `Api/Middleware/ShipmentContextMiddleware.cs:18`, makbuz `ShipmentRepository.cs:249`, dispatch makbuzu `ShipmentDispatchCoordinator.cs:182`, INVENTORY'ye iletim `:310`. Değişmez |
| R11-5 tekrar yalnız parmak izine bakar | `ShipmentRepository.cs:76` (makbuz korelasyonu ≠ istek → 400) silinir; `:75` (parmak izi → 409 `IDEMPOTENCY_KEY_REUSED`) kalır. Dispatch tekrarı da bu yoldan geçer (`ShipmentDispatchCoordinator.cs:29` → `FindReceiptAsync` → `ReplayAsync`) |
| R11-6 `X-Client-Correlation-Id` okunmaz; 400 kuralı aynı | servis bugün okumuyor (`ShipmentContextMiddleware.cs:10-16` yalnız `X-Correlation-Id`); değişmez |
| R11-7 Web: details kök göndermez | `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js:22-23` (`authoritativeRoot`), `:206-210` (niyet kökle eşlenir), `:213` (`'X-Correlation-Id': intent.correlationId`) — niyet başına bir UUID üretilir ve tekrarlarında korunur, `create.js:11-12` gibi |
| R11-8 intake: çağıranın korelasyonu karşılaştırılmaz | `Application/Features/SourceIntake/WarehouseIntakeCoordinator.cs:55-56` — karşılaştırılan kümeden **yalnız** `context.CorrelationId` çıkar; upstream ve envelope kalır. `:26-28` (`CONFLICTING_CORRELATION`) ve `:43` (kök = önceki niyetin kökü) değişmez |

(Yollar `services/Diten.SupplyChainService/src/Diten.SupplyChainService.<Katman>/` altındadır.)

### 1.2 İzinli dosyalar

| # | Dosya | DL-8 değişikliği | Dayanak |
|---|---|---|---|
| A1 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Shipments/Handlers/CommandHandlers/TransitionShipmentHandler.cs` | `:31` silinir | R11-2; §5 madde 1-2 |
| A2 | `.../Handlers/CommandHandlers/CapturePodHandler.cs` | `:17` silinir | R11-2 |
| A3 | `.../Features/Shipments/Dispatch/ShipmentDispatchCoordinator.cs` | `:33` silinir | R11-2 |
| A4 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Shipments/ShipmentRepository.cs` | `:76` silinir (`ReplayAsync` / `FindReceiptAsync` imzası korunabilir; değiştirilirse `Domain/Features/Shipments/IShipmentRepository.cs` de MOD-0183 kapsamındadır) | R11-5 |
| A5 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SourceIntake/WarehouseIntakeCoordinator.cs` | `:55-56` karşılaştırma kümesi: yalnız upstream + envelope | R11-8 |
| C1 | `docs/analysis/contracts/shipment-bundle.openapi.yaml` | **yalnız** `SHIPMENT-BUNDLE-3.5.1.patch`'in `git apply`'ı; başka bayt yok. Beklenen sonuç LF sha256 `8f7564b30af0fdc05bd3b8cae1b66306c6bf1ec682955801c32766a86083e241` (CT 2026-11-18'de yamayı 3.5.0 kopyasına uygulayarak ölçtü; `info.version: 3.5.1`) | §5 "§26 DL-8 exception"; 26.2 |
| C2 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Common/ShipmentBundleContract.cs` | izinli; **değişmesi beklenmez** (çıkarım: yardımcı açıklama alanlarını yok sayar, dosya başı yorumu) — değişirse nedenini rapora yaz | §5 "§26 DL-8 exception" |
| T1 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ShipmentTests.cs` | `:181` "farklı korelasyon kabul edilir"e döner (CR-1); CR-1 / CR-3 / CR-4 vakaları burada ya da `tests/**/Shipments/**` altında yeni dosyada | 26.7; §5 madde 3 |
| T2 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Shipments/Dispatch/ShipmentDispatchTests.cs` (gerekirse `DispatchTestHost.cs`) | CR-2 (dispatch + CompensationFailed çıkışı farklı korelasyonla) | 26.7 |
| T3 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/SourceIntakeTests.cs` | `:114-116` (`wrongRoot` → `CONFLICTING_PERSISTED_ROOT`) CR-9'a göre değişir; CR-9 vakaları | §5 madde 1 (servis ağacı, MOD-0183 özelliği); 26.7 CR-9 — dosya 26.7'de adıyla geçmez, bkz. §2 madde 6 |
| T4 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Shipments/<yeni>.cs` (ad geliştiricinin; ör. `ShipmentCorrelationRootTests.cs`) | CR-1…CR-4, CR-8 için yeni vakalar gerekiyorsa | §5 madde 3 |
| W1 | `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js` | R11-7: `authoritativeRoot` ve kökle eşleme kalkar; niyet = `{ body, key, correlationId: uuid() }`, aynı gövdeyle tekrar aynı niyeti kullanır. **`:184` (`detailRoot`) ve `:182-185` hücreleri DL-9'undur; DL-8'de dokunulmaz** | R11-7; §5 madde 4 |
| W2 | `frontend/Diten.Web.Tests/JavaScript/ShipmentDetailActionTests.cs` | `:15` `MutationsUseOnlyValidatedAuthoritativeRoot` CR-7'ye göre ters çevrilir (kök gönderilmez, niyet başına UUID, tekrarında aynı); `:14` **değişmez** (DL-9 kapsamı) | 26.7; §5 madde 5 |
| W3 | `frontend/Diten.Web.Tests/JavaScript/Node/shipment-*.test.mjs` (yeni, isteğe bağlı) + sarmalayıcı `JavaScript/Shipment*Tests.cs` | CR-7'yi davranışla ölçmek için (dize testine ek) | §5 madde 5 |

**Değişmez (bağlayıcı):** `ContractErrorCodes.cs` ve `ContractErrorCodeParityTests.cs` (3.5.1 kod eklemez / silmez, satır 180); `CreateShipmentHandler.cs`;
`ShipmentContextMiddleware.cs` (400 kuralı, R11-6); olay yükleri ve zarf şeması; §23 dispatch yazımı / telafi; lifecycle, izinler, kapsam,
durum ve hata kodları (26.4); `Details.cshtml`, `ShipmentsIndex.*.resx`, `details.js:182-185` (DL-9); `create.js`;
`frontend/Diten.Web/Controllers/SupplyChainShipmentsController.cs` (korelasyonu zaten iletir, `:422`; değişiklik gerekmez — gerekirse dur, sor);
`docs/reference/architecture/docs-path-authority.json` (**DL-8'in listesinde yok**, §2 madde 1); `gateway/**` (özellikle
`gateway/Diten.ApiGateway/Observability/CorrelationPropagationDelegatingHandler.cs` ve `ocelot.json`); `.antigravity/**`; Building.Blocks.

**XMC-001 (dokunmadan önce sor):** MOD-0185 / MOD-0186 / MOD-0187 dosyaları — `LoadRepository.cs`, `ReturnRepository.cs`, `ClaimRepository.cs` aynı
desende kırık ama **DL-8 yazmaz** (satır 180-181, 26.5); `returns-semantics-v3.0.0.md`, `claims-semantics-v3.0.0.md`,
`loads-semantics-v3.1.0.md`, `shipment-root-semantics-v3.0.0.md` değişmez (öneri §2). MOD-0177 (`ShipmentBundleHttpReader.cs`,
`RecallCrmLoggingTests.cs` — sürüm `>= 3.5.0` kontrolü `:75-76`, 3.5.1 ile yeşil kalması beklenir) ve MOD-0178 dosyaları; Gateway / ocelot.
Bunlardan birinde kırmızı ya da düzeltme gerekirse **dur**, CT'ye kanıtla yaz.

### 1.3 Kapsam dışı

DL-9 (CR-6, Details ekranında GUID yok — §6); §26.5 Returns / Claims / Loads revizyonları; SHP kayıt kodu; DL-5'in 3.6.0'ı ve
`warehouse-intake` HTTP makbuzu (R11-4'ün intake yarısı DL-5'te); Gateway route'ları; docs-path repin (§2 madde 1); dev'e dağıtım.

## 2. Kararlar (kapandı — sahip Beste 2026-11-18, kalanlar CT)

1. **DocsPathGuard — sahip kararı: ayrı CT commit'i.** Geliştirici `docs/reference/architecture/docs-path-authority.json`'a **dokunmaz**.
   DL-8 commit'inden sonra `DocsPathGuardTests` 1 ek kırmızı verir (beklenen; rapora adıyla yaz). CT, DL-8'in hemen ardından aynı hatta
   ayrı commit'le sahip karar kaydı (`mod0183-docs-path-shipment-bundle-351-repin-owner-decision-01.json`) + authority satırını yazar
   (yeni hash `8f7564b3…e241`); kırmızı pencere `main`'e çıkmaz.
2. **CR-5 (E4, gerçek Gateway) — sahip kararı: ortak canlı tura.** Geliştirici CR-5'i koşmaz, filo başlatmaz; raporda "CR-5: canlı turda
   (sahip kararı 2026-11-18)" yazar. DL-8 kabulü o zamana dek KOŞULLU kalır.
3. **CR-8'in ölçümü (CT):** sözleşmenin LF sha256'sı `8f7564b3…e241` + `git apply -R --check` çıktısı rapora; test olarak CT repin'i sonrası
   DocsPathGuard. Ayrı hash testi yazılmaz.
4. **Windows'ta SupplyChain test mongod'u (CT):** geliştirici `C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe` ile scratch dizininde
   tek üyeli replica set açar (`--replSet`, `--setParameter enableTestCommands=1`, port **27022 ve üstü**; 27017–27021 yasak); sekiz
   değişkeni CI betiğinin (`supplychain-ci-mongo.sh`) eşlediği biçimde bu örneğe bağlar; tarifi rapora yazar. Kurulamazsa dur ve CT'ye yaz.
5. **POD `CorrelationId` (CT):** değişmez (R11-2 "no other check moves"; 26.4). Teyit edildi.
6. **`SourceIntakeTests.cs` yolu (CT):** mevcut dosya kabul (§5 `services/Diten.SupplyChainService/**` kapsar). Yeni test dosyası gerekirse
   `tests/**/Shipments/**` altına.
7. **Commit biçimi (CT):** sözleşme yaması + servis değişikliği + servis testleri **tek commit** ("one change", §5); intake (R11-8, CR-9)
   ve Web (CR-7) aynı dilimde ayrı commit'ler olabilir.
8. **Dispatch devam korelasyonu (CT):** deneme alanı değişmez; CR-2'de kira sonrası devam vakası **ölçülür** (INVENTORY'ye giden korelasyonun
   güncel istekten geldiği, tekrarın parmak iziyle tanındığı) — ek test yazılır, davranış değişmez.
9. **WP yeri (CT):** bu dosya `execution/domains/supply-chain-execution/work-packs/mvp6/WP-0183-DL8-correlation-root.md`; CT-QUEUE satırı yok
   (tek çalışma dalı, CT doğrulaması kabul kaydına yazılır).
10. **DL-9 — sahip kararı: DL-8'ten sonra**, aynı oturumda, DL-8 CT doğrulamasından sonra ayrı talimatla (§6 bu WP'de uygulanmaz).

## 3. Kurallar

- **Kiracı:** değişiklik kapsam süzgeçlerine dokunmaz (`ShipmentRepository.cs:13-17`, `:70-71`); başka kiracı / şirket 404 aynen.
  `CrossScopeMutationsAre404AndWriteNothingAnywhere` ve `Scope_Rbac_Schema_AndCorrelationFailClosed` (§16) değişmeden yeşil — ikincisinde
  yalnız `:181` satırı CR-1'e göre değişir; `:179-180` (eksik / UUID olmayan korelasyon → 400) **kalır**.
- **Yetki:** `[HasPermission]` ve `TransitionShipmentHandler.cs:14-15` izin ayrımı değişmez.
- **Dil:** DL-8 kullanıcıya görünen metin eklemez / değiştirmez (resx yok). Gerekirse **dur**.
- **Sözleşme:** tek yazıcı MVP-6 CT; geliştirici yalnız yamayı `git apply` eder, elle düzenlemez. Yama uygulanmazsa **dur**.
- **Korunan yollar ve XMC-001:** §1.2 tablosu dışında dosya yok; `ocelot.json` ve Gateway yalnız integration-agent; MOD-0177 / 0178 /
  0185 / 0186 / 0187 dosyasına dokunmadan önce sor.
- **Git:** `main`'e commit / push yok; **push ve PR yok** (modül sonunda ayrı onayla); stage yalnız **açık dosya yoluyla** (`git add -A` /
  `git add .` yok); `--no-verify` yok; yıkıcı git komutu yok. Yerel commit serbest; iletiler `feat(sce): MOD-0183 DL-8 — …`.
- **Commit sırası (her commit derlenir ve SupplyChain takımı yeşil; kırmızı pencere yok):**
  (1) R11-2 + R11-5 + 3.5.1 yaması + T1 / T2 / T4 vakaları (CR-1…CR-4, CR-8) — **tek commit** (sözleşme ve davranış birlikte, satır 175-176);
  (2) R11-8 + T3 (CR-9); (3) R11-7 + W1 / W2 / W3 (CR-7). §2 madde 7 kararına göre (1) ve (2) birleşebilir.

## 4. Test, sabotaj ve ölçüm

### 4.1 AC → test eşlemesi (paket 26.7, satır 1534-1544)

| AC | Ölçülecek | Test yeri (öneri) |
|---|---|---|
| CR-1 | c1 ile create; Planned, Dispatched, InTransit ve POD her biri **yeni** korelasyonla → hepsi başarılı; `ShipmentTests.cs:181` "farklı korelasyon kabul edilir" olur | T1 / T4 (Dispatched adımı INVENTORY sahtesi ister: `Shipments/Dispatch/FakeInventory.cs`) |
| CR-2 | CR-1'in dispatch'i koordinatör üzerinden ve CompensationFailed çıkışı (Cancelled) farklı korelasyonla başarılı | T2 |
| CR-3 | CR-1'in her lifecycle kaydı (`sce_shipment_history.CorrelationId`) ve outbox olayı (`sce_shipment_outbox.CorrelationId` + `PayloadJson.correlationId`) **c1**; geçiş makbuzları (`sce_shipment_receipts.CorrelationId`) kendi istek korelasyonlarını taşır | T1 / T4 |
| CR-4 | create, transition ve POD'un aynı anahtar + aynı gövde + yeni korelasyonla tekrarı: orijinal sonuç, `idempotentReplay: true`, yanıt `X-Correlation-Id` = yeni korelasyon, ikinci yazım / olay yok; gövde değişirse 409 `IDEMPOTENCY_KEY_REUSED` | T1 / T4 (örnek: `SupplierPerformanceReplayTests.cs:296` RP13 deseni) |
| CR-5 | gerçek Gateway üzerinden (lane stack): Gateway ile yaratılmış sevkiyatta geçiş ve POD başarılı; çağıranın gönderdiği `X-Client-Correlation-Id` hiçbir şeyi değiştirmez | E4 canlı koşu — §2 madde 2 |
| CR-7 | `details.js` niyet başına taze UUID gönderir, tekrarlarında aynısını; asla `lifecycleCorrelationId` değil | W2 (+ W3) |
| CR-8 | 3.5.1 birebir: `git apply` sonucu, `info.version: 3.5.1`, sha `8f7564b3…e241`; tüm sözleşme örnek testleri (`ShipmentBundleContractExampleTests`) yeşil; `ContractErrorCodeParityTests` değişmeden yeşil | T-yok / ölçüm — §2 madde 3 |
| CR-9 | koordinatörle intake: c1 ile ilk çağrı kökü c1 olan niyeti hazırlar; aynı anahtarla c2 tekrarı, `COMMIT_FAILED` sonrası c3 devamı ve commit edilmiş outbound'un c4 ile sonraki alımı — hepsi kök c1 ile başarılı, `CONFLICTING_PERSISTED_ROOT` kanıt satırı yok; envelope = kendi çağıran korelasyonu c5 (≠ c1) olan çağrı **hâlâ** `CONFLICTING_PERSISTED_ROOT`; envelope ≠ çağıran ise önce `CONFLICTING_CORRELATION` (`:26-28`) | T3 (`COMMIT_FAILED` için mevcut commit probe deseni, ör. `ShipmentTests.cs` `Probe`) |

**Değişmeden yeşil:** §16'nın MET satırlarının testleri (ör. `GoldenFlow_Replay…`, `SameKeyTransitionReplayIsOneWrite…`,
`EveryForbiddenTransitionLeavesPersistenceUnchanged`, `AllowedBranchesAndPodFailureHaveAtomicHistory`, `CrossScopeMutationsAre404…`,
`Live_responses_match_the_frozen_contract`), `ShipmentTelemetryTests` (çıkarım: yeni korelasyonlu tekrar bugün 400 alıyordu, DL-8'den sonra
gerçek tekrar olur ve `IdempotentReplays` sayacını `ShipmentRepository.cs:92` kuralıyla bir kez artırır — mevcut sayaç testi kırmızıya dönerse
dur ve nedenini yaz), `ShipmentTests.cs:136,152` (`lifecycleCorrelationId` = create korelasyonu), DL-1…DL-4 vakaları.

### 4.2 Sabotaj tablosu (paket 26.7 + CT ekleri)

| S | Değişiklik (geçici) | Kırmızıya dönmesi gereken |
|---|---|---|
| S-CR1 | `TransitionShipmentHandler.cs:31`'i geri koy | CR-1 vakası |
| S-CR1b (CT eki) | `CapturePodHandler.cs:17`'yi geri koy | CR-1'in POD adımı |
| S-CR2 | `ShipmentDispatchCoordinator.cs:33`'ü geri koy | CR-2 vakası (dispatch ve çıkış) |
| S-CR3 | `ShipmentRepository.cs:235` ve `:244`'te `s.CorrelationId` yerine istek `correlationId`'si | CR-3 vakası |
| S-CR4 | `ShipmentRepository.cs:76`'yı geri koy | CR-4 vakası (create, transition, POD tekrarı) |
| S-CR7 | `details.js` niyetine `lifecycleCorrelationId`'yi geri koy | W2 / W3 |
| S-CR8 | sözleşmede yama dışı tek bayt (ör. bir açıklamaya boşluk) | §2 madde 3'ün kararına göre (DocsPathGuard repin sonrası ya da hash ölçümü) |
| S-CR9 | `WarehouseIntakeCoordinator.cs:55-56`'da çağıran korelasyonunu karşılaştırmaya geri koy | CR-9 vakası |

Her biri: değişiklik + kırmızı test adı + çıktı özeti → **bayt-eşit geri al** (sha256) → yeşil; `git status` temiz. Hiçbiri commit'e girmez.
Sabotajlar ayrık worktree'de.

### 4.3 Ölçüm komutları (filtresiz; her proje için **en fazla 2 tam koşu**: dilim başı + dilim sonu)

- **SupplyChain:** `dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests` (`~filter` yok; §0 madde 5 değişkenleri
  set). Geçti / kırmızı / atlandı; taban kırmızıları adıyla. Commit başına derleme + değişen test sınıfları hedefli koşulabilir (sayım
  rapora "hedefli" diye), tam koşu sayısı 2'yi geçmez.
- **Web.Tests:** `dotnet test frontend/Diten.Web.Tests` — dilim başı ve (3) commit'inden sonra.
- **Mimari:** `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` — dilim başı ve sonu; `DocsPathGuardTests` sonucu adıyla
  (§2 madde 1 kararına göre beklenen: repin öncesi kırmızı, CT repin'i sonrası yeşil).
- **Sözleşme:** `tr -d '\r' < docs/analysis/contracts/shipment-bundle.openapi.yaml | sha256sum` önce `42ccf361…` / sonra `8f7564b3…`;
  `git diff --stat <başı>..<sonu> -- docs/analysis/contracts/` yalnız bu dosya.
- **Kapsam kanıtı:** `git diff --name-only <başı>..<sonu>` — §1.2 tablosu dışında dosya yok.
- **InventoryService takımı** koşulmaz (MOD-0177 dosyası değişmez; `RecallCrmLoggingTests.cs:75-76` `>= 3.5.0` kontrolü). CT isterse:
  `INVENTORY_TEST_MONGO` boş, 27017 yok, `DITEN_TEST_MONGOD` yukarıdaki gibi.

## 5. Rapor

1. Paket sha256 ölçümü, onay satırı (satır no + son sütun), dal adımı (ata / ff / no-ff), dilim başı HEAD, sözleşme önce sha'sı.
2. Ortam: mongod nasıl açıldı (port, replica set, değişkenler — değer değil, ad), ayrık worktree yolu.
3. Commit listesi (SHA + ileti); sözleşme ile servis kodunun aynı commit'te olduğu açıkça.
4. Takım sayıları: SupplyChain (önce / sonra, kırmızılar adıyla, taban kırmızıları `main` ölçümüyle), Web.Tests, mimari (`DocsPathGuardTests` adıyla).
5. AC tablosu CR-1…CR-5, CR-7, CR-8, CR-9 — test adı + sonuç; CR-5 ve §2'de "bekliyor" olanlar ayrı satırda.
6. Sabotaj tablosu (her kalem: değişiklik, kırmızı test + çıktı, geri alma sha256, yeşil).
7. `git diff --stat` / `--name-only` kanıtları; sözleşme sonra sha'sı; `ContractErrorCodes.cs` / parity testi değişmedi kanıtı.
8. Açık noktalar ve CT'ye sorular (XMC-001 bulguları dahil; MOD-0185/0186/0187 aynı desen kırıkları yalnız gözlem olarak).

## 6. DL-9 — "Details ekranında GUID yok" (DL-8 ile birlikte ya da sonra — **CT kararı**)

Paket: DL-9 CR-6'yı kapsar, "DL-8 ile ya da sonra" inebilir (satır 1603), kanıt E3 (satır 1580), §26.3 (1479-1502). Kısa kapsam (ölçüm):
`Details.cshtml:26` `LifecycleRoot` satırı ve 7 `ShipmentsIndex.*.resx`'teki `LifecycleRoot` anahtarı kalkar; `details.js:184` (`detailRoot`),
`:185` (`carrierId`, `loadId`) satırları kalkar (Details.cshtml:27 `CarrierId` / `LoadId` hücreleri dahil); çözülemeyen SKU / konum / lot /
depo hücresi yalnız yerelleştirilmiş "ad alınamadı" metnini gösterir (SB-10 → CR-6); hata iletisindeki "Support reference" kalır
(`details.js:80-81`, `:196-197`); intake hücreleri (`:182-183`) DL-5'ten sonra ölçülür. `ShipmentDetailActionTests.cs:14`
(`data.lifecycleCorrelationId` iddiası) DL-9'da değişir.

**CT önerisi: DL-8 tek başına, DL-9 hemen ardından ayrı WP.** Gerekçe:
1. **Aciliyet farkı:** DL-8 dev'de MOD-0177-FU01 turunu bloke ediyor (§26.6); DL-9 "rule breach, the tour is not blocked".
2. **Kanıt seviyesi ve inceleme yüzeyi farklı:** DL-8 E4, arka uç + sözleşme + tek JS niyet değişikliği; DL-9 E3, Razor + 7 dil resx + ekran
   taraması — l10n incelemesi DL-8'in kabulünü geciktirir.
3. **DL-9 kısmen DL-5'e bağlı:** CR-6'nın intake vakaları "from DL-5 on"; DL-9 bugün yalnız manuel sevkiyat parçasını ölçebilir.
4. **Sözleşme / DocsPathGuard penceresi:** DL-8'in kırmızı penceresi (§2 madde 1) kısa tutulmalı; DL-9'u eklemek pencereyi uzatır.
5. **Çakışma riski düşük:** ikisi de `details.js`'e dokunur ama farklı satırlara (DL-8 `:22-23`, `:205-213`; DL-9 `:182-185`, `:184`);
   aynı dalda **sırayla** yapılırsa çakışma yok. Paralel yapılmamalı.

Birlikte alınırsa: DL-9 dosyaları (`Views/SupplyChain/Shipments/Details.cshtml`, `Resources/Views/SupplyChain/Shipments/ShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx`,
`details.js`, `ShipmentDetailActionTests.cs`, `ShipmentDetailsDateNodeTests.cs`, details controller testleri) §1.2'ye eklenir, 7 dil zorunlu,
CR-6 ayrı commit, sabotajı (referansı bir hata hücresinde göstermek) ayrı satır.
