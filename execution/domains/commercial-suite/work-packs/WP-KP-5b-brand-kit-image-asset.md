# WORK PACKAGE — WP-KP-5b · Marka kiti (ürün başına) + onaylı görsel kaydı — İçerik Stüdyosu (CRM)

> **CT (SoR), 2026-10-02.**
> - **Karar:** DEC-SCMM-05 (yönetişim İçerik Stüdyosu'nda, dosyalar Platform Belge Yönetimi'nde; kit ürüne = MDM Global Product bağlı).
> - **Mockup + analiz + kullanıcı kararları:** `mockups/kp-5b-brand-assets/KP-5b-mockup-analysis.md` §3 (E1–E11), §4 (alan modeli), **§7 (K-1..K-5)**.
> - **Paralel paket:** WP-KP-5b-PLT (Platform) — §Sözleşme'si burada **sahte istemciyle** kullanılır. Web (KP-5b-UI) bu iki paket birleşince.
> - **Kapsam:** CrmService (+ Auth izin kataloğu kaydı). Merkezi log (audit) YOK (kullanıcı: bekliyor).
>
> **Çalışma yeri:** worktree `C:\tmp\kp-5b`, dal `wp/kp-5b`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Kanıt (CT)
- **Yaşam döngüsü deseni (KP-5a — kullan, kopyalama):** `Domain/Entities/RegulatoryText.cs` (ortak taban: taslak → incelemede → aktif / yerini aldı / arşiv; `ReviewRounds`, `Decisions`, türetilmiş `OpenKey`, `IsArchivable()`), `Application/Features/Knowledge/Regulatory/{RegulatoryTextCore, RegulatoryTextLifecycle, …}.cs`, depo `Persistence/Repositories/RegulatoryTextRepositories.cs` (`DuplicateKey` → çakışma istisnası), tek aktif + tek açık sürüm kısmi index'leri (`$ne` yok).
  - Yaşam döngüsü genelleştirilebiliyorsa (tür parametresiyle) yeniden kullan; güvenlilik metni / yasal profil davranışı **değişmez** (testleri yeşil kalır). Genelleştirme mümkün değilse ince tür sınıflarıyla deseni izle ve raporla.
- **Onay altyapısı:** `Infrastructure/Workflow/GatewayClaimWorkflowClient.cs` (`ReasonCode` artık istekten — KP-5a-FIX-1), sonuç tüketicisi `Infrastructure/Eventing/ClaimWorkflowOutcomeConsumer.cs` (ObjectType yönlendirmesi), karar ucu deseni KP-5a; ad çözümleme `Application/Common/IUserDisplayNameResolver.cs`.
- **Ülke / dil:** KP-1 doğrulayıcısı (`ChainContextErrors`, `ChainContextValidation`), BRD `country-content-languages` (`ClaimReferenceSets.CountryContentLanguages`).
- **Ürün:** MDM Global Product, fail-closed (KP-5a güvenlilik metni deseni).
- **Platform görsel uçları:** WP-KP-5b-PLT §Sözleşme (görsel kaydı, yeni sürüm, `image-info`, `renditions`, seçenekler `mediaType=image`). CRM bunlara Gateway üzerinden kullanıcının token'ıyla gider (iddia kanıt istemcisi `Infrastructure/Evidence/GatewayClaimEvidenceClient.cs` deseni).

## NE
### 1. `ImageAsset` (koleksiyon `image_assets`)
- **Kimlik:** `AssetCode` (`VRL-{sıra}`, sunucu), kayıt sürümü `VersionNumber`.
- **Dosya bağı (sabit):** `DocumentId`, `DocumentCode`, **`DocumentVersionId` (sabitlenmiş)**, `Checksum`, `MediaType`, `Width`, `Height` — oluşturma / yeni sürümde Platform `image-info`'dan anlık görüntü. Belgenin daha yeni sürümü varsa okumada `newerDocumentVersion` (bilgi) → yeni kayıt sürümü + yeniden onay.
- **Alanlar:** `Title{dil}`, `Kind` (photo / diagram / logo / icon / chart), `AltText{dil}`, `Credit?`, `GlobalProductIds[]` (≥ 1), `CountryCodes[]` (boş = tüm ülkeler), `Tags[]`, kullanım hakkı `{Source: agency | stock | internal, LicenceType, Holder, ValidFrom?, ValidTo?, Perpetual}`.
- **Kurallar (K-4):**
  - **E1 alternatif metin:** zorunlu diller = izinli ülkelerin içerik dillerinin birleşimi (ülke boşsa 7 dil); eksikse gönderim 400 `alt_text_required` (eksik diller listesiyle). **İngilizce yedek YOK.**
  - **E2 lisans:** `Source = internal` ise `Perpetual = true` olabilir (`ValidTo` boş); diğer kaynaklarda `ValidTo` zorunlu (400 `licence_expiry_required`).
  - Ürünler MDM'de olmalı (fail-closed); ülkeler KP-1 doğrulayıcısıyla.
- **Durumlar (E3):** `draft` → `in-review` → `approved`; `withdrawn` (kararla, **gerekçe zorunlu**, kullanan kayıtlara uyarı); `expired` (**türetilmiş**: `approved` + `ValidTo < bugün` — kalıcı yazım yok); `archived` (yalnız kullanılmıyorsa; kullanımdaysa 409 `asset_in_use`); yeni sürüm onaylanınca önceki `superseded`.
- **Onay (K-2, K-3):** MOD-0023 **küresel** şablon `KP-ASSET` (ayar `Crm:ImageAssets:Workflow:TemplateCode`), ObjectType `crm.image-asset`, gerekçe kodu `CRM_IMAGE_ASSET_SUBMITTED`, derin bağlantı `/CRM/ImageAssets/{id}`; Regülasyon tek adım; ret yorumu zorunlu; kişi bazlı SoD (yazar / gönderen karar veremez); karar Görev Merkezi görevinden (K1) + CRM karar ucu.
- **Yükleme (K-5):** `POST image-assets/upload` (multipart) → Platform `images` ucu → dönen belge + v1 ile **taslak** `ImageAsset`; ya da `POST image-assets` mevcut `documentId + versionId` ile (Platform `image-info` → `isImage` değilse 400 `document_not_image`).

### 2. `BrandKit` (koleksiyon `brand_kits`)
- **Kimlik:** `BrandKitCode` (`BK-{sıra}`), `GlobalProductId` (+ görüntü; **oluşturulunca değişmez**), `MdmBrandId?` (bilgi), `VersionNumber`. **Ürün başına tek aktif + tek açık sürüm** (KP-5a index deseni).
- **Alanlar:** `Palette[] {Hex, Role: primary | secondary | accent | text | background, AllowText, AllowBackground, Name{7 dil}, SortOrder}`, `Logos[] {Variant: main | mono | inverse, ImageAssetId?, MinWidthPx, MinWidthMm, ClearSpacePercent, AllowedBackgrounds: light | dark | photo | color}`, `Fonts {HeadingFamily, HeadingFallback, BodyFamily, BodyFallback, FontFileDocumentId?}`, `Typography {MinFontPt, H1, H2, H3, Body}`, `UsageNotes`.
- **Gönderim kontrol listesi (E6, engelleyici, kodlu):** ana logo dolu + `approved` `ImageAsset` + `Kind = logo` + ürünü kitin ürününü içeriyor + ülke kısıtı yok (`logo_*` kodları); en az bir `AllowText` ve bir `AllowBackground` rengi; geçerli HEX; metin / zemin çiftlerinde WCAG AA kontrastı (4.5:1); renk adları 7 dil. Mono / ters logo eksikse **uyarı**.
- **Arşiv (E7):** aktif kit kullanımdaysa 409 `brand_kit_in_use` (kullanım okuması bir arayüz arkasında; bugün yollar kit referansı taşımıyor → 0, KP-UI-3 doldurur).
- **Onay (K-1, K-3):** küresel şablon `KP-BRAND`, ObjectType `crm.brand-kit`, gerekçe `CRM_BRAND_KIT_SUBMITTED`, derin bağlantı `/CRM/BrandKits/{id}`; KP-5a kuralları.
- **Çözümleme:** `GET brand-kits/resolve?productId=` → aktif kit (logoların görüntü bilgileriyle) ya da 404 `brand_kit_missing`.

### 3. Seçici araması (E8)
- `GET image-assets/picker?productId=&countryCode=&languageCode=&kind=&q=&page=` — bağlam **çağırandan** gelir; her öğe `{assetId, assetCode, title, kind, width, height, selectable, reasonCode?}`.
- Gerekçe kodları: `not_approved`, `expired`, `withdrawn`, `country_restricted`, `product_not_allowed`, `alt_text_missing` (o dilde metin yok).
- Kit logo seçimi: `kind=logo`, bağlam = kitin ürünü, ülke yok → ülke kısıtlı logo `country_restricted`.

### 4. Okuma
- Liste / detay (sürüm geçmişi, kararlar + adlar, `canEdit / canSubmit / canDecide / canArchive / canWithdraw`), görselde **nerede kullanılıyor** (bugün: marka kitleri; yollar / sayfalar KP-UI-3), kit için kullanım (bugün 0).
- Önizleme / küçük resim CRM'de **saklanmaz**; Web, Platform `renditions` ucunu proxy'ler (KP-5b-UI).

### 5. Yetki anahtarları
`crm.brand-kit.read | manage | submit`, `crm.image-asset.read | manage | submit` → Auth kataloğu + `permission-scope-baseline.csv` (kiracı, `crm-knowledge`), İngilizce açıklama (kullanıcı kararı: 7 dil açıklama yok). Rollere verme KP-5b-CFG'de.

## KORU / YAPMA
- Güvenlilik metni / yasal profil davranışı ve testleri DEĞİŞMEZ; iddia / Bilgi Yolu onay akışları DEĞİŞMEZ.
- Dosya CRM'de saklanmaz (yalnız referans + anlık metadata). Platform'a yeni uç açma (PLT paketi açıyor); Web DOKUNMA.
- Merkezi log YOK. Ham repository yazması yok; silme yok.
- Class-map string-Guid; kısmi index'te `$ne` yok.
- **DUR:** yaşam döngüsünü genelleştirmek güvenlilik metni davranışını değiştirmeyi gerektiriyorsa; Platform sözleşmesi (PLT §Sözleşme) CRM ihtiyacını karşılamıyorsa → raporla.

## Sözleşme (KP-5b-UI ile paylaşılan)
| Uç | Not |
|---|---|
| `/api/crm/knowledge/brand-kits` — liste (`productId, status, onlyActive, q`), `GET/PUT /{id}`, `POST` (ürün), `POST /{id}/new-version`, `/submit`, `/withdraw`, `/decision {outcome, comment}`, `/archive`, `GET /{id}/checklist`, `GET /resolve?productId` | detay: palet / logolar (logo için `assetCode, title, width, height`) / yazı tipleri / tipografi / notlar + bayraklar + `decisions[{by, byName, outcome, comment, at}]` |
| `/api/crm/knowledge/image-assets` — liste (`productId, countryCode, kind, status, usableOnly, expiringWithinDays, q, page`), `GET/PUT /{id}`, `POST` (mevcut belge), `POST /upload` (multipart), `POST /{id}/new-version` (`documentVersionId?`), `/submit`, `/withdraw-review`, `/decision`, `/withdraw {reason}`, `/archive`, `GET /picker`, `GET /{id}/usage` | detay: dosya bağı + `newerDocumentVersion`, alanlar, `status` (türetilmiş `expired` dahil), bayraklar, kararlar |
| Hata kodları | `alt_text_required`, `licence_expiry_required`, `document_not_image`, `asset_in_use`, `brand_kit_in_use`, `brand_kit_open_draft_exists`, `image_asset_open_draft_exists`, `withdraw_reason_required`, `rejection_comment_required`, `self_decision_forbidden`, `review_template_missing`, `logo_missing`, `logo_not_approved`, `logo_not_logo_kind`, `logo_product_mismatch`, `logo_country_restricted`, `palette_text_color_required`, `palette_background_color_required`, `palette_contrast_insufficient`, `palette_name_languages_incomplete`, `invalid_hex`, `product_not_found`, `dependency_unavailable`, `country_invalid` |

## Acceptance
- **E2:** CRM 0 kırmızı (taban ölç; son CT 2187/0/5), Web 0 kırmızı (taban 473), Auth 0 kırmızı, build 0 hata.
  - **Yeni testler:** iki aggregate yaşam döngüsü (tek aktif + tek açık, superseded, 409 çakışma); E1 zorunlu dil hesabı (ülke dilleri birleşimi, boş = 7) + İngilizce yedek yok; E2 süresiz yalnız kurum içi; E3 `expired` türetilmiş + `withdrawn` gerekçe + kullanımdaki görsel arşivlenemez; E6 kontrol listesinin her kodu; E7 kullanımdaki kit arşivlenemez; E8 seçici gerekçe kodları + bağlam; onay: `KP-ASSET` / `KP-BRAND`, ObjectType yönlendirmesi, gerekçe kodları, ret yorumu, kişi bazlı SoD; Platform istemcisi sahte (yükleme, `image-info`, `isImage` değil → 400); MDM fail-closed; kiracı izolasyonu; class-map + index; güvenlilik metni / iddia / yol testleri yeşil.
  - **Sabotaj:** (1) alternatif metin zorunluluğunda İngilizce yedeği aç → E1 testi kırmızı; (2) kontrol listesinden kontrast kontrolünü kaldır → kırmızı.
- **E4:** KP-5b-UI + KP-5b-CFG sonrası.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-KP-5b · Marka kiti (ürün başına) + onaylı görsel kaydı — İçerik Stüdyosu (CRM)
Repository: C:\tmp\kp-5b (worktree) · Branch: wp/kp-5b · commit bu dala, push YOK · CrmService (+ Auth izin kataloğu kaydı)

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-KP-5b-brand-kit-image-asset.md — önce tamamını oku (Kanıt / NE / KORU / Sözleşme / Acceptance). Ayrıca: …/mockups/kp-5b-brand-assets/KP-5b-mockup-analysis.md (§3 E1-E11, §4 alan modeli, §7 kararlar) · docs/decisions/DEC-SCMM-05-brand-kit-approved-asset-placement.md · …/WP-KP-5b-PLT-docmgmt-image-support.md (§Sözleşme — paralel yazılıyor, sahte istemci) · …/WP-KP-5a-safety-text-legal-profile.md + WP-KP-5a-FIX-1 (§37) · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/RegulatoryText.cs · Application/Features/Knowledge/Regulatory/** · Persistence/Repositories/RegulatoryTextRepositories.cs · Infrastructure/Workflow/GatewayClaimWorkflowClient.cs · Infrastructure/Eventing/ClaimWorkflowOutcomeConsumer.cs · Infrastructure/Evidence/GatewayClaimEvidenceClient.cs · Application/Common/IUserDisplayNameResolver.cs · KP-1 ChainContextValidation · memory crm-new-aggregate-classmap-guid, crm-classmap-rejects-unknown-elements, mongo-partial-index-ne-crash.

NE: (1) ImageAsset (image_assets): VRL-{sıra}, VersionNumber; sabit dosya bağı (DocumentId/Code, DocumentVersionId, Checksum, MediaType, Width, Height — Platform image-info anlık); Title{dil}, Kind, AltText{dil}, Credit?, GlobalProductIds[]≥1, CountryCodes[] (boş=tümü), Tags[], kullanım hakkı (Source agency|stock|internal, LicenceType, Holder, ValidFrom?, ValidTo?, Perpetual). E1: alt metin zorunlu diller = izinli ülkelerin içerik dilleri birleşimi (boş=7), İngilizce yedek YOK (400 alt_text_required); E2: Perpetual yalnız internal, diğerlerinde ValidTo zorunlu; E3: draft→in-review→approved, withdrawn (gerekçe zorunlu), expired TÜRETİLMİŞ, archived (kullanımdaysa 409 asset_in_use), superseded; newerDocumentVersion bilgisi. Onay KP-ASSET (küresel), crm.image-asset, CRM_IMAGE_ASSET_SUBMITTED, /CRM/ImageAssets/{id}, Regülasyon tek adım, ret yorumu, kişi bazlı SoD. Yükleme: POST image-assets/upload (Platform images ucu) + POST image-assets (mevcut belge; isImage değilse 400 document_not_image). (2) BrandKit (brand_kits): BK-{sıra}, GlobalProductId (değişmez), MdmBrandId?, ürün başına tek aktif + tek açık; Palette/Logos/Fonts/Typography/UsageNotes (WP alanları); gönderim kontrol listesi (E6 kodları, mono/ters uyarı); arşiv kullanımdaysa 409 brand_kit_in_use (kullanım arayüzü, bugün 0); onay KP-BRAND, crm.brand-kit, CRM_BRAND_KIT_SUBMITTED, /CRM/BrandKits/{id}; resolve?productId (404 brand_kit_missing). (3) Seçici GET image-assets/picker (bağlam çağırandan; selectable + reasonCode: not_approved, expired, withdrawn, country_restricted, product_not_allowed, alt_text_missing; kit logosu kind=logo). (4) Okuma: liste/detay/bayraklar/kararlar+adlar/kullanım; önizleme CRM'de saklanmaz. (5) Yetki anahtarları crm.brand-kit.{read,manage,submit}, crm.image-asset.{read,manage,submit} → Auth katalog + permission-scope-baseline.csv (İngilizce açıklama). KP-5a yaşam döngüsünü genelleştirerek yeniden kullan (güvenlilik metni davranışı değişmeden); olmuyorsa deseni izle + raporla.
KORU/YAPMA: güvenlilik metni/yasal profil/iddia/yol davranışı değişmez; dosya CRM'de saklanmaz; Platform'a yeni uç yok; Web DOKUNMA; merkezi log YOK; silme yok; class-map string-Guid; index'te $ne yok.
DOĞRULA (E2): cd C:\tmp\kp-5b; CRM testleri (tabanı ölç; son CT 2187/0/5) → 0 kırmızı (PiiMasking flake'i hariç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 473); Auth testleri 0 kırmızı; build 0 hata. Yeni testler WP Acceptance. Sabotaj: (1) alt metinde İngilizce yedeği aç → E1 testi kırmızı; (2) kontrol listesinden kontrastı kaldır → kırmızı; geri al. TestResults/*.trx izleniyor, klasörü silme. Commit ("feat(crm): WP-KP-5b — brand kit per product + approved image asset (Content Studio), regulatory single-step approval" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: genelleştirme güvenlilik metni davranışını değiştirmek zorundaysa; PLT sözleşmesi CRM ihtiyacını karşılamıyorsa → DUR + raporla.
```
