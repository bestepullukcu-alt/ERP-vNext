# WORK PACKAGE — WP-KP-5a-FIX-1 · Güvenlilik metni / yasal profil: canlı test (E4) bulguları

> **CT (SoR), 2026-10-02.**
> - **Kaynak:** WP-KP-5a-CFG §36.2 madde 7-8 + §37 (canlı E4: `SAF-TR-0001` sema onayıyla aktif) ve WP-KP-5a §37 (eşzamanlı taslak).
> - **Kullanıcı (2026-10-02):** "paketle". Test kaydı `SAF-TR-0001` canlıda **kalır** (KP-UI-3 testleri için; gerçek metin gelince "Yeni sürüm"). Bu paket veriye dokunmaz.
> - **Kapsam:** CrmService + `frontend/Diten.Web` (küçük). Merkezi log (audit) bu pakette YOK (ayrı karar bekliyor).
>
> **Çalışma yeri:** worktree `C:\tmp\kp-5a-fix-1`, dal `wp/kp-5a-fix-1`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Bulgular ve kanıt (CT)
1. **Yanlış gerekçe kodu (denetim etiketi):** ortak iş akışı başlatma istemcisi her örneği iddia koduyla başlatıyor — `Infrastructure/Workflow/GatewayClaimWorkflowClient.cs:63` `reasonCode = ClaimReviewRules.SubmitReasonCode` (`CRM_CLAIM_SUBMITTED`).
   - Güvenlilik metni / yasal profil kendi kodunu tanımlıyor ama geçmiyor: `Features/Knowledge/Regulatory/RegulatoryTextCore.cs:73` `SubmitReason => ReasonPrefix + "_SUBMITTED"`.
   - **Aynı hata Bilgi Yolu'nda da var:** `Features/Knowledge/Path/Review/KnowledgePathReviewCore.cs:20` `SubmitReasonCode = "CRM_KNOWLEDGE_PATH_SUBMITTED"` tanımlı, gönderilmiyor (KP-2'den beri).
2. **Ham kullanıcı kimliği:** ayrıntı sayfasında "Gönderen" ve karar kaydında karar veren `c5769c62-…` / `d27fa4a6-…` olarak görünüyor.
   - Hazır seam: `Application/Common/IUserDisplayNameResolver.cs` (tek toplu çağrı, kiracıya bağlı, fail-closed: çözülemeyen kimlik sonuçta yok); uygulama `AuthUserDisplayNameClient` (DI :186); kullananlar `GetSegmentByIdHandler`, `GetVisitFrequencyPolicyAnalysisHandler`. **Kullan, kopyalama.**
3. **İncelemedeyken "Arşivle" düğmesi:** `frontend/Diten.Web/wwwroot/assets/js/CRM/RegulatoryTexts/details.js:167` `if (canManage && s !== 'archived')` → `in-review`'da da görünüyor; CRM 409 `not_editable` veriyor.
4. **Eşzamanlı ikinci taslak 500:** iki istek aynı anahtar için aynı anda taslak açarsa `OpenKey` benzersiz index'i ikinciyi reddediyor ama yanıt 500. `Persistence/Repositories/RegulatoryTextRepositories.cs:52` `InsertOneAsync` yarış durumunu yakalamıyor (aynı dosyada :67 başka bir yazmada `DuplicateKey` yakalama deseni var).

## NE
1. **Gerekçe kodu:** `ClaimWorkflowStartRequest`'e isteğe bağlı `ReasonCode` (varsayılan bugünkü iddia kodu → iddia akışı değişmez); istemci onu gönderir.
   - Güvenlilik metni → `CRM_SAFETY_TEXT_SUBMITTED`, yasal profil → kendi `SubmitReason`'ı (ör. `CRM_COUNTRY_LEGAL_PROFILE_SUBMITTED`);
   - **Bilgi Yolu revizyonu → `CRM_KNOWLEDGE_PATH_SUBMITTED`** (aynı kök, birlikte düzelt).
2. **Adlar:** güvenlilik metni / yasal profil detay DTO'suna `createdByName?`, `submittedByName?`, `decisions[].byName?` (ve varsa `updatedByName?`). `IUserDisplayNameResolver` ile **tek toplu çağrı**; çözülemeyen kimlik → ad alanı `null` (kimlik uydurulmaz). Liste DTO'su değişmez (liste ad göstermiyor ise).
   - Web: ayrıntıda ad varsa adı, yoksa **ham kimlik yerine** yerelleştirilmiş "Bilinmeyen kullanıcı" + tarih göster (7 dil).
3. **Arşiv düğmesi:** CRM DTO'ya `canArchive` (yalnız `draft`, `active`, `superseded` için true; `in-review` ve `archived` false — CRM'in gerçek kuralından türet, kopyalama). Web düğmeyi bu bayrakla gösterir (diğer `canEdit / canSubmit / canDecide` gibi).
4. **Eşzamanlı taslak:** yeni taslak / yeni sürüm yazımında `DuplicateKey` → 409 `safety_text_open_draft_exists` / `legal_profile_open_draft_exists` (uygulama içi ön kontrolün verdiği kodla aynı).

## KORU / YAPMA
- İddia akışının gerekçe kodu ve davranışı DEĞİŞMEZ (varsayılan korunur; mevcut iddia testleri yeşil).
- Canlı veri / RBAC / şablonlara DOKUNMA. `SAF-TR-0001` kalır.
- Ad çözümlemesi için Auth'a yeni uç açma; mevcut seam. Kişisel veri olarak yalnız görünen ad.
- Merkezi log (audit) YOK.
- **DUR:** `IUserDisplayNameResolver` CRM'de kiracı kullanıcıları için çalışmıyorsa (S2S yapılandırması eksik vb.) → raporla.

## Acceptance
- **E2:** CRM 0 kırmızı (taban **2180/0/5**; bilinen PiiMasking sıra flake'i hariç), Web 0 kırmızı (taban **469/0**), build 0 hata.
  - **Yeni testler:** başlatma isteği türün gerekçe kodunu taşıyor (güvenlilik metni, yasal profil, Bilgi Yolu) ve iddia kodu aynı kalıyor; ad çözümlemesi tek toplu çağrı + çözülemeyen → `null`; `canArchive` durum tablosu; eşzamanlı taslakta 409 (sahte depoda `DuplicateKey`); Web: `canArchive` false iken düğme yok, ad yoksa ham kimlik görünmüyor.
  - **Sabotaj:** (1) istemcide `ReasonCode`'u yok say → gerekçe kodu testi kırmızı; (2) `DuplicateKey` yakalamayı kaldır → 409 testi kırmızı.
- **E4 (CT):** `SAF-TR-0001` ayrıntısında karar veren "sema …" adıyla; yeni bir taslak (ör. TR / en) onaya gönderildiğinde Platform görevinin gerekçe kodu `CRM_SAFETY_TEXT_SUBMITTED`; incelemede "Arşivle" yok.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-KP-5a-FIX-1 · Güvenlilik metni / yasal profil: canlı test (E4) bulguları
Repository: C:\tmp\kp-5a-fix-1 (worktree) · Branch: wp/kp-5a-fix-1 · commit bu dala, push YOK · CrmService + frontend/Diten.Web (küçük)

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-KP-5a-FIX-1-regulatory-text-e4-findings.md — önce tamamını oku (Bulgular ve kanıt / NE / KORU / Acceptance). Ayrıca: …/WP-KP-5a-CFG-regulatory-templates.md (§36.2 madde 7-8, §37) · …/WP-KP-5a-safety-text-legal-profile.md (§37) · services/Diten.CrmService/src/Diten.CrmService.Infrastructure/Workflow/GatewayClaimWorkflowClient.cs · Application/Features/Knowledge/Regulatory/** · Application/Features/Knowledge/Path/Review/** · Application/Common/IUserDisplayNameResolver.cs (+ GetSegmentByIdHandler kullanım deseni) · Persistence/Repositories/RegulatoryTextRepositories.cs · frontend/Diten.Web/wwwroot/assets/js/CRM/RegulatoryTexts/details.js + Resources (RegulatoryTexts resx 7 dil).

NE: (1) ClaimWorkflowStartRequest'e isteğe bağlı ReasonCode (varsayılan iddia kodu); güvenlilik metni CRM_SAFETY_TEXT_SUBMITTED, yasal profil kendi SubmitReason'ı, Bilgi Yolu revizyonu CRM_KNOWLEDGE_PATH_SUBMITTED gönderir. (2) Detay DTO'larına createdByName?/submittedByName?/decisions[].byName? — IUserDisplayNameResolver tek toplu çağrı, çözülemeyen → null; Web ad yoksa ham kimlik değil yerelleştirilmiş "Bilinmeyen kullanıcı" (7 dil). (3) CRM DTO canArchive (CRM kuralından türet; in-review/archived false); Web Arşivle düğmesini canArchive ile gösterir. (4) Taslak/yeni sürüm yazımında DuplicateKey → 409 safety_text_open_draft_exists / legal_profile_open_draft_exists.
KORU/YAPMA: iddia gerekçe kodu ve davranışı değişmez; canlı veri/RBAC/şablon DOKUNMA (SAF-TR-0001 kalır); Auth'a yeni uç yok; merkezi log YOK.
DOĞRULA (E2): cd C:\tmp\kp-5a-fix-1; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 2180/0/5; PiiMasking flake'i hariç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 469); build 0 hata. Yeni testler WP Acceptance. Sabotaj: (1) istemcide ReasonCode'u yok say → kırmızı; (2) DuplicateKey yakalamayı kaldır → kırmızı; geri al. TestResults/*.trx izleniyor, klasörü silme. Commit ("fix(crm): WP-KP-5a-FIX-1 — workflow reason code per kind, display names, canArchive, concurrent draft 409" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: IUserDisplayNameResolver kiracı kullanıcıları için çalışmıyorsa → DUR + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-10-02) — **ACCEPTED (E2)**
- **Commit:** ajan `9d43c460` → DEC-SCMM-05 (`13909ef4`) üzerine rebase (çakışmasız) → `test/crm-content-visit-e2e` fast-forward. 31 dosya (+355 / −51).
- **DUR yok:** ad çözücü kiracı kullanıcılarını çözüyor (ajan canlı: "sema pullukcu", "Admin User").
- **Diff (K13 okuma):**
  - `GatewayClaimWorkflowClient`: `reasonCode = request.ReasonCode ?? iddia kodu` — iddia varsayılanı korunuyor; güvenlilik metni / yasal profil / **Bilgi Yolu** kendi kodunu gönderiyor.
  - Adlar: `IUserDisplayNameResolver` tek toplu çağrı; çözülemeyen → `null`; Web'de "Bilinmeyen kullanıcı" (7 dil), ham kimlik yok.
  - Arşiv kuralı tek yerde `RegulatoryText.IsArchivable()` (arşivsiz + incelemede değil); `ArchiveAsync` ve DTO `canArchive` aynı kuraldan.
  - Depo `InsertAsync` `DuplicateKey` → `RegulatoryTextKeyConflictException` → yaşam döngüsünde 409.
- **CT testleri:** CRM **2187/0/5**, Web **473/0**.
- **CT sabotajı:** `IsArchivable`'dan "incelemede değil" koşulu kaldırıldı → 2 kırmızı. Kod geri alındı. Ajan: `ReasonCode` yok sayma (3) + çakışma yakalama (1).
- **Bilinen sınır:** Mongo `DuplicateKey` çevirisi birim testte sahte depo üzerinden taklit ediliyor (projede Mongo testi yok).
- **E4 (CT, CRM + Web yeniden başlatma sonrası):** `SAF-TR-0001` ayrıntısında karar veren adıyla; yeni taslak (TR / en) onaya gönderilince Platform görev gerekçe kodu `CRM_SAFETY_TEXT_SUBMITTED`; incelemede "Arşivle" yok.

### §37 ek — E4 (CT, canlı, 2026-10-02) — **ACCEPTED (E4)**
- Kullanıcı fleet'i güncel dalla yeniden başlattı ve giriş yaptı; CT yerleşik tarayıcıda, ayrı sekmede.
- **1. Adlar:** `SAF-TR-0001` v1 ayrıntısı: "Gönderen **Admin User**", karar kaydı "Onaylandı · **sema pullukcu**" — ham kimlik yok. ✓
- **2. Gerekçe kodu:** v1'den "Yeni sürüm" → v2 taslak (`ffc1455d…`) → onaya gönder → Platform örneği `KP-REG-TR`, `ObjectType crm.safety-text`; `approval_tasks.ReasonCode` ve `workflow_transition_logs.ReasonCode` = **`CRM_SAFETY_TEXT_SUBMITTED`** (Mongo, salt okuma). ✓ (TR için yalnız `tr` dili tanımlı olduğundan TR / en yerine v2 kullanıldı.)
- **3. Arşivle düğmesi:** incelemedeki v2'de eylemler yalnız "Geri çek"; Onayla / Reddet gönderene gizli. Taslağa dönünce "Düzenle / Onaya gönder / Arşivle". ✓
- **Temizlik:** v2 geri çekildi (örnek + görev iptal, durum 6) ve arşivlendi; v1 `active` (KP-UI-3 testleri için kalır). Platform'da v2 için tek örnek (çift gönderim yok).
- **Gözlem (ürün hatası değil):** tarayıcı paneli arka plandayken SweetAlert kapanma animasyonu tamamlanmıyor, eski onay penceresi DOM'da kalıyor — sayfa yenilenince düzeliyor.
