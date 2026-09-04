---
id: MOD-0155-FU06B
name: Activity Time Budget
parent: MOD-0155
parent_name: Field Sales / Visit Planning
siblings: MOD-0155-FU01 (Planned Visit — draft) · MOD-0155-FU02 (Visit Report) · MOD-0155-FU03 (Route Planning) · MOD-0155-FU04 (Visit Content Sequence Execution) · MOD-0155-FU05 (MicroTarget) · MOD-0155-FU06 (Cycle Capacity — SHIPPED, review) · MOD-0155-FU07 (Cycle Capacity Monthly Redesign — SHIPPED, review)
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
runtime_code_allowed: true
flip_approved_by: "user via Control Tower — 2026-08-29 (D-COLLISION resolved to REUSE of FU06 root fields; only BetweenVisitTimeMinutes added; Control-Tower-verified). Build ordered AFTER FU01."
runtime_code_scope: "ACTIVE (flipped 2026-08-29, user/Control Tower). Kapsam: mevcut `CycleCapacity` aggregate'ine TEK yeni config alanı `BetweenVisitTimeMinutes` (root int) + class-map (AutoMap scalar) + read-time `EnsureBetweenVisitTime` migration (backfill YOK) + SAF `ActivityTimeBudgetCalculator` (pure duration fonksiyonu, FU06'nın MEVCUT kök alanlarını OKUR, motor DEĞİL) + contract yüzeyine 1 alan + CycleCapacity Compact _Form/Details'e tek input'luk config kartı + 7 dil RESX + boundary/hesap testleri (`Diten.CrmService` + `frontend/Diten.Web`). YASAK: FU06/FU07 shipped davranışının DEĞİŞTİRİLMESİ (kök `PromoProductTime`/`NonPromoProductTime`/`ReportDuration`/`TravelingTime`/`QuizDuration` alanları, `CycleCapacityCalculator`, `TotalVisitNumber` hesabı DOKUNULMAZ ve yeniden BEYAN edilmez), duplicate süre alanı EKLEME (REUSE kararı), scheduling/packing/route/plan üretimi (motor), CyclePeriod aggregate/contract/flag yazımı, `services/Diten.Platform/**` içine dosya, hesaplanan sürenin PERSIST edilmesi (duration asla saklanmaz), backfill/migration script, Mongo hand-edit, ocelot.json yazımı, RBAC seed/grant, MOD-0048 publish, registry yazımı."
owner: module-pack-author
branch: feature/crm/mod-0155-fu06b-activity-time-budget
started: 2026-08-29
target: TBD (kullanıcı onayı + ready-for-dev flip sonrası)
form_field_count: 10
predecessor: MOD-0155-FU06 (Cycle Capacity — SHIPPED) + MOD-0155-FU07 (Monthly Redesign — SHIPPED)
consumers: MOD-0155-FU04 (PlannedVisit → PlannedDurationMinutes) · MOD-0155-FU05 (packing engine → between-visit buffer + durations)
dependencies:
  - MOD-0155 (parent — Field Sales / Visit Planning; SoR = saha planlama)
  - MOD-0155-FU06/FU07 (ZORUNLU ÖNCÜL — `CycleCapacity` aggregate + kök süre alanları + Compact UI; bu FU onları ADDITIVE genişletir ve kök alanları REUSE eder, YENİDEN ŞEKİLLENDİRMEZ)
  - MOD-0155-FU04 (İLERİ TÜKETİCİ — PlannedVisit content-ref'ten `PlannedDurationMinutes` üretir; bu FU ona bir hesap FONKSİYONU verir, çağırmaz)
  - MOD-0155-FU05 (İLERİ TÜKETİCİ — paketleme motoru `BetweenVisitTimeMinutes` + süreleri okur; bu FU motor DEĞİLDİR)
  - MOD-0165-FU06/FU07 (CyclePeriod — SALT-OKUNUR, DEĞİŞMEZ)
  - MOD-0018 (RBAC — yeni anahtar YOK; mevcut `crm.cycle-capacity.*` tüketilir)
  - DEV-0001 (Golden Reference Compact — şablon, Compact kararı DEĞİŞMEZ)
---

# MOD-0155-FU06B — Activity Time Budget

> **⛔ DRAFT — KOD YETKİSİ YOKTUR (`runtime_code_allowed: false`).**
> Bu pack bir **planlama + hazırlık** dokümanıdır. `status: ready-for-dev` + `runtime_code_allowed: true` flip'i
> **AYRI bir kullanıcı kararıdır**; `@orchestrator` bu pack ile kod yazamaz.
>
> FU06 kapasiteyi kurdu ("bu ay kaç ziyaret YAPILABİLİR") ve bunu yaparken bir ziyaretin dakika bileşenlerini —
> `PromoProductTime`, `NonPromoProductTime`, `ReportDuration` — zaten **root alan olarak sakladı**. FU07 ekranı
> okunabilir hâle getirdi. Bu FU üçüncü bir soruyu sözleşmeye bağlar:
> *"**TEK bir ziyaret** kaç dakika sürer, ve iki ziyaret arasında ne kadar tampon bırakılır?"*
>
> **Cevabın büyük kısmı ZATEN vardır (REUSE):** süre = f(içerik sayısı × FU06'nın mevcut kök süreleri). Bu FU o
> mevcut alanları **tek gerçek kaynak** olarak yeniden kullanır, **duplike etmez**. Eklenen **tek yeni şey**
> iki-ziyaret-arası tampondur (`BetweenVisitTimeMinutes`) — FU06'da `TravelingTime` var ama between-visit yok.
> Ayrıca **saf bir aritmetik fonksiyon** tanımlanır. Bu FU plan **üretmez**, ziyaret **paketlemez**, rota
> **çıkarmaz**, süre **saklamaz**.
>
> Otorite sırası: **Blueprint Excel** > bu pack > [Domain Config](../domain-config.md) >
> [crm-sor-boundary.md](../crm-sor-boundary.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 0. Kimlik Geçidi ve Ev Kararı

### 0.1 DCP-002 — PASS (2026-08-29)

```text
$ py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU06B --name "Activity Time Budget" --parent MOD-0155
OK  MOD-0155-FU06B: proven against Blueprint/registry.
REAL_EXIT=0
```

**FU numarası gerekçesi (D-FU).** İki aday değerlendirildi ve **FU06B** seçildi:

| Aday | Geçit sonucu | Karar |
|---|---|---|
| **`MOD-0155-FU06B`** | **PASS** (yukarıda) | **SEÇİLDİ.** `B` son eki bu FU'nun **FU06 kapasite aggregate'inin additive bir alt-uzantısı** olduğunu — yeni bir yüzey değil, aynı `CycleCapacity` üstünde tek bir yeni alan + bir hesap fonksiyonu — kimlik seviyesinde bildirir. FU06/FU07 ile birlikte "kapasite ailesi"ni oluşturur. |
| `MOD-0155-FU08` | (denenmedi — gereksiz) | Görev girdisi FU06B reddedilirse fallback izin verdi; **FU06B geçtiği için kullanılmadı**. FU08 "kapasite ailesinden bağımsız yeni bir FU" izlenimi verirdi ki bu yanlış olurdu. |

Geçit **kimliği** doğrular (parent'ın Blueprint/registry'de varlığı + FU id çakışması), açıklayıcı **adı** değil.
Parent'ın kanonik adı **"Field Sales / Visit Planning"**'dir ve değişmez; frontmatter'daki `name` repo-tarafı
açıklayıcıdır. **Registry satırı bu pack tarafından EKLENMEZ** (FU06/FU07 emsali) → §20 / F-REGISTRY.

### 0.2 D-HOME — Ev **MOD-0155**'tir (FU06 ile aynı gerekçe)

Bu FU, FU06'nın açtığı `CycleCapacity` aggregate'ini genişletir; aggregate zaten MOD-0155'e ait ve
`Diten.CrmService`'te yaşıyor. Duration ("bir ziyaret kaç sürer") bir **saha planlama** ölçüsüdür —
`crm-sor-boundary.md`'nin *"Visit Plan / MicroTarget / Visit / route plan → MOD-0155"* satırının tam içinde.
CyclePeriod (MOD-0165) salt-okunur kalır; bu FU CyclePeriod'a **hiç dokunmaz**.

---

## 1. Module Summary

### 1.1 Ne yapar

Mevcut **`CycleCapacity`** aggregate'ine (aynı cycle-period scope) **TEK** bir yeni config alanı ekler ve **saf
bir süre fonksiyonu** tanımlar. Süre bütçesinin çekirdeği FU06'da **zaten vardır** ve bu FU onu **yeniden
kullanır**:

| Alan | Kaynak | Bu FU'da |
|---|---|---|
| `PromoProductTime` | **FU06 kök alanı (mevcut)** | **REUSE** — BİR promo ürünü sunma dakikası; okunur, dokunulmaz |
| `NonPromoProductTime` | **FU06 kök alanı (mevcut)** | **REUSE** — BİR non-promo ürünü sunma dakikası; okunur, dokunulmaz |
| `ReportDuration` | **FU06 kök alanı (mevcut)** | **REUSE** — ziyaret raporu dakikası; okunur, dokunulmaz |
| **`BetweenVisitTimeMinutes`** | **YENİ (bu FU)** | Paketlemede iki ardışık ziyaret arası tampon; FU06'da yoktu |

Ve bir **saf süre fonksiyonu** (read-model / hesaplayıcı, **motor DEĞİL**), FU06'nın mevcut alanlarını okuyarak:

```
visitDurationMinutes = (#promoContentItems  × PromoProductTime)      // FU06 kök alanı
                     + (#nonPromoContentItems × NonPromoProductTime)  // FU06 kök alanı
                     + ReportDuration                                 // FU06 kök alanı
```

Ürün süresi **DÜZ**tir (promo/non-promo başına sabit — legacy `CyclePeriodCalendar` tarzı) ve o ziyaretin
**gerçek içerik sayısıyla** çarpılır. **Yol süresi bu formülün parçası DEĞİLDİR** (rota optimizer, FU03).
**İki-ziyaret-arası tampon da bu formülün parçası DEĞİLDİR** — `BetweenVisitTimeMinutes` **saklanır** ama
formüle **girmez**; onu paketleme motoru (FU05) ardışık ziyaretler **arasına** uygular.

### 1.2 Hedef kullanıcı

Saha satış yöneticisi / CRM admin (dönem bazında bu bütçeyi **kurar** — çoğu alanı FU06'da zaten kuruyor; bu FU
yalnız between-visit tamponu ekler). Fonksiyonun tüketicileri **makinelerdir**: MOD-0155-FU04 (bir PlannedVisit'in
`PlannedDurationMinutes`'ini kendi content-ref'inden hesaplar) ve MOD-0155-FU05 (paketleme motoru).

### 1.3 Kapasite özeti

Mevcut CycleCapacity aggregate'ine **1 (bir)** yeni root config alanı (`BetweenVisitTimeMinutes`) · class-map
(AutoMap scalar — yeni gömülü tip YOK) + **read-time** `EnsureBetweenVisitTime` migration (backfill YOK) ·
**saf** `ActivityTimeBudgetCalculator.VisitDuration(capacity, promoCount, nonPromoCount)` (FU06 kök alanlarını
OKUR) · contract yüzeyine 1 alan · CycleCapacity Compact _Form/Details'e **tek input'luk** config kartı · 7 dil
RESX. **Yeni endpoint YOK, yeni aggregate/gömülü tip YOK, yeni index YOK, yeni Ocelot route YOK, duplicate süre
alanı YOK.**

### 1.4 Bu FU bir MOTOR DEĞİLDİR

Plan **üretmez**, ziyaret **paketlemez**, rota **çıkarmaz**, sıra/slot **atamaz**, tampon **uygulamaz** (yalnız
değeri saklar; uygulama FU05'te), süre **saklamaz** (duration asla persist edilmez — girdi verilince hesaplanır).
Girdi alır (bir ziyaretin promo/non-promo içerik sayısı), FU06'nın mevcut alanlarını okur, **dakika döndürür**.

> **⚠️ EK BEYAN — bu FU FU06'nın kapasite sayısını DEĞİŞTİRMEZ.** `ActivityTimeBudgetCalculator` **yeni bir read
> model**dir; `CycleCapacityCalculator`'a ve `TotalVisitNumber` hesabına **girmez**. FU06/FU07'nin kök alanları ve
> kapasite bölücüsü (`minutesPerVisit = PromoProductTime + NonPromoProductTime`) **aynen kalır** — bu FU o alanları
> **okur**, ne yeniden beyan eder ne değiştirir. Bir testle kilitlenir (**AC-ADD-1**).

---

## 2. Ownership and Boundaries

### 2.1 In-scope / Out-of-scope

| Kapsam | Karar |
|---|---|
| **In-scope** | Root `BetweenVisitTimeMinutes` alanı (TEK yeni alan) · class-map (AutoMap scalar) + read-time `EnsureBetweenVisitTime` · **saf** `ActivityTimeBudgetCalculator` (FU06 kök alanlarını okuyan pure duration fonksiyonu) · contract yüzeyine 1 alan + limit · CycleCapacity Compact _Form/Details'e 1 input'luk config kartı · 7 dil RESX · hesap + boundary testleri |
| **Out-of-scope (AÇIKÇA ERTELENİR)** | Ziyaret **süresinin bir PlannedVisit'e yazılması** (**FU04**) · süre-bazlı **paketleme** + between-visit tamponun uygulanması (**FU05**) · **rota / yol süresi** (**FU03**) · içerik item'lerinin **promo/non-promo sınıflandırması** (bkz. §19/D-CONTENT-SPLIT → **F-CONTENT-PROMO-SPLIT**) · FU06 kök süre alanlarının **duplike edilmesi** (**REUSE kararı, §4.5**) · süre bütçesinin **per-ay** varyasyonu · approval / actuals / senaryo (FU06 devralınan follow-up'lar) |

### 2.2 SoR sınırı — sahiplenilen vs. yalnız tüketilen

| Nesne | Sahip | Bu FU'da |
|---|---|---|
| `CycleCapacity.BetweenVisitTimeMinutes` (yeni root alan) | **MOD-0155** | **AÇILIR** — bu FU'nun tek yeni alanı |
| kök `PromoProductTime` / `NonPromoProductTime` / `ReportDuration` | MOD-0155 (FU06) | **REUSE — SALT-OKUNUR**; hesap fonksiyonu okur, **yeniden beyan etmez, değiştirmez** (§4.5) |
| kök `TravelingTime` / `QuizDuration` | MOD-0155 (FU06) | **DOKUNULMAZ** — travel FU03'ün, quiz kapasite modelinin |
| `CycleCapacityMonth` + per-ay `Fte` (FU07) | MOD-0155 (FU07) | **DOKUNULMAZ** — süre bütçesi cycle-wide'dır, aya inmez (§4.4) |
| `CycleCapacityCalculator` + `TotalVisitNumber` | MOD-0155 (FU06/FU07) | **DOKUNULMAZ** — yeni fonksiyon AYRI sınıftır, kapasite hesabına girmez |
| `CyclePeriod` | MOD-0165-FU06/FU07 | **SALT-OKUNUR** — FU06 zaten öyle tüketiyor; bu FU dokunmaz |
| `PlannedVisit` / content-ref / `PlannedDurationMinutes` | MOD-0155-FU01/FU04 | **AÇILMAZ** — bu FU tüketicisine bir **fonksiyon** verir, satır yazmaz |
| Paketleme / rota / MicroTarget | MOD-0155-FU03/FU05 | **AÇILMAZ** — motor değil |

### 2.3 Komşu ölçülerle tek cümlelik sınır

> **CycleCapacity** o dönemde **kaç ziyaret sığdığını** söyler (kaba, dönem-geneli tahmin) ·
> **ActivityTimeBudgetCalculator** **TEK bir ziyaretin** kaç dakika sürdüğünü söyler (ince, içerik-bazlı,
> FU06 alanlarını okuyarak) · **Route optimizer (FU03)** ziyaretler arası **yol süresini** söyler ·
> **Packing engine (FU05)** bu süreleri + `BetweenVisitTimeMinutes` tamponu + yolu çalışma gününe **yerleştirir**.
> Dördü ayrı ölçüdür; bu FU yalnız ikincinin **fonksiyonunu** ve tek eksik **tampon alanını** açar.

### 2.4 FU06/FU07'den DEĞİŞMEYENLER (kilitli — additive garantisi)

| Kural | Durum |
|---|---|
| Kök `PromoProductTime`/`NonPromoProductTime`/`ReportDuration`/`TravelingTime`/`QuizDuration` | **DEĞİŞMEZ, yeniden BEYAN edilmez** — yalnız okunur (§4.5) |
| `CycleCapacityCalculator` (saf) + `minutesPerVisit` bölücüsü + `TotalVisitNumber` | **DEĞİŞMEZ** — yeni fonksiyon ona dokunmaz (AC-ADD-1) |
| `TotalVisitNumber` read-time projeksiyon, ASLA persist edilmez | **KORUNUR** — duration da ASLA persist edilmez, aynı ilkeye uyar |
| Per-ay `Fte` (FU07) + read-time `EnsureMonthlyFte` | **DOKUNULMAZ** |
| 1:1 CyclePeriod pin, archive-frees-period, kendi lifecycle'ı yok | **KORUNUR** |
| `golden_reference: compact`, tek Compact konsol, section/card parite kuralı | **KORUNUR ve genişletilir** (1 yeni kart, harita _Form≡Details) |
| `services/Diten.Platform/**` protected | **KORUNUR** — bu FU zaten WC'ye dokunmuyor |

---

## 3. Owned Objects

### 3.1 Domain

| Nesne | Dosya | Not |
|---|---|---|
| `CycleCapacity.BetweenVisitTimeMinutes` (property) | `Domain/Entities/CycleCapacity.cs` (aynı dosya) | `int`, tek yeni alan; paketleme tamponu |
| `CycleCapacity.EnsureBetweenVisitTime(default)` | aynı dosya | Read-time normalleştirme (§4.6/D-MIGRATION); `EnsureMonthlyFte` emsali, tek skaler |
| `CycleCapacityLimits.MaxBufferMinutes` | aynı dosya (mevcut sınıfa 1 sabit) | `240` |
| `CycleCapacityReasonCodes.BetweenVisitTimeInvalid` | aynı dosya (mevcut sınıfa 1 sabit) | `cycle_capacity_between_visit_time_invalid` |

> **Gömülü value object EKLENMEZ.** Tek yeni alan skaler bir `int` olduğu için ayrı bir `ActivityTimeBudget`
> gömülü tipi gereksizdir (bir alan için container = fazladan class-map + round-trip riski, kazancı sıfır). Alan
> doğrudan root'ta, mevcut `TravelingTime`/`ReportDuration` kök int alanlarıyla **aynı seviyede** yaşar. "Activity
> Time Budget" kavramı artık bir **fonksiyondur** (calculator), FU06'nın mevcut alanları + bu tek yeni alan
> üzerinden derlenir.

### 3.2 Application

| Katman | Dosyalar |
|---|---|
| Rules (saf) | **`ActivityTimeBudgetCalculator`** (`Features/CycleCapacity/Rules/ActivityTimeBudgetCalculator.cs`) — **I/O yok**; `(CycleCapacity capacity, int promoCount, int nonPromoCount) → int visitDurationMinutes`, **FU06'nın kök `PromoProductTime`/`NonPromoProductTime`/`ReportDuration` alanlarını okur**. `CycleCapacityCalculator`'a **DOKUNULMAZ** |
| Validation | `CycleCapacityValidation.cs` (mevcut) + `ValidateBetweenVisitTime` — tek alanın aralık kontrolü |
| Services | `CycleCapacityWriteValidator.cs` (mevcut) — `BetweenVisitTimeMinutes`'i normalleştirir; `ICycleCapacityDefaultsProvider.cs` (mevcut) — config default sağlar (magic constant yok) |
| Models | `CycleCapacityModels.cs` (mevcut) — DTO'lara `BetweenVisitTimeMinutes`; `CycleCapacityMapper.cs` (mevcut) — map |
| Contract | `CycleCapacityContract` (mevcut) — 1 alan + limit yayınlanır (hint için) |
| Handlers | Create / Update / Preview (mevcut) — `BetweenVisitTimeMinutes` okur/yazar; **yeni handler klasörü yok** |

> **`ActivityTimeBudgetCalculator` NEDEN ayrı sınıf.** `CycleCapacityCalculator` bir AYın kaç ziyaret sığdırdığını
> (kapasite bölücüsü), yenisi TEK bir ziyaretin kaç sürdüğünü (içerik-bazlı) hesaplar. Ayrı sınıf, AC-ADD-1'i
> (kapasite sayısı değişmez) **yapısal** olarak garanti eder — ikisi FU06'nın **aynı** kök alanlarını okusa da
> farklı sonuç türetir ve birbirini çağırmaz.

### 3.3 Infrastructure / Persistence

| Nesne | Dosya | Not |
|---|---|---|
| Class-map | `Persistence/DependencyInjection.cs` (mevcut) | `BetweenVisitTimeMinutes` `int` skalerdir — mevcut `CycleCapacity` class-map'i `AutoMap` olduğu için **ek eşleme genelde gerekmez**; yeni gömülü tip YOK, Guid serializer YOK. Eski belge okuması için extra-elements (`LegacyElements`) zaten mevcut (§4.6) |
| Index | **YOK** | Config değeridir, sorgulanmaz. `$ne` partial-index crash tuzağı N/A |
| Repository | `Persistence/Repositories/CycleCapacityRepository.cs` (mevcut) | **Her okumada** `EnsureBetweenVisitTime(default)` (FU07 `EnsureMonthlyFte` deseni birebir, tek skaler) |

### 3.4 API endpoints

**YENİ ENDPOINT YOK.** Mevcut CycleCapacity endpoint'lerinin gövdesi 1 alan büyür: `GET .../contract` alanı+limiti
döner; `GET/POST/PUT .../cycle-capacities[/{id}]` `BetweenVisitTimeMinutes` taşır; `POST .../calculation-preview`
onu kabul eder. Duration fonksiyonu **bir API değildir** — tüketicisi (FU04/FU05) in-process çağırır.

### 3.5 Frontend routes

**YENİ ROUTE YOK.** Mevcut `/CRM/CycleCapacities/{Create,Edit,Details}` sayfaları tek input'luk bir config kartı
kazanır. Duration için canlı önizleme gerekmez; kartın altına *"örnek: 2 promo + 1 non-promo → süre FU06
sürelerinden hesaplanır"* şeklinde statik açıklama konabilir (JS gerektirmez).

### 3.6 D-LIFECYCLE — kendi yaşam döngüsü YOKTUR

`BetweenVisitTimeMinutes` bir config alanıdır, kendi durumu yoktur. Düzenlenebilirliği kapsayan CycleCapacity'nin
(dolayısıyla pinlenen CyclePeriod'un) durumundan türer: `closed` dönem onu da dondurur (`409 period_closed`, FU06
kuralı). İkinci durum makinesi yok.

---

## 4. Entity Fields

### 4.1 `CycleCapacity` (root) — eklenen TEK alan

| # | Alan | Tip | Zorunlu | Form? | Kural | Default (config) |
|---|---|---|---|---|---|---|
| — | `BetweenVisitTimeMinutes` | `int` | **Evet** | ✓ | `0 ≤ x ≤ 240`. Paketlemede iki ardışık ziyaret arası tampon. **Süre formülüne GİRMEZ**; FU05 ardışık ziyaretler arasına uygular | **5** |

**Eklenmeyen (REUSE):** `PromoProductTime`, `NonPromoProductTime`, `ReportDuration` — **FU06'da zaten var**,
yeniden beyan **edilmez**. Hesap fonksiyonu onları capacity üstünden **okur** (§4.5).

### 4.2 FU06'dan REUSE edilen alanlar (okunur, bu FU tarafından tanımlanmaz)

| Alan | FU06 tanımı (mevcut) | Bu FU'daki rol |
|---|---|---|
| `PromoProductTime` (int) | "Minutes spent on promoted products in ONE visit" | Süre formülünde **per-promo-item** rate; `#promoContentItems` ile çarpılır |
| `NonPromoProductTime` (int) | "Minutes spent on non-promoted products in ONE visit" | Süre formülünde **per-non-promo-item** rate; `#nonPromoContentItems` ile çarpılır |
| `ReportDuration` (int) | "Minutes spent reporting on a field DAY" | Süre formülünde **per-visit rapor** sabiti (bir kez eklenir) |

> **Semantik not (dürüst).** FU06 bu alanları kapasite hesabında farklı bir mercekle kullanır
> (`minutesPerVisit = PromoProductTime + NonPromoProductTime` düz bölücü; `ReportDuration` per-DAY `DailySpend`'e
> girer). Bu FU **aynı sayısal alanları** ince mercekle (per-item × sayı; report per-visit) okur. **İki merceğin
> ikisi de meşrudur** ve **aynı tek gerçek kaynağı** paylaşır — Control-Tower kararı gereği ayrı/duplike alan
> **açılmaz** (§4.5/D-COLLISION-RESOLVED). Kapasite hesabı bu FU'dan **etkilenmez** (AC-ADD-1).

### 4.3 Duration fonksiyonu — **saf, saklanmaz** (normatif)

```
visitDurationMinutes(capacity, visit) =
      (#promoContentItems(visit)    × capacity.PromoProductTime)      // FU06 kök alanı, REUSE
    + (#nonPromoContentItems(visit) × capacity.NonPromoProductTime)   // FU06 kök alanı, REUSE
    + capacity.ReportDuration                                         // FU06 kök alanı, REUSE
```

- **Ürün süresi DÜZDÜR** (promo/non-promo başına sabit rate), ziyaretin **gerçek içerik sayısıyla** çarpılır.
- **Yol süresi DAHİL DEĞİL** — rota optimizer (FU03).
- **`BetweenVisitTimeMinutes` DAHİL DEĞİL** — tek bir ziyaretin süresine girmez; FU05 **ardışık ziyaretler
  arasına** uygular. Bu yüzden formülde görünmez ama config'te saklanır.
- **Sonuç ASLA persist edilmez.** Fonksiyon saftır; süreyi bir yere yazmak (ör. `PlannedVisit.
  PlannedDurationMinutes`) **tüketicinin** (FU04) işidir. Bu FU hiçbir yere süre yazmaz.
- Girdi (`#promoContentItems`, `#nonPromoContentItems`) **bu FU tarafından ÜRETİLMEZ**; çağıran (FU04) kendi
  content-ref'inden sayar ve fonksiyona **parametre** verir. İçerik item'inin promo/non-promo **sınıflandırma
  kaynağı** bu FU'nun sorumluluğunda değildir (§19/D-CONTENT-SPLIT → F-CONTENT-PROMO-SPLIT).

### 4.4 D-PLACEMENT — `BetweenVisitTimeMinutes` **ROOT'ta**, per-ay DEĞİL

Between-visit tamponu mevsimlik değildir (Ağustos'ta da Mart'ta da ziyaretler arası ~5 dk). FU07'nin "FTE aya
insin" argümanı (saha ekibi mevsimlik) buraya **transfer edilmez**. Alan root'ta, mevcut `TravelingTime` kök
config'iyle **aynı seviyede** yaşar. Aya indirmek 12× anlamsız tekrar üretirdi.

### 4.5 D-COLLISION-RESOLVED — REUSE (Control-Tower kararı, 2026-08-29)

**İlk taslak** üç yeni alan (`PromoProductTimeMinutes` / `NonPromoProductTimeMinutes` / `ReportDurationMinutes`)
önermiş ve kök FU06 alanlarıyla isim çakışmasını *"ayrı container + `Minutes` son eki"* ile **yönetmeyi** planlamıştı.
**Control-Tower bunu REUSE (option A) ile çözdü** — çakışma **yönetilmez, KALDIRILIR**:

| Karar | Sonuç |
|---|---|
| **Duplike alan AÇILMAZ** | `PromoProductTimeMinutes` / `NonPromoProductTimeMinutes` / `ReportDurationMinutes` **kaldırıldı**. Aynı kavramın iki kaydı olmaz |
| **Tek gerçek kaynak = FU06 kök alanları** | `PromoProductTime` / `NonPromoProductTime` / `ReportDuration` zaten bu değerlerdir; fonksiyon onları **okur** |
| Kök alanlar **DOKUNULMAZ** | Salt-okunur, yeniden beyan edilmez, değiştirilmez (§4.2) |
| Tek genuinely-yeni alan | **`BetweenVisitTimeMinutes`** (FU06'da `TravelingTime` var, between-visit yok) |

> **Neden REUSE doğru.** FU06 kök `PromoProductTime`'ı zaten *"minutes on promoted products in ONE visit"* olarak
> tanımlar — bu tam olarak süre formülünün ihtiyaç duyduğu per-promo rate'tir. İkinci bir alan açmak, operatörün iki
> yerde tutarlı tutması gereken **çift kayıt** ve bir gün sessizce sapabilecek iki gerçek üretirdi. REUSE, tek gerçek
> kaynağı korur ve migration'ı da basitleştirir (kopyalanacak/seed edilecek bir şey yok — yalnızca yeni tampon
> alanı için bir default).

### 4.6 D-MIGRATION — **read-time**, backfill YOK (FU07 emsali, tek skaler)

FU07'nin `EnsureMonthlyFte()` deseninin aynısı, ama tek bir skaler için (okuma anında normalleştirme, MIGRATION
değil; geri yazılmaz; değer yalnız satır kendi sebebiyle güncellenince persist olur):

```csharp
// Read-time normalisation — NOT a migration. Nothing written back; no backfill script.
// FU06/FU07 rows have no BetweenVisitTimeMinutes. They get the configured default (5).
// The reused fields (PromoProductTime/NonPromoProductTime/ReportDuration) already exist on every
// FU06 row, so there is NOTHING to seed or copy — only the new buffer field needs a default.
public CycleCapacity EnsureBetweenVisitTime(int configuredDefault) { … }
```

| Durum | Davranış |
|---|---|
| `BetweenVisitTimeMinutes > 0` (veya açıkça 0 set) | Olduğu gibi bırakılır |
| Alan yok / okunamaz (eski FU06/FU07 satırı) | **Config default** (5) uygulanır |

> Kök süre alanları her FU06 satırında zaten dolu olduğundan **hiçbir seed/kopya gerekmez** — REUSE kararının bir
> yan faydası. Eski kayıtların FU06 `TotalVisitNumber`'ı bu migration'dan **ETKİLENMEZ** (yeni alan kapasite
> hesabına girmez; AC-ADD-1 / AC-MIG-1).

> **Backfill script NEDEN yok:** repo yerleşik kuralı (CycleCapacity FU07 emsali); `runtime_code_scope` Mongo elle
> dokunmayı yasaklar. Read-time ensure operatör adımı gerektirmez, yarı-uygulanamaz.

### 4.7 Limit + reason code

`CycleCapacityLimits`'e eklenen: `MaxBufferMinutes = 240`. `CycleCapacityReasonCodes.All`'a eklenen:
`cycle_capacity_between_visit_time_invalid`. **Yeni vokabüler SETİ yoktur** — alan düz int'tir; promo/non-promo
*sınıflandırması* bu FU'da tanımlanmaz (§19/D-CONTENT-SPLIT). Fail-closed in-domain kuralı yalnız aralık üzerinden.

---

## 5. Repo Scope

```text
── Backend: services/Diten.CrmService/ ──
src/Diten.CrmService.Domain/Entities/CycleCapacity.cs
    (DEĞİŞİR — +BetweenVisitTimeMinutes property, +EnsureBetweenVisitTime, +CycleCapacityLimits.MaxBufferMinutes,
     +CycleCapacityReasonCodes.BetweenVisitTimeInvalid.  Kök süre alanları DOKUNULMAZ, yeniden beyan EDİLMEZ)
src/Diten.CrmService.Application/Features/CycleCapacity/
├── Rules/ActivityTimeBudgetCalculator.cs                     (YENİ — saf duration fonksiyonu; FU06 kök alanlarını okur)
├── CycleCapacityValidation.cs                                (DEĞİŞİR — +ValidateBetweenVisitTime)
├── Services/CycleCapacityWriteValidator.cs                   (DEĞİŞİR — between-visit normalize)
├── Services/ICycleCapacityDefaultsProvider.cs +impl          (DEĞİŞİR — +BetweenVisitTime default)
├── CycleCapacityModels.cs                                    (DEĞİŞİR — DTO'lara BetweenVisitTimeMinutes)
├── CycleCapacityMapper.cs                                    (DEĞİŞİR — map)
├── Contract/CycleCapacityContract.cs                         (DEĞİŞİR — 1 alan + limit yayınlanır)
└── Handlers/{CommandHandlers,QueryHandlers}/**               (DEĞİŞİR — between-visit okuma/yazma; yeni handler YOK)
src/Diten.CrmService.Persistence/DependencyInjection.cs       (DEĞİŞİR — gerekirse class-map; yeni gömülü tip/index YOK)
src/Diten.CrmService.Persistence/Repositories/CycleCapacityRepository.cs
    (DEĞİŞİR — her okumada EnsureBetweenVisitTime)
tests/Diten.CrmService.Application.Tests/CycleCapacity/CycleCapacityRuntimeTests.cs   (DEĞİŞİR + yeni testler)

── Frontend: frontend/Diten.Web/ ──
Controllers/CRM/CycleCapacitiesController.cs                  (DEĞİŞİR — proxy gövdesi 1 alanı taşır; yeni route YOK)
Models/CRM/CycleCapacityFormViewModels.cs                     (DEĞİŞİR — +1 form alanı)
Models/CRM/CycleCapacityViewModels.cs                         (DEĞİŞİR — read-side +1 alan)
Views/CRM/CycleCapacities/{_Form,Details}.cshtml             (DEĞİŞİR — +1 config kartı, section haritası _Form≡Details)
Resources/Views/CRM/CycleCapacities/CycleCapacitiesIndex.{ar,en,es,fr,ru,tr,zh}.resx   (DEĞİŞİR — +anahtarlar × 7)
```

**Bu pack (bugün geçerli olan tek yazma alanı):**

```text
execution/domains/commercial-suite/module-packs/MOD-0155-FU06B-activity-time-budget.md
```

---

## 6. Protected Paths

| Path | Neden |
|---|---|
| `.antigravity/**` | Global engineering system |
| **`services/Diten.CrmService/**/Features/CycleCapacity/Rules/CycleCapacityCalculator.cs`** | **Bu FU'nun en sert additive kilidi** — kapasite hesabı DEĞİŞMEZ; yeni fonksiyon AYRI dosyadadır |
| kök `CycleCapacity` alanları `PromoProductTime`/`NonPromoProductTime`/`ReportDuration`/`TravelingTime`/`QuizDuration` + `MinutesPerVisit()`/`DailySpendMinutes()` | FU06 shipped davranışı; **okunur, yeniden beyan/değiştirilmez** (§4.5) |
| `services/Diten.CrmService/**/Features/CyclePeriod/**` | CyclePeriod DEĞİŞMEZ (tek dosya bile) |
| **`services/Diten.Platform/**`** | Başka domain servisi (Working Calendar) — bu FU zaten dokunmuyor |
| `services/Diten.CrmService/**/Features/{Campaign,VisitFrequencyPolicy,Segmentation,StrategyTemplate,Territory,Account,Contact,PlannedVisit}/**` | Komşu FU aggregate'leri; PlannedVisit tüketicidir, bu FU ona yazmaz |
| `frontend/Diten.Web/Views/CRM/CyclePeriods/**`, `.../js/CRM/CyclePeriods/**` | FU06 satır-aksiyonu yerinde; bu FU dokunmaz |
| `gateway/**/ocelot.json` | Yeni route gerekmiyor; integration-agent owned |
| `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, `Archive/**` | FROZEN |
| Diğer domain servisleri (`Diten.MdmService/**`, `Diten.AuthService/**`, `Diten.HcmService/**`, `Diten.EnterpriseStrategyService/**`) | Domain-dışı |
| RBAC katalog/seed + `rolePermissions` | **F-RBAC** — bu pack seed yazmaz (zaten yeni anahtar YOK) |
| `execution/registries/**` | **F-REGISTRY** — registry yazımı pack yetkisi dışı |
| Mongo hand-edit | Yasak (GUID subtype tuzağı tüm login'leri kırar) |

---

## 7. Dependencies

| Bağımlılık | Yön | Durum | Not |
|---|---|---|---|
| **MOD-0155-FU06/FU07** `CycleCapacity` + kök süre alanları + Compact UI | **genişletilir (additive) + REUSE, in-process** | SHIPPED (review) | Bu FU kök alanları **okur** ve 1 alan ekler; shipped davranış DEĞİŞMEZ |
| **MOD-0155-FU04** PlannedVisit content-sequence | **ileri tüketici** | yapılmadı | `ActivityTimeBudgetCalculator`'ı in-process çağırır; bu FU ona yazmaz |
| **MOD-0155-FU05** MicroTarget / packing engine | **ileri tüketici** | yapılmadı | `BetweenVisitTimeMinutes` + süreleri okur; bu FU motor değil |
| **MOD-0165-FU06/FU07** CyclePeriod | salt-okunur, in-process | SHIPPED | FU06 zaten tüketiyor; bu FU dokunmaz |
| **MOD-0018** RBAC | tüketim | kısmi | `crm.cycle-capacity.read`/`.manage` (mevcut); **yeni anahtar YOK** |
| **DEV-0001** Golden Compact | şablon | mevcut | §10/§11 birebir |
| Content promo/non-promo **sınıflandırması** | **eksik — açık** | yok | İçerik item'inin promo mu olduğu bu FU'da tanımlı DEĞİL → **F-CONTENT-PROMO-SPLIT** (§19). Fonksiyon sayıları **parametre** alır |

---

## 8. Runtime Constraints

### 8.1 Genel

- **Persistence:** MongoDB, `TenantId` server-resolved, cross-tenant 404. `BetweenVisitTimeMinutes` skaler root
  alanıdır.
- **Class-map:** yeni alan `int` skalerdir; `CycleCapacity` class-map'i `AutoMap` olduğundan ek eşleme **genelde
  gerekmez**. Yeni gömülü tip YOK, Guid serializer YOK. Round-trip testi yine de eklenir (AC-B-2).
- **Index YOK:** config değeridir; `$ne` partial-index crash tuzağı **N/A**.
- **DateTimeOffset tuzağı N/A:** alan `int`; parallel-arrays 500 riski yok.
- **Duration ASLA persist edilmez:** `TotalVisitNumber`'ın hiç saklanmaması ilkesiyle aynı.

### 8.2 Hesap fonksiyonu **saftır**

`ActivityTimeBudgetCalculator.VisitDuration(CycleCapacity capacity, int promoCount, int nonPromoCount)` → `int`.
`HttpClient` yok, repository yok, `DateTime.UtcNow` yok, `ITenantContext` yok. `capacity`'nin **yalnız**
`PromoProductTime`/`NonPromoProductTime`/`ReportDuration` alanlarını okur (mutasyon yok). Negatif sayı korumalı
(`promoCount`/`nonPromoCount` < 0 → 0'a klipslenir). `int` taşması korumalı (`CycleCapacityCalculator.Visits`
emsali).

### 8.3 Additive garantisi — yapısal

`ActivityTimeBudgetCalculator` **ayrı dosyadadır** ve `CycleCapacityCalculator`'ı ne referans alır ne çağırır;
kök alanları yalnız **okur**, yazmaz. `CycleCapacityCalculator.Calculate` imzası ve gövdesi **değişmez**. Bu,
"yeni fonksiyon kapasite sayısını değiştirmez" iddiasını (AC-ADD-1) kod düzeyinde garanti eder.

---

## 9. Layout & Shell Contract

`shell: tenant` ⇒ **`Layout = "_LayoutTenantShell"`**, dört sayfada da AÇIKÇA yazılı (FU06'da öyle; değişmez).

**Section/card haritası — GENİŞLETİLİR (parite korunur).** FU07'nin son teslim haritası:

```text
_Form    ActivityBudgetSection NotesSection PinnedPeriodSection FieldForceSection MonthlyPlanSection
Details  ActivityBudgetSection NotesSection PinnedPeriodSection FieldForceSection MonthlyPlanSection
```

Bu FU **`BetweenVisitSection`** kartını ekler (tek input); doğal yeri kök minute-budget kartının hemen ardıdır.
Yeni harita (her iki dosyada **birebir aynı** ilan edilmelidir):

```text
_Form    ActivityBudgetSection BetweenVisitSection NotesSection PinnedPeriodSection FieldForceSection MonthlyPlanSection
Details  ActivityBudgetSection BetweenVisitSection NotesSection PinnedPeriodSection FieldForceSection MonthlyPlanSection
```

> **Alternatif (daha az invaziv, önerilir):** ayrı bir kart açmak yerine `BetweenVisitTimeMinutes` input'unu
> **mevcut `ActivityBudgetSection` kartına** (kök süre alanlarının yanına) eklemek — kavramsal olarak oraya aittir
> (hepsi dakika bütçesi) ve section haritasını **hiç değiştirmez**, dolayısıyla verifier parite riski sıfırdır.
> Bu durumda §9 haritası FU07'deki gibi kalır. **Nihai yerleşim (yeni kart vs mevcut karta ekleme) kullanıcı/
> geliştirici tercihidir**; ikisi de _Form≡Details paritesini korur.

> ⚠️ **Kayıtlı tuzak (FU06/FU07):** verifier `h6.text-uppercase.text-heading.fw-semibold` başlıkları section
> başlığı sayar; yeni kart açılırsa bu şekli section başlığı için kullan, kart-içi alt başlık için kullanma.

---

## 10. Backend File Convention

FU06'nın klasör yapısı (Golden Compact birebir) **aynen** kalır. **Bir yeni dosya:**
`Features/CycleCapacity/Rules/ActivityTimeBudgetCalculator.cs` (saf sınıf, `CycleCapacityMonthRules` emsali —
static/sealed, Command/Query suffix yok). Diğer dosyalar yalnız **değişir**. Naming değişmez.

---

## 11. Frontend File Contract (Compact)

FU06 dosya seti **aynen** kalır; **yeni dosya yok, dosya silinmez.** Değişen: `_Form.cshtml` + `Details.cshtml`
(+1 input, §9'a göre yeni kart veya mevcut karta ekleme), form/read ViewModel'ler (+1 alan), 7 dil RESX
(+anahtarlar).

**Compact'ta YASAK (değişmez):** `_CreateEditOffcanvas.cshtml` · `_DetailsQuickView.cshtml`.

### 11.1 Config alanı — sözleşme

| # | Alan | Tip | Kontrol |
|---|---|---|---|
| 1 | `BetweenVisitTimeMinutes` | int | `<input type=number min=0 max=240>` **editable** |

Tek yeni alan **operatör-set config**tir (editable, disabled DEĞİL). Yanında yardım metni: *"İki ardışık ziyaret
arasında bırakılan tampon. Ziyaret süresine dahil değildir; paketleme motoru ardışık ziyaretler arasına uygular."*
(7 dil). FU06'nın mevcut süre alanları (`PromoProductTime` vb.) zaten formda; bu FU onları **tekrar eklemez**.

> **`form_field_count: 10`.** FU07 sonrası 9 alan (`CyclePeriodId`, `CalendarCountryCode`, `DailyWorkMinutes`,
> `PromoProductTime`, `NonPromoProductTime`, `TravelingTime`, `ReportDuration`, `QuizDuration`, `Description`) +
> bu FU'nun **1** yeni editable config alanı (`BetweenVisitTimeMinutes`) = **10**. Ay grid'i (per-ay `Fte` dâhil)
> embedded child grid'dir ve sayılmaz. **10 > 8 ⇒ `golden_reference: compact` DEĞİŞMEZ.**

---

## 12. Validation Rules

| Field | Required | Format/Rule | Pre-check |
|---|---|---|---|
| `BetweenVisitTimeMinutes` | Evet | int, `0 ≤ x ≤ 240` | — |
| Başlık + kök süre + ay alanları | — | **FU06/FU07 ile aynı** (dokunulmaz) | — |

**Yeni reason code:** `cycle_capacity_between_visit_time_invalid`. Aralık dışı → **400**. Payload eksikse sunucu
config default (5) damgalar (fail-closed). Kök süre alanlarının doğrulaması FU06'ya aittir; bu FU onları
doğrulamaz/değiştirmez.

---

## 13. Failure Path to Verify

| Senaryo | Beklenen |
|---|---|
| **Eski kayıt (BetweenVisitTimeMinutes yok)** | Read-time ensure default (5) verir; **FU06 `TotalVisitNumber` DEĞİŞMEZ** (AC-MIG-1); kök süre alanları zaten dolu, seed gerekmez |
| **BetweenVisitTimeMinutes aralık dışı (ör. 999)** | 400 `cycle_capacity_between_visit_time_invalid` |
| **Payload alanı taşımaz** | Sunucu config default (5) damgalar |
| **Negatif içerik sayısı fonksiyona verilir** | 0'a klipslenir; exception yok (saf fonksiyon) |
| **`closed` dönem** | 409 `period_closed` (FU06 kuralı) |
| **Concurrency** | 409, sessiz overwrite yok (FU06 `Version` token'ı) |
| **Duration hesabı** | Fonksiyon `(#promo × capacity.PromoProductTime) + (#nonPromo × capacity.NonPromoProductTime) + capacity.ReportDuration` döner; between-visit **DAHİL DEĞİL**, yol **DAHİL DEĞİL** |

---

## 14. Authorization Convention

**DEĞİŞİKLİK YOK.** `BetweenVisitTimeMinutes` mevcut CycleCapacity endpoint gövdesinde taşınır:
`crm.cycle-capacity.read` / `crm.cycle-capacity.manage`. **Yeni permission anahtarı YOK.** Duration fonksiyonunun
kendi RBAC'ı yoktur — in-process çağrılır; FU04/FU05 kendi yetkilendirmesini uygular. Policy: `[Authorize]`
(tenant shell). Actor: `tenant_user`.

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKSİZ.** `/api/crm/cycle-capacities` + `/{everything}` (GET/POST/PUT/OPTIONS)
FU06'da eklendi. Bu FU **yeni endpoint eklemez** — mevcut gövde 1 alan büyür. `ocelot.json` protected; bu pack
dokunmaz.

---

## 16. Acceptance Criteria

### 16.1 Additive garantisi (en kritik)

- **AC-ADD-1** `CycleCapacityCalculator.Calculate` imzası ve `TotalVisitNumber` çıktısı, `BetweenVisitTimeMinutes`
  eklendikten SONRA da FU07'deki golden örnek sayısını (**3036**) birebir üretir. Yeni alanın hiçbir değeri
  kapasite sayısını değiştirmez.
- **AC-ADD-2** `ActivityTimeBudgetCalculator`, `CycleCapacityCalculator`'ı referans almaz/çağırmaz; kök alanları
  yalnız **okur**, mutate **etmez** (reflection/mutation testi).
- **AC-ADD-3** Kök `PromoProductTime`/`NonPromoProductTime`/`ReportDuration` alanları **yeniden beyan edilmemiştir**
  (duplicate property YOK — statik kod kontrolü).
- **AC-MIG-1** Eski FU06/FU07 dokümanı (BetweenVisitTimeMinutes yok) okunduğunda config default (5) kazanır;
  **aynı dokümanın `TotalVisitNumber`'ı okuma öncesi/sonrası aynıdır**; Mongo'ya hiçbir şey yazılmaz.

### 16.2 Duration fonksiyonu (REUSE)

- **AC-F-1** `capacity{PromoProductTime=5, NonPromoProductTime=3, ReportDuration=3}` için `VisitDuration(capacity,
  promo=2, nonPromo=1)` = `2×5 + 1×3 + 3` = **16** dk (FU06 kök alanlarından okunur).
- **AC-F-2** `BetweenVisitTimeMinutes` süre çıktısına **girmez** (capacity'de between'i 999 yapmak `VisitDuration`
  sonucunu değiştirmez).
- **AC-F-3** Yol süresi çıktıda **yoktur** (fonksiyon travel parametresi almaz).
- **AC-F-4** `promoCount<0` veya `nonPromoCount<0` → 0'a klipslenir, exception yok.
- **AC-F-5** Sonuç hiçbir yere persist edilmez (handler/repository `VisitDuration` sonucunu yazmaz — reflection).

### 16.3 Aggregate + persistence

- **AC-B-1** `CycleCapacity`'de `BetweenVisitTimeMinutes` property'si vardır; başka yeni süre alanı **yoktur**.
- **AC-B-2** Round-trip (write→read) `BetweenVisitTimeMinutes`'i korur.
- **AC-B-3** Payload aralık-dışı → 400 `cycle_capacity_between_visit_time_invalid`.

### 16.4 UI + verifier

- **AC-UI-1** `Views/CRM/CycleCapacities/{Create,Edit,Details}.cshtml`'de `Layout = "_LayoutTenantShell"` AÇIKÇA.
- **AC-UI-2** `_Form.cshtml` ve `Details.cshtml` **aynı** section haritasını ilan eder (§9), yeni alan editable.
- **AC-UI-3** `verify_datatable_page.py --area CRM --module CycleCapacities --reference compact --api-profile
  proxy` → 8 FAIL kümesi **CyclePeriods baseline ile BİREBİR AYNI** (FU06/FU07 baseline korunur).
  `[PASS] Compact _Form matches Details section/card map`.
- **AC-UI-4** 7 dil RESX paritesi: eklenen anahtarlar 7 dilin **hepsinde** var, değer ≠ anahtar-echo.

---

## 17. Test Expectations

- **Unit (saf):** `ActivityTimeBudgetCalculator` — AC-F-1..F-4 (FU06 alanlarından çarpım, between hariç, travel
  yok, negatif klips, int taşma).
- **Additive:** AC-ADD-1 (golden 3036 korunur), AC-ADD-2 (bağımsızlık + read-only), AC-ADD-3 (duplicate property
  yok), AC-MIG-1 (read-time ensure + sayı değişmez + ham doküman yazılmaz).
- **Persistence:** AC-B-2 round-trip; tenant izolasyonu (mevcut CycleCapacity testleri kapsıyor).
- **Validation:** AC-B-3 (aralık).
- **Build:** `dotnet build services/Diten.CrmService/src/Diten.CrmService.Api` → 0 hata;
  `dotnet build frontend/Diten.Web` → 0 hata.
- **Suite:** `dotnet test --filter CycleCapacity` → mevcut 58 + yeni testler, 0 fail; tam suite 0 fail.
- **Verifier:** `verify_module_id --check-id MOD-0155-FU06B` exit 0; `--check-all` HARD violations 0;
  `verify_datatable_page` CycleCapacities ≡ CyclePeriods baseline (AC-UI-3).
- **RESX:** parite PASS (AC-UI-4).
- **Smoke (kullanıcı tarafından):** authenticated — bir CycleCapacity kaydı aç, between-visit alanını kur/kaydet,
  Details'te doğru göster; eski bir kaydı aç → default 5 görünür, `TotalVisitNumber` değişmemiş.

---

## 18. Ready-for-dev Checklist

- [ ] Golden Reference **Compact** referans olarak okundu (FU06/FU07 CycleCapacities canlı kod)
- [ ] Frontmatter tüm zorunlu alanlar dolu (`service`, `shell`, `golden_reference`, `entity_base`, `form_field_count: 10`)
- [ ] **D-COLLISION-RESOLVED = REUSE** kabul edildi: duplicate süre alanı YOK, tek gerçek kaynak FU06 kök alanları (§4.5)
- [ ] **D-PLACEMENT** (BetweenVisitTimeMinutes root) onaylandı (§4.4)
- [ ] **§9 yerleşim tercihi** (yeni `BetweenVisitSection` kartı vs mevcut `ActivityBudgetSection`'a ekleme) seçildi
- [ ] **F-CONTENT-PROMO-SPLIT** açık bağımlılığı kabul edildi: promo/non-promo sınıflandırma kaynağı bu FU'da YOK (§19)
- [ ] `ActivityTimeBudgetCalculator` **saf**, FU06 alanlarını **okur-mutate-etmez**, `CycleCapacityCalculator`'dan **bağımsız** (AC-ADD-1/2/3)
- [ ] Read-time `EnsureBetweenVisitTime` (tek skaler default); backfill YOK; kök alanlardan seed **gerekmiyor**
- [ ] Validation between-visit için yazılı (§12); kök alanlar dokunulmaz
- [ ] Failure Path ≥ 4 senaryo (§13)
- [ ] Authorization: yeni anahtar YOK, mevcut `crm.cycle-capacity.*` (§14)
- [ ] Gateway: gereksiz (§15)
- [ ] Acceptance criteria test edilebilir; section-map parite + duplicate-yok maddesi var (AC-UI-2/3, AC-ADD-3)
- [ ] **Additive garantisi:** FU06/FU07 shipped davranışı (kök alanlar + kapasite hesabı) DEĞİŞMEZ, testle kilitli

---

## 19. Implementation Notes / Decisions

- **D-COLLISION-RESOLVED = REUSE (Control-Tower, 2026-08-29).** İlk taslağın önerdiği üç yeni alan
  (`PromoProductTimeMinutes` / `NonPromoProductTimeMinutes` / `ReportDurationMinutes`) **kaldırıldı**. FU06'nın
  mevcut kök `PromoProductTime` / `NonPromoProductTime` / `ReportDuration` alanları **tek gerçek kaynaktır** ve
  hesap fonksiyonu onları okur. Çakışma **yönetilmez, kaldırılır** — duplike alan yok (§4.5).
- **D-NEW-FIELD = yalnız `BetweenVisitTimeMinutes`.** FU06'da `TravelingTime` var ama between-visit tamponu yok;
  eklenen tek genuinely-yeni alan budur (default 5). Süre formülüne girmez, FU05 paketlemede uygular.
- **D-PLACEMENT = ROOT.** Between-visit mevsimlik değil; FU07'nin "aya indir" argümanı transfer edilmez (§4.4).
  Skaler root alan (gömülü value object gereksiz — tek alan için container fazladan class-map/round-trip riski).
- **D-MIGRATION = read-time ensure, backfill YOK.** FU07 `EnsureMonthlyFte` emsali; tek skaler default; kök
  alanlar zaten dolu olduğundan **seed/kopya gerekmez**; `TotalVisitNumber` etkilenmez (§4.6).
- **D-DEFAULT = config, hardcoded değil.** BetweenVisitTimeMinutes = 5, `ICycleCapacityDefaultsProvider`
  üzerinden (Application katmanı `IConfiguration` taşımaz; FU06 S2 emsali).
- **D-UI = mevcut CycleCapacity Compact'ı genişlet.** Tek input; §9'da iki eşdeğer yerleşim (yeni kart / mevcut
  karta ekleme), ikisi de _Form≡Details paritesini korur.
- **D-NO-ENGINE.** Yalnız depolama + saf fonksiyon. Between-visit **saklanır ama uygulanmaz** (FU05);
  duration **hesaplanır ama saklanmaz** (FU04 tüketir).

### 19.1 ⚠️ AÇIK ITEM (flag — icat edilmedi) — downstream FU04 bağımlılığı

**F-CONTENT-PROMO-SPLIT — içerik item'inin promo/non-promo SINIFLANDIRMASININ kaynağı bu FU'da tanımlı DEĞİL.**
Duration formülü `#promoContentItems` ve `#nonPromoContentItems` sayılarına ihtiyaç duyar. Fonksiyon bu sayıları
**parametre** olarak alır ve saf kalır — **bir içerik item'inin "promo" mu "non-promo" mu olduğunu kim/nasıl
belirler**, LOCKED karar **yoktur**. Aday kaynaklar: MOD-0162 içerik bayrağı, MOD-0290 Brand/Product promotion
durumu, ya da StrategyTemplate SubjectList (SKU% promo) eşlemesi (bkz.
[[legacy-crmv2-ucln-subjectlist-forwhom-analysis]]). **Bu FU bunu SEÇMEZ**; tüketici FU04'ün çözeceği bağımlılık
olarak flag'lenir. Duration fonksiyonu bu karar verilmeden de doğrudur (sayıları parametre alır).

---

## 20. Follow-up Items

| ID | İş | Neden ertelendi |
|---|---|---|
| **F-CONTENT-PROMO-SPLIT** | İçerik item'inin promo/non-promo sınıflandırma kaynağı (§19.1) | LOCKED karar yok; FU04 tüketici sorumluluğu |
| **F-DURATION-CONSUME-FU04** | PlannedVisit `PlannedDurationMinutes`'i bu fonksiyon + content-ref'ten üretme | FU04 kapsamı |
| **F-PACKING-BETWEEN-FU05** | `BetweenVisitTimeMinutes` + süreleri paketlemede uygulama | FU05 motor kapsamı |
| **F-REGISTRY** | `module-implementation-status.md` satırı | Registry yazımı pack yetkisi dışı |
| **F-RBAC** | (gerekmez — yeni anahtar yok) | — |

---

## 21. Legacy reference (frozen — kod taşınmaz)

Legacy `CyclePeriodCalendar` (Campaign servisi) minute-budget'ları taşıyordu: `PromoProductTime`,
`NonPromoProductTime`, `BetweenVisitTime`, `TravelingTime`, `ReportDuration`. Bunların **duration'a ait üçü**
(`Promo`/`NonPromo`/`Report`) vNext'e **FU06'da zaten taşındı** (kök alanlar) — bu FU onları yeniden taşımaz,
**yeniden kullanır**. `TravelingTime` de FU06'da var (yol FU03'ün işi). Legacy'nin **tek eksik** bileşeni
`BetweenVisitTime`di; bu FU onu ekler. Kod/kolon/`OldSystem` coupling **taşınmaz**. İlgili:
[[legacy-visit-planning-analysis]], [[mod0155-fu06-cycle-capacity-pack]], [[mod0155-visit-route-planning-program]].
