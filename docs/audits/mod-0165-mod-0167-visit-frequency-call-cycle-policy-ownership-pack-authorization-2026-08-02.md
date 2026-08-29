# MOD-0165 / MOD-0167 — Visit Frequency / Call-Cycle Policy Ownership Pack Authorization

> Tarih: 2026-08-02
> Kapsam: Sahiplik / veri sözleşmesi / öncelik kuralı / tüketim boundary'si — **kod yazma yok, runtime yok**
> Sonuç: **PASS**

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Task türü | **Documentation / pack authorization** (implementation değil) |
| Runtime servis çağrısı | **Yapılmadı** (bu task canlı smoke gerektirmez) |
| Değiştirilen runtime kod | **0 dosya** |
| Çalışma alanı | `execution/domains/commercial-suite/module-packs/**` + `docs/audits/**` |
| DCP-002 gate `MOD-0165` | `OK  MOD-0165: proven against Blueprint/registry.` (exit 0) |
| DCP-002 gate `MOD-0167` | `OK  MOD-0167: proven against Blueprint/registry.` (exit 0) |
| DCP-002 gate `MOD-0165-FU01` | `OK  MOD-0165-FU01: proven against Blueprint/registry.` (exit 0) — `--parent MOD-0165` |
| DCP-002 gate `MOD-0167-FU01` | `OK  MOD-0167-FU01: proven against Blueprint/registry.` (exit 0) — `--parent MOD-0167` |
| Blueprint canonical parent adları | `MOD-0165 = Campaign Management` · `MOD-0167 = Segmentation / CDP` (registry satır 220/222 ile birebir) |
| Registry satırı yazımı | **Yapılmadı** (pack yetkisi dışı → follow-up F4) |

Komut (Windows'ta `python3` yok, `py` launcher kullanıldı):

```
py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU01 \
   --name "Visit Frequency / Call-Cycle Policy Ownership" --parent MOD-0165
py .antigravity/scripts/verify_module_id.py . --check-id MOD-0167-FU01 \
   --name "Segment-Sourced Visit Frequency Policy Authoring" --parent MOD-0167
```

Zorunlu bağlam okundu: `AGENTS.md` · `execution/domains/commercial-suite/domain-config.md` ·
`module-packs/README.md` · `crm-sor-boundary.md` · `legacy-value-preservation.md` ·
`execution/registries/module-id-registry.md` · `.antigravity/rules/module-pack-standard.md` ·
`.antigravity/agents/module-pack-author.md` · MOD-0150 pack §20 · MOD-0151 pack §7.12/§11.4/§22.6/§23.

---

## 2. Dependency Confirmation

| Ön koşul | Durum | Kanıt |
|---|---|---|
| MOD-0150 Contact Availability & Visit Preference | **PASS** | [mod-0150-contact-availability-visit-preference-implementation-2026-08-01.md](mod-0150-contact-availability-visit-preference-implementation-2026-08-01.md) · [positive live smoke retry](mod-0150-contact-availability-visit-preference-positive-live-smoke-retry-2026-08-02.md) |
| MOD-0151 FU09A Visit/Route Readiness | **PASS** | [implementation](mod-0151-fu09a-visit-route-readiness-implementation-2026-08-02.md) · [positive closeout](mod-0151-fu09a-positive-resource-current-coverage-live-smoke-closeout-2026-08-02.md) |
| MOD-0151 FU09A authenticated Gateway smoke | **PASS** | [read-only reverification](mod-0151-fu09a-read-only-reverification-stable-positive-resource-coverage-fixture-2026-08-02.md) |
| MOD-0155 | **Başlamadı** | `module-packs/` altında pack yok — sahiplik bu task'ta sözleşme olarak kayda geçti |
| Brand/Product master | **Netleşmedi** | MOD-0151 F6/F16; bu task'ta future optional bırakıldı (§11) |
| Knowledge/Content Management | **Netleşmedi** | Ayrı pack authorization follow-up'ı (§12) |
| Workflow / approval / ChangeRequest | **En sona bırakıldı** | MOD-0151 FU06 future; bu task'ta açılmadı |

Canlı readiness bugünkü davranışı (FU09A raporundan, değiştirilmedi): `frequencyStatus=unknown` ·
`selectedFrequencyPolicyId=null` · `lastVisitDate=null` · `dueStatus=unknown` · `reasonCodes=[frequency_unknown]`.

---

## 3. Business Need Summary

MOD-0150 *"bu kişi bu lokasyonda **ne zaman** ziyaret edilebilir?"* sorusunu kapattı.
MOD-0151 FU09A *"bu account/contact **kimin** sorumluluğunda, coverage **current** mı?"* sorusunu kapattı.

Açık kalan tek soru: **"Bu kişi / account / lokasyon hangi **sıklıkta** ziyaret edilmeli?"**

Bu bilgi MOD-0155 Visit Planning'in zorunlu girdisidir ve sahipsiz kalırsa en kolay ama en yanlış yere gömülür:
`Contact` veya `Account` üzerine düz bir `VisitFrequency` alanı. Bu kısayol, aynı hedefin farklı campaign, farklı
segment, farklı BU, farklı dönem ve (ileride) farklı brand/product altında **farklı sıklık** taşıyabilmesi
gerçeğini kaybeder ve geri dönüşü pahalıdır (MOD-0151 R10).

Kanonik ihtiyaç (kayda geçirildi):

```text
Dr. Ayşe
- Almiba Q1 Campaign için ayda 2 ziyaret
- Bekant için ayda 1 ziyaret            (brand-scoped → future)
- A segment genel kuralı için ayda 4 ziyaret
- Medicana Beylikdüzü lokasyonunda ziyaret edilecek
```

Bu dört satır **çakışma değil**, aynı anda doğru kurallardır; tekilleştirme bir **öncelik kuralıyla** yapılır (§9).

---

## 4. Ownership Decision

| Katman | Sorumluluk | Sahiplik türü |
|---|---|---|
| **MOD-0165** Campaign Management | `VisitFrequencyPolicy` / `CallCyclePolicy` **aggregate'inin SoR'u**; campaign/cycle bazlı policy üretimi; **tek provider read contract'ı** | Owner / SoR |
| **MOD-0167** Segmentation / CDP | Segment/targeting bazlı policy **yazarı**; `Segment`/`TargetCustomer`/`UCLN` SoR'u; aynı aggregate'e `Source=segmentation` ile yazar | Co-author / producer |
| **MOD-0155** Field Sales / Visit Planning | Policy'yi **tüketir**; last visit + due/overdue; visit target, visit plan, route plan | Consumer / engine |
| **MOD-0151** Territory Management | Policy **üretmez, saklamaz, engine çalıştırmaz**; yalnız eşleşme anahtarını verir; readiness'te selected policy metadata'sı görünebilir | Boundary consumer |
| **MOD-0150** Contact & Relationship | Frequency sahibi **değildir**; yalnız availability | Sibling boundary |

**D1 — Tek aggregate, tek store, tek provider contract.** MOD-0167 ayrı bir frequency store açmaz.
*Reddedilen alternatif:* her üretici kendi store'unu tutsun, MOD-0155 birleştirsin → iki priority engine, iki farklı
"seçilen policy" cevabı, deterministik tek provider endpoint'inin kaybı, çakışma diagnostiğinin imkânsızlaşması.

**D2 — Frequency asla düz alan değildir.** `Contact.VisitFrequency`, `Account.VisitFrequency`,
`AccountContactLink.VisitFrequency`, `TerritoryNode.VisitFrequency`, `Resource.VisitFrequency` **kalıcı olarak
yasaklandı**.

**D3 — Segment üyeliği policy'ye kopyalanmaz.** `TargetType=segment` policy'si segmenti hedefler, üyelerini değil;
üyelik resolution anında MOD-0167'ye sorulur (MOD-0167-FU01 D2).

---

## 5. Authorized Policy Model

`VisitFrequencyPolicy` (alias `CallCyclePolicy`) — **sözleşme yetkilendirildi, implement edilmedi**:

`PolicyId` · `TenantId` (JWT claim'inden; payload'da **asla**) · `PolicyCode` · `PolicyName` · `TargetType` ·
`TargetId` · `BusinessUnit` · `TerritoryNodeId?` · `CampaignId?` · `SegmentId?` · `BrandId?` *(future)* ·
`ProductId?` *(future)* · `CycleId?` · `CyclePeriodId?` · `FrequencyType` · `RequiredVisitCount` · `PeriodType` ·
`EffectiveFrom` · `EffectiveTo?` · `Priority` · `Source` · `Status` · `Notes?` ·
`CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy`.

Kurallar: hard delete yok · Account/Contact/AccountContactLink/TerritoryNode master'ı mutate edilmez · coverage ve
availability verisi policy'ye **kopyalanmaz** (yalnız anahtarla referans) · vokabülerler MOD-0048 reference set
olarak yayınlanır, **hardcoded fallback yasaktır** (öneri set adları: `visit-frequency-type` ·
`visit-frequency-period-type` · `visit-frequency-source` · `visit-frequency-status`).

---

## 6. TargetType Policy

| `TargetType` | Anlam | Zorunlu ek alan |
|---|---|---|
| `account` | Kurum/eczane/hastane sıklığı | — |
| `contact` | Doktor/eczacı genel sıklığı | — |
| `account-contact-link` | **En doğru saha hedefi** — "Dr. Ayşe + Medicana Beylikdüzü" | — |
| `segment` | A/B/C segment kuralı | `SegmentId` |
| `territory-node` | Territory geneli kural | `TerritoryNodeId` |
| `campaign-target` | Campaign hedef listesinden türeyen sıklık | `CampaignId` |

Spesifiklik sırası (tie-breaker'da kullanılır):
`account-contact-link > campaign-target > contact > account > segment > territory-node`.

---

## 7. Frequency / Period Policy

**`FrequencyType`:** `weekly` · `biweekly` · `monthly` · `cycle-based` · `custom`
**`PeriodType`:** `day` · `week` · `month` · `quarter` · `cycle` · `campaign-period` · `custom`

Tutarlılık kuralları: `RequiredVisitCount <= 0` → **400** · `cycle-based` ise `CycleId` **veya** `CyclePeriodId`
zorunlu → yoksa **400** · `PeriodType=campaign-period` ise `CampaignId` zorunlu → yoksa **400** · çelişkili
`FrequencyType × PeriodType` kombinasyonu → **400** (matris implementation FU'sunda sabitlenir) · `custom`
serbest metinle değil, açık `PeriodType=custom` + `RequiredVisitCount` + `Notes` ile ifade edilir.

---

## 8. Source / Status Policy

**`Source`:** `campaign` · `segmentation` · `manual` · `legacy-import` · `business-rule` · `manager-override` ·
`other` — provenance'tır, varsayılan öncelik bandını belirler, sessizce değiştirilemez (audit'lenir).

**`Status`:** `draft` · `active` · `inactive` · `archived`

| Kural | Karar |
|---|---|
| Hard delete | **Yasak** |
| `draft` | Resolution'a girmez |
| `active` | Yalnız effective window içinde girer |
| `archived` | Yeni visit planning'e **girdi olmaz**; geçmiş açıklanabilirliği için okunabilir kalır |
| History | Durum geçişleri audit'lenir, kayıtlar silinmez |

---

## 9. Priority / Conflict Policy

**`Priority` zorunlu sayısal alandır ve küçük değer kazanır.** Açık değer verilmezse `(Source, TargetType)` ikilisi
varsayılan bandı belirler:

```text
1. manager-override        → 100
2. campaign-target         → 200
3. account-contact-link    → 300
4. contact                 → 400
5. account                 → 500
6. segment                 → 600
7. territory-node          → 700
8. business-rule / default → 800
```

Aynı `Priority` durumunda **deterministik tie-breaker**:

```text
1. most specific target wins   (§6 sırası)
2. latest EffectiveFrom wins
3. stable PolicyId ordinal     (final fallback)
```

Değişmez kurallar:

- **Sessiz/rastgele seçim yasaktır** — aynı girdi her zaman aynı policy'yi seçer.
- **Seçilen policy görünür olmalıdır**: `SelectedFrequencyPolicyId` + `PolicyCode` + `Source` + `Priority` +
  `SelectionReason` (`priority` / `specificity` / `effective_from` / `policy_id_order`).
- **Çakışanlar diagnostics olarak raporlanabilir**: `CandidatePolicies[]` (elenenler + eleme nedeni). Bastırılan
  segment policy'si kaybolmaz.
- Policy yoksa `FrequencyStatus=unknown` + `SelectedFrequencyPolicyId=null`; **varsayılan sıklık uydurulmaz**.

---

## 10. Effective Window / Cycle Policy

| Kural | Karar |
|---|---|
| `EffectiveFrom` | **Zorunlu** |
| `EffectiveTo` | Optional (açık uçlu policy geçerli) |
| `EffectiveTo < EffectiveFrom` | **400** |
| Window dışı policy | Resolution'a **giremez** |
| Future policy | Yalnız ilgili `effectiveAt` ile dahil olur; bugünün planına sızmaz |
| `cycle-based` | `CycleId` veya `CyclePeriodId` gerekir |
| `campaign-period` | `CampaignId` gerekir |
| Campaign/cycle penceresi | Policy penceresi campaign/cycle penceresini aşamaz → **400** |
| `effectiveAt` semantiği | MOD-0151 FU05A/FU09A ile aynı: açık parametre, sunucu "şu an"ı sessizce varsaymaz |

---

## 11. Brand / Product Boundary

- `BrandId` / `ProductId` **future optional** — sözleşmede rezerve, implement edilmedi.
- Bu FU **Brand/Product master implementasyonu yapmaz**, sahiplik talep etmez (MOD-0151 F6/F16 ile tutarlı).
- Brand/Product **yokken** policy `account` / `contact` / `account-contact-link` / `segment` / `campaign-target` /
  `territory-node` üzerinden tam çalışır.
- Brand/Product geldiğinde policy bu boyutlarla **daraltılabilir**; mevcut policy'ler geçersizleşmez.
- **Follow-up:** `Brand/Product Master Boundary Pack Authorization`.

---

## 12. Knowledge / Content Boundary

| Soru | Sahip |
|---|---|
| "Ne sıklıkla gidilecek?" | Frequency policy — **MOD-0165 / MOD-0167** |
| "Ne anlatılacak / hangi içerik?" | **Knowledge / Content Management** (ayrı pack) |
| İkisini birlikte tüketen | **MOD-0155** |

Content selection frequency policy içinde **hardcoded olmaz**; campaign policy ileride Content/Knowledge
paketleriyle **ilişkilendirilebilir**, ancak içerik SoR'u frequency policy'de değildir.
**Follow-up:** `Knowledge / Content Management Pack Authorization`.

---

## 13. MOD-0155 Consumer Boundary

MOD-0155 ileride yapar: selected policy'yi okuma · last visit ile due/overdue hesabı · visit target üretimi ·
route planning · daily/weekly visit plan · visit execution.

**Bu FU bunların hiçbirini yapmaz.** Provider read contract (öneri; route'lar `integration-agent` yetkisindedir):

```text
GET /api/crm/visit-frequency-policies/resolve
    ?targetType=…&targetId=…&businessUnit=…&territoryNodeId=…&campaignId=…&effectiveAt=…
→ SelectedPolicy{…, SelectionReason} + CandidatePolicies[]{…, ExclusionReason} + FrequencyStatus
```

Provider **asla** döndürmez: due/overdue verdict'i · last visit tarihi · ziyaret sırası · mesafe/süre · günlük plan ·
optimizasyon skoru. Provider **kuralı** verir, **kararı** MOD-0155 verir.

---

## 14. MOD-0151 FU09A Integration Boundary

- MOD-0151 **policy provider değildir**: yazmaz, saklamaz, engine çalıştırmaz.
- MOD-0151'in verdiği tek şey **eşleşme anahtarıdır**: `TargetType/TargetId` + `BusinessUnit` + `TerritoryNodeId` +
  current coverage (FU05A guard'lı) + current resource responsibility.
- Route readiness response'unda ileride **selected policy metadata'sı görünebilir** (read-only); policy seçimi ve
  due/overdue engine **MOD-0155'e** aittir.
- Provider yokken bugünkü davranış **doğrudur ve korunur**: `FrequencyStatus=unknown`,
  `SelectedFrequencyPolicyId=null`, `DueStatus=unknown`, `reasonCodes=[frequency_unknown]`.
  **Default frequency uydurulmaz.**
- Bu authorization nedeniyle MOD-0151 tarafında **hiçbir kod/şema/contract değişikliği gerekmedi**; yalnız pack
  içindeki F21 follow-up satırı ve §25 next-prompt maddesi **dokümantasyon olarak** güncellendi.

---

## 15. Explicit Exclusions

Runtime implementation · backend/frontend kod · aggregate/migration/endpoint/resolver · UI · visit planning ·
route planning · route optimization · due/overdue engine · last visit history · visit execution ·
GPS/check-in/check-out · visit report · digital detailing · survey · Knowledge/Content implementation ·
Brand/Product master implementation · Campaign engine implementation · Segmentation engine implementation ·
Account/Contact mutation · ContactAvailability mutation · territory assignment mutation ·
`ContactTerritoryAssignment` · patient data · workflow approval · ChangeRequest · MOD-0023 entegrasyonu ·
evidence pack · import/export yeni scope · hard delete · Mongo hand-edit · RBAC seed/grant · MOD-0048 publish ·
registry satırı yazımı · `TenantId` payload'da · doğrudan `5061` business API çağrısı.

---

## 16. Contract Flags

Pack seviyesinde **öneri** olarak kaydedildi (implementation anlamına gelmez, canlı contract'a yazılmadı):

```json
{
  "supportsVisitFrequencyPolicy": true,
  "supportsCallCyclePolicy": true,
  "supportsFrequencyPolicyPriority": true,
  "supportsFrequencyPolicyEffectiveWindow": true,
  "supportsFrequencyPolicyProvider": true
}
```

**Eklenmedi (kesin):** `supportsVisitPlanning` · `supportsRoutePlanning` · `supportsRouteOptimization`.
MOD-0151 canlı contract'ı **değiştirilmedi**; `supportsWorkflowActivation=false` ve mevcut FU09A flag seti korundu.
MOD-0167 için ayrı frequency flag'i tanımlanmadı — tek kaynak MOD-0165-FU01 §16'dır.

---

## 17. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** |
| MOD-0155 code changed? | **No** |
| MOD-0151 code changed? | **No** (yalnız pack dokümanında F21 + §25 metin güncellemesi) |
| MOD-0150 code changed? | **No** |
| Visit planning opened? | **No** |
| Route planning opened? | **No** |
| Frequency runtime engine opened? | **No** |
| Campaign engine opened? | **No** |
| Segmentation engine opened? | **No** |
| Brand/Product implementation opened? | **No** |
| Knowledge/Content implementation opened? | **No** |
| Account/Contact mutation opened? | **No** |
| ContactAvailability mutation opened? | **No** |
| Territory mutation opened? | **No** |
| ContactTerritoryAssignment opened? | **No** |
| Patient data opened? | **No** |
| Workflow opened? | **No** |
| RBAC seed/grant changed? | **No** |
| MOD-0048 publish changed? | **No** |
| Registry satırı yazıldı mı? | **No** (follow-up olarak açıldı) |
| Policy ownership boundary added? | **Yes** |
| Follow-ups opened? | **Yes** (7 adet — §19) |

Ek doğrulama: yeni pack'lerin ikisinde de `runtime_code_allowed: false`, `status: draft`, `shell: none`,
`golden_reference: none`, `form_field_count: 0`.

---

## 18. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0165-FU01-visit-frequency-call-cycle-policy.md` | **Oluşturuldu** — policy SoR + alan sözleşmesi + priority/conflict + effective window + boundary'ler |
| `execution/domains/commercial-suite/module-packs/MOD-0167-FU01-segment-sourced-frequency-policy-authoring.md` | **Oluşturuldu** — segment co-author sahipliği + membership seam |
| `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md` | **Güncellendi** (yalnız doküman) — §23 F21 satırına frequency alt-follow-up kapanışı; §25 madde 5 güncellendi |
| `docs/audits/mod-0165-mod-0167-visit-frequency-call-cycle-policy-ownership-pack-authorization-2026-08-02.md` | **Oluşturuldu** (bu rapor) |

Runtime kod, config, gateway, RBAC, reference data ve registry **değiştirilmedi**.

---

## 19. Follow-ups Opened

| # | Follow-up | Owner |
|---|---|---|
| F1 | **`Brand/Product Master Boundary Pack Authorization`** | EA / MDM + commercial-suite |
| F2 | **`Knowledge / Content Management Pack Authorization`** | commercial-suite / EA |
| F3 | MOD-0048 frequency reference set authoring + publish (4 set) | MOD-0048 operator |
| F4 | Registry satırları: `MOD-0165-FU01`, `MOD-0167-FU01` | registry / governance owner |
| F5 | `MOD-0165-FU01-RBAC — Visit Frequency Policy Permission Catalog Alignment` | MOD-0018 / commercial-suite |
| F6 | `MOD-0165-FU02 — Visit Frequency Policy Implementation` | commercial-suite |
| F7 | `MOD-0155-PREREQ — Last Visit / Due-Overdue Engine Ownership` (zincirin son açık ayağı) | MOD-0155 |

---

## 20. Final Verdict

### **PASS**

- `VisitFrequencyPolicy` / `CallCyclePolicy` sahipliği netleşti: **tek aggregate, tek SoR (MOD-0165), tek provider
  contract**.
- MOD-0165 **owner/custodian**, MOD-0167 **co-author** olarak konumlandı; ayrı segment-frequency store reddedildi.
- MOD-0155 **consumer/engine** olarak konumlandı (due/overdue + plan yalnız orada).
- MOD-0151 yalnız **readiness/boundary tüketicisi** olarak kaldı; canlı `unknown` davranışı korundu ve MOD-0151'de
  hiçbir kod değişikliği gerekmedi.
- Frequency, `Contact`/`Account`/`AccountContactLink`/`TerritoryNode`/`Resource` içine düz alan olarak **gömülmedi**
  (kalıcı yasak yazıldı).
- `TargetType` / `FrequencyType` / `PeriodType` / `Source` / `Status` kararları yazıldı.
- Priority + deterministik tie-breaker + görünürlük/diagnostics kuralı netleşti; sessiz seçim yasaklandı.
- Brand/Product ve Knowledge/Content boundary'leri kaybolmadan follow-up olarak açıldı.
- Runtime / route / visit / frequency-engine implementation scope'u **açılmadı**; mevcut scope'lar bozulmadı.
- Implementation prompt'u hazırlanabilir (`MOD-0165-FU02`).

**PARTIAL/FAIL kriterleri karşılanmadı:** module pack'ler güncellendi (2 yeni + 1 cross-ref), Brand/Product ve
Knowledge boundary'leri yazıldı, priority/conflict policy tam, MOD-0155 tüketim boundary'si tam; frequency Contact
içine açılmadı, MOD-0151 frequency engine sahibi yapılmadı, due/overdue sahipliği MOD-0155'te kaldı, runtime ve
route/visit scope açılmadı, Account/Contact mutation açılmadı.

---

## 21. Next Recommended Prompt

`Brand/Product Master Boundary Pack Authorization`
