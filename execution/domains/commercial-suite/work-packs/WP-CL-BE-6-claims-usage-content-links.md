# WORK PACKAGE — WP-CL-BE-6 · Bilgi İçeriği ↔ iddia bağı + "Nerede kullanılıyor" okuması (içerik · içerik seti · yolculuk) + yayın kapısı (CRM, backend)

> **CT (SoR).** İddialar v2 Faz 2. Kaynak: `SCMM-claims-v2-mockup-analysis-plan.md` (CL-BE-6, K9, **kullanıcı kararı D4 = Bilgi İçeriği de iddiaya bağlansın**).
>
> **Amaç:**
> - Bir Bilgi İçeriği (sunum, broşür…) hangi **onaylı iddiaları** kullandığını tutsun.
> - İçerik yayınlanırken bağlı iddialar **o ülkede onaylı** olsun (uyum kapısı).
> - İddia için "Nerede kullanılıyor?" sorusu tek okumayla cevaplansın: **içerik, içerik seti, etkileşim yolculuğu**, ülke bazında.
>
> **Kapsam:** Yalnız CRM. Arayüz YOK (CL-FE-6). Platform, Auth ve Web DEĞİŞMEZ.
>
> **Çalışma yeri:** worktree `C:\tmp\cl-be-6`, dal `wp/cl-be-6` (taban `test/crm-content-visit-e2e` @ `1e2f05c7`). Commit bu dala, push YOK.
>
> **Paralel iş:** BE-4 aynı anda çalışıyor. **`ClaimsController.cs`, `ClaimQueryHandlers.cs`, `ClaimCommandHandlers.cs` DOKUNMA.** Kullanım okuması **ayrı dosyalarda** yazılır (aşağıda).

## Kanıt (CT kod okuması)
- **KnowledgeContent** (`Domain/Entities/KnowledgeContent.cs`):
  - Mevcut alanlar: ContentCode, ContentTitle, ContentType, ContentStatus, SubjectId, TopicId, AudienceProfileId, ProductId, **LanguageCode**, ContentVersion, EffectiveFrom/To, ContentSetId (dil varyant grubu)…
  - **Ülke alanı YOK. İddia referansı YOK.**
  - Yayın akışı: `Application/Features/Knowledge/Content/*`.
- **İçerik Seti:** `ContentSet.SelectedClaims: List<ContentSetClaim{ClaimId, ClaimVersion}>` (`ContentSet.cs:37,98`), çekirdek iddia kaydına referans. Kapsam referansı `ContentSetScopeRef` → ContentScope (MarketRefs).
- **Yolculuk:**
  - `ContentEngagementJourney.Stages[].RecommendedKnowledgePathId` (`ContentEngagementJourney.cs:122`) → `KnowledgePath.Steps[].ContentId` (`KnowledgePath.cs:119`) → KnowledgeContent.
  - Yolculuk içeriği yol üzerinden kullanır.
- **İddia (BE-1):**
  - `Claim` (ClaimCode, Kind, ProductId, Status…).
  - `ClaimCountryVersion` (ClaimCode, CountryCode, Version, **Texts[].LanguageCode**, Status).
  - Durumlar: approved / review-required / in-review / draft / inactive / archived.
- **Tuzaklar:** yeni Guid alanları class-map'te stringGuid (memory `crm-new-aggregate-classmap-guid`); class-map katı okuma (`crm-classmap-rejects-unknown-elements`).

## Kararlar
- **Bağ nesnesi** `KnowledgeContent.ClaimRefs` (≤20): `[{ ClaimCode, ClaimId, CountryVersionId?, CountryCode? }]`.
  - **Ülkeye özgü içerik** → ülke sürümüne bağlanır (`CountryVersionId` + `CountryCode`).
  - **Global/çekirdek içerik** → yalnız çekirdek iddiaya bağlanır (`CountryVersionId` boş).
- **Kaydetme doğrulaması** (create/update):
  - İddia bu tenant'ta var. `ClaimId` o `ClaimCode`'a ait.
  - `CountryVersionId` verildiyse aynı ClaimCode'a ve `CountryCode`'a ait.
  - İçerik `ProductId` ile iddia `ProductId` ikisi de doluysa eşit olmalı → aksi **400 `claim_product_mismatch`**.
  - **Durum serbest:** taslak içerik taslak iddiaya bağlanabilir.
- **Yayın kapısı** (KnowledgeContent publish):
  - Her ref **kullanılabilir** olmalı:
    - ülke sürümü `approved` ya da `review-required`;
    - ya da (sürümsüz ref'te) çekirdek `approved` / `review-required`.
    - Aksi → **409 `claim_not_approved`**.
  - Ülke sürümü ref'inde içerik `LanguageCode` ∈ o sürümün `Texts[].LanguageCode` → aksi **409 `claim_language_mismatch`**.
  - `review-required` yayını **engellemez**. Ancak okuma modelinde `claimNeedsReview=true` işaretlenir.
  - ClaimRefs boşsa kapı yok. **Mevcut davranış değişmez.**
- **Kullanım okuması:** `GET api/crm/content-composition/claims/usage?claimCode={code}&countryCode={cc?}` (`crm.claim.read`).
  - Yanıt: `{ claimCode, groups: [ { countryCode | "GLOBAL", items: [ { type: "content" | "content-set" | "journey", id, code, name, languageCode?, version?, status, via?, claimNeedsReview } ] } ] }`.
  - **İçerik:** ClaimRefs'te ClaimCode geçenler. Grup, ref'in `CountryCode`'u; yoksa GLOBAL.
  - **İçerik seti:** `SelectedClaims.ClaimId` ∈ o ClaimCode'un **tüm** kayıt id'leri. Grup: setin ContentScope'unun MarketRefs'i (birden çoksa her gruba); yoksa GLOBAL.
  - **Yolculuk:** aşamasının önerilen yolunda, bu iddiaya bağlı bir içeriğe işaret eden adım olan yolculuklar. `via` = içerik kodu. Grup içerikten gelir.
  - Arşivlenenler hariç.
  - **Ülke kısıtı YOK** (D1). `countryCode` yalnız filtre.
- **İçerik detayına** ClaimRefs + her ref'in güncel iddia durumu (okuma zenginleştirmesi, FE için).

## NE
1. **Domain:** `KnowledgeContent.ClaimRefs` + `KnowledgeContentClaimRef` value object. Class-map: ClaimId ve CountryVersionId stringGuid. Eski dokümanlar sorunsuz okunur.
2. **Content create/update:** ClaimRefs opsiyonel. Doğrulama yukarıdaki gibi. **Mevcut istek gövdeleri aynen çalışır.**
3. **Publish kapısı:** mevcut publish handler'ına kapı eklenir (yalnız ClaimRefs doluysa).
4. **Kullanım okuması:**
   - Yeni dosyalar: `Features/ContentComposition/Claims/ClaimUsageQueries.cs` + `ClaimUsageQueryHandlers.cs` + `Api/Controllers/CRM/ClaimUsageController.cs`.
   - Route `api/crm/content-composition/claims/usage`, gateway'deki mevcut wildcard kapsar.
   - Repository sorguları tenant-first. Gerekirse indeks: `knowledge_contents` `{TenantId, ClaimRefs.ClaimCode}`.
5. **İçerik okuma zenginleştirmesi:** KnowledgeContent detay DTO'suna `claimRefs[{…, claimStatus, countryVersionStatus}]`.
6. **Audit:** `knowledge_content_claim_refs_changed` (id ve sayılar; metin yok).

## KORU / YAPMA
- **`ClaimsController.cs`, `ClaimQueryHandlers.cs`, `ClaimCommandHandlers.cs`, `ClaimV2CommandHandlers.cs` DOKUNMA** (BE-4 çakışması). İddia tarafında yalnız **okuma** yapılır (repository arayüzleri).
- **ContentSetClaim, KnowledgePath, Journey modelleri DEĞİŞMEZ.** Yalnız okunur.
- Ülke kısıtı YOK. UI YOK. Platform, Auth, Web DOKUNMA.
- ClaimRefs boş içeriklerde davranış **birebir aynı** (Knowledge UI ve testleri bozulmaz).
- **DUR:**
  - KnowledgeContent publish akışı birden fazla yerde dağınıksa (tek kapı noktası yoksa) → kapıyı en içteki ortak noktaya koy ve raporla.
  - ContentScope MarketRefs biçimi ülke koduna eşlenemiyorsa → GLOBAL'e koy ve raporla.

## Acceptance
- **E2:**
  - `dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo` → **0 kırmızı** (taban 1974/0/5 + yeni).
  - `dotnet test frontend/Diten.Web.Tests -c Release --nologo` → 229/0.
  - CRM API build 0 hata.
- **Yeni testler:**
  - ClaimRefs kaydet: yabancı ClaimId → 400; sürüm başka ülke → 400; ürün uyuşmazlığı → `claim_product_mismatch`; taslak iddiaya bağ serbest.
  - Yayın: onaysız ref → `claim_not_approved`; dil uyuşmazlığı → `claim_language_mismatch`; review-required geçer + işaret; ClaimRefs boş → eski davranış.
  - Kullanım: içerik, set ve yolculuk doğru gruplarda; arşivliler hariç; `countryCode` filtresi; kiracı izolasyonu.
  - Eski içerik dokümanı (ClaimRefs yok) okunuyor. Guid sorgusu boş dönmüyor.
  - **Sabotaj kanıtı** en az 3 kural için.
- **Diff:** yalnız `services/Diten.CrmService/**`. Yasaklı 4 dosyada diff YOK.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-CL-BE-6 · Bilgi İçeriği ↔ iddia bağı + "Nerede kullanılıyor" okuması (içerik · içerik seti · yolculuk) + yayın kapısı (CRM, backend)
Repository: C:\tmp\cl-be-6 (worktree) · Branch: wp/cl-be-6 (taban test/crm-content-visit-e2e @1e2f05c7) · commit bu dala, push YOK · BE-4 PARALEL: ClaimsController.cs / ClaimQueryHandlers.cs / ClaimCommandHandlers.cs / ClaimV2CommandHandlers.cs DOKUNMA

Amaç: Bilgi İçeriği kullandığı onaylı iddiaları tutsun (ClaimRefs); içerik yayınlanırken bağlı iddialar o ülkede onaylı olsun (kapı); iddia için "nerede kullanılıyor" (içerik/içerik seti/yolculuk, ülke bazında) tek okumayla gelsin. Kullanıcı kararı D4. UI YOK.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-BE-6-claims-usage-content-links.md (Kararlar + hata kodları + yanıt şekli) · …/WP-CL-BE-1-claims-model-v2.md · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/{KnowledgeContent,ContentSet,ContentEngagementJourney,KnowledgePath,Claim,ClaimCountryVersion}.cs · Application/Features/Knowledge/Content/** (create/update/publish) · Persistence/DependencyInjection.cs (class-map) · memory crm-new-aggregate-classmap-guid, crm-classmap-rejects-unknown-elements.

NE:
 1) KnowledgeContent.ClaimRefs ≤20 [{ClaimCode, ClaimId, CountryVersionId?, CountryCode?}] + class-map stringGuid; eski dokümanlar okunur.
 2) Content create/update: ClaimRefs opsiyonel; iddia tenant'ta var, ClaimId o ClaimCode'a ait, CountryVersionId aynı ClaimCode+CountryCode'a ait, içerik ProductId ile iddia ProductId (ikisi doluysa) eşit değilse 400 claim_product_mismatch; durum serbest.
 3) Publish kapısı (yalnız ClaimRefs doluysa): ref ülke sürümü approved|review-required ya da (sürümsüz) çekirdek approved|review-required değilse 409 claim_not_approved; ülke sürümü ref'inde içerik LanguageCode ∉ sürüm Texts dilleri → 409 claim_language_mismatch; review-required engellemez (okumada claimNeedsReview).
 4) Kullanım okuması — YENİ dosyalar ClaimUsageQueries.cs + ClaimUsageQueryHandlers.cs + Api/Controllers/CRM/ClaimUsageController.cs: GET api/crm/content-composition/claims/usage?claimCode&countryCode (crm.claim.read) → groups[{countryCode|"GLOBAL", items[{type content|content-set|journey, id, code, name, languageCode?, version?, status, via?, claimNeedsReview}]}]; içerik=ClaimRefs; set=SelectedClaims.ClaimId ∈ ClaimCode'un tüm kayıtları (grup ContentScope MarketRefs, yoksa GLOBAL); yolculuk=aşama önerilen yolunda bu iddiaya bağlı içeriğe işaret eden adım (via=içerik kodu); arşivliler hariç; ülke kısıtı YOK; gerekirse indeks knowledge_contents {TenantId, ClaimRefs.ClaimCode}.
 5) KnowledgeContent detay DTO'suna claimRefs[+claimStatus, countryVersionStatus].
 6) Audit knowledge_content_claim_refs_changed (metinsiz).
KORU/YAPMA: 4 iddia dosyasına DOKUNMA (yalnız repository okuması); ContentSetClaim/KnowledgePath/Journey modelleri DEĞİŞMEZ; ülke kısıtı YOK; UI/Platform/Auth/Web DOKUNMA; ClaimRefs boşsa davranış birebir aynı.
DOĞRULA (E2): cd C:\tmp\cl-be-6; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 1974/0/5 + yeni); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 229/0; CRM API build 0 hata; yeni testler WP Acceptance listesindeki gibi + ≥3 sabotaj kanıtı; git diff yalnız services/Diten.CrmService/** ve 4 yasaklı dosyada diff YOK. Commit ("feat(crm): WP-CL-BE-6 — knowledge content claim refs, publish gate, claim usage read (content/content set/journey)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: publish akışı dağınıksa kapıyı en içteki ortak noktaya koy+raporla; ContentScope MarketRefs ülke koduna eşlenemiyorsa GLOBAL+raporla.
```
