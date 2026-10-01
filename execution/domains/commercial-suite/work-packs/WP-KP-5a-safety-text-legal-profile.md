# WORK PACKAGE — WP-KP-5a · Güvenlilik metni (ürün × ülke × dil) + ülke yasal profili — Regülasyon onaylı ana veri (CRM)

> **CT (SoR), 2026-10-01.**
> - **Tasarım:** `DESIGN-KP-STUDIO-knowledge-path-studio.md` §2.4 (kilitli `safety` / `legal-footer` blokları), §4 D-KP-5 (K2: güvenlilik iddia değil), §8 KP-5, §9.1.
> - **Kullanıcı (2026-10-01):** güvenlilik metnini **yalnız Regülasyon (Ruhsat)** onaylar; tam MLR değil.
> - **Bölme (CT):** KP-5 → **KP-5a** (bu paket, CRM) ∥ **KP-5a-UI** (Web) → **KP-5a-CFG** (canlı onay şablonları + yetki). **KP-5b** (marka kiti + onaylı görsel kütüphanesi, Belge Yönetimi entegrasyonu) ayrı analizle KP-UI-3 öncesi.
> - **CT varsayılanı (kullanıcıya bildirildi):** ülke yasal profili de **aynı tek adımlı Regülasyon** akışıyla onaylanır.
>
> **Çalışma yeri:** worktree `C:\tmp\kp-5a`, dal `wp/kp-5a`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Amaç
Sayfa tasarımcısının (KP-UI-3) kilitli bloklarını besleyecek iki onaylı ana veri:
- **Güvenlilik metni:** ürün × ülke × dil başına onaylı güvenlilik bilgisi (uzun + isteğe bağlı kısa metin). Bir anahtar için aynı anda **tek aktif** sürüm.
- **Ülke yasal profili:** ülke × dil başına yasal altbilgi, ruhsat sahibi, yan etki bildirim metni, sayfa onay kodu biçimi. Tek aktif sürüm.
- Her ikisi: taslak → **Regülasyon onayı** (MOD-0023, tek adım) → aktif; yeni sürüm aktifin klonu; onaydan sonra değişmez.

## Kanıt (CT)
- **Onay altyapısı (KP-2 deseni — kullan, kopyalama):**
  - `Application/Features/Knowledge/Path/Review/{KnowledgePathReviewCore, KnowledgePathReviewHandlers, KnowledgePathReviewSubmission, KnowledgePathReviewContracts}.cs`: ObjectType `crm.knowledge-path-revision` (:19), şablon kodu biçimi `KP-MLR-{0}` + ayar `Crm:KnowledgePaths:Workflow` (`Infrastructure/Workflow/ConfigurationKnowledgePathReviewSettings.cs`), derin bağlantı `ReviewLink` (:31-32), karar ucu (K1, ret yorumu zorunlu), uzlaşma (reconcile).
  - Sonuç tüketicisi `Infrastructure/Eventing/ClaimWorkflowOutcomeConsumer.cs`: aynı gelen kutusu, **ObjectType'a göre yönlendirme** (:21, :54-56). Yeni tipler buraya eklenir.
  - İddialar: `Features/ContentComposition/Claims/ClaimReviewCore.cs` (`CLAIM-LOCAL-MLR-{0}`, ülke sürümü nesne tipi).
- **Canlı onay şablonları:** `KP-MLR-{CC}` / `CLAIM-LOCAL-MLR-{CC}` 3 adımlı; Regülasyon adımı pozisyonları **REG-MGR, REG-SPC** (KP-2-CFG §36.2). Platform `requestedObjectType`'ı doğrulamıyor.
- **Ülke / dil doğrulaması:** Bilgi Yolu kimliği (KP-1): `ChainContextErrors.CountryInvalid` / `LanguageNotInCountry` / `ReferenceSetUnavailable` (`Domain/Entities/ChainContextErrors.cs`), BRD `country-content-languages`. Aynı doğrulayıcıyı kullan.
- **Ürün:** MDM global product, fail-closed (strateji şablonu `IStrategyTemplateProductReferenceValidator` ve konu dış referansı deseni).
- **K2:** güvenlilik metni iddia değildir; iddia altyapısına (`Claim`) eklenmez.

## NE
1. **`SafetyText` aggregate** (koleksiyon `safety_texts`):
   - kimlik: `SafetyTextCode` (sunucu üretir; `SAF-{CC}-{sıra}`), `GlobalProductId` (+ `GlobalProductCodeDisplay`), `CountryCode`, `LanguageCode`, `Version` (int, 1'den);
   - içerik: `Body` (zorunlu, düz metin, paragraf korunur, ≤ 20 000), `ShortBody?` (≤ 2 000, dar yerleşimler için), `SourceDocumentRef?` (ör. KÜB / SmPC sürümü), `SourceDate?`, `ApprovalReference?` (yerel onay numarası, serbest);
   - durum: `draft` → `in-review` → `active` | (ret) → `draft`; `superseded`; `archived`. **Bir (ürün, ülke, dil) için en fazla bir `active`**; onaylanan aktif olur, önceki `superseded`.
   - inceleme kaydı: `SubmittedBy/At`, `WorkflowInstanceId`, `Decisions[] {by, outcome, comment, at}`; `in-review` ve sonrası **değişmez**.
   - **yeni sürüm:** yalnız aktiften (ya da superseded'dan) klon → `draft`, `Version + 1`; aynı anahtar için en fazla bir açık taslak / inceleme (409 `safety_text_open_draft_exists`).
2. **`CountryLegalProfile` aggregate** (koleksiyon `country_legal_profiles`), anahtar ülke × dil:
   - `LegalFooterText` (zorunlu), `MarketingAuthorizationHolder?` (ad + adres), `AdverseEventReportingText?`, `PromotionalNotice?`, `PageApprovalCodeFormat?` (ör. `{CC}-{YYYY}-{SEQ}`; KP-UI-3 sayfa onay kodu), `Version`;
   - aynı yaşam döngüsü, aynı tek aktif kuralı, aynı açık taslak kuralı (409 `legal_profile_open_draft_exists`).
3. **Onay (K1, tek kanal = iş akışı):**
   - gönder: MOD-0023 örneği şablon `KP-REG-{CC}` (ayar `Crm:RegulatoryTexts:Workflow:TemplateCodeFormat`, varsayılan `KP-REG-{0}`), ObjectType `crm.safety-text` / `crm.country-legal-profile`, derin bağlantı `/CRM/SafetyTexts/{id}` / `/CRM/LegalProfiles/{id}`;
   - şablon bulunamazsa 409 `review_template_missing` (KP-2 ile aynı davranış);
   - **karar ucu** (CRM → Platform approve / reject, yorumla; **ret yorumu zorunlu**); gönderen ≠ karar veren (SoD);
   - sonuç tüketicisi ObjectType'a göre yönlendirir: onay → aktif + öncekini superseded; ret → taslağa döner, karar kaydı kalır; uzlaşma (reconcile) KP-2 gibi;
   - geri çekme: `in-review` → `draft` (yalnız gönderen, örnek iptal).
4. **Okuma uçları:** liste (filtre: ürün, ülke, dil, durum), detay (sürüm geçmişi + karar kaydı), **çözümleme**:
   - `GET /api/crm/knowledge/safety-texts/resolve?productId=&countryCode=&languageCode=` → aktif sürüm ya da 404 `safety_text_missing`;
   - `GET /api/crm/knowledge/country-legal-profiles/resolve?countryCode=&languageCode=` → aktif ya da 404 `legal_profile_missing`.
   - KP-UI-3 bu iki ucu kullanacak (bu pakette tüketici yok).
5. **Doğrulama:** ürün MDM fail-closed (MDM yoksa 503 / `dependency_unavailable`, sahte kabul yok); ülke + dil KP-1 doğrulayıcısıyla (`country_invalid`, `language_not_in_country`, `reference_set_unavailable`); metin uzunlukları; boş gövde 400.
6. **Yetki anahtarları:** `crm.safety-text.read / manage / submit`, `crm.country-legal-profile.read / manage / submit`. Karar Platform görev izinleriyle (KP-2 gibi). Auth kataloğuna, CRM izinlerinin mevcut kayıt desenine göre ekle (7 dil açıklama). **Rollere verme** KP-5a-CFG'de (kullanıcı betiği).
7. **Class-map:** iki aggregate + gömülü tipler (string-Guid; `crm-new-aggregate-classmap-guid`); benzersiz kısmi index: aktif sürüm (tenant, ürün, ülke, dil) ve (tenant, ülke, dil) — kısmi filtrede `$ne` YOK (eşitlik).

## KORU / YAPMA
- **İddia altyapısı (`Claim`, ülke sürümleri, `CLAIM-*` şablonları) DEĞİŞMEZ** (K2).
- Bilgi Yolu, revizyon, MLR, yayın kuralları DEĞİŞMEZ (güvenlilik metninin yol yayınına ön koşul olması KP-UI-3'te).
- Web DOKUNMA (KP-5a-UI). Platform kodu DOKUNMA (şablonlar KP-5a-CFG'de canlı).
- Ham repository yazması yok; silme yok (arşiv).
- **DUR:** sonuç tüketicisine yeni ObjectType eklemek KP-2 / iddia akışını değiştirmeyi gerektiriyorsa; KP-1 ülke / dil doğrulayıcısı yeniden kullanılamıyorsa → raporla.

## Sözleşme (KP-5a-UI ile paylaşılan)
| Uç | Not |
|---|---|
| `GET/POST /api/crm/knowledge/safety-texts`, `GET/PUT /{id}`, `POST /{id}/new-version`, `POST /{id}/submit`, `POST /{id}/withdraw`, `POST /{id}/decision` {outcome: approve\|reject, comment}, `POST /{id}/archive`, `GET /resolve` | liste filtreleri `productId, countryCode, languageCode, status, includeArchived` |
| aynısı `/api/crm/knowledge/country-legal-profiles` | ürün yok |
| DTO alanları | yukarıdaki alanlar + `status`, `version`, `isActive`, `canEdit`, `canSubmit`, `canDecide` (çağıranın görevi var mı), `decisions[]`, `workflowInstanceId`, `reviewLink` |
| Hata kodları | `safety_text_open_draft_exists`, `legal_profile_open_draft_exists`, `review_template_missing`, `rejection_comment_required`, `not_editable`, `self_decision_forbidden`, `country_invalid`, `language_not_in_country`, `reference_set_unavailable`, `product_not_found`, `dependency_unavailable` |

## Acceptance
- **E2:** CRM 0 kırmızı (taban: ajan ölçer; son CT **2147/0/5**, KP-CH-1 birleşirse artar), Web 0 kırmızı (taban **422**; Auth / CRM sabitleri Web testlerini etkileyebilir), Auth testleri 0 kırmızı (katalog değişirse), build 0 hata.
  - **Yeni testler:** yaşam döngüsü (taslak → gönder → onay → aktif, önceki superseded; ret → taslak + karar kaydı; geri çekme); tek aktif + tek açık taslak; gönderilen değişmez; ret yorumu zorunlu; kendi kararı yasak; şablon yok → 409; ObjectType yönlendirmesi (iddia ve yol akışları etkilenmez — mevcut testler yeşil); çözümleme (aktif / yok / başka ülke-dil dönmez); tenant izolasyonu; MDM fail-closed; ülke-dil doğrulaması; class-map round-trip; index tanımı.
  - **Sabotaj:** (1) onayda önceki aktifi superseded yapma kaldırılınca tek-aktif testi kırmızı; (2) ret yorumu kontrolü kaldırılınca ilgili test kırmızı.
- **E4:** KP-5a-UI + KP-5a-CFG sonrası CT: TR / tr güvenlilik metni taslak → gönder → Regülasyon (sema) onayı → aktif → çözümleme.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-KP-5a · Güvenlilik metni (ürün × ülke × dil) + ülke yasal profili — Regülasyon onaylı ana veri (CRM)
Repository: C:\tmp\kp-5a (worktree) · Branch: wp/kp-5a · commit bu dala, push YOK · CrmService (+ Auth izin kataloğu kaydı)

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-KP-5a-safety-text-legal-profile.md — önce tamamını oku (Kanıt / NE / KORU / Sözleşme / Acceptance). Ayrıca: …/DESIGN-KP-STUDIO-knowledge-path-studio.md §2.4, §4, §8, §9.1 · …/WP-KP-2-path-revision-mlr.md (§37) + WP-KP-2-CFG-path-mlr-templates.md (§36.2: REG-MGR/REG-SPC) · services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/Path/Review/** (desen — kullan, kopyalama) · Infrastructure/Eventing/ClaimWorkflowOutcomeConsumer.cs (ObjectType yönlendirmesi) · Infrastructure/Workflow/ConfigurationKnowledgePathReviewSettings.cs · Domain/Entities/ChainContextErrors.cs + KP-1 ülke/dil doğrulayıcısı · Features/StrategyTemplate/Binding/IStrategyTemplateProductReferenceValidator.cs (MDM fail-closed) · Persistence/DependencyInjection.cs · memory crm-new-aggregate-classmap-guid, crm-classmap-rejects-unknown-elements, mongo-partial-index-ne-crash, crm-standalone-mongo-transaction-fallback.

NE: (1) SafetyText (safety_texts): SAF-{CC}-{sıra}, GlobalProductId(+display), CountryCode, LanguageCode, Version; Body (zorunlu ≤20000), ShortBody? (≤2000), SourceDocumentRef?, SourceDate?, ApprovalReference?; draft → in-review → active | ret → draft; superseded; archived; (ürün,ülke,dil) başına tek active (onay → öncekini superseded); tek açık taslak/inceleme (409 safety_text_open_draft_exists); in-review sonrası değişmez; new-version aktif/superseded'dan klon. (2) CountryLegalProfile (country_legal_profiles), ülke×dil: LegalFooterText (zorunlu), MarketingAuthorizationHolder?, AdverseEventReportingText?, PromotionalNotice?, PageApprovalCodeFormat?; aynı yaşam döngüsü/kurallar (409 legal_profile_open_draft_exists). (3) Onay K1: MOD-0023 şablonu KP-REG-{CC} (ayar Crm:RegulatoryTexts:Workflow:TemplateCodeFormat, varsayılan KP-REG-{0}), ObjectType crm.safety-text / crm.country-legal-profile, derin bağlantı /CRM/SafetyTexts/{id} · /CRM/LegalProfiles/{id}; şablon yok → 409 review_template_missing; karar ucu (Platform approve/reject + yorum, ret yorumu zorunlu, gönderen≠karar veren); ClaimWorkflowOutcomeConsumer'a ObjectType yönlendirmesi (onay → aktif + önceki superseded; ret → taslak); reconcile KP-2 gibi; withdraw. (4) Liste/detay/çözümleme uçları (WP Sözleşme tablosu): safety-texts/resolve (404 safety_text_missing), country-legal-profiles/resolve (404 legal_profile_missing). (5) Doğrulama: ürün MDM fail-closed; ülke+dil KP-1 doğrulayıcısı; uzunluklar. (6) İzin anahtarları crm.safety-text.{read,manage,submit}, crm.country-legal-profile.{read,manage,submit} → Auth kataloğuna mevcut desenle (rollere verme YOK — CFG). (7) Class-map (string-Guid) + benzersiz kısmi index'ler (eşitlik filtresi, $ne YOK).
KORU/YAPMA: Claim/ülke sürümü/CLAIM-* şablonları DEĞİŞMEZ (K2); Bilgi Yolu/revizyon/MLR/yayın DEĞİŞMEZ; Web ve Platform kodu DOKUNMA; ham repository yazması yok; silme yok.
DOĞRULA (E2): cd C:\tmp\kp-5a; CRM testleri (tabanı ölç; son CT 2147/0/5) → 0 kırmızı (bilinen sıra flake'i hariç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 422); Auth testleri katalog değiştiyse 0 kırmızı; build 0 hata. Yeni testler WP Acceptance listesi; mevcut iddia + yol inceleme testleri yeşil. Sabotaj: (1) onayda önceki aktifi superseded yapmayı kaldır → tek-aktif testi kırmızı; (2) ret yorumu kontrolünü kaldır → test kırmızı. Commit ("feat(crm): WP-KP-5a — safety text + country legal profile, regulatory-only approval via MOD-0023" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: sonuç tüketicisine yeni ObjectType eklemek KP-2/iddia akışını değiştirmeyi gerektiriyorsa ya da KP-1 ülke/dil doğrulayıcısı yeniden kullanılamıyorsa → DUR + raporla.
```
