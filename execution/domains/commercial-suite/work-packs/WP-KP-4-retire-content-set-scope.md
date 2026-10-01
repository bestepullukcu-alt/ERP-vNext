# WORK PACKAGE — WP-KP-4 · İçerik Seti + İçerik Kapsamı emekliliği (SB-2 üreticisi dahil) + iddia kullanımında yol kaynağı

> **CT (SoR).**
> - **Tasarım:** `DESIGN-KP-STUDIO-knowledge-path-studio.md` §7 (emeklilik sırası), D-KP-1 / D-KP-9.
> - **Karar:** bridge-decision §8 (İçerik Seti kalkar), §7 (İçerik Kapsamı kalkar).
> - **Ön koşul sağlandı:** KP-1 / KP-2 / KP-3 / KP-UI-1 / KP-UI-2 birleşti; Bilgi Yolu kurgu + MLR + çıktı + yayın hattı çalışıyor.
> - **Canlı veri:** 0 İçerik Seti, 0 set revizyonu, 1 İçerik Kapsamı ("test", SCOPE-2026-395306).
> - **Kapsam:** CrmService + Web + Platform (yalnız manifest sayfa kaydı) + Auth (yalnız anahtar açıklaması).
>
> **Çalışma yeri:** worktree `C:\tmp\kp-4`, dal `wp/kp-4`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Amaç
Bilgi Yolu Stüdyosu İçerik Seti'nin işini devraldı. İçerik Seti'nin **yazma** yüzeyi (kurgu, revizyon, render, yayın), SB-2'nin setten içerik / yol üretimi, İçerik Kapsamı'nın kalan okuma ucu ve Web ekranları / menüleri kaldırılır.
- İddia kullanım okuması, set kaynağı yerine **Bilgi Yolu'nun iddialarını** (KP-1 `KnowledgePath.Claims`) sayar.
- Eski verinin okunabilirliği korunur. Repository'lerin ve class-map'lerin tamamen silinmesi, verinin 0 olduğu doğrulandıktan sonra ayrı küçük işte yapılır.

## Kanıt (CT)
- **CRM dosyaları:**
  - `Application/Features/ContentComposition/ContentSets/*`: Commands, CommandHandlers, SelectionHandlers, EligibilityHandler, Queries, QueryHandlers, Dtos, Mapper, Permissions, `ContentSetContext` (KP-1'den beri ortak `Chain` yardımcısına delege).
  - `ContentSetRevisions/*`: Commands, CommandHandlers, Queries, QueryHandlers, Dtos, Mapper, Permissions, **`ContentSetReleaseProducer`** (SB-2; KP-3'ten beri ortak `Path/Release` kurallarını çağırıyor), `ContentSetPathOrder` (KP-1'den beri `ChainArrangementOrder`'a delege), `Rendering/{IContentSetRevisionRenderer, IContentArtifactStore}`.
  - `ContentScopes/*` (SB-1R sonrası yalnız okuma).
  - **Api:** `ContentSetsController`, `ContentSetRevisionsController`, `ContentScopesController` (+ Requests).
  - **Infra:** `PdfSharpContentSetRevisionRenderer` (KP-3'ten beri ortak `MigraDocPdf`). **`IContentArtifactStore` / `HttpContentArtifactStore` Bilgi Yolu renderer'ı da kullanıyor — KALIR** (Path/Release altına taşı ya da ortak yere al).
  - **Persistence:** set / revizyon / kapsam repository'leri + class-map'ler (`ContentSet.LegacyScope` dahil).
  - **DI:** Application / Infrastructure / Persistence.
- **Set dışından referans:**
  - `Claims/ClaimUsageQueryHandlers.cs` (`IContentSetRepository`; `ClaimUsageItemTypes.ContentSet`; `SetGroup`);
  - `Claims/ClaimListCounts.cs` (`setsRepo`, `ContentSet` kullanım sayımı);
  - `Claims/ClaimQueryHandlers.cs`, `ClaimUsageQueries.cs`;
  - `IContentCompositionAuditPublisher` (set olay kodları);
  - `Domain/Entities/KnowledgeStudioOrigin.cs` (SB-2 geri izi, KnowledgeContent / Path üstünde).
- **Platform:** `Crm/SelfRegistration/CrmManifestProvider.cs:152-154` `CONTENT_SETS` sayfası (`CONTENT_SCOPES` SB-1R'de kalktı).
- **Auth:** `DataSeeder.cs:503-504` `crm.content-set.read | manage` + set revizyon anahtarları (review / render / release / withdraw — grep). `crm.content-scope.*` SB-1R'de "deprecated" yapıldı.
- **Web:** `Controllers/CRM/ContentSetsController*.cs`, `Views/CRM/ContentSets/**`, `wwwroot/assets/js/CRM/ContentSets/**`, resx `ContentSets*` / `_Workspace*`. Sidebar `CONTENT_SETS` manifest'ten. ContentScopes Web'de SB-1R'de kalktı.
- **İsim çakışması uyarısı:** `KnowledgeContent.ContentSetId` = **dil varyantı grubu** (SCMM-13). İçerik Seti aggregate'i DEĞİL. **DOKUNMA.**

## NE

### 1. CRM — yazma ve üretim yüzeyinin kaldırılması
- **İçerik Seti komutları + handler'ları + uçları kaldırılır:** create, update, clone, archive, components / claims add / arrange / remove, apply-eligibility.
- **Set revizyonu yazma uçları kaldırılır:** submit, review-decision, render, release, withdraw.
- **`ContentSetReleaseProducer` (SB-2) ve set renderer'ı kaldırılır.** `IContentArtifactStore` + `HttpContentArtifactStore` Bilgi Yolu için KALIR (uygun ortak yere taşı, davranış değişmez).
- **Salt okunur bırakılanlar** (eski verinin denetim izi): `GET content-sets`, `GET content-sets/{id}`, `GET content-set-revisions…` (+ artifact okuma).
  - Okuma uçları `[Obsolete]` belgelenir.
  - Permissions sabitleri yalnız okuma için kalır.
- **İçerik Kapsamı:** okuma uçları + query handler'lar + DTO / mapper kaldırılır. Repository ve class-map **kalır**: set okuması eski `LegacyScope`'u parse ediyor; silme ayrı iş.
- Kalan `ContentSetContext` / `ContentSetPathOrder` sarmalayıcıları kullanılmıyorsa kaldırılır (ortak `Chain` yardımcıları kalır).
- DI temizlenir; derleme uyarısı bırakılmaz.

### 2. İddia kullanım okuması
- `ClaimUsageQueryHandlers` + `ClaimListCounts`'ta **İçerik Seti kaynağı kaldırılır.** Yerine **Bilgi Yolu kaynağı** gelir:
  - kaynak: `KnowledgePath.Claims[]`'ta iddianın kayıt kimliği geçen arşivsiz yollar;
  - öğe tipi `knowledge-path`: `{pathId, pathCode, pathName, status, countryCode, version}`;
  - grup yolun ülkesi.
- `ClaimUsageItemTypes.ContentSet` kaldırılır (ya da okuma uyumluluğu için kalır ama üretilmez). Web İddialar "Kullanım" bölümü yeni tipi gösterir. FE-1 quick view / sayaç bu tipi sayar.
- **İçerik (KnowledgeContent `ClaimRefs`) ve yolculuk kaynakları DEĞİŞMEZ.**

### 3. Web
- İçerik Setleri ekranları, controller'ları, JS, resx ve proxy'leri **kaldırılır**.
- `/CRM/ContentSets*` istekleri `/CRM/KnowledgePaths`'e kalıcı yönlendirme (eski yer imleri).
- İddialar ekranındaki kullanım listesinde `knowledge-path` öğesi: etiket "Bilgi yolu" (7 dil) + bağlantı `/CRM/KnowledgePaths/{id}`.

### 4. Platform / Auth
- `CrmManifestProvider` `CONTENT_SETS` sayfası kaldırılır; manifest yeniden kaydında canlı menüden budanır (SB-1R'deki `CONTENT_SCOPES` deseni + test).
- Auth `crm.content-set.*` ve set revizyon anahtarları **silinmez** (rol bağları kopmasın); açıklamaları "deprecated (WP-KP-4)".

### 5. Kalan iz (bilinçli)
- `KnowledgeStudioOrigin` (SB-2 geri izi) KnowledgeContent / Path üstünde **kalır**. Veri yok; okuma uyumluluğu. `[Obsolete]` notu.
- İçerik tipi `assembled-presentation` ve kaynak `content-studio` sözlük değerleri kalır (veri yok; zararsız). Yeni içerik formunda **önerilmez**.

## KORU / YAPMA
- **Bilgi Yolu (KP-1 / 2 / 3), iddia onayı, Bilgi İçeriği yayın kapısı (BE-6), yolculuk / ziyaret DEĞİŞMEZ.**
- `KnowledgeContent.ContentSetId` (dil varyantı grubu) **DOKUNMA.**
- Repository / class-map **silme YOK.** Eski doküman okunur kalmalı (memory `crm-classmap-rejects-unknown-elements`).
- Veri silme / göç YOK.
- Kampanya / Dönem / Strateji Şablonu "scope"ları ilgisiz.
- **DUR:**
  - `IContentArtifactStore` dışında Bilgi Yolu'nun hâlâ set koduna bağımlı başka bir parçası çıkarsa → taşı, kopyalama; belirsizse raporla.
  - Platform / Auth'ta set anahtarlarını başka bir modül (ör. rol şablonu) kullanıyorsa → raporla.

## Acceptance
- **E2:**
  - CRM testleri 0 kırmızı (taban 2186/0/5; set testleri kaldırılan yüzeyle birlikte gider ya da salt okunur testlere iner; bilinen sıra flake'i hariç).
  - Web 0 kırmızı (taban 399). Platform ilgili + Auth testleri yeşil. Build'ler 0 hata. `node --check` temiz.
  - **Yeni / güncel testler:**
    - set yazma uçları yok (404 / route yok);
    - set okuma uçları çalışıyor (eski doküman + `LegacyScope`);
    - iddia kullanımında `knowledge-path` öğesi (yol iddiası → görünür, arşivli yol → görünmez) ve İçerik Seti öğesi üretilmiyor;
    - `ClaimListCounts` yol sayıyor;
    - Bilgi Yolu render / yayın (KP-3) testleri yeşil (artifact store taşındı);
    - manifest'te `CONTENT_SETS` yok;
    - `/CRM/ContentSets` → `/CRM/KnowledgePaths` yönlendirmesi;
    - L10n 7 dil.
  - **Sabotaj:** iddia kullanımında yol kaynağı ve `CONTENT_SETS` manifest testi kırmızıya dönmeli.
- **E4 (CT):**
  - menüde İçerik Setleri / İçerik Kapsamları yok;
  - iddia kullanımında bir Bilgi Yolu (ALMIBA yolu kurulunca);
  - `/CRM/ContentSets` yönlendiriyor.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md]
WP: WP-KP-4 · İçerik Seti + İçerik Kapsamı emekliliği (SB-2 üreticisi dahil) + iddia kullanımında yol kaynağı
Repository: C:\tmp\kp-4 (worktree) · Branch: wp/kp-4 · commit bu dala, push YOK

Amaç: Bilgi Yolu Stüdyosu İçerik Seti'nin işini devraldı (KP-1..UI-2 birleşti). İçerik Seti yazma yüzeyi (kurgu/revizyon/render/yayın) + SB-2 üreticisi + İçerik Kapsamı okuma ucu + Web ekranları/menü kalkar; iddia kullanımı set yerine Bilgi Yolu iddialarını sayar. Eski veri okunur kalır (repository/class-map silme ayrı iş). Canlıda 0 set, 1 kapsam.

Önce oku: execution/domains/commercial-suite/work-packs/WP-KP-4-retire-content-set-scope.md · …/DESIGN-KP-STUDIO-knowledge-path-studio.md (§7) · …/SCMM-studio-knowledge-bridge-decision.md (§7, §8) · …/WP-SB-1R-remove-content-scope-derive-set-context.md (§37: CONTENT_SCOPES manifest budama deseni) · …/WP-KP-3-path-render-release.md (§37: IContentArtifactStore / MigraDocPdf ortaklığı) · services/Diten.CrmService/src/**/ContentComposition/{ContentSets, ContentSetRevisions, ContentScopes, Claims/ClaimUsageQueryHandlers.cs, Claims/ClaimListCounts.cs, Claims/ClaimQueryHandlers.cs}/** · Api/Controllers/CRM/{ContentSetsController, ContentSetRevisionsController, ContentScopesController}.cs · Infrastructure/ContentComposition/Rendering/** · Persistence/DependencyInjection.cs · Domain/Entities/{ContentSet, ContentSetRevision, KnowledgeStudioOrigin, KnowledgePath, KnowledgeContent}.cs · services/Diten.Platform/src/**/Crm/SelfRegistration/CrmManifestProvider.cs · services/Diten.AuthService/src/**/Seed/DataSeeder.cs · frontend/Diten.Web/{Controllers/CRM/ContentSetsController*, Views/CRM/ContentSets/**, wwwroot/assets/js/CRM/ContentSets/**, Controllers/CRM/Claims* (kullanım listesi)} · memory crm-classmap-rejects-unknown-elements.

NE:
 1) CRM: İçerik Seti komut/handler/uçları kaldır (create/update/clone/archive/components+claims add/arrange/remove/apply-eligibility); set revizyonu yazma uçları kaldır (submit/review-decision/render/release/withdraw); ContentSetReleaseProducer (SB-2) + set renderer'ı kaldır; IContentArtifactStore + HttpContentArtifactStore Bilgi Yolu için KALIR (uygun ortak yere taşı, davranış aynı). SALT OKUNUR kalan: GET content-sets, content-sets/{id}, content-set-revisions… (+artifact) — [Obsolete] belgeli. İçerik Kapsamı okuma uçları + handler/DTO/mapper kaldır (repository + class-map KALIR). Kullanılmayan ContentSetContext/ContentSetPathOrder sarmalayıcılarını kaldır (ortak Chain yardımcıları kalır). DI temiz, uyarı yok.
 2) İddia kullanımı: ClaimUsageQueryHandlers + ClaimListCounts'ta İçerik Seti kaynağı kalkar; yerine Bilgi Yolu: KnowledgePath.Claims'te iddianın kayıt kimliği geçen arşivsiz yollar → öğe tipi knowledge-path {pathId, pathCode, pathName, status, countryCode, version}, grup yolun ülkesi; ContentSet öğe tipi artık üretilmez; içerik + yolculuk kaynakları DEĞİŞMEZ.
 3) Web: İçerik Setleri controller/görünüm/JS/resx/proxy kaldır; /CRM/ContentSets* → /CRM/KnowledgePaths kalıcı yönlendirme; İddialar kullanım listesinde knowledge-path öğesi ("Bilgi yolu" 7 dil + /CRM/KnowledgePaths/{id} bağlantısı).
 4) Platform: CrmManifestProvider CONTENT_SETS sayfasını kaldır (manifest yeniden kaydında budanır; SB-1R deseni + test). Auth: crm.content-set.* ve set revizyon anahtarları SİLİNMEZ, açıklama "deprecated (WP-KP-4)".
 5) Bilinçli iz: KnowledgeStudioOrigin kalır ([Obsolete] notu); assembled-presentation / content-studio sözlük değerleri kalır ama yeni içerik formunda önerilmez.
KORU/YAPMA: Bilgi Yolu (KP-1/2/3), iddia onayı, BE-6 yayın kapısı, yolculuk/ziyaret DEĞİŞMEZ; KnowledgeContent.ContentSetId (dil varyantı grubu) DOKUNMA; repository/class-map SİLME yok; veri silme/göç yok; Kampanya/Dönem/Strateji scope'ları ilgisiz.
DOĞRULA (E2): cd C:\tmp\kp-4; CRM testleri 0 kırmızı (taban 2186/0/5; bilinen sıra flake'i hariç); dotnet test frontend/Diten.Web.Tests → 0 kırmızı (taban 399); Platform ilgili + Auth testleri yeşil; build'ler 0 hata; node --check temiz. Yeni/güncel testler: set yazma uçları yok, set okuma uçları (eski doküman + LegacyScope), iddia kullanımında knowledge-path öğesi (arşivli yol görünmez) + ContentSet öğesi yok, ClaimListCounts yol sayar, KP-3 render/yayın testleri yeşil (store taşındı), manifest'te CONTENT_SETS yok, /CRM/ContentSets yönlendirmesi, L10n 7 dil. Sabotaj: kullanımda yol kaynağı + CONTENT_SETS manifest testi kırmızıya dönmeli. Commit ("refactor(crm): WP-KP-4 — retire content sets + scopes (SB-2 producer), claim usage counts knowledge paths" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: IContentArtifactStore dışında Bilgi Yolu'nun set koduna bağımlı başka parçası çıkarsa (taşı, belirsizse DUR) ya da set anahtarlarını Platform/Auth'ta başka bir modül kullanıyorsa DUR + raporla.
```
