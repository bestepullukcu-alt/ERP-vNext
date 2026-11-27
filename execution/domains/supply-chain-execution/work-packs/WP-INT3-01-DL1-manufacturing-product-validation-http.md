# WP-INT3-01-DL1 — MOD-0193: ürün doğrulaması Permissive → Http (yalnız `exists`, MOD-0290 `validate`)

## Künye

| Alan | Değer |
|---|---|
| Paket | `execution/domains/supply-chain-execution/module-packs/MOD-0193-bom-routings.md`, `status: done` (G2B koşullu kabul, Ali 2026-10-13) |
| Paket LF sha256 | `b8641e8cf7cfd69f1f51df7ef49d67a54de46cfe18eb2fcdb93217640ca27e20` (HEAD `103c6b2fe`) |
| Kimlik | Yeni MOD/FU yok. `verify_module_id.py . --check-id MOD-0193 --name "BOM & Routings"` → `OK`, exit 0 (2026-11-25) |
| Paketin kapsadığı yer | §7 MOD-0290 satırı: seam `IProductReferenceValidator`, varsayılan `Permissive`, hedef `ProductMaster:Mode=Http` ile `POST /validate`. §12 / §13: bilinmeyen ürün → 422 `UNKNOWN_ITEM`. §20 F-0193-07: "Gerçek `Mode=Http` entegrasyonu … (bugün permissive)". §5 Repo Scope: `services/Diten.ManufacturingService/**`, `Resources/Views/Manufacturing/Boms/**` |
| Feragat | W-G2B-0290 (`docs/records/decisions/2026-10/mvp3-g2b-bom-structure-gate-owner-decision-01.md:23-43`). Bu dilim feragati **kapatmaz**: kapanış koşulu Http + testler + UoM (F-0193-03) ve aktiflik kararıdır (DL-2) |
| Sahip kararları | (1) Beste 2026-11-24: `docs/records/decisions/2026-11/mvp3-int3-owner-decisions-01.md` (LF sha256 `c9a6baad5a11d7a95a18300104869f85c0955fbeed52a501ed11637d5b118861`, commit `d4377ac56`), satır 6-9: "dar iş paketi (200'lük parçalama, 400 / 503 ayrımı, …)". (2) Ali 2026-11-24, aynı dosya satır 19-26: kimlik = Auth servis token'ı; iş paketi v1.1 dondurma + servis kimliği işini bekler. (3) **Beste 2026-11-25 (CT'ye sözlü; henüz dosyada yok):** INT3-01 şimdi, aktif/pasif kontrolü olmadan, yalnız `exists`; kimlik şimdilik kullanıcı token'ı iletimi; servis token'ı Ali'nin işinden sonra |
| Kapsam | Yalnız DL-1: Http modu (dev), 200'lük parçalama, MDM yanıtlarının ayrı eşlenmesi, kimlik seam'i, testler, sabotajlar, canlı kanıt |
| CT doğrulayıcı | MVP-3 CT (sahip Beste) |
| Durum | **draft.** Başlama koşulları: (a) karar (3) karar dosyasına ek olarak yazılır (CT); (b) K-2…K-4 varsayılanları CT/Beste tarafından kabul edilir; (c) Ali'ye S-A1 iletilir. Ali'nin cevabı beklenmez; Beste'nin kararıyla başlanır, Ali "hayır" derse DL-1 yalnız dev ortamında kalır (appsettings.json değişmez) |

## Bağlam (ölçülen, 2026-11-25, HEAD `103c6b2fe`)

Kök: `services/Diten.ManufacturingService/src/`.

- `Diten.ManufacturingService.Infrastructure/ProductMaster/HttpProductReferenceValidator.cs`:
  - `:15` iletilen başlıklar `Authorization, X-Tenant-Id, X-Legal-Entity-Id, X-Correlation-Id` (gelen istekten kopya).
  - `:24-26` **tek istek**: bütün kimlikler bir `refs` dizisinde, parçalama yok.
  - `:28-35` başlıklar `HttpContext`'ten okunur.
  - `:40` `EnsureSuccessStatusCode()`; `:46-48` her `HttpRequestException` / `TaskCanceledException` / `InvalidOperationException` /
    `JsonException` → `BomPersistenceUnavailableException`. Sonuç: MDM 400 de, 403 de, zaman aşımı da 503 **`PERSISTENCE_UNAVAILABLE`**
    ("The change could not be saved") olur (`Application/Behaviors/ExceptionHandlingBehavior.cs:28-33`). Çağıranın iptali de 503'e döner.
  - `:43-44` sonuçta `exists: true` olmayan **ya da sonuçta hiç geçmeyen** kimlik "bilinmeyen" sayılır → eksik yanıt 422'ye dönüşebilir.
  - `:52-54` yalnız `itemId` + `exists` okunur.
- `Diten.ManufacturingService.Infrastructure/DependencyInjection.cs:30-49`: `ProductMaster:Mode` yoksa `Permissive`; `Http` → typed client
  (`BaseAddress = BaseUrl + "/"`, `Timeout = 5 s`); `BaseUrl` yoksa başlangıç hatası; başka değer → başlangıç hatası.
- `Diten.ManufacturingService.Api/appsettings.json:27-30`: `Mode: Permissive`, `BaseUrl: http://localhost:5000/api/product-master` (Gateway).
  `appsettings.Development.json` (34 satır) `ProductMaster` bölümü taşımıyor.
- `Diten.ManufacturingService.Application/Features/Boms/BomDraftSupport.cs:38-51`: kendine referans önce (`SELF_REFERENCE`), sonra
  ana ürün + bileşen + alternatifler tek kümede doğrulanır (`UNKNOWN_ITEM`). Çağıranlar yalnız `CreateBomDraftHandler.cs:29` ve
  `UpdateBomDraftHandler.cs:42`; **release doğrulamaz** (DL-2 notu).
- `Diten.ManufacturingService.Application/Common/BomErrorCodes.cs:11-12` `UNKNOWN_ITEM`, `SELF_REFERENCE`; `:20` `DEPENDENCY_UNAVAILABLE`,
  mesajı `:36` tüzel kişiye özel ("The legal entity could not be checked …").
- Web: `frontend/Diten.Web/Controllers/ManufacturingBomsController.cs:306-329` `DEPENDENCY_UNAVAILABLE` → `ErrDependencyUnavailable`
  (tr: "Tüzel kişi şu an doğrulanamadı …", `Resources/Views/Manufacturing/Boms/BomsIndex.tr.resx:88`); bilinmeyen kod + 5xx → `ErrPersistence`.
- Sözleşmeler:
  - `docs/analysis/contracts/product-master-bundle.v1.0.openapi.yaml` (FROZEN 1.0.0) `:104-116` `POST /validate`, `:191-203`
    `ValidateResponse.results[{itemId, skuId, skuLevel, exists}]`. v1.0'da 400 ve `maxItems` yok.
  - `product-master-bundle.openapi.yaml` `1.1.0-draft.16` (dondurulmadı): `refs` en çok 200, `/validate` 400, `lifecycleStatus`.
  - `docs/analysis/contracts/bom.openapi.yaml` 1.2.0 FROZEN: `createBomDraft` 400 / 422 `UNKNOWN_ITEM | SELF_REFERENCE`; `:13-15`
    "MDM'e ulaşılamaz → 503 `DEPENDENCY_UNAVAILABLE`". **Bu WP bu sözleşmeyi değiştirmez.**
- MDM (çalışan kod v1.1'i uygular): `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductMasterController.cs:21-24`
  `[Authorize]` + `[HasPermission("mdm.product-master.read")]`, `:76-80` `validate`, gövde > 64 KB → 413. İşleyici
  `Application/Features/ProductMaster/Handlers/ProductMasterQueryHandler.cs:353-398`: `refs` 0 ya da > 200 → 400 `BATCH_LIMIT_EXCEEDED`;
  biçim → 400 `INVALID_REF`; kiracı yok → 400 `TENANT_CONTEXT_REQUIRED` (`:520`); depo erişilemez → 503 `PRODUCT_MASTER_UNAVAILABLE`.
  Sonuç kimlikleri ürün kapsamına (tüzel kişi rollout'u) göre `exists` verir.
- Gateway: `/api/product-master/{everything}` → 5059, `GET, POST` (`gateway/Diten.ApiGateway/ocelot.json:465-473`). **ocelot değişmez.**
- Testler: `services/Diten.ManufacturingService/tests/Diten.ManufacturingService.Tests/Api/BomApiFactory.cs:51` `Mode=Permissive`,
  `:68` `IProductReferenceValidator` sahte ile değiştirilir → **Http istemcisinin bugün hiçbir testi yok** (G2B ölçümüyle aynı).
  `UnreachableMdmFactory` (`:96-109`) gerçek istemciyi koruyan alt sınıf deseninin örneğidir.
- İzin kaynağı: `mdm.product-master.read` genel okuma tabanından **gelmez**; ürün modülünün yetkilendirmesiyle gelir
  (`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs:164-167`).

## Davranış sözleşmesi (DL-1 hedefi)

MDM'nin cevabı → `GetUnknownItemIdsAsync` → BOM yanıtı. "Hiçbir şey yazılmaz" her ret satırında geçerlidir.

| MDM cevabı (herhangi bir parça) | istemci | BOM yanıtı |
|---|---|---|
| 200, her gönderilen `itemId` sonuçta, `exists` bool | bilinmeyenleri döner | hepsi var → 201 / 200; biri yok → 422 `UNKNOWN_ITEM` |
| 200 ama gövde bozuk, `results` yok, sayı farklı ya da gönderilen bir kimlik sonuçta yok | `ProductMasterUnavailableException` | 503 `DEPENDENCY_UNAVAILABLE` (K-2) |
| 400, 404, 409, 413, 415 ve öteki 4xx (401 / 403 hariç) | `ProductMasterRequestRejectedException(status, mdmCode)` | **503 değil:** 500 `INTERNAL_ERROR` + ayrı hata günlüğü (K-3) |
| 401, 403 | `ProductMasterUnavailableException` (neden `auth`) | 503 `DEPENDENCY_UNAVAILABLE` (K-4) |
| 5xx, bağlantı hatası, istek zaman aşımı (5 s) | `ProductMasterUnavailableException` | 503 `DEPENDENCY_UNAVAILABLE` |
| çağıranın kendi iptali | `OperationCanceledException` yayılır | 503'e çevrilmez |

Parçalar sırayla gönderilir; bir parça başarısızsa kalanlar gönderilmez ve kısmi sonuç dönmez (fail-closed).

### Kararlar (varsayılan; CT/Beste onaylar)

- **K-1 parçalama:** kimlikler tekilleştirilir, en çok **200**'lük parçalar (`MaxRefsPerRequest = 200`, MDM `MaximumBatch` ile aynı;
  sabit kodda, yapılandırmada değil). Üst sınır: 500 satır × (1 + 10 alternatif) + ana ürün = 5 501 kimlik → 28 istek. Tekrar deneme yok
  (LE doğrulayıcısının tek tekrarından bilinçli fark; DL-1'de basit kalır).
- **K-2 erişilemezlik kodu:** 503 **`DEPENDENCY_UNAVAILABLE`** (bom 1.2.0 `:13-15` MDM erişilemezliği için bu kodu adlandırır;
  `PERSISTENCE_UNAVAILABLE` yanlış söyler: "kaydedilemedi"). Mesaj genelleşir: servis `BomErrorCodes.cs:36` → "Master data (legal entity or
  product) could not be checked; nothing was read or written."; Web `ErrDependencyUnavailable` 7 dilde aynı anlam (tr: "Ana veri (tüzel kişi
  ya da ürün) şu an doğrulanamadı; hiçbir şey kaydedilmedi. Tekrar deneyin."). Alternatif: `PERSISTENCE_UNAVAILABLE`'ı korumak (Web değişmez,
  kod yanlış ad taşır).
- **K-3 MDM 4xx:** bizim istemcimiz ≤ 200 geçerli kimlik gönderdiği için MDM'nin 400'ü istemci/sözleşme kusurudur, kullanıcının hatası ya da
  kesinti değildir. Varsayılan: 500 `INTERNAL_ERROR` (sözleşmeye dokunmaz; Web `ErrPersistence` metnini gösterir), günlükte
  `PRODUCT_MASTER_REQUEST_REJECTED status={} mdmCode={} correlationId={}` (Error). Alternatif: yeni kod (ör. `PRODUCT_MASTER_REJECTED`) →
  `bom.openapi.yaml` additive 1.3.0, sahip onayı + 7 dil.
- **K-4 401/403:** LE doğrulayıcısının deseni (`MdmLegalEntityReferenceValidator.cs:17-20`): yetki reddi "soramadık" sayılır → 503. Günlükte
  ayrı neden (`PRODUCT_MASTER_AUTH_REJECTED status={}`, Warning). Servis token'ına geçilince (DL-3) 403 yapılandırma hatası olur ve yeniden
  değerlendirilir.

## Tasarım

1. **Kimlik seam'i** `Infrastructure/ProductMaster/IProductMasterCallerCredentials.cs`:
   `ValueTask<AuthenticationHeaderValue?> GetAuthorizationAsync(CancellationToken ct)`. Bugünkü uygulama
   `ForwardedUserTokenCredentials` (gelen isteğin `Authorization` başlığı, `AuthenticationHeaderValue.TryParse`). `null` → başlık gönderilmez
   (MDM 401 → 503, fail-closed). DI: `Mode=Http` dalında scoped kayıt. DL-3'te yalnız bu uygulama değişir; doğrulayıcı değişmez.
2. **Doğrulayıcı** `HttpProductReferenceValidator`:
   - `Authorization` artık `HttpContext`'ten değil seam'den gelir (`ForwardedHeaders`'tan çıkar).
   - `X-Tenant-Id` `ITenantContext`'ten (çözümlenmişse; JWT esas), `X-Legal-Entity-Id` gelen istekten (middleware doğruladı),
     `X-Correlation-Id` `ICorrelationContext`'ten — LE doğrulayıcısıyla aynı kaynaklar.
   - Parçalama (K-1), yanıt denetimi (her gönderilen `itemId` sonuçta tam bir kez; sayı = parça boyu), eşleme tablosu, iptal ayrımı
     (`catch … when (!ct.IsCancellationRequested)`).
3. **İstisnalar** `Application/Common/`: `ProductMasterUnavailableException`, `ProductMasterRequestRejectedException(int Status, string? MdmCode)`.
   `ExceptionHandlingBehavior` iki ayrı `catch` alır (K-2 → 503 `DEPENDENCY_UNAVAILABLE`, K-3 → 500 `INTERNAL_ERROR` + yapılı günlük).
   `BomPersistenceUnavailableException` yalnız depo için kalır.
4. **Yapılandırma:** `Api/appsettings.Development.json` += `"ProductMaster": { "Mode": "Http" }` (BaseUrl `appsettings.json`'dan).
   `appsettings.json` **`Permissive` kalır**. `Testing` ortamı `BomApiFactory` ayarıyla `Permissive` kalır.
5. **Permissive değişmez:** `PermissiveProductReferenceValidator` ve `Mode` yok → `Permissive` kuralı aynen.
6. **Yorumlar:** `IProductReferenceValidator.cs:3-18` ve `DependencyInjection.cs:30` "validate canlı değil" ifadeleri güncel duruma çekilir
   (Http = dev'de açık, aktiflik DL-2'de).

## Testler

Kök: `services/Diten.ManufacturingService/tests/Diten.ManufacturingService.Tests/`.

**Birim** — `ProductMaster/HttpProductReferenceValidatorTests.cs` (Mongo gerekmez). Sahte `HttpMessageHandler` her isteği (yol, gövde,
başlıklar) kaydeder ve senaryoya göre yanıt verir; `HttpClient.BaseAddress = http://pm.test/api/product-master/`.

| # | senaryo | beklenen |
|---|---|---|
| U1 | 0 kimlik | istek yok, `[]` |
| U2 | 1 kimlik, `exists: true` | `[]`; yol `/api/product-master/validate`, gövde `{"refs":[{"itemId":"…"}]}` |
| U3 | 200 / 201 / 5 501 kimlik (tekrarlı girdi dahil) | 1 / 2 / 28 istek, her biri ≤ 200; gönderilenlerin birleşimi = girdi kümesi, tekrar yok |
| U4 | ikinci parçada `exists: false` | o kimlik döner |
| U5 | 400 `BATCH_LIMIT_EXCEEDED`; ayrıca 404, 409, 413 | `ProductMasterRequestRejectedException` (status + mdmCode); Unavailable **değil** |
| U6 | 500, 502, 503, 504; bağlantı reddi; yanıt 5 s'yi aşar | `ProductMasterUnavailableException` |
| U7 | 401, 403 | `ProductMasterUnavailableException` |
| U8 | 200 + bozuk JSON / `results` yok / gönderilen kimlik sonuçta yok / fazla sonuç | `ProductMasterUnavailableException` (422'ye **dönmez**) |
| U9 | çağıran `ct`'yi iptal eder | `OperationCanceledException`, Unavailable değil |
| U10 | seam `Bearer X` verir, `HttpContext`'te başka token var | istekte `Bearer X`; seam `null` → `Authorization` yok |
| U11 | `ForwardedUserTokenCredentials` | gelen `Authorization`'ı döner; yoksa `null` |
| U12 | başlıklar | `X-Tenant-Id` = `ITenantContext`, `X-Legal-Entity-Id` iletilir, `X-Correlation-Id` = `ICorrelationContext` |

**API** — `BomApiFactory` deseni: `FakeLegalEntities` gibi `protected virtual bool FakeProducts => true;` eklenir (`:68` yalnız true iken
değiştirir). Yeni `HttpProductMasterFactory : BomApiFactory` → `FakeProducts = false`, `ProductMaster:Mode=Http`,
`ProductMaster:BaseUrl=http://pm.test/api/product-master`, typed client'ın birincil işleyicisi sahte işleyiciyle değiştirilir
(`AddHttpClient(<typed client adı>).ConfigurePrimaryHttpMessageHandler(...)`; ad ölçülür, her testte işleyici çağrı sayısı > 0 denetlenir —
çağrı 0 ise test kırmızı, böylece sahte işleyicinin devrede olmadığı fark edilir).

| # | senaryo | beklenen |
|---|---|---|
| A1 | create, bir bileşen `exists: false` | 422 `UNKNOWN_ITEM`; BOM satırı ve geçmiş satırı yok |
| A2 | create, hepsi var | 201 |
| A3 | update taslak, yeni bileşen `exists: false` | 422 `UNKNOWN_ITEM`, taslak değişmez |
| A4 | MDM 503 / bağlantı yok | 503 `DEPENDENCY_UNAVAILABLE`, hiçbir şey yazılmaz |
| A5 | MDM 400 | 500 `INTERNAL_ERROR` (503 değil, 422 değil), hiçbir şey yazılmaz |
| A6 | MDM 403 | 503 `DEPENDENCY_UNAVAILABLE` |
| A7 | kendine referans + bilinmeyen birlikte | 422 `SELF_REFERENCE`, MDM'ye istek yok (sıra korunur) |

**DI / yapılandırma** — `Mode` yok → Permissive; `Permissive` → Permissive; `Http` + BaseUrl → `HttpProductReferenceValidator` ve
`ForwardedUserTokenCredentials`; `Http` BaseUrl'siz → başlangıç hatası; `Bogus` → başlangıç hatası. Yapılandırma pini:
`appsettings.json` `Permissive`, `appsettings.Development.json` `Http` (yanlışlıkla çevrilmeye karşı).

**Mevcut takım:** `BomApiTests`, `LegalEntityScopeTests` vb. değişmeden yeşil (Permissive davranışı aynı). Web: K-2 seçilirse
`BomsL10nTests` + 7 resx anahtar paritesi.

### Sabotajlar (her biri tek değişiklik → kırmızı → bayt-eşit geri al → yeşil)

| # | değişiklik | kırmızı olması gereken |
|---|---|---|
| S1 | parça boyu 200 → 201 (ya da parçalamayı kaldır) | U3 |
| S2 | 4xx'i Unavailable'a eşle (eski `EnsureSuccessStatusCode` yolu) | U5, A5 |
| S3 | 5xx'i Rejected'a eşle | U6, A4 |
| S4 | yanıt denetimini kaldır (sonuçta olmayan kimlik = bilinmeyen) | U8 |
| S5 | `Authorization`'ı yine `HttpContext`'ten oku | U10 |
| S6 | `Http` dalında Permissive kaydet | A1, DI testi |
| S7 | `Mode` yokken varsayılanı `Http` yap | DI testi |
| S8 | `ProductMasterUnavailableException` → `PERSISTENCE_UNAVAILABLE` | A4 |
| S9 | iptal ayrımını kaldır (`when` koşulu) | U9 |

### Koşu kuralları

- Filtresiz Manufacturing takımı (`--filter` yok), `MANUFACTURING_TEST_MONGO` = geçici replica set; aynı gün taban (`origin/main`) ile karşılaştır,
  yeni kırmızı yok; taban kırmızıları adlarıyla. `tests/architecture` tabana göre yeni kırmızı yok. K-2 seçildiyse `frontend/Diten.Web.Tests`.
- Derleme / test / sabotaj, `dotnet watch` koşan checkout'ta değil, commit üzerinde ayrık (detached) bir worktree'de.

## Canlı kanıt (gerçek MDM, Gateway üzerinden)

Ortam: Auth, Platform, MDM 5059, Gateway 5000, Manufacturing 5070 `ASPNETCORE_ENVIRONMENT=Development` (→ `Mode=Http`), isteğe bağlı Web 5001.
Ön koşul: ürün modülü yetkilendirilmiş bir kiracı; o kiracıda ürün kapsamında gerçek bir ana ürün P ve bileşenler C1, C2 (MDM'den okunur,
uydurulmaz); rastgele uuid X.

| # | adım | beklenen |
|---|---|---|
| L1 | BOM kullanıcısıyla `POST /api/bom/versions` (P, bileşen X) | 422 `UNKNOWN_ITEM` (Permissive'de 201 olurdu — Http'nin açık olduğunun kanıtı) |
| L2 | aynı, bileşenler C1, C2 | 201; listede görünür |
| L3 | `mdm.product-master.read` taşımayan BOM kullanıcısı, L2 gövdesi | 503 `DEPENDENCY_UNAVAILABLE` (rol notunun kanıtı; K-4) |
| L4 | MDM durdurulur, L2 gövdesi | 503 `DEPENDENCY_UNAVAILABLE`; liste sayısı değişmez; MDM yeniden açılır |
| L5 | MDM günlüğü | L1/L2'nin `validate` çağrıları görünür (Gateway korelasyon kimliği) |
| L6 (Web açıksa) | ekranda L1 | `ErrUnknownItem` metni |

Kanıt yeri: `docs/records/audits/2026-11/mvp3-int3-01-dl1-01/` (`docs-organization`), token, parola ve bağlantı adresi yazılmaz.

## Rol notu

- Kullanıcı token'ı iletildiği sürece **BOM kullanıcısı** (GRN kullanıcısı değil — doğrulama çağrısını BOM create/update yapar) MDM'de
  `mdm.product-master.read` taşımalıdır; ayrıca bugünkü gibi `mdm.legal-entities.read` (paket §8). Anahtar ürün modülünün yetkilendirmesiyle
  gelir; kiracıda o modül yoksa rol yöneticisi anahtarı veremez ve BOM yazımı 503 alır.
- Bu, Ali'nin 2026-11-24 "BOM kullanıcılarına `mdm.product-master.read` gerekmez" sonucuna aykırı geçici bir durumdur; DL-3 (servis token'ı)
  ile kalkar. Canlı kanıtta hangi rolün kullanıldığı ve anahtarın nasıl verildiği yazılır.

## Sonraki dilimler (yalnız taslak not — bu WP'de yapılmaz)

- **DL-2 aktiflik (`ITEM_NOT_ACTIVE`):** PRODUCT-MASTER `1.1.0` donduktan sonra. `validate` sonucundaki `lifecycleStatus` ≠ `Active` → 422
  `ITEM_NOT_ACTIVE` (Beste 2026-11-24: taslak ya da pasif ürün BOM'a girmez). Gerekenler: `bom.openapi.yaml` additive (1.3.0, yeni kod),
  Web 7 dil `ErrItemNotActive`, açık enum kuralı (yalnız tam `Active` kabul), release'te yeniden denetim kararı (bugün release doğrulamaz),
  aktifliğini yitiren ürünü taşıyan mevcut taslakların davranışı.
- **DL-3 servis token'ı:** Ali'nin MDM servis kimliği işinden sonra `ForwardedUserTokenCredentials` → Auth servis token'ı uygulaması;
  MDM'nin `ProductMasterController`'ı servis kimliğini kabul etmeli (bugün `:18-19` "no service identity reads here"). Rol notu kalkar.
- **F-0193-03 UoM (`getSkuUom`)** ayrı kalır. W-G2B-0290 DL-2 + UoM sonrası kapanabilir.

## Yapılmayacaklar

- `bom.openapi.yaml`, `product-master-bundle*.yaml`, `services/Diten.MdmService/**`, `ocelot.json`, `.antigravity/**` değişmez (paket §6).
- `appsettings.json` varsayılanı `Permissive` kalır; aktiflik, servis token'ı, `getSkuUom` yok. Başka MVP'nin dosyasına dokunulmaz (XMC-001).
- Push, PR, `main`'e commit yok; reset / amend / rebase / force yok; stage yalnız açık yolla.

## Kabul (CT doğrulayıcı: MVP-3 CT)

- Davranış tablosunun her satırı U/A testiyle kanıtlı; S1–S9 kırmızı → geri alınca yeşil.
- Filtresiz Manufacturing takımı ve mimari testler tabana göre yeni kırmızı içermez; Permissive yolu değişmedi (mevcut testler aynı).
- L1–L5 canlı kanıtı kayıtlı (L1 422, L2 201, L3/L4 503).
- `git grep -n "BomPersistenceUnavailableException" -- services/Diten.ManufacturingService/src/Diten.ManufacturingService.Infrastructure/ProductMaster`
  boş.
- CT kapanışta paketin §22/§23'üne uygulama kaydı satırı ekler; F-0193-07 "DL-1 yapıldı (exists), DL-2/DL-3 açık" olarak güncellenir.

## Teslim raporu (biçim)

1. Dal, commit SHA'ları ve `git show --stat`.
2. K-1…K-4 için uygulanan seçenek.
3. U/A/DI test listesi ve sonuçları; sabotaj tablosu `| # | değişiklik | hata satırı | geri alındı (diff boş) |`.
4. Takım sayıları (toplam / geçen / kalan / atlanan), dal ve taban, aynı gün; taban kırmızıları adlarıyla.
5. Canlı kanıt L1–L6: istek/yanıt durum kodları ve hata kodları, kullanılan rol, kanıt dosya yolları.
6. Ölçülmeyenler ("ölçülmedi" diye) ve açık noktalar.
