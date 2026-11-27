# WP-0179-DL2 — MOD-0179 3PL Entegrasyonu DL-2: mesaj defteri + kanonik işlemci, istisna kutusu ve yeniden işleme, bekletilenler, 90 gün kişisel veri kuralı, sağlık / KPI, `TPX` (arka uç)

Tarih: 2026-11-27 · Veren: CT (MVP-5 WMS CT) · Künye: **draft** · Sahip: **Beste** · Yürüten: geliştirici oturumu (MVP-5)
Durum: **taslak — başlama kapıları AÇIK (§0 madde 3: Q-14 CT kararı, XMC-001 onayları; §2 C-1…C-9)**

- Modül: **MOD-0179 — 3PL Integration**, servis `Diten.InventoryService` (5067), kiracı modülü (7 dil).
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md` **r3**, LF sha256
  `1ac6e1a7a22a05c90c56a69ffe65e2a9b7ea62b79e35b7db31eadbddc7d354ab` (CT ölçümü 2026-11-27: hat `feature/sce/mod-0173-inventory-ledger-valuation`
  @ `8c32f4d90` ve `origin/main` @ `85e3b1eec` — ikisinde de aynı), `status: ready-for-dev`. Künyedeki `approval_status: pending` paket metnidir;
  yetki kaynağı onay satırıdır: `execution/approvals/approvals.tsv:94` (onaylayan Ali / `GMG-ERP`, 2026-11-13). İnceleme r3 UYGUN:
  `docs/records/reviews/MOD-0179/2026-11-13-1ac6e1a7.md`.
- Dilim: **DL-2** (paket §23 satırı "(r1 DL-1b)", §24 DL-2 satırı), kanıt seviyesi **E4**.
- DL-1: **VERIFIED 2026-11-17** — `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md` "MOD-0179 DL-1 (3PL)" (commit'ler `37fd11c46` …
  `73ed15e1a`, `04b8fefbc`, `66f7bdd59`; hatta). Paket §24 DL-2 kapısının "DL-1 kabulü" parçası **kapalı**.
- **Sıra notu:** paket §24 "DL-1 → DL-2 → DL-3 ve DL-4 …". **DL-3 (Web ekranları: sağlık, kanallar, eşlemeler, mesajlar, bekletilenler, istisna
  kutusu, ayarlar) bu dilimin kabulünün hemen ardından gelir** — DL-3'ün başlama kapısı DL-2 kabulü + Q-17 (CT); WP'si DL-2 kabulünde yazılır.
  DL-2 bu yüzden DL-3'ün okuyacağı bütün uçları (11) sözleşmeyle birlikte bitirir; Web dosyasına **yazmaz** (§1.4 istisnası C-6 hariç).
- Sahip kararı (devam): `docs/records/decisions/2026-11/ct-owner-decisions-2026-11-27.md` §3 — "MOD-0179 DL-2" Beste'nin "devam edelim" listesinde.
  Bu kayıt kod, commit ya da dağıtım izni değildir; kod izni paket onay satırı + bu WP'nin başlama kapılarının kapanmasıyla doğar.
- Yetki: paket > `domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse **paket geçerlidir**; çelişkiyi CT'ye yaz, kendin çözme.
  Bu WP push, PR ya da `.antigravity/**` değişikliği izni değildir.

`INV` = `services/Diten.InventoryService/src/Diten.InventoryService.`, `INVT` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/` (paket §5).

## 0. Başlamadan

1. **Dal.** Tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` (ana checkout `C:\Users\Ali\erp-vnext`); **yeni dal açma**.
   - `git merge-base --is-ancestor 8c32f4d90 HEAD` → çıkış 0 (CT'nin bu WP'yi yazdığı hat ucu). Değilse `git merge --ff-only 8c32f4d90`; ff olmazsa **dur**,
     CT'ye yaz (merge commit'i ya da rebase yapma).
   - CT başlama mesajında `ct/line-ff-…` etiketi verirse ona ileri sar (aynı kural). Doğrula (çıkış 0): `… 73ed15e1a HEAD`, `… 04b8fefbc HEAD`,
     `… 66f7bdd59 HEAD` (DL-1 hatta).
2. **Paket sha'sı:** `tr -d '\r' < execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md | sha256sum` → `1ac6e1a7…` ile
   başlamalı; raporun ilk satırına yaz. Farklıysa **dur**.
3. **Başlama kapıları (hepsi kapanmadan kod commit'i yok — kendi ağacında okuma / taslak çalışması serbest):**

   | # | Kapı | Durum (CT, 2026-11-27) | Kapanış |
   |---|---|---|---|
   | G-1 | **Q-14** — mesaj komutlarının denetim yolu (paket §8.12, §21, §24 DL-2 "Q-14 (CT)") | **AÇIK** — paket §19.4 `:1032` "Açık — CT: Q-14"; karar kaydı yok (`mod0179-3pl-owner-decisions-01.md`'deki "Q-14" MOD-0012-FU01'in sorusudur, bu değil) | CT kararı (§2 C-1) + karar kaydı commit'i |
   | G-2 | XMC-001 **X-6 (a) +7** (`CentralAuditDeclarationTests` komut sayısı) | **AÇIK** — 2026-11-14 onayı yalnız "DL-1 +12" der (`mod0179-3pl-owner-decisions-01.md` "DL-1 başlangıcı") | Beste onayı, karar kaydı |
   | G-3 | XMC-001 **X-7** (`WarehouseRecordCodeRules.cs` `TPX`; MOD-0178 dosyası) | **AÇIK** — onay kaydı yok; RCS-001 `TPX` satırı ve Platform `RuleAheadOfCatalogue["TPX"]` `08a3a6ac9`'da var | Beste onayı |
   | G-4 | XMC-001 **X-8 +11** (Gateway `Mod0178WarehouseRouteTests` sayı satırı; integration-agent) | **AÇIK** — onay "DL-1 +17" ile sınırlı | Beste onayı |
   | G-5 | XMC-001 **X-5'** (pakette DL-2 için satır **yok** — CT ekledi): `SingleSystemOfRecordArchitectureTests` 0179 vakası dört koleksiyonu **adıyla sabitler** (`Assert.Equal(["wms_tpl_channels", "wms_tpl_command_requests", "wms_tpl_mappings", "wms_tpl_partners"], …)`) — DL-2'nin dört yeni koleksiyonu bu satırı değiştirmeden yeşil kalamaz | **AÇIK** — paket boşluğu (DL-1 WP'sindeki X-W emsali) | Beste onayı |
   | G-6 | XMC-001 **X-15'** (pakette X-15 DL-3 / DL-8'dir — CT öne çekme önerisi): yeni manifest anahtarları (`messages.*`, `raw-messages.read`, `exceptions.*`) Web `role-assignments-label-ledger.test.js`'i `Perm.Entity.*` satır adı eksiğiyle kırmızı yapar (DL-1'de ölçüldü: 27/28, onay `ed61055e0`) | **AÇIK** | Beste onayı (DL-1 "Perm.Entity satırları" eki emsali) |

   Onay commit'leri CT başlama mesajında verilir. **Onay commit'i olmayan seam dosyasına dokunma.** Kapı kapanmadıkça o seam'i bekleyen commit'i
   atma; CT'ye yaz.
4. **Dilim başı commit'i (`<dilim-başı>`)** kaydet; bütün "önce" ölçümleri bu commit'te, aynı gün alınır.
5. **Dilim başında ölçülecek sayaçlar** — aşağıdakiler CT'nin `8c32f4d90` ölçümüdür. **Satırın kendisine atıf yap, satır numarasına değil**:

   | Sayaç | Dosya / satır (`8c32f4d90`) | Bugün | DL-2 değişikliği | Beklenen sonra (çıkarım) |
   |---|---|---|---|---|
   | Gateway uç sayısı (X-8) | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` `Assert.Equal(128, endpoints.Count)` (`:131`) | **128** | **+11** | 139 |
   | Merkezi denetim yazma komutu (X-6 (a)) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` `Assert.Equal(151, WriteCommands.Length)` (`:73`) | **151** | **+7** | 158 |
   | Merkezi denetim `seen` (X-6 (b)) | aynı dosya `Assert.Equal(17, seen)` (`:156`) | **17** | **0** — DL-2 yazımları `ThirdPartyTransactions.InTransactionAsync` ile (yeni `WithTransactionAsync(` çağrı yeri açma) | 17 |
   | Kayıt kodu önekleri (X-7) | `INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs` `TypePrefixes` (`:34`) | **7** — PUT, PCK, OUT, PKG, CNT, WAV, DVC | + `TPX` | 8 (MOD-0182 DL-4 `LBL`'i önce gelirse 9) |
   | 0179 koleksiyon bildirimi (X-5') | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` 0179 vakası | 4 ad | + `wms_tpl_messages`, `wms_tpl_message_payloads`, `wms_tpl_instructions`, `wms_tpl_exceptions` | 8 |
   | Manifest bütünlük dışlaması | `INVT/Api/InventoryLedgerManifestCompletenessTests.cs` `.Controllers.ThirdPartyLogistics` satırı (`:229`) | var | **0** — dokunulmaz | — |
   | Web sayfasız modül | `frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs` `PagelessUntil["THIRD-PARTY-LOGISTICS"]` | var | **0** — sayfalar DL-3'te | — |
   | `Program.cs` | `AddThirdPartyLogistics()` (`:72`) | var | **0** — DL-2 kayıtları 0179'un kendi `ThirdPartyLogisticsServiceCollectionExtensions`'ında | — |

6. **Eşzamanlılık ve ortam:**
   - Aynı sayaç satırlarını **MOD-0180 DL-5** (`WP-0180-DL5-kpis-csv.md`: Gateway +2) ve **MOD-0182 DL-4** (`WP-0182-DL4-label-templates-prints.md`:
     Gateway +11, komut +5, `LBL`) de değiştirir. Her sayaç commit'inden **hemen önce** hattı al, sayıyı o anki değerden yeniden hesapla (yalnız 0179'un
     +11 / +7'si eklenir), yorumda "measured <n> at <commit>" yaz; çakışmada iki tarafın eklemesini de koru, CT'ye yaz.
   - **Ana checkout'ta derleme / test / sabotaj yok** (Inventory 5067 oradan `dotnet watch` ile koşar). Hepsi ayrık (detached) worktree'de
     (`git worktree add --detach ..\erp-vnext-wt-0179-dl2 HEAD` + farkı uygula); commit çalışma dalına.
   - **Makinede aynı anda tek InventoryService takımı** (bütün oturumlar toplamı). Başka oturum Inventory koşuyorsa bekle. Mongo bağlantı zaman aşımıyla
     düşen koşu (yüzlerce kırmızı + `TimeoutException`) sayılmaz, rapora "geçersiz koşu" yazılır. Süre testleri (ör. `BarcodeScanTests.Two_hundred_consecutive_scans_stay_under_the_p95_bound`)
     yük altında kırmızıysa tek başına iki kez koşup yeşilini raporla.
   - `dotnet test` çıktısını boruya verme (`| tail` yok) — dosyaya yönlendir.
7. **Paketi oku (özet yetmez):** §2.3, §3 (DL-2 nesneleri, 7 komut, sorgular, 11 uç, controller tabanı), §4.4, §4.5, §4.6, §4.8 (`DedupKey`), §4.9
   (DL-2 koleksiyonlarının index'leri), §5, §5.1 (X-6, X-7, X-8 ve "bekçi testleri değişmez"), §6, §7, §8.3, §8.6, §8.7, §8.11, §8.12, §8.13, §8.15,
   §10, §12 (gelen mesaj, toplu yeniden işleme, yok sayma, sorumlu ata, bekletilen işleme satırları), §13 (DL-2 vakaları), §14 (DL-2 anahtarları), §16 DL-2
   bloğu, §17, §17.1, §21 (DL-2 komutları), §22, §24 DL-2 satırı. Ayrıca 3PL-BUNDLE taslağı `docs/analysis/contracts/drafts/3pl-bundle-v1-proposal.md`
   §3.5, §4.2 (DL-2 satırları), §5, §9 (madde 7).

## 1. Kapsam

### 1.1 Paket DL-2 satırı (§23, §24) — birebir
"(r1 DL-1b) Mesaj defteri ve kanonik model, istisna kutusu ve yeniden işleme, bekletilenler, 90 gün kişisel veri kuralı, sağlık / KPI, kayıt kodu".
Kapsadığı bölümler: ThirdPartyMessage ve ThirdPartyMessagePayload, ThirdPartyInstruction, ThirdPartyException, Kanonik mesaj modeli ve mesaj listesi,
Askıya alma ve bekletilen mesajlar, İstisna kutusu yeniden işleme ve kişisel veri, Sağlık ve performans eşikleri, Merkezi denetim ve yerel iz, Kayıt kodu
üretimi, Hata kodları, Record Codes. **Yalnız arka uç.** Bu listenin dışına çıkma.

- **İlk adım — 3PL-BUNDLE taslağının DL-2 bölümünü bağlayıcı yap (contract-first; taslak §1 "her dilim kendi satırlarını ilk adımında bağlayıcı yapar"):**
  §3.5 kanonik modeli ve §4.2'deki 11 DL-2 satırını şemalarıyla (`ThirdPartyMessage`, `ThirdPartyMessageRaw`, `ThirdPartyException`, `ThirdPartyDashboard`,
  istek gövdeleri, satır sonuçları) yaz; §5 hata kodlarını DL-2 için tamamla. Sürüm `1.0.0-draft` kalır; **`FROZEN` koyma, `3pl-bundle.openapi.yaml`
  yazma** (dondurma DL-5'ten önce ayrı CT adımı, paket §8.16). Ayrı commit, controller kodundan **önce**. Sözleşme testi (`INVT/ThirdPartyLogistics/`;
  DL-1'in kapalı şema testi emsali) DL-2 cevaplarını da kapalı doğrular.
- **Komutlar (7, §3, §21):** `ReceiveThirdPartyMessageCommand`, `ReprocessThirdPartyExceptionsCommand` (1..200; "eşleme ekle ve yeniden işle" dahil),
  `IgnoreThirdPartyExceptionCommand`, `AssignThirdPartyExceptionCommand`, `ProcessHeldThirdPartyMessagesCommand`, `DiscardHeldThirdPartyMessageCommand`,
  `PurgeThirdPartyMessagePiiCommand` (F-5; zamanlanmış, sistem aktörü).
- **Sorgular (6):** `GetThirdPartyDashboardQuery`, `GetThirdPartyMessageListQuery`, `GetThirdPartyMessageByIdQuery`, `GetThirdPartyMessageRawQuery`,
  `GetThirdPartyExceptionListQuery`, `GetThirdPartyExceptionByIdQuery`. (`GetThirdPartyInstruction*` DL-6, `GetThirdPartyPerformanceQuery` DL-8.)
- **Uçlar (11, taban `/api/warehouse/third-party-logistics`, §3):** `GET dashboard` · `GET messages` · `GET messages/{messageId}` ·
  `GET messages/{messageId}/raw` · `POST messages/held/process` · `POST messages/{messageId}/discard` · `GET exceptions` · `GET exceptions/{exceptionId}` ·
  `POST exceptions/reprocess` · `POST exceptions/{exceptionId}/ignore` · `POST exceptions/{exceptionId}/assign`. Sabit kesimler (`dashboard`, `held`)
  parametreli yoldan önce; `{…Id}` `:guid`; **PUT / DELETE yok**. Controller'lar `ThirdPartyMessagesController`, `ThirdPartyExceptionsController` (+ pano
  ucu §3 tabanına göre — `ThirdPartyPartnersController` ya da ayrı sınıf; geliştirici seçer, raporda yazar), `[Route("api/warehouse")]`, namespace
  `…Controllers.ThirdPartyLogistics`; `[HasPermission]` **yalnız eylem düzeyinde** (AC-7 0179 bekçisi DL-1'den yeşil kalır); `AllowAnonymous` **yok**.
- **İzinler (§14, DL-2 satırları — 5 anahtar, yeni fiil yok):** `warehouse.third-party-logistics.messages.read` / `.update`, `….raw-messages.read`,
  `….exceptions.read` / `.update`; pano `partners.read` ile (§14 "DL-2'de sağlık / KPI"). `ThirdPartyLogisticsPermissions.Implemented` += DL-2 listesi;
  manifest yalnız `warehouse.third-party-logistics.*`. Uç → izin eşlemesi taslak §4.2 tablosu.
- **Varlıklar ve koleksiyonlar (§3, §4.4–§4.6, §4.9):** `ThirdPartyMessage` (`wms_tpl_messages`), `ThirdPartyMessagePayload` (`wms_tpl_message_payloads`,
  ≤ 5 MB, `OriginalSha256` / `CurrentSha256` / `PiiPurgedAt`), `ThirdPartyInstruction` (`wms_tpl_instructions` — **yalnız model + index**; yazan komut
  DL-6 / DL-7), `ThirdPartyException` (`wms_tpl_exceptions`, `Number` `{ŞİRKET}-TPX-{000001}`). Hepsi `ThirdPartyLogisticsCollections.All`'a.
- **Kurallar:** kanonik işlemci (§8.3; iş kuralları yalnız kanonik modeli görür); `DedupKey` tekilliği → `Duplicate` + istisna doğrudan `Ignored`, ikinci
  yan etki yok (AC-12); kapalı kanal → `Rejected` + `ChannelClosed` (AC-10); askıdaki ortak → `Held`, `HeldUntil = ReceivedAt + 30 gün`, giden `NotSent`,
  etkinleştirme kendiliğinden işlemez, geliş sırası + `confirmOutOfOrder`, askıdayken işleme 422 `TPL_PARTNER_SUSPENDED`, süresi dolan 422
  `TPL_HELD_EXPIRED` ve silinmez (§8.6, AC-13); istisna yaşam döngüsü `Open` / `Retrying` / `Resolved` / `Ignored`, yok sayma gerekçesi 10..500 (AC-14);
  toplu yeniden işleme 1..200 aynı ortak + aynı tür, 201 → `TPL_BULK_LIMIT`, karışık → `TPL_BULK_MIXED`, her kayıt kendi sonucu; "eşleme ekle ve yeniden
  işle" aynı ortağın aynı koddaki açık `UnknownProductMapping` istisnaları (≤ 200) (AC-15); lot / SKT farkı `LotExpiryMismatch`, **bizdeki lot ve SKT
  değişmez**, MOD-0174 koleksiyonlarına yazım yok (AC-16, T-3); `UnknownProductMapping`, `UnknownLot`, `QuantityMismatch`, `LateConfirmation`, `Format`,
  `Unsupported` ayrı vakalar, metin 7 dilde koddan (AC-17); sağlık + KPI okuma anında (§8.11, AC-18) — DL-1 B-5'in "Hatalı" rozet parçası burada
  tamamlanır; ham içerik maskesi (alıcı adı, telefon, e-posta, adres satırı — ilk / son iki karakter dışında `•`; imza / sır alanı hiç yok) (AC-24);
  90 gün kuralı — süpürücü günde bir, kiracı başına ≤ 1.000 yük / döngü, kişisel veri kalıcı kaldırılır, zarf / kanonik özet / `OriginalSha256` aynı,
  ikinci koşu yazmaz (AC-60); `TPX` numarası değişmez, eşzamanlı ikisi farklı (AC-21).
- **İşçi (§5 `INV/Api/ThirdPartyLogistics/` "90 gün süpürücüsü", DL-2):** 0179'un kendi hosted service'i; testten çağrılabilen tek tur yöntemi (MOD-0180
  `WaveMaintenanceHostedService` emsali); tur yalnız `PurgeThirdPartyMessagePiiCommand` gönderir (mimari vaka — §4). Gönderici işçi (`SendThirdPartyMessageCommand`)
  **DL-5**'tir.

### 1.2 Dokunulacak dosyalar

**0179'un kendi dosyaları (paket §5 DL-2 satırları):**
- `docs/analysis/contracts/drafts/3pl-bundle-v1-proposal.md` — yalnız DL-2 bölümleri (eklemeli; DL-1 bölümü değişmez)
- `INV/Domain/ThirdPartyLogistics/{Entities,Enums,Rules,Repositories}/` — yeni varlıklar, istisna / mesaj durum makineleri, sağlık kuralı, maskeleme ve
  90 gün temizleme kuralları (saf). **`Product` sözcüğü yalnız `ThirdPartyEnums.cs`'te** kalır (X-P hariç listesi tek dosyadır — `d8b017faa`); yeni dosyada
  gerekirse **dur**, CT'ye yaz (yeni XMC-001).
- `INV/Application/Features/ThirdPartyLogistics/` — `ThirdPartyContract.cs`, `ThirdPartyErrorCodes.cs`, `ThirdPartyCentralAudit.cs` (eklemeli), `Services/`
  (kanonik işlemci), `Ports/` (C-3, C-4), `Resources/ThirdPartyText.{en,tr,fr,es,zh,ar,ru}.resx` (istisna `DetailCode` metinleri — 7 dil)
- `INV/Application/Features/ThirdPartyMessages/`, `ThirdPartyExceptions/` — Golden Slim (§10: `sealed record` Command / Query, `Handlers/CommandHandlers` +
  `Handlers/QueryHandlers`, soneksiz handler / validator, dosya başına tek `public` tip, tek `{Module}Models.cs`; silme yok — G-0179-01)
- `INV/Persistence/ThirdPartyLogistics/` — depolar, `ThirdPartyLogisticsCollections` (+4), index'ler (§4.9), başka modül okumaları **yalnız `Find`**
  (lot — MOD-0174; LOCATION; SKU) ve yazımlar yalnız `ThirdPartyTransactions.InTransactionAsync` içinde
- `INV/Api/Controllers/ThirdPartyLogistics/` — DL-2 controller'ları
- `INV/Api/ThirdPartyLogistics/` — manifest sağlayıcı (eklemeli), `ThirdPartyLogisticsServiceCollectionExtensions` (eklemeli), 90 gün süpürücüsü
- `INV/Infrastructure/Authorization/ThirdPartyLogisticsPermissions.cs` — DL-2 anahtarları
- `INVT/ThirdPartyLogistics/` — birim, entegrasyon, mimari, sözleşme testleri

### 1.3 XMC-001 dokunuşları (yalnız eklemeli; satırlar `<dilim-başı>`'nda yeniden ölçülür; **onay commit'i §0 madde 3**)

| # | Dosya | DL-2 değişikliği | Sahip | Onay | Hangi commit'te |
|---|---|---|---|---|---|
| X-6 (a) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` `Assert.Equal(<n>, WriteCommands.Length)` + yorum | dilim başı **+7**; yorumda 7 komut adıyla ve "measured <n> at <commit>" (DL-1 yorum emsali); modül listesi değişmez (`ThirdPartyLogisticsManifestProvider` DL-1'de girdi) | MOD-0173 (merkez, Beste) | **bekliyor** (G-2) | komutlarla aynı commit |
| X-6 (b) | aynı dosya `Assert.Equal(<n>, seen)` | **değişmez** (tek yardımcı). Yeni `WithTransactionAsync(` çağrı yeri gerekirse **dur**, CT'ye yaz | MOD-0173 | — | — |
| X-7 | `INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs` | `ThirdPartyException = "TPX"` sabiti + `TypePrefixes`'e (WAV / DVC emsali); var olan satır değişmez | MOD-0178 (merkez) | **bekliyor** (G-3) | `TPX` numarası üreten commit (AC-21) |
| X-8 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — **yalnız** `Assert.Equal(<n>, endpoints.Count)` + üstündeki yorum | dilim başı **+11**, yorumda 11 uç adıyla; **integration-agent** yazar (geliştirici önce sayıyı ve listeyi verir, dosyayı kendisi düzenlemez) | integration-agent | **bekliyor** (G-4) | DL-2 uçlarıyla **tek commit** |
| X-5' (CT) | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` 0179 vakası `Assert.Equal([...], ThirdPartyLogisticsCollections.All…)` | listeye dört ad (alfabetik); yorumda "MOD-0179 DL-2"; `declared` zinciri ve önek süzgeci **değişmez** (`All` zaten zincirde) | MOD-0173 (merkez) | **bekliyor** (G-5) | koleksiyon sabitleri commit'i |
| X-15' (CT) | `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` `Perm.Entity.*` | DL-2'nin yeni varlıkları için **yalnız satır adı** (kanonik anahtarı vitest `role-assignments-label-ledger` belirler; beklenen `MESSAGES`, `RAWMESSAGES`, `EXCEPTIONS` — çıkarım); yeni fiil yok; `_IndexL10n.cshtml`, `RoleAssignmentsIndex.*.resx`, `perm-label.js` değişmez | Web ortak (merkez, Beste) | **bekliyor** (G-6) | manifest anahtarlarını bildiren commit'in hemen ardından, ayrı küçük commit (DL-1 `66f7bdd59` emsali); vitest yeşil |

**Bu dilimde değil:** X-4 / X-9 (DL-1'de bitti, dokunulmaz); X-10…X-13 DL-6 / DL-7; X-14 DL-5 sonrası FU; X-15'in sayfa / nav / yardım parçası DL-3;
X-16 ayrı WP (Ali); X-19 DL-5.

**Değişmez (bağlayıcı):** `INVT/Warehouse/**`, `INVT/CycleCount/**`, `INVT/Barcode/**`, `INVT/WavePlanning/**` hiçbir satırı; `AuthorizationGuardTests`,
`LedgerImmutabilityArchitectureTests`, `ManifestNamespaceOwnershipTests`, `WarehouseRegistrationTests`, `InventoryLedgerManifestCompletenessTests`; Platform
`RecordCodeTypeCatalogTests` (`TPX` zaten `RuleAheadOfCatalogue`'da); `NavManifestL10nGuardTests`; MOD-0174 kodu ve koleksiyonları.

### 1.4 Kapsam dışı
DL-3: bütün Web ekranları, sayfa resx'leri, nav / sayfa yardımı, `PagelessUntil` silme (X-15' satır adları **hariç** — G-6). DL-4: `IBusinessPartnerDirectory`
gerçek istemcisi. DL-5: imzalı gelen uç (`POST inbound/{keyId}/messages`), `SendThirdPartyMessageCommand` + gönderici işçi, `SetThirdPartyChannelSecretRefCommand`,
`TestThirdPartyChannelCommand`, X-19, 3PL-BUNDLE dondurma. DL-6 / DL-7: talimat yazımı, dış release, MOD-0183 dispatch, MOD-0142 GRN (onayların **iş etkisi**
— C-3). DL-8: stok raporu, mutabakat, performans. Auth rol şablonu (Q-13, Ali).

## 2. Kararlar ve CT önerileri

**Kapalı (uygulanır):** kararlar 01'in bütünü (T-1…T-6, M-2, M-4, M-6, M-7, M-15, M-16, Q-1…Q-9, F-5 90 gün, F-8, F-10, F-11); DL-1 B-1…B-8; T-2 eki
(ücret mutabakatı v1'de yok — Beste 2026-11-20). Kaynak `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md`.

**Açık — CT kararı (geliştirici kendisi seçmez; CT önerisi parantezde; karar gelene dek ilgili vaka "bekliyor"):**
- **C-1 = Q-14 (başlama kapısı G-1):** `ReceiveThirdPartyMessageCommand` ve (DL-5'te) `SendThirdPartyMessageCommand` denetim yolu. (CT önerisi: **yol b**,
  her mesajda 1 merkezi kayıt — paketin varsayılanı §8.12; MOD-0182 Q-16 = yol b emsali; hacim ölçülünce yol c yeniden değerlendirilir. `Duplicate` mesaj
  da yazan komuttur — mesaj zarfı + `Ignored` istisna yazılır, 1 kayıt; idempotent replay 0.)
- **C-2 — DL-2'de `ReceiveThirdPartyMessageCommand`'ı kim çağırır:** gelen HTTP ucu DL-5'tir. (CT önerisi: DL-2'de üretimde çağıran yok; komut ve kanonik
  işlemci testlerden MediatR ile sürülür; aktör `channel:{channelId}` (§14) test kurulumunda verilir. `AllowAnonymous` ve imza süzgeci yazılmaz.)
- **C-3 — geçerli onayın iş etkisi:** `ShipmentConfirmation` / `ReceiptConfirmation` / `InventoryReport` kuralları geçince etkisi (dispatch / GRN / stok
  raporu) DL-6 / DL-7 / DL-8'dir. (CT önerisi: işlemci türe göre bir **akış portu** çağırır — tür başına port, adı geliştirici; DL-2'nin üretim kaydı
  "akış henüz etkin değil" der: mesaj `Failed`, istisna `Unsupported` + `DetailCode` = akış etkin değil (taslağa yeni detay kodu, CT inceler); akış
  dilimi gelince istisna yeniden işlenir. Testler sahte portla `Processed` yolunu da ölçer. Farklı okuma gerekirse CT'ye yaz.)
- **C-4 — `QuantityMismatch` / `LateConfirmation` talimat ister:** talimat yazımı DL-6 / DL-7. (CT önerisi: DL-2 testleri `ThirdPartyInstruction`'ı depoya
  doğrudan yazar (test kurulumu); üretimde talimat yoksa onay `LateConfirmation` değil `Format` / `Unsupported` değildir — "kaynak belge bulunamadı" ayrımı
  taslakta yazılır, CT inceler.)
- **C-5 — lot / SKT okuması:** (CT önerisi: MOD-0174 lot kaydı yalnız `Find` ile, var olan Inventory okuma yolundan; `LotIdentityPolicy` dışında lot
  anahtarı kurma yok — `TraceArchitectureTests.Only_LotIdentityPolicy_builds_a_lot_or_serial_key` değişmeden yeşil. Gerekirse dur.)
- **C-6 — X-15' (G-6):** Perm.Entity satır adları DL-2'de (DL-1 emsali). Sahip onaylamazsa: DL-2 manifest anahtarlarını bildirmez ve vitest kırmızı
  kalır — **kabul edilmez**; bu yüzden G-6 başlama kapısıdır.
- **C-7 — 90 gün süpürücüsü ayarı:** (CT önerisi: aralık `Warehouse:ThirdParty:PiiPurgeHours` = 24, yalnız okunur; kiracı listesi mevcut Inventory kiracı
  taraması deseninden; sistem aktörü merkezi kayıtta `SourceModule = THIRD-PARTY-LOGISTICS`.)
- **C-8 — taslak §9 madde 7 (ortağın deposu çıkarılınca kanal / eşleme):** (CT önerisi: DL-2 sağlık hesabı bunu "Hatalı" saymaz; paket kuralı yok — bilgi
  olarak taslakta kalır, DL-3 ekranında not. Bekletmez.)
- **C-9 — pano ucunun izni:** paket §14 "partners.read … DL-2'de sağlık / KPI"; taslak §4.2 `getThirdPartyDashboard` = `partners.read`. (Uygulanır; ayrı
  anahtar yok.)

## 3. Kurallar

- **Kiracı (Çekirdek 2):** her okuma / yazma `TenantId` + `LegalEntityId` (sunucuda); başka kiracı ve aynı kiracıda başka şirket **404**; gövde / sorguda
  `tenantId` / `legalEntityId` **400**. Sabit / varsayılan kiracı yok. **Silme yok** — mesaj atılır (`Discarded`, gerekçe), istisna yok sayılır (`Ignored`),
  süresi dolan bekletilen salt okunur kalır.
- **Yetki (Çekirdek 3):** `[Authorize]` + her eylemde eylem düzeyinde `[HasPermission(ThirdPartyLogisticsPermissions.X)]`; ham içerik **yalnız**
  `raw-messages.read` (AC-24); manifest yalnız `warehouse.third-party-logistics.*`; yeni fiil yok.
- **Denetim (BL-501, §8.12, §21):** her DL-2 yazma komutu `ICentralAuditDeclaration`, `SourceModule = THIRD-PARTY-LOGISTICS`, yol b (C-1); başarılı ve yazan
  1 kayıt, replay 0, başarısız 0. Olay adları ve alanlar §21 DL-2 satırları; yok sayma / atma **gerekçeyi** varlıkta taşır (taslak §9 madde 6 — BL-501
  kaydı gövde taşımaz). İş yazımı + idempotency kaydı + merkezi kayıt **aynı işlemde**.
- **Kişisel veri (F-5, §8.7):** ham içerik log'a / OTel'e / merkezi kayda / idempotency deposuna / kanonik özete **yazılmaz**; okumada maske; 90. günde kalıcı
  temizlik. `ThirdPartyException.DetailArgs` kişisel veri taşımaz. Sır değeri ve imza hiçbir yerde yok.
- **Dil (Çekirdek 4):** 7 dil (en, tr, fr, es, zh, ar, ru); istisna metinleri `DetailCode` + `ThirdPartyText.{7}.resx` (parite testi + `Accept-Language` vakası).
  Bir dil eksikse iş bitmemiştir.
- **Korunan yollar (Çekirdek 6):** `.antigravity/**` yazılmaz; `ocelot.json` değişmez (X-8 sayı satırını **integration-agent** yazar);
  `services/Diten.Building.Blocks/**`, `_Layout.cshtml`, Archive, `services/Diten.{MdmService,Platform,AuthService,ProcurementService,SupplyChainService}/**`
  dokunulmaz; donmuş sözleşmeler (`wms-bundle`, `location`, `inventory-bundle`, `grn-event`) değişmez.
- **XMC-001 (Çekirdek 9):** §1.3 dışında başka modül dosyasına dokunma; gerekirse **dur**, CT'ye sor.
- **Git (Çekirdek 8, GIT-002):** `main`'e commit / push yok; **push ve PR yok** (modül sonunda, ayrı kullanıcı onayıyla, öncesinde `origin/main` merge
  commit ile alınır). Stage yalnız açık dosya yoluyla (`git add -A` / `git add .` yok; izlenmeyen `__pycache__/`, `docs/records/reviews/MOD-0178/*-raw-*`
  stage'e girmez). Hook / imza atlanmaz; yıkıcı git komutu onaysız yok; `git stash` yalnız etiketli ve SHA ile. İletiler `feat(sce): MOD-0179 DL-2 — …`,
  sonda oturumun `Co-Authored-By` satırı.
- **Commit sırası (öneri):** (1) taslak DL-2 bölümü → (2) Domain (saf kurallar + birim testleri) → (3) Persistence + index'ler + koleksiyon sabitleri + X-5'
  (**aynı commit**) → (4) komutlar / sorgular / kanonik işlemci + X-6 (a) (**aynı commit**) + X-7 (`TPX`, numara üreten commit) → (5) izinler + manifest →
  (6) X-15' satır adları (ayrı küçük commit) → (7) controller uçları + X-8 (**tek commit**, integration-agent adımı) → (8) süpürücü + kalan vakalar,
  sözleşme testi. Sayaç commit'lerinde (3), (4), (7) InventoryService filtresiz koşusu (kırmızı pencere yok kanıtı; tek takım kuralıyla).

## 4. Test ve kanıt

- **AC (paket §24 DL-2 satırı — tam):** AC-10, AC-12, AC-13, AC-14, AC-15, AC-16, AC-17, AC-18, AC-20, AC-21, AC-24, AC-25, AC-60. Her vaka paketteki
  metinle (§16 — aç, özet yetmez). Ek (WP): sözleşme testi DL-2 şemaları kapalı; kiracı / şirket 404 ve gövdede kapsam 400 DL-2 uçlarında; süpürücü yalnız
  `PurgeThirdPartyMessagePiiCommand` gönderir (mimari vaka).
- **Sabotajlar (§24 DL-2 — tam, 20):** S-10, S-12, S-13a, S-13b, S-14, S-15a, S-15b, S-15c, S-16, S-17, S-18, S-20a, S-20b, S-21, S-24a, S-24b, S-25,
  S-60a, S-60b, S-60c. Her biri **üretim koduna** (Çekirdek 10): değişiklik → kırmızı test adı + çıktı özeti → **bayt-eşit geri al** (dosya sha256 önce =
  sonra) → yeşil; `git status` temiz; commit'e girmez. Pakette adı geçen kırmızılar: S-20a / S-20b → `CentralAuditDeclarationTests`; S-21 → `TPX`'i
  `TypePrefixes`'ten çıkar → numara vakası; S-25 → `Mod0178WarehouseRouteTests.Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route`
  (sayıyı eski değere döndür). Ek WP sabotajları: **S-W1** X-5' satırından bir adı çıkar → `SingleSystemOfRecordArchitectureTests` kırmızı; **S-W2**
  `SharedResource.ru.resx`'ten bir DL-2 `Perm.Entity.*` satırını sil → `role-assignments-label-ledger.test.js` kırmızı; **S-W3** süpürücüye başka komut
  gönderimi ekle → süpürücü mimari vakası kırmızı. Diğerlerinin test adı pakette yok — `INVT/ThirdPartyLogistics/` altında adlandır, raporda `S-n` ile eşle.
  Listede olmayan her kırmızı adıyla raporlanır.
- **InventoryService takımı filtresiz** (servis dokunuldu; `~filter` yok): `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests`.
  **`INVENTORY_TEST_MONGO` boş** (takım kendi replika setini açar), **27017 kullanılmaz**; `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`.
  **Aynı anda tek Inventory takımı.** Dilim başı ve sonu (toplam / geçti / kırmızı / atlandı); son CT ölçümü `6cdb92e43`: 2662 / 2658 / 0 / 4. Taban
  kırmızıları adıyla, aynı gün `main`'de ölçülerek ayrılır.
- **Gateway:** `dotnet test gateway/Diten.ApiGateway.Tests` — önce `<dilim-başı>` sayısıyla, sonra +11 ile yeşil (AC-25); son CT ölçümü 384 / 384.
  `git diff <başı>..<sonu> -- gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` yalnız sayı satırı + yorum.
- **Web (X-15' nedeniyle):** vitest `node_modules\.bin\vitest.cmd run tests/role-assignments-label-ledger.test.js` (`frontend/Diten.Web`; ayrık worktree'de
  ana checkout'un `node_modules`'una junction, `npx` yok). `dotnet test frontend/Diten.Web.Tests` filtresiz — `NavManifestL10nGuardTests` (girdi duruyor)
  yeşil; taban kırmızıları adıyla. Build `Diten.Web` PASS.
- **Platform** `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~RecordCodeTypeCatalogTests` — değişmeden
  yeşil (`TPX` önde). Platform takımıyla aynı anda başka Platform koşusu yok (makine çapında Mongo kilidi).
- **Mimari:** `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests`.
- **Değişmez dizin kanıtı:** `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse
  services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Barcode
  services/Diten.InventoryService/tests/Diten.InventoryService.Tests/WavePlanning` **boş**.
- **`Diten.AuthService.AccountKindAcceptanceHost.Tests` koşulmaz.** Portlar yalnız AGENTS.md §3 (Inventory 5067, Gateway 5000).

## 5. Kabul kapıları

| # | Kapı | Ölçüm |
|---|---|---|
| K-0 | **Başlama:** hat ucu ≥ `8c32f4d90`; paket sha `1ac6e1a7…`; G-1 (Q-14) ve G-2…G-6 onay commit'leri var | §0 |
| K-1 | Taslağın DL-2 bölümü bağlayıcı (11 işlem, kanonik model, hata kodları); sözleşme testi kapalı şemayla yeşil; `FROZEN` yok | §1.1 |
| K-2 | AC tablosu: 13 AC'nin hepsi test adı + sonuçla; C-3 / C-4 uygulanışı ayrı satırda | §4 |
| K-3 | 20 paket sabotajı + S-W1…S-W3 kırmızı → geri al (sha256 eşit) → yeşil | §4 |
| K-4 | Sayaçlar dilim başı + 0179 artışı: uç +11, komut +7, `seen` 0, `TypePrefixes` + `TPX`, 0179 koleksiyon vakası +4 | §0 madde 5 |
| K-5 | InventoryService filtresiz takımda yeni kırmızı yok; Gateway, Web.Tests, vitest, Platform katalog, TenantArchitecture yeşil ya da yalnız taban | §4 |
| K-6 | Değişmez dizin diff'i boş; `ocelot.json`, `.antigravity/**`, Building.Blocks, Platform, MDM, Auth, Procurement, SupplyChain diff'i boş; Web diff'i yalnız X-15' satırları | `git diff --stat` |
| K-7 | Kişisel veri taraması: ham içerik log / merkezi kayıt / idempotency / kanonik özette yok; 90. gün sonrası depoda maskeli | AC-24, AC-60 |
| K-8 | Stage açık yollarla, hook atlanmadı, push / PR yok, `main`'e commit yok | `git log`, rapor |

**DL-2 VERIFIED** CT kabul kaydıyla verilir (CT kendi ayrık worktree'sinde filtresiz koşuları ve seçili sabotajları tekrar eder; bağımsız güvenlik incelemesi —
kiracı 404, izin, ham içerik / kişisel veri sızıntısı). Ardından **DL-3 WP'si** (Q-17 CT kararıyla) yazılır.

## 6. Rapor

1. Paket sha256 ölçümü; dal adımı ve `merge-base` doğrulamaları; `<dilim-başı>` commit'i.
2. Başlama kapıları G-1…G-6: karar / onay commit'leri — ya da "onay yok — dokunulmadı".
3. Commit listesi (`git log --oneline <dilim-başı>..HEAD`) ve `git diff --stat`; X-8'in uçlarla, X-6 (a)'nın komutlarla, X-5'in koleksiyonlarla aynı commit'te
   olduğu açıkça; sayaç commit'leri başına filtresiz takım sayısı.
4. Sayaçlar önce → sonra (satırın kendisiyle, ölçüldüğü commit ile): `endpoints.Count`, `WriteCommands.Length`, `seen`, `TypePrefixes`, 0179 koleksiyon listesi.
5. Takım sayıları önce / sonra: InventoryService (geçersiz koşular ayrı), Gateway, Web.Tests, vitest, Platform katalog, TenantArchitecture.
6. AC tablosu (13 AC): test adı + sonuç; C-1…C-9'un nasıl uygulandığı.
7. Sabotaj tablosu (23): üretim dosyası:satır, değişiklik, kırmızı test adı + çıktı özeti, geri alma sha256, geri yeşil.
8. 3PL-BUNDLE taslağı: değişen bölümler, yeni hata / detay kodları, CT'ye düzeltme önerileri.
9. Değişmez dizin / korunan yol diff kanıtları; MOD-0180 DL-5 / MOD-0182 DL-4 ile sayaç çakışması olduysa nasıl çözüldüğü; DL-3'e devredilenler (ekran
   ihtiyaçları, `displayName`, nav / yardım anahtarları).

## 7. Kural haritası (AGENTS.md §6.1)

- **Daima:** `multi-tenancy`, `security-jwt`, `git-safety`, `code-style`, `docs-organization`, `cross-module-change-consent` (XMC-001).
- **Backend:** `handler-design`, `repository-standard`, `response-envelope`, `pipeline-behaviors`, `entity-base-template`, `entity-versioning`, `mongo-indexing`,
  `api-conventions`, `routes`, `ports`, `audit-trail-standard` (AUD-001). 90 gün kişisel veri kuralının kaynağı paket F-5'tir (ayrı kural dosyası yok).
- **Yetki:** `permission-key-standard` (PKS-001 `warehouse.third-party-logistics.*`), `business-module-enforcement-standard`, `data-scope-enforcement`.
- **Kayıt kodu:** `record-coding-standard` (RCS-001 §3 `TPX`).
- **Metin:** `localization-standard` (kiracı 7 dil).
- **Ortam / kanıt:** `dev-runbook`, `configuration-safety`, `logging-observability` (ham içerik log'a yazılmaz), `status-reporting-and-evidence` (REP-001).

Dosyalar `.antigravity/rules/<ad>.md`; okumak serbest, yazmak yasak.
