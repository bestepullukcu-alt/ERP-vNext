# MOD-0162-FU01A — KnowledgePath / Content Sequence Boundary Pack Authorization

> Tarih: 2026-08-02
> Kimlik: **MOD-0162-FU01A — KnowledgePath / Content Sequence Boundary** (parent `MOD-0162 Knowledge Base`)
> Kapsam: İçerik sıralama modeli + boundary — **kod yazma yok, runtime yok**
> Sonuç: **PASS**

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Task türü | Documentation / boundary authorization (implementation değil) |
| Değiştirilen runtime kod | **0 dosya** |
| Çalışma alanı | `execution/domains/commercial-suite/module-packs/**` + `docs/audits/**` |
| DCP-002 `MOD-0162-FU01A` | `OK  MOD-0162-FU01A: proven against Blueprint/registry.` (exit 0, `--parent MOD-0162`) |
| Kimlik notu | MOD-0162-FU01'in identity note'u aynen geçerli: model domain-nötr; EA yatay capability göçü kararı (FU01 §18/F1) bu pack'i de kapsar |
| Registry satırı yazımı | **Yapılmadı** (pack yetkisi dışı) |

Komut:

```
py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU01A \
   --name "KnowledgePath / Content Sequence Boundary" --parent MOD-0162
```

---

## 2. Dependency Confirmation

| Ön koşul | Durum | Not |
|---|---|---|
| MOD-0162-FU01 Knowledge / Content & Subject Taxonomy | **PASS** | [knowledge-content-subject-taxonomy-pack-authorization-2026-08-02.md](knowledge-content-subject-taxonomy-pack-authorization-2026-08-02.md) — **hard prerequisite** |
| MOD-0165 / MOD-0167 Visit Frequency Ownership | **PASS** | [mod-0165-mod-0167-...-2026-08-02.md](mod-0165-mod-0167-visit-frequency-call-cycle-policy-ownership-pack-authorization-2026-08-02.md) |
| MOD-0150 Contact Availability | **PASS** | |
| MOD-0151 FU09A Visit/Route Readiness | **PASS** | |
| MOD-0155 | **Başlamadı** | Tüketim boundary'si sözleşme olarak yazıldı |
| Brand/Product Master | **Başlamadı** | Path seviyesinde de **opsiyonel** kaldı |
| Workflow / approval | **En sona bırakıldı** | `review`/`approved` yalnız future-ready metadata |
| MOD-0028/0029 Document Management | **Repoda canlı** | File boundary korundu |

---

## 3. Business Need Summary

MOD-0162-FU01 **tekil içerik** nesnesini, taksonomiyi ve profil modelini kapattı. Kapanmayan soru:
*"ilk bunu anlatacağım, sonra bunu"* — yani **içerik zinciri**.

Bu boşluk kapanmazsa sıra en kolay ama en yanlış üç yere sızar:

| Sızma noktası | Neden yıkıcı |
|---|---|
| `KnowledgeContent.NextContentId` | Aynı içerik farklı path'lerde farklı sırada geçer; tek "next" bunu kaybeder ve içeriği zincire kilitler |
| `BrandContentFlow` | Akışı pharma'ya kilitler; Almanca dersi / SOP eğitimi / onboarding dışarıda kalır |
| `VisitPlan` içinde hardcoded content sırası | Akış sahipliğini MOD-0155'e gömer; aynı akış eğitimde yeniden kullanılamaz, iki yerde iki sıralama mantığı doğar |

Bu FU kapattığı sorular: hangi içerikler hangi sırayla · hangi adımlar zorunlu/opsiyonel · bir adıma geçmek için
önceki gerekli mi · path hangi Subject/Topic/AudienceProfile için geçerli · pharma ziyaret akışı nasıl temsil
edilir · Almanca dersi / SOP eğitimi gibi pharma dışı akışlar nasıl temsil edilir · branching **engine açmadan**
nasıl future-ready kalır.

---

## 4. Ownership Decision

| Soru | Sahip |
|---|---|
| Ne anlatılacak / hangi versiyon / kime uygun? | MOD-0162-FU01 |
| **Hangi sırayla anlatılacak / öğrenilecek / gösterilecek?** | **Bu FU (MOD-0162-FU01A)** |
| Ne sıklıkla gidilecek? | MOD-0165 / MOD-0167 |
| Ne zaman müsait? | MOD-0150 |
| Kim sorumlu / coverage current mı? | MOD-0151 |
| Visit/route plan, digital detailing, gösterim kaydı | **MOD-0155** |
| Completion / score / attendance / certificate | **MOD-0309** |
| Dosya / binary / controlled document | **MOD-0028 / MOD-0029** |

**Temel mimari kural:** *path bir **şablon**dur, bir **çalıştırma** değildir.* Bir adımın gerçekten gösterildiği,
tamamlandığı veya atlandığı bilgisi bu FU'da **yoktur**.

---

## 5. KnowledgeContent vs KnowledgePath

```text
KnowledgeContent   = tekil içerik
KnowledgePath      = içeriklerin anlatım / öğrenme / sunum zinciri
KnowledgePathStep  = path içindeki tek adım
```

MOD-0162-FU01 pack'ine kalıcı yasak olarak yazıldı: `KnowledgeContent`'e **`NextContentId` benzeri bir alan
eklenmez** (FU01 §3 notu + §18 F10).

---

## 6. Authorized KnowledgePath Model

`PathId` · `TenantId` (**JWT claim'inden**, payload'da asla) · `PathCode` (sürümler arası stabil) · `PathName` ·
`Description?` · **`SubjectId` (zorunlu)** · `TopicId?` · `AudienceProfileId?` · **`Objective` (zorunlu)** ·
`Language?` · `Version` · `Status` · `EffectiveFrom` · `EffectiveTo?` · `Source` · audit dörtlüsü.

Path **hiçbir pharma alanını zorunlu kılmaz**; Brand/Product/Indication/ATC/Campaign yalnız FU01 §7'deki
opsiyonel metadata üzerinden gelir.

---

## 7. Authorized KnowledgePathStep Model

`StepId` · `PathId` · **`StepOrder`** · **`ContentId`** (published+effective) · `ContentCode` (türetilir) ·
**`VersionPinPolicy`** (`pinned` varsayılan / `latest-published`) · `StepCode` · `StepTitle` · `StepType` ·
`IsRequired` · `CompletionRule` · `PrerequisiteStepId?` · `BranchCondition?` *(future)* ·
`EstimatedDurationMinutes?` · `Notes?` · audit dörtlüsü.

`StepTitle` içerik başlığından **bağımsız** olabilir — aynı içerik farklı path'te farklı çerçevelenebilir.

---

## 8. StepType Policy

```text
intro · core-message · clinical-evidence · indication · brand-message · objection-handling · faq ·
practice · quiz · assignment · summary · closing · lesson · vocabulary · grammar · listening ·
speaking · reading · homework
```

**Hardcoded enum olarak gömülmez** → MOD-0048 reference set'leri: `knowledge-path-step-type` ·
`knowledge-path-status` · `knowledge-path-completion-rule` · `knowledge-path-source`. Set yayınlanmadan
create/update **fail-closed 400**. Liste bilinçli olarak hem pharma hem eğitim değerlerini içerir — tek vokabüler,
iki bağlam.

**`CompletionRule` (beyan, motor değil):** `none` · `viewed` · `acknowledged` · `assessment-passed`
(içerikte `AssessmentRequired=true` olmalı, aksi 400) · `duration-met` (`EstimatedDurationMinutes` zorunlu, aksi
400). Ölçüm ve kayıt execution tarafındadır (§13/§14).

---

## 9. Pharma Sequence Example

```text
Almiba Q1 Doctor Visit Flow
1. Hipertansiyon farkındalık      (intro)
2. Almiba endikasyon özeti        (indication)
3. Klinik fayda mesajı            (core-message)
4. Klinik kanıt                   (clinical-evidence)
5. Objection handling             (objection-handling)
6. FAQ                            (faq)
7. Kapanış mesajı                 (closing)
```

Subject = `Pharma` · Topic = `Kardiyoloji / Hipertansiyon` · AudienceProfile = `Kardiyoloji A segment doktor` ·
optional metadata = Brand/Product/Indication/ATC/Campaign. **Brand/Product zorunlu değildir** — path bunlar boşken
de tam çalışır.

---

## 10. Learning Sequence Example

```text
German A1 Greetings Path
1. Selamlaşma kelimeleri          (vocabulary)
2. Kendini tanıtma cümleleri      (lesson)
3. Dinleme alıştırması            (listening)
4. Konuşma pratiği                (speaking)
5. Mini quiz                      (quiz)
6. Ödev                           (homework)
```

Subject = `Almanca` · Topic = `A1 / Selamlaşma` · AudienceProfile = `Beginner learner` · LearningLevel = `A1` ·
Skill = `Speaking`. **Brand/Product boş kalır** ve bu bir eksiklik değildir.

---

## 11. Sequence Rules

| Kural | Karar |
|---|---|
| `StepOrder` | Zorunlu, path version içinde **deterministik**; duplicate → **409** |
| Sıra aralığı | Boşluk serbest (10/20/30 önerilir) — araya ekleme yeniden numaralama gerektirmesin |
| `ContentId` | **published + effective** içeriğe referans → aksi **400** |
| Archived içerik | Yeni active/published path'e **eklenemez**; mevcut path'lerde tarihsel referans kalır |
| Hard delete | **Yasak** (path / step / içerik) |
| Path versiyonlama | **Zorunlu**; published sürümde **adım seti dondurulur** — değişiklik yeni sürüm ister |
| Tüketilebilirlik | Yalnız **published + effective** path; **draft/review MOD-0155'e recommendation gitmez** |
| Content version determinizmi | `VersionPinPolicy`: **`pinned`** (varsayılan — regüle içerik) veya `latest-published` (açıkça seçilir); çözülen `ContentId` + `Version` cevapta **görünür**; sessiz sürüm kayması **yasak** |
| `PrerequisiteStepId` | Aynı path içinde · **daha küçük `StepOrder`** · döngü yasak → aksi **400** |
| Required ↔ optional | **Zorunlu adım, opsiyonel adıma prerequisite olamaz** → **400** |
| Boş path | `published` path **en az bir `IsRequired=true` adım** içermeli → aksi **400** |
| Örtüşme | Aynı `(PathCode, Language)` için örtüşen pencerede iki published sürüm → **409** |
| Subject tutarlılığı | Cross-subject adım (pharma akışında QMS hatırlatması) **yasak değil ama görünür** olmalı |
| History | Path/adım geçmişi korunur; "hangi sürümde hangi adım vardı" cevaplanabilir |

---

## 12. Branching Boundary

`BranchCondition` **optional/future metadata**dır; bu FU'da **evaluator yoktur**.
Beyan edilen şekil: `ConditionCode` + `Description` + `TargetStepId?`.
Örnekler (yalnız kayıt): doktor klinik kanıt sorarsa · fiyat itirazı varsa · quiz skoru düşükse · öğrenci önceki
dersi tamamlamadıysa.

**Zorunlu kısıt:** bir path, branch condition **olmadan da baştan sona yürünebilir** olmalıdır — lineer geçiş
eksiksizdir. Runtime branching / dinamik öneri ileride **Digital Detailing / Learning Execution / MOD-0155**
tarafında ayrı authorization ile ele alınır.

---

## 13. MOD-0155 Consumer Boundary

MOD-0155 ileride tüketebilir: visit objective için önerilen path · campaign target için content sequence ·
doctor profile için anlatım akışı · ziyarette hangi step/content gösterildiğinin evidence'ı.

**Bu FU:** visit plan · route plan · digital detailing · content usage tracking · visit execution **yapmaz**;
"en uygun path"i **seçmez** (öneri/skor motoru yoktur). Katalog sıralı **şablon** döndürür.

---

## 14. MOD-0309 Completion Boundary

Completion **MOD-0309 Learning / Training Records** kapsamındadır. Bu FU: completion kaydı tutmaz · learner score
tutmaz · attendance tutmaz · certificate üretmez · quiz motoru çalıştırmaz.
`CompletionRule` iki tarafın **sözleşme alanıdır**: bu FU **beyan eder**, MOD-0309 **ölçer ve kaydeder**.

---

## 15. MOD-0028 / MOD-0029 File Boundary

`KnowledgePathStep` yalnız `ContentId`'ye referans verir; `FileRef` hâlâ MOD-0028/0029 doküman/file SoR'undan
gelir (FU01 §5.2). Bu FU: file upload yapmaz · binary storage açmaz · render/preview yapmaz · doküman kopyalamaz ·
içeriğin ikinci kopyasını path içinde tutmaz.

---

## 16. Explicit Exclusions

Runtime implementation · visit planning · route planning · digital detailing · content usage tracking ·
learning completion · quiz engine · **branch evaluator** · recommendation engine · campaign engine ·
segmentation engine · frequency engine · Brand/Product master implementation · approval workflow ·
MOD-0023 entegrasyonu · file upload/render/preview · Account/Contact mutation · territory mutation ·
patient data · hard delete · Mongo hand-edit · RBAC seed/grant · registry write · MOD-0048 publish ·
`TenantId` payload'da.

---

## 17. Contract Flags

```json
{
  "supportsKnowledgePath": true,
  "supportsContentSequence": true,
  "supportsKnowledgePathVersioning": true,
  "supportsRequiredOptionalSteps": true,
  "supportsFutureBranchingMetadata": true
}
```

Pack seviyesinde **öneri**; canlı contract'a yazılmadı. **Eklenmedi:** `supportsVisitPlanning` ·
`supportsRoutePlanning` · `supportsDigitalDetailing` · `supportsLearningCompletion` ·
`supportsRecommendationEngine` · `supportsWorkflowApproval`. MOD-0162-FU01 flag seti ve MOD-0151 canlı contract'ı
(`supportsWorkflowActivation=false` dahil) **değişmedi**.

---

## 18. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** |
| MOD-0155 code changed? | **No** |
| MOD-0162-FU01 runtime changed? | **No** (yalnız pack dokümanında §3 notu + F10 satırı) |
| MOD-0165/0167 runtime changed? | **No** |
| Digital detailing opened? | **No** |
| Visit planning opened? | **No** |
| Route planning opened? | **No** |
| Content usage tracking opened? | **No** |
| Learning completion / quiz engine opened? | **No** |
| Branch evaluator opened? | **No** (yalnız future metadata) |
| Recommendation engine opened? | **No** |
| Campaign / segmentation / frequency engine opened? | **No** |
| Brand/Product implementation opened? | **No** |
| Workflow/approval opened? | **No** |
| File upload / render / preview opened? | **No** |
| Account/Contact mutation opened? | **No** |
| Territory mutation opened? | **No** |
| Patient data opened? | **No** |
| RBAC seed/grant changed? | **No** |
| MOD-0048 publish changed? | **No** |
| Registry write? | **No** |
| KnowledgePath / sequence boundary added? | **Yes** |
| Required/optional step ayrımı tanımlandı? | **Yes** |
| Branching future-only mu? | **Yes** |
| Follow-ups opened? | **Yes** (8 adet) |

Pack frontmatter doğrulaması: `status: draft` · `runtime_code_allowed: false` · `shell: none` ·
`golden_reference: none` · `form_field_count: 0`.

---

## 19. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0162-FU01A-knowledge-path-content-sequence.md` | **Oluşturuldu** |
| `execution/domains/commercial-suite/module-packs/MOD-0162-FU01-knowledge-content-subject-taxonomy.md` | **Güncellendi** (yalnız doküman) — §3'e `NextContentId` yasağı + FU01A cross-ref; §18'e F10 (kapatıldı) satırı |
| `docs/audits/mod-0162-fu01a-knowledge-path-content-sequence-pack-authorization-2026-08-02.md` | **Oluşturuldu** (bu rapor) |

Runtime kod, config, gateway, RBAC, reference data ve registry **değiştirilmedi**.

---

## 20. Final Verdict

### **PASS**

- `KnowledgePath` / `KnowledgePathStep` **ayrı model** olarak yetkilendirildi; `KnowledgeContent` **tekil içerik**
  olarak kaldı ve `NextContentId` gömme yasağı FU01 pack'ine yazıldı.
- Sıralama modeli tam tanımlandı: unique `StepOrder`, prerequisite yönü ve döngü yasağı, required↔optional zincir
  kuralı, published+effective içerik şartı, archived içerik yasağı, hard-delete yasağı, boş path yasağı.
- **Content version determinizmi** `VersionPinPolicy` ile çözüldü (varsayılan `pinned`; çözülen sürüm görünür;
  sessiz sürüm kayması yasak) ve published path sürümünde **adım seti donduruldu** — "hangi akış uygulandı?"
  sorusu cevaplanabilir kaldı.
- Pharma (Almiba Q1 Doctor Visit Flow) ve learning (German A1 Greetings Path) örnekleri **aynı modelle**
  desteklendi; Brand/Product boşken de path tam çalışıyor.
- Required/optional step ayrımı ve `CompletionRule` **beyan olarak** tanımlandı; motor açılmadı.
- Branching **future metadata** olarak sınırlandı; evaluator yok ve lineer yürünebilirlik zorunlu kılındı.
- MOD-0155 consumer, MOD-0309 completion ve MOD-0028/0029 file boundary'leri netleşti.
- Runtime / detailing / planning / completion / workflow **açılmadı**; mevcut scope'lar bozulmadı.

FAIL kriterlerinin hiçbiri tetiklenmedi: sequence VisitPlan'a gömülmedi, model pharma-only yapılmadı, runtime ve
branching engine açılmadı, mevcut scope'lar korundu.

---

## 21. Next Recommended Prompt

`Brand/Product Master Boundary Pack Authorization`
