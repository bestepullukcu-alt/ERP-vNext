# Knowledge / Content & Subject Taxonomy — Pack Authorization

> Tarih: 2026-08-02
> Kimlik: **MOD-0162-FU01 — Knowledge Content & Subject Taxonomy Foundation** (parent `MOD-0162 Knowledge Base`)
> Kapsam: Sahiplik / kavramsal model / taksonomi / boundary — **kod yazma yok, runtime yok**
> Sonuç: **PASS**

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Task türü | Documentation / pack authorization (implementation değil) |
| Değiştirilen runtime kod | **0 dosya** |
| Çalışma alanı | `execution/domains/commercial-suite/module-packs/**` + `docs/audits/**` |
| DCP-002 `MOD-0162` (parent) | `OK  MOD-0162: proven against Blueprint/registry.` (exit 0) |
| DCP-002 `MOD-0162-FU01` | `OK  MOD-0162-FU01: proven against Blueprint/registry.` (exit 0, `--parent MOD-0162`) |
| DCP-002 `CAND-CAP-0006` (denenen yatay kimlik) | **BLOCKED** — `has no registry row` · `not recorded in the reconciliation ledger` (exit **2**) |
| Registry satırı yazımı | **Yapılmadı** (pack yetkisi dışı → F1/F-registry) |

### 1.1 Kimlik kararı (neden MOD-0162-FU01)

Blueprint `Blueprint_Data` taraması (Module Name üzerinde `knowledge|learning|training|content|taxonomy`):

| Blueprint ID | Ad | Suite / Grup | Neden **tam karşılık değil** |
|---|---|---|---|
| **MOD-0162** | Knowledge Base | Commercial Suite / Service | SoR = *knowledge articles, article feedback*; kapsam self-service KB — ama **knowledge article SoR'una sahip tek Blueprint capability'si** |
| MOD-0309 | Learning / Training Records | HCM / Talent & Development | SoR = training **records / completions / evidence** — içerik kataloğu değil |
| MOD-0057 | Semantic Tagging & Taxonomy Management | Data & Knowledge Plane | SoR = **data-asset** taksonomi/etiket yönetimi — business subject taksonomisi değil |
| MOD-0028 / MOD-0029 | Documentation & Evidence / Controlled Documents | Platform Content Service | SoR = **doküman/binary + controlled state** — içerik anlatım kataloğu değil (repoda canlı) |

DCP-002 kimlik sırası **"Blueprint lookup → mevcut MOD veya FU → ancak yoksa CAND-CAP → asla yeni MOD/PSS/NEW
uydurma"**. Yatay aday kimlik (`CAND-CAP-0006`) **fail-closed BLOCKED** döndüğü ve registry satırı yazmak bu
pack'in yetkisi dışında olduğu için kimlik **MOD-0162-FU01** olarak alındı.

**Açıkça belirtilen varsayım:** yeteneğin **modeli domain-nötrdür** (pharma + Almanca + QMS + onboarding +
teknik/regülasyon eğitimi) ve MOD-0162'nin Service/self-service KB kapsamından **geniştir**. EA bunu yatay bir
platform capability'sine taşımak isterse kimlik göçü **F1** follow-up'ıdır; pack gövdesi (model, taksonomi,
boundary) değişmeden taşınabilir. Bu, işi bloklamayan bir governance kararıdır.

---

## 2. Dependency Confirmation

| Ön koşul | Durum | Kanıt / Not |
|---|---|---|
| MOD-0150 Contact Availability | **PASS** | `mod-0150-contact-availability-visit-preference-implementation-2026-08-01.md` |
| MOD-0151 FU09A Visit/Route Readiness | **PASS** | `mod-0151-fu09a-read-only-reverification-...-2026-08-02.md` |
| MOD-0165 / MOD-0167 Visit Frequency Ownership | **PASS** | [mod-0165-mod-0167-visit-frequency-call-cycle-policy-ownership-pack-authorization-2026-08-02.md](mod-0165-mod-0167-visit-frequency-call-cycle-policy-ownership-pack-authorization-2026-08-02.md) |
| MOD-0155 | **Başlamadı** | Tüketim boundary'si sözleşme olarak yazıldı |
| Brand/Product Master | **Başlamadı** | Optional metadata olarak bırakıldı |
| Workflow / approval / ChangeRequest | **En sona bırakıldı** | `review`/`approved` yalnız future-ready metadata |
| Document Management (MOD-0028/0029) | **Repoda canlı** | `FileRef` seam'inin dayanağı; yeni dosya deposu açılmadı |

---

## 3. Business Need Summary

Zincir tamamlanıyor:

| Soru | Sahip | Durum |
|---|---|---|
| Ne zaman ziyaret edilebilir? | MOD-0150 | PASS |
| Kim sorumlu / coverage current mı? | MOD-0151 | PASS |
| Ne sıklıkla gidilecek? | MOD-0165 / MOD-0167 | PASS (2026-08-02) |
| **Gidince ne anlatılacak?** | **Bu FU** | **Bu task** |
| Nasıl planlanır, ne gösterildi, ne oldu? | MOD-0155 | Başlamadı |

**Kritik mimari karar:** içerik yalnızca pharma ürün anlatımı değildir. Almanca A1 dersi, SOP eğitimi, QMS Document
Control training, onboarding, teknik/regülasyon eğitimi, satış argümanı, FAQ ve objection handling notu aynı
kataloğun birinci sınıf vatandaşlarıdır. Bu yüzden model `BrandContent`/`ProductContent` değil,
**`KnowledgeContent` + `Subject`/`Topic`/`AudienceProfile`**tir; Brand/Product yalnız **opsiyonel metadata**dır.

---

## 4. Ownership Decision

Bu FU şu soruların sahibidir: *ne anlatılacak · hangi içerik gösterilecek · hangi subject/topic işlenecek · hangi
versiyon geçerli · hangi audience/profile için uygun.*

| Nesne / sorumluluk | Sahip |
|---|---|
| `Subject` · `Topic` · `AudienceProfile` · `KnowledgeContent` (+ opsiyonel metadata) | **Bu FU (MOD-0162-FU01)** |
| Binary dosya / controlled document / doküman versiyonu | **MOD-0028 / MOD-0029** (repoda canlı) |
| Training completion / mandatory training evidence | **MOD-0309** Learning / Training Records |
| Customer self-service KB yayın/feedback/search-sync | **MOD-0162 parent** (future) |
| Enterprise search / semantic tagging | **MOD-0056 / MOD-0057** (future seam) |
| Ne sıklıkla gidilecek | **MOD-0165 / MOD-0167** |
| Visit/route plan, digital detailing, content usage | **MOD-0155** |
| Brand / Product master | **Ayrı master (MDM/Product)** |

---

## 5. Authorized Conceptual Model

```text
Subject            (en üst anlatım alanı)
  └── Topic        (hiyerarşik alt konu, parent-child)
AudienceProfile    (kime anlatılacak — generic)
KnowledgeContent   (asıl içerik nesnesi, versioned)
    ├── Optional Pharma Metadata
    ├── Optional Learning Metadata
    └── Optional Campaign / Brand / Product Metadata
```

Dördü de tenant-scoped; `TenantId` **JWT claim'inden** gelir, payload'da **asla** bulunmaz.

---

## 6. Subject / Topic Taxonomy

Subject örnekleri: `Pharma` · `Almanca` · `QMS` · `Onboarding` · `Sales Training` · `Technical Training` ·
`Regulatory` · `Product Training` · `CRM Training`.

```text
Pharma > Cardiology > Hypertension | Heart Failure
German > A1 > Greetings | Numbers
QMS    > SOP > Document Control
```

Kararlar: Subject **unique `SubjectCode`** · Topic **stabil `TopicCode`** + `ParentTopicId` ile hiyerarşi ·
rename **kod değiştirmez**, `DisplayName`/`Alias` ile yapılır (path bozulmaz) · **hard delete yasak** ·
`archived` topic'e **yeni** içerik bağlanamaz, mevcut içerik bağlı kalır ve **history korunur** ·
parent döngüsü ve cross-subject parent → **400** · maksimum derinlik implementation FU'sunda sabitlenir (öneri 5) ·
bu taksonomi MOD-0057 semantic tagging'in **yerine geçmez**.

---

## 7. KnowledgeContent

Alan sözleşmesi (yetkilendirildi, implement edilmedi): `ContentId` · `TenantId` · `ContentCode` · `Title` ·
`Description` · `ContentType` · `SubjectId` · `TopicId` · `AudienceProfileId?` · `Language` · `Version` ·
`Status` · `EffectiveFrom` · `EffectiveTo?` · `OwnerUserId`/`OwnerTeamId` · `Tags[]` ·
`FileRef`/`Url`/`BodyRef` (en az biri) · `Source` · audit dörtlüsü.

`ContentType`: `presentation` · `brochure` · `lesson` · `faq` · `clinical-summary` · `objection-handling` ·
`quiz` · `video` · `pdf` · `html-detail` · `sop` · `training-material` · `message-script` · `knowledge-article`.

**Dosya sınırı (kritik):** `FileRef` = **MOD-0028/0029 doküman referansı** (documentId + versionId).
Bu FU **file upload, depolama, preview üretimi veya render yapmaz**; yeni binary depo açılmaz ve aynı dosyanın
ikinci kopyası KnowledgeContent içinde tutulmaz. Vokabülerler MOD-0048 set'i olacaktır (hardcoded fallback yasak,
set yayınlanmadan **fail-closed 400**).

---

## 8. AudienceProfile

Generic profil: pharma'da doktor profili, eğitimde learner profili gibi çalışır — **tek nesne, iki bağlam**.
Örnekler: `Kardiyoloji A segment doktor` · `Eczacı` · `Yeni başlayan çalışan` · `A1 Almanca öğrencisi` ·
`Satış temsilcisi` · `Manager` · `Admin user`.

İçerikte **optional**; yoksa içerik genel kabul edilir (uydurma profil atanmaz). Profil ↔ contact/segment/pozisyon
eşleştirme kuralı **bu FU'da yazılmaz** (tüketici tarafı). Hard delete yasak; `archived` profil yeni içeriğe
bağlanamaz.

---

## 9. Optional Pharma Metadata

`BrandId?` *(future)* · `ProductId?` *(future)* · `IndicationId?` · `IndicationName?` · `ATCCode?` ·
`TherapeuticArea?` · `Specialty?` · `DoctorProfileId?` · `CampaignId?` · `SegmentId?` · `MedicalMessageCode?`

```text
Content : Almiba Q1 Doctor Deck
Metadata: Brand=Almiba · Indication=Hipertansiyon · ATCCode=C09AA
          AudienceProfile=Kardiyoloji A segment doktor · Campaign=Almiba Q1
```

Kurallar: Brand/Product **zorunlu değil** · Indication/ATC yalnız pharma içerikte · master yokken **future
optional** · **içerik sistemi Brand/Product master'a bağımlı başlatılmaz** · master geldiğinde metadata linkage
eklenir · `DoctorProfileId` bir kısayol değildir, birincil alan generic `AudienceProfileId`'dir.

---

## 10. Optional Learning Metadata

`LearningLevel?` · `Skill?` · `LearningObjective?` · `PrerequisiteTopicId?` · `LessonType?` ·
`EstimatedDurationMinutes?` · `AssessmentRequired?` · `VocabularySetId?` · `GrammarTopic?`

```text
Content : German A1 Greetings Lesson
Metadata: Subject=Almanca · Topic=A1/Selamlaşma · LearningLevel=A1 · Skill=Speaking
          LearningObjective=Kendini tanıtabilmek
```

`AssessmentRequired=true` bir **işarettir**; quiz motoru, puanlama ve completion kaydı bu FU'da **yoktur**
(completion SoR'u **MOD-0309**).

---

## 11. Versioning / Status

**Versioning:** `Version` zorunlu · aynı `ContentCode` altında çoklu sürüm · `EffectiveFrom`/`EffectiveTo`
penceresi · archived sürüm **silinmez** · **hard delete yok** · yalnız **published + effective** sürüm
ziyaret/eğitimde kullanılabilir · visit/training execution **hangi sürüm gösterildiyse onu** evidence olarak
saklamalıdır (tüketici sorumluluğu) · aynı `(ContentCode, Language)` için örtüşen pencerede iki published sürüm →
kontrollü **409** (sessiz seçim yok).

**Status:** `draft` · `review` · `approved` · `published` · `inactive` · `archived`.
Bu task **workflow implementation açmaz**; `review`/`approved` yalnız **future-ready metadata**dır ve gerçek
approval MOD-0023'e **en sonda** bağlanır. **Published olmayan içerik MOD-0155'e active recommendation olarak
gitmez.** `archived` yeni kullanım için önerilmez, **history için okunabilir** kalır.

---

## 12. Permission Boundary

Canonical öneriler: `crm.knowledge.content.read` · `crm.knowledge.content.manage` ·
`crm.knowledge.content.publish` · `crm.knowledge.taxonomy.manage`.
`publish` `manage`'den **ayrıdır** (SoD: yazan ≠ yayınlayan).

**RBAC seed/grant yapılmadı.** Katalog hazır değilse implementation FU'sunda MOD-0151 FU08 precedent'i uygulanır
(anahtar tanımlanır ama `All` listesine eklenmez + geçici fallback) ve follow-up açılır:
`Knowledge-Content-RBAC — Permission Catalog Alignment`.

---

## 13. MOD-0155 Consumer Boundary

MOD-0155 ileride tüketir: visit objective için önerilen içerik · campaign content · doctor/profile bazlı mesaj ·
product/brand bazlı materyal · digital detailing içeriği · ziyaret sonrası content usage evidence.

MOD-0155'e ait olanlar (bu FU'da **yok**): visit plan · route plan · visit execution · ziyarette gösterilen
içerik · digital detailing · content usage tracking · visit report.

Önerilen tüketim seam'i yalnız **published + effective** satırları döndürür; katalog **sıralama/skor/"en iyi
içerik"/plan üretmez**.

---

## 14. MOD-0165 / MOD-0167 Integration Boundary

```text
Campaign = Almiba Q1 · Target = Kardiyoloji A segment doktorlar · Frequency = ayda 2
RecommendedContent = Almiba Q1 Doctor Deck v1.3   ← yalnız boundary
```

Campaign engine, segmentation engine ve frequency runtime **bu FU'da yapılmaz**. Content selection policy yalnız
boundary olarak tanımlandı ve **iki yön de yasaklandı**: içerik listesi frequency policy'ye gömülmez
(MOD-0165-FU01 §12), frequency kuralı `KnowledgeContent`'e gömülmez. İki soruyu **MOD-0155** birleştirir.

---

## 15. Brand/Product Boundary

Brand/Product **ayrı master** olarak kalır; bu FU master implementasyonu yapmaz ve sahiplik talep etmez.
Brand/Product `KnowledgeContent` için **optional metadata**dır, merkezi değildir; katalog pharma dışı subject'leri
**birinci sınıf** destekler. **Follow-up:** `Brand/Product Master Boundary Pack Authorization`.

---

## 16. Explicit Exclusions

Runtime implementation · file upload implementation · render/preview · arama indeksi · digital detailing ·
visit planning · route planning · visit execution · content usage tracking · campaign engine · segmentation
engine · frequency policy runtime · Brand/Product master implementation · approval workflow implementation ·
MOD-0023 entegrasyonu · e-signature · evidence pack · patient data · Account/Contact mutation ·
ContactAvailability mutation · territory mutation · yeni import/export scope · hard delete · Mongo hand-edit ·
RBAC seed/grant · MOD-0048 publish · registry satırı yazımı · `TenantId` payload'da.

---

## 17. Contract Flags

```json
{
  "supportsKnowledgeContent": true,
  "supportsSubjectTaxonomy": true,
  "supportsAudienceProfile": true,
  "supportsContentVersioning": true,
  "supportsOptionalPharmaMetadata": true,
  "supportsOptionalLearningMetadata": true
}
```

Pack seviyesinde **öneri**; canlı contract'a yazılmadı. Visit planning / digital detailing / workflow approval
yapıldığı anlamına **gelmez**. **Eklenmedi:** `supportsVisitPlanning` · `supportsRoutePlanning` ·
`supportsDigitalDetailing` · `supportsWorkflowApproval`. MOD-0151 canlı contract'ı ve
`supportsWorkflowActivation=false` **değişmedi**.

---

## 18. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** |
| MOD-0155 code changed? | **No** |
| MOD-0165/0167 runtime changed? | **No** (yalnız MOD-0165-FU01 pack dokümanında F2 cross-ref) |
| Brand/Product implementation opened? | **No** |
| Digital detailing opened? | **No** |
| Visit planning opened? | **No** |
| Route planning opened? | **No** |
| Campaign engine opened? | **No** |
| Segmentation engine opened? | **No** |
| Frequency runtime opened? | **No** |
| Workflow/approval opened? | **No** |
| File upload / storage opened? | **No** |
| Patient data opened? | **No** |
| Account/Contact mutation opened? | **No** |
| Territory mutation opened? | **No** |
| RBAC seed/grant changed? | **No** |
| MOD-0048 publish changed? | **No** |
| Registry satırı yazıldı mı? | **No** |
| Knowledge/Content boundary added? | **Yes** |
| Subject taxonomy boundary added? | **Yes** |
| Pharma metadata optional? | **Yes** |
| Learning metadata optional? | **Yes** |
| Follow-ups opened? | **Yes** (9 adet) |

Pack frontmatter doğrulaması: `status: draft` · `runtime_code_allowed: false` · `shell: none` ·
`golden_reference: none` · `form_field_count: 0`.

---

## 19. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0162-FU01-knowledge-content-subject-taxonomy.md` | **Oluşturuldu** |
| `execution/domains/commercial-suite/module-packs/MOD-0165-FU01-visit-frequency-call-cycle-policy.md` | **Güncellendi** (yalnız doküman) — §12 + §20 F2 "Knowledge/Content" follow-up'ı kapatıldı ve yeni pack'e bağlandı |
| `docs/audits/knowledge-content-subject-taxonomy-pack-authorization-2026-08-02.md` | **Oluşturuldu** (bu rapor) |

Runtime kod, config, gateway, RBAC, reference data ve registry **değiştirilmedi**.

---

## 20. Final Verdict

### **PASS**

- `KnowledgeContent` **merkezi model** olarak tanımlandı; `BrandContent`/`ProductContent` daraltması reddedildi.
- `Subject`/`Topic` taksonomisi netleşti (stabil kod, hiyerarşi, alias/rename, archive, hard-delete yasağı,
  history koruması).
- `AudienceProfile` **generic** profil olarak tanımlandı; doktor profili özel bir nesne değil, bir profil örneği.
- Brand/Product yalnız **optional metadata** olarak konumlandı; katalog Brand/Product master'a **bağımlı değil**.
- Indication / ATC / TherapeuticArea / Specialty / DoctorProfile / MedicalMessage **kaybolmadan** pharma metadata
  olarak yazıldı.
- Almanca A1, SOP/QMS training, onboarding, teknik ve regülasyon eğitimi gibi **pharma dışı** subject'ler
  birinci sınıf desteklendi (learning metadata dahil).
- Versioning/status policy netleşti (published+effective kullanım, evidence beklentisi, hard-delete yasağı,
  workflow'un future-ready metadata olarak bırakılması).
- MOD-0155 consumer boundary ve MOD-0165/MOD-0167 integration boundary netleşti (çift yönlü gömme yasağı).
- Dosya/doküman sınırı MOD-0028/0029'a, completion sınırı MOD-0309'a bağlandı — **yeni depo açılmadı**.
- Runtime / digital detailing / visit-route planning / workflow implementation **açılmadı**; mevcut scope'lar
  bozulmadı; follow-up prompt'u hazırlanabilir.

**Kayda geçen tek governance açığı (PASS'ı düşürmez):** yatay kimlik `CAND-CAP-0006` gate'i **BLOCKED** olduğu için
kimlik `MOD-0162-FU01` olarak alındı; yeteneğin kapsamı MOD-0162'nin Service-KB kapsamından geniştir ve EA kimlik
kararı **F1** follow-up'ı olarak açıktır. Pack gövdesi kimlikten bağımsızdır, göç maliyeti düşüktür.

---

## 21. Next Recommended Prompt

`Brand/Product Master Boundary Pack Authorization`
