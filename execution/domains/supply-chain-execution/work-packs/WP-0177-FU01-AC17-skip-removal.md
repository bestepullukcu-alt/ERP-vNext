# WP-0177-FU01-AC17 — MOD-0177-FU01: SHIPMENT_BUNDLE_350 Skip'inin kaldırılması (AC-17, D-1)

## Künye

| Alan | Değer |
|---|---|
| Paket | `execution/domains/supply-chain-execution/module-packs/MOD-0177-FU01-recall-crm-customer-ref.md` **r9**, `ready-for-dev` |
| Paket LF sha256 | `c12df7b73a26f9bb2fb9a6ff18f40e184a75b4ae6435aa07672b044736896aaf` |
| Onay | `execution/approvals/approvals.tsv`, Ali (GMG-ERP), satırı yazan bestepullukcu-alt, commit `ac493e735` (2026-11-13). Paket künyesindeki `approval_status: pending` onay öncesinden kalmadır; geçerli kayıt approvals.tsv satırıdır |
| İnceleme | `docs/records/reviews/MOD-0177-FU01/2026-11-13-c12df7b7.md`: UYGUN, F-1 ve F-2 düşük (ikisi de paket metniyle ilgili, bu WP'nin kapsamında değil) |
| Sahip kararı | `docs/records/decisions/2026-11/mod0177-fu01-dependency-confirmations-01.md` son ek (2026-11-13, `3d26264f0`) |
| Kapsam | Yalnız AC-17, S-17 (§16, §17, §17.1, §19.13) |
| CT doğrulayıcı | MVP-1 CT (sahip Beste) |
| Durum | **ready**: paket onaylı, sözleşme donmuş |
| Talimatı veren | MVP-1 CT, 2026-11-13 (CT kararları: tek çalışma dalı, cherry-pick, yeniden yazma yok) |

## Bağlam (ölçülen, 2026-11-13)

- Sözleşme `docs/analysis/contracts/shipment-bundle.openapi.yaml`:
  - `:15` `version: 3.5.0`, `:16` `x-status: FROZEN`.
  - `getShipment` 200 şeması `ShipmentDetail` (`:203`).
  - `ShipmentDetail` = `allOf` [`$ref ShipmentSummary`, satır içi nesne] (`:3438-3440`).
  - `customerRef: { $ref: '#/components/schemas/CustomerRef' }` `ShipmentSummary` içinde (`:3427`).
  - `CustomerRef` (`:3434-3437`) = `type: [string, 'null']` + `pattern: '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'`, `format` yok.
- Kod: `ShipmentBundleHttpReader.CustomerRefField = "customerRef"` (`services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Recall/ShipmentBundleHttpReader.cs:25`).
- Atlanmış test: `ShipmentBundle350AlignmentTests.CustomerRef_field_matches`, `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Recall/RecallCrmLoggingTests.cs:57-79`.
  - Skip `:66`.
  - Test r8 ölçütünü uygular: 200 şemasının kendi `properties`'ine bakar (`ShipmentDetail`'de bu yok) ve `("string", "uuid")` bekler (`:77`).
  - Özet yorumu (`:57-61`) "3.4.0 today" der.
  - Bu hâliyle r9'a uymaz.
- Allow-list ve CI:
  - `scripts/ci-skip-allowlist.tsv:15`: `inventory-tests SHIPMENT_BUNDLE_350 1 …`.
  - Python pini `scripts/tests/test_check_trx_no_skipped.py:113-126`, sözlükte `("inventory-tests", "SHIPMENT_BUNDLE_350"): 1` var. Satır kalkınca bu pin de değişmeli.
  - CI çağrısı `.github/workflows/service-tests.yml:275`: `python3 scripts/check_trx_no_skipped.py --suite "$SUITE" …`.
- **WIP commit'i `016c892b7`** (dal `fix/ci/remove-shipment-bundle-350-skip-01`, yalnız yerel; **bu dala dokunulmaz, dal silinmez**):
  - Tek commit, mesajında "NOT READY … waits MOD-0177-FU01 r9" yazıyor. Ebeveyni `ce4358070`, hat dalı bu commit'i içeriyor.
  - Değişen dosyalar (4):
    - `ci-skip-allowlist.tsv` (−1 satır).
    - `test_check_trx_no_skipped.py` (pin + yorum).
    - `RecallCrmLoggingTests.cs`: Skip kaldırıldı, ama özet yorumu hâlâ "(string, uuid)" diyor ve denetim r8'de kaldı, bu yüzden test **kırmızı**.
    - `TestInfrastructure/RepoPaths.cs`: `Contract()`, `docs/records/` altındaki kopyaları aday saymaz.
  - Ölçüm: hat dalında bu dört dosya `ce4358070` ile aynı; cherry-pick'te çakışma beklenmez.
- **RepoPaths değişikliğinin gerekçesi:**
  - Hat dalında `shipment-bundle.openapi.yaml` iki yerde var: `docs/analysis/contracts/` ve
    `docs/records/audits/2026-09/mvp6-combined-final-release-pack-01/publication/docs/analysis/contracts/`.
  - `RepoPaths.Contract` tam bir eşleşme ister; değişmeden test "found 2" ile düşer.
  - `RepoPaths.Contract`'ı çağıran öteki 11 dosya adının (PrismMockFixture, ContractSchemas, Expiry/Quarantine/Recall öneri dosyaları) `origin/main`'de
    tek kopyası var ve hiçbiri `docs/records` altında değil. Filtre bugün yalnız shipment-bundle'ın seçimini değiştirir.
  - Bu çıkarım adım 5'teki filtresiz koşuyla doğrulanır.

## Çalışma yeri ve paylaşılan checkout kuralları

- **Tek çalışma dalı:** `feature/sce/mod-0173-inventory-ledger-valuation` (MVP-1 hattı), ana checkout `C:\Users\Ali\erp-vnext`. Ayrı bir fix/ci dalı açılmaz.
- **Aynı checkout'ta iki oturum daha çalışıyor:** MOD-0180 DL-1 (InventoryService arka uç) ve MOD-0178-FU01 DL-1 (yalnız Web).
  - Stage yalnız açık yolla ve yalnız bu WP'nin dosyalarıyla yapılır (`git add -A` / `git add .` yok). Öteki oturumların değişikliklerine dokunulmaz.
  - **Ana checkout'ta derleme ve test yok:** Inventory 5067 orada `dotnet watch` ile koşuyor.
  - Derleme, test ve sabotaj koşuları ayrık (detached) bir worktree'de, **commit'in kendisi üzerinde** yapılır:

        git -C C:\Users\Ali\erp-vnext worktree add --detach C:\Users\Ali\erp-vnext\.claude\worktrees\verify-0177-skip <commit-SHA>

  - Taban için ikinci bir ayrık worktree açılır: `…\verify-0177-base`, `ct/line-ff-2026-11-13e` üzerinde. İş bitince iki ayrık worktree de kaldırılır.
- Sabotajlar yalnız ayrık worktree'de yapılır, ana checkout'ta asla.

## Yapılacaklar

0. **Hat dalını CT etiketine getir (ana checkout):** `git merge-base --is-ancestor ct/line-ff-2026-11-13e HEAD` doğruysa dal etiketi zaten içeriyor — **devam et**. Değilse `git merge --ff-only ct/line-ff-2026-11-13e`. ff mümkün değilse (aynı dalda MOD-0180 oturumu etiketten sonra commit atmışsa) **CT kararı:** `git merge --no-ff ct/line-ff-2026-11-13e -m "merge: CT chain ct/line-ff-2026-11-13e into MVP-1 line (CT standing approval, docs/records only)"`; etiketin getirdiği yalnız CT kayıtlarıdır (paket, karar, inceleme, onay satırı, WP). Çakışma çıkarsa `git merge --abort` ve **dur**, CT'ye bildir; rebase yok.
   - Şu durumlardan herhangi birinde **dur** ve CT'ye yaz:
     - Birleştirmede çakışma.
     - Bu WP'nin dokunacağı dosyalarda (adım 2 listesi) commit'lenmemiş değişiklik var.
     - Etiket yok.
   - Sonra AC-17'yi hat dalından oku: `git show HEAD:execution/domains/supply-chain-execution/module-packs/MOD-0177-FU01-recall-crm-customer-ref.md | sha256sum`.
     Sonuç `c12df7b7…6aaf` değilse dur. `approvals.tsv`'de `c12df7b7` satırı yoksa da dur.
1. **WIP commit'ini al:** `git cherry-pick -x 016c892b7`.
   - Geçmiş yeniden yazılmaz: reset, amend ve squash yok.
   - Çakışma çıkarsa `git cherry-pick --abort` ile geri çık ve dur.
   - Bu commit'te test r8 denetimiyle kırmızıdır; bu bilinen bir durumdur ve düzeltme commit'i hemen arkasından gelir.
2. **Düzeltme commit'i: testi r9 AC-17'ye göre yeniden yaz.** Yalnız `RecallCrmLoggingTests.cs` içindeki `ShipmentBundle350AlignmentTests` değişir.
   - Test adı değişmez (`ShipmentBundle350AlignmentTests.CustomerRef_field_matches`). Sözleşme yine `RepoPaths.Contract("shipment-bundle.openapi.yaml")`
     ile okunur; kopya ya da sabit yol kullanılmaz.
   - `info.version` ≥ 3.5.0 (bugünkü denetim kalır).
   - `operationId == "getShipment"` olan işlemin `responses.200.content.application/json.schema.$ref`'i çözülür. Bugün bu `ShipmentDetail`'dir.
   - **`allOf` çözülür:**
     - Her `allOf` parçası ya `$ref`'tir (bileşene çözülür) ya da satır içi nesnedir.
     - `properties`'inde `ShipmentBundleHttpReader.CustomerRefField` adlı üye taşıyan parçalar toplanır. Şemanın kendi `properties`'i varsa o da sayılır.
     - Hiçbir parça taşımıyorsa test kırmızıdır ve mesajda alan adı geçer (`NullReferenceException` değil, adlı `Assert`).
     - Birden çok parça taşıyorsa her biri aşağıdaki `$ref` koşulunu sağlamalıdır.
   - Üye tam olarak `$ref: '#/components/schemas/CustomerRef'` olmalı (satır içi `type` kabul edilmez). `$ref` çözülerek `components/schemas/CustomerRef` okunur.
   - `CustomerRef.type` bir **küme** olarak karşılaştırılır ve tam `{string, null}` olmalı. Sıra önemsizdir; eksik ya da fazla tip kırmızıdır.
     Skaler `type: string` tek elemanlı küme sayılır ve kırmızıdır.
   - `CustomerRef.pattern` testteki sabit ile **harfiyen** eşit olmalı: `^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$`.
     Desen yoksa ya da farklıysa kırmızıdır.
   - `format`'a bakılmaz (ne beklenir ne yasaklanır). r8'in `("string", "uuid")` denetimi tamamen kalkar.
   - Özet yorumu (`:57-61`) güncellenir; "3.4.0 today", "(string, uuid)" ve WIP'teki "(string, uuid)" ifadeleri kalkar. Yorum r9 ölçütünü anlatır:
     donmuş 3.5.0, `allOf` → `CustomerRef` `$ref`, `[string, null]`, küçük harf GUID deseni, `format` yok, sahip kararı 2026-11-13.
   - Skip, allow-list satırı ve pin değişikliği cherry-pick ile birlikte gelir: üçü `016c892b7`'de birlikte kalkar (AC-17 "birlikte" koşulu).
     Düzeltme commit'i bunlara dokunmaz.
   - **`RepoPaths.Contract` değişikliği (WIP):** incele. Yukarıdaki gerekçe geçerliyse (iki kopya hat dalında duruyor) **tut**.
     Gerekçeyi, iki yolu ve öteki çağıranların tek kopyalı olduğunu rapora yaz. Kanıt adım 4'te.
   - Commit:
     - Stage yalnız açık yolla: `git add services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Recall/RecallCrmLoggingTests.cs`.
     - Hook atlanmaz (`--no-verify` yok).
     - Mesaj örneği: `test(inventory): MOD-0177-FU01 AC-17 r9 — CustomerRef via allOf/$ref, [string,null] + pattern (fixes 016c892b7 cherry-pick)`.
   - Commit'ten önce `git diff --cached --stat` yalnız bu dosyayı göstermelidir.
3. **Ayrık doğrulama worktree'si:** düzeltme commit'inin SHA'sıyla `…\verify-0177-skip` açılır. Adım 4–6 orada koşulur.
4. **Sabotajlar.** Her koşu tek değişiklik içerir, testi kırmızı yapar, sonra bayt-eşit geri alınır ve test yeniden yeşil olur.
   Kanıt olarak test çıktısındaki hata satırını ve geri almadan sonraki `git diff --stat` (boş) çıktısını yaz.
   - S-17 (1): `docs/analysis/contracts/shipment-bundle.openapi.yaml` `CustomerRef` (`:3436`) `pattern` satırını sil → kırmızı.
   - S-17 (1b): desende `a-f`'yi `A-F` yap → kırmızı.
   - S-17 (2): `CustomerRef` `type: [string, 'null']` → `type: string` → kırmızı.
   - S-17 (3): `ShipmentBundleHttpReader.CustomerRefField` değerini `"customerReference"` yap → kırmızı. Yalnız bu testin kırmızısı kanıttır; öteki
     testlerin durumu yazılır ama ölçüt değildir.
   - RepoPaths: `Contract()`'taki `docs/records` filtresini geçici kaldır → test "Expected exactly one …, found 2" ile kırmızı.
   - Sözleşme ve üretim kodu sabotaj dışında **değişmez** (sözleşme donmuştur, değişiklik MVP-6'nındır; XMC-001).
5. **Filtresiz Inventory takımı** (ayrık worktree'de):
   - Komut: `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests --logger "trx;LogFileName=inventory-tests.trx"`.
     `--filter` kullanılmaz.
   - `INVENTORY_TEST_MONGO` **ayarlanmaz**. Takım kendi replica set'ini açar (mongod `DITEN_TEST_MONGOD` ya da `C:\Program Files\MongoDB\Server\9.0\bin`).
     27017'ye istek gitmez.
   - `Diten.AuthService.AccountKindAcceptanceHost.Tests` koşulmaz.
   - Aynı gün aynı komutu taban worktree'sinde (`ct/line-ff-2026-11-13e`) koş. Karşılaştır:
     - Toplam / geçen / kalan / atlanan; yeni kırmızı yok.
     - Taban kırmızıları adlarıyla yazılır.
     - Dalda `CustomerRef_field_matches` **geçti** olmalı; tabanda atlanmış (SHIPMENT_BUNDLE_350) görünür.
     - Atlananlar dalda yalnız 4 `INVENTORY_PERF` olmalı.
   - Takım MOD-0180 DL-1 oturumunun commit'lediği değişiklikleri de içerebilir. Tabanla aynı noktadan ölçüldüğü için fark bu WP'nindir; tabanda
     olmayan bir kırmızı çıkarsa adını ve dosyasını yaz, kimin olduğu belirsizse CT'ye sor.
6. **CI skip denetimi** (ayrık worktree'de):
   - `python3 scripts/check_trx_no_skipped.py --suite inventory-tests <dal TRX'i>` → çıkış 0, sayaçta `SHIPMENT_BUNDLE_350` yok.
   - Ters kanıt: aynı betiği taban TRX'iyle ve dalın allow-list'iyle koş (`--allowlist <verify-0177-skip>\scripts\ci-skip-allowlist.tsv`).
     `SHIPMENT_BUNDLE_350` atlaması tanınmayan kod olduğu için kırmızı olmalı.
   - `python3 -m unittest discover -s scripts/tests -p "test_*.py"` → yeşil (pin dahil). Tabanla aynı gün karşılaştır.
   - `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` (filtresiz) → tabana göre yeni kırmızı yok. `DocsPathGuardTests` ve
     `CiServiceTestsGuardTests` adıyla yazılır.
7. **Kapanış:**
   - İki ayrık worktree kaldırılır (`git worktree remove`).
   - Ana checkout'ta `git status --short` yalnız öteki oturumların dosyalarını göstermelidir; bu WP'den commit'lenmemiş dosya kalmaz.
   - Bir adım kırmızıysa düzeltme yeni bir commit'tir (geçmiş yeniden yazılmaz), ardından adım 3–6 yeni commit üzerinde tekrarlanır.

## Yapılmayacaklar

- **Push yok, PR yok.** Hat PR'ı geliştirme sonunda Beste'nin onayıyla açılır; PR'ı geliştirici tarafı açar. `main`'e doğrudan commit ya da push yok, force yok.
- Reset, amend, rebase, squash yok. `fix/ci/remove-shipment-bundle-350-skip-01` dalı silinmez, dalda değişiklik yapılmaz.
- Sözleşme, `ShipmentBundleHttpReader.cs`, paket ve `.antigravity/**` değişmez. Başka bir modülde düzeltme gerekirse önce sor (XMC-001).
- Ana checkout'ta derleme, test ve sabotaj yok. 27017'ye istek yok. `INVENTORY_TEST_MONGO` ayarlanmaz.

## Kabul (CT doğrulayıcı: MVP-1 CT)

- Hat dalında iki commit var: `016c892b7`'nin cherry-pick'i (`-x` notlu; Skip, allow-list satırı ve pin birlikte kalkar) ve düzeltme commit'i.
- `CustomerRef_field_matches` Skip'siz koşar ve yeşildir. Denetim r9 AC-17'nin her maddesini yapar (`allOf`, `$ref`, tip kümesi, harfiyen desen, sürüm);
  `format` denetimi yoktur. Özet yorumu günceldir.
- S-17 (1), (1b), (2), (3) her biri kırmızı, geri alınınca yeşil. RepoPaths filtresi kaldırılınca test kırmızı.
- Filtresiz Inventory takımı tabana göre yeni kırmızı içermez. Atlananlar yalnız `INVENTORY_PERF` × 4'tür.
- `check_trx_no_skipped.py` dal TRX'inde yeşil, taban TRX'i + dal allow-list'inde kırmızıdır. `scripts/tests` ve mimari testler tabana göre yeni kırmızı içermez.
- `ci-skip-allowlist.tsv`'de `SHIPMENT_BUNDLE_350` yok (`git grep -n SHIPMENT_BUNDLE_350 -- services scripts`: yalnız yorum).
- DL-1 kabulü bu WP ile verilmez. CT kabul kaydı Skip'in kaldırıldığı commit'i (cherry-pick SHA'sı) ve testin yeşil olduğu düzeltme commit'ini yazar
  (AC-17). D-2…D-4 ve öteki DL-1 kapıları (§22) ayrıdır.

## Teslim raporu (biçim)

1. Adım 0: ff öncesi ve sonrası HEAD, paket sha256 sonucu, onay satırı var/yok.
2. Cherry-pick SHA'sı, düzeltme commit SHA'sı ve mesajları; her commit için `git show --stat`.
3. Adım 2: testin yeni denetim listesi (madde madde AC-17 karşılığı), değişen satır aralıkları; RepoPaths kararı (tutuldu / atıldı), gerekçesi ve iki kopyanın yolu.
4. Adım 4: sabotaj tablosu `| koşu | değişiklik | sonuç (hata satırı) | geri alındı (diff boş) |`.
5. Adım 5: dal ve taban sayıları (toplam / geçen / kalan / atlanan), taban kırmızıları adlarıyla, yeni kırmızı yok beyanı, koşu tarihi ve saati,
   ayrık worktree yolları ve SHA'ları.
6. Adım 6: `check_trx_no_skipped.py` iki koşusunun çıkış kodu ve sayaç satırı; `scripts/tests` ve mimari test sonuçları.
7. Adım 7: ana checkout `git status --short` çıktısı ve worktree'lerin kaldırıldığı.
8. Ölçülmeyenler ve açık noktalar (ör. CI'da koşmadı, push yok). Uydurma yok: ölçülmeyen "ölçülmedi" diye yazılır.
