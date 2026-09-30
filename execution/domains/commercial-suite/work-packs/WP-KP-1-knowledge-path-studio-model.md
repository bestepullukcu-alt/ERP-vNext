# WORK PACKAGE — WP-KP-1 · Bilgi Yolu Stüdyosu — model (zincir + kimlik + yerleşim + iddialar)

> **CT (SoR).** Tasarım: `DESIGN-KP-STUDIO-knowledge-path-studio.md` §2, §3.1, §4 (D-KP-1/2/3/7), §6. Karar: bridge-decision §8. Mockup: `mockups/kp-studio/` (v2 kabul).
>
> **Kapsam:** yalnız **CrmService** (Domain / Application / Api / Persistence + test).
> - Revizyon / MLR (KP-2), çıktı / yayın (KP-3), set emekliliği (KP-4) ve Web (KP-UI-1) **YOK**.
> - Mevcut yayın ucu bu pakette **değişmez**.
>
> **Çalışma yeri:** worktree `C:\tmp\kp-1`, dal `wp/kp-1`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Amaç
Bilgi Yolu'nu, İçerik Seti'nin yerini alacak şekilde genişletmek:
- zincire bağlı;
- ülke + dil kimlikli;
- ürün + kitle zincirden türetilen;
- adımları zincirin dal / adımlarına yerleşen ve dal-öncelikli sıralanan;
- iddiaları yolda taşıyan.

Zincirsiz mevcut yollar "onaysız eski yol" olarak işaretlenir ve çalışmaya devam eder.

## Kanıt (CT)
- **`KnowledgePath`** (`Domain/Entities/KnowledgePath.cs`): `PathCode`, `SubjectId`, `TopicId?`, `AudienceProfileId?`, `LanguageCode?`, `PathVersion`, `PathStatus`, `Steps[]`, `StepSetFrozenAt`, `SupersedesPathId`, `StudioOrigin?` (SB-2).
- **`KnowledgePathStep`:** `StepOrder`, `StepCode`, `StepType`, `ContentId`, `IsRequired`, `PrerequisiteStepId`, `ConceptNodeId`, `EstimatedDurationMinutes`, `BranchConditions`, `StepStatus`. Adımdaki içerik yayında olmalı (`KnowledgePathStepCommandHandlers.cs:78`). Yayında adım seti donar (V-S02).
- **API** (`KnowledgePathsController`):
  - `paths` GET / POST / PUT, `publish`, `new-version`, `archive`;
  - `steps` GET / POST / PUT / archive.
- **Taşınacak yardımcılar (kopyalama değil, ORTAKLAŞTIR):**
  - `ContentComposition/ContentSets/ContentSetContext.cs` → `ContentSetContextResolver` + `ContentSetContextValidation` (SB-1R: ülke COUNTRY_CODES, dil ülkenin `country-content-languages` dillerinde, ürün zincir konusunun birincil `global-product` referansından, kitle ForWhom'dan; BRD kapalıysa 503).
  - `ContentSetRevisions/ContentSetPathOrder.cs` (SB-2 dal-öncelikli sıra).
  - Yeni ortak yer: `Features/Knowledge/Chain/` (ör. `ChainContextResolver`, `ChainArrangementOrder`).
  - İçerik Seti KP-4'e kadar yaşadığı için set de ortak yardımcıyı kullanır.
- **İddia kuralları:** `KnowledgeContentClaimLinks` (BE-6: ürün uyumu, kullanılabilirlik, dil), `ContentSetReleaseProducer`'daki ülke sürümü seçimi (kapsama önceliği: approved › review-required › in-review › draft).
- **Class-map tuzakları:** memory `crm-new-aggregate-classmap-guid`, `crm-classmap-rejects-unknown-elements`.

## NE

### 1) Kimlik ve bağlam (yol)
- **Yeni alanlar:** `ChainTemplate {ConceptChainTemplateId, ChainVersion}` (null = eski yol) ve `CountryCode`. `LanguageCode` mevcut alan kullanılır.
- **Bağlama:**
  - **Oluştururken:** zincir + ülke + dil verilebilir. Verilirse üçü birlikte zorunlu.
  - **Mevcut zincirsiz bir yola:** yeni **`POST paths/{id}/bind-chain`** `{chainTemplateId, countryCode, languageCode}` ile bir kez bağlanır. Eski yol sihirbazının arka ucu budur; yalnız **taslak** yolda çalışır.
- **Doğrulama** (SB-1R kodları):
  - `country_invalid`, `language_not_in_country`, `reference_set_unavailable` (503);
  - zincir yayında ve arşivsiz olmalı (400 `chain_template_invalid`);
  - `SubjectId` zincirin konusu olur. Farklıysa: oluşturmada zincirinki alınır; bağlamada 409 `chain_subject_mismatch`.
- **K3 / D-KP-3 — kimlik kilidi:** zincire bağlı bir yolda `CountryCode` ve `LanguageCode` bir daha değişmez. Update denemesi 409 `path_identity_locked`.
- **Yeni sürüm:** kimlik aynen kopyalanır. Zincir ref'i de kopyalanır; zincir yükseltme bu pakette YOK.
- **Türetilmiş bağlam** (ortak resolver): `{productId, productCode, productName, audienceProfileIds}`. Tek ForWhom varsa `AudienceProfileId` alanına da yazılır (mevcut filtreler çalışsın).
- **Eski yol işareti:** `IsLegacyUnapproved` = `ChainTemplate == null` (türetilir, saklanmaz). DTO'da döner. Veri yazılmaz, göç yok.

### 2) Adım yerleşimi (zincire bağlı yolda)
- `KnowledgePathStep.Arrangement {ChainStepId (= ConceptTypeId), BranchCode, Position}`.
- **Adım ekleme / güncelleme:**
  - `Arrangement` zorunlu; zincirde olmayan dal / adım → 400 `chain_slot_invalid`;
  - adımın en çok öğe sınırı doluysa → 409 `chain_slot_full`.
- **İçerik dili:** yolun dili ile aynı olmalı → 409 `component_language_mismatch`. Zincirsiz eski yolda mevcut çapraz dil davranışı değişmez.
- **Sıra:** `StepOrder` sunucuda hesaplanır: **dal-öncelikli** (dal `SortOrder` → dalın adım listesi → `Position`). İstemcinin verdiği `StepOrder` yok sayılır. `Position` ile yeniden sıralama için mevcut güncelleme ucu kullanılır.
- **D-KP-7 — zincir iskeleti:** dal / adım yaratılmaz, zincir dışına yerleştirilmez. Adım, **dallar arasında taşınamaz** (Arrangement'ın dal / zincir adımı değişirse 409 `chain_slot_move_forbidden`; yalnız `Position` değişir).
- **Uyum özeti** (okuma): her zincir slotu için `{branchCode, chainStepId, name, count, min, max, status: ok | under | over}`.

### 3) Yolda iddialar
- Yeni `KnowledgePath.Claims[] {ClaimId, ClaimCode, Arrangement}` ve uçlar:
  - `POST paths/{id}/claims` `{claimId, arrangement}`;
  - `POST paths/{id}/claims/{claimId}/arrange` `{position}`;
  - `POST paths/{id}/claims/{claimId}/remove`.
  - Yalnız taslakta; donmuş yolda 409 (V-S02 deseni).
- **Kurallar:**
  - iddia tenant'ta olmalı (404);
  - iddianın ürünü yolun türetilmiş ürünü olmalı (409 `claim_product_mismatch`);
  - aynı iddia iki kez eklenemez (409 `claim_ref_duplicate`);
  - slot `chain_slot_invalid` kuralı iddialar için de geçerli.
- **Ülkede sürümü olmayan / onaysız iddia EKLENEBİLİR.** Okumada engelleyici işaret taşır, karar yayında (KP-3).
- **Okuma** (yol detayı): her iddia için `{claimCode, name, text, qualifier, countryVersionId?, countryVersion?, status, usable, reason: not_approved | no_country_version | language_mismatch}`.
  - Ülke sürümü, kapsama önceliğiyle yolun ülkesinden çözülür.
  - `text` / `qualifier` ülke sürümünün **yolun dilindeki** metni (KP-UI-1 kurgu kartı için; mockup kalan #8).
- Zincirsiz eski yola iddia eklenemez (409 `chain_template_required`).

### 4) Kod öneki
- Yeni yol kodu `KP-…` (mevcut öneri korunur; mockup kalan #5).
- `BY-` gibi ISO ülke koduyla başlayan önek üretilmez.

### 5) DTO / okuma
- Yol DTO'su şunları taşır:
  - `chainTemplate {id, code, name, version}`, `countryCode`, `languageCode`;
  - `derivedContext`;
  - `isLegacyUnapproved`, `identityLocked`;
  - `claims[]` (§3 okuma), `chainConformance[]` (§2).
- Adım DTO'su `arrangement` taşır.
- Liste okuması en az `countryCode`, `languageCode`, `chainTemplateCode`, `isLegacyUnapproved` taşır (KP-UI-1 filtreleri).

## KORU / YAPMA
- **Yayın ucu, `new-version` mantığı (kimlik kopyası hariç), `archive`, yolculuk / ziyaret DEĞİŞMEZ.**
- **Revizyon / MLR YOK** (KP-2). Onay gerektiren hiçbir kural yok; yayın kapısı KP-3.
- İçerik Seti ve SB-2 kodu KALIR (KP-4 kaldırır). Yalnız ortak yardımcıya geçer, davranışı değişmez; mevcut set testleri yeşil kalır.
- **Zincirsiz eski yollar bugünkü gibi çalışır** (oluştur / düzenle / adım / yayın). Yeni kurallar yalnız zincire bağlı yollarda.
- Web / Platform / Auth DOKUNMA. Ham repository yazması yok. Yeni alanlar class-map'e (GUID → string).
- **DUR:**
  - `ConceptChainTemplate`'te dal olmadan (eski düz şablon) slot tanımı tek anlamlı değilse;
  - mevcut adım "tipi / kavram düğümü" alanlarının zincir adımıyla çelişen bir kullanımı varsa: raporla.

## Acceptance
- **E2:**
  - CRM testleri 0 kırmızı (taban 2118/0/5; bilinen sıra flake'i hariç). Build 0 hata.
  - Yeni testler:
    - zincirli oluşturma + doğrulama kodları;
    - `bind-chain` (taslak, bir kez, konu uyuşmazlığı);
    - kimlik kilidi 409;
    - yeni sürüm kimlik kopyası;
    - `chain_slot_invalid` / `chain_slot_full` / `chain_slot_move_forbidden`;
    - dil uyuşmazlığı 409 (zincirli) ve eski yolda eski davranış;
    - dal-öncelikli `StepOrder`;
    - iddia ekleme / sıralama / çıkarma;
    - `claim_product_mismatch` / duplicate / `chain_template_required`;
    - iddia okumasında ülke sürümü çözümü + yolun dilindeki metin;
    - uyum özeti;
    - `IsLegacyUnapproved`;
    - class-map round-trip;
    - İçerik Seti testleri ortak yardımcıyla yeşil.
  - **Sabotaj:** dal-öncelikli sıra, kimlik kilidi ve slot doğrulaması testleri kırmızıya dönmeli.
- **E4:** Web yok (KP-UI-1). CT API ile kontrol eder: ALMIBA zinciri yayınlanınca zincirli TR / tr yol + adım + iddia.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-KP-1 · Bilgi Yolu Stüdyosu — model (zincir + kimlik + yerleşim + iddialar)
Repository: C:\tmp\kp-1 (worktree) · Branch: wp/kp-1 · commit bu dala, push YOK

Amaç: DESIGN-KP-STUDIO §2/§3.1: Bilgi Yolu zincire bağlı + ülke/dil kimlikli + ürün/kitle zincirden türetilen + adımları zincir slotlarına yerleşen (dal-öncelikli sıra) + iddiaları taşıyan hale gelsin. Zincirsiz mevcut yollar "onaysız eski yol" işaretiyle eskisi gibi çalışsın. Yalnız CrmService; revizyon/MLR/yayın/Web/set emekliliği YOK.

Önce oku: execution/domains/commercial-suite/work-packs/WP-KP-1-knowledge-path-studio-model.md · …/DESIGN-KP-STUDIO-knowledge-path-studio.md · …/mockups/kp-studio/KP-STUDIO-mockup-analysis.md (v2 kalanları #5 #8) · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/{KnowledgePath, ConceptChainTemplate, ContentSet}.cs · Application/Features/Knowledge/Path/** · Application/Features/ContentComposition/ContentSets/ContentSetContext.cs · ContentSetRevisions/{ContentSetPathOrder, ContentSetReleaseProducer}.cs · Features/Knowledge/Content/KnowledgeContentClaimLinks.cs · Api/Controllers/CRM/KnowledgePathsController.cs + Models · Persistence/DependencyInjection.cs · memory crm-new-aggregate-classmap-guid, crm-classmap-rejects-unknown-elements.

NE:
 1) Kimlik/bağlam: ChainTemplate{id, version} (null = eski yol) + CountryCode (LanguageCode mevcut). Oluşturmada zincir+ülke+dil birlikte (verilirse üçü zorunlu); zincirsiz taslak yola bir kez POST paths/{id}/bind-chain. Doğrulama: country_invalid / language_not_in_country / reference_set_unavailable(503) / chain_template_invalid (yayında+arşivsiz) / SubjectId = zincir konusu (oluşturmada alınır, bağlamada 409 chain_subject_mismatch). Kimlik kilidi: zincirli yolda ülke/dil değişmez (409 path_identity_locked); new-version kimliği + zincir ref'ini kopyalar. Türetilmiş bağlam ortak resolver'dan (tek ForWhom → AudienceProfileId'ye de yaz). IsLegacyUnapproved türetilir (saklanmaz, göç yok).
 2) Adım yerleşimi (zincirli yol): Step.Arrangement{ChainStepId, BranchCode, Position} zorunlu; chain_slot_invalid (400) / chain_slot_full (409, max) / chain_slot_move_forbidden (409, dal/adım değişemez, yalnız Position); içerik dili = yol dili (409 component_language_mismatch; eski yolda eski davranış); StepOrder sunucuda dal-öncelikli hesaplanır (istemcininki yok sayılır); okumada chainConformance[] {branchCode, chainStepId, name, count, min, max, status ok|under|over}.
 3) Yolda iddialar: KnowledgePath.Claims[]{ClaimId, ClaimCode, Arrangement}; POST paths/{id}/claims · …/claims/{claimId}/arrange · …/claims/{claimId}/remove (yalnız taslak; donmuşta 409). Kurallar: tenant'ta (404), claim_product_mismatch, claim_ref_duplicate, chain_slot_invalid, zincirsiz yolda chain_template_required. Onaysız/ülkede sürümü olmayan iddia EKLENEBİLİR (engel yayında, KP-3). Okuma: {claimCode, name, text, qualifier, countryVersionId?, countryVersion?, status, usable, reason: not_approved|no_country_version|language_mismatch}; ülke sürümü kapsama önceliğiyle; text/qualifier yolun dilinde.
 4) Kod öneki KP- (ISO ülke koduyla başlayan önek yok).
 5) DTO: chainTemplate{id, code, name, version}, countryCode, languageCode, derivedContext, isLegacyUnapproved, identityLocked, claims[], chainConformance[]; adım arrangement; liste en az countryCode/languageCode/chainTemplateCode/isLegacyUnapproved.
 ORTAKLAŞTIR: ContentSetContextResolver/Validation + ContentSetPathOrder → Features/Knowledge/Chain/ (ChainContextResolver, ChainArrangementOrder); İçerik Seti + SB-2 de bunları kullansın, davranışları değişmesin (kopya kalmasın).
KORU/YAPMA: publish ucu, new-version (kimlik kopyası hariç), archive, yolculuk/ziyaret DEĞİŞMEZ; revizyon/MLR/yayın kapısı YOK; İçerik Seti+SB-2 kalır (yalnız ortak yardımcı); zincirsiz eski yollar bugünkü gibi; Web/Platform/Auth DOKUNMA; ham repository yazması yok; yeni alanlar class-map'e (GUID string).
DOĞRULA (E2): cd C:\tmp\kp-1; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 2118/0/5; bilinen sıra flake'i hariç); build 0 hata. Yeni testler: zincirli oluşturma + doğrulama kodları, bind-chain (taslak/bir kez/konu), kimlik kilidi, new-version kopyası, chain_slot_invalid/full/move_forbidden, dil uyuşmazlığı (zincirli) + eski yolda eski davranış, dal-öncelikli StepOrder, iddia ekle/sırala/çıkar, claim_product_mismatch/duplicate/chain_template_required, iddia okuması (ülke sürümü + yol dilindeki metin), uyum özeti, IsLegacyUnapproved, class-map round-trip, İçerik Seti testleri ortak yardımcıyla yeşil. Sabotaj: dal-öncelikli sıra + kimlik kilidi + slot doğrulaması testleri kırmızıya dönmeli. Commit ("feat(crm): WP-KP-1 — knowledge path studio model (chain, identity, arrangement, claims)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: dalsız eski düz zincir şablonunda slot tanımı tek anlamlı değilse ya da adımların tip/kavram düğümü alanlarının zincir adımıyla çelişen kullanımı varsa DUR + raporla.
```
