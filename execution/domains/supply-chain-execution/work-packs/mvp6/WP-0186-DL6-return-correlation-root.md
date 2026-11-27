# WP-0186-DL6 — MOD-0186 Reverse Logistics DL-6: iade korelasyon kökü servisin (C yolu) + SHIPMENT-BUNDLE 3.6.0 / returns-semantics v3.1.0

Tarih: 2026-11-27 · Veren: CT (MVP-6 CT / merkez, Beste adına) · Durum: **TASLAK — §0 başlama kapıları açık; §2'deki iki TBD Beste'nin**

- Modül: **MOD-0186 — Reverse Logistics**, servis `Diten.SupplyChainService` (5066, AGENTS.md §3), kiracı modülü. Gateway 5000;
  `ocelot.json` bu WP'nin kapsamında **değildir** (iade rotaları yalnız geçirir; paket 36.2 son cümle).
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md` **r12**, LF sha256
  `f533773b7f419128f913feac82f3cbbbf23b01c6f0910e6f8d98347526732229` (CT ölçümü 2026-11-27: `origin/main` @ `85e3b1eec` ve CT çalışma ağacı
  @ `1208410dc` aynı bayt; 1540 satır).
- Onay: `origin/main`'deki `execution/approvals/approvals.tsv` **satır 114** — sha `f533773b…2229`, `approve`, risk **HIGH**, yazan
  `bestepullukcu-alt`, onaylayan `GMG-ERP` (Ali), 2026-11-25 (commit `1bbb10a3a`). Onay satırının notu dilim adlandırmaz; paket metni r12
  onayını "only DL-6" diye bağlar (künye `status_note`, dilim notu satır 1540). İnceleme: `reviews.tsv` satır 418 →
  `docs/records/reviews/MOD-0186/2026-11-24-f533773b.md` (**KOŞULLU**, açık engel 0; tek bulgu O-R11-1 kararıyla kapandı).
- Karar kayıtları: `docs/records/decisions/2026-11/mod-0186-correlation-c-path-owner-decision-01.md` (C yolu 2026-11-20; O-R11-1 = seçenek C
  2026-11-24); sürüm ataması `docs/records/decisions/2026-11/shipment-bundle-version-assignment-ct-decision-01.md` (3.6.0 = MOD-0186,
  3.7.0 = MOD-0183 depo alımı, 3.6.0 üzerine); devam kararı `docs/records/decisions/2026-11/ct-owner-decisions-2026-11-27.md` §3.
- Dilim: **DL-6** (paket "Teslim dilimleri" satır 1528), kanıt seviyesi **E4**. Kapsar: **RC-1…RC-9**, §16 korelasyon maddesi (satır 133) ve
  RU-VS1'in kök cümlesinin yeniden ölçümü (dilim notu 1536-1540).
- Yetki: paket > `domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse **paket geçerlidir**; çelişkiyi CT'ye yaz,
  kendin çözme. Bu WP push, PR, `.antigravity/**` ya da `ocelot.json` değişikliği izni değildir.

## 0. Başlama kapıları (fail-closed — biri tutmazsa kod yok)

1. **K-1 — DL-8 `main`'de (ön adım, CT + Beste).** Yama 3.5.1 tabanlıdır; `origin/main` bugün 3.5.0 taşır (CT ölçümü 2026-11-27:
   `origin/main` @ `85e3b1eec` → `info.version: 3.5.0`). MOD-0183 DL-8 (`20aaffc78`) ve 3.5.1 DocsPathGuard yeniden sabitlemesi (`9b89fd8c3`)
   **`main`'de değil** (yalnız MVP-1 çalışma dalında). Ölçüm:
   `git merge-base --is-ancestor 20aaffc78 origin/main && git merge-base --is-ancestor 9b89fd8c3 origin/main` → ikisi de 0 dönmeli.
   Dönmüyorsa **dur**: DL-8'in `main`'e alınması (MVP-1 hattının PR'ı, Beste onayıyla, merge commit — squash değil) bu WP'nin ön adımıdır.
   Gerekçe: DL-6 `main`'e 3.6.0 ile girer; 3.5.1 `main`'de yokken 3.6.0 yaması `main`'e uygulanamaz (yamanın ilk hunk'ı `version: 3.5.1`
   taşır; öneri §5) ve DL-6 dalı `main`'e alınırken DL-8'i de sürükler (iki dilim tek PR'da, ayrı onaysız).
2. **K-2 — CT kayıtları çalışma hattında.** Tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` (ana checkout
   `C:\Users\Ali\erp-vnext`; **yeni dal yok**). Dal şunları içermeli (ölçüm 2026-11-27: hiçbiri dalda değil): r12 onay satırı
   (`1bbb10a3a`, `origin/main`), 3.6.0 öneri + yama (`090b78f47`, CT kayıt dalı), sürüm kararı ve bu WP. CT etiketi **`<CT belirler>`**
   (öneri `ct/line-ff-2026-11-27a`; son etiket `ct/line-ff-2026-11-14i`): etiket dalın atasıysa devam; değilse `git merge --ff-only <etiket>`;
   olmazsa `git merge --no-ff <etiket>`; çakışmada `git merge --abort` ve CT'ye yaz. Etiket yoksa **dur**.
3. **K-3 — Paket sha'sı:** `git show HEAD:execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md | tr -d '\r' | sha256sum`
   → `f533773b…2229`; raporun ilk satırına yaz. Farklıysa **dur**.
4. **K-4 — Onay satırı:** `approvals.tsv`'de bu paket yolu için **son** satır: 2. sütun `f533773b…2229`, 3. sütun `approve` (bugün `main`
   satır 114). Sonradan aynı yol için başka satır yazılmışsa **dur**.
5. **K-5 — Sözleşme ön kontrolü:** `docs/analysis/contracts/shipment-bundle.openapi.yaml` LF sha256 =
   `8f7564b30af0fdc05bd3b8cae1b66306c6bf1ec682955801c32766a86083e241` (`info.version: 3.5.1`); `returns-semantics-v3.1.0.md` **yok**;
   `git apply --check docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.6.0.patch` geçmeli. Yama dosyası LF sha256
   `a47973cf399b532edb5f32f3adf451924056f7f3ff401e0a37238f882c03e794` (CT ölçümü). CT 2026-11-27'de 3.5.1 kopyasına uyguladı: geçti, sonuç
   YAML `6024d3f0738a4d55830ced9e97e7b4db41e4c6576ece3da6163e38ade615d7bb`, ek belge `60ded8ddc5bb8c496086601a4edccdc1a9980c8c53b04e69aef08c0fc2df34bb`
   (öneri §5 ile aynı). Sözleşme 3.7.0 ise (MOD-0183 DL-5 önce girdiyse) **dur** — sıra bozulmuştur (sürüm kararı "Sıra").
6. **K-6 — §2 madde 1 ve 2 kapandı** (Beste). Açıksa **dur**.
7. **Ortam:** ana checkout'ta derleme / test **yok** (Inventory 5067 oradan `dotnet watch` ile koşar); derleme, test ve sabotaj **ayrık
   (detached) worktree**'de, **LF** checkout ile (`git -c core.autocrlf=false worktree add --detach …`; CRLF kopyada DocsPathGuard ve
   sözleşme özetleri yanlış kırmızı verir). `INVENTORY_TEST_MONGO` boş, 27017 kullanılmaz. SupplyChain takımı kendi mongod'unu açmaz: sekiz
   değişken (`MOD0183_TEST_MONGO`, `MOD0184_TEST_MONGO`, `MOD0185_TEST_MONGO`, `MVP6_MOD0190_MONGO_URI`, `MVP6_MOD0192_MONGO_URI`,
   `RETURNS_MONGO_URI`, `CLAIMS_TEST_MONGO`, `SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI`; `scripts/test-env/supplychain-ci-mongo.sh:30-39`)
   tek üyeli replica set'e (`C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe`, `--replSet`, `--setParameter enableTestCommands=1`, port
   **27022+**, yalnız loopback) bağlanır — WP-0183-DL8 §2 madde 4 tarifi. `Diten.AuthService.AccountKindAcceptanceHost.Tests` koşulmaz.
8. **Dilim başı taban koşusu** (§4.3) koşulmadan kod yazılmaz; taban kırmızıları adıyla rapora, aynı gün `main`'de ölçülerek ayrılır.
9. **Paketi oku (r12 satır numaralarıyla):** §7 satır 80, §8 satır 87, §16 satır 133, §32.8 `CORRELATION_ROOT_MISMATCH` satırı 766, §36 tamamı
   (1347-1474: neden tablosu 1362-1367, kurallar RR-1…RR-9 1379-1391, 36.2 1393-1410, 36.3 1412-1416, 36.4 1418-1423, 36.5 1425-1444,
   36.6 1446-1460, 36.7 1462-1474), dilim tablosu 1528, dilim notu 1536-1540. Öneri: `docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.6.0-PROPOSAL.md`
   (§1 değişiklik tablosu, §3 semver istisnası, §4 tüketiciler).

## 1. Kapsam

### 1.1 Kurallar → uygulama yeri (CT ölçümü 2026-11-27; servis dosyaları `origin/main` ile çalışma ağacında bayt-eşit)

Önek `S = services/Diten.SupplyChainService/src/Diten.SupplyChainService.`

| Kural | Uygulama yeri |
|---|---|
| RR-1 kök = `createReturn`'ün aldığı `X-Correlation-Id` | `S.Persistence/Features/Returns/ReturnRepository.cs:100` — `order.CorrelationRoot = observation!.Root` → istek korelasyonu (`root`) |
| RR-2 karşılaştırmalar kalkar | `ReturnRepository.cs:71` (create: sevkiyat kökü ≠ istek), `:109` (transition), `S.Application/Features/Returns/Receipt/ReturnReceiptCoordinator.cs:50` (§34 Received) — üç satır silinir; **başka kontrol yer değiştirmez** (kapsam, izin, lifecycle, §34 kira ve anahtar kuralları, `ReturnReferenceReader.cs:78-79` 503 / 502) |
| RR-3 olay / audit saklı kökü taşır | olay zarfı `ReturnRepository.cs:154` zaten `order.CorrelationRoot`; audit `:135` istek değeri → `order.CorrelationRoot`. `causationId` değişmez |
| RR-4 istek korelasyonu = komut izi | makbuz `:128` (alan adı korunur, göç yok), yanıt başlığı / `error.correlationId` `S.Api/Features/Returns/ReturnContextMiddleware.cs:54`, `:60`, sevkiyat okuması `ReturnReferenceReader.cs:89`, §34.1 INVENTORY iletimi — değişmez |
| RR-5 tekrar yalnız parmak izine bakar | `ReturnRepository.cs:31` silinir; `FindTransitionReplayAsync` (`:226-230`) aynı `Replay` yolundan geçer; farklı parmak izi → 409 `IDEMPOTENCY_KEY_REUSED` kalır |
| RR-6 `X-Client-Correlation-Id` okunmaz; 400 aynı | `ReturnContextMiddleware.cs:66` (400), `:53` (üretilen iz kök olmaz) — değişmez |
| RR-7 iade yollarında `CORRELATION_ROOT_MISMATCH` yok | RR-2 + RR-5'in sonucu; `ContractErrorCodes.CorrelationRootMismatch` sabiti **kalır** (Loads kullanır, `LoadRepository.cs:17`, `:45`) |
| RR-8 Web adaptörü kendi izini gönderir | `frontend/Diten.Web/Controllers/SupplyChainReturnsController.cs:560` (`ReturnRootResolution.From`) ve `:573` (`AddReturnsHeaders(request, scope, root.Root)`) → `scope.Trace`; yanıt günlüğü satırı adaptör izini **ve** Gateway yanıtının `X-Correlation-Id`'sini birlikte yazar — başlık `:581` / `:585`'te izle değiştirilmeden önce okunur; dosya başı açıklamaları `:17`, `:26`, `:30`. Sevkiyat ön okuması 404 eşlemesi için kalır (O-R11-3) |
| RR-9 UI kod kümesi | adaptör listesi `:55`, açıklama `:348`; `wwwroot/assets/js/SupplyChain/Returns/index.js:84`, `:876`, `:924`, `:1226`; `index.l10n.js:157`; `Views/SupplyChain/Returns/_IndexL10n.cshtml:130`; `Resources/Views/SupplyChain/Returns/ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx:104` (`ErrCorrelationRootMismatch`); Web testleri `Forms/ReturnFormContractTests.cs:22`, `JavaScript/ReturnIndexBehaviorTests.cs:18`, `:177`. Loads UI'ya dokunulmaz |

### 1.2 İzinli dosyalar (paket 36.6 + sözleşme istisnası)

| # | Dosya | DL-6 değişikliği | Dayanak |
|---|---|---|---|
| A1 | `S.Persistence/Features/Returns/ReturnRepository.cs` | `:31`, `:71`, `:109` silinir; `:100` kök = istek; `:135` audit = `order.CorrelationRoot` | RR-1, RR-2, RR-3, RR-5 |
| A2 | `S.Application/Features/Returns/Receipt/ReturnReceiptCoordinator.cs` | `:50` silinir | RR-2 |
| A3 | `S.Api/Features/Returns/ReturnsController.cs` | yalnız `:28-29` açıklaması ("the Shipment root on mutations") | 36.6 |
| C1 | `docs/analysis/contracts/shipment-bundle.openapi.yaml` | **yalnız** `SHIPMENT-BUNDLE-3.6.0.patch`'in `git apply`'ı; sonuç LF `6024d3f0…d7bb`, `info.version: 3.6.0` | 36.2, RC-9; sürüm kararı madde 1 |
| C2 | `docs/analysis/contracts/returns-semantics-v3.1.0.md` (yeni) | **yalnız** aynı yamanın ürettiği dosya; LF `60ded8dd…34bb`. `returns-semantics-v3.0.0.md` ve `shipment-root-semantics-v3.0.0.md` değişmez | 36.2 C-4 |
| T1 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnReplayTests.cs` | `:13` "farklı korelasyon kabul edilir"e döner | RC-5 |
| T2 | `…/Returns/ReturnEventCorrelationTests.cs` | `Every_return_event_type_carries_the_shipment_root` → iade kökü (RC-4) | RC-4; §16 |
| T3 | `…/Returns/ReturnCorrelationRootTests.cs` (yeni) | RC-1…RC-5 vakaları | 36.6 |
| T4 | `…/Returns/Receipt/*.cs` — yalnız sabit korelasyon gönderen §34 testleri | Received'ı yeni korelasyonla (RC-3) | 36.6 |
| T5 | `…/Common/ReturnsClaimsHttpHost.cs` | **yalnız** Returns sahtesi (`StubReturnReferenceReader`, `:104-108`) host kurulurken sabitlenen kökü döner; açıklama `:41-45`. **Claims sahtesi (`:112-117`) değişmez** (36.4; MOD-0187'nin) | RC-6 |
| T6 | `…/Common/ReturnsClaimsObservabilityTests.cs` | `:70-73` açıklaması ve iade geçişinin korelasyonu | 36.6 |
| W1 | `frontend/Diten.Web/Controllers/SupplyChainReturnsController.cs` | RR-8, RR-9 | 36.6 |
| W2 | `frontend/Diten.Web/Models/SupplyChain/Returns/ReturnViewModels.cs` | `ReturnRootResolution` başka kullanıcı kalmazsa kalkar | 36.6 |
| W3 | RR-9 dosyaları: `index.js`, `index.l10n.js`, `_IndexL10n.cshtml`, 7 `ReturnsIndex.*.resx` | `CORRELATION_ROOT_MISMATCH` / `ErrCorrelationRootMismatch` kalkar; 7 dil eşliği korunur | RR-9, RC-9 |
| W4 | `frontend/Diten.Web.Tests/{Controllers/SupplyChainReturnsControllerTests.cs, Forms/ReturnFormContractTests.cs, JavaScript/ReturnIndexBehaviorTests.cs}` | RC-7 (S-RC-7, S-RC-7b) ve RR-9 listeleri | 36.6 |

**Değişmez (bağlayıcı):** `ContractErrorCodes.cs` ve `ContractErrorCodeParityTests.cs` (kod Loads'ta kalır); `Common/ShipmentBundleContract.cs`
(değişmesi beklenmez; değişirse nedenini rapora yaz); servis `Program.cs`; her Shipment, Claims ve Loads kaynağı ve UI'sı
(`LoadRepository.cs`, `ClaimRepository.cs`, `SupplyChainLoadsController.cs`, `SupplyChainClaimsController.cs`, Loads / Claims `index.js`
ve resx); `ReturnReferenceReader.cs` (O-R11-2: 503 / 502 kalır); `docs/reference/architecture/docs-path-authority.json` (**CT'nin ayrı
commit'i**, §2 madde 2); `gateway/**`, `ocelot.json`; `.antigravity/**`; Building.Blocks.

**XMC-001 (dokunmadan önce sor):** MOD-0187 Claims sahtesi ve testleri (`ReturnsClaimsHttpHost.cs:112-117`, `Claims/**`), MOD-0185 Loads
dosyaları, MOD-0183 dosyaları, MOD-0177 (`ShipmentBundleHttpReader.cs`, `RecallCrmLoggingTests.cs:75-76` — `>= 3.5.0` kontrolü 3.6.0 ile
yeşil kalmalı). Birinde kırmızı ya da düzeltme gerekirse **dur**, CT'ye kanıtla yaz.

### 1.3 Kapsam dışı

MOD-0183 DL-5 (3.7.0 depo alımı) ve DL-9; MOD-0185 / MOD-0187 korelasyon revizyonları (ayrı paket revizyonları, D7); O-R11-2 (503 / 502
kök kodları) ve O-R11-3 (ön okuma) temizliği; DL-5 (K2 onay iş akışı); Gateway rotaları; dev'e dağıtım.

## 2. Kararlar

1. **Yamanın uygulanma zamanı — TBD (Beste).** CT önerisi: 3.6.0 yaması DL-6'nın servis değişikliğiyle **aynı commit'te** uygulanır
   (MOD-0183 DL-8 deseni). Gerekçe: semver istisnasının (a) dayanağı "sunucu kodu artık göndermez"dir; sözleşmenin, sunucunun hâlâ gönderdiği
   bir kodu atladığı bir aralık olmamalı (karar kaydı "Açık kalanlar" 1. madde; öneri §3 son paragraf).
2. **DocsPathGuard yeniden sabitlemesi — TBD (Beste); taslak hazır.** CT önerisi: 3.5.0 / 3.5.1 deseni — geliştirici `docs-path-authority.json`'a
   dokunmaz; DL-6 commit'inden sonra `DocsPathGuardTests` 1 beklenen kırmızı verir (adıyla rapora); CT hemen ardından aynı hatta **ayrı
   commit**'le sahip kaydını (`mod0186-docs-path-shipment-bundle-360-repin-owner-decision-01.json` + `.md`) ve authority satırını yazar
   (`8f7564b3…e241` → `6024d3f0…d7bb`). Kırmızı pencere `main`'e çıkmaz. Taslak:
   `docs/records/decisions/2026-11/mod0186-docs-path-shipment-bundle-360-repin-owner-decision-01-DRAFT.md`.
3. **RC-8 (E4, gerçek Gateway) — CT önerisi:** canlı tura (Beste 2026-11-19: "canlı tur maddeleri için filoyu başlat"); geliştirici filo
   başlatmaz, raporda "RC-8: canlı turda" yazar. DL-6 kabulü o zamana dek KOŞULLU kalır. (DL-8 CR-5 deseni; Beste teyit eder.)
4. **RC-9'un ölçümü (CT):** iki dosyanın LF sha'ları (`6024d3f0…`, `60ded8dd…`) + `git apply -R --check` çıktısı rapora; test olarak CT
   repin'i sonrası DocsPathGuard ve tüm sözleşme örnek testleri. Ayrı hash testi yazılmaz.
5. **Commit sırası (CT):** (1) RR-1…RR-7 + 3.6.0 yaması (C1, C2) + T1…T6 — **tek commit**; (2) RR-8 + W1 / W2 + RC-7 testleri; (3) RR-9 + W3 /
   W4 (7 dil). (2) ve (3) birleşebilir. Her commit derlenir ve SupplyChain takımı yeşil (repin öncesi `DocsPathGuardTests` hariç, §2 madde 2).
6. **WP yeri (CT):** bu dosya; CT-QUEUE satırı yok (tek çalışma dalı; CT doğrulaması kabul kaydına yazılır).

## 3. Kurallar

- **Kiracı:** kapsam süzgeçlerine dokunulmaz; başka kiracı / şirket 404 aynen (`ReturnIsolationTests` değişmeden yeşil).
- **Yetki:** `[HasPermission]` ve iade izin ayrımı değişmez.
- **Dil:** yalnız `ErrCorrelationRootMismatch` anahtarı 7 dilden birlikte kalkar; yeni metin yok. 7 dil eşliği testleri yeşil.
- **Sözleşme:** tek yazıcı MVP-6 CT; geliştirici yamayı yalnız `git apply` eder, elle düzenlemez. Yama uygulanmazsa **dur**.
- **Git:** `main`'e commit / push yok; **push ve PR yok** (modül sonunda ayrı onayla); stage yalnız açık dosya yoluyla; `--no-verify` yok;
  yıkıcı git komutu yok. Yerel commit serbest; iletiler `feat(sce): MOD-0186 DL-6 — …`.
- **AUD-001:** DL-6 yeni yazma komutu eklemez; 35.10 satırları değişmez (36.6 son paragraf).

## 4. Test, sabotaj ve ölçüm

### 4.1 AC → test eşlemesi (paket 36.5, satır 1434-1442)

**Gateway benzeri host:** her RC satırında Returns okuyucu sahtesi host kurulurken sabitlenen kökü döner, her istek yeni korelasyon taşır.

| AC | Ölçülecek | Test yeri |
|---|---|---|
| RC-1 | sevkiyat kökü s0 ≠ c1 iken c1 ile create: 201; saklı `CorrelationRoot` = c1; `ReturnRequested` zarfı = c1 | T3 |
| RC-2 | Authorized, InTransit (Rejected ve Cancelled yolları dahil) her biri yeni korelasyonla: 200 (CT inceleme açığı S3) | T3 |
| RC-3 | §34 Received yeni korelasyonla §34 sonucuna ulaşır (200 ya da 409 `RETURN_RECEIPT_IN_PROGRESS` / 422 / 503), hiç `CORRELATION_ROOT_MISMATCH` değil | T3 / T4 |
| RC-4 | RC-1…RC-3'ün her zarfı ve `returns_audit` satırı c1; her makbuz kendi istek korelasyonunu; yanıt başlığı ve `error.correlationId` = o istek | T2 / T3 |
| RC-5 | create, transition ve Received'ın aynı anahtar + gövde + yeni korelasyonla tekrarı: orijinal sonuç, `idempotentReplay: true`, başlık = yeni korelasyon, ikinci yazım / makbuz / audit / olay yok; gövde değişirse 409 `IDEMPOTENCY_KEY_REUSED` | T1 / T3 |
| RC-6 | host öz-testi: farklı korelasyonlu iki istek aynı sabit kökü alır, ikisinden de farklı | T5 |
| RC-7 | Web adaptörü: create ve transition `scope.Trace` gönderir; fikstür sevkiyatın `lifecycleCorrelationId`'si hiçbir giden başlıkta, günlükte ya da yanıtta yok; adaptör `RETURN_SHIPMENT_ROOT_*` üretmez; yanıt günlüğü iz + Gateway yanıtının `X-Correlation-Id`'si (fikstür: izden farklı); tarayıcı izi alır | W4 |
| RC-8 | gerçek Gateway: create, Authorized, InTransit, Received başarılı; `X-Client-Correlation-Id` hiçbir şeyi değiştirmez; saklı kök = Gateway'in create korelasyonu | canlı tur — §2 madde 3 |
| RC-9 | sözleşme + ek belge yamayla bayt-bayt; sözleşme örnek testleri yeşil; `ContractErrorCodeParityTests` değişmeden yeşil; iade UI / adaptör / testlerde `CORRELATION_ROOT_MISMATCH` yok; 7 `ReturnsIndex` dili eşit | ölçüm — §2 madde 4; W3 / W4 |

**Değişmeden yeşil:** §34 testleri (kira, anahtarlar `a{k}`, geç yazıcı, retler), `ReturnIsolationTests`, `ReturnLifecycleTests`,
`ReturnAtomicityTests`, `ReturnConcurrencyTests`, `ReturnContractTests`, Claims ve Loads takımları (Claims sahtesi değişmediği için),
`ShipmentBundleContractExampleTests`, MOD-0183 DL-8 vakaları (`ShipmentCorrelationRootTests`).

### 4.2 Sabotaj tablosu (paket 36.5)

| S | Geçici değişiklik | Kırmızıya dönmesi gereken |
|---|---|---|
| S-RC-1 | `ReturnRepository.cs:71`'i geri koy | RC-1 |
| S-RC-1b | `:100`'de `observation.Root` sakla | RC-1 |
| S-RC-2 | `:109`'u geri koy | RC-2 |
| S-RC-3 | `ReturnReceiptCoordinator.cs:50`'yi geri koy | RC-3 |
| S-RC-4 | zarfa istek korelasyonunu yaz ya da audit'te `:135` istek değerini tut | RC-4 |
| S-RC-5 | `:31`'i geri koy | RC-5 |
| S-RC-6 | sahteyi yine `context.CorrelationId` yankılatır yap (`:108`) | RC-6 |
| S-RC-7 | kökü `X-Correlation-Id` olarak gönder | RC-7 |
| S-RC-7b | Gateway yanıtının `X-Correlation-Id`'sini yanıt günlüğünden çıkar | RC-7 |
| S-RC-9 (CT eki) | sözleşmede ya da ek belgede yama dışı tek bayt; ya da bir iade UI dosyasında `CORRELATION_ROOT_MISMATCH`'i geri koy | RC-9 (DocsPathGuard repin sonrası / UI listesi testi) |

Her biri: değişiklik + kırmızı test adı + çıktı özeti → **bayt-eşit geri al** (sha256) → yeşil; `git status` temiz. Hiçbiri commit'e girmez;
ayrık worktree'de.

### 4.3 Ölçüm komutları (filtresiz; proje başına en fazla 2 tam koşu: dilim başı + dilim sonu)

- **SupplyChain:** `dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests` (`~filter` yok; §0 madde 7 değişkenleri).
- **Web.Tests:** `dotnet test frontend/Diten.Web.Tests` — dilim başı ve (2)/(3) commit'lerinden sonra.
- **Mimari:** `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` — `DocsPathGuardTests` sonucu adıyla (repin öncesi 1
  beklenen kırmızı, CT repin'i sonrası yeşil).
- **Sözleşme:** `tr -d '\r' < docs/analysis/contracts/shipment-bundle.openapi.yaml | sha256sum` önce `8f7564b3…` / sonra `6024d3f0…`;
  `tr -d '\r' < docs/analysis/contracts/returns-semantics-v3.1.0.md | sha256sum` = `60ded8dd…`; `git diff --stat <başı>..<sonu> -- docs/analysis/contracts/`
  yalnız bu iki dosya.
- **Kapsam kanıtı:** `git diff --name-only <başı>..<sonu>` — §1.2 tablosu dışında dosya yok.
- **Python betik testleri ve faz-1 kapıları:** PR'dan önce (36.5 son cümle) — modül sonu PR'ında, bu dilimde değil.

## 5. Rapor

1. Paket sha'sı, onay satırı (satır no), K-1 ölçümü (iki `merge-base` çıktısı), dal adımı (ata / ff / no-ff), dilim başı HEAD, sözleşme önce sha'sı.
2. Ortam: mongod (port, replica set, değişken adları), ayrık worktree yolu, LF checkout kanıtı.
3. Commit listesi (SHA + ileti); sözleşme ile servis kodunun aynı commit'te olduğu açıkça.
4. Takım sayıları: SupplyChain, Web.Tests, mimari (önce / sonra; kırmızılar adıyla; taban kırmızıları `main` ölçümüyle).
5. AC tablosu RC-1…RC-9 — test adı + sonuç; RC-8 ayrı satırda "canlı turda".
6. Sabotaj tablosu (değişiklik, kırmızı test + çıktı, geri alma sha256, yeşil).
7. `git diff --stat` / `--name-only` kanıtları; sözleşme ve ek belge sonra sha'ları; `ContractErrorCodes.cs` değişmedi kanıtı.
8. Açık noktalar ve CT'ye sorular (XMC-001 bulguları; Claims / Loads aynı desen kırıkları yalnız gözlem).
