# Doğrulanmış referans verisi (GSKU UOM + pazar) — yerel kurulum ve canlı operatör notu

> **Kaynak:** WP-VW-W4 · W4-INFRA (2026-10-14).
> **Kod:**
> - Platform: `Configuration/Verified*Options.cs`, `Services/BusinessReferenceData/*Runner.cs`, `Development*Eligibility.cs`, `Security/VerifiedGskuResolverCredentialAuthenticator.cs`.
> - MDM: `Infrastructure/ReferenceData/Platform*ResolverClient.cs`.
> - Modül paketi: MOD-0048-FU01 (sağlayıcı) ve MOD-0290 (GSKU / pazar).

## 1. Ne işe yarar

MDM iki işlemde Platform'a sorar:
- **GSKU taslağı** (`GET api/gskus/create-options`, UOM listesi);
- **ülke fiyatlı rakip kaydı** (pazar doğrulaması).

Platform yanıtı **doğrulanmış yayından** verir:
- kilitli katalog dosyası (SHA-256 parmak izi kodda sabit);
- **referans kiracı** altında yayımlanmış set;
- GSKU için ek olarak **tüketici kiracıya atama**.

Bunlardan biri yoksa MDM 503 döner:
- "Doğrulanmış referans verileri kullanılamıyor";
- `reference_data_unavailable`.

## 2. Anahtarlar

### 2.1 Platform

| anahtar | değer | gizli mi |
|---|---|---|
| `BusinessReferenceData:Provider:ReferenceTenantId` | referans sağlayıcı kiracı (tüketiciden **farklı**) | hayır |
| `BusinessReferenceData:VerifiedGskuOperationalProvisioning:Enabled` / `EnumerationEnabled` | `true` | hayır |
| `…:CatalogPath` | `Seed/business-reference-data/mod-0290-gsku-reference.json` (çalışma dizini = API proje dizini) | hayır |
| `…:ExpectedCatalogVersion` / `ExpectedCatalogFingerprint` | `1.0.0` / `e95ef856…422a5d`. Kod sabitiyle **aynı** olmalı; değiştirilmez. | hayır |
| `…:ConsumerTenantId` | tüketici kiracı. `ReferenceTenantId` ile **aynı olamaz** (MOD-0048-FU01). | hayır |
| `…:ActorId` / `IdempotencyNamespace` | işletim kimliği / tekrar oynatma ad alanı | hayır |
| `BusinessReferenceData:VerifiedMarketOperationalProvisioning:*` | `Enabled`, `CatalogPath` (`mod-0290-market-reference.json`), `UNSD-M49-2026-08-08` / `b94c4528…360039`, `ActorId`, `IdempotencyNamespace` (`[A-Za-z0-9._-]`, ≤ 128) | hayır |
| `VerifiedGskuResolverCredential:Mdm:Identifier` | MDM'in kimlik adı | hayır |
| `VerifiedGskuResolverCredential:Mdm:ConsumerService` / `AllowedAudience` | `DITENMDMSERVICE` / `VERIFIED_GSKU_RESOLVE` (kod sabiti) | hayır |
| `VerifiedGskuResolverCredential:Mdm:ActiveSecret` | paylaşılan sır | **EVET** |
| `…:PreviousSecret` / `PreviousValidUntilUtc` | yalnız sır değiştirirken (eski sırrın geçerlilik süresi) | **EVET** |

### 2.2 MDM

GSKU ve pazar istemcisi **aynı** `VerifiedGskuResolver` bölümünü okur.

| anahtar | değer | gizli mi |
|---|---|---|
| `VerifiedGskuResolver:PlatformBaseAddress` | Platform'un **doğrudan** adresi (Gateway değil): `api/internal/v1/reference-data/...` | hayır |
| `VerifiedGskuResolver:Timeout` | `00:00:02`. 0'dan büyük ve **en çok 2 sn** olmalı; daha uzunu istemci reddeder (503). | hayır |
| `VerifiedGskuResolver:CredentialIdentifier` | Platform `…:Mdm:Identifier` ile aynı | hayır |
| `VerifiedGskuResolver:CredentialSecret` | Platform `…:Mdm:ActiveSecret` ile aynı | **EVET** |

**İstek koşulu:** MDM, kullanıcının token'ını aynen iletir. Token tek bir `tenant_id` iddiası taşıyan bir **kiracı kullanıcısına** (`actor_type = tenant_user`) ait olmalı. Ayrıca bir izin anahtarı gerekmez.

## 3. Yerel geliştirme (Development)

Gizli olmayan değerler depoda, sır user-secrets'ta:
- `appsettings.Development.json` (Platform + MDM) gizli olmayan değerleri taşır;
- referans kiracı yerelde ayrılmış bir GUID'dir: `0b7d0048-0290-4000-8000-000000000001`;
- tüketici kiracı `97c5`;
- kimlik adı `mdm-verified-reference-dev`.

Adımlar:
1. **Satır sonu (bir kez).** `.gitattributes` iki kilitli kataloğu LF'ye sabitler. Daha önce CRLF olarak açılmış bir çalışma ağacı kendiliğinden düzelmez. İki dosya bir kez yeniden alınır:
   ```bash
   rm services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/mod-0290-gsku-reference.json services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/mod-0290-market-reference.json
   ```
   ```bash
   git restore -- services/Diten.Platform/src/Diten.Platform.API/Seed/business-reference-data/
   ```
2. **Sır (bir kez; yenilemek için tekrar).** `powershell -ExecutionPolicy Bypass -File scripts/dev/set-verified-reference-dev-credential.ps1`. Rastgele bir sır üretir, Platform ve MDM user-secrets'a **aynı** değeri yazar ve değeri ekrana basmaz.
3. **Fleet'i yeniden başlat.** Platform açılışta GSKU kataloğunu yükler, referans kiracıda yayımlar ve 97c5'e atar. Bu işlem idempotenttir.
4. **Pazar (bir kez).** `powershell -ExecutionPolicy Bypass -File scripts/dev/run-verified-market-provisioning.ps1`.
   - Platform'un tek seferlik komutudur (`--run-verified-market-provisioning`).
   - Fleet'in derlemesini kullanır; port açmaz.
5. **Doğrula:**
   - Platform `GET http://localhost:5057/health/ready` → 200;
   - MDM `GET api/gskus/create-options` → 200 + UOM listesi;
   - ülke fiyatlı rakip kaydının doğrulama adımı 503 dönmez.

⚠ **Açılış riski.** GSKU runner'ı uygun bulup yüklemeye başlar da hata alırsa istisna fırlatır ve Platform **açılmaz**. Bu bilinçli bir tasarımdır (yarım yayın kalmasın).
- Böyle bir durumda logda şu satır görünür: "Verified GSKU operational reconciliation failed with classification …".
- Hızlı kapatma: Platform user-secrets'ına `BusinessReferenceData:VerifiedGskuOperationalProvisioning:Enabled=false` (ve `EnumerationEnabled=false`) yazıp yeniden başlatın.

## 4. Canlı (Production) — DURUM: KAPALI, ayrı karar gerekiyor

**⛔ Üretim yolu bugün yok.** Bu nedenle bu belge burada durur; canlıya hiçbir ayar uygulanmadı.
1. **Uygunluk yalnız Development'ta.** İki uygunluk sınıfı da `Development…Eligibility`'dir ve `IsDevelopment()` değilse reddeder (`REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE`).
   - Pazar komutu Development dışında hemen durur (`VERIFIED_MARKET_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED`).
   - Canlıda `Enabled=true` vermek yükleme yapmaz. Üstelik sağlık kontrolü "pilot not eligible" diye **Unhealthy** döner.
   - **Canlıda Enabled vermeyin.**
2. **Referans kiracı ile tüketici kiracı aynı olamaz.** CT'nin önerdiği canlı referans kiracı `be823006`, canlıdaki tek müşteri kiracısıdır, yani tüketicinin kendisidir. MOD-0048-FU01 ve kod bu eşitliği reddeder.
   - Canlıda ayrı bir **referans sağlayıcı kiracı** gerekir: veri sahibinin kiracısı, müşteri kiracısı değil.

**Açık kararlar** (İş Referans Verisi sahibi + mimari):
- **K-INFRA-1 · Üretim provisioning yolu.** Seçenekler:
  - (a) üretim için onaylı, imzalı bir uygunluk sınıfı ve sürüm denetimli bir işletim komutu;
  - (b) katalog yayınını yönetişim ekranından (MOD-0048 yayın onayı) yapmak;
  - (c) şimdilik canlıda yalnız ülke fiyatsız rakip kaydı.
- **K-INFRA-2 · Canlı referans kiracı GUID'i.** `be823006` olamaz (tüketici). Ayrılmış bir sağlayıcı kiracı belirlenmeli.
- **K-INFRA-3 · Canlı kimlik bilgisi.**
  - Kim üretir (operatör, sır kasası)?
  - Nereye yazılır? Platform `VerifiedGskuResolverCredential__Mdm__ActiveSecret`, MDM `VerifiedGskuResolver__CredentialSecret`; ortam değişkeni ya da sır kasası.
  - Döndürme: yeni sır `ActiveSecret`'a, eski sır `PreviousSecret` + `PreviousValidUntilUtc`'ye.

**Bugün canlıda güvenle yapılabilecek tek ayar (BL-404):** `BusinessReferenceData__Provider__ReferenceTenantId` vermek.
- Değer, K-INFRA-2'deki sağlayıcı kiracıdır.
- Bu ayar `/health` ve `/health/ready`'nin 503 dönmesini giderir (§7/11, `go-live-2026-09-meetings-task-engine.md`).
- Provisioning bölümleri **verilmezse** sağlık kontrolü "pilot not provisioned" der ve sağlıklı döner.
