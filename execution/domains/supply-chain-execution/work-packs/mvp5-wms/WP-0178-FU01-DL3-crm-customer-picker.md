# WP-0178-FU01-DL3 — Çıkış formu v2 DL-3: alıcıyı CRM'den seçme (Web)

- **Kimlik:** WP-0178-FU01-DL3 · **Modül:** MOD-0178-FU01 Outbound Shipment Form v2 (supply-chain-execution, yalnız `Diten.Web`; kiracı modülü).
- **Paket:** `execution/domains/supply-chain-execution/module-packs/MOD-0178-FU01-outbound-form-v2.md` **r4**, LF sha256
  `949ab3c997e65d59f0ba5579c3a3b6d3a796642675f991014c789f012ce711ca` (HEAD `30c313eb4`'te ölçüldü), `status: ready-for-dev`, onay satırı
  `execution/approvals/approvals.tsv:90` (onaylayan Ali / GMG-ERP). Paket bu dilimde **değişmez** (değişirse onay düşer).
- **Önceki dilimler:** DL-1 VERIFIED, DL-2 VERIFIED (`9922f2b8f` + `321b9a40d`) — `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md`
  §2, "Ek — MOD-0178-FU01 DL-1 canlı tur", "Ek — MOD-0178-FU01 DL-2". CT sözleşme adımı `2a1ce1c85`: WMS-BUNDLE **1.3.0** + WAREHOUSE-OUTBOUND
  **1.1.0** (`customerRef`, `customerAddressId`, `removeCustomerRef`, dört adres üyesi, üç `WMS_CUSTOMER_*` kodu; bekleyen listeler silindi).
- **Sahip kararı (başlatma):** Beste, 2026-11-14 — "DL-3'ü başlat, bekleyenler (Q-5 Ali Auth eşlemesi, XMC-CRM-01 Beste, Q-6 Natig) geldiğinde
  eklemeleri yaparız"; kayıt `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md` "Ek — … MOD-0178-FU01 başlatma"
  (commit `a974b20d5`).
- **Dilim:** DL-3 (paket §23, §24 DL-3 satırı). **Kanıt seviyesi:** E4. **Sahip:** geliştirici oturumu. **CT doğrulayıcı:** MVP-5 WMS CT (Beste).
- **Dil:** kullanıcıya görünen her metin 7 dilde (en, tr, fr, es, zh, ar, ru); `ar` sağdan sola. Bir dil eksikse iş bitmemiştir.

## 1. Kapılar — ne hemen, ne sonra

Bu WP DL-3'ü **iki parçaya** böler. **A parçası hemen kodlanır.** **B parçası** paketin DL-3 başlama kapısı olan CRM-ACCOUNT-LOOKUP 1.1
donmasına bağlıdır (paket §1 madde 3 "CRM 1.1 donmadan bu dilim başlamaz", §24 DL-3). Sahip kararı üç kapıyı (Q-5, XMC-CRM-01, Q-6) beklemeden
başlatır; 1.1 donmasını kayıtta adıyla kaldırmaz. Bu yüzden 1.1'in yeni ucuna (U-4 `ship-to`) dayanan her şey B'dedir (B-1, §11).

### 1.1 Kapıların bugünkü durumu (ölçüldü, HEAD `30c313eb4`)

| Kapı | Paketteki yeri | Bugün | Etkilediği | Sahip |
|---|---|---|---|---|
| **G-1 — CRM-ACCOUNT-LOOKUP 1.1.0 donması** (U-4 `GET /api/crm/accounts/{id}/ship-to`, `crm.account-ship-to.read`) | §24 DL-3 başlama | **Donmadı.** Temel `docs/analysis/contracts/crm-account-lookup.md` hâlâ `1.0.0 FROZEN` (LF sha256 `fce0d36b…`), U-4 yok, MOD-0178 §5a'da yok. Taslak `docs/analysis/contracts/drafts/crm-account-lookup-v1.1-ship-to-proposal.md` **r7** (LF sha256 `fb758409…`, `x-status: DRAFT`), main'de (PR #420). Taslağın kendi donma koşulu: "pack eki onayı + DL-3 CT K13 kabulü" (`:6`). MOD-0149 §32 eki onaylı (`approvals.tsv:85`, `8749bc2a` = HEAD'deki paket LF sha256'sı). **CrmService'te U-4 kodu yok** (`git grep account-ship-to -- services/Diten.CrmService` boş; `git log --all -S "account-ship-to.read" -- services` boş). CRM manifestinde `crm.account-ship-to.read` yok (`CrmManifestProvider.cs` yalnız `crm.account.lookup-validation`, `:66`). Tahmin C-10: ana adresle ~2 hafta | **B parçası** | Beste — CRM (kod + CT K13) |
| **G-2 — XMC-CRM-01** (§5a'da MOD-0178 satırı: U-1, U-3, U-4) | §24 DL-3 başlama; DL-2 kabul | Yok (`crm-account-lookup.md:250-267`: MOD-0120 tüketici; MOD-0177, MOD-0183 / MOD-0186 aday) | DL-3 **kabulü** (belge kanıtı; kod değişmez). Sahip kararıyla başlamayı bekletmez | Beste — CRM |
| **G-3 — Q-5 Auth rol eşlemesi** (`crm.account.lookup-validation` + `crm.account-ship-to.read` → `ShippingPlanner`, `WarehouseSupervisor`) | §24 DL-3 kabul | Yok (`services/Diten.AuthService` içinde `crm.account.lookup-validation` geçmiyor). İkinci anahtar CRM manifestine girmeden eşlenemez (G-1'in DL-3b'si) | Sevkiyat rolleriyle canlı ölçüm (AC-31 (a)). Eşleme yokken seçici bu rollerde **çizilmez** — sistem elle girişle çalışır | Ali |
| **G-4 — Q-6 MVP-6 cevabı** | DL-2 yayın kapısı | Açık; CT WAREHOUSE-OUTBOUND 1.1.0'ı paketin Q-6 varsayılanıyla yazdı (`2a1ce1c85` iletisi) | DL-3 kodunu **etkilemez** (bilgi). MVP-6'nın U-4'e geçişi kendi işi | Natig |
| Q-11 — Reference Data kaynağı | §24 DL-3 başlama | **Kaynak ölçüldü** (§4.3): Platform `GET /api/lookups/reference-data/consumable-sets/{setCode}/published-values`, `country` / `city` / `district` izinli listede. Kalan: kümelerde 7 dil etiketi var mı, `city` / `district` boyutu ve kod biçimi → geliştiricinin ilk ölçümü | A parçası (ülke seçici, Details), B (kod → ad) | CT ölçümü + geliştirici |
| Q-13 — U-4 cevabında hesap durumu | §24 DL-3 başlama | Taslak r7 §2.2 `status` taşıyor (zorunlu, boş değil); `isBillable` taşımıyor (`:96`). Donmada kesinleşir | B parçası (Q-12 uyarısı) | Beste — CRM |
| DL-1 ve DL-2 kabulü | §24 DL-3 | **Kapandı** (VERIFIED) | — | — |

### 1.2 CT ön adımı gerekiyor mu?

- **A parçası için hayır.** A yalnız donmuş sözleşmelere dayanır: CRM-ACCOUNT-LOOKUP **1.0.0 U-3** (FROZEN, main'de `38f7ea08d`) ve WMS-BUNDLE
  **1.3.0** (CT adımı `2a1ce1c85`). Tek CT işi: hat etiketi `ct/line-ff-2026-11-14d`'yi oluşturmak (§3).
- **B parçası için evet — CT ön adımı (commercial-suite CT zinciri): CRM-ACCOUNT-LOOKUP 1.1.0 donması.** Gerekenler, sırayla:
  1. MOD-0149 DL-3 (U-4 ucu) ve DL-3b (CRM manifestinde `crm.account-ship-to.read`, Platform dosyası) kodu — paket §32.5 AC-ST-1…AC-ST-10,
     §32.6 sabotajları; §32.9'a göre CRM senkron işinden (S0) sonra. CRM geliştiricisinin işi (Beste — CRM).
  2. CT K13 kabulü (MOD-0149 DL-3 + DL-3b).
  3. Taslağın temel dosyaya işlenmesi — taslak §5 "Donmada temel dosyaya işleniş" listesi: künye (`version 1.1.0`, `x-status`, `x-consumers`,
     `x-implemented`), §1, yeni §2b (U-4 gövdesi), §5a (tüketici satırları — **MOD-0178 satırı burada yazılırsa XMC-CRM-01 de kapanır**),
     yeni §5b (U-4 tüketici kuralları), §6, §7, §8, §9, §10, §11 (K-9 kapandı, K-12…K-19), §12.
  4. Donma commit'i hatta; CT bu WP'ye "G-1 kapandı" ekini yazar (commit + temel dosyanın LF sha256'sı) ve B parçasını açar.
- **XMC-CRM-01 için ayrıca:** U-1 / U-3 satırı 1.0 içinde, donmayı beklemeden eklenebilir (paket §7, "U-1 ve U-3 satırı 1.0 içinde
  eklenebilir; `ship-to` sütunu 1.1 metniyle birlikte"). CRM sahibine iletilecek satır metni paket §7 XMC-CRM-01 hücresindedir.

## 2. Amaç ve kapsam

**Amaç (paket §23 DL-3):** alıcıyı CRM'den seçme (izin koşullu seçici, teslimat adresi listesi, ön doldurma, boş liste, bağlantıyı kaldırma,
kayıtta CRM hatası, seçimden sonra pasifleşen hesap uyarısı) + Details rozeti, ülke adı, ilçe + canlı E3.

**Paket bölümleri (özet yetmez — aç):** §1 madde 3, §4.1a, §5 "DL-3", §6, §8.4 (tamamı), §8.7 son iki madde (CT bulgusu), §9, §11 (resx anahtarları),
§12, §13, §14, §16 AC-25…AC-32, §17, §19.3 ASSUMPTION-FU01-03, §21 son iki satır, §24 DL-3, §25.

**Servis değişikliği yok.** InventoryService, WMS sözleşmeleri, Gateway ve `ocelot.json` değişmez (paket §8.4 "WMS sözleşmesi ve InventoryService
değişmez", §15). Bekçi: `git diff --stat <dilim-başı>..<dilim-sonu> -- services/ gateway/ docs/analysis/contracts/` **boş**.

### 2.1 A parçası — hemen kodlanır

| # | İş | Paket | AC |
|---|---|---|---|
| A-1 | **Web gövde / görünüm tamamlama (DL-2 boşluğu, ölçüldü):** Web proxy dört yeni adres üyesini bugün **düşürüyor** — `OutboundAddressRequest` ve `WmsOutboundAddressDto` altı üye (`Models/Warehouse/OutboundDtos.cs:9-17`, `:249-257`), `Address()` yalnız altı üyeyi yazıyor (`Controllers/OutboundShipmentsController.cs:440-448`); `WmsOutboundViewDto`'da (`OutboundDtos.cs:107-130`) ve `Shape()`'te (`OutboundShipmentsController.cs:183-225`) `customerRef` / `customerAddressId` yok. Ekle: `countryName`, `cityRef`, `district`, `districtRef` (create / preview / edit gövdesi ve görünüm; sınırlar paket §12, boşsa gönderme — kural 4); görünüme `customerRef`, `customerAddressId`. GUID JSON'da Edit formu için taşınır, ekranda hiçbir yerde basılmaz | §4.1a, §8.5 madde 6, §12 | AC-26 (gövde), AC-30 |
| A-2 | **Çizim kapısı (UAS-001):** Create / Edit `_Form.cshtml` CRM kontrollerini sunucu tarafı izin anlık görüntüsüyle çizer (`@inject IPermissionSnapshot Perms` — `Create.cshtml:6` deseni; emsal `Views/SupplyChain/Shipments/_Form.cshtml:12-13`, ama devre dışı + gerekçe **değil**, hiç çizilmez). CRM anahtar sabitleri denetleyicide (emsal `SupplyChainShipmentsController.cs:23`); `WarehousePermissionKeys`'e CRM anahtarı yazılmaz. A'da çizilenler: "CRM müşterisi" seçici + "Bağlantıyı kaldır" (`crm.account.lookup-validation`). Teslimat adresi seçici ve "CRM'den yeniden al" **B'de** eklenir | §8.4 çizim kapısı tablosu, §9, §14 | AC-28 (a), (b), (d)-`customers` |
| A-3 | **`customers` proxy (U-3, donmuş 1.0):** `GET /api/crm/accounts/lookup?search=&page=&pageSize=20&billableOnly=true`, sunucu tarafında Gateway (5000), kullanıcının token'ıyla; sayfa anahtarı + `crm.account.lookup-validation` yoksa 403 ve CRM çağrısı yok. Cevaba yalnız kimlik, ad, kod. 400 / 401 / 403 / 5xx / zaman aşımı → seçicide "CRM araması yapılamadı — alıcıyı elle girin". CRM'in genel uçları (`/api/crm/accounts`, `/{id}`) ve `crm.account.read` **hiçbir dosyada yok**. Günlüğe CRM'den değer yazılmaz | §8.4, §8.7, §2 günlük kuralı | AC-25, AC-28 |
| A-4 | **Seçimde ön doldurma (yalnız U-3):** seçim alıcı adını U-3 satırından doldurur, `customerRef` gizli alanda; adres alanları elle (B gelene kadar herkes için "yalnız `lookup-validation`" satırının davranışı — paket §8.4 tablo satır 2). Ad düzenlenebilir, bağlantı kalır (Q-3). "Bağlantıyı kaldır": `customerRef` / `customerAddressId` gönderilmez, alanlar kalır | §8.4 | AC-26 (Q-3 ad vakası), AC-27 |
| A-5 | **Ülke seçicisi (elle girişte de):** ülke Reference Data `country` kümesinden seçilir (kod + ad), gövdeye `country` = küçük harf kod ve `countryName` = kullanıcının dilindeki ad. **Eski serbest metin ülke** (F-11): Edit'te kayıtlı değer önceden seçili tek ek seçenek ("(kayıtlı değer)" eki); dokunulmazsa gövdede `shipTo` yok, başka alıcı alanı değişirse kayıtlı metin aynen gider. Edit karşılaştırması (`form.js:526`, bugün altı üye) dört yeni üyeyi de sayar. Kaynak §4.3 | §8.4 son üç madde, §4.1a | AC-26 (eski ülke vakası) |
| A-6 | **Details:** `customerRef` varsa "CRM müşterisi" rozeti (GUID yok, CRM çağrısı yok); "Ülke" `countryName` doluysa "ad (kod)", yoksa `country` aynen (`Details.cshtml:93`, `details.js:144`); yeni "İlçe" satırı (`FieldDistrict`) "Şehir"den sonra, boşsa "-"; `cityRef` / `districtRef` görünmez. `details.js`'te yalnız özet kartı — onay akışları (`:171-245`) değişmez | §8.4 Details, §6 | AC-30 |
| A-7 | **Kayıtta CRM hatası:** `customerRef`'li kayıt 503 `WMS_CUSTOMER_DIRECTORY_UNAVAILABLE` alırsa uyarı + "Bağlantıyı kaldırıp elle kaydet"; eylem `customerRef`'siz, `customerAddressId`'siz ve **yeni** istek anahtarıyla gönderir. 422 `WMS_CUSTOMER_NOT_FOUND` / `WMS_CUSTOMER_NOT_ACTIVE` kendi metniyle | §8.4 "Kayıtta CRM hatası" | AC-29 (ilk cümle) |
| A-8 | **Üç kodun Web metni (DL-2 raporu açık nokta 3):** `Warehouse.Error.WMS_CUSTOMER_NOT_FOUND`, `…_NOT_ACTIVE`, `…_DIRECTORY_UNAVAILABLE` — `OutboundShipmentsIndex.{7}.resx`. Bugün `WarehouseErrorMessages.KeyForS2` (`Models/Warehouse/WarehouseProxy.cs:232-233`) bu kodları bilmiyor, 422 / 503 genel metne düşüyor. **CT kararı (K-1, §5):** kodlar `S2Codes`'a **eklenmez** (eklenirse `WarehouseS2ScreensTests.cs:315` PickTasks ve Packing ailelerinde de 7 dil ister — paket kapsamı dışı); yalnız outbound denetleyicisinin okuduğu ayrı liste + anahtar seçici, parite testi yalnız `OutboundShipmentsIndex` ailesi için genişler | §11 son satır | AC-29, CT ek S-FU01-DL3-L10N-WP |
| A-9 | **Metinler:** `Customer.*` (seçici, ipuçları, "CRM araması yapılamadı — alıcıyı elle girin", "Bağlantıyı kaldır", "Bağlantıyı kaldırıp elle kaydet", rozet, "(kayıtlı değer)", "CRM kaydından dolduruldu ({0}). Değişiklik yalnız bu sevkiyata yazılır."), `FieldDistrict` — 7 dil; JS kelimeleri `WarehouseVocabulary.OutboundPageWords` (`:104`) üzerinden. "Yetkiniz yok" metni **yazılmaz** | §11 | AC-28, AC-13 deseni |
| A-10 | **Sayfa yardımı:** `Help.OUTBOUNDSHIPMENTS.Step4` (7 dil, `PageHelpResource`) — "alıcıyı elle yaz ya da — CRM yetkin varsa — CRM'den seç…"; çizilmeyen bir kontrolü koşulsuz vaat etmez; adres seçimi cümlesi B'de eklenir. Tur yok | §25 | AC-31 sahip turu |

### 2.2 B parçası — G-1 (CRM 1.1 donması) gelince ek adım

B, CT'nin "G-1 kapandı" ekiyle açılır. **Donmuş metin taslak r7'den farklıysa** (alan adı, `addresses[]` kuralı, durum kodları, `contractVersion`)
geliştirici donmuş metne göre yazar ve farkı raporlar; paket metniyle çelişki çıkarsa durur, CT'ye yazar (paket kazanır, paket değişikliği CT / sahip işi).

| # | İş | Paket | AC |
|---|---|---|---|
| B-1 | **`customer-ship-to/{accountId}` proxy (U-4):** sayfa anahtarı + `crm.account-ship-to.read` yoksa 403, CRM çağrısı yok. Cevap: öğe başına kod + Reference Data'dan kullanıcının dilinde ad (`country`, `city`, `district`); ad çözülemezse kod + "Ad okunamadı". 404 → "CRM kaydı bulunamadı — alıcıyı elle girin", bağlantı kurulmaz; 400 / 401 / 403 / 5xx / zaman aşımı / okunamayan gövde → "CRM adresi okunamadı — adresi elle girin", ad (U-3 satırından) dolar, adres boş, bağlantı korunur, `customerAddressId` gönderilmez. Günlük kuralı (adres alanı, posta kodu, `label` hiçbir satırda yok) | §8.4, §2 | AC-26, AC-32, AC-28 (c), (d)-`customer-ship-to`, AC-25 günlük vakası |
| B-2 | **Teslimat adresi seçici + ön doldurma:** `defaultAddressId` önceden seçili (yoksa ilk öğe); tek öğede salt gösterim; seçilen öğe ad, ülke (kod + ad), şehir (ad; kod gizli), ilçe (ad; kod gizli), posta kodu (doluysa), Adres 1'i doldurur; `customerAddressId` gizli alanda. Başka adres seçimi düzenlenmiş alanları ezmeden önce `window.showConfirm`. Elle değiştirilen şehir / ilçe adının kodu gönderilmez. **Boş liste:** bağlantı kurulur, ad dolar, adres boş ve açık, uyarı "Bu CRM kaydında teslimat adresi yok — adresi elle girin.", `customerAddressId` yok, kayıt engellenmez | §8.4 | AC-26 |
| B-3 | **"CRM'den yeniden al" (Edit):** liste yeniden okunur; kayıtlı `customerAddressId` listede varsa o, yoksa varsayılan seçili; gövde kayıtlı `customerRef`'i aynen taşır (InventoryService U-1'i çağırmaz — F-8) | §8.4 Edit | AC-26, AC-29 |
| B-4 | **Q-12 uyarısı:** `edit/{id}` (`OutboundShipmentsController.cs:316-329`) ve `release/{id}` (`:332-346`) InventoryService **başarılı** döndükten sonra, outbound'un `customerRef`'i doluysa **ve** kullanıcının `crm.account-ship-to.read` anahtarı varsa U-4'ü okur; `status ≠ "active"` → Web cevabına `customerWarning { code: "CustomerNotActive", accountCode }`. Okunamazsa uyarısız, cevap aynen. İkinci WMS çağrısı yok, ret yok. `customerRef` kaynağı: önce ölç — `WriteAsync` (`OutboundShipmentsController.cs:515-527`) tarayıcıya yalnız id / number / status / etag döndürüyor; servis cevabının görünümünde `customerRef` var mı (DL-2 sonrası beklenir) **ilk ölçüm**. Gösterim: Edit'te yönlendirmeden **önce** `window.showConfirm` (ikon `bx-error`, tek "Tamam", onay ve kapatma aynı yönlendirme; bugünkü toast + hemen yönlendirme `form.js:530-531`); Details "sevke hazır"da `load()`'dan sonra (`details.js:199-203`) | §8.4 Q-12, §21 | AC-29 (Q-12, gösterim, anahtar vakaları) |
| B-5 | Sayfa yardımı Step4'e teslimat adresi onayı cümlesi (7 dil) | §25 | — |
| B-6 | **Canlı (AC-31 (a)):** Gateway üzerinden gerçek oturumla CRM'den seçilmiş bir outbound. G-3 yoksa yönetici rolüyle yapılır ve kayda "sevkiyat rolüyle ölçülmedi (Q-5)" yazılır (emsal WP-0178-FU01-DL2 §3) | §16 AC-31 | AC-31 (a) |

## 3. Adım 0 — hattı al

Ana checkout'ta, çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` üzerinde (yeni dal açılmaz):

```bash
git checkout feature/sce/mod-0173-inventory-ledger-valuation
git merge-base --is-ancestor ct/line-ff-2026-11-14d HEAD && echo "ata — devam"
# ata değilse:
git merge --ff-only ct/line-ff-2026-11-14d
# ff olmazsa:
git merge --no-ff ct/line-ff-2026-11-14d
# çakışmada: git merge --abort, dur, CT'ye bildir (çakışmayı kendin çözme)
```

- Etiketi CT oluşturur; etiket yoksa dur, CT'ye sor.
- Doğrula: `git merge-base --is-ancestor 2a1ce1c85 HEAD` ve `git merge-base --is-ancestor a974b20d5 HEAD` çıkış 0; paket LF sha256
  (`git show HEAD:<paket> | sha256sum`) `949ab3c9…` ile başlar. Değilse dur.
- Dilim başı commit'ini (`<dilim-başı>`) kaydet. "Önce" ölçümleri bu commit'te, aynı gün: Inventory, Web.Tests, Gateway filtresiz taban (kırmızılar
  adıyla — §8); vitest dokunulan dosyalar; `NavManifestL10nGuardTests.NavVisiblePageKeyFloor` ve `PagelessUntil` (değişmemeli);
  `Mod0178WarehouseRouteTests` `Assert.Equal(<n>, endpoints.Count)` (değişmemeli — Web proxy eylemi Inventory ucu değildir).
- **Eşzamanlılık:** ana checkout'ta Inventory 5067 `dotnet watch` ile çalışabilir ve başka işlerin commit'lenmemiş değişiklikleri durabilir
  (HEAD'de CycleCount / sözleşme dosyaları). Onlara dokunma, stage etme. Derleme, test ve sabotaj ayrık worktree'de; commit çalışma dalına.

## 4. İlk adımlar (kod yazmadan önce)

### 4.1 XMC-001 kayıt denetimi
§5 tablosu dışındaki bir modül dosyası gerekirse **dur**, CT'ye yaz. Onay kayıtları teslim raporuna.

### 4.2 Paket §14 ilk ölçümü — eylem başına anahtar
Ölçüm (HEAD `30c313eb4`): denetleyici her eylemde **tek** sayfa anahtarını satır içinde ister (`_permissions.Has(...)`, `OutboundShipmentsController.cs:243-377`).
Paket §14: "Web proxy eylemi tek anahtar alıyorsa `customers` / `customer-ship-to` her sayfa için ayrı eylemdir; iki anahtardan birini kabul eden ortak
eylem yazılmaz." Buna göre: Create sayfası eylemleri `.create` + CRM anahtarı, Edit sayfası eylemleri `.update` + CRM anahtarı ister — iki ayrı rota
(ad önerisi: `customers`, `customer-ship-to/{accountId}` ve `edit-customers`, `edit-customer-ship-to/{accountId}`; adı geliştirici seçer, raporda yazar).
`countries` (A-5) için aynı kural.

### 4.3 Reference Data kaynağı (Q-11) — ölçüldü, kalanı ilk ölçüm
- Uç: Platform `GET /api/lookups/reference-data/consumable-sets/{setCode}/published-values` (Gateway üzerinden), `[LoginOnly]` — her oturumlu kiracı
  kullanıcısı (`services/Diten.Platform/src/Diten.Platform.API/Controllers/ConsumableReferenceDataController.cs:17`, `:44`). `country`, `city`,
  `district` izinli listede (`…/BusinessReferenceData/Services/BusinessReferenceDataConsumableSetsOptions.cs` `DefaultConsumableSets`). CRM anahtarı
  gerektirmez → ülke seçicisi elle girişte de çalışır.
- Web'de okuyucu var: `frontend/Diten.Web/Services/CrmReferenceSetReader.cs` (yedek yol ve 404 sinyalleri dahil). **Değiştirmeden** kullanılabilir
  (K-2, §5). Değer biçimi: `code` / `label` + isteğe bağlı `attributes["label_<dil>"]` — dil etiketi varsa o, yoksa `label` (kuralın ölçüldüğü yer
  `frontend/Diten.Web/Models/CRM/AccountViewModels.cs:332-356` `PublishedValueItemModel.TextFor`). Warehouse kendi küçük DTO'sunu yazar; CRM model
  sınıfını kullanmaz (Q-9 emsali: modüller arası kod paylaşımı yok).
- **İlk ölçüm (rapora):** kiracıda üç kümenin yayımlı değer sayısı; değerlerde 7 dil `label_*` var mı (yoksa paket Q-11 önerisi: `label`'a düşülür,
  kopyaya o ad yazılır); ülke kodlarının biçimi (küçük / büyük harf — donmuş `Address.country` küçük harf kod ister, ASSUMPTION-FU01-03); `city` /
  `district` kodları ülkeler arasında tekil mi. Ölçülemeyen her madde "ölçülmedi + neden" ile yazılır.

## 5. Dokunulacak dosyalar ve XMC-001

`WEB` = `frontend/Diten.Web/`, `WEBT` = `frontend/Diten.Web.Tests/`.

**FU01'in kendi dosyaları (paket §5 DL-3, §11):**
- `WEB/Controllers/OutboundShipmentsController.cs` — A-1, A-3, A-5 proxy'leri, A-7 / A-8 hata anahtarı; B-1, B-4.
- `WEB/Models/Warehouse/OutboundDtos.cs` — A-1 (adres üyeleri, görünüm üyeleri, form görünüm modeli), Reference Data değer DTO'su.
- `WEB/Views/Warehouse/OutboundShipments/_Form.cshtml` (izin koşullu çizim), `Details.cshtml` (rozet, ilçe satırı). `Create.cshtml` / `Edit.cshtml`
  yalnız gerekirse (Layout satırı değişmez).
- `WEB/wwwroot/assets/js/Warehouse/OutboundShipments/form.js`, `rules.js`, `details.js` (yalnız özet kartı + "sevke hazır" başarı dalı), `index.l10n.js`.
- `WEB/Resources/Views/Warehouse/OutboundShipments/OutboundShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx`.
- `WEB/Resources/PageHelpResource.{7}.resx` — yalnız `Help.OUTBOUNDSHIPMENTS.*` (A-10, B-5).
- Testler: `WEBT/Warehouse/**` (yeni ve `OutboundCustomerRefBodyTests.cs` eki), `WEB/tests/**` (vitest).

**MOD-0178 (ebeveyn) ortak dosyaları — paket §5 DL-3'te adı yok; CT kararı K-1, yalnız ekleme:**
- `WEB/Models/Warehouse/WarehouseProxy.cs` — outbound'a özel `WMS_CUSTOMER_*` listesi + anahtar seçici (`S2Codes` değişmez).
- `WEB/Models/Warehouse/WarehouseVocabulary.cs` — `OutboundPageWords`'e yeni kelimeler.
- `WEBT/Warehouse/WarehouseS2ScreensTests.cs` — yalnız `OutboundShipmentsIndex` ailesi için üç kodun 7 dil paritesi (mevcut vakalar değişmez).
- PickTasks / Packing resx'lerine **dokunulmaz**.

**XMC-001 — başka modül dosyaları:**

| # | Dosya | Bu dilimde | Sahip / onay |
|---|---|---|---|
| X-1 | `WEB/Services/CrmReferenceSetReader.cs` | **değiştirilmeden kullanılır** (K-2). Değişiklik gerekirse dur | CRM Web (Beste — CRM) — **sahip onayı gerekli** (değişirse) |
| X-2 | `WEB/Models/CRM/AccountViewModels.cs` (`PublishedValueItemModel`) | kullanılmaz, değişmez (yalnız kural okundu) | CRM — dokunulmaz |
| X-3 | `docs/analysis/contracts/crm-account-lookup.md` §5a (XMC-CRM-01), taslak 1.1 | yazılmaz | Beste — CRM; **sahip onayı gerekli** (CRM sahibi yazar) |
| X-4 | `services/Diten.Platform/src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs` (`crm.account-ship-to.read`, MOD-0149 DL-3b) | yazılmaz | Beste — CRM; **sahip onayı gerekli** |
| X-5 | `services/Diten.AuthService/**` rol şablonları (Q-5) | yazılmaz | Ali — **sahip onayı gerekli** |
| X-6 | `WEB/Controllers/SupplyChainShipmentsController.cs`, `WEB/Views/SupplyChain/Shipments/_Form.cshtml` (MVP-6 emsali) | yalnız okunur | Natig — dokunulmaz |
| X-7 | `services/Diten.Platform/**/BusinessReferenceDataConsumableSetsOptions.cs` | değişiklik gerekmez (üç küme listede) | Platform — dokunulmaz |

**Dokunulmayacaklar:** paket dosyası; WMS sözleşmeleri; `services/**` (InventoryService dahil); `gateway/**` ve `ocelot.json`; `.antigravity/**`;
`_Layout.cshtml`, Archive; `details.js` onay akışları (`:171-245`, FIX-01); `SharedResource.*.resx` (yeni sayfa / menü / izin yok → `Nav.Module`,
`Nav.Page`, `Perm.Entity` eklenmez; `NavManifestL10nGuardTests` sayıları önce = sonra); bu WP dosyası.

## 6. Uygulama sırası (öneri)

- **A.** Adım 0 → §4 → taban koşuları.
- **B.** A-1 (DTO / proxy tamamlama) + gövde testleri.
- **C.** A-5 ülke seçicisi + Reference Data okuması (§4.3 ilk ölçümü raporla).
- **D.** A-2 çizim kapısı + A-3 `customers` proxy + A-4 seçim / kaldırma.
- **E.** A-6 Details; A-7 / A-8 hata yolu ve metinleri; A-9 / A-10 resx ve yardım (7 dil aynı commit'te — anahtar gelip dil gelmezse Web.Tests kırmızı).
- **F.** A testleri, A sabotajları, tam koşular, A canlı (AC-31 (b), (c)), **ara teslim raporu (A)**.
- **G. (G-1 sonrası)** B-1…B-6, B sabotajları, tam koşular, canlı (a), **son teslim raporu (B)**.

## 7. Kabul ölçütleri ve sabotajlar

Her vaka paketteki metinle ölçülür (paket §16 — aç). Her sabotaj **üretim koduna** uygulanır, kırmızı çıktı alınır, bayt-eşit geri alınır, commit'e
girmez (Çekirdek md. 10).

### 7.1 A parçası

| AC | A'daki vakalar | Sabotajlar |
|---|---|---|
| AC-25 | aynı-origin `customers`; tarayıcıdan 5000 / 5061'e istek yok; U-3 kullanıcı token'ı + `billableOnly=true`; genel CRM uçları ve `crm.account.read` yok; en az 2 karakter; günlük vakası `customers` için (200 / 503) | S-FU01-25, S-FU01-25b |
| AC-26 (A payı) | U-3'ten ad ön doldurma; Q-3 ad vakası (ad düzenlenir, `customerRef` gövdede kalır); ülke kod + `countryName`; eski serbest metin ülke vakası; dört adres üyesi gövdede (A-1) | S-FU01-26d (ad alanı), S-FU01-26g |
| AC-27 | "Bağlantıyı kaldır" → gövdede `customerRef` / `customerAddressId` yok, alanlar korunur | S-FU01-27 |
| AC-28 (A payı) | seçici hatasında "CRM araması yapılamadı", elle alanlar açık, elle kayıt; çizim vakaları (a) anahtarsız → seçici yok, `customers` isteği yok; (b) yalnız `lookup-validation` → seçici var, adres seçici ve "CRM'den yeniden al" yok, gövdede `customerRef` var `customerAddressId` yok; (d) anahtarsız doğrudan `customers` → 403, CRM çağrısı yok | S-FU01-28, S-FU01-28b |
| AC-29 (A payı) | kayıtta 503 → uyarı + "Bağlantıyı kaldırıp elle kaydet", yeni istek anahtarı; 422 iki kod kendi metniyle | S-FU01-29 |
| AC-30 | rozet; "Türkiye (tr)"; "Kadıköy"; eski outbound'da `country` aynen, ilçe "-"; kod üyeleri görünmez | S-FU01-30, S-FU01-30b |
| AC-31 (b), (c) | canlı: elle girilerek ve CRM kapalıyken elle girilerek outbound; CRM kapalıyken `customerRef`'li kayıt 503 (negatif kanıt) — `customerRef` yönetici rolüyle seçiciden ya da API'den; 375 px, tr / en / ar | — (canlı kanıt) |
| CT ek (bu WP) | üç `Warehouse.Error.WMS_CUSTOMER_*` anahtarı `OutboundShipmentsIndex` ailesinin 7 dilinde | **S-FU01-DL3-L10N-WP:** `OutboundShipmentsIndex.ar.resx`'ten `Warehouse.Error.WMS_CUSTOMER_NOT_ACTIVE`'i sil → parite testi kırmızı |

**A sabotaj listesi:** S-FU01-25, S-FU01-25b, S-FU01-26d, S-FU01-26g, S-FU01-27, S-FU01-28, S-FU01-28b, S-FU01-29, S-FU01-30, S-FU01-30b (**10 paket**) +
S-FU01-DL3-L10N-WP (**1 CT ek**) = **11**.

### 7.2 B parçası (G-1 sonrası)

| AC | B'deki vakalar | Sabotajlar |
|---|---|---|
| AC-25 (B payı) | günlük vakası `customer-ship-to` için (200 / 404 / 503) | S-FU01-25c |
| AC-26 (B payı) | U-4'ten kod → ad; `cityRef` / `districtRef`; posta kodu (Q-10); iki öğe (varsayılan ikinci) / tek öğe (`"primary"`, posta kodu boş) / boş liste fikstürleri; Q-3 Adres 1 vakası | S-FU01-26a, S-FU01-26b, S-FU01-26e, S-FU01-26f (+ S-FU01-26d Adres 1 ile yeniden) |
| AC-32 | 404 / 400 / 401 / 403 / 5xx / 503 / zaman aşımı eşlemesi; "Ad okunamadı" | S-FU01-26c |
| AC-28 (B payı) | (c) iki anahtarlı kullanıcıda hepsi çizili; (d) anahtarsız doğrudan `customer-ship-to` → 403, CRM çağrısı yok | S-FU01-28c |
| AC-29 (B payı) | Q-12 vakası (`suspended`, `inactive`, `archived` → 2xx + `customerWarning`; `active` → yok; U-4 403 / 404 / 503 / zaman aşımı → 2xx, uyarı yok; InventoryService hatası → U-4 çağrılmaz); gösterim vakası (F-6); anahtar vakası | S-FU01-29b, S-FU01-29c, S-FU01-29d, S-FU01-29e |
| AC-31 (a) | canlı: CRM'den seçilmiş outbound (B-6) | — (canlı kanıt) |

**B sabotaj listesi:** S-FU01-25c, S-FU01-26a, S-FU01-26b, S-FU01-26c, S-FU01-26e, S-FU01-26f, S-FU01-28c, S-FU01-29b, S-FU01-29c, S-FU01-29d, S-FU01-29e
(**11 paket**). A + B = paket §24 DL-3'ün **21** sabotajının tamamı + 1 CT ek.

**Beklenen kırmızı test adları:** pakette yok — geliştirici adlandırır, raporda her `S-n` ile eşler. S-FU01-28b / S-FU01-28c `WEBT` görünüm / proxy
testinde (emsal `WarehouseS2ScreensTests`), vitest vakaları gerçek `form.js` / `rules.js` / `details.js`'e karşı (jsdom + fetch sahtesi).

## 8. Test koşuları (tam koşu filtresiz)

- **InventoryService, filtre yok:** `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests`. Servis değişmez; koşu hattın
  sağlığı için (CT kuralı). Taban `<dilim-başı>`'nda aynı gün; sonda yeni kırmızı yok. Yük duyarlı süre testleri
  (`BarcodeScanTests.Two_hundred_consecutive_scans_stay_under_the_p95_bound`, `CancelReleaseTests.AC32_…`) kırmızıysa tek başına iki kez koş, yaz.
- **Web.Tests, filtre yok:** `dotnet test frontend/Diten.Web.Tests`. Taban yeniden ölçülür (son CT ölçümü 4554 / 3 kırmızı, üçü main'de de kırmızı).
  `NavManifestL10nGuardTests` yeşil ve sayıları değişmemiş; `WarehouseLocalizerPlaceholderGuardTests` yeşil (yeni `{0}`'lı anahtarlar `.Value` ile).
- **Gateway, filtre yok:** `dotnet test gateway/Diten.ApiGateway.Tests` — `Mod0178WarehouseRouteTests` sayısı önce = sonra.
- **vitest — yalnız repodaki `node_modules`** (`frontend/Diten.Web/node_modules`; `npx`'in npm'den indirmesine izin verme — o koşu sayılmaz):
  `node_modules/.bin/vitest run` dokunulan test dosyaları + `diten-field-icons`, `diten-enter-primary`, `warehouse-s2-e3-fix-01`. Ayrık worktree'de
  `node_modules` için kayıtlı junction yöntemi. Windows satır sonu / yol kırmızıları tabanla karşılaştırılır.
- **Araçlar:** `resx-check` (kiracı 7 dil) — sayfa resx'leri için parite testi; `verify_datatable_page.py` yalnız Index / liste dosyalarına
  dokunulursa (plan: dokunulmaz).
- **Bekçi kanıtı:** `git diff --stat <dilim-başı>..<dilim-sonu> -- services/ gateway/ docs/analysis/contracts/` **boş**.
- **Build:** `Diten.Web` PASS (InventoryService değişmediği için build taban koşusuyla aynı).

**Ortam:**
- Port **27017 kullanma**. **`INVENTORY_TEST_MONGO` boş bırak** — takım kendi replica set'ini başlatır.
- **`Diten.AuthService.AccountKindAcceptanceHost.Tests` koşma.**
- Portlar yalnız AGENTS.md §3'ten (Web 5001, Gateway 5000, Inventory 5067, CRM 5061); port uydurma. Web servise doğrudan değil, Gateway üzerinden.

## 9. Git kuralları (GIT-002)

- Çalışma dalı **`feature/sce/mod-0173-inventory-ledger-valuation`** — yeni dal açılmaz; `main`'e commit / push yok.
- Stage yalnız **açık dosya yoluyla** (`git add <yol> …`); `git add -A` / `git add .` yok. İzlenmeyen `__pycache__/`, review ham dosyaları ve başka
  işlerin commit'lenmemiş değişiklikleri stage'e girmez.
- Hook atlama yok (`--no-verify` yok, `core.hooksPath` değiştirilmez — geçici commit'te de), imza atlama yok; yıkıcı git komutu onaysız yok.
- Yerel commit'ler serbest; resx anahtarları onları okuyan görünüm / JS ile aynı commit'te. A ve B ayrı commit grupları.
- **Push / PR yok.** Commit mesajı sonu: `Co-Authored-By` satırı oturumun atıf kuralıyla.

## 10. Teslim raporu biçimi (A için ara rapor, B için son rapor)

1. Commit listesi (`git log --oneline <dilim-başı>..HEAD`), `git diff --stat`, Adım 0'da hangi yol (ata / ff / no-ff).
2. Test sayıları önce / sonra: Inventory, Web.Tests, Gateway (toplam / geçen / kalan / atlanan; kırmızılar adıyla), vitest.
3. AC tablosu: her AC (A ya da B payı) için test adı ve sonuç.
4. Sabotaj tablosu: her `S-n` için üretim dosyası:satır, değişiklik, kırmızı test adı + çıktı özeti, geri alma, geri yeşil.
5. §4.2 eylem / rota adları; §4.3 Reference Data ilk ölçümü (değer sayıları, 7 dil etiketi, kod biçimi, tekillik).
6. Sayılar önce → sonra: `Mod0178WarehouseRouteTests` sayısı, `NavVisiblePageKeyFloor` / `PagelessUntil` (değişmedi).
7. Canlı: AC-31 (b), (c) [A]; (a) [B] — rol (yönetici / sevkiyat), 375 px, tr / en / ar, ekran görüntüsü yolları.
8. B için: donmuş 1.1 metni ile taslak r7 arasındaki fark ve koda etkisi.
9. XMC-001: X-1…X-7 için "dokunulmadı" ya da onay kaydı; bekçi boş diff.
10. Açık kalanlar: ölçülemeyenler (nedeniyle), G-2 / G-3 durumu.

## 11. Belirsizlikler ve CT kararları

- **K-1 (CT kararı):** üç `WMS_CUSTOMER_*` kodu `S2Codes`'a eklenmez; outbound'a özel liste + anahtar seçici, parite yalnız `OutboundShipmentsIndex`
  ailesinde (§2.1 A-8). Gerekçe: `S2Codes` üç aileye (OutboundShipments, PickTasks, Packing) 7 dil anahtarı zorunlu kılar; PickTasks / Packing bu
  kodlarla karşılaşmaz ve paket kapsamında değildir. `WarehouseProxy.cs`, `WarehouseVocabulary.cs`, `WarehouseS2ScreensTests.cs` MOD-0178'in kendi
  dosyalarıdır; paket §5 DL-3 listesinde adları olmadığı için burada adıyla izin verilir (yalnız ekleme).
- **K-2 (CT kararı):** `CrmReferenceSetReader` değiştirilmeden kullanılır; değer DTO'su Warehouse'ta yazılır (§4.3). Okuyucunun değişmesi gerekirse X-1.
- **B-1 (açık — Beste):** U-4'e dayanan B parçası 1.1 **taslağına** (r7) karşı G-1'den önce kodlansın mı? CT önerisi: **hayır** — paket §1 madde 3 ve §24
  DL-3 1.1 donmasını başlama kapısı sayar, sahip kaydı (`a974b20d5`) yalnız Q-5 / XMC-CRM-01 / Q-6'yı adlandırır; CRM'de U-4 ucu yokken çağrı
  Gateway jokerinden CRM'e gidip 404 döner ve paket 404'ü "kayıt bulunamadı, bağlantı kurulmaz" diye eşler. (Bugün `crm.account-ship-to.read` hiçbir
  manifestte olmadığı için çizim kapısı çağrıyı zaten gizler; risk düşük ama sıfır değil — tüm izinleri taşıyan bir rol varsa.) Sahip "evet" derse
  B, taslağa karşı fikstürlerle yazılır, donmada yeniden doğrulanır; karar kayda geçer.
- **B-2 (bilgi):** MOD-0149 §32 başlığındaki "ONAY BEKLİYOR — kod izni vermez" notu bayat görünüyor (onay satırı `approvals.tsv:85` var, sha eşleşiyor).
  CRM paketinin işi; bu dilimi etkilemez.
- **B-3 (ölçülecek):** taslak r7 §4, birden çok `isDefault = true` ya da listede olmayan `defaultAddressId`'yi "geçersiz gövde" sayar; paket §8.4 "okunamayan
  gövde → CRM adresi okunamadı" der. B'de bu iki durum "okunamadı" yoluna eşlenir (paketle tutarlı); donmuş metin farklı derse CT'ye yazılır.

## 12. Kural haritası (AGENTS.md §6.1)

- **Daima:** `multi-tenancy`, `security-jwt`, `git-safety`, `code-style`, `docs-organization`, `cross-module-change-consent` (XMC-001).
- **Frontend:** `frontend-datatable-template` (Compact eşliği), `frontend-form-template` (select2, bağımlı alan), `unauthorized-surface-standard`
  (UAS-001 — çizilmeyen kontrol), `page-help-and-keyboard` (UXH-001), `_LayoutTenantShell`, aynı-origin proxy deseni.
- **Metin:** `localization-standard` (kiracı 7 dil, RTL), `dynamic-localization-standard` (`window.L10n` köprüsü).
- **Yetki:** `permission-key-standard` (yeni anahtar yok; CRM anahtarları CRM'in).
- **Kayıt:** `logging-observability` (CRM'den gelen değer günlüğe yazılmaz — paket §2), `status-reporting-and-evidence` (REP-001), `dev-runbook`.

## Ek — B-1 kararı: B parçası taslağa göre (CT, 2026-11-14)

Sahip (Beste): B parçası G-1'i (CRM 1.1 donması) beklemeden **taslak r7'ye göre** kodlanır; A ve B aynı dilimde. Kayıt:
`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md` "Ek — MOD-0049-FU01 DL-2 ve MOD-0178-FU01 DL-3 kapıları".
- B testleri CRM ikiziyle, taslağın §'lerindeki şemaya karşı yazılır; taslak sürümü (r7) ve sha'sı testte sabit olarak anılır.
- U-4 ucu CRM'de yokken 404 → paketin "kayıt bulunamadı, bağlantı kurulmaz" yolu (kod bunu zaten karşılar); bu durum raporda "canlı ölçülemedi" diye yazılır.
- G-1 kabul kapısıdır: donma geldiğinde fark varsa ek adım; canlı AC-31 (a) o zaman.
- Adım 0 etiketi: `ct/line-ff-2026-11-14e`.
