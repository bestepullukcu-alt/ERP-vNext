# MOD-0165-FU05 — Campaign / Targeting Admin UI Implementation

**Tarih:** 2026-08-03  
**Module Pack:** `MOD-0165-FU05-campaign-targeting-admin-ui.md`  
**Servis:** `frontend/Diten.Web`  
**Shell:** `_LayoutTenantShell.cshtml`  
**Verdict:** **PARTIAL / REVIEW** — UI implementasyonu, build, modül testleri, RESX parity ve authenticated read-only browser smoke tamamlandı. Canonical Campaign permission seed/grant'i kapsam dışı olduğu için mevcut oturumda Campaigns menüsü görünmedi; deep link FU04 territory fallback ile çalıştı. Generic DataTable doğrulayıcının kalan yedi kontrolü bulk-delete beklediğinden, pack'in DELETE/hard-delete yasağı gereği uygulanmadı.

## 1. Yetkilendirme ve preflight

Kullanıcının Phase 1.5 onayı esas alındı. Pack `ready-for-dev` durumundan `in-progress`, doğrulama sonrası `review` durumuna taşındı. DCP-002 kimlik kapısı pack hazırlığında PASS idi. Uygulama yalnız pack §5 repo scope içinde yürütüldü.

## 2. Scope sonucu

Campaign liste/detail/create/edit/archive, target liste/create/edit/archive, static snapshot, consent controls/provenance, tenant-shell Campaigns linki, localization, frontend testleri ve evidence üretildi. Backend, Gateway, seed/grant, registry, Mongo ve MOD-0155 kapsamında FU05 tarafından değişiklik yapılmadı.

## 3. Frontend mimarisi

`CampaignsController` MVC page ve aynı-origin JSON proxy yüzeylerini barındırır. Browser yalnız `/CRM/Campaigns/api/**` çağırır; controller token/cookie ve tenant claim bağlamını server-side Gateway'e aktarır. Business istekleri `GatewayUrl` üzerinden 5000'e gider.

## 4. MVC rotaları

`/CRM/Campaigns`, `/Create`, `/Edit/{id}`, `/Details/{id}` ve `/api/**` proxy rotaları eklendi. Controller `[Authorize]` ile korunur; page/action permission kontrolleri canonical Campaign anahtarlarını ve yalnız FU04'te zaten var olan territory fallback'ini kullanır.

## 5. Campaign liste yüzeyi

DataTable v2; skeleton, contract error, empty/loading/error durumları, export/column visibility/filter toolbar ve responsive kolonlar içerir. Canlı smoke sırasında iki FU04 Campaign satırı render edildi, skeleton kapandı ve console error/warning oluşmadı.

## 6. Server-side filtre sınırı

Yalnız `CampaignStatus`, `CampaignType`, `BrandId`, `ProductId`, `SubjectId`, `IncludeArchived` query'leri gönderilir. Search, ObjectiveType, BusinessUnit ve date range eklenmedi; client-side fake filter uygulanmadı. UI bu sınırlamayı görünür bilgi notuyla açıklar.

## 7. Golden Compact Campaign formu

Create/Edit ortak `_Form.cshtml` kullanır. Summary, References, Consent Context ve External References bölümleri Create/Edit/Details arasında aynı sıradadır. CampaignCode create sırasında required, edit sırasında immutable; CampaignName, CampaignType, CampaignStatus ve StartDate required; ters tarih aralığı engellenir.

## 8. Campaign detail

Summary, reference IDs, consent defaults, external references ve Targets yüzeyleri tek detail ekranında bulunur. Archived campaign read-only uyarısı ve mutation action gating uygulanır.

## 9. ID-only referanslar

BusinessUnit, Brand, Product, Subject, Topic, ConceptChain, EngagementJourney, KnowledgePath ve KnowledgeContent yalnız ID olarak gösterilir. Master fetch, display resolution veya hardcoded fallback yoktur.

## 10. Campaign archive

Archive aksiyonu `POST /archive`, `window.showConfirm` ve `window.showToast` kullanır. HTTP DELETE veya hard delete yolu yoktur.

## 11. Target DataTable

Target status/source/type, reason codes, exclusion reason, batch/provenance ve consent decision görünürdür. Archived/excluded/blocked/unknown satırlar UI tarafından gizlenmez.

## 12. Golden Slim target canvas

Manual target create/edit offcanvas aynı detail yüzeyinde uygulanmıştır. TargetType/TargetId create identity alanları, source/status, selection reason, reason codes, dates ve conditional ExclusionReason validation bulunur. `campaign-target` option'ı contracttan gelse bile deny-list ile çıkarılır.

## 13. Target archive

Target lifecycle `POST .../archive` ile yürür. Archived target yeniden edit edilemez; DELETE yolu üretilmez.

## 14. Static snapshot

Lightweight row editor ve JSON paste fallback sunulur. Submit öncesinde non-empty/parse/row doğrulaması yapılır; success panelinde SnapshotBatchId ile created/reconciled/excluded sayıları gösterilir ve target tablosu yenilenir. Snapshot history veya import/export engine uydurulmadı.

## 15. Consent davranışı

`ApplyConsentFilter=true` için görünür channel ve purpose gerekir; campaign defaultları prefill olabilir fakat sessiz varsayım yapılmaz. Filter kapatıldığında `consent_filter_not_applied` güçlü uyarısı görünür. allowed/blocked/unknown/not_applicable kararları badge olarak render edilir.

## 16. Provenance ve data minimization

MatchedConsentId ve MatchedPreferenceIds yalnız provenance identifier olarak gösterilir. Consent/preference record payload DOM'a, view modeline veya toast'a taşınmaz. Yasak response alanları source taramasında 0 eşleşme verdi.

## 17. Contract-driven capability gating

Önce `/CRM/Campaigns/api/contract` yüklenir. Contract okunamazsa create/action capability'leri fail-closed kapanır ve kontrollü error state görünür. Contract dışı capability türetilmez.

## 18. Permission ve navigation sonucu

Tenant shell'e yalnız `Perms.Has("crm.campaign.read")` guard'lı, localized `/CRM/Campaigns` linki eklendi. Authenticated smoke oturumunda canonical grant yoktu; bu nedenle menü görünmedi. Deep link mevcut FU04 `crm.territory.read` fallback'iyle 200 açıldı. Seed/grant veya daha geniş navigation fallback'i yazılmadı; follow-up `MOD-0165-FU-RBAC` gereklidir.

## 19. Localization

`CampaignIndex.en/fr/es/zh/ar/ru/tr.resx` dosyalarının her biri 124 anahtar taşır; missing=0, extra=0. Shared `CampaignsMenu` anahtarı da yedi dilde eklendi. `_IndexL10n` JSON bridge'i `window.L10n` ve module-local immutable sözlüğü besler.

## 20. Otomatik test sonucu

Komut: `NODE_OPTIONS=--require=./tests/vitest-crypto-polyfill.cjs npx vitest run tests/campaign-targeting-admin-ui.test.js`  
Sonuç: **1 file PASS, 27/27 tests PASS**. Testler route/contract, state'ler, validation, TenantId'siz payload, POST archive/no DELETE, targets, snapshot, consent, permission, seven-locale parity ve direct-5061 yokluğunu kapsar. Polyfill yalnız Node 16 test runner uyumluluğudur; production bundle'a girmez.

## 21. Build sonucu

Komut: `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Release --no-restore`  
Sonuç: **PASS — 0 hata, 14 mevcut/pre-existing nullable uyarı**. İlk Debug denemesi çalışan Diten.Web sürecinin satellite resource DLL kilidi nedeniyle yazma hatası verdi; kullanıcı sürecini sonlandırmadan Release build ile temiz compile/Razor doğrulaması tamamlandı.

## 22. DataTable doğrulayıcı sonucu

Komut: `py .antigravity/scripts/verify_datatable_page.py . --area CRM --module Campaigns --reference compact --api-profile proxy`  
Sonuç: **78 PASS / 7 FAIL**. Kalan yedi kontrolün tamamı select-all checkbox, bulk config, `/bulk`, bulk-delete trigger, bulk reload ve clear-selection bekler. FU05 pack'i DELETE/hard-delete'ı açıkça yasakladığı ve FU04 bulk archive endpointi sağlamadığı için bu öğeler eklenmedi. Bu bir fonksiyon eksikliği değil, generic verifier ile daha spesifik module-pack lifecycle kontratı arasındaki bilinçli sapmadır.

## 23. Runtime smoke

Anonim preflight: MVC page/proxy 302 login; Gateway contract 401 — beklenen güvenlik davranışı. Authenticated Chrome smoke: `/CRM/Campaigns` 200, contract error yok, iki backend row render, filter/export/colvis toolbar görünür, console clean. `/CRM/Campaigns/Create` 200; dört Compact section, required alanlar, contract vocabularies, ID-only yardım ve consent defaults görünür. Veri oluşturan/arşivleyen mutating smoke yapılmadı; mevcut kullanıcı verisine dokunulmadı.

## 24. Sınırlar, açıklar ve closeout kararı

- Direct 5061, HTTP DELETE, hard delete, TenantId payload ve MOD-0155/future field source taramaları temizdir.
- Backend/Gateway/Auth/registry/Mongo değişikliği bu çalışma kapsamında yapılmadı.
- Canonical Campaign permission seed/grant eksikliği nedeniyle menü positive smoke deferred; deep link/fallback positive smoke PASS.
- Full create → target → snapshot → archive mutating golden flow, uygun disposable fixture sağlandığında tekrar çalıştırılmalıdır.
- Generic DataTable bulk-delete kontrolleri pack yasağı nedeniyle intentionally N/A'dır.

Bu nedenle kod **review-ready**, fakat lifecycle verdict **PARTIAL** tutulur. Canonical RBAC follow-up ve disposable-fixture mutating smoke tamamlandıktan sonra pack `done` yapılabilir.
