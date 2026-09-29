# WORK PACKAGE — WP-CL-FE-5 · Bilgi İçeriği ↔ iddia bağlama (seçici · yayın kapısı hataları · detayda bağlı iddialar) (frontend)

> **CT (SoR).** İçerik → ziyaret canlı testinde (2026-09-29) bulunan boşluk:
> - BE-6 (`72fdac60`) CRM tarafında `KnowledgeContent.ClaimRefs` + yayın kapısını kurdu, ama **Web'de iddia bağlama hiç yok**.
> - `frontend/Diten.Web` içinde `claimRefs` geçmiyor. Create/Edit MVC formu `KnowledgeContentEditViewModel` → `ToPayload` ile CRM'e gidiyor ve `ClaimRefs` taşımıyor.
> - Sonuç: onaylı iddia (CLM-ALMIBA-02 + TR v1.0) içeriğe bağlanamıyor; yayın kapısı canlıda denenemiyor.
>
> **Çalışma yeri:** worktree `C:\tmp\cl-fe-5`, dal `wp/cl-fe-5`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Kanıt (CT)
- **CRM yazma** (`KnowledgeRequests.cs:33/59`): create/update `ClaimRefs: [{claimCode, claimId, countryVersionId?, countryCode?}]`.
  - `null` = saklananı koru; `[]` = temizle.
  - En çok N ref (`claim_refs_too_many`).
- **CRM okuma** (`KnowledgeContentDto.ClaimRefs`): `[{claimCode, claimId, countryVersionId, countryCode, claimStatus, countryVersionStatus, claimNeedsReview}]`. Durumlar yalnız detay okumasında dolu.
- **Kurallar** (`KnowledgeContentClaimLinks.cs`):
  - Kayıtta içerik ve iddia ürünü farklıysa `claim_product_mismatch` (ikisi de MDM Global Product id).
  - Yayında (`ContentStatus=published`; ayrı bir yayın komutu yok) ref kullanılabilir olmalı: çekirdek ref → iddia onaylı / gözden geçirilmeli; ülke ref → sürüm onaylı / gözden geçirilmeli. Değilse `claim_not_approved`.
  - Ülke ref'inde içerik dili sürümün dillerinde olmalı; değilse `claim_language_mismatch`.
  - Çekirdek ref'te dil kontrolü yok.
- **Hata kodları** (`KnowledgeContent.cs:194–203`): `claim_refs_too_many`, `claim_ref_invalid`, `claim_ref_duplicate`, `claim_not_found`, `claim_ref_mismatch`, `claim_country_version_not_found`, `claim_product_mismatch`, `claim_not_approved`, `claim_language_mismatch`, `dependency_unavailable`. Hata biçimi `[code, message]`.
- **Seçenek kaynağı (tek çağrı):** CRM `GET api/crm/content-composition/claims/coverage?productId=` → satır başına iddia + ülke hücreleri (`state`, `versionId`, `version`). Ülke dilleri Global BRD `country-content-languages` setinden (Web FE-1/FE-2 zaten okuyor: `ReadReferenceValuesAsync`, `ClaimDisplayNames`).
- **Web:**
  - `Controllers/CRM/KnowledgeController.cs` (Create/Edit POST 67/111, `ToPayload`, `ToEditModel`);
  - `Views/CRM/Knowledge/{_Form, Details}.cshtml`;
  - `wwwroot/assets/js/CRM/Knowledge/{form, details}.js`;
  - resx `KnowledgeIndex.{7}`.

## NE
1. **Seçenek ucu** — `KnowledgeController`'a `GET api/claim-options?productId=&languageCode=`.
   - İzin: içerik okuma izni (knowledge).
   - CRM coverage'ı bir kez okur; ülke dillerini BRD'den alır.
   - Döner: `[{claimId, claimCode, claimName, kind, countryVersionId?, countryCode?, countryName?, version, status, languages[], usable, reason?}]`.
     - Satırlar: her iddia için **çekirdek satırı** + sürümü olan her ülke için **ülke satırı**.
     - `usable=false` nedenleri: `not_approved` (onaylı ya da gözden geçirilmeli değil), `language_mismatch` (ülke satırında içerik dili yok).
   - `productId` boşsa boş liste + "önce ürün seçin".
   - CRM kapalıysa `{disabled:true, reason}` döner, sessiz boş liste YOK (mevcut `global-product-options` deseni).
2. **Formda "İddialar" bölümü** (Ürün alanının yakınında) — Create + Edit:
   - Çoklu seçim (select2). Seçenek metni: "CLM-ALMIBA-02 · Etki mekanizması — Çekirdek · Onaylı" / "— Türkiye v1.0 · Onaylı · tr".
   - Kullanılamaz seçenekler seçilebilir ama uyarı rozetli: taslak içerik taslak iddiayı bağlayabilir, kapı yayında devreye girer. Yardım metni: "Yayınlamak için bağlı iddialar onaylı olmalı; ülke sürümünün dili içerik diliyle aynı olmalı."
   - Ürün ya da dil değişince seçenekler yeniden yüklenir. Artık uymayan seçili ref'ler **silinmez**; uyarıyla işaretlenir.
   - Model bağlama: `ClaimRefs[i].ClaimId / ClaimCode / CountryVersionId / CountryCode` gizli alanları.
   - `ToPayload` her zaman listeyi gönderir (boşsa `[]`); Edit formu mevcut ref'leri yüklediği için bu doğru.
   - `ToEditModel` DTO'dan doldurur.
3. **Hatalar:** CRM'in iddia hata kodları "İddialar" bölümünün altında kullanıcı dilinde (ModelState → alan hatası). Genel "işlem başarısız" değil. `claim_not_approved` / `claim_language_mismatch` mesajı hangi iddiayı işaret ettiğini söyler (CRM mesajındaki kodu kullan).
4. **Detay sayfası:** "Bağlı iddialar" kartı.
   - Satırlar: kod · ad · Çekirdek / Ülke adı + sürüm · durum rozeti (onaylı / incelemede / taslak / gözden geçirilmeli) · `claimNeedsReview` uyarısı.
   - Her satır iddiaya bağlanır: çekirdek → `/CRM/Claims/Edit/{id}`, ülke → `/CRM/Claims/CountryVersions/{versionId}/Edit`. Bu bağlantı yalnız `crm.claim.read` varsa; yoksa düz metin.
5. **L10n:** `KnowledgeIndex` resx 7 dil (bölüm, yardım metni, rozetler, 10 hata kodu).

## KORU / YAPMA
- CRM, Platform, Auth DOKUNMA. Claims ekranları (`CRM/Claims/**`) DOKUNMA.
- İçerik formunun diğer alanları ve davranışı DEĞİŞMEZ.
- Yayın kapısı istemci tarafında TEKRARLANMAZ: yalnız uyarı gösterilir, karar CRM'in.
- Tarayıcıda ayrı sekme, canlı yazma YOK (E4'ü CT yapar).
- **DUR:** coverage satırında ülke sürümü dışında gerekli bir bilgi eksikse (ör. sürüm dilleri ülke dillerinden farklı olabiliyorsa) → sürüm başına CRM çağrısı yapma, raporla.

## Acceptance
- **E2:**
  - Web testleri 0 kırmızı (taban 305). Web build 0 hata. `node --check` temiz.
  - Yeni testler:
    - `claim-options` (çekirdek + ülke satırları, `usable` / `reason`, dil uyumsuzluğu, ürünsüz boş, CRM kapalıyken `disabled`);
    - `ToPayload` `ClaimRefs` (dolu / boş `[]`);
    - `ToEditModel` round-trip;
    - hata kodlarının alan hatasına eşlenmesi;
    - L10n 7 dil.
  - **Sabotaj:** `ToPayload`'dan `ClaimRefs` çıkarılınca test kırmızı.
- **E4 (CT):**
  - ALMIBA TR içeriği + CLM-ALMIBA-02 TR v1.0 → yayın geçer.
  - ru içerik + TR ref → `claim_language_mismatch`.
  - taslak iddia ref'i + yayın → `claim_not_approved`.
  - iddia listesinde kullanım sayısı 1.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-CL-FE-5 · Bilgi İçeriği ↔ iddia bağlama (seçici · yayın kapısı hataları · detayda bağlı iddialar) (frontend)
Repository: C:\tmp\cl-fe-5 (worktree) · Branch: wp/cl-fe-5 · commit bu dala, push YOK

Amaç: BE-6 CRM'de KnowledgeContent.ClaimRefs + yayın kapısını kurdu ama Web'de iddia bağlama yok (MVC form ClaimRefs taşımıyor). İçerik formuna iddia seçici, CRM kapı hatalarını anlaşılır gösterme ve detayda bağlı iddialar kartı.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-FE-5-knowledge-content-claim-picker.md · …/WP-CL-BE-6-claims-usage-content-links.md · services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/{KnowledgeDtos.cs [KnowledgeContentClaimRefInput/Dto], Content/KnowledgeContentClaimLinks.cs} · Diten.CrmService.Domain/Entities/KnowledgeContent.cs (194–203 hata kodları) · Diten.CrmService.Api/Models/CRM/KnowledgeRequests.cs · frontend/Diten.Web/Controllers/CRM/{KnowledgeController.cs [Create/Edit POST, ToPayload, ToEditModel, global-product-options deseni], ClaimsController.V2.cs [ReadReferenceValuesAsync country-content-languages], ClaimDisplayNames.cs} · Views/CRM/Knowledge/{_Form, Details}.cshtml · wwwroot/assets/js/CRM/Knowledge/{form, details}.js · Resources/Views/CRM/Knowledge/KnowledgeIndex.*.resx · memory l10n-bridge-pascalcase-loader.

NE:
 1) KnowledgeController GET api/claim-options?productId=&languageCode= (knowledge okuma izni): CRM claims/coverage?productId= tek çağrı + BRD country-content-languages → [{claimId, claimCode, claimName, kind, countryVersionId?, countryCode?, countryName?, version, status, languages[], usable, reason?}] (çekirdek satırı + sürümü olan her ülke satırı; reason not_approved | language_mismatch); ürünsüz → boş; CRM kapalı → {disabled:true, reason} (sessiz boş YOK).
 2) _Form "İddialar" bölümü (Create+Edit): select2 çoklu; metin "CLM-… · ad — Çekirdek · Onaylı" / "— Türkiye v1.0 · Onaylı · tr"; kullanılamaz seçenek seçilebilir ama uyarı rozetli + yardım metni; ürün/dil değişince seçenekleri yeniden yükle, seçili uymayanı silme (uyar); gizli ClaimRefs[i].{ClaimId, ClaimCode, CountryVersionId, CountryCode}; ToPayload her zaman liste ([] dahil); ToEditModel DTO'dan.
 3) CRM iddia hata kodlarını (claim_refs_too_many, claim_ref_invalid, claim_ref_duplicate, claim_not_found, claim_ref_mismatch, claim_country_version_not_found, claim_product_mismatch, claim_not_approved, claim_language_mismatch, dependency_unavailable) "İddialar" bölümünde kullanıcı dilinde alan hatası olarak göster.
 4) Details "Bağlı iddialar" kartı: kod · ad · Çekirdek/ülke+sürüm · durum rozeti · claimNeedsReview uyarısı; bağlantı çekirdek → /CRM/Claims/Edit/{id}, ülke → /CRM/Claims/CountryVersions/{versionId}/Edit (yalnız crm.claim.read varsa, yoksa düz metin).
 5) KnowledgeIndex resx 7 dil.
KORU/YAPMA: CRM/Platform/Auth ve CRM/Claims/** DOKUNMA; içerik formunun diğer alanları DEĞİŞMEZ; yayın kapısı istemcide tekrarlanmaz (yalnız uyarı); tarayıcıda ayrı sekme, canlı yazma YOK.
DOĞRULA (E2): cd C:\tmp\cl-fe-5; dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 305); Web build 0 hata; node --check temiz; yeni testler: claim-options (satırlar, usable/reason, dil uyumsuzluğu, ürünsüz boş, CRM kapalı disabled), ToPayload ClaimRefs (dolu/[]), ToEditModel round-trip, hata kodu → alan hatası, L10n 7 dil. Sabotaj: ToPayload'dan ClaimRefs çıkar → kırmızı. Commit ("feat(crm): WP-CL-FE-5 — knowledge content claim picker, publish-gate errors, linked claims card" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: coverage ülke sürümü dillerini ülke dillerinden farklı tutabiliyorsa (sürüm başına CRM çağrısı gerekecekse) DUR + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-09-29) — **ACCEPTED (E2)**
- **Commit:** `a289ca2c` (`wp/cl-fe-5`) → `test/crm-content-visit-e2e` fast-forward. 14 dosya, yalnız `frontend/`.
- **Diff (K13 okuma):**
  - `ToPayload` Create/Edit ikisinde de `ClaimRefs` (her zaman liste; `[]` temizler).
  - `ToEditModel` DTO'dan dolduruyor. CRM `KnowledgeMapper` `ClaimRefs`'i her zaman liste döndürüyor → null riski yok.
  - `claim-options` knowledge okuma izniyle; iddia bağlantısı yalnız `crm.claim.read` ile.
  - Gizli alanlar `esc()` ile basılıyor.
  - CRM, Platform, Auth ve `CRM/Claims/**` dokunulmamış.
- **CT testleri:** Web **333/0** (305 + 28). `form.js` `node --check` temiz.
- **CT sabotajı:** Edit `ToPayload`'dan `ClaimRefs` çıkarıldı → `KnowledgeClaimRefs` 2 kırmızı. Kod geri alındı.
- **Ajan notları (kabul):**
  - Ürünü olmayan iddialar seçicide çıkmıyor (coverage `productId` filtresi). Ayrı iş.
  - Onaydan sonra BRD ülke dilleri değişirse seçici uyarısı CRM kararından sapabilir; karar CRM'de.
- **E4:** CT, fleet restart sonrası KC-2026-6CD926 üzerinde yayın kapısı senaryoları.
