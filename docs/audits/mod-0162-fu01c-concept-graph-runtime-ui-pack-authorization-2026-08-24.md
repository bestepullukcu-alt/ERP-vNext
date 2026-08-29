# MOD-0162-FU01C — ConceptGraph Runtime + UI: Pack Authorization & Gate Audit

**Tarih:** 2026-08-24
**Talep:** "MOD-0162-FU01C ConceptGraph Runtime + UI işine başla" (runtime + UI implementation)
**Sonuç:** ⛔ **RUNTIME BLOCKED — kod yazılmadı.** Governance kapısı kapalı; yerine implementation FU pack taslağı
(`MOD-0162-FU03`) üretildi.

---

## 1. Scope of this audit

Bu doküman bir **implementation evidence** dosyası değildir. Talep edilen runtime + UI işi için repo/pack/boundary
durumunun doğrulanmasını, blokajın gerekçesini ve üretilen authorization çıktısını kayda geçirir.

---

## 2. Doğrulanan repo durumu

| Kontrol | Bulgu | Kaynak |
|---|---|---|
| FU01C pack var mı? | ✔ VAR | `execution/domains/commercial-suite/module-packs/MOD-0162-FU01C-subject-concept-graph-configurable-concept-chain.md` |
| FU01C status | `approved` — ama **boundary** olarak | pack frontmatter |
| FU01C `runtime_code_allowed` | **`false`** | pack frontmatter |
| FU01C `runtime_code_scope` | *"NONE — … Runtime yetkisi bu pack'te DEĞİLDİR"* | pack frontmatter |
| FU01C-ADDENDUM | `status: draft`, `runtime_code_allowed: false` | `MOD-0162-FU01C-ADDENDUM-content-model-acceptance-criteria.md` |
| ADDENDUM §10 talimatı | *"Bu addendum'u temel alan bir ConceptGraph **implementation FU pack taslağı** … Runtime yetkisi ayrı authorization ile açılır."* | addendum |
| ConceptGraph implementation pack | ❌ **YOK** (bu audit'ten önce) | `ls module-packs/` |
| FU02 prerequisite | ✔ `status: done`, authenticated smoke 22/0 PASS | FU02 pack + `module-implementation-status.md:117` |
| Runtime kod izi (`ConceptType` / `ConceptNode` / `ConceptRelationship` / `ConceptChainTemplate` aggregate) | ❌ **YOK** — yalnız `KnowledgeContent.ConceptNodeId : Guid?` (format-level) | `Domain/Entities/`, `Features/Knowledge/` |
| Frontend concept yüzeyi | ❌ YOK — form alanı **disabled** ("runtime source yok") | `Views/CRM/Knowledge/_Form.cshtml:134-139`, `Controllers/CRM/KnowledgeController.cs:318-319` |
| Gateway route ihtiyacı | ✔ **YOK** — `/api/crm/knowledge/{everything}` wildcard mevcut | `gateway/Diten.ApiGateway/ocelot.json:2073-2081` |
| MOD-0048 concept set publish | ❌ yayınlanmamış (yalnız authoring template'inde taslak) | `docs/audits/mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-template.json:371,387` |

---

## 3. Blokajın gerekçesi (governance)

`AGENTS.md` §7 ve §10:

> "`@orchestrator` module pack oluşturmaz. Yeni modül geliştirmesi yalnızca … `approved` veya `ready-for-dev`
> module pack üzerinden başlar."
> "**Onay kapısı:** `draft` module pack yalnızca planlama dokümanıdır. Kod üretimi için status `approved` veya
> `ready-for-dev` olmalıdır."

FU01C `approved`'dır **ancak** `runtime_code_allowed: false` taşır ve `runtime_code_scope` alanı runtime'ı açıkça
`NONE` olarak tanımlar. Yani onay **boundary** onayıdır, **implementation** onayı değildir. Emsal desen repoda
zaten kuruludur: **FU01 (boundary, `runtime_code_allowed: false`) → FU02 (implementation, `runtime_code_allowed: true`)**.
ConceptGraph için bu ikinci halka eksiktir.

Kullanıcının görev tanımı §16 da aynı yolu emreder: *"Eğer MOD-0162-FU01C ready-for-dev değilse önce pack
authorization öner."*

---

## 4. Üretilen çıktı

| Dosya | İçerik |
|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0162-FU03-concept-graph-runtime-ui.md` | Implementation FU pack **taslağı** (`status: draft`, `runtime_code_allowed: false`) — 18 bölüm: domain modeli, API contract, UI scope, 20 validasyon kuralı, test planı, 22 adımlı authenticated smoke planı, repo scope, protected paths, exclusions, açık kararlar, ready-for-dev checklist, follow-up'lar |
| `docs/audits/mod-0162-fu01c-concept-graph-runtime-ui-pack-authorization-2026-08-24.md` | Bu doküman |

**Değiştirilmeyenler:** hiçbir `.cs` / `.cshtml` / `.resx` / `ocelot.json` / registry / Mongo dokunuşu yok.
FU01C approved gövdesi **değiştirilmedi** (ADDENDUM zaten additive kuralını koyuyor).

---

## 5. DCP-002 kimlik kapısı

```
py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU03 \
   --name "Concept Graph Runtime + UI" --parent MOD-0162
→ OK  MOD-0162-FU03: proven against Blueprint/registry.   (exit 0)
```

---

## 6. Talep edilen taslak model ↔ approved boundary sapmaları

Kullanıcı prompt'undaki domain modeli, approved FU01C ile **beş** yapısal noktada ayrışıyor. Boundary `approved`
olduğu için pack taslağı boundary'yi esas aldı (detay: FU03 pack §2).

| # | Prompt taslağı | Boundary (kazanan) |
|---|---|---|
| D1 | `ConceptNodeType`, `ConceptEdge` | `ConceptType`, `ConceptRelationship` |
| D2 | `SubjectId` yok | `SubjectId` **zorunlu**; cross-subject ilişki 400 |
| D3 | `ParentTypeId` / `ParentNodeId` | yok — hiyerarşi `ConceptRelationship` + `ConceptChainTemplate` |
| D4 | `BrandId` / `ProductId` / `TopicId` / `AudienceProfileId` doğrudan FK | `ExternalRefType` + `ExternalRefId` (node hiçbir varlığın SoR'u değil) |
| D5 | `ConceptChainTemplate` yok | **var ve zorunlu** (legacy `UCLNDesign`; ayrıca canlı `Campaign.ConceptChainTemplateId` referansını kapatır) |

Validasyon farkları: cycle için "explicit allowed flag" **yok** (koşulsuz 400) · template'e uymayan ilişki
**reddedilmez**, `IsTemplateConforming=false` ile görünür kılınır · duplicate `(From,To,Type)` active → 409.

---

## 7. Risk kaydı — MOD-0048 vokabüler çelişkisi

`concept-relationship-type` authoring taslağının değerleri (`related-to`, `depends-on`, `supports`, `addresses`,
`requires`, `belongs-to`, `targets`, `maps-to`, `replaces`, `other`) FU01C §5'in kanonik listesiyle
(`leads-to`, `requires`, `addresses`, `evidences`, `belongs-to`, `custom`) **çelişiyor**. Bu, MOD-0164-FU02'de
yaşanan legal-basis/source sapmasının aynısıdır. **Karar verilmeden bu set yayınlanmamalıdır** (F-RD).

---

## 8. Verdict

**BLOCKED-BY-DESIGN.** ConceptGraph runtime + UI, `MOD-0162-FU03` pack'i kullanıcı tarafından
`status: ready-for-dev` + `runtime_code_allowed: true` yapılana ve §16'daki D1–D5 kararları verilene kadar
başlatılamaz. Onay sonrası pack, tek oturumda implementasyona yetecek ayrıntıya sahiptir.

**Sıradaki adım (onay sonrası):** MOD-0162-FU03 runtime + UI → ardından **MOD-0162-FU01A KnowledgePath Runtime + UI**
(AC-SEQ-1: ConceptGraph → Package sırası).
