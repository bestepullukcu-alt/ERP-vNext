# WORK PACKAGE — WP-SB-1R · İçerik Kapsamı'nı kaldır; set bağlamı türetilsin (ülke + dil setin üstünde)

> **CT (SoR).** Karar: `SCMM-studio-knowledge-bridge-decision.md` §7 (kullanıcı, 2026-09-30).
> - İçerik Kapsamı ayrı kayıt olmaktan çıkar. Bu, SCMM-14 D14-a'dan bilinçli bir sapma.
> - Ürün, kitle ve dil zaten Bilgi Bankası / zincir / iddialarda yapılandırılmış; serbest metin tekrarı kalkar.
> - SB-2 birleşti (`289fcf83`); bu paket onun ülke okumasını da çevirir.
>
> **Çalışma yeri:** worktree `C:\tmp\sb-1r`, dal `wp/sb-1r`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Yeni model — set bağlamı
| Alan | Kaynak | Nerede tutulur |
|---|---|---|
| **Ülke** | kullanıcı seçer, tek seçim, COUNTRY_CODES | `ContentSet.CountryCode` (yeni) |
| **Dil** | kullanıcı seçer; seçenekler ülkenin `country-content-languages` dilleri | `ContentSet.LanguageCode` (yeni) |
| **Ürün** | zincirin konusu → birincil `global-product` ExternalReference (SB-2'deki çözümle aynı) | türetilir, salt okunur |
| **Kitle** | zincirin `ForWhomAudienceProfileIds`'i | türetilir, salt okunur |

**Tek dil kuralı erkene çekilir:**
- Dilinden farklı dilde bir bileşen eklemek → 409 `component_language_mismatch` (yazımda).
- SB-2'nin yayındaki `component_language_mixed` kontrolü yedek olarak kalır.
- Set dili sonradan değiştirilirse, bileşenlerin hepsi o dilde değilse 409.

## Kanıt (CT) — dokunulacak yerler
- **CRM Domain:** `ContentSet.cs` (`Scope` = `ContentSetScopeRef`), `ContentSetRevision.cs` (dondurulmuş `Scope`), `ContentScope.cs`, `IContentScopeRepository`.
- **CRM Application:**
  - `ContentComposition/ContentSets/*`: Commands, CommandHandlers, Dtos, Mapper, `ContentSetEligibilityHandler.BuildContextAsync` (kapsamdan product / market / audience / channel).
  - `ContentSetRevisions/*`: CommandHandlers (dondurma), Dtos, Mapper, `ContentSetReleaseProducer` (kapsam `MarketRefs` → ülke).
  - `Claims/ClaimUsageQueryHandlers.cs:143` + `:280` (setin kapsam `MarketRefs`'i → ülke grubu).
  - `ContentScopes/*`.
- **CRM Api:** `ContentSetsController` + `ContentSetRequests`, `ContentScopesController` + `ContentScopeRequests`.
- **CRM Infra:** `PdfSharpContentSetRevisionRenderer.cs:75-78` ("Scope" bölümü). `Persistence/DependencyInjection.cs` (class-map).
- **Menü / yetki:**
  - `Platform …/Crm/SelfRegistration/CrmManifestProvider.cs:152` (`CONTENT_SCOPES` sayfası);
  - `AuthService …/Seed/DataSeeder.cs:501-502, 1526-1527` (`crm.content-scope.read|manage`).
- **Web:**
  - `Controllers/CRM/ContentScopesController.cs`; `Views/CRM/ContentScopes/**`; `wwwroot/assets/js/CRM/ContentScopes/**`;
  - `Controllers/CRM/ContentSetsController.cs` (`api/scopes`); `Views/CRM/ContentSets/Create.cshtml` + `js/CRM/ContentSets/create.js` (kapsam seçici);
  - `workspace.js` (başlıkta kapsam);
  - kapsam resx ailesi.
- **Testler:** `ContentCompositionControllersTests`, `ContentSetAssemblyTests`, `ContentSetReleaseKnowledgeTests`, `ContentSetRevisionRenderTests`, `KnowledgeContentClaimLinkTests`.
- **Canlı veri:** 1 kapsam ("test", SCOPE-2026-395306), **0 set**.

## NE
1. **Set modeli:**
   - `ContentSet.CountryCode` + `LanguageCode` eklenir; `Scope` kaldırılır.
   - **Eski dokümanlar okunabilmeli:** CRM class-map bilinmeyen alanı reddeder (memory `crm-classmap-rejects-unknown-elements`), bu yüzden eski `Scope` elemanı okumada **yok sayılır**, çökme olmaz.
   - Oluştur ve güncelle ülke + dil ister:
     - ülke COUNTRY_CODES'ta (400 `country_invalid`);
     - dil o ülkenin dillerinde (400 `language_not_in_country`);
     - BRD okunamazsa 503.
   - Taslakta ülke / dil değiştirilebilir (tek dil kuralıyla).
   - Bileşen eklemede dil kontrolü (409 `component_language_mismatch`).
2. **Türetilmiş bağlam okuması:**
   - Tek yardımcı: `ContentSetContextResolver` → `{countryCode, languageCode, productId, productCode, productName, audienceProfileIds}`.
   - Ürün ve kitle zincirden (SB-2'deki çözümün aynısı; kodu ortaklaştır, kopyalama).
   - Set DTO'su bu bağlamı taşır.
3. **Revizyon dondurma:**
   - Yeni revizyonlar `Context` anlık görüntüsü tutar (ülke, dil, ürün, kitle).
   - Eski revizyonlardaki `Scope` elemanı okunur ama kullanılmaz (geriye uyumlu).
4. **Tüketiciler:**
   - **Uygunluk kontrolü:** bağlamı setten kurar — product = MDM ürün kodu, market = ülke kodu, audience = hedef kitle profili kodları, channel yok.
   - **İddia kullanım raporu:** ülkeyi setin `CountryCode`'undan alır.
   - **SB-2 üreticisi:** iddia ülke sürümü setin ülkesinden (tek nokta). İçerik ve yolun dili set dili.
   - **PDF render:** "Bağlam" bölümü (ülke, dil, ürün, kitle).
5. **Kapsamın kaldırılması:**
   - Web kapsam sayfaları, JS, resx, `api/scopes` proxy'si ve Studio oluşturma ekranındaki kapsam seçici kalkar.
   - Menü kaydı (`CONTENT_SCOPES` manifest sayfası) kalkar.
   - CRM kapsam **yazma** uçları (create / update / archive) kalkar. **Okuma uçları ve repository şimdilik kalır** (salt okunur; ayrı temizlik işi).
   - Auth seed'deki `crm.content-scope.*` anahtarları **silinmez** (rol bağları kopmasın); açıklamaları "deprecated" yapılır.
6. **Web:**
   - Studio oluşturma ekranı: zincir şablonu + **ülke** (ülke adıyla, FE-2'nin `ClaimDisplayNames`'i) + **dil** (ülkenin dilleri, yerel adıyla).
   - Çalışma alanı başlığı: ülke, dil, ürün (zincirden), kitle (zincirden) salt okunur.
   - Taslakta ülke / dil düzenlenebilir.
   - Hata kodları kullanıcı dilinde. 7 dil L10n.

## KORU / YAPMA
- **Ziyaret (SB-3), İddialar ekranları, Bilgi İçeriği / Yol / Yolculuk DEĞİŞMEZ.**
- SB-2 davranışı yalnız ülke / dil kaynağı kadar değişir.
- Kampanya, Dönem ve Strateji Şablonu'nun kendi "scope" alanları bu işle **ilgisiz**, DOKUNMA.
- Platform'da yalnız `CrmManifestProvider` sayfa kaydı; Auth'ta yalnız anahtar açıklaması.
- Ham repository yazması yok. Yeni alanlar class-map'e.
- Tarayıcıda ayrı sekme, canlı yazma YOK.
- **DUR:**
  - bir tüketici kapsamın `Channel` / `PeriodFrom-To` alanlarına gerçekten dayanıyorsa;
  - uygunluk politikalarında kapsam değerleriyle eşleşen **canlı** kural varsa (eşleşme bozulur): raporla.

## Acceptance
- **E2:**
  - CRM testleri 0 kırmızı (taban 2109/0/5; bilinen sıra flake'i hariç). Web testleri 0 kırmızı (taban 333).
  - Platform ve Auth ilgili testleri yeşil. Build'ler 0 hata. `node --check` temiz.
  - Yeni testler:
    - ülke / dil doğrulaması (geçersiz ülke, ülkede olmayan dil, BRD kapalı 503);
    - bileşen dil uyumsuzluğu 409;
    - eski `Scope` elemanlı doküman okunuyor (class-map);
    - bağlam çözümü (ürün / kitle zincirden);
    - uygunluk bağlamı setten;
    - kullanım raporu ülkesi setten;
    - SB-2 ülke sürümü setten;
    - menüde kapsam sayfası yok;
    - L10n 7 dil.
  - **Sabotaj:** dil uyumsuzluğu kontrolü ve SB-2 ülke kaynağı testleri kırmızıya dönmeli.
- **E4 (CT):**
  - İçerik Setleri → Yeni: zincir TPL-ALMIBA-01 + Türkiye + Türkçe.
  - Başlıkta ALMIBA / Nefroloji görünüyor.
  - İngilizce içerik eklemek reddediliyor.
  - Menüde İçerik Kapsamları yok.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SB-1R · İçerik Kapsamı'nı kaldır; set bağlamı türetilsin (ülke + dil setin üstünde)
Repository: C:\tmp\sb-1r (worktree) · Branch: wp/sb-1r · commit bu dala, push YOK

Amaç: Kullanıcı kararı (bridge-decision §7): İçerik Kapsamı ayrı kayıt olmaktan çıkar. Set bağlamı = ülke + dil (setin üstünde, kullanıcı seçer) + ürün + kitle (zincirden türetilir). Tüketiciler (uygunluk, kullanım raporu, SB-2 üreticisi, PDF) setten okur; kapsam sayfası/menüsü/yazma uçları kalkar.

Önce oku: execution/domains/commercial-suite/work-packs/WP-SB-1R-remove-content-scope-derive-set-context.md · …/SCMM-studio-knowledge-bridge-decision.md (§6, §7) · …/WP-SB-2-content-set-release-to-knowledge.md (§37) · services/Diten.CrmService/src/** ContentComposition/{ContentSets, ContentSetRevisions (ContentSetReleaseProducer), ContentScopes, Claims/ClaimUsageQueryHandlers.cs} · Domain/Entities/{ContentSet, ContentSetRevision, ContentScope}.cs · Infrastructure/ContentComposition/Rendering/PdfSharpContentSetRevisionRenderer.cs · Persistence/DependencyInjection.cs · services/Diten.Platform/src/**/Crm/SelfRegistration/CrmManifestProvider.cs · services/Diten.AuthService/src/**/Seed/DataSeeder.cs · frontend/Diten.Web/{Controllers/CRM/ContentScopesController.cs, Controllers/CRM/ContentSetsController.cs, Controllers/CRM/ClaimDisplayNames.cs, Views/CRM/ContentScopes/**, Views/CRM/ContentSets/**, wwwroot/assets/js/CRM/ContentScopes/**, wwwroot/assets/js/CRM/ContentSets/**} · memory crm-classmap-rejects-unknown-elements, crm-new-aggregate-classmap-guid, l10n-bridge-pascalcase-loader.

NE:
 1) ContentSet: CountryCode + LanguageCode ekle, Scope kaldır (eski Scope elemanı okumada yok sayılsın — class-map çökmesin). Oluştur/güncelle: ülke COUNTRY_CODES'ta (400 country_invalid), dil ülkenin country-content-languages dillerinde (400 language_not_in_country), BRD okunamazsa 503; taslakta değiştirilebilir; bileşen eklemede dil uyumu (409 component_language_mismatch); dil değişiminde tüm bileşenler uymuyorsa 409.
 2) ContentSetContextResolver (tek yardımcı): {countryCode, languageCode, productId/Code/Name (zincir konusu → birincil global-product ExternalReference; SB-2 kodunu ortaklaştır, kopyalama), audienceProfileIds (zincir ForWhom)}; set DTO'su bağlamı taşır.
 3) Revizyon: yeni revizyonlar Context anlık görüntüsü tutar; eski revizyonların Scope elemanı okunur, kullanılmaz.
 4) Tüketiciler: uygunluk bağlamı setten (product = MDM ürün kodu, market = ülke kodu, audience = hedef kitle profili kodları); ClaimUsage ülkesi setten; SB-2 üreticisi ülke/dil setten (tek nokta); PDF "Bağlam" bölümü.
 5) Kaldır: Web kapsam sayfaları/JS/resx/api/scopes proxy + Studio Create'teki kapsam seçici; CrmManifestProvider CONTENT_SCOPES sayfası; CRM kapsam yazma uçları (create/update/archive). Okuma uçları + repository KALIR (salt okunur). Auth seed crm.content-scope.* anahtarları SİLİNMEZ, açıklaması "deprecated".
 6) Web: Studio Create = zincir + ülke (ClaimDisplayNames ile ad) + dil (ülkenin dilleri, yerel ad); çalışma alanı başlığında ülke/dil/ürün/kitle (ürün+kitle salt okunur); taslakta ülke/dil düzenlenebilir; hata kodları kullanıcı dilinde; 7 dil L10n.
KORU/YAPMA: Ziyaret (SB-3), İddialar ekranları, Bilgi İçeriği/Yol/Yolculuk DEĞİŞMEZ; SB-2 yalnız ülke/dil kaynağı kadar değişir; Kampanya/Dönem/Strateji Şablonu scope alanları İLGİSİZ, DOKUNMA; Platform'da yalnız manifest sayfa kaydı, Auth'ta yalnız anahtar açıklaması; ham repository yazması yok; yeni alanlar class-map'e; tarayıcıda ayrı sekme, canlı yazma YOK.
DOĞRULA (E2): cd C:\tmp\sb-1r; CRM testleri 0 kırmızı (taban 2109/0/5; bilinen sıra flake'i hariç); Web testleri 0 kırmızı (taban 333); Platform + Auth ilgili testler yeşil; build'ler 0 hata; node --check temiz. Yeni testler: ülke/dil doğrulaması (geçersiz ülke, ülkede olmayan dil, BRD kapalı 503), bileşen dil uyumsuzluğu 409, eski Scope elemanlı doküman okunur, bağlam çözümü, uygunluk bağlamı setten, kullanım raporu ülkesi setten, SB-2 ülke sürümü setten, menüde kapsam sayfası yok, L10n 7 dil. Sabotaj: dil uyumsuzluğu + SB-2 ülke kaynağı testleri kırmızıya dönmeli. Commit ("refactor(crm): WP-SB-1R — remove ContentScope, derive content set context (country + language on the set)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: bir tüketici kapsamın Channel/PeriodFrom-To alanlarına gerçekten dayanıyorsa ya da uygunluk politikalarında kapsam değerleriyle eşleşen canlı kural varsa DUR + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-09-30) — **ACCEPTED (E2)**
- **Commit:** `9df79043` (`wp/sb-1r`) → `test/crm-content-visit-e2e` fast-forward. 75 dosya (+1860 / −2427).
- **Diff (K13 okuma):**
  - `ContentSet.CountryCode` + `LanguageCode`. Eski `Scope` → `[Obsolete] LegacyScope`, element adı "Scope", `SetIgnoreIfNull`: eski doküman okunur, çökme yok. Revizyonda da aynı.
  - `ContentSetContextSnapshot`: `ProductId` / `AudienceProfileIds` string-Guid.
  - Bileşen ekleme dil kapısı `ContentSetSelectionHandlers` (409 `component_language_mismatch`). Dili olmayan eski set kapıdan geçmez.
  - Web'de kapsam sayfası, JS ve proxy kalmadı (yalnız iki yorum satırı "mirrors ContentScopes" diyor, zararsız). Manifest `CONTENT_SCOPES` kaldırıldı + test. Auth anahtarları "deprecated".
- **CT testleri:** CRM **2118/0/5**, Web **343/0**. Ajan: Platform 174/0, Auth 1019/0.
- **CT sabotajı:** bileşen dil kapısı devre dışı (`if (false && …)`) → `ContentSetContext` 1 kırmızı. Kod geri alındı.
- **DUR yok (ajan canlı salt okuma):** 0 uygunluk politikası, 0 politikalı iddia; Channel yalnız boyut; Period okuyan yok.
- **Ek karar (ajan, kabul):** ülke / dil yalnız taslakta değişir (409 `context_locked`). SB-2 ülkeyi revizyonun donmuş bağlamından okur (eski revizyonda set).
- **Açık:**
  - canlı "test" kapsamı arşivlenmedi (UI kalktı; okuma ucu var; zararsız);
  - CRM kapsam okuma uçları + repository ayrı temizlik işi.
- **E4:** CT, fleet restart sonrası (TPL-ALMIBA-01 + Türkiye + Türkçe, başlıkta ALMIBA / Nefroloji, İngilizce içerik reddi, menüde İçerik Kapsamları yok).
