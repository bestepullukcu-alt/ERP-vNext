---
id: MOD-0167-FU02
name: Segment Foundation - Definition, Criteria, Membership Resolution and Target Customer
parent: MOD-0167
parent_name: Segmentation / CDP
implements_boundary: MOD-0167-FU01 (§5 membership seam) + MOD-0165-FU02 (§5.1 snapshot boundary)
siblings: MOD-0167-FU01
domain: commercial-suite
service: Diten.CrmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
runtime_code_allowed: true
runtime_code_scope: "AÇIK (ready-for-dev, flip 2026-08-27 kullanıcı kararı). Yetkilendirilen kapsam: `Segment` aggregate root (static/dynamic/hybrid, tenant-scoped, lifecycle + sürüm + effective dating) — kriter ağacı segment DOKÜMANI İÇİNDE embedded — VE ayrı `TargetCustomer` aggregate'i (yalnız MANUEL üyelik: include/exclude) VE deterministik real-time üyelik çözümlemesi (`resolve` / `is-member`, hiçbir şey persist etmez) VE nitelik kataloğu + contract + `concept.affinity` niteliğini MOD-0162-FU03 ConceptGraph'ından SALT-OKUNUR türeten in-service adaptör, tümü `Diten.CrmService` içinde; ayrıca CRM Admin → Segments TEK Compact sayfası (gömülü kriter ağacı editörü + gömülü üye alt-editörü) `frontend/Diten.Web` içinde. Materialized membership / refresh job / event-driven yeniden hesaplama, ICP scoring, StrategyTemplate, SubjectList, UCLN, segment usage log, campaign target üretimi, VisitFrequencyPolicy yazımı, MOD-0164 consent mutation, MOD-0151 territory mutation, MOD-0162 ConceptGraph mutation (node/ilişki/tip/şablon yazımı VE repository/query imza değişikliği dâhil), yeni graph aggregate/endpoint, MDM write, RBAC seed/grant, MOD-0048 publish, `ocelot.json` yazımı, registry yazımı ve Mongo hand-edit YASAKTIR."
owner: module-pack-author
branch: feature/crm/mod-0167-fu02-segment-foundation
started: 2026-08-27
target: TBD (kullanıcı onayı sonrası)
form_field_count: 12   # Segment = TEK golden-reference yüzeyi; türetme §11.1'de GÖSTERİLİR (12 > 8 → Compact). Gömülü kriter ağacı ve gömülü üye listesi ayrı yüzey DEĞİLDİR.
dependencies:
  - MOD-0167 (parent — Segment / TargetCustomer / UCLN / SubjectList / StrategyTemplate SoR)
  - MOD-0167-FU01 (draft boundary — §5 membership seam'inin RUNTIME karşılığı BURADA; frequency store AÇILMAZ)
  - MOD-0165-FU02 / MOD-0165-FU04 (consumer — CampaignTarget snapshot bu FU'nun resolve çıktısını tüketir; CampaignTarget SoR MOD-0165'te KALIR)
  - MOD-0165-FU01 (boundary — VisitFrequencyPolicy SoR; bu FU policy YAZMAZ)
  - MOD-0149 / MOD-0150 (in-service nitelik kaynağı — Account / Contact / AccountContactLink; MUTATE EDİLMEZ)
  - MOD-0151 (in-service nitelik kaynağı — `AccountCurrentCoverageResolver`; territory MUTATE EDİLMEZ)
  - MOD-0164 (in-service nitelik kaynağı — `IConsentPreferenceEvaluator`; consent MUTATE EDİLMEZ, imzası GENİŞLETİLMEZ)
  - MOD-0162-FU03 (in-service nitelik kaynağı — ConceptGraph; `concept.affinity` ürün-affinity türetmesinin kaynağı, SALT-OKUNUR; graph MUTATE EDİLMEZ, imzası GENİŞLETİLMEZ — §4.4/D-PRODUCT)
  - MDM / MOD-0290 (cross-service — yalnız kriter DEĞERİNİN referans doğrulaması, fail-closed; üyelik türetmez — §4.4/D-PRODUCT)
  - MOD-0048 (reference data — D-VOCAB=A: in-domain vokabüler, runtime ön koşulu DEĞİL; setler ayrı operatör işi → F-RD)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te YOK)
  - DEV-0001 (Golden Reference Compact — tek yüzey, tek klasör)
---

# MOD-0167-FU02 — Segment Foundation (Tanım, Kriter, Üyelik Çözümlemesi, Target Customer)

> **✅ READY-FOR-DEV — KOD YETKİSİ AÇIK (flip 2026-08-27 kullanıcı kararı).** `status: ready-for-dev` ve
> `runtime_code_allowed: true`. `@orchestrator` bu pack ile kod yazabilir; kapsam yalnızca yukarıdaki
> `runtime_code_scope` ile sınırlıdır ve oradaki YASAK maddeleri (ConceptGraph/MOD-0165/0164/0151 mutation,
> VisitFrequencyPolicy/CampaignTarget yazımı, materialized membership, MDM/registry/ocelot write) flip sonrası da bağlayıcıdır.
>
> **Bu dosya, kullanıcı talebindeki "FU-A (foundation)" paketidir.** Repo kanonik numaralandırması `FU-A/FU-B`
> değil `FUxx`'tir; ayrıca [DCP-006](../../../portfolio/delivery-capability-packs/DCP-006-crm-commercial-delivery-capability-pack.md)
> satır 23 zaten **MOD-0167-FU02**'yi bu izde (*Target Customer*, gate exit 0) ayırmıştır ve MOD-0167-FU01 §11/F1
> bu FU'yu adıyla ister. Bu nedenle **FU-A ≡ MOD-0167-FU02**; eşleme §1.2'de tablolanmıştır.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-27):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0167-FU02 --name "Segment Foundation - Definition, Criteria, Membership Resolution and Target Customer" --parent MOD-0167`
> → `OK  MOD-0167-FU02: proven against Blueprint/registry.` (**exit 0**).
> Parent `MOD-0167 | Segmentation / CDP` Blueprint canonical'dır (registry satırı 239, *DCP-002 gate OK 2026-07-14*).
> Registry satırı bu pack tarafından **yazılmaz** (§20/F-REG).
>
> **DCP-006 adlandırma uzlaşımı:** DCP-006 bu FU'yu dar biçimde *"Target Customer"* olarak önerdi. Bu pack onu
> **kapsayarak** genişletir (Segment + kriter + üyelik + TargetCustomer), çünkü `TargetCustomer`'ı sahibi olduğu
> `Segment` olmadan açmak, üyeliğin nereden geldiği sorusunu cevapsız bırakır ve ikinci bir üyelik master'ı
> doğurur. Ad çakışması değil, kapsam genişletmesidir; DCP-006 satır 23 bu pack ile **karşılanmış** sayılır.
>
> **📌 REVİZYON — 2026-08-27 (D-PRODUCT çözüldü):** Kullanıcı kararı ile **ürün-affinity FU-A'da YAPILABİLİR**:
> MOD-0162 ConceptGraph `Segment`/`Account` ile **aynı serviste** (`Diten.CrmService`) olduğu için affinity
> **derived-in-service** (sınıf **D**) bir kriterdir — cross-service değil, EA-TBD değil, FU-D'ye ertelenmiş değil.
> Kataloğa `concept.affinity` niteliği eklendi (§4.4). Revizyonun **dokunmadığı** kararlar: **D1–D6, D-TC, D-VER,
> D-TENANT, D-VOCAB, D-RBAC**; `VisitFrequencyPolicy` / `CampaignTarget` **hâlâ yazılmıyor**; ConceptGraph
> **mutate edilmiyor**; `status: draft` ve `runtime_code_allowed: false` **korunuyor** (flip ayrı karar).
>
> Otorite sırası: **Blueprint Excel** > bu pack > MOD-0167-FU01 (draft boundary) >
> [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 1. Module Summary

MOD-0167'nin cevapladığı tek soru **"kim?"**dir. Bugün repoda bu sorunun hiçbir cevabı yok: MOD-0167-FU01 bir
`Segment`'e *atıfta bulunur* ama onu **sağlamaz**; MOD-0165-FU04 `CampaignTarget.SourceReferenceId` alanında bir
segment id'si taşır ama o segmentin **var olduğunu doğrulayamaz**. Bu FU o boşluğu — ve yalnız onu — kapatır.

```text
Segment                = "kim bu kümede?" sorusunun TANIMI                       (BU FU — aggregate root)
  └── Criteria[]       = segment DOKÜMANI İÇİNDE embedded predicate ağacı        (BU FU — FU04/FU05 embedded deseni)
TargetCustomer         = MANUEL üyelik kaydı (include / exclude)                 (BU FU — AYRI collection, §4.3/D-TC)
Resolution             = kurala göre "şu an kimler uygun?" (persist YOK)         (BU FU — deterministik, fail-closed)
AttributeCatalog       = kriterde hangi alan, hangi operatörle kullanılabilir    (BU FU — kapalı, beyan edilmiş katalog)
```

| Cevapladığı soru | Sahip |
|---|---|
| Bu segment nedir, hangi konu için, hangi sürüm geçerli, ne zaman geçerli? | **Bu FU** |
| Segment kuralı nedir (nitelik + operatör + AND/OR/NOT + grup)? | **Bu FU** |
| `effectiveAt` anında bu kurala **kimler uyuyor** ve **kim neden elendi**? | **Bu FU** |
| Bu account/contact **elle** dâhil mi / **elle** hariç mi tutulmuş? | **Bu FU** (`TargetCustomer`) |
| "Şu contact, şu segmentin üyesi mi?" (MOD-0167-FU01 §5 seam'i) | **Bu FU** (`ISegmentMembershipReader`) |

| Cevaplamadığı soru | Sahip |
|---|---|
| Bu segmente **ne sıklıkla** gidilecek | MOD-0165 (`VisitFrequencyPolicy`) — bu FU policy **yazmaz** |
| Bu segmentten **hangi kampanya hedefleri** üretildi | MOD-0165-FU04 (`CampaignTarget`) — snapshot **orada** |
| Kime, ne zaman, hangi rotayla gidilecek; gerçekte ne oldu | MOD-0155 |
| İletişim izni var mı | MOD-0164 — bu FU **okur**, karar vermez, yazmaz |
| Hangi territory node kapsıyor | MOD-0151 — bu FU **okur**, atama yapmaz |
| Hangi kavram hangi kavrama bağlı (ürün ↔ uzmanlık grafiği) | MOD-0162-FU03 — bu FU **okur**, düğüm/ilişki yazmaz |
| Marka / ürün master | MDM / MOD-0290 — bu FU **okur**, kopyalamaz |
| Ziyaret stratejisi şablonu (`StrategyTemplate`), `SubjectList`, `UCLN` | **MOD-0167 — ama SONRAKİ FU'lar** (§1.2) |
| Segment kullanım logu (*segment usage logs*) | MOD-0167 — **FU-D** (§20/F-CDP) |
| ICP scoring | MOD-0167 — **FU-D** (DCP-006 satır 12: 0167 içi feature, ayrı MOD değil) |

**Temel mimari kural:** *`Segment` bir **tanımdır**, bir **liste** değildir.* Dinamik üyelik hiçbir yere
yazılmaz; her sorulduğunda yeniden türetilir. Bir dinamik segmentin üyelerini persist etmek, MOD-0167-FU01/D2'nin
("segment değiştiğinde kural sessizce eskir") yasakladığı hatanın ta kendisidir.

**Reddedilen dört model:** `Contact.SegmentCode` düz alanı · `Account.SegmentCode` düz alanı ·
`Campaign`'e gömülü segment kuralı (MOD-0165 SoR ihlali) · `VisitFrequencyPolicy`'ye kopyalanmış üye listesi
(MOD-0167-FU01/D2 ihlali).

### 1.1 D-Karar özeti (onayınıza sunulur — tam gerekçe ve reddedilen alternatifler: [Ek D](#ek-d--karar-gerekçeleri-tam))

| # | Karar | Öneri |
|---|---|---|
| **D1** | FU ayrışması | **FU-A = tam dinamik** (Segment + kriter + real-time eval + TargetCustomer). Statik/dinamik ikiye bölme **reddedildi** |
| **D2** | Kriter modeli | **Stored predicate-tree** (embedded, tiplenmiş). Query-DSL ve tag-based **reddedildi** |
| **D3** | Üyelik | **Real-time, persist YOK.** Materialization + refresh **FU-B**'ye ertelendi |
| **D4** | Ölçek | **İki fazlı bounded evaluation** (pushdown → post-filter) + sert tavanlar + **sessiz kırpma YOK** (422) |
| **D5** | Nitelik kataloğu | **Kapalı, beyan edilmiş katalog** + `attribute-catalog` endpoint'i. Serbest alan adı **reddedildi** |
| **D6** | Fail-closed sınırı | **In-service = değerlendirilir + gerekçelenir · Cross-process = 503, kısmi sonuç YOK** |
| **D-TC** | TargetCustomer şekli | **Ayrı collection**, yalnız `manual-include` / `manual-exclude`. Segment'e embedded üye listesi **reddedildi** |
| **D-VER** | Sürüm / effective dating | `SegmentVersion` (iş alanı, `Version` DEĞİL) + `active` sürümde kriter **DONDURULUR** + `new-version` klonu |
| **D-TENANT** | Tenant izolasyonu | `EntityBase` tenant-owned; `TenantId` server-side; cross-tenant **404 / boş liste**; çözümleme yalnız tenant içi |
| **D-VOCAB** | Vokabüler | **A = in-domain fail-closed** (FU02/FU03/FU04/FU05 emsali); MOD-0048 publish runtime ön koşulu **değil** |
| **D-PRODUCT** | Ürün-affinity | ✅ **ÇÖZÜLDÜ (2026-08-27)** — ürün-affinity **FU-A'da**, **ConceptGraph-türetilmiş derived-in-service** kriter (sınıf **D**): `concept.affinity`. **CANLI** (authoring-freeze yok), `specialty↔alan` eşleme tablosu **gerekmez** — eşlemeyi graph'ın kendisi dinamik olarak taşır |
| **D-RBAC** | Yetki | 6 kanonik anahtar **tanımlanır**, seed/grant **YOK**; belgelenmiş fallback ile çalışır (MOD-0165-FU04 emsali) |

### 1.2 FU decomposition önerisi (MOD-0167 yol haritası)

| Kullanıcı etiketi | Kanonik FU | Kapsam | Durum |
|---|---|---|---|
| **FU-A** | **MOD-0167-FU02** | `Segment` + kriter ağacı + **tam dinamik** real-time çözümleme + `TargetCustomer` + nitelik kataloğu + UI | **BU PACK (draft)** |
| **FU-B** | MOD-0167-FU03 *(önerilen)* | **Ölçek katmanı**: materialized membership + delta/refresh + değişiklik-tetikli yeniden hesaplama + üyelik geçmişi + snapshot karşılaştırma | önerilir; FU-A `done` olmadan açılmaz |
| **FU-C** | MOD-0167-FU04 *(önerilen)* | `StrategyTemplate` + `SubjectList` + `ForWhom` (legacy CrmV2 rule-capture'ın adapt edilmiş hâli) — **segment × strateji** bağı | önerilir |
| **FU-D** | MOD-0167-FU05 *(önerilen)* | **CDP nitelik birleştirme**: hesaplanmış/türev nitelikler (RFM, ICP score, son ziyaret, etkileşim yoğunluğu) + `segment usage log` | önerilir |
| — | MOD-0167-FU01 | Segment→frequency **co-author** boundary'si | **mevcut, BOZULMAZ** (§2.2) |
| — | MOD-0167-FU-RBAC | `crm.segment.*` katalog + rol ataması | önerilir, en sona |

> **Neden bu sıra:** FU-C (`StrategyTemplate`) ve FU-D (CDP nitelikleri) **ikisi de** "kim?" sorusunun cevabına
> yaslanır. FU-A olmadan FU-C'nin `ForWhom` alanı boş bir referans, FU-D'nin skoru ise sahibi olmayan bir sayıdır.
> FU-B ise FU-A'nın **performans borcunu** kapatır; FU-A ölçüm vermeden FU-B'yi tasarlamak spekülasyondur (§8.5).

### 1.3 Bu FU'nun MOD-0167-FU01'e sağladığı şey

MOD-0167-FU01 §5, membership seam'ini **sözleşme olarak** yetkilendirdi ve implementasyonunu "MOD-0167
segmentation engine FU'suna" bıraktı. Bu FU o implementasyondur:

```text
FU01 §5 sorusu:  "Bu contact/account, effectiveAt anında şu segmentin üyesi mi?"
FU02 cevabı:     ISegmentMembershipReader.IsMemberAsync(segmentId, subjectType, subjectId, effectiveAt)
                 → member | not-member | unknown  + reason codes   (§8.6)
```

**Bu FU frequency store'u AÇMAZ**: `VisitFrequencyPolicy` yazımı, `Source=segmentation` policy üretimi ve priority
zinciri MOD-0165'te kalır (§2.3).

---

## 2. Ownership and Boundaries

**In-scope:** `Segment` aggregate root'u ve **içindeki embedded kriter ağacı** · ayrı `TargetCustomer` aggregate'i
(yalnız manuel include/exclude) · CRUD-minus-delete (create/read/update/activate/archive; **DELETE ve PATCH yok**) ·
segment sürümleme (`new-version` klonu) + `active` sürümde kriter dondurma · effective dating ·
deterministik real-time üyelik çözümlemesi (`resolve` + `is-member`; **hiçbir şey persist etmez**) ·
kapalı nitelik kataloğu + `attribute-catalog` + `contract` endpoint'leri · in-domain vokabüler ·
read-only tüketim seam'i (`ISegmentMembershipReader`) · CRM Admin **tek** Compact sayfa · 7 dil RESX.

**Out-of-scope (§13 tam liste):** materialized membership · üyelik refresh/scheduler/event handler · üyelik geçmişi
tablosu · ICP scoring · `StrategyTemplate` / `SubjectList` / `UCLN` · segment usage log · `CampaignTarget` üretimi ·
`VisitFrequencyPolicy` yazımı · consent mutation · territory mutation · Account/Contact mutation · MDM write ·
visit/route planning · journey/automation · import/export yeni scope · hard delete · RBAC seed/grant ·
MOD-0048 publish · `ocelot.json` yazımı · registry yazımı · Mongo hand-edit.

### 2.1 Kilitli sınırlar (kullanıcı talebinden — değiştirilemez)

| Sınır | Karar |
|---|---|
| SoR | MOD-0167 `Segment` / `TargetCustomer` / `UCLN` / `SubjectList` / `StrategyTemplate` sahibidir ([crm-sor-boundary.md](../crm-sor-boundary.md) satır 21). Bu FU ilk ikisini açar |
| MOD-0167-FU01 | **BOZULMAZ.** Bu FU onun referansladığı `Segment`'i **sağlar**; frequency store'u **açmaz** (§2.2) |
| MOD-0165 | `VisitFrequencyPolicy` + `Campaign` + `CampaignTarget` SoR'u MOD-0165'te. **Tekrar edilmez**, yalnız gelecekte referanslanır (§2.3) |
| Legacy CrmV2 | `UCLN` / `StrategyTemplate` / `SubjectList` / `ForWhom` → **adapt-not-copy**. Legacy controller/view **taşınmaz** (§2.4) |
| Rep | **Rep = User** (JWT actor). Bu FU yeni bir rep/person master'ı **açmaz**; MOD-0288 referansı kriterde de **yer almaz** (§4.4) |
| Golden reference | **Compact** (segment çok alanlı — §11.1 türetmesi) |
| RBAC | Anahtarlar **tanımlanır**; seed/grant **YOK** (§14) |
| Registry / Gateway config | **YAZILMAZ**. Gateway route ihtiyacı `integration-agent` task'ı olarak §15'te ayrıştırılır |

### 2.2 MOD-0167-FU01 sözleşme koruması (kırmızı çizgi)

- FU01/**D1** (ayrı segment-frequency store açılmaz) ve FU01/**D2** (segment üyeliği policy'ye kopyalanmaz)
  **aynen geçerlidir**. Bu FU `VisitFrequencyPolicy` dosyalarına **dokunmaz** (§6 protected).
- FU01 §4'teki `TargetType=segment` policy profili **değişmez**; bu FU o profilin `SegmentId` alanının artık
  **doğrulanabilir** bir hedefi olmasını sağlar.
- FU01 §6 priority zinciri (`… > account (500) > segment (600) > territory-node (700) > …`) **bu FU'da yeniden
  tanımlanmaz ve uygulanmaz**.
- FU01 §5'teki "üyelik verisi yok → sonuç `unknown` kalır, varsayılan üyelik uydurulmaz" kuralı bu FU'nun
  `unknown` semantiğiyle **birebir** uyumludur (§8.4).

### 2.3 MOD-0165 sözleşme koruması (kırmızı çizgi)

- `CampaignTarget` SoR'u **MOD-0165**'tedir. Bu FU `CampaignTarget` **yazmaz, üretmez, şeması değişmez**.
- MOD-0165-FU02 §5.1 snapshot boundary'si aynen geçerli: snapshot bir **türev**tir, ikinci bir segment master'ı
  değildir; auto-refresh **yoktur**; provenance (`SourceReferenceType=segment`, `SourceReferenceId`,
  `SnapshotBatchId`) **görünür** kalır.
- MOD-0165-FU04'ün `CampaignTargetSnapshotHandler`'ı ileride bu FU'nun `resolve` çıktısını tüketebilir;
  **bu bağlantı bu FU'da kurulmaz** (§20/F-SNAPSHOT) — MOD-0165 kodu bu FU'da **değişmez**.
- `Campaign.BrandId/ProductId/SubjectId` gibi alanların hiçbiri `Segment`'e **kopyalanmaz**.

### 2.4 Legacy CrmV2 — adapt-not-copy

[legacy-value-preservation.md](../legacy-value-preservation.md) satır 28: *TargetCustomer / UCLN / StrategyTemplate /
SubjectList / ForWhom → CrmV2 → MOD-0167 → Reference schema + rule capture → **Segment eval greenfield***.

| Legacy kavram | Bu FU'daki karşılığı |
|---|---|
| `TargetCustomer` | **Alınır** — ama yalnız *manuel üyelik kaydı* olarak daraltılmış anlamıyla (§4.3) |
| Kriter yakalama (kural mantığı) | **Alınır** — predicate ağacı olarak yeniden yazılır (§4.2) |
| `UCLN` / `StrategyTemplate` / `SubjectList` / `ForWhom` | **Alınmaz** — FU-C'ye ertelenir (§1.2) |
| Legacy controller / view / DataTable | **TAŞINMAZ.** `frontend/Diten.Web/{Controllers,Views}/Archive/**` FROZEN (§6) |

---

## 3. Owned Objects

| Tür | Nesne |
|---|---|
| **Entity** | `Segment` (aggregate root) · `SegmentCriteriaNode` (embedded, `Segment.Criteria`) · `TargetCustomer` (ayrı aggregate) |
| **Repository** | `ISegmentRepository` · `ITargetCustomerRepository` (2 repo, 2 collection — §4.6) |
| **Commands** | `CreateSegment` · `UpdateSegment` · `ActivateSegment` · `ArchiveSegment` · `CreateSegmentVersion` · `AddTargetCustomer` · `UpdateTargetCustomer` · `ArchiveTargetCustomer` |
| **Queries** | `ListSegments` · `GetSegmentById` · `GetSegmentContract` · `GetSegmentAttributeCatalog` · `ResolveSegmentMembership` · `EvaluateSegmentMembership` (is-member) · `ListTargetCustomers` · `ListSubjectSegments` |
| **Services** | `SegmentCriteriaEvaluator` · `SegmentMembershipResolver` (motor) · `SegmentAttributeCatalog` (kapalı katalog) · `ISegmentAttributeSourceReader` (nitelik kaynağı adaptörleri) · `ISegmentProductReferenceValidator` (cross-service, fail-closed) |
| **Consumer seam** | `ISegmentMembershipReader` (read-only; MOD-0167-FU01 §5 + MOD-0165 snapshot tüketicisi için) |
| **API** | §8.1 — 15 endpoint, hepsi `/api/crm/segments…` (+ 1 ters sorgu `/api/crm/subjects/…`) altında |
| **Frontend route** | `/CRM/Segments` (tek Compact sayfa) |
| **Permissions** | `crm.segment.read` · `.manage` · `.activate` · `.resolve` · `crm.segment.target.read` · `.target.manage` (§14) |

---

## 4. Entity Fields

### 4.1 `Segment` (aggregate root)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Id` | Guid | otomatik | `SegmentId`. `EntityBase` |
| `TenantId` | Guid | server-side | Payload'da **yer almaz** (D-TENANT) |
| `SegmentCode` | string | **Evet** | Kararlı iş anahtarı; tenant içinde arşivlenmemişler arasında **unique**; **rename edilmez** (ad değişimi `SegmentName` ile) |
| `SegmentName` | string | **Evet** | max 200, trim |
| `SegmentType` | string | **Evet** | `static` \| `dynamic` \| `hybrid` (§4.5) |
| `SubjectType` | string | **Evet** | `account` \| `contact` — segmentin **neyi** kümelediği. **Create sonrası IMMUTABLE** (bir segment sessizce başka bir soruyu cevaplayamaz — MOD-0164 emsali) |
| `SegmentStatus` | string | **Evet** | `draft` \| `active` \| `archived` (§4.5). Varsayılan `draft` |
| `SegmentVersion` | int | **Evet** | **İş** sürümü; ilk sürüm `1`. `EntityBase.Version` (concurrency token) ile **karıştırılmaz** — `entity-base-template.md` naming kuralı |
| `VersionLineageId` | Guid | **Evet** | Aynı segmentin tüm sürümlerini bağlayan kök kimlik. İlk sürümde `= Id` |
| `SupersededBySegmentId` | Guid? | Hayır | Bu sürümü geçersizleştiren yeni sürüm. `new-version` + `activate` ile server-side dolar |
| `BusinessUnitId` | string? | Hayır | Opak MOD-0048 business-unit kodu (boş-olmayan string doğrulaması; MOD-0288 master'ı **okunmaz**) |
| `Description` | string? | Hayır | max 2000 |
| `EffectiveFrom` | DateTimeOffset | **Evet** | Sürümün geçerlilik başlangıcı |
| `EffectiveTo` | DateTimeOffset? | Hayır | Boş = açık uçlu. `EffectiveTo > EffectiveFrom` |
| `MatchMode` | string | **Evet** | Kök birleştirici: `all` (AND) \| `any` (OR). Varsayılan `all` |
| `Criteria` | `List<SegmentCriteriaNode>` | koşullu | `SegmentType != static` iken **en az 1 predicate zorunlu**; `static` iken **boş olmak zorunda** (§12) |
| `Notes` | string? | Hayır | max 2000 |
| `CriteriaFrozenAt` | DateTimeOffset? | Hayır | `activate` anında server-side damgalanır; dolu iken kriter **değişmez** (D-VER) |
| `ActivatedAt` / `ActivatedBy` | DateTimeOffset? / string? | Hayır | Audit damgası |
| `ArchivedAt` / `ArchivedBy` | DateTimeOffset? / string? | Hayır | Soft lifecycle; **hard delete yok** |
| `CreatedBy` / `UpdatedBy` | string? | Hayır | Actor damgası |
| `Version` | int | otomatik | **Teknik** concurrency token (`EntityBase`) |

> `Segment` üzerinde **bulunmayan ve bulunamayacak** alanlar: `MemberIds[]` · `MemberCount` (canlı sayı bir
> **türev**dir, §8.3) · `LastResolvedAt` · `FrequencyCode` (MOD-0165) · `CampaignId` (MOD-0165) ·
> `TerritoryNodeId` (MOD-0151) · `ConsentStatus` (MOD-0164) · `ProductId`/`BrandId` kopyası (MDM).

### 4.2 `SegmentCriteriaNode` — **embedded** (`Segment.Criteria[]`, D2)

Düz liste olarak saklanan, `ParentNodeId` ile ağaç kuran tiplenmiş predicate düğümü. (İç içe C# tipi yerine düz
liste: Mongo class-map'i basit kalır, derinlik doğrulaması tek geçişte yapılır, UI repeater'ı doğrudan eşlenir.)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `NodeId` | Guid | otomatik | Segment içinde unique |
| `ParentNodeId` | Guid? | Hayır | `null` = kök çocuğu. Döngü **yasak**; parent aynı segmentte olmalı |
| `NodeKind` | string | **Evet** | `group` \| `predicate` (§4.5) |
| `GroupOperator` | string? | koşullu | `NodeKind=group` iken **zorunlu**: `and` \| `or` \| `not`. `predicate` iken **boş** |
| `AttributeCode` | string? | koşullu | `NodeKind=predicate` iken **zorunlu**; **katalogda olmak zorunda** (§4.4) — serbest metin **kabul edilmez** |
| `Operator` | string? | koşullu | `predicate` iken zorunlu; katalogun o nitelik için **izin verdiği** operatör olmak zorunda (§4.4) |
| `Values` | `List<string>` | koşullu | Operatörün arity'sine göre: `eq/ne/contains/gt/lt/gte/lte` → 1 · `between` → 2 · `in/not-in` → 1..50 · `is-null/is-not-null` → 0 |
| `ValueType` | string? | koşullu | `string` \| `number` \| `date` \| `bool` \| `guid` — katalogla **eşleşmek zorunda** |
| `Parameters` | `Dictionary<string,string>` | Hayır | Nitelik-özel bağlam (ör. consent niteliği için `channel` + `purpose`; katalog hangi parametrenin zorunlu olduğunu **beyan eder**) |
| `Negate` | bool | Hayır | Düğüm düzeyinde NOT (grup `not` operatörüne alternatif kısayol) |
| `SortOrder` | int | **Evet** | Aynı parent altında **unique**; determinizmin (§8.3) bir parçası |
| `Label` | string? | Hayır | Yalnız görüntü; **değerlendirmede kullanılmaz** |

**Ağaç sınırları (doküman büyümesi + değerlendirme maliyeti):** max **derinlik 5** · max **100 düğüm** ·
grup başına max **20 çocuk** · `in/not-in` başına max **50 değer**. Aşım → **400**, sessiz kırpma **yok**.

### 4.3 `TargetCustomer` — **ayrı aggregate** (D-TC)

*"Somut üyelik kaydı"* — ama **yalnız insanın elle yazdığı** üyelik. Türetilmiş üyelik buraya **asla** yazılmaz.

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Id` | Guid | otomatik | `TargetCustomerId` |
| `SegmentId` | Guid | **Evet** | Sahibi segment. **IMMUTABLE** |
| `SubjectType` | string | **Evet** | `account` \| `contact` — **segmentin `SubjectType`'ı ile eşleşmek zorunda** (§12) |
| `SubjectId` | Guid | **Evet** | Çözümleme anahtarı. Referans edilen master **okunmaz/mutate edilmez** — çağıran id'yi verir (`CampaignTarget` emsali) |
| `MembershipMode` | string | **Evet** | `manual-include` \| `manual-exclude` (§4.5). Üçüncü bir değer **yoktur** — türetilmiş üyelik burada saklanmaz |
| `SubjectDisplayName` | string? | Hayır | **Yalnız görüntü/audit.** Açıkça SoT **değildir**; tüketici adı sahibinden çözer (`CampaignTarget.TargetDisplayName` emsali) |
| `SelectionReason` | string | **Evet** | Serbest metin gerekçe. **Gerekçesiz manuel üyelik authorable değildir** (MOD-0165-FU04 emsali) |
| `ReasonCodes` | `List<string>` | **Evet** | Boş olamaz (§4.5 `SegmentReasonCodes`) |
| `EffectiveFrom` | DateTimeOffset | **Evet** | Üyeliğin başlangıcı |
| `EffectiveTo` | DateTimeOffset? | Hayır | Açık uçlu olabilir |
| `Notes` | string? | Hayır | max 2000 |
| `ArchivedAt` / `ArchivedBy` | DateTimeOffset? / string? | Hayır | Soft lifecycle; **hard delete yok** |
| `CreatedBy` / `UpdatedBy` | string? | Hayır | Actor damgası |
| `TenantId` / `Version` / `IsDeleted` | — | — | `EntityBase` |

**Benzersizlik:** `(TenantId, SegmentId, SubjectType, SubjectId)` başına **en fazla bir** arşivlenmemiş kayıt →
ikinci ekleme **409**. `manual-include` ↔ `manual-exclude` geçişi bir **update**'tir, ikinci satır değil.

### 4.4 Nitelik kataloğu (D5) — **kapalı, beyan edilmiş**

Kriterde kullanılabilecek her nitelik `SegmentAttributeCatalog` içinde **kod olarak beyan edilir** ve
`GET /api/crm/segments/attribute-catalog` ile **aynen** yayınlanır. Katalogda olmayan `AttributeCode` → **400**.

| Sınıf | Nasıl değerlendirilir | Bağımlılık kırılırsa |
|---|---|---|
| **N — native** | Mongo filtresine **pushdown** (Faz 1, §8.5) | — (in-service, aynı collection) |
| **J — in-service join** | `AccountContactLink` üzerinden aday daraltma (Faz 1.5) | — |
| **D — derived in-service** | Aday kümesi üzerinde **post-filter** (Faz 2), toplu okuma | **Elenir + reason code** (§8.4) |
| **X — cross-service** | HTTP (Gateway) — **yalnız kriter DEĞERİNİN doğrulaması**, üyelik türetmez | **503, kısmi sonuç YOK** (§8.4) |

| `AttributeCode` | Sınıf | Kaynak | Tip | Operatörler |
|---|---|---|---|---|
| `account.type` · `account.category` · `account.status` | N | `Account` (MOD-0149) | string | `eq` `ne` `in` `not-in` `is-null` `is-not-null` |
| `account.country` · `account.city` · `account.district` | N | `Account.{Country,City,District}Ref` | string | `eq` `ne` `in` `not-in` |
| `account.parent-account` | N | `Account.ParentAccountId` | guid | `eq` `ne` `is-null` `is-not-null` |
| `account.created-at` | N | `Account.CreatedAt` | date | `gt` `gte` `lt` `lte` `between` |
| `account.attribute` | N | `AccountAttributeValue` (`Parameters.attributeCode` **zorunlu**) | string | `eq` `ne` `in` `not-in` `contains` `is-null` `is-not-null` |
| `contact.type` · `contact.status` · `contact.gender` | N | `Contact` (MOD-0150) | string | `eq` `ne` `in` `not-in` |
| `contact.specialty` · `contact.professional-title` · `contact.department` | N | `Contact` | string | `eq` `ne` `in` `not-in` `contains` `is-null` `is-not-null` |
| `contact.country` · `contact.city` · `contact.district` · `contact.preferred-language` | N | `Contact` | string | `eq` `ne` `in` `not-in` |
| `contact.created-at` | N | `Contact.CreatedAt` | date | `gt` `gte` `lt` `lte` `between` |
| `contact.account-role` | J | `AccountContactLink.RoleCode` (aktif bağ) | string | `eq` `in` |
| `contact.is-primary` | J | `AccountContactLink.IsPrimary` (aktif bağ) | bool | `eq` |
| `contact.account-type` | J | ilişkili `Account.AccountType` | string | `eq` `in` |
| `territory.has-coverage` | D | MOD-0151 `AccountCurrentCoverageResolver` | bool | `eq` |
| `territory.node` | D | MOD-0151 current coverage | guid | `eq` `in` |
| `territory.model` | D | MOD-0151 current coverage | guid | `eq` `in` |
| `consent.eligibility` | D | MOD-0164 `IConsentPreferenceEvaluator` (`Parameters.channel` + `Parameters.purpose` **zorunlu**) | string | `eq` `in` — değerler: `allowed` \| `blocked` \| `unknown` \| `not_applicable` |
| `consent.scope-product` / `consent.scope-brand` | D + **X** | MOD-0164 consent scope; **değer** MDM'de fail-closed doğrulanır | guid | `eq` `in` |
| **`concept.affinity`** | **D** | **MOD-0162-FU03 ConceptGraph** (salt-okunur) — "bu ürünle ilgilenen" türetmesi (§4.4.1). Değer = MDM **global-product** id'si; değer **X** olarak MDM'de fail-closed doğrulanır | guid | `eq` `in` |

#### 4.4.1 `concept.affinity` — ürün-affinity türetmesi (D-PRODUCT, sınıf D)

**Cevapladığı soru:** *"Bu doktor, şu ürünle ilgilenen bir doktor mu?"* — ve bunu **kişiye yazılmış hiçbir ürün
alanı olmadan**, yalnız kavram grafiğinden türeterek cevaplar.

```text
Değer: global-product P
  1. ConceptNode'lar  → ExternalRefType = "global-product" ve ExternalRefId = P  (active + effectiveAt)   [ürün düğümleri]
  2. Çıkan kenarlar   → RelationshipType ∈ { "addresses", "belongs-to" }, aynı SubjectId, active + effectiveAt
                        BOUNDED: varsayılan derinlik 1, max 2 (Parameters.maxDepth; >2 → 400)
  3. Varılan düğümler → ExternalRefType = "reference-data-value"                                          [uzmanlık düğümleri]
  4. Uzmanlık kümesi  → o düğümlerin ExternalRefId değerleri = BRD specialty value code kümesi  S
  5. Eşleme           → aday contact'ın in-service `contact.specialty` değeri  S ∈ ise ÜYE
```

| Kural | Karar |
|---|---|
| Yön | **Yalnız çıkan (outbound)** kenar izlenir. `Direction=bidirectional` **açık bir beyandır** ve izlenir; ters kenar **asla türetilmez** (MOD-0162-FU03 kuralı aynen korunur) |
| İlişki tipi | Yalnız `addresses` ve `belongs-to`. `leads-to` / `requires` / `evidences` / `custom` **izlenmez** (bunlar anlatım/kanıt akışıdır, ilgi alanı değil) |
| Derinlik | `Parameters.maxDepth` opsiyonel; varsayılan **1**, tavan **2**. Tavan aşımı → **400**. Geçişli kapanış (transitive closure) **yoktur** |
| Konu daraltması | `Parameters.subjectId` opsiyonel — verilmezse tenant'taki **tüm** konuların ürün düğümleri değerlendirilir |
| Lifecycle | Yalnız `active` **ve** `effectiveAt` penceresi içindeki düğüm/kenarlar; `archived` / `inactive` **sayılmaz** |
| `SubjectType` | Nitelik **yalnız** `SubjectType=contact` segmentlerinde kullanılabilir (uzmanlık kişiye aittir) → `account` segmentinde **400** `segment_attribute_not_applicable_for_subject_type` |
| Eşleme tablosu | **YOKTUR.** `specialty ↔ terapötik alan` eşlemesi bir tabloya yazılmaz; eşlemeyi graph **kendisi** taşır ve graph değişince kriter **anında** değişir |
| Dondurma | Affinity **CANLI**'dır — `activate` yalnız **kriter ağacını** dondurur (D-VER), türetmenin **sonucunu** değil. Graph güncellenince aynı segment aynı sürümle farklı üye kümesi döndürebilir; bu **beklenen** davranıştır ve determinizm sözleşmesi (§8.3) "**değişmemiş kaynak veri**" koşuluyla ifade edilmiştir |
| Okuma profili | Çözümleme başına **TEK bulk graph read** (§8.5 Faz 2); aday başına graph çağrısı **YASAK** |
| Mutation | **YOK.** ConceptGraph'a yazılmaz, kopyalanmaz; yeni graph aggregate/endpoint/repository metodu/imza değişikliği **açılmaz** (§6) |

**Tüketim biçimi (mevcut yüzey, imza değişmeden):** adaptör MOD-0162-FU03'ün **var olan** salt-okunur
repository/query yüzeyini kullanır — `IConceptNodeRepository.ListAsync` / `ListBySubjectAsync` ·
`IConceptRelationshipRepository.ListAsync` / `ListBySubjectAsync` (ve gerekirse MediatR
`GetConceptGraphQuery` / `GetConceptGraphByNodeQuery`). **Not:** bu yüzeyde "ExternalRef ile düğüm bul" metodu
**yoktur**; ürün düğümü çözümlemesi, mevcut graph handler'larının kendi yaptığı gibi (liste + bellek-içi filtre)
yapılır. Bu, MOD-0162'ye **tek satır** değişiklik gerektirmez **ve** N+1 yasağını yapısal olarak sağlar
(çözümleme başına tek liste okuması).

> **Kod adı notu:** kullanıcı talebindeki *`concept-affinity`* niteliği budur; katalog `{kaynak}.{ad}` biçimini
> koruduğu için `AttributeCode` **`concept.affinity`** olarak yazılır.

> **"Uzmanlık / tier" niteliği (kullanıcı talebi):** *uzmanlık* → `contact.specialty` (N, hazır).
> *tier* → repoda **birinci sınıf alan yoktur**; en yakın gerçek `account.attribute` +
> `Parameters.attributeCode="tier"` (`AccountAttributeValue`, N). Bu bir **tenant-authored** anahtardır;
> attribute-definition SoR'u `AccountAttributeValue` başlığında zaten **EA-TBD**'dir. Katalog `account.attribute`'ü
> beyan eder, `tier`'i **uydurmaz** (§20/F-TIER).

> **Katalogda kasıtla BULUNMAYANLAR:** `visit.*` / `last-visit` (MOD-0155 — repoda visit runtime yok) ·
> `frequency.*` (MOD-0165) · `campaign.*` (MOD-0165) · `journey.*` (MOD-0166) · `knowledge.content.*`
> (MOD-0162-FU02 içerik niteliği — `concept.affinity` **içerik** değil **kavram** okur) ·
> `rep.*` / `person.*` (Rep = User; person master MOD-0288) · `score.*` / `rfm.*` / `icp.*` (**FU-D**) ·
> `segment.*` (segment içinde segment = döngü riski; **FU-B**).

**D-PRODUCT — ✅ ÇÖZÜLDÜ (kullanıcı kararı, 2026-08-27): ürün-affinity FU-A'da, ConceptGraph-türetilmiş
derived-in-service kriter.**

Önceki taslak bu kriteri *"Account/Contact ↔ Product ilgi bağı repoda yok → EA-TBD → FU-D"* diye ertelemişti.
**Bu erteleme yanlıştı ve geri alındı:** doğrudan bir ilgi bağı gerçekten yoktur, ama **gerekli de değildir** —
ilgi bağı zaten **MOD-0162-FU03 ConceptGraph** içinde, `ürün düğümü --addresses/belongs-to--> uzmanlık düğümü`
kenarları olarak **var** ve o graph `Segment` ile **aynı serviste** (`Diten.CrmService`) duruyor. Yani affinity
**yeni bir aggregate değil, mevcut bir grafiğin salt-okunur türevidir**.

| Karar | Değer |
|---|---|
| Nitelik | `concept.affinity` (§4.4.1) |
| Sınıf | **D — derived-in-service** (cross-service **değil**; §8.4 ayrımına göre belirsizlik **503 üretmez**) |
| Ne zaman | **FU-A** — FU-D'ye ertelenmedi |
| Tazelik | **CANLI** — authoring-freeze **yok**; graph değişince kriterin cevabı değişir |
| Eşleme tablosu | **Gerekmez** — `specialty ↔ terapötik alan` eşlemesini graph'ın kendisi **dinamik** olarak taşır |
| ConceptGraph'a etki | **Sıfır** — okunur; node/ilişki/tip/şablon yazılmaz, imza genişletilmez, kopyalanmaz (§6) |

**Ürünün üç ayrı rolü (karıştırılmamalı):**
1. **`concept.affinity`** — ürün, **üyelik türetiminin girdisidir** (graph üzerinden uzmanlığa iner). *(YENİ)*
2. **`consent.scope-product` / `consent.scope-brand`** — ürün, **consent'in scope'udur**; üyelik consent üzerinden türer.
3. Her üç nitelikte de ürün/marka id'si **X sınıfı fail-closed referans doğrulamasından** geçer
   (`GET /api/mdm/products/{id}` — §8.4): 404 → **400**, ulaşılamıyor → **503**, persist **yok**.

**Veri hizalama ön koşulu (KOD DEĞİL — operatör/veri işi, `F-RD` ile aynı sınıf → §20/F-CONCEPT-DATA).**
`concept.affinity` **doğru çalışması için** iki hizalama ister; ikisi de bu FU'nun **kod kapsamı dışındadır** ve
eksikliği bir **hata değil**, boş sonuçtur (aday elenir + reason code — §8.4):
- **(a)** Concept graph, **ürün → uzmanlık** ilişkileriyle doldurulmuş olmalı
  (`ExternalRefType=global-product` düğümü → `addresses`/`belongs-to` → `ExternalRefType=reference-data-value` düğümü).
- **(b)** Doktorun `contact.specialty` değeri ile uzmanlık düğümünün `ExternalRefId`'si **AYNI BRD specialty
  set'inden** gelmeli. İki farklı set kullanılırsa kriter **sessizce boş** döner — bu yüzden bu koşul
  §16'da açık bir AC, §18'de açık bir checklist maddesidir.

### 4.5 Vokabüler — **D-VOCAB = A (in-domain fail-closed)**

`Domain/Entities/Segment.cs` içinde `static class` olarak; set dışı değer → **400**. MOD-0048 publish'i runtime ön
koşulu **değildir** (MOD-0162-FU02/FU03/FU04/FU05 ve MOD-0164-FU02 emsali; setler ayrı operatör işi → §20/F-RD).

```text
SegmentTypes            : static | dynamic | hybrid
SegmentSubjectTypes     : account | contact
SegmentStatuses         : draft | active | archived
SegmentMatchModes       : all | any
SegmentCriteriaNodeKinds: group | predicate
SegmentGroupOperators   : and | or | not
SegmentOperators        : eq | ne | in | not-in | contains | gt | gte | lt | lte | between | is-null | is-not-null
SegmentValueTypes       : string | number | date | bool | guid
MembershipModes         : manual-include | manual-exclude
MembershipVerdicts      : member | not-member | unknown
SegmentReasonCodes      : criteria_matched | criteria_not_matched | manual_include | manual_exclude |
                          consent_unknown | consent_blocked | territory_coverage_unavailable |
                          concept_product_node_missing | concept_affinity_no_specialty_reached |
                          concept_affinity_not_matched | concept_subject_specialty_missing |
                          attribute_not_resolvable | subject_type_mismatch | outside_effective_window |
                          segment_not_active | dependency_unavailable

ConceptAffinityRelationshipTypes : addresses | belongs-to        # izlenen kenar tipleri (§4.4.1) — MOD-0162 vokabülerinin ALT KÜMESİ,
                                                                #  yeni vokabüler DEĞİL; MOD-0162 seti burada yeniden tanımlanmaz
```

### 4.6 Persistence kararı — **2 collection**

| Collection | İçerik | Gerekçe |
|---|---|---|
| `segments` | `Segment` + embedded `Criteria[]` | Kriter ağacı **sınırlıdır** (max 100 düğüm) ve segmentle aynı ömrü/aynı concurrency token'ı paylaşır → embedded (FU04/D2 + FU05/S2 emsali). Tek doküman yazımı ⇒ transaction/compensation **gerekmez** |
| `target_customers` | `TargetCustomer` | **Kardinalite sınırsızdır** (statik segmentte binlerce satır), ömrü segmentten bağımsızdır, satır başına provenance taşır ve MOD-0165 tarafından referanslanır → embedded **reddedildi** (16MB limiti + satır düzeyi concurrency + sorgulanabilirlik) |

**Index'ler** (`Persistence/DependencyInjection.cs`, additive):
- `segments`: `(TenantId, SegmentCode)` **unique partial** (arşivlenmemiş + `IsDeleted=false`) ·
  `(TenantId, SegmentStatus, SegmentType)` · `(TenantId, VersionLineageId, SegmentVersion)` · `(TenantId, SubjectType)`
- `target_customers`: `(TenantId, SegmentId, SubjectType, SubjectId)` **unique partial** (arşivlenmemiş) ·
  `(TenantId, SubjectId)` (ters sorgu: "bu kişi hangi segmentlere elle eklenmiş?")
- **Kritik:** `crm-datetimeoffset-array-pitfalls` — **iki `DateTimeOffset` alanı aynı index'e veya aynı sort'a
  KONULMAZ** (`EffectiveFrom` + `EffectiveTo` birlikte index'lenmez, birlikte sort edilmez). Mongo partial index
  filtresinde **`$ne` kullanılmaz** (`mongo-partial-index-ne-crash`) → `$type`/`$exists` ile ifade edilir.
- **Class-map:** `Segment`, `SegmentCriteriaNode`, `TargetCustomer` üçü de `RegisterClassMaps`'e eklenir —
  aksi hâlde Guid FK'lar binary yazılır, filtreler string serialize eder ve sorgular **sessizce boş döner**
  (`crm-new-aggregate-classmap-guid`).

---

## 5. Repo Scope

```text
# --- backend ---
services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Segment.cs                                  (yeni; Segment + embedded SegmentCriteriaNode + vokabüler + reason-code static class'ları)
services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/TargetCustomer.cs                            (yeni)
services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/ISegmentRepository.cs                    (yeni)
services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/ITargetCustomerRepository.cs             (yeni)
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Segmentation/**                         (yeni — §10)
services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/SegmentRepository.cs                (yeni)
services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/TargetCustomerRepository.cs         (yeni)
services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs                           (RegisterClassMaps + index + DI — additive)
services/Diten.CrmService/src/Diten.CrmService.Infrastructure/Segmentation/MdmSegmentProductReferenceValidator.cs  (yeni — cross-service fail-closed; WorkingCalendarLegalEntityValidator deseni)
services/Diten.CrmService/src/Diten.CrmService.Infrastructure/DependencyInjection.cs                        (HttpClient + DI — additive)
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/SegmentsController.cs                    (yeni — segment + resolve + alt-kaynak target'lar)
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/SegmentContractController.cs             (yeni — contract + attribute-catalog)
services/Diten.CrmService/src/Diten.CrmService.Api/Models/CRM/SegmentRequests.cs                            (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Segmentation/SegmentAggregateTests.cs          (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Segmentation/SegmentCriteriaValidationTests.cs (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Segmentation/SegmentMembershipResolverTests.cs (yeni — determinizm + fail-closed)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Segmentation/TargetCustomerRulesTests.cs       (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Segmentation/SegmentAttributeCatalogTests.cs   (yeni)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Segmentation/ConceptAffinityResolutionTests.cs (yeni — §4.4.1 türetmesi: bounded depth, tek bulk read, boş küme, ExternalRef eşleşmesi)

# --- frontend: TEK proxy controller + viewmodel ---
frontend/Diten.Web/Controllers/CRM/SegmentsController.cs                                                   (yeni, proxy-only)
frontend/Diten.Web/Models/CRM/SegmentViewModels.cs                                                         (yeni; segment VM + kriter düğümü VM + üye VM)

# --- frontend: Views/CRM/Segments/ — DEV-0001 Compact kanonik 9 dosya (§11.2) ---
frontend/Diten.Web/Views/CRM/Segments/Index.cshtml                                                         (Layout="_LayoutTenantShell" AÇIKÇA)
frontend/Diten.Web/Views/CRM/Segments/Create.cshtml
frontend/Diten.Web/Views/CRM/Segments/Edit.cshtml
frontend/Diten.Web/Views/CRM/Segments/Details.cshtml                                                       (salt-okunur kriter ağacı + çözümleme önizlemesi + üye listesi)
frontend/Diten.Web/Views/CRM/Segments/_Form.cshtml                                                         (segment formu + GÖMÜLÜ kriter ağacı editörü + GÖMÜLÜ üye alt-editörü)
frontend/Diten.Web/Views/CRM/Segments/_Filter.cshtml
frontend/Diten.Web/Views/CRM/Segments/_DataTable.cshtml                                                    (data-dt-standard="v2" + skeleton; TEK DataTable = segment listesi)
frontend/Diten.Web/Views/CRM/Segments/_IndexL10n.cshtml
frontend/Diten.Web/Views/CRM/Segments/SegmentsIndex.cs                                                     (marker class)

# --- frontend: JS + RESX + nav ---
frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/{index.js, index.l10n.js, form.js}                       (form.js: kriter ağacı repeater + katalog-güdümlü operatör/değer alanları + üye repeater + resolve önizleme)
frontend/Diten.Web/Resources/Views/CRM/Segments/SegmentsIndex.{ar,en,es,fr,ru,tr,zh}.resx                   (7 dil)
frontend/Diten.Web/Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx                                     (SegmentsMenu anahtarı ×7)
frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml                                                   (TEK <li>, dar istisna — §6)

# --- doğrulama ---
scripts/smoke-mod0167-fu02-segment-foundation-authenticated.ps1                                             (yeni; MOD-0162-FU05 script'i şablon)
docs/audits/mod-0167-fu02-segment-foundation-*.md                                                           (evidence)
```

> **Repo scope'a HİÇ girmeyenler:** materialized membership collection'ı · refresh job / hosted service / scheduler ·
> `segment_membership_history` · `segment_usage_log` · `StrategyTemplate` / `SubjectList` / `UCLN` dosyaları ·
> `CampaignTarget*` dosyaları · `VisitFrequencyPolicy*` dosyaları · ikinci golden-reference sayfası
> (`Views/CRM/TargetCustomers/**` **yoktur**) · `ocelot.json` (§15).

---

## 6. Protected Paths

`.antigravity/**` · `gateway/Diten.ApiGateway/**/ocelot.json` (**bu pack yazmaz** — route ihtiyacı `integration-agent`
task'ı, §15) · `services/Diten.MdmService/**` · `services/Diten.Platform/**` · `services/Diten.AuthService/**` ·
`services/Diten.HcmService/**` · `services/Diten.EnterpriseStrategyService/**` ·
`services/Diten.DevEnablementService/**` (Golden Reference — okunur, değiştirilmez) ·
**MOD-0165 yüzeyi**: `Features/Campaign/**`, `Features/VisitFrequencyPolicy/**`, `Domain/Entities/Campaign.cs`,
`Domain/Entities/VisitFrequencyPolicy.cs`, `Api/Controllers/CRM/{Campaigns,VisitFrequencyPolicies}Controller.cs`,
`Views/CRM/Campaigns/**` ·
**MOD-0164 yüzeyi**: `Features/ConsentPreference/**` (`IConsentPreferenceEvaluator` imzası **dâhil**),
`Domain/Entities/ConsentRecord.cs`, `Views/CRM/ConsentPreferences/**` ·
**MOD-0151 yüzeyi**: `Features/Territory/**` (`AccountCurrentCoverageResolver` **dâhil**),
`Domain/Entities/Territory*.cs`, `Views/CRM/TerritoryManagement/**` ·
**MOD-0149/0150 yüzeyi**: `Features/{Account,Contact,AccountContact}/**`,
`Domain/Entities/{Account,Contact,AccountContactLink,AccountAttributeValue}.cs` (**okunur, mutate edilmez**) ·
**MOD-0162-FU03 ConceptGraph yüzeyi — SALT-OKUNUR tüketilir, DEĞİŞTİRİLMEZ (§4.4.1):**
`Features/Knowledge/Concept/**` (`Graph/ConceptGraphQueries.cs` + `Graph/ConceptGraphQueryHandlers.cs` **dâhil**),
`Domain/Entities/{ConceptNode,ConceptRelationship,ConceptType,ConceptChainTemplate,ConceptGraphVocabulary,KnowledgeContentConceptLink}.cs`,
`Domain/Repositories/IConceptGraphRepositories.cs` (**imza genişletilmez — yeni repository metodu eklenmez**),
`Api/Controllers/CRM/KnowledgeConcept*.cs`, `Views/CRM/KnowledgeConcepts/**`,
`wwwroot/assets/js/CRM/KnowledgeConcepts/**` ·
**MOD-0162 diğer Knowledge yüzeyi** (FU02 içerik, FU04 path, FU05 journey) ·
RBAC seed / role template / permission catalog (`crm.segment.*` **kataloğa yazılmaz**) ·
MOD-0048 publish · Mongo hand-edit · `execution/registries/**` (yalnız closeout'ta, kullanıcı onayıyla) ·
`execution/portfolio/**` · **MOD-0167-FU01 pack dosyası** (okunur, değiştirilmez) ·
`frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN) · `frontend/Diten.Web/Controllers/Archive/**` +
`frontend/Diten.Web/Views/Archive/**` (FROZEN — legacy CrmV2 buradan **taşınmaz**, §2.4).

**Kasıtlı dokunulan tek istisna (protected DEĞİL — dar kapsam):**
`frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — CRM Admin nav'ına **tek `<li>`**
(*Segments* → `/CRM/Segments`, permission-guard'lı) eklenir; mevcut CRM `<li>`'leri, `active` yol mantığı ve
oturum davranışı **değişmez**.

---

## 7. Dependencies

| Bağımlılık | Yön | Sözleşme / etki |
|---|---|---|
| **MOD-0167-FU01** (draft boundary) | implement eder | §5 membership seam'i BURADA runtime'a döner (§1.3). D1/D2 **aynen** korunur; frequency store **açılmaz** |
| **MOD-0165-FU02 / FU04** | **consumer** | `CampaignTarget` snapshot'ı ileride `resolve` çıktısını tüketir; **bu FU'da bağlanmaz** (F-SNAPSHOT). MOD-0165 kodu **değişmez** |
| **MOD-0149 Account / MOD-0150 Contact** | **hard prerequisite (read-only)** | Native niteliklerin kaynağı; **hiçbir alan eklenmez/değiştirilmez**; `Contact.PhotoDataUri` ve `Account.LogoDataUri` çözümleme projeksiyonlarından **dışlanır** (PII/boyut) |
| **MOD-0151 Territory** | read-only, in-service | `AccountCurrentCoverageResolver` **olduğu gibi** çağrılır; imzası genişletilmez. Aktif `TerritoryModel` yoksa coverage **`unknown`** — varsayılan uydurulmaz (MOD-0151-FU05A emsali) |
| **MOD-0164 Consent** | read-only, in-service | `IConsentPreferenceEvaluator` **olduğu gibi** çağrılır; imzası genişletilmez. Provider hiçbir zaman throw etmez, kontrollü `unknown` döner — **`unknown` asla `allowed` değildir** |
| **MOD-0162-FU03 ConceptGraph** | **read-only, in-service** | `concept.affinity`'nin kaynağı (§4.4.1). **Var olan** salt-okunur repository/query yüzeyi tüketilir; `IConceptGraphRepositories` imzası **genişletilmez**, yeni graph aggregate/endpoint **açılmaz**, graph **mutate edilmez** (§6). Graph boşsa/ilişki yoksa → aday **elenir + reason code**, **503 yok** (§8.4) |
| **MDM / MOD-0290** | cross-service, **fail-closed** | Yalnız `GET /api/mdm/products/{id}` ile **kriter değeri doğrulaması** (`concept.affinity` ve `consent.scope-*` değerleri). Üyelik türetimi MDM'den **değil**, in-service graph'tan gelir (D-PRODUCT). MDM'e **yazılmaz** |
| **MOD-0048** (reference data) | D-VOCAB=A | Runtime ön koşulu **değil**; `BusinessUnitId` opak kod olarak doğrulanır (set okunmaz) |
| **MOD-0018** (RBAC) | yalnız tüketim | seed/grant **YOK**; belgelenmiş fallback §14; F-RBAC en sonda |
| **MOD-0288 / HCM** | **yok** | Rep = User. Person/Position master bu FU'da **okunmaz** |
| **DEV-0001** (Golden Reference Compact) | golden reference | **Tek** yüzey, **tek** klasör (§11); Slim dosya seti **kullanılmaz** |

---

## 8. Runtime Constraints

- **Servis:** `Diten.CrmService` (port **5061**), **yeni servis yaratılmaz** — Account/Contact ile aynı serviste,
  çünkü kriter **onların üzerinde** çalışır (kullanıcı kararı; Faz-1 pushdown'ı ancak aynı DB'de mümkündür, §8.5).
- **Gateway:** tüm çağrılar `:5000` üzerinden; browser JS **servis portuna gitmez** (same-origin MVC proxy).
- **Soft delete:** `DELETE` ve `PATCH` **yoktur** — kaldırma = archive (segment **ve** target customer);
  archived kayıt update kabul etmez (**409**).
- **Tenant (D-TENANT):** `EntityBase` tenant-owned; `TenantId` **server-side** claim'den, DTO/payload'da **yer almaz**;
  cross-tenant erişim **404 / boş liste**. **Çözümleme yalnız tenant içi adaylar üzerinde çalışır** — aday sorgusu
  tenant filtresi olmadan **kurulamaz** (test edilir: §17.2).
- **Concurrency:** tek `EntityBase.Version` (segment root; kriter düzenlemeleri **de** bu token'a tabidir).
  `TargetCustomer` kendi token'ını taşır. Uyuşmazlık **409**, sessiz overwrite **yasak**.
- **Atomiklik:** her yazma **tek doküman** yazımıdır → çok-doküman transaction, `SupportsTransactionsAsync` guard'ı
  ve compensation **gerekmez** (`crm-standalone-mongo-transaction-fallback` riski doğmaz). `new-version` *(oku →
  klonla → tek insert)* iki bağımsız yazımdır ve **yarım segment** üretmez.
- **Runtime state YOK:** `MemberIds`, `MemberCount`, `LastResolvedAt`, `CurrentSegmentId` gibi hiçbir alan
  **ne segmentte, ne Contact'ta, ne Account'ta** tutulmaz.
- **Çözümleme hiçbir şey yazmaz:** `resolve` ve `is-member` **saf okuma**dır — kayıt oluşturmaz, güncellemez,
  log tablosu yazmaz (usage log = **FU-D**).

### 8.1 API Contract

```text
GET    /api/crm/segments/contract                              → contract flags + vokabüler + reason codes + limitler
GET    /api/crm/segments/attribute-catalog                     → kapalı nitelik kataloğu (§4.4): sınıf + operatör + parametre beyanı
GET    /api/crm/segments                                       → liste (?segmentType&segmentStatus&subjectType&businessUnitId&includeArchived=true)
POST   /api/crm/segments                                       → create (draft, SegmentVersion=1)
GET    /api/crm/segments/{id}                                  → detay (kriter ağacı dâhil)
PUT    /api/crm/segments/{id}                                  → update (kriter ağacı dâhil; CriteriaFrozenAt dolu ise kriter alanları 409)
POST   /api/crm/segments/{id}/activate                         → draft → active (+ CriteriaFrozenAt damgası)   [SoD: .activate]
POST   /api/crm/segments/{id}/archive                          → draft|active → archived
POST   /api/crm/segments/{id}/new-version                      → active sürümden yeni draft klon (SegmentVersion+1, aynı VersionLineageId)
POST   /api/crm/segments/{id}/resolve                          → { effectiveAt?, limit?, offset?, includeExcluded? } → DETERMİNİSTİK üye kümesi (PERSIST YOK)   [.resolve]
POST   /api/crm/segments/{id}/membership/evaluate              → { subjectType, subjectId, effectiveAt? } → member|not-member|unknown + reason codes   [.resolve]
GET    /api/crm/segments/{id}/targets                          → manuel üyelik satırları (?membershipMode&includeArchived=true)
POST   /api/crm/segments/{id}/targets                          → manuel include/exclude ekle
PUT    /api/crm/segments/{id}/targets/{targetId}               → güncelle (mode geçişi dâhil)
POST   /api/crm/segments/{id}/targets/{targetId}/archive       → arşivle
GET    /api/crm/subjects/{subjectType}/{subjectId}/segments    → TERS soru: bu kişi hangi aktif segmentlerde? (M tavanı — §8.5)   [.resolve]
```

Tümü `Response<T>` envelope + `CustomBaseController` (`response-envelope.md`). `TenantId` **hiçbir payload'da yok**.

### 8.2 Contract flags

```text
# açık (bu FU)
supportsSegmentDefinition                 : true
supportsStaticSegments                    : true
supportsDynamicSegments                   : true
supportsHybridSegments                    : true
supportsCriteriaTree                      : true
supportsRealTimeMembershipResolution      : true
supportsManualTargetCustomer              : true
supportsSegmentVersioning                 : true
supportsEffectiveDating                   : true
supportsAttributeCatalog                  : true
supportsMembershipReasonCodes             : true
supportsCrossServiceAttributeValidation   : true
supportsProductAffinityAttributes         : true    # D-PRODUCT çözüldü — concept.affinity (§4.4.1)
supportsConceptGraphDerivedAttributes     : true    # MOD-0162-FU03 salt-okunur tüketimi

# KAPALI (motor yok — sessiz varsayım yasağı)
supportsMaterializedMembership            : false   # FU-B
supportsMembershipRefreshJob              : false   # FU-B
supportsMembershipHistory                 : false   # FU-B
supportsSegmentOfSegment                  : false   # FU-B
supportsIcpScoring                        : false   # FU-D
supportsComputedAttributes                : false   # FU-D
supportsSegmentUsageLog                   : false   # FU-D
supportsStrategyTemplate                  : false   # FU-C
supportsSubjectList                       : false   # FU-C
supportsUcln                              : false   # FU-C
supportsCampaignTargetGeneration          : false   # MOD-0165
supportsFrequencyPolicyWrite              : false   # MOD-0165
supportsConceptGraphAuthoring             : false   # MOD-0162-FU03 — bu FU graph YAZMAZ, yalnız okur
supportsConceptGraphTraversalEngine       : false   # geçişli kapanış / en-iyi-yol / skorlama YOK (bounded ≤2)
```

### 8.3 Çözümleme semantiği — **determinizm sözleşmesi** (bu FU'nun ana AC'si)

Verilen `(TenantId, SegmentId, SegmentVersion, effectiveAt)` ve **değişmemiş** kaynak veri için `resolve`:

1. **Aynı üye kümesini** döner (küme eşitliği),
2. **Aynı sırada** döner — sıralama `(SubjectId ASC)` üzerinden **tek ve tam** anahtarla yapılır
   (`DateTimeOffset` alanları üzerinden sıralama **yasak** — `mongo-datetimeoffset-parallel-arrays-sort`),
3. **Her satır için aynı reason code** kümesini döner,
4. **Elenen adayları da** `includeExcluded=true` ile **gerekçesiyle** döner — **sessiz eleme yasaktır**.

**Üyelik formülü:**

```text
SegmentType = static   →  members = manual-include                          (kriter YOK, çalıştırılmaz)
SegmentType = dynamic  →  members = criteriaMatched                         (manuel satır KABUL EDİLMEZ — bkz. not)
SegmentType = hybrid   →  members = (criteriaMatched ∪ manual-include) \ manual-exclude
```

> **Not (kasıtlı katılık):** `dynamic` bir segmentte manuel satır **kabul edilmez** (POST → **400**
> `segment_type_forbids_manual_membership`). Manuel istisna isteyen `hybrid`'e geçmelidir. Aksi hâlde "dinamik"
> etiketi yalan söyler ve hangi üyenin nereden geldiği ancak satır satır okunarak anlaşılır.

**Effective window:** `effectiveAt` verilmezse `UtcNow`. Segment o anda `active` **ve** effective window içinde
değilse → **hiçbir üye dönmez**, `segment_not_active` / `outside_effective_window` reason code'u ile
**boş ve gerekçeli** cevap döner (200 + boş küme; 404 **değil** — segment vardır, sadece o anda geçerli değildir).

**Sürüm çözümlemesi (D-VER):** `resolve` **her zaman** çağrılan `{id}`'nin **kendi** `SegmentVersion`'ıyla çalışır;
sessizce "en son sürüme" kaymaz. `SupersededBySegmentId` dolu bir sürüm çözümlenebilir (geçmişi açıklamak için) ve
cevap `superseded: true` bayrağını **görünür** taşır.

### 8.4 Fail-closed matrisi (D6) — **tek kural: in-service gerekçelenir, cross-process 503'tür**

| Durum | Sınıf | Davranış |
|---|---|---|
| MOD-0164 consent provider kontrollü `unknown` döndürdü | D | Aday **elenir**, `consent_unknown` reason code'u ile **görünür**. Çözümleme **tamamlanır**. `unknown` **asla** `allowed` sayılmaz |
| MOD-0164 `blocked` | D | Aday elenir, `consent_blocked` |
| MOD-0151 aktif `TerritoryModel` yok / coverage çözümlenemedi | D | Territory kriteri olan aday **elenir**, `territory_coverage_unavailable`. Varsayılan coverage **uydurulmaz** |
| **ConceptGraph'ta ürüne ait düğüm yok** (`global-product` + `ExternalRefId=P` bulunamadı) | **D** | Uzmanlık kümesi **boş** → **hiçbir aday eşleşmez**; her aday `concept_product_node_missing` ile **elenir**. Çözümleme **200 + boş küme** ile **tamamlanır** — **503 YOK**, "hepsi üye" varsayımı **YOK** |
| **Ürün düğümü var ama uzmanlık düğümüne ulaşılamıyor** (kenar yok / tip dışı / archived / effective değil / derinlik tavanında) | **D** | Aday **elenir**, `concept_affinity_no_specialty_reached`. Türetme **genişletilmez**, derinlik **aşılmaz** |
| **Adayın `contact.specialty` değeri boş** | **D** | Aday **elenir**, `concept_subject_specialty_missing`. Boş uzmanlık **hiçbir zaman** "eşleşti" sayılmaz |
| **Uzmanlık kümesi dolu, adayın uzmanlığı kümede değil** | **D** | Aday **elenir**, `concept_affinity_not_matched` (normal negatif sonuç) |
| Native nitelik değeri kayıtta yok (`null`) | N | Operatör semantiği uygulanır (`is-null` eşleşir, `eq` eşleşmez). `attribute_not_resolvable` **yalnız** kaynak okunamadığında |
| **MDM ürün/marka referans doğrulaması: 404** | X | **400** — kriter authorable değil (`segment_criteria_reference_not_found`). Kayıt **oluşmaz** |
| **MDM ulaşılamıyor / timeout / 5xx / auth reddi / gövde bozuk** | X | **503** `segment_dependency_unavailable` — **kısmi sonuç YOK, persist YOK** |
| Aday kümesi tavanı aşıldı | — | **422** `segment_candidate_set_too_large` + aşılan tavan cevapta **görünür**. **Sessiz kırpma yasak** |
| Tenant context yok | — | **400** (`ITenantContext` çözülemedi) — varsayılan tenant **kullanılmaz** |

**Cross-service çağrı profili** (`WorkingCalendarLegalEntityValidator` deseni birebir):
**cache YOK** · toplam timeout **3 sn** · **1** transient retry (502/503/504, 75 ms) · `Authorization` + `X-Tenant-Id`
+ `X-Correlation-Id` **forward** · Gateway (`:5000`) üzerinden, servis portuna **doğrudan gidilmez** ·
**doğrulama başarısızsa hiçbir şey persist edilmez** (`CreateAsync`/`ReplaceAsync` **öncesinde** çağrılır).

### 8.5 Ölçek stratejisi (D4) — N contact × M segment sorunu

**Sorunun dürüst hâli:** *"Tüm segmentleri tüm kişiler için canlı hesapla"* N×M'dir ve tasarım gereği
**bu FU'da hiçbir yerde yapılmaz**. Sistem yalnız üç soruya cevap verir ve üçü de sınırlıdır:

| Soru | Şekli | Maliyet | Tavan |
|---|---|---|---|
| `resolve` — "**bu bir** segmentte kimler var?" | 1 segment × N aday | **Faz 1** tek Mongo sorgusu (index'li, pushdown) → **Faz 2** yalnız Faz-1 çıktısı üzerinde | `MaxCandidateSet = 10.000` |
| `is-member` — "**bu bir** kişi **bu bir** segmentte mi?" | 1 × 1 | Tek doküman + gerekirse tek türev okuma | — |
| `subjects/{id}/segments` — "bu kişi hangi segmentlerde?" | 1 kişi × M segment | Her segment için **tek-aday** değerlendirme (N yok) | `MaxSegmentsPerSubject = 200` |

**İki fazlı bounded evaluation:**

```text
Faz 1   (pushdown)   : N sınıfı nitelikler  →  TEK Mongo filtresi (TenantId + index'li alanlar) → aday kümesi
                       Tavan aşılırsa 422 (yazar kuralı daraltır); sessiz kırpma YOK.
Faz 1.5 (join)       : J sınıfı  →  AccountContactLink üzerinden aday daraltma (TEK toplu sorgu)
Faz 2   (post-filter): D sınıfı  →  YALNIZ aday kümesi üzerinde, KAYNAK BAŞINA TEK TOPLU çağrı
                       (aday başına çağrı YASAK — N+1 yasağı, testle sabitlenir)
                       · consent   → tek toplu evaluator okuması
                       · territory → tek toplu coverage okuması
                       · concept.affinity → çözümleme başına TEK bulk graph read; uzmanlık kümesi S bir kez
                         türetilir ve TÜM adaylara aynı küme uygulanır (aday başına graph okuması YASAK).
                         S'nin türetilmesi ADAY SAYISINDAN BAĞIMSIZDIR → maliyet graph boyutuyla sınırlıdır.
Faz 3   (manuel)     : hybrid için  ∪ manual-include  \ manual-exclude
Faz 4   (sayfalama)  : deterministik sıralama (SubjectId ASC) + limit/offset
```

**Performans bütçesi (AC'ye bağlanır, §16):** 10.000 adaylık bir dinamik segment `resolve`'u tek istekte
**≤ 5 sn**; `is-member` **≤ 300 ms**; her ikisi de **cache'siz**. Bütçe aşılırsa cevap **kesilmez** — istek
**time-out** olur ve **503** döner; yarım küme **asla** dönmez.
`concept.affinity` bu bütçeye **dâhildir** ve `MaxCandidateSet` tavanını **değiştirmez**: graph okuması aday
kümesine değil, tenant'ın kavram grafiği boyutuna bağlıdır ve çözümleme başına **bir kez** yapılır.

**Kasıtlı olarak YAPILMAYANLAR (FU-B):** materialized üyelik · üyelik cache'i · arka plan yeniden hesaplama ·
Account/Contact değişikliğine tetiklenen delta · üyelik geçmişi · segment-içinde-segment. FU-B'nin tasarımı
**FU-A'nın ölçüm çıktısına** dayanmalıdır (§20/F-SCALE).

### 8.6 Tüketim seam'i — `ISegmentMembershipReader` (read-only)

```csharp
public interface ISegmentMembershipReader
{
    // MOD-0167-FU01 §5 sorusu — "üye mi?"
    Task<SegmentMembershipVerdict> IsMemberAsync(
        Guid segmentId, string subjectType, Guid subjectId, DateTimeOffset effectiveAt, CancellationToken ct);

    // MOD-0165 snapshot tüketicisi için — bounded, deterministik, PERSIST YOK
    Task<SegmentResolutionResult> ResolveAsync(
        Guid segmentId, DateTimeOffset effectiveAt, int limit, int offset, CancellationToken ct);
}
```

- **Motor değildir, rapor eder:** yazmaz, `CampaignTarget` üretmez, `VisitFrequencyPolicy` yazmaz.
- **`unknown` bir cevaptır, hata değildir** — ve **asla `member` değildir** (MOD-0167-FU01 §5 ile birebir).
- Tüketiciler ham segment/collection okuma yetkisine **ihtiyaç duymaz**; seam üzerinden gider.

---

## 9. Layout & Shell Contract

- `shell: tenant` → **tüm** `frontend/Diten.Web/Views/CRM/Segments/*.cshtml` dosyalarında
  `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır (`_ViewStart.cshtml` varsayılanına güvenilmez).
- View klasörü: `frontend/Diten.Web/Views/CRM/Segments/`
- Frontend route: `/CRM/Segments` · Create `/CRM/Segments/Create` · Edit `/CRM/Segments/Edit/{id}` ·
  Details `/CRM/Segments/Details/{id}`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` **FROZEN** — kullanılmaz.
- Nav: `_LayoutTenantShell.cshtml` içine **tek** permission-guard'lı `<li>`
  (`@if (Perms.Has("crm.segment.read"))` — dev fallback §14).
- Partial path'leri **absolute**: `~/Views/CRM/Segments/_Filter.cshtml` vb.
- Bölüm sırası (Index): ① `_Filter` → ② `_BulkActionBar` (shared VM) → ③ `_DataTable`.

---

## 10. Backend File Convention

Golden Reference **Compact** (DEV-0001) birebir; handler/validator adlarında **Command/Query suffix YOK**.

```text
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Segmentation/
├── Commands/
│   ├── CreateSegmentCommand.cs                 (sealed record, IRequest<Response<Guid>>)
│   ├── UpdateSegmentCommand.cs                 (sealed record, IRequest<Response<NoContent>>)
│   ├── ActivateSegmentCommand.cs
│   ├── ArchiveSegmentCommand.cs
│   ├── CreateSegmentVersionCommand.cs
│   ├── AddTargetCustomerCommand.cs
│   ├── UpdateTargetCustomerCommand.cs
│   └── ArchiveTargetCustomerCommand.cs
├── Queries/
│   ├── ListSegmentsQuery.cs
│   ├── GetSegmentByIdQuery.cs
│   ├── GetSegmentContractQuery.cs
│   ├── GetSegmentAttributeCatalogQuery.cs
│   ├── ResolveSegmentMembershipQuery.cs
│   ├── EvaluateSegmentMembershipQuery.cs
│   ├── ListTargetCustomersQuery.cs
│   └── ListSubjectSegmentsQuery.cs
├── Handlers/
│   ├── CommandHandlers/                        ← AYRI klasör (zorunlu)
│   │   ├── CreateSegmentHandler.cs             (sealed class, suffix YOK)
│   │   ├── UpdateSegmentHandler.cs
│   │   ├── ActivateSegmentHandler.cs
│   │   ├── ArchiveSegmentHandler.cs
│   │   ├── CreateSegmentVersionHandler.cs
│   │   ├── AddTargetCustomerHandler.cs
│   │   ├── UpdateTargetCustomerHandler.cs
│   │   └── ArchiveTargetCustomerHandler.cs
│   └── QueryHandlers/                          ← AYRI klasör (zorunlu)
│       ├── ListSegmentsHandler.cs
│       ├── GetSegmentByIdHandler.cs
│       ├── GetSegmentContractHandler.cs
│       ├── GetSegmentAttributeCatalogHandler.cs
│       ├── ResolveSegmentMembershipHandler.cs
│       ├── EvaluateSegmentMembershipHandler.cs
│       ├── ListTargetCustomersHandler.cs
│       └── ListSubjectSegmentsHandler.cs
├── Validators/
│   ├── CreateSegmentValidator.cs               (Command suffix YOK)
│   ├── UpdateSegmentValidator.cs
│   ├── AddTargetCustomerValidator.cs
│   └── UpdateTargetCustomerValidator.cs
├── Resolution/
│   ├── SegmentMembershipResolver.cs            (iki fazlı motor — §8.5)
│   ├── SegmentCriteriaEvaluator.cs             (saf fonksiyon: ağaç × nitelik değerleri → verdict + reason codes)
│   ├── ISegmentAttributeSourceReader.cs        (+ Account/Contact/Link/Territory/Consent adaptörleri)
│   ├── ConceptAffinitySourceReader.cs          (§4.4.1 — MOD-0162-FU03'ü SALT-OKUNUR tüketir; ürün düğümü → bounded
│   │                                            addresses/belongs-to → reference-data-value → specialty kümesi S.
│   │                                            MOD-0162 tarafında SIFIR dosya değişikliği; imza genişletilmez)
│   └── ISegmentMembershipReader.cs             (tüketim seam'i — §8.6)
├── Catalog/
│   ├── SegmentAttributeCatalog.cs              (kapalı katalog — §4.4)
│   └── ISegmentProductReferenceValidator.cs    (cross-service sözleşme; impl Infrastructure'da)
├── Contract/
│   └── SegmentContract.cs
├── SegmentPermissions.cs
└── SegmentModels.cs                            ← TEK dosyada tüm DTO/ViewModel'ler
```

**Yasaklar:** tek dosyada birden fazla `public class`/`record` (`SegmentModels.cs` hariç) ·
`*CommandHandler.cs` / `*QueryHandler.cs` suffix'i · `CommandHandlers`/`QueryHandlers` ayrımını yapmamak ·
`Requests/Commands/` gibi ekstra alt klasör.

> **Not (mevcut kod ile fark):** repodaki bazı CRM feature'ları (`Campaign`, `ConsentPreference`) komutları tek
> dosyada topluyor. Bu FU **standarda uyar** (`module-pack-standard.md` §4), mevcut sapmayı **çoğaltmaz** ve
> **mevcut dosyaları da düzeltmez** (§6 protected).

---

## 11. Frontend File Contract

### 11.1 Golden reference kararı — kullanıcı-form alanı türetmesi (GÖSTERİLİR)

Sayılan: create/edit formunda kullanıcının doldurduğu **Segment** alanları.
Sayılmayan: `Id`, `TenantId`, `IsDeleted`, `CreatedAt`, `UpdatedAt`, `DeletedAt`, `Version`, audit alanları,
server-side damgalar (`CriteriaFrozenAt`, `ActivatedAt`, `ArchivedAt`, `SupersededBySegmentId`,
`VersionLineageId`, `SegmentVersion`), DataTable checkbox/action kolonları.

| # | Alan | # | Alan |
|---|---|---|---|
| 1 | `SegmentCode` | 7 | `EffectiveFrom` |
| 2 | `SegmentName` | 8 | `EffectiveTo` |
| 3 | `SegmentType` | 9 | `MatchMode` |
| 4 | `SubjectType` | 10 | `Description` |
| 5 | `SegmentStatus` | 11 | `Notes` |
| 6 | `BusinessUnitId` | 12 | `Criteria` (gömülü ağaç editörü — **tek** form alanı sayılır) |

**12 > 8 → `golden_reference: compact`.** Gömülü kriter ağacı editörü ve gömülü üye alt-editörü **ayrı yüzey
değildir**; `_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` **YASAKTIR** (Compact kuralı).

### 11.2 Dosya seti — TEK klasör, kanonik Compact 9 dosya

```text
frontend/Diten.Web/Views/CRM/Segments/
├── Index.cshtml                     (Layout AÇIKÇA; ① _Filter ② _BulkActionBar ③ _DataTable)
├── Create.cshtml                    (sayfa kabuğu + _Form)
├── Edit.cshtml                      (sayfa kabuğu + _Form)
├── Details.cshtml                   (salt-okunur: kriter ağacı + üye listesi + resolve önizlemesi)
├── _Form.cshtml                     (segment formu + GÖMÜLÜ kriter ağacı repeater + GÖMÜLÜ üye repeater)
├── _Filter.cshtml                   (inline collapsible; dt-inline-filter-host sınıfı ile)
├── _DataTable.cshtml                (data-dt-standard="v2" + skeleton; TEK DataTable)
├── _IndexL10n.cshtml                (JSON payload bridge)
└── SegmentsIndex.cs                 (marker class)

frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/
├── index.js                         (DataTable + filtre + bulk action)
├── index.l10n.js                    (camelCase→PascalCase köprüsü ZORUNLU — l10n-bridge-pascalcase-loader)
└── form.js                          (kriter ağacı repeater + katalog-güdümlü operatör/değer alanları + üye repeater + resolve önizleme)
```

**Katalog-güdümlü form (kritik UI kuralı):** `_Form.cshtml`/`form.js` operatör ve değer alanlarını
**`attribute-catalog` cevabından** üretir. Nitelik listesi, operatör listesi ve zorunlu `Parameters`
(ör. consent için `channel`/`purpose`, `concept.affinity` için opsiyonel `maxDepth`/`subjectId`)
**JS'te hardcode edilmez** — hardcoded fallback liste **kabul edilmez** (`platform-lookups-reference-data.md`).

**Ürün seçicisi (`concept.affinity` değeri):** `AttributeCode="concept.affinity"` seçildiğinde değer alanı
**mevcut** `/api/global-products/selector` proxy'sinden beslenen bir Select2 olur (MOD-0162-FU03'ün concept node
ExternalRef seçicisiyle **aynı** yüzey; `mdm.global-products.read` gerektirir). **Yeni proxy/endpoint açılmaz**
(§20/F-CONCEPT-UX). Yetki yoksa alan serbest-metin GUID'e düşmez — **devre dışı** kalır ve gerekçe gösterilir.

**Verifier:** `python3 .antigravity/scripts/verify_datatable_page.py . --area CRM --module Segments --reference compact`
→ mevcut CRM Compact baseline'ı ile **karşılaştırmalı** raporlanır; yeni FAIL **açıklanır veya kapatılır**
(§17.1). Baseline'ın kendisi 0 FAIL olmayabilir; **kendi çalıştırmanla doğrula, rapor edilen sayıya güvenme**.

---

## 12. Validation Rules

### 12.1 `Segment`

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| `SegmentCode` | Evet | trim, max 64, `^[a-z0-9][a-z0-9-]*$` | `(TenantId, SegmentCode)` unique partial | `ExistsByCodeAsync` → 409 |
| `SegmentName` | Evet | trim, max 200 | — | — |
| `SegmentType` | Evet | `SegmentTypes` üyesi | — | set dışı → 400 |
| `SubjectType` | Evet | `SegmentSubjectTypes` üyesi; **update'te değişemez** | — | değişim denemesi → 409 |
| `SegmentStatus` | Evet | `SegmentStatuses` üyesi; geçiş yalnız `draft→active`, `draft→archived`, `active→archived` | — | geçersiz geçiş → 409 |
| `EffectiveFrom` | Evet | geçerli tarih | — | — |
| `EffectiveTo` | Hayır | `> EffectiveFrom` | — | ihlal → 400 |
| `MatchMode` | Evet | `SegmentMatchModes` üyesi | — | — |
| `BusinessUnitId` | Hayır | boş-olmayan string, max 64 (**set okunmaz**) | — | — |
| `Criteria` | koşullu | `static` → **boş olmalı** (dolu ise 400) · `dynamic`/`hybrid` → **≥1 predicate** (boş ise 400) | — | — |
| `Criteria` (ağaç) | — | derinlik ≤5 · düğüm ≤100 · grup çocuk ≤20 · döngü yok · her `group` **en az 1** çocuk · `SortOrder` parent içinde unique | — | ihlal → 400 |
| `Criteria` (dondurma) | — | `CriteriaFrozenAt` dolu iken kriter alanı değişimi | — | → **409** `segment_criteria_frozen` |
| `Description` / `Notes` | Hayır | max 2000 | — | — |

### 12.2 `SegmentCriteriaNode` (predicate)

| Field | Required | Rule |
|---|---|---|
| `AttributeCode` | Evet | **Katalogda olmalı** (§4.4); yoksa 400 `segment_attribute_unknown` |
| `Operator` | Evet | Katalogun **o nitelik için** izin verdiği operatör; değilse 400 `segment_operator_not_supported` |
| `ValueType` | Evet | Katalogun beyan ettiği tiple **eşleşmeli**; değilse 400 |
| `Values` | koşullu | Arity (§4.2); `between` → 2 ve `[0] < [1]`; `in/not-in` → 1..50 · `date` değerleri ISO-8601 · `guid` değerleri parse edilebilir |
| `Parameters` | koşullu | Katalog **zorunlu** dediyse dolu olmalı (`account.attribute` → `attributeCode`; `consent.eligibility` → `channel` + `purpose`); eksikse 400 `segment_attribute_parameter_missing` |
| `Values` (X sınıfı) | koşullu | `consent.scope-product` / `-brand` **ve** `concept.affinity` değerleri MDM'de **fail-closed doğrulanır**: 404 → 400, ulaşılamıyor → **503** (§8.4) |
| `concept.affinity` — `Parameters.maxDepth` | Hayır | Verilirse `1` veya `2`; `>2` veya sayı değil → **400** `segment_concept_depth_exceeded`. Varsayılan **1** |
| `concept.affinity` — `Parameters.subjectId` | Hayır | Verilirse geçerli Guid; verilmezse tenant'taki tüm konular taranır |
| `concept.affinity` — segment uyumu | — | Yalnız `SubjectType=contact` segmentinde kullanılabilir; `account` segmentinde → **400** `segment_attribute_not_applicable_for_subject_type` |
| `GroupOperator` | koşullu | `NodeKind=group` iken zorunlu ve `SegmentGroupOperators` üyesi; `not` grubunun **tam 1** çocuğu olmalı |

### 12.3 `TargetCustomer`

| Field | Required | Rule |
|---|---|---|
| `SegmentId` | Evet | Var olan, arşivlenmemiş segment; `dynamic` segment → **400** `segment_type_forbids_manual_membership` |
| `SubjectType` | Evet | **Segmentin `SubjectType`'ı ile eşleşmeli**; değilse 400 `subject_type_mismatch` |
| `SubjectId` | Evet | `Guid.Empty` yasak. Master **okunmaz** (çağıran id'yi verir) |
| `MembershipMode` | Evet | `manual-include` \| `manual-exclude` |
| `SelectionReason` | Evet | trim, 1..1000 — **boş gerekçe authorable değil** |
| `ReasonCodes` | Evet | ≥1 ve hepsi `SegmentReasonCodes` üyesi |
| `EffectiveFrom` / `EffectiveTo` | Evet / Hayır | `EffectiveTo > EffectiveFrom` |
| benzersizlik | — | `(TenantId, SegmentId, SubjectType, SubjectId)` arşivlenmemişlerde unique → ikinci ekleme **409** |

---

## 13. Failure Path to Verify

- **Duplicate `SegmentCode`** → **409** + UI alan-düzeyi hata + kayıt **oluşmaz** + reload sonrası temiz state.
- **Duplicate `TargetCustomer`** (aynı segment + aynı subject, arşivlenmemiş) → **409**; mevcut satır **değişmez**.
- **Missing `SegmentName` / `SelectionReason`** → **400** + validator mesajı + kaydetme engellenir.
- **Concurrency conflict** (iki sekme aynı segmenti düzenledi) → **409** + UI *"veri değişti, yeniden yükleyin"* +
  **sessiz overwrite YOK** (kriter ağacı düzenlemeleri de aynı token'a tabidir).
- **Unauthorized actor** → **403** + UI aksiyonu disabled/permission-denied state (`.resolve` yetkisi olmayan
  kullanıcı üye kimliklerini **hiç görmez**).
- **Cross-tenant erişim** → **404** (segment) / **boş liste** (list) — varlık sızıntısı yok.
- **Dondurulmuş kriteri düzenleme** (`active` segment) → **409** `segment_criteria_frozen` + UI kullanıcıyı
  `new-version` akışına yönlendirir.
- **`dynamic` segmente manuel üye ekleme** → **400** `segment_type_forbids_manual_membership`.
- **Katalogda olmayan `AttributeCode`** → **400** `segment_attribute_unknown` (kayıt oluşmaz).
- **MDM ulaşılamıyor** (kriterde ürün/marka scope'u var) → **503** `segment_dependency_unavailable`,
  **hiçbir kayıt persist edilmez**, **kısmi üye kümesi dönmez**.
- **MOD-0164 `unknown`** → çözümleme **tamamlanır**; aday `consent_unknown` ile **elenir**; `allowed` **sayılmaz**.
- **MOD-0151 aktif model yok** → territory kriteri olan aday `territory_coverage_unavailable` ile elenir;
  varsayılan coverage **uydurulmaz**.
- **ConceptGraph'ta ürün düğümü yok** → **200 + boş küme**; her aday `concept_product_node_missing` ile elenir;
  **503 yok**, "hepsi üye" **yok** (sınıf D — §8.4).
- **`concept.affinity` derinlik 3 istendi** → **400** `segment_concept_depth_exceeded` (kayıt oluşmaz).
- **`concept.affinity` bir `SubjectType=account` segmentinde** → **400**
  `segment_attribute_not_applicable_for_subject_type`.
- **Aday kümesi tavanı aşıldı** → **422** `segment_candidate_set_too_large` + tavan cevapta görünür;
  **sessiz kırpma yok**.
- **Arşivlenmiş segmenti güncelleme** → **409**.
- **Geçersiz lifecycle geçişi** (`archived → active`) → **409**.

---

## 14. Authorization Convention

```text
Policy:     [Authorize]                                   // shell: tenant
Permission: [HasPermission("crm.segment.{action}")]       // PKS-001 lowercase-dotted, ≥3 segment, kebab-case
Actor type: tenant_user  (platform SuperAdmin tüm permission'lardan geçer)
```

| Anahtar | Kapsam |
|---|---|
| `crm.segment.read` | Segment listesi/detayı + kriter ağacı + contract + attribute-catalog |
| `crm.segment.manage` | create / update / archive / new-version |
| `crm.segment.activate` | `activate` — **SoD**: kuralı yazan ile canlıya alan ayrılabilsin (`crm.campaign.publish` emsali) |
| `crm.segment.resolve` | `resolve` / `membership/evaluate` / `subjects/{id}/segments` — **ayrı tutulur çünkü çözümleme kişi kimliği (PII) döndürür**; segment tanımını okumak üyeleri görmeye yetmez |
| `crm.segment.target.read` | manuel üyelik satırlarını okuma |
| `crm.segment.target.manage` | manuel üyelik ekleme / güncelleme / arşivleme |

**Bu pack seed/grant YAPMAZ.** `SegmentPermissions.cs` **yalnız tanım** dosyasıdır (DB yazımı yok, rol şablonu yok).
RBAC kataloğu `crm.segment.*` taşımadığı için endpoint'ler MOD-0165-FU04 / MOD-0164-FU02 ile **aynı belgelenmiş
fallback** üzerinde çalışır: okumalar `crm.territory.read`, yazmalar `crm.territory.model.manage`.
**Fallback hiçbir guard'ı genişletmez** — tenant izolasyonu, lifecycle ve doğrulama guard'ları aynen çalışır.
Kanonik anahtarların kataloğa alınması ve role atanması → **§20/F-RBAC** (en sona bırakıldı; `manual-grant`
marker'lı ayrı operatör işi).

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKLİ.** (Bu, MOD-0162-FU05'ten farklıdır — orada `/api/crm/knowledge/{everything}`
wildcard'ı zaten vardı.)

`gateway/Diten.ApiGateway/ocelot.json` bugün `/api/crm/` altında **yalnız** şu ailelere sahiptir:
`accounts` · `contacts` · `territory-management` · `territory-models` · `resources` ·
`visit-frequency-policies` · `consents` · `preferences` · `campaigns` · `knowledge`.
**`segments` ve `subjects` rotaları YOKTUR** → yeni route olmadan endpoint'ler Gateway'de **404 + boş `{}` gövde**
döner (`gateway-404-empty-body-signature`).

Gerekli çiftler (`campaigns` bloğu birebir şablon; `OPTIONS` **dâhil**):

```text
/api/crm/segments                 ↔ 5061   (GET, POST, OPTIONS)
/api/crm/segments/{everything}    ↔ 5061   (GET, POST, PUT, OPTIONS)
/api/crm/subjects/{everything}    ↔ 5061   (GET, OPTIONS)
```

- `ocelot.json` **protected path**'tir; **bu pack yazmaz** (§6). Ayrı bir `integration-agent` task'ı olarak
  yürütülür → **§20/F-GATEWAY**.
- Frontend (5001) **doğrudan 5061'e gitmez**; `frontend/Diten.Web/Controllers/CRM/SegmentsController.cs`
  same-origin proxy'dir. Proxy'nin `ForwardAsync`'i **204/205/304/1xx için gövdesiz** dönmelidir
  (`proxy-forward-204-content-length-crash`) — bu FU'da `archive`/`activate` 204 dönebilir.
- **Kabul kapısı:** route eklenmeden authenticated smoke (§17.3) çalıştırılmaz; 404 + `{}` görülürse **kod hatası
  değil, eksik route** olarak teşhis edilir (anon 403'ler routing'den önceki middleware'den geldiği için probe
  `OPTIONS` ile yapılır).

---

## 16. Acceptance Criteria

**Kimlik & kapsam**
- [ ] `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0167-FU02 --name "Segment Foundation - Definition, Criteria, Membership Resolution and Target Customer" --parent MOD-0167` **exit 0**.
- [ ] Repoda `StrategyTemplate`, `SubjectList`, `UCLN`, `IcpScore`, `SegmentUsageLog`, `MaterializedMembership`
      adında **hiçbir** tip/dosya/endpoint **yoktur** (grep ile kanıtlanır).
- [ ] `VisitFrequencyPolicy*`, `Campaign*`, `ConsentRecord`, `PreferenceRecord`, `Territory*`, `Account`, `Contact`
      dosyalarında **tek satır değişiklik yoktur** (`git diff --stat` ile kanıtlanır).

**Segment tanımı & lifecycle**
- [ ] `POST /api/crm/segments` `draft` + `SegmentVersion=1` + `VersionLineageId == Id` üretir.
- [ ] Aynı tenant'ta aynı `SegmentCode` ile ikinci create → **409**; farklı tenant'ta aynı kod → **başarılı**.
- [ ] `SubjectType` update ile değiştirilemez → **409**.
- [ ] `activate` sonrası `CriteriaFrozenAt` doludur ve kriter alanı update'i → **409** `segment_criteria_frozen`.
- [ ] `new-version` yeni bir `draft` üretir; `SegmentVersion` +1; `VersionLineageId` **aynı**; **yeni `NodeId`'ler**
      üretilir ve `ParentNodeId` referansları **yeni id'lere remap edilir** (eski ağaca sızıntı yok).
- [ ] `new-version` **activate** edildiğinde önceki sürümün `SupersededBySegmentId`'si dolar; **eski sürüm
      çözümlenebilir kalır** ve cevabı `superseded: true` taşır.
- [ ] `archived → active` geçişi → **409**; hiçbir endpoint hard delete yapmaz (`DELETE` route'u **yoktur**).
- [ ] Cross-tenant `GET /api/crm/segments/{id}` → **404**; cross-tenant list → **boş**.

**Kriter modeli**
- [ ] `SegmentType=static` + dolu `Criteria` → **400**; `SegmentType=dynamic` + boş `Criteria` → **400**.
- [ ] Katalogda olmayan `AttributeCode` → **400** `segment_attribute_unknown`.
- [ ] Katalogun izin vermediği operatör (ör. `contact.is-primary` + `between`) → **400** `segment_operator_not_supported`.
- [ ] `consent.eligibility` predicate'i `Parameters.channel` + `purpose` olmadan → **400** `segment_attribute_parameter_missing`.
- [ ] Derinlik 6 / 101 düğüm / 21 çocuk / 51 `in` değeri → **400** (dördü de ayrı test).
- [ ] `ParentNodeId` döngüsü → **400**; `not` grubu 2 çocukla → **400**.
- [ ] `GET /api/crm/segments/attribute-catalog` §4.4 tablosunun **tamamını** döner ve her nitelik için
      `class` (N/J/D/X) + izinli operatörler + zorunlu parametreler **beyan edilir**.

**Dinamik üyelik determinizmi (bu FU'nun ANA kabul kriteri)**
- [ ] Aynı `(segmentId, SegmentVersion, effectiveAt)` ile **arka arkaya 3 `resolve`** → **bit-bit aynı** üye listesi,
      **aynı sıra**, **aynı reason code**'lar (script otomatik karşılaştırır).
- [ ] Sıralama **yalnız** `SubjectId ASC` üzerindedir; hiçbir `DateTimeOffset` alanı sort anahtarı **değildir**.
- [ ] `includeExcluded=true` ile **elenen her aday** cevapta **gerekçesiyle** görünür; hiçbir aday **sessizce
      düşmez** (elenen sayısı + kabul edilen sayısı = aday sayısı, testle sabitlenir).
- [ ] `hybrid`: `manual-exclude`, kriterle eşleşen bir üyeyi **kesin olarak** dışarı alır (`manual_exclude` reason).
- [ ] `hybrid`: `manual-include`, kriterle eşleşmeyen bir subject'i **içeri alır** (`manual_include` reason).
- [ ] `dynamic` segmente manuel üye ekleme → **400**.
- [ ] `static` segment `resolve`'u kriteri **hiç çalıştırmaz** (motor çağrısı 0 — testte sahte reader ile kanıtlanır).
- [ ] `effectiveAt` segment window'unun dışında → **200 + boş küme + `outside_effective_window`** (404 **değil**).
- [ ] `resolve` çağrısından sonra `segments`, `target_customers` ve **hiçbir** collection'da **yeni/değişmiş
      doküman yoktur** (öncesi/sonrası sayım + `UpdatedAt` karşılaştırması).

**`concept.affinity` — ConceptGraph-türetilmiş ürün-affinity (D-PRODUCT)**
- [ ] **ExternalRef eşleşmesi:** ürün düğümü **yalnız** `ExternalRefType="global-product"` **ve**
      `ExternalRefId == P` olan düğümlerden bulunur; uzmanlık kümesi **yalnız**
      `ExternalRefType="reference-data-value"` düğümlerinin `ExternalRefId` değerlerinden kurulur. Başka bir
      `ExternalRefType` (`document` / `audience-profile` / `other`) **hiçbir zaman** uzmanlık kümesine girmez.
- [ ] **Ürün düğümü yok → boş küme:** graph'ta `global-product`/`P` düğümü bulunmadığında `resolve`
      **200 + boş üye kümesi** döner, her aday `concept_product_node_missing` ile **elenir**;
      **503 dönmez** ve hiçbir aday varsayılan olarak üye **sayılmaz**.
- [ ] **Bounded depth:** varsayılan derinlik **1**; `Parameters.maxDepth=2` ikinci katmanı getirir;
      `maxDepth=3` → **400** `segment_concept_depth_exceeded`. Üçüncü katman hiçbir yolda gezilmez
      (geçişli kapanış yok — 3 katmanlı zincir kurulan fixture ile kanıtlanır).
- [ ] **Yalnız izinli kenar tipleri:** `addresses` ve `belongs-to` izlenir; aynı düğümden çıkan
      `leads-to` / `requires` / `evidences` / `custom` kenarları **izlenmez** (karışık kenar fixture'ı ile).
- [ ] **Yön kuralı:** yalnız çıkan kenar; `Direction=bidirectional` beyanı izlenir, ters kenar **türetilmez**.
- [ ] **Lifecycle:** `archived` / `inactive` / `effectiveAt` dışı düğüm ve kenarlar kümeye **girmez**.
- [ ] **Tek bulk read (N+1 YOK):** 500 adaylık bir `concept.affinity` çözümlemesinde concept node reader ve
      relationship reader **her biri ≤ 1** kez çağrılır; uzmanlık kümesi **bir kez** türetilip tüm adaylara
      uygulanır (sahte repo çağrı sayacı ile kanıtlanır).
- [ ] **ConceptGraph mutate EDİLMEDİ:** çözümleme öncesi/sonrası `concept_nodes`, `concept_relationships`,
      `concept_types`, `concept_chain_templates` collection'larında **doküman sayısı ve `UpdatedAt` değişmez**;
      ayrıca `git diff --stat` çıktısında `Features/Knowledge/Concept/**`, `Domain/Entities/Concept*.cs` ve
      `Domain/Repositories/IConceptGraphRepositories.cs` **boştur (diff ∅)**.
- [ ] **Yeni graph yüzeyi açılmadı:** repoda yeni bir graph aggregate / repository / controller / endpoint
      **yoktur**; `IConceptGraphRepositories` metot imzaları **birebir aynıdır** (grep + diff ile).
- [ ] **Canlılık (freeze yok):** `active` bir segmentte graph'a yeni bir `addresses` kenarı eklendiğinde
      **aynı** `(segmentId, SegmentVersion)` ile yapılan `resolve` **yeni** üyeyi döndürür; kriter ağacı
      dondurulmuş olsa bile affinity **canlıdır** (§4.4.1).
- [ ] **`SubjectType` uyumu:** `concept.affinity` `account` segmentinde → **400**
      `segment_attribute_not_applicable_for_subject_type`.
- [ ] **Uzmanlığı boş aday** `concept_subject_specialty_missing` ile elenir — boş uzmanlık asla eşleşme sayılmaz.
- [ ] **Veri hizalama görünürlüğü:** uzmanlık düğümlerinin `ExternalRefId` değerleri ile adayların
      `contact.specialty` değerleri **farklı BRD set'lerinden** geldiğinde sonuç **boş küme + reason code**'dur
      (sessiz "eşleşti" **yok**); smoke raporu bu durumu **veri hizalama eksikliği** olarak ayırt eder (§20/F-CONCEPT-DATA).

**Cross-service / cross-module fail-closed**
- [ ] MDM erişilemezken ürün/marka değerli kriterle (`consent.scope-*` **veya** `concept.affinity`) **create** →
      **503** `segment_dependency_unavailable` **ve** `segments` collection'ında **yeni doküman yok**
      (doğrulama `CreateAsync` **öncesinde** çağrılır).
- [ ] MDM 404 → **400** `segment_criteria_reference_not_found` (503 **değil** — ayrım testlenir).
- [ ] MDM validator'ında **cache yoktur** (aynı id iki kez → **iki** HTTP çağrısı), timeout **3 sn**,
      **1** transient retry, `Authorization` / `X-Tenant-Id` / `X-Correlation-Id` **forward** edilir.
- [ ] MOD-0164 `unknown` → aday **elenir** + `consent_unknown`; hiçbir kod yolunda `unknown` → `allowed` **değildir**.
- [ ] MOD-0151 aktif model yokken territory kriteri → aday elenir + `territory_coverage_unavailable`;
      çözümleme **503 vermez** (in-service degradation ≠ dependency failure — §8.4 ayrımı testlenir).
- [ ] Faz 2'de **aday başına HTTP/repo çağrısı yoktur**: 500 adaylık bir çözümlemede consent reader **≤2**,
      territory reader **≤2** kez çağrılır (N+1 yasağı).

**Ölçek**
- [ ] 10.001 aday üreten bir kriter → **422** `segment_candidate_set_too_large`, tavan cevapta **görünür**,
      kısmi liste **dönmez**.
- [ ] 10.000 adaylık dinamik segment `resolve` **≤ 5 sn**; `is-member` **≤ 300 ms** (ölçüm audit dosyasına yazılır).
- [ ] `GET /api/crm/subjects/{type}/{id}/segments` **≤ 200** aktif segment değerlendirir; aşımda **422**.

**Yetki**
- [ ] `crm.segment.read` olan ama `crm.segment.resolve` olmayan aktör: segmenti **görür**, `resolve` / `evaluate` →
      **403**, ve cevabın hiçbir yerinde üye kimliği **yoktur**.
- [ ] `activate` `crm.segment.activate` gerektirir; `crm.segment.manage` tek başına **yetmez** (SoD).
- [ ] `SegmentPermissions.cs` **hiçbir DB yazımı yapmaz** (grep: repository/collection referansı yok).

**Frontend**
- [ ] `Views/CRM/Segments/*.cshtml` dosyalarının **tamamında** `Layout = "_LayoutTenantShell"` **AÇIKÇA** yazılıdır.
- [ ] Klasörde `_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` **YOKTUR** (Compact kuralı).
- [ ] `verify_datatable_page.py --area CRM --module Segments --reference compact` çalıştırılır; sonuç mevcut CRM
      Compact baseline'ı ile **karşılaştırılır**; yeni FAIL **kapatılır veya gerekçesi audit'e yazılır**.
- [ ] Kriter editöründeki nitelik / operatör / parametre alanları **`attribute-catalog` cevabından** üretilir;
      JS'te **hardcoded liste yoktur** (grep ile kanıtlanır).
- [ ] 7 dil RESX **parite**dir (anahtar seti eşit); `index.l10n.js` camelCase→PascalCase köprüsünü uygular
      (toast'ta `(undefined: <corrId>)` **görülmez**).
- [ ] Filtre host'u `class="dt-inline-filter-host"` taşır; ikinci bir DataTable'ın `drawCallback`'i bu tablonun
      filtre/colvis rozetini **silmez**.
- [ ] Nav `<li>` permission-guard'lıdır ve mevcut CRM `<li>`'leri **değişmemiştir** (`git diff` ile).

---

## 17. Test Expectations

### 17.1 Build & statik doğrulama
- `dotnet build services/Diten.CrmService/src/Diten.CrmService.Api/Diten.CrmService.Api.csproj -c Debug` → **PASS**
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` → **PASS**
  (fleet lock'u varsa `-t:CoreCompile` / `-p:BuildProjectReferences=false` hilesi)
- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug` → **PASS**
- `verify_datatable_page.py --area CRM --module Segments --reference compact` → çalıştırılır ve **baseline ile
  diff'lenir**; ham sayı tek başına kabul kriteri **değildir**.
- RESX parite kontrolü (7 dil, anahtar seti eşit).

### 17.2 Backend unit/integration testleri (`Diten.CrmService.Application.Tests`) — hedef **≥ 55 test**

| Alan | Kapsam (min) |
|---|---|
| `SegmentAggregateTests` | create/update/activate/archive/new-version · kod benzersizliği · `SubjectType` immutability · lifecycle geçiş matrisi · **tenant izolasyonu** · concurrency 409 · **hard delete yokluğu** |
| `SegmentCriteriaValidationTests` | katalog dışı nitelik · izinsiz operatör · arity · `Parameters` zorunluluğu · derinlik/düğüm/çocuk/`in` tavanları · döngü · `not` arity · `static`↔`Criteria` çelişkisi · dondurma 409 |
| `SegmentMembershipResolverTests` | **determinizm (3× aynı sonuç)** · sıralama anahtarı · elenen adayların reason code'ları · `static`/`dynamic`/`hybrid` formülü · include/exclude önceliği · effective window · superseded sürüm çözümleme · **persist yokluğu** · consent `unknown` → elenir · territory unavailable → elenir · **MDM unavailable → 503 + persist yok** · MDM 404 → 400 · **N+1 yasağı** · aday tavanı 422 |
| `TargetCustomerRulesTests` | benzersizlik 409 · `SubjectType` eşleşmesi · `dynamic` segmentte 400 · gerekçe zorunluluğu · mode geçişi update'tir · arşiv sonrası update 409 |
| `SegmentAttributeCatalogTests` | katalog **kapalıdır** (beyan edilmemiş kod reddedilir) · her nitelik sınıf + operatör + parametre beyanı taşır · katalog endpoint çıktısı iç katalogla **birebir** · `concept.affinity` sınıf **D** olarak beyan edilir (X **değil**) |
| `ConceptAffinityResolutionTests` | ExternalRef eşleşmesi (`global-product` → `reference-data-value`; diğer tipler kümeye girmez) · ürün düğümü yok → **boş küme + `concept_product_node_missing`, 503 yok** · derinlik 1 / 2 / 3→400 · yalnız `addresses`+`belongs-to` (karışık kenar fixture'ı) · yön + `bidirectional` · archived/inactive/effective dışı eleme · **tek bulk read (N+1 yasağı, çağrı sayacı)** · uzmanlığı boş aday elenir · `SubjectType=account` → 400 · **graph mutate edilmediği** (sahte repo'da Insert/Update çağrısı **0**) · canlılık (kenar eklenince aynı sürüm yeni üye döndürür) |

### 17.3 Authenticated smoke (Gateway) — `scripts/smoke-mod0167-fu02-segment-foundation-authenticated.ps1`

- Şablon: `smoke-mod0162-fu05-*.ps1`. **Script yazıldıktan sonra GERÇEKTEN çalıştırılır**; sonuçlar rapora
  kopyalanır. (Emsal: FU04'te 19 bozuk `Add-Result` çağrısıyla script hiç çalıştırılmadan "PASS" bildirildi.)
- Tenant-scoped login: `X-Tenant-Id` header'ı **zorunlu** (aksi hâlde platform tenant token'ı gelir).
- PS 5.1 tuzağı: `@(Where-Object).Count` sayımı — tekil sonuçta `.Count` beklendiği gibi davranmaz; dizi sarmalama
  zorunlu.
- Kapsam (min **48** assertion): contract + attribute-catalog (`concept.affinity` sınıf **D** beyanı dâhil) ·
  **concept-affinity uçtan uca**: ürün düğümü olmayan P → boş küme + `concept_product_node_missing` (200, **503 değil**) ·
  graph'ta ürün→uzmanlık kenarı kurulmuş P → beklenen doktor üye · `maxDepth=3` → 400 ·
  `account` segmentinde nitelik → 400 · **çözümleme sonrası ConceptGraph okumaları değişmedi** (node/edge sayısı sabit) ·
  segment CRUD + 409'lar · activate/SoD ·
  new-version + remap · static/dynamic/hybrid resolve · **3× determinizm karşılaştırması** · includeExcluded ·
  is-member (member / not-member / unknown) · manuel üye CRUD + 409 · `dynamic` + manuel → 400 · cross-tenant 404 ·
  403 (resolve yetkisiz) · MDM-down senaryosu → 503 + **persist yok** doğrulaması · aday tavanı 422.
- **Ön koşul:** §15 Gateway route'ları eklenmiş **ve** fleet yeniden başlatılmış olmalı; aksi hâlde tüm çağrılar
  404 + `{}` döner.
- **`concept.affinity` pozitif senaryosunun veri ön koşulu (kod değil):** tenant'ta bir ürün düğümü
  (`global-product`) → `addresses`/`belongs-to` → uzmanlık düğümü (`reference-data-value`) zinciri **ve** o
  uzmanlık koduyla eşleşen `contact.specialty` değerine sahip en az bir Contact bulunmalı. Zincir yoksa
  **boş-küme senaryosu** koşulur ve rapor bunu **veri eksikliği** olarak işaretler — kod hatası olarak **değil**
  (§20/F-CONCEPT-DATA).

### 17.4 Browser smoke
- `/CRM/Segments` yüklenir; DataTable v2 + skeleton + filtre + colvis rozetleri çalışır.
- Create → kriter ağacı editöründe grup/predicate ekleme; nitelik seçilince operatör listesi **katalogdan** dolar.
- Details → salt-okunur ağaç + `resolve` önizlemesi (üye sayısı + elenen gerekçeleri) görünür.
- `active` segmentte kriter alanları **disabled** ve *"yeni sürüm oluştur"* yönlendirmesi görünür.
- Dil değiştirme: 7 dilde etiketler dolu, `undefined` yok.

---

## 18. Ready-for-dev Checklist

- [ ] Golden Reference **Compact** (DEV-0001 pack + gerçek kod) referans olarak okundu.
- [ ] Frontmatter tüm zorunlu alanlar dolu (`service`, `shell`, `golden_reference`, `entity_base`, `form_field_count`).
- [ ] Layout & Shell Contract'ta Razor `Layout = "_LayoutTenantShell"` açıkça yazılı (§9).
- [ ] Backend File Convention Golden Reference ile birebir (`CommandHandlers`/`QueryHandlers` ayrı; suffix yok) (§10).
- [ ] Frontend File Contract Compact 9 dosya tam; `_CreateEditOffcanvas` / `_DetailsQuickView` **listelenmemiş** (§11.2).
- [ ] Validation Rules her field için yazılı (§12).
- [ ] Failure Path ≥4 senaryo (duplicate + missing + unauthorized + concurrency **ve** fazlası) (§13).
- [ ] Authorization Convention: permission listesi + policy + actor + fallback + seed/grant **yok** beyanı (§14).
- [ ] Gateway routing kararı açık: **gerekli**, `integration-agent` task'ı ayrıştırıldı (§15, F-GATEWAY).
- [ ] Acceptance criteria test edilebilir maddeler (§16).
- [ ] Test expectations build / verifier / RESX / smoke kapsıyor (§17).
- [ ] **D1–D6 + D-TC / D-VER / D-TENANT / D-VOCAB / D-RBAC kullanıcı tarafından onaylandı** ([Ek D](#ek-d--karar-gerekçeleri-tam)).
- [x] **D-PRODUCT karara bağlandı (2026-08-27):** ürün-affinity = `concept.affinity`, ConceptGraph-türetilmiş
      **derived-in-service** kriter, **FU-A'da**, canlı, eşleme tablosuz (§4.4.1 + §19.1).
- [ ] **Veri hizalama ön koşulu (a) + (b) operatör tarafında planlandı** (§4.4 D-PRODUCT · §20/F-CONCEPT-DATA):
      concept graph ürün→uzmanlık ilişkileriyle dolduruldu **ve** `contact.specialty` ile uzmanlık düğümlerinin
      `ExternalRefId`'si **aynı BRD specialty set'inden** geliyor. **Kod blocker'ı değildir**; eksikse
      `concept.affinity` boş küme + reason code döner.
- [ ] Performans bütçesi (§8.5) kabul edildi veya revize edildi.
- [ ] `status: ready-for-dev` **ve** `runtime_code_allowed: true` **ayrı ve açık** kullanıcı kararıyla çevrildi.

---

## 19. Implementation Notes

- **Neden `Diten.CrmService`:** kriter Account/Contact **üzerinde** çalışır; Faz-1 pushdown'ı (tek Mongo filtresi)
  ancak aynı serviste/aynı DB'de mümkündür. Ayrı bir "segmentation service" her kriteri HTTP'ye çevirir ve
  10.000 adaylık bir çözümlemeyi imkânsız kılar. Ayrıca `IConsentPreferenceEvaluator` ve
  `AccountCurrentCoverageResolver` **zaten** bu servistedir — in-process çağrı, cross-service çağrıya tercih edilir.
- **Mevcut kodun tekrar etmemesi gereken tuzakları:** `DateTimeOffset` BSON dizisi (paralel-array index/sort 500'ü) ·
  yeni aggregate'in `RegisterClassMaps`'e eklenmemesi (sessiz boş sorgu) · partial index'te `$ne` (Platform
  crash-loop'u) · proxy `ForwardAsync` 204 → 500 · `index.l10n.js` PascalCase köprüsünün atlanması ·
  ikinci DataTable'ın `drawCallback`'inin ilk tablonun rozetlerini silmesi.
- **Doğrulama disiplini:** verifier ve smoke sonuçları **kendi çalıştırmanla** doğrulanır; orchestrator'ın
  bildirdiği sayılar tek başına kanıt sayılmaz (MOD-0162-FU03/FU04/FU05 emsali).
- **`resolve`'un sözleşmesi bilerek dar:** persist yok, log yok, sayfalı, tavanlı. Bu darlık FU-B'nin
  (materialization) tasarım özgürlüğünü korur: bugün bir cache eklemek, yarın onu geri almayı imkânsızlaştırır.
- **`superseded` sürümün çözümlenebilir kalması** kasıtlıdır: geçmiş bir kampanya hedefinin *"o gün neden bu
  kişi seçilmişti?"* sorusu ancak o günkü sürüm çözümlenebilirse cevaplanır.
- **Master-plan/registry saplaması:** `master-development-plan.md` satır 134 MOD-0167'yi `planned / reserved · 0%`
  gösteriyor; `module-implementation-status.md`'ye satır **yalnız kod indiğinde** ve closeout'ta eklenir (F-REG).

### 19.1 Ürün-affinity kararının gerekçesi (D-PRODUCT — revize, 2026-08-27)

**Karar:** *"X ürünüyle ilgilenen doktorlar"* kriteri **FU-A'da yapılır** ve `concept.affinity` olarak
**MOD-0162-FU03 ConceptGraph'ından türetilir** (sınıf **D — derived-in-service**).

**Gerekçe:**
1. **Zaten aynı serviste.** ConceptGraph, `Segment`/`Account`/`Contact` ile **aynı** `Diten.CrmService`
   içindedir. Bu, affinity'yi §8.4'ün **in-service** tarafına koyar: belirsizlik bir *cevaptır* (aday elenir +
   reason code), bir *arıza* değil → **503 yok**, kısmi sonuç sorunu yok, yeni bir cross-service bağımlılık yok.
2. **İlgi bağı zaten var — sadece kişiye yazılmamış.** `ürün --addresses/belongs-to--> uzmanlık` kenarı
   MOD-0162-FU03'te **authorable**'dır. Bu bağı yeniden modellemek, var olan bir bilgiyi **ikinci kez**
   sahiplenmek olurdu; SoR ihlali ve senkronizasyon borcu doğardı.
3. **Eşleme tablosu gerekmez ve istenmez.** `specialty ↔ terapötik alan` eşlemesini ayrı bir tabloya yazmak,
   graph ile tablo arasında **iki doğruluk kaynağı** yaratırdı; graph güncellenince tablo sessizce eskirdi.
   Eşlemeyi graph'ın kendisi taşıdığı için kriter **dinamiktir** — bu, "TAM DİNAMİK CDP" kararının doğal sonucudur.
4. **Canlılık kasıtlıdır.** `activate` (D-VER) **kriter ağacını** dondurur — *hangi soru soruluyor*'u sabitler.
   Türetmenin **cevabını** dondurmaz. Graph'a yeni bir kenar eklendiğinde aynı segment sürümü yeni bir üye
   döndürür; bu bir determinizm ihlali **değildir**, çünkü §8.3 sözleşmesi determinizmi "**değişmemiş kaynak
   veri**" koşuluyla tanımlar — tıpkı bir Contact'ın `specialty`'si değiştiğinde olduğu gibi.
5. **Ölçek riski yok.** Uzmanlık kümesi `S` **aday sayısından bağımsız** olarak, çözümleme başına **bir kez**
   türetilir (§8.5 Faz 2); maliyet graph boyutuyla sınırlıdır ve traversal derinliği **≤2** ile tavanlıdır.

**Reddedilen alternatifler:**

| Alternatif | Neden reddedildi |
|---|---|
| **Önceki taslak kararı: "EA-TBD → FU-D'ye ertele"** | Yanlış bir yokluk varsayımına dayanıyordu ("ilgi bağı repoda yok"). Bağ **var**, sadece Contact üzerinde değil graph'ta. Erteleme, FU-A'yı en yüksek değerli pharma kriteri olmadan teslim ederdi |
| `Contact.ProductInterests[]` / `ContactProductAffinity` aggregate'i | Kişiye ürün listesi yazmak = **üçüncü** bir ürün-ilgi master'ı; graph ile çelişir, elle bakım ister, MOD-0162 SoR'unu böler |
| Ayrı `SpecialtyProductMap` eşleme tablosu | İki doğruluk kaynağı + sessiz eskime (gerekçe 3) |
| MDM'den cross-service affinity okuma | MDM ürün master'ıdır; *"kim ilgilenir"* bilgisi orada **yok**. Ayrıca §8.4 gereği her graph okuması 503 riski taşırdı |
| Sınırsız/geçişli graph traversal (en-iyi-yol, skorlama) | MOD-0162-FU03 bilinçli olarak **motor değildir** (1hop/2-layer). Burada bir traversal motoru açmak o sınırı FU-0167 üzerinden delerdi → derinlik **≤2**, `supportsConceptGraphTraversalEngine: false` |
| Türetilmiş uzmanlık kümesini `TargetCustomer`'a yazmak | D3 (persist yok) ve D-TC (yalnız manuel üyelik) ihlali olurdu |

---

## 20. Follow-up Items

| # | Follow-up | Sahip |
|---|---|---|
| **F-GATEWAY** | `ocelot.json`'a `/api/crm/segments`, `/api/crm/segments/{everything}`, `/api/crm/subjects/{everything}` çiftleri (OPTIONS dâhil) — **runtime ön koşulu** | `integration-agent` |
| **F-RBAC** | `crm.segment.*` 6 anahtarın RBAC kataloğuna alınması + tenant Admin rolüne grant (`manual-grant-*` marker) + re-login | MOD-0018 / operatör |
| **F-REG** | `execution/registries/module-id-registry.md` + `module-implementation-status.md` satırları (MOD-0167-FU01 §11/F2 ile birlikte) | registry / governance owner |
| **F-RD** | MOD-0048'de segment vokabüler setlerinin publish'i (runtime blocker **değil** — D-VOCAB=A) | MOD-0048 operatör |
| **F-SCALE** | **FU-B (MOD-0167-FU03)**: materialized membership + refresh + üyelik geçmişi — tasarımı FU-A'nın **ölçüm çıktısına** dayanmalı | commercial-suite |
| **F-STRATEGY** | **FU-C (MOD-0167-FU04)**: `StrategyTemplate` + `SubjectList` + `UCLN` (legacy CrmV2 adapt-not-copy) | commercial-suite |
| **F-CDP** | **FU-D (MOD-0167-FU05)**: hesaplanmış/türev nitelikler (RFM, ICP score, son ziyaret) + **segment usage log** (MOD-0167-FU01 §11/F4) | commercial-suite |
| ~~F-PRODUCT-LINK~~ | ✅ **KAPATILDI 2026-08-27** — ayrı bir Account/Contact ↔ Product ilgi bağı **gerekmiyor**; affinity MOD-0162-FU03 ConceptGraph'ından türetiliyor (§4.4.1 + §19.1). EA kararı **beklenmiyor** | — |
| **F-CONCEPT-DATA** | **Veri hizalama (kod değil, `F-RD` sınıfı operatör işi):** (a) concept graph'ın **ürün → uzmanlık** (`addresses`/`belongs-to`) ilişkileriyle doldurulması; (b) `contact.specialty` ile uzmanlık düğümlerinin `ExternalRefId`'sinin **aynı BRD specialty set'ini** kullanması. Eksikse `concept.affinity` **boş küme + reason code** döner (hata değil) | MOD-0162 içerik sahibi / MOD-0048 operatör |
| **F-CONCEPT-UX** | Segment kriter editöründe ürün seçicisi: mevcut `/api/global-products/selector` proxy'si (`mdm.global-products.read`) yeniden kullanılır — MOD-0162-FU03'ün kullandığı yüzeyin aynısı; yeni endpoint açılmaz | commercial-suite |
| **F-TIER** | `tier` niteliğinin birinci sınıf alan mı yoksa `AccountAttributeValue` anahtarı mı olduğu — `AccountAttributeValue` attribute-definition SoR'u zaten EA-TBD | EA / MOD-0149 |
| **F-SNAPSHOT** | MOD-0165 `CampaignTargetSnapshotHandler`'ın `ISegmentMembershipReader`'a bağlanması (**MOD-0165 tarafında**, bu FU'da değil) | MOD-0165 |
| **F-FU01-CLOSE** | MOD-0167-FU01 §10'daki *"Reviewer onayı → status: approved"* kutusunun kapatılması; FU01 §11/F1 bu pack ile **karşılandı** | commercial-suite |

---

## Ek D — Karar Gerekçeleri (tam)

### D1 — FU ayrışması: **FU-A tam dinamiktir** (Seçenek A)

**Seçenek A (ÖNERİLEN):** FU-A = Segment + kriter + **real-time dinamik değerlendirme** + TargetCustomer.
**Seçenek B (REDDEDİLDİ):** FU-A = Segment + yalnız statik üyelik; FU-B = dinamik motor.

**Gerekçe:** (1) Statik-only bir `Segment`, MOD-0167-FU01'in ihtiyacı olan **membership seam'ini karşılamaz** —
FU01 §5 "kurala göre kim?" sorusunu sorar, "listeye kim yazıldı?" sorusunu değil; yani B, FU01'i bloklu bırakır.
(2) Kriter modeli (D2) **ancak bir değerlendirici tarafından** falsifiye edilebilir; motor olmadan yazılan bir
predicate şeması, FU-B'de neredeyse kesin olarak yeniden yazılır → **iki migration**. (3) Kullanıcı kararı
"TAM DİNAMİK CDP"dir; B bu kararı FU-B'ye erteleyip FU-A'yı "boş bir liste ekranına" indirger.
**B'nin haklı olduğu tek nokta** — ölçek riski — A içinde **D3 + D4** ile ayrıca ele alınmıştır: dinamik motor
vardır, ama **materialization yoktur** ve **tavanlıdır**.

### D2 — Kriter modeli: **stored predicate-tree**

| Aday | Karar | Gerekçe |
|---|---|---|
| **Stored predicate-tree** (embedded, tiplenmiş düğümler) | **SEÇİLDİ** | Şema ile doğrulanabilir; katalog + operatör kısıtı **create anında** uygulanabilir; UI doğrudan eşlenir; sürümlenebilir/dondurulabilir; kriter ağacı **veriden ibarettir** — kod çalıştırmaz |
| **Query-DSL** (string ifade: `specialty = 'cardiology' AND tier IN (...)`) | REDDEDİLDİ | Parser + grammar + injection yüzeyi getirir; hatalar ancak çalıştırma anında görünür; UI ancak metin kutusu olur; sürüm farkını diff'lemek imkânsızlaşır; katalog kısıtı zorlanamaz |
| **Tag-based** (kişilere etiket, segment = etiket kümesi) | REDDEDİLDİ | Operatör/aralık/tarih semantiğini kaybeder (`created-at between`, `is-null` ifade edilemez); etiketleri **kimin, ne zaman, hangi kuralla** verdiği yeni bir SoR (etiketleme motoru) ister; "dinamik" değil, **materialize edilmiş** üyeliğin kılık değiştirmiş hâlidir |

### D3 — Üyelik: **real-time, persist YOK**

Kullanıcı kararı real-time'dır ve bu FU onu **saflıkla** uygular: dinamik üyelik hiçbir collection'a yazılmaz.
**Gerekçe:** MOD-0167-FU01/D2 ("üyelik kopyalanırsa segment değiştiğinde kural sessizce eskir") ve MOD-0165-FU02
§5.1 ("snapshot bir türevdir, ikinci bir master değildir") aynı yasağı iki farklı yerden koyar. Üyeliği persist
etmek, bu FU'yu **ikinci bir üyelik master'ı** hâline getirirdi. Materialization bir **performans optimizasyonudur**
ve optimizasyon, ölçüm olmadan tasarlanmaz → **FU-B** (F-SCALE).

### D4 — Ölçek: **iki fazlı bounded evaluation + sert tavan**

N×M kombinatoriği **hiçbir zaman** çalıştırılmaz (§8.5 tablosu). Tavan aşımında **sessiz kırpma yerine 422**
seçilmiştir: kısmi bir üye listesi, hiç liste olmamasından **daha tehlikelidir** — kimse eksik olduğunu bilmez.
422 ise yazarı kuralı daraltmaya zorlar. Performans bütçesi (§8.5) **AC'ye bağlanmıştır**, yani ölçülür ve
audit'e yazılır — FU-B'nin girdisi budur.

### D5 — Nitelik kataloğu: **kapalı ve beyan edilmiş**

Serbest alan adı kabul etmek, kriteri **sessizce kırılabilir** yapardı (alan adı değişince kural hiçbir şey
eşleştirmez, hata da vermez). Kapalı katalog: (a) create anında 400 verir, (b) UI'ı hardcode'suz besler
(`platform-lookups-reference-data.md` "hardcoded fallback yasak"), (c) hangi niteliğin **cross-service** olduğunu
**görünür** kılar — yani fail-closed davranış tahmin edilebilir olur.

### D6 — Fail-closed sınırı: **in-service gerekçelenir, cross-process 503'tür**

| | In-service (MOD-0151, MOD-0164) | Cross-process (MDM) |
|---|---|---|
| Belirsizlik | **Deterministik bir cevaptır** (`unknown`) → aday elenir + reason code | **Bilgi yokluğudur** → cevap verilemez |
| Sonuç | Çözümleme **tamamlanır** | **503**, kısmi sonuç yok, persist yok |

**Gerekçe:** MOD-0164 evaluator'ı sözleşmesi gereği **throw etmez** ve kontrollü `unknown` döner — bunu 503'e
çevirmek, sistemin normal bir cevabını arıza gibi göstermek olurdu. Buna karşılık ağ üzerinden gelen bir
belirsizlik gerçekten **bilgisizliktir**; Working Calendar FU03'ün MDM validator'ı bu durumda persist etmeyi
yasaklar ve aynı profil (cache yok / 3 sn / 1 retry / 503) burada birebir uygulanır.

### D-TC — TargetCustomer: **ayrı collection, yalnız manuel**

Embedded reddedildi: statik bir segmentte üye sayısı **sınırsızdır** (16MB doküman limiti), satır düzeyinde
concurrency gerekir ve satırlar bağımsız sorgulanır ("bu kişi hangi segmentlere elle eklenmiş?").
`MembershipMode`'un yalnız iki değeri olması kasıtlıdır: türetilmiş üyelik buraya yazılabilseydi D3 çöker
ve "bu satır kuraldan mı geldi, elle mi?" sorusu satır satır okunarak cevaplanırdı.

### D-VER — Sürüm / effective-dating semantiği

- **`SegmentVersion` iş alanıdır, `Version` değildir** (`entity-base-template.md` naming kuralı: `Version` teknik
  concurrency için rezerve).
- **`active` sürümde kriter DONDURULUR.** Gerekçe: bir çözümleme sonucu ancak `(SegmentId, SegmentVersion)`
  çiftiyle açıklanabiliyorsa denetlenebilir. Kriteri yerinde düzenlemek, geçmiş her açıklamayı sessizce
  geçersizleştirirdi.
- **Değişiklik = `new-version`**: klon yeni `NodeId`'lerle üretilir ve `ParentNodeId` referansları remap edilir
  (MOD-0162-FU04/FU05'in klon + remap deseni).
- **Effective dating sürüm başınadır**; `resolve` daima çağrılan sürümle çalışır, "en son"a **sessizce kaymaz**.
- Sürüm dışı kalmış (`superseded`) sürüm **çözümlenebilir kalır** ve bayrağı **görünürdür**.

### D-TENANT — Tenant izolasyonu

`EntityBase` tenant-owned; `TenantId` **server-side** claim'den çözülür ve **hiçbir DTO/payload'da** yer almaz;
cross-tenant okuma **404 / boş liste**. Ek olarak **çözümleme yüzeyi** de izole edilir: aday sorgusu tenant
filtresi olmadan **kurulamaz**, ve türev okumalar (`AccountCurrentCoverageResolver`, `IConsentPreferenceEvaluator`)
zaten tenant-scoped'tur. `resolve` cevabı **yalnız** aynı tenant'ın subject id'lerini taşır (§16'da testlenir).

### D-VOCAB — Vokabüler = **A (in-domain fail-closed)**

MOD-0162-FU02/FU03/FU04/FU05 ve MOD-0164-FU02 ile aynı: vokabüler `Domain/Entities/Segment.cs` içinde
`static class`, set dışı değer **400**. MOD-0048 publish'i **runtime ön koşulu değildir** — aksi hâlde bu FU,
sahibi başka bir modül olan bir operatör işine bloklanırdı. Setler aynı vokabülerle ayrıca yayınlanır (F-RD).

### D-RBAC — Yetki

6 kanonik anahtar **tanımlanır**, **seed/grant yapılmaz** (kullanıcı kilidi). Endpoint'ler MOD-0165-FU04 ile
aynı belgelenmiş fallback üzerinde çalışır ve fallback **hiçbir guard'ı genişletmez**. `.resolve`'un ayrı bir
anahtar olması kasıtlıdır: **segment tanımını okumak, üyelerin kimliğini görmeye yetmemelidir** (PII).
`.activate`'in ayrı olması SoD içindir (kuralı yazan ≠ canlıya alan).

---

## Handoff

Module pack **`draft`** olarak hazır. Lütfen inceleyip gerekli alan/scope düzeltmelerini yapın.
Geliştirme için status `approved` veya `ready-for-dev` **ve** `runtime_code_allowed: true` olmalıdır
(**ayrı ve açık** kullanıcı kararı); sonra `@orchestrator MOD-0167-FU02` çağrılır.

Hazırlık sırasında Golden Reference **Compact** (DEV-0001) şablon olarak alındı — sapma yok.

**Onayınızı bekleyen kararlar:** D1–D6 · D-TC · D-VER · D-TENANT · D-VOCAB · D-RBAC ([Ek D](#ek-d--karar-gerekçeleri-tam)).

**Kapanan karar:** ✅ **D-PRODUCT** (2026-08-27) — ürün-affinity `concept.affinity` olarak **FU-A'dadır**;
MOD-0162-FU03 ConceptGraph'ından **derived-in-service** türetilir, **canlıdır**, eşleme tablosu **gerektirmez**
(§4.4.1 · §19.1). Geriye kalan tek iş **kod değil veri**: F-CONCEPT-DATA (graph'ın ürün→uzmanlık ilişkileriyle
doldurulması + ortak BRD specialty set'i).
