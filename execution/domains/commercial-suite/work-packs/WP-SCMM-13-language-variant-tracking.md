# WORK PACKAGE — WP-SCMM-13 · İki-dil varyant takibi (source/target + translation assessment)

> **Control Tower kaydı (SoR).** SCMM R1 content band. Module: **MOD-0162** (KnowledgeContent — component). Branch: `feature/scmm-content-studio` (HEAD ebf921c1). Bağ: SCMM-12 ✅.
> **docx §13:** "Source and target language variants are **distinct versioned records under a shared logical component**; a **source edit opens translation assessment**; **no silent fallback** to another language. Archive/retire rather than physically delete referenced records."

## Ölçülmüş girdi (CT, 2026-09-14)
- **KnowledgeContent aggregate** (`Domain/Entities/KnowledgeContent.cs`): `ContentCode`, `ContentTitle`, `ContentStatus` (draft/review/approved/published/inactive/archived), `SubjectId`, `LanguageCode` (**düz — variant linkage YOK**), `ContentVersion`, `EffectiveFrom/To`, `Source`, refs. **Variant-set / source-designation / translation-status alanları YOK** → eklenecek.
- **CQRS mirror:** `Features/Knowledge/Content/` (`KnowledgeContentCommands.cs`, `KnowledgeContentCommandHandlers.cs`, `KnowledgeContentQueryHandlers.cs`, `Queries/KnowledgeContentQueries.cs`, `IKnowledgeContentLinkageReader.cs`). Yeni komut/handler bunları örnek alır.
- **Frozen model (SCMM-03):** additive; mevcut content kırılmaz (read-time migration).

## Kapsam (MOD-0162 KnowledgeContent extend — backend)
1. **Variant-set linkage:** KnowledgeContent'e `ContentSetId` (Guid — mantıksal component; source+target aynı set'te) + `IsSourceLanguage` (bool) ekle. **Read-time migration:** mevcut satır → ContentSetId=kendi Id, IsSourceLanguage=true (kendi source'u). Bir set'te dil başına en fazla 1 aktif varyant; source tek.
2. **Translation status:** `TranslationStatus` (ör. `current` / `needs_assessment`) — target varyantlar için. Source'ta anlamsız/`current`.
3. **Source-edit trigger:** source-dil varyantı **içerik-etkileyen** düzenleme aldığında (text/body/summary/refs), aynı ContentSetId'deki **target varyantlar → `needs_assessment`** (docx: "source edit opens translation assessment"). Sadece metadata (title alias) değişimi tetiklemez — ölç ve kararı belgele.
4. **No silent fallback:** (ContentSetId, language) ile çözüm o dilin varyantını ya da **açık unresolved** döner — başka dile **düşmez**. `IKnowledgeContentLinkageReader`/query bu davranışı uygular.
5. **Komutlar:** target varyant oluştur (source'a bağla, ContentSetId paylaş) · varyant listele (set bazında) · `mark-assessed` (needs_assessment→current geçişi, SoD/perm). CRUD+repo+class-map(GUID trap)+audit (MOD-0162). Physical delete YOK (archive/retire).

## Frozen model uyumu
- D02c: component = KnowledgeContent reuse (yeni aggregate KURMA — extend). Additive alanlar; mevcut ContentVersion≠Version ayrımı korunur. Sektör-nötr.

## Acceptance
- **E2:** build temiz; unit — variant-set create (source+target aynı ContentSetId) · read-time migration (eski satır=self-source) · source içerik-edit → target `needs_assessment` (metadata-only tetiklemez) · no-fallback (yanlış dil isteyince unresolved, başka dil DÖNMEZ) · mark-assessed transition · dil başına tek source guard · audit · class-map GUID.
- **Regresyon:** tam CrmService.Application.Tests — bilinen PII flake HARİÇ yeni fail YOK (baseline-diff).
- **E4 (fleet):** authenticated — target variant oluştur, source düzenle→target needs_assessment, resolve no-fallback (follow / UI SCMM-14'te).
- Kapsam: yalnız KnowledgeContent variant-linkage (+audit). Claim/eligibility/concept aggregate DEĞİŞMEZ; yeni aggregate YOK; başka modül YOK; HTTP/UI = SCMM-14 tüketimi/follow.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-13 · Prompt P-SCMM-13 v1.0  (İki-dil varyant takibi — MOD-0162 KnowledgeContent extend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: ebf921c1 · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-13-language-variant-tracking.md (bu WP)
2. services/Diten.CrmService/.../Domain/Entities/KnowledgeContent.cs (LanguageCode/ContentVersion/ContentStatus — extend edilecek)
3. DESEN: services/Diten.CrmService/.../Features/Knowledge/Content/* (KnowledgeContentCommands/CommandHandlers/QueryHandlers/Queries/IKnowledgeContentLinkageReader — yeni komut/handler/query bunları örnek al)
4. docs/decisions/DEC-SCMM-03-model-policy-freeze.md (D02c component reuse, additive, sektör-nötr)

NE (MOD-0162 KnowledgeContent extend, backend):
 1) Alanlar: ContentSetId (Guid, mantıksal component) + IsSourceLanguage (bool) + TranslationStatus (current/needs_assessment).
    READ-TIME MIGRATION: mevcut satır → ContentSetId=kendi Id, IsSourceLanguage=true, TranslationStatus=current.
 2) Target varyant oluştur komutu: source'a bağla (aynı ContentSetId), farklı LanguageCode; dil başına tek aktif varyant + set'te tek source guard.
 3) Source-edit trigger: source varyant İÇERİK-etkileyen edit (text/body/summary/refs) alınca aynı ContentSetId target'lar → needs_assessment. Metadata-only (title alias) TETİKLEMEZ (kararı belgele).
 4) No silent fallback: (ContentSetId, language) çözümü o dilin varyantını ya da AÇIK unresolved döner; başka dile DÜŞMEZ (IKnowledgeContentLinkageReader/query).
 5) mark-assessed komutu (needs_assessment→current, perm/SoD). CRUD+repo+class-map(GUID trap)+audit(MOD-0162). Physical delete YOK.
NASIL: mevcut KnowledgeContent CQRS/versioning/status desenini birebir izle. Additive (mevcut content kırılmaz). new-aggregate class-map/GUID subtype-4 tuzağı (CrmService).
YAPMA: yeni aggregate KURMA (KnowledgeContent extend); Claim/eligibility/concept DEĞİŞTİRME; silent fallback; physical delete; sektöre özgü değer; global serializer; başka modül; HTTP/UI (SCMM-14 tüketir).
DOĞRULA (E2):
 - build temiz; unit: variant-set create · read-time migration (eski=self-source) · source içerik-edit→target needs_assessment (metadata-only tetiklemez) · no-fallback (unresolved, başka dil dönmez) · mark-assessed · dil-başına-tek-source guard · audit · class-map.
 - TAM CrmService.Application.Tests: bilinen ContactLocationPii flake HARİÇ yeni fail YOK (baseline-diff).
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13).

Durma koşulları: KnowledgeContent'te zaten variant/set alanı varsa (raporla) · "içerik-etkileyen edit" sınırı belirsizse (kararı belgele, DUR değil) · no-fallback mevcut query deseniyle çelişiyorsa · kapsam KnowledgeContent variant-linkage dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- **SCMM-14** (Content Studio ④⑤⑥ assembly — component+claim+template+variant tüketir; clone-to-draft, provenance, no-inherited-approval).
- HTTP uç + UI (variant yönetimi SCMM-14 içinde / follow) · SCMM-11-follow (eligibility HTTP/UI).
