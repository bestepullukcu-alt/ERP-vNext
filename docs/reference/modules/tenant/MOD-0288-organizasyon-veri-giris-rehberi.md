# Organizasyon Yapısı — Veri Giriş Rehberi ve Model Değerlendirme Dokümanı

**Amaç:** Sisteme şirket yapısının girilebilmesi için Legal Entity (Tüzel Kişilik), Organization Unit (Organizasyon Birimi), Position (Pozisyon) ve Position Assignment (Pozisyon Ataması) varlıklarının **bugün kodda gerçekten nasıl çalıştığını** eksiksiz tarif etmek.

**Kime:** Yöneticiye — "bizim şirket yapımızı bu modelle kurabilir miyiz?" kararını verebilmesi için.

**Kapsam kaynağı:** Bu doküman spesifikasyondan değil, çalışan koddan çıkarıldı.
- Legal Entity → `Diten.MdmService` (MOD-0220)
- Organization Unit / Position / Position Assignment → `Diten.Platform` (MOD-0288 v1)

**Tarih:** 2026-09-03 · **Durum:** MOD-0288 v1 (backend sözleşmesi tamam), MOD-0220 Faz 1

---

## 1. Model — dört katman

```
Legal Entity (Tüzel Kişilik)          MDM servisi, şirketin hukuki varlığı
        │  1 : N
        ▼
Organization Unit (Organizasyon Birimi)   Platform servisi, kendi içinde ağaç (parent → child)
        │  1 : N
        ▼
Position (Pozisyon)                    Platform servisi, kendi içinde raporlama ağacı (reports-to)
        │  1 : N (zaman aralıklı)
        ▼
Position Assignment (Atama)            Bir kullanıcıyı bir pozisyona, bir tarih aralığında bağlar
        │
        ▼
User (Sistem Kullanıcısı)              AuthService — kişi burada var olmak ZORUNDA
```

### Modelin taşıdığı temel tasarım kararları

| # | Karar | Sonucu |
|---|-------|--------|
| 1 | **Kişi değil pozisyon esas alınır.** Organizasyon şeması pozisyonlardan oluşur; kişiler pozisyonlara *atanır*. | Bir kişi ayrıldığında pozisyon ve raporlama yapısı bozulmaz; sadece atama sonlanır. |
| 2 | **Birim yöneticisi bir kişi değil, bir pozisyondur** (`ManagerPositionId`). | Yönetici değiştiğinde birim kaydına dokunulmaz, sadece o pozisyona yeni atama yapılır. |
| 3 | **Atamalar tarih aralıklıdır** (`EffectiveFrom` / `EffectiveTo`). | Geleceğe dönük atama girilebilir; geçmiş atamalar kayıtta kalır. |
| 4 | **Atama durumu saklanmaz, hesaplanır** (Planned / Active / Ended). | "Aktif" bir bayrağı elle güncellenmez; tarih geldiğinde kendiliğinden aktifleşir. |
| 5 | **Pozisyon doluluğu saklanmaz, hesaplanır** (`IsVacant`, `ActiveAssignmentCount`). | Boş kadro raporu her zaman gerçek atamalarla tutarlıdır. |
| 6 | **Bir pozisyonda aynı anda tek bir Primary (asıl) atama olabilir.** Secondary / Acting / Delegated üst üste binebilir. | Vekâlet ve ikincil görev modellenebilir, çift asıl kadro engellenir. |
| 7 | **Pozisyon RBAC rolü değildir.** `JobTitle` bir İK etiketi; yetki sistemi ayrıdır. | Yetkiler bu yapıdan otomatik türemez (bkz. Bölüm 8, sınır #9). |

---

## 2. Legal Entity (Tüzel Kişilik) — MOD-0220

**Ekran:** `/LegalEntities` — liste + 6 adımlı sihirbaz (Wizard) + detay sayfası
**Yetkiler:** `mdm.legal-entities.read` / `.create` / `.update` / `.delete` (+ yaşam döngüsü aksiyonları)

Sihirbaz adımları: **1) Kimlik → 2) Yasal & Vergi → 3) Yapı → 4) Finans → 5) Adres & İletişim → 6) Özet**
Adımlar ileriye doğru kilitlidir: bir adımdaki zorunlu alanlar dolmadan sonraki adıma geçilemez.

### 2.1 Alan tablosu

Z = Zorunlu · O = Opsiyonel

| Bölüm | Alan | Z/O | Tip / Sınır | İzin verilen değerler | Not |
|---|---|---|---|---|---|
| **1 Kimlik** | Code (Kod) | **Z** | metin, ≤64 | serbest | **Tenant genelinde benzersiz.** Sadece baştaki/sondaki boşluk temizlenir — **büyük/küçük harf ve tire dönüşümü YOK**, yazdığınız gibi saklanır. |
| | LegalName (Yasal Unvan) | **Z** | metin, ≤256 | serbest | Ticaret sicilindeki tam unvan. |
| | DisplayName (Görünen Ad) | O | metin, ≤256 | serbest | Ekranlarda kullanılan kısa ad. |
| | LegalFormCode (Hukuki Form) | **Z** | seçim listesi | `CORPORATION` (A.Ş./Corporation), `LLC` (Ltd. Şti.), `PARTNERSHIP` (Ortaklık), `SOLEPROP` (Şahıs), `BRANCH` (Şube), `REPOFFICE` (İrtibat Bürosu) | Liste **kodda sabit**; ekrandan yönetilemez. |
| **2 Yasal & Vergi** | RegistrationNumber (Sicil No) | O | metin, ≤128 | serbest | Format doğrulaması yok. |
| | TaxId (Vergi No) | O | metin, ≤128 | serbest | Format doğrulaması yok. |
| | VatNumber (KDV/VAT No) | O | metin, ≤64 | serbest | TaxId'den ayrı alan (AB VAT ID gibi). VIES doğrulaması yok. |
| | PlaceOfIncorporation (Kuruluş Yeri) | O | metin, ≤256 | serbest | Ülkeden farklı olabilir. |
| | IncorporationDate (Kuruluş Tarihi) | O | tarih | — | Kısıt yok. |
| | DissolutionDate (Fesih Tarihi) | O | tarih | — | Kuruluş tarihinden önce olamayacağına dair **kontrol yok**. |
| | CountryCode (Ülke) | **Z** | seçim, ≤3 | Sabit ülke listesi, ISO 3166-1 alpha-2 (TR, DE, US…). TR listenin başında. | Platform lookup'ından gelir. |
| | StatutoryStatus (Yasal Statü) | O (vars. `Registered`) | seçim | `Registered` (Tescilli), `Pending` (Beklemede), `Suspended` (Askıda), `Dissolved` (Feshedilmiş) | Operasyonel durumdan **bağımsız** bir alandır. |
| | BaseCurrencyCode (Ana Para Birimi) | **Z** | seçim, ≤3 | ISO 4217 (TRY, EUR, USD…) | Platform lookup'ından gelir. |
| **3 Yapı** | ParentLegalEntityId (Üst Tüzel Kişilik) | Koşullu | referans | Mevcut bir Legal Entity | **Rol BRANCH veya REPOFFICE ise zorunlu.** Girildiğinde kaydın var olduğu kontrol edilir. |
| | OrganizationRoleCode (Organizasyon Rolü) | O (vars. `LEGALENTITY`) | seçim | `LEGALENTITY`, `BRANCH`, `REPOFFICE`, `DIVISION` | Sihirbazda **şu an gösterilmiyor**; boş bırakılırsa `LEGALENTITY` atanır. |
| | OwnershipPercent (Sahiplik %) | O | sayı | 0 – 100 | Kardeş şirketlerin toplamının %100 olduğu **kontrol edilmez**. |
| | ControlTypeCode (Kontrol Tipi) | O | seçim | `SUBSIDIARY` (Bağlı Ortaklık), `ASSOCIATE` (İştirak), `JOINTVENTURE` (Ortak Girişim), `BRANCH` (Şube) | Kodda sabit liste. |
| **4 Finans** | FiscalYearVariant (Mali Yıl) | O | metin, ≤64 | serbest | Sabit liste yok, serbest metin. |
| | AccountingStandardCode | O | seçim | `IFRS`, `USGAAP`, `LOCALGAAP` | |
| | TaxRegimeCode (Vergi Rejimi) | O | seçim | `STANDARD`, `SIMPLIFIED`, `EXEMPT`, `GROUP` | |
| **5 Adres & İletişim** | RegisteredAddress (Tescilli Adres) | O | alt form | `line1`, `city`, `state`, `postalCode`, `country` | Girilirse **line1 + city + country zorunlu** olur; hiç girilmezse kaydedilmez. |
| | CorrespondenceAddress (Yazışma Adresi) | O | alt form | aynı alanlar | Serbest. |
| | OfficialEmail | O | e-posta | geçerli e-posta formatı | |
| | OfficialPhone | O | metin, ≤64 | serbest | Format doğrulaması yok. |
| | Website | O | metin, ≤256 | serbest | |
| **6 Yönetişim** | OperationalStatus (Operasyonel Durum) | sistem | — | `Draft`, `InReview`, `Approved`, `Active`, `Suspended`, `Archived` | Formdan **değiştirilemez**; yalnızca aksiyon butonlarıyla değişir (bkz. 2.2). Yeni kayıt daima `Draft`. |
| | ApprovalStatus (Onay Durumu) | O (vars. `Draft`) | seçim | `Draft`, `Submitted`, `Approved`, `Rejected` | Faz 1'de **sadece etikettir**, hiçbir şeyi kilitlemez. |
| | ReviewDueUtc (Gözden Geçirme Tarihi) | O | tarih | — | Hatırlatma/otomasyon bağlı **değil**. |
| | SourceSystem (Kaynak Sistem) | O | metin, ≤128 | serbest | Göç (migration) izlenebilirliği için. |
| | LegacyCode (Eski Kod) | O | metin, ≤128 | serbest | Eski sistemdeki kod. |
| **7 Kanıt** | EvidenceStatus | O (vars. `NotStarted`) | seçim | `NotStarted`, `Complete`, `Verified` | Faz 1'de **sadece etiket**; hiçbir geçişi engellemez. |
| | CompletenessScore (Tamlık %) | sistem | 0–100 | — | Sunucu hesaplar: Code, LegalName, LegalFormCode, OrganizationRoleCode, CountryCode, BaseCurrencyCode, RegisteredAddress (+ rol gerektiriyorsa Parent) dolu mu? |

> **Veri toplarken kritik nokta:** Sihirbazın zorunlu tuttuğu alanlar sadece **Code, LegalName, LegalFormCode, CountryCode, BaseCurrencyCode**'dur. Diğer her şey sonradan tamamlanabilir. Yani eksik veriyle bile kayıt açılabilir — tamlık `CompletenessScore` ile takip edilir.

### 2.2 Yaşam döngüsü (kim ne zaman referans alınabilir)

```
Yeni kayıt ──► Draft
                 │  "Aktifleştir"
                 ▼
              Active ◄──────────┐
               │  │             │ "Aktifleştir"
   "Askıya al" │  │ "Arşivle"   │
               ▼  ▼             │
          Suspended ─────► Archived
               └───────────────►┘  ("Aktifleştir" ile geri alınabilir)
```

Kurallar (kodda birebir):
- **Aktifleştir:** Active dışındaki **her** durumdan çalışır (Draft, InReview, Approved, Suspended, Archived). Zaten Active ise hata (409).
- **Askıya al:** Yalnızca Active'ten.
- **Arşivle:** Yalnızca Active veya Suspended'tan.
- **Sil:** Yumuşak silme (kayıt fiziken durur, listede görünmez).
- ⚠️ **Aktifleştirme için hiçbir ön koşul yok:** onay, kanıt veya tamlık şartı **aranmaz**. (Kodda "MOD-0023 ile sonra eklenecek" notu var.)

**Neden önemli:** Bir Organizasyon Birimi ancak **`Active` durumdaki** bir Legal Entity'ye bağlanabilir. Draft bir tüzel kişilik seçilemez ("Legal Entity is not referenceable" hatası).

---

## 3. Organization Unit (Organizasyon Birimi) — MOD-0288

**Ekran:** `/OrganizationUnits` — liste + form + detay
**Yetkiler:** `platform.organization-units.read` / `.create` / `.update` / `.archive` / `.delete`

| Alan | Z/O | Tip / Sınır | Değerler | Kural |
|---|---|---|---|---|
| Code (Kod) | **Z** | metin, ≤80 | serbest | **Otomatik normalize edilir:** büyük harfe çevrilir, harf/rakam dışındaki her karakter tek bir `-` olur, baştaki/sondaki tireler atılır. `"Satış Bölümü 1"` → `SATI-BÖLÜMÜ-1`. **Tenant genelinde benzersiz** (tüzel kişilik bazında değil — tüm şirket grubunda tek). |
| Name (Ad) | **Z** | metin, ≤160 | serbest | |
| LegalEntityId (Tüzel Kişilik) | **Z** | referans | Aktif bir Legal Entity | Her birim bir tüzel kişiliğe aittir. |
| ParentOrganizationUnitId (Üst Birim) | O | referans | Mevcut, arşivlenmemiş birim | **Üst birim aynı Legal Entity'de olmak zorunda.** Farklı tüzel kişiliğe bağlanamaz (409). Kendisinin üstü olamaz. Döngü engellenir. **Maksimum zincir derinliği 32.** |
| OrgUnitType (Birim Tipi) | O (vars. `Department`) | seçim | `HQ` (Merkez), `Division` (Bölüm/Divizyon), `Department` (Departman), `Branch` (Şube), `Team` (Ekip) | Liste **kodda sabit** — yeni tip eklemek geliştirme gerektirir. Tipler arasında hiyerarşi kuralı **yok** (bir Team'in altına Division bağlanabilir). |
| ManagerPositionId (Yönetici Pozisyonu) | O | referans | Bir Position | ⚠️ Girilen pozisyonun **var olduğu doğrulanmaz** ve o birime ait olma şartı yoktur. |
| Description (Açıklama) | O | metin, ≤1024 | serbest | |
| Status (Durum) | O (vars. `Active`) | seçim | `Active`, `Inactive` | Sadece etiket; `Inactive` bir birime pozisyon bağlanmasını **engellemez**. |
| EffectiveFrom / EffectiveTo | O | tarih | — | Basit geçerlilik tarihleri. **Tarihsel versiyonlama değildir** — geçmiş bir tarihteki organizasyon şeması sorgulanamaz. Sıra kontrolü (From < To) **yoktur**. |
| LocationCode (Lokasyon) | — | metin | — | Alan var, **ekranda yok**. İleride lokasyon entegrasyonu için ayrılmış. |
| CostCenterCode (Masraf Merkezi) | — | metin | — | Alan var, **ekranda yok**. |

**Arşivleme / silme:**
- Arşivlenen birim güncellenemez (409), üst birim olarak seçilemez.
- ⚠️ **Alt birimi veya pozisyonu olan bir birim arşivlenebilir ve silinebilir** — bloklayan bir kontrol yok. Alt kayıtlar "öksüz" kalır.

---

## 4. Position (Pozisyon) — MOD-0288

**Ekran:** `/Positions` — liste + form + detay + yönetici zinciri
**Yetkiler:** `platform.positions.read` / `.create` / `.update` / `.archive` / `.delete` · `platform.organization.read-manager-chain`

| Alan | Z/O | Tip / Sınır | Değerler | Kural |
|---|---|---|---|---|
| Code (Kod) | **Z** | metin, ≤80 | serbest | Birim kodu ile aynı normalizasyon. **Tenant genelinde benzersiz.** |
| Name (Ad) | **Z** | metin, ≤160 | serbest | Kadro adı (ör. "Satış Müdürü"). |
| OrganizationUnitId (Birim) | **Z** | referans | Arşivlenmemiş bir birim | Her pozisyon tam olarak bir birime aittir. |
| ReportsToPositionId (Bağlı Olduğu Pozisyon) | O | referans | Arşivlenmemiş bir pozisyon | Kendine raporlayamaz; döngü engellenir; **maks. derinlik 32**. ⚠️ **Farklı birimdeki/tüzel kişilikteki bir pozisyona raporlayabilir** — kısıt yok (matris yapıya izin verir). |
| JobTitle (Görev Unvanı) | O | metin, ≤160 | serbest | İK unvan etiketi. **RBAC rolü değildir.** |
| PositionType (Pozisyon Tipi) | O (vars. `Permanent`) | seçim | `Permanent` (Kadrolu), `Temporary` (Geçici), `Contractor` (Sözleşmeli/Taşeron), `Intern` (Stajyer) | Kodda sabit liste. |
| Fte (Kadro Oranı) | O | ondalık ≥ 0 | ör. 1.0 · 0.5 | Yarı zamanlı kadro. ⚠️ Atamaların `AllocationPercent` toplamı ile **karşılaştırılmaz**. |
| Status (Durum) | O (vars. **`Draft`**) | seçim | `Draft` (Taslak), `Active` (Aktif), `Frozen` (Donduruldu), `Closed` (Kapatıldı) | ⚠️ Varsayılan `Draft`'tır — formda değiştirilmezse pozisyon taslak kalır. ⚠️ **`Draft`/`Frozen`/`Closed` bir pozisyona atama yapılmasını engellemez.** |
| EffectiveFrom / EffectiveTo | O | tarih | — | Bilgi amaçlı; sıra kontrolü yok. |
| LocationCode / CostCenterCode / GradeCode | — | metin | — | Alanlar var, **ekranda yok** (ileriye dönük). |
| **IsVacant (Boş mu)** | hesaplanan | evet/hayır | — | Bugün aktif ataması yoksa `true`. Saklanmaz. |
| **ActiveAssignmentCount** | hesaplanan | sayı | — | Bugün aktif atama adedi. Saklanmaz. |

**Yönetici zinciri (Manager Chain):** Bir pozisyondan başlayıp `ReportsToPositionId` üzerinden yukarı doğru zinciri (pozisyon kodu, adı, derinlik) döndüren ayrı bir servis vardır — onay akışlarının "bir üst amir kim?" sorusunu buradan cevaplayacağı nokta budur.

⚠️ **Arşivleme:** Aktif ataması olan bir pozisyon arşivlenebilir/silinebilir; engelleyen kontrol yoktur.

---

## 5. Position Assignment (Pozisyon Ataması) — MOD-0288

**Ekran:** `/PositionAssignments` — liste + form + detay
**Yetkiler:** `platform.position-assignments.read` / `.create` / `.update` / `.delete`

| Alan | Z/O | Tip | Değerler | Kural |
|---|---|---|---|---|
| PositionId (Pozisyon) | **Z** | referans | Arşivlenmemiş bir pozisyon | Listeden seçilir. |
| UserId (Kullanıcı) | **Z** | referans | **Tenant'ta var olan ve aktif bir sistem kullanıcısı** | ⚠️ Kişi listesi AuthService kullanıcılarından gelir — **sistem hesabı olmayan bir çalışan atanamaz.** Doğrulama başarısız olursa 404 ("User is not referenceable"). Liste ekranda ilk **200** kullanıcı ile sınırlıdır. |
| EffectiveFrom (Başlangıç) | **Z** | tarih | — | Geçmiş veya gelecek olabilir. |
| EffectiveTo (Bitiş) | O | tarih | — | Boş = süresiz. Doluysa **başlangıçtan büyük olmak zorunda**. |
| AssignmentType (Atama Tipi) | O (vars. `Primary`) | seçim | `Primary` (Asıl), `Secondary` (İkincil), `Acting` (Vekâleten), `Delegated` (Devredilmiş) | Kodda sabit liste. |
| AllocationPercent (Tahsis %) | O | 0 – 100 | — | ⚠️ Bir kişinin farklı pozisyonlardaki toplamının %100'ü aşıp aşmadığı **kontrol edilmez**. |
| Reason (Gerekçe) | O (vars. `Hire`) | seçim | `Hire` (İşe alım), `Transfer` (Nakil), `Promotion` (Terfi), `Backfill` (Yerine atama) | Kodda sabit liste. |
| Notes (Not) | O | metin, ≤1024 | serbest | |
| IsCancelled (İptal) | O | evet/hayır | — | İptal edilen atama çakışma hesabına girmez ve durumu `Ended` sayılır. |
| **DerivedStatus (Durum)** | hesaplanan | — | `Planned` / `Active` / `Ended` | İptalliyse `Ended`; başlangıç gelecekteyse `Planned`; bitiş geçmişse `Ended`; aksi halde `Active`. |

### 5.1 Tek asıl atama kuralı (en önemli iş kuralı)

> Aynı pozisyonda, **zaman aralıkları çakışan iki `Primary` atama olamaz** (409 hatası).
> `Secondary`, `Acting` ve `Delegated` atamalar hem birbirleriyle hem de bir `Primary` ile serbestçe çakışabilir.
> İptal edilmiş (`IsCancelled`) ve silinmiş atamalar çakışma sayılmaz.
> Aralıklar yarı-açık `[başlangıç, bitiş)` olarak yorumlanır: 31.12'de biten bir atamayla 31.12'de başlayan bir atama çakışmaz.

**Pratik sonucu:** Devir teslim doğal olarak modellenir — ayrılan kişinin atamasına bitiş tarihi verilir, yeni kişiye o tarihten itibaren `Primary` atama açılır. Vekâlet için `Acting` kullanılır, asıl kadroyu kapatmaya gerek kalmaz.

⚠️ Aynı kişinin **aynı pozisyona** iki kez atanmasını engelleyen ayrı bir kural **yoktur** (tip farklıysa mümkün).

---

## 6. Veri giriş sırası (uygulanması gereken sıra)

Bağımlılıklar zorunlu bir sıra dayatır:

| Adım | Ne girilir | Ön koşul |
|---|---|---|
| **1** | **Legal Entity** kayıtları (ana şirket → bağlı ortaklıklar → şubeler) | Şube/irtibat bürosu girmeden önce üst tüzel kişilik girilmiş olmalı |
| **2** | Her Legal Entity'yi **Aktifleştir** | Aktif olmayan tüzel kişiliğe birim bağlanamaz |
| **3** | **Organization Unit** ağacı — **üstten alta** (önce merkez/HQ, sonra alt birimler) | Üst birim ve tüzel kişilik hazır olmalı; üst birim **aynı** tüzel kişilikte olmalı |
| **4** | **Position** kayıtları — **raporlama hiyerarşisinde üstten alta** | Bağlı olunan pozisyon önce açılmış olmalı |
| **5** | Birimlerin **ManagerPositionId** alanını doldur (geri dönüp güncelleme) | Yönetici pozisyonları 4. adımda oluşmuş olur |
| **6** | Atanacak kişiler için **sistem kullanıcısı** oluştur | Kullanıcı yoksa atama yapılamaz |
| **7** | **Position Assignment** kayıtları | Pozisyon + kullanıcı hazır olmalı |

> 3. ve 4. adım için **iki turlu giriş** gerekir: birimleri açarken yönetici pozisyonu henüz yoktur, pozisyonları açarken de birim gerekir. Doğru yol: önce birimleri yöneticisiz aç → pozisyonları aç → birimlere dönüp yönetici pozisyonunu ata.

---

## 7. Veri toplama şablonu (yöneticiye verilecek liste)

Aşağıdaki dört tablo, sisteme girilecek verinin toplanması için birebir kullanılabilir. **Zorunlu** sütunlar boş kalırsa kayıt açılamaz.

### Tablo A — Legal Entity
| Kod* | Yasal Unvan* | Görünen Ad | Hukuki Form* | Ülke* | Ana Para Birimi* | Sicil No | Vergi No | VAT No | Kuruluş Yeri | Kuruluş Tarihi | Yasal Statü | Üst Tüzel Kişilik (Kod) | Sahiplik % | Kontrol Tipi | Mali Yıl | Muhasebe Std. | Vergi Rejimi | Tescilli Adres (Sokak / Şehir / Ülke) | E-posta | Telefon | Web |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|

### Tablo B — Organization Unit
| Kod* | Ad* | Tüzel Kişilik (Kod)* | Üst Birim (Kod) | Birim Tipi | Yönetici Pozisyonu (Kod) | Açıklama | Durum | Geçerlilik Başlangıç | Geçerlilik Bitiş |
|---|---|---|---|---|---|---|---|---|---|

### Tablo C — Position
| Kod* | Ad* | Birim (Kod)* | Bağlı Olduğu Pozisyon (Kod) | Görev Unvanı | Pozisyon Tipi | FTE | Durum | Geçerlilik Başlangıç | Geçerlilik Bitiş |
|---|---|---|---|---|---|---|---|---|---|

### Tablo D — Position Assignment
| Pozisyon (Kod)* | Kişi (e-posta / kullanıcı adı)* | Başlangıç* | Bitiş | Atama Tipi | Tahsis % | Gerekçe | Not |
|---|---|---|---|---|---|---|---|

**Kod verirken dikkat:**
- Organizasyon Birimi ve Pozisyon kodları **büyük harfe çevrilir ve boşluk/özel karakterler tireye dönüşür**. `Satış Md.` ve `SATIS-MD` çakışabilir. Kodları baştan `BÜYÜK-HARFLİ-TİRELİ` yazmak sürprizi önler.
- Kod benzersizliği **tüm tenant genelindedir** (tüzel kişilik başına değil). İki şirkette de "Muhasebe" departmanı varsa kodlar farklı olmalı: `ACME-MUHASEBE`, `BETA-MUHASEBE`.
- Legal Entity kodu normalize **edilmez** — yazıldığı gibi kalır, bu yüzden yazım disiplini elle sağlanmalıdır.

---

## 8. Modelin bugünkü sınırları — yöneticinin karar vermesi gereken noktalar

Bunlar hata değil, **v1 kapsam kararlarıdır**. Şirket yapısı bunlardan birine ihtiyaç duyuyorsa ek geliştirme gerekir.

| # | Sınır | İş üzerindeki etkisi | Risk |
|---|---|---|---|
| 1 | **Toplu veri aktarımı (Excel/CSV import) yok** | Tüm veri ekranlardan tek tek girilir. | Yüzlerce pozisyon varsa ciddi emek. Büyük hacim varsa import geliştirmesi konuşulmalı. |
| 2 | **Sayfalama yok** | Birim/pozisyon/atama listeleri tek seferde tümüyle çekilir. | Birkaç bin kaydın üzerinde performans sorunu beklenir. |
| 3 | **Tarihsel versiyonlama yok** | `EffectiveFrom/To` sadece bilgi alanıdır; "1 Ocak'taki organizasyon şeması neydi?" sorusu cevaplanamaz. | Yalnızca **atamalar** zaman boyutludur; birim ve pozisyon değişimleri geçmişe dönük izlenmez. |
| 4 | **Bir pozisyon tek birime bağlıdır** | Klasik matris (bir pozisyonun iki birime birden ait olması) desteklenmez. | Matris, `ReportsToPositionId`'nin başka birimdeki bir pozisyonu gösterebilmesiyle **kısmen** kurulabilir; ikinci bir "dotted-line" alanı yoktur. |
| 5 | **Silme/arşivleme koruması yok** | Alt birimi olan birim, aktif ataması olan pozisyon silinebilir/arşivlenebilir. | Yanlış silme sessizce öksüz kayıt bırakır. Yetkiyi dar tutmak gerekir. |
| 6 | **Yönetici pozisyonu doğrulanmıyor** | Birime, var olmayan veya başka birimdeki bir pozisyon yönetici olarak yazılabilir. | Veri kalitesi elle sağlanmalı. |
| 7 | **FTE ve tahsis yüzdesi denetlenmiyor** | Bir kişi 5 pozisyonda %100 tahsisle görünebilir; bir pozisyonun FTE'si ile atamalarının toplamı karşılaştırılmaz. | Kapasite planlaması bu veriye güvenemez. |
| 8 | **Pozisyon durumu atamayı kilitlemiyor** | `Closed` (kapatılmış) bir pozisyona atama yapılabilir. | Süreç disiplini elle sağlanmalı. |
| 9 | **Organizasyon yapısı yetkiyi belirlemiyor** | Pozisyon ≠ RBAC rolü. Kullanıcı yetkileri ayrı yönetilir. | "Satış Müdürü olan otomatik şu ekranları görür" davranışı **yok**; yetkiler ayrıca verilir. |
| 10 | **Atama = sistem kullanıcısı** | Sistem hesabı olmayan çalışan (saha personeli, mavi yaka) organizasyon şemasına konulamaz. | Tam kadro şeması isteniyorsa herkes için kullanıcı açmak gerekir. `PersonReference` diye ayrı bir "kişi" kaydı altyapıda var ama **atamalarda kullanılmıyor.** |
| 11 | **Legal Entity aktivasyonu denetimsiz** | Onay/kanıt şartı olmadan herkes (yetkisi varsa) aktifleştirebilir. | İleride MOD-0023 iş akışıyla kapatılması planlanmış. |
| 12 | **Seçim listeleri kodda sabit** | Birim tipleri, pozisyon tipleri, atama tipleri, hukuki formlar ekrandan yönetilemez. | Şirketin farklı bir terminolojisi varsa geliştirme gerekir (etiketler 7 dilde çevrilebilir; **kod listesine yeni değer eklemek** geliştirmedir). |
| 13 | **Hiyerarşi derinliği en fazla 32** | Hem birim ağacı hem raporlama zinciri için. | Pratikte sorun değil. |
| 14 | **Birim tipleri arasında hiyerarşi kuralı yok** | Bir "Ekip"in altına "Divizyon" bağlanabilir. | Tutarlılık elle sağlanmalı. |

---

## 9. Yöneticinin cevaplaması gereken sorular

Bu sorular cevaplanınca veri girişi tek seferde ve doğru yapılabilir:

**Tüzel yapı**
1. Kaç ayrı tüzel kişilik var? Aralarındaki sahiplik/kontrol ilişkisi nedir (yüzdeleriyle)?
2. Şube veya irtibat bürosu var mı? (Varsa üst tüzel kişiliği zorunlu.)
3. Her tüzel kişiliğin ana para birimi ve ülkesi nedir?
4. Tüzel kişilik kodlama standardı ne olacak?

**Organizasyon**
5. Birim ağacı kaç seviye? Seviyeler hangi tipe karşılık geliyor (HQ / Divizyon / Departman / Ekip)?
6. Birim kodlama standardı ne olacak? (Kodlar tenant genelinde benzersiz olmak zorunda.)
7. Bir birim birden fazla tüzel kişiliğe hizmet veriyor mu? **Veriyorsa mevcut model bunu desteklemez** — tüzel kişilik başına ayrı birim açılması gerekir.

**Pozisyonlar**
8. Kadro bazlı mı çalışılacak (her çalışan için ayrı pozisyon) yoksa çoklu kadro mu (bir "Satış Temsilcisi" pozisyonuna 10 kişi)? — Model ikisini de kaldırır, ama çoklu kadroda **tek `Primary` kuralı** nedeniyle 10 kişiden 9'unun `Secondary` olması gerekir. **Öneri: her çalışan için ayrı pozisyon.**
9. Raporlama şeması saf ağaç mı, matris mi? Matris varsa ikincil raporlama nasıl kaydedilecek?

**Kişiler**
10. Organizasyon şemasında görünecek herkesin sistem kullanıcısı olacak mı? Olmayacaksa şema eksik kalacağı kabul ediliyor mu?
11. Geçmiş atamalar (işten ayrılanlar, eski görevler) da girilecek mi, yoksa sadece bugünkü durum mu?

**Hacim**
12. Toplam kaç birim, kaç pozisyon, kaç atama girilecek? (Birkaç yüzü aşıyorsa toplu aktarım geliştirmesi gündeme alınmalı — bkz. sınır #1.)

---

## 10. Özet değerlendirme

**Model neyi iyi yapıyor:** Pozisyon-merkezli, tarih aralıklı, çok-tüzel-kişilikli, çok-kiracılı bir organizasyon modeli. Vekâlet ve ikincil görev doğal olarak modellenebiliyor. Doluluk/durum hesaplanan alanlar olduğu için veri kendi kendine tutarlı kalıyor. Döngü ve referans bütünlüğü kontrolleri yerinde. Kurumsal bir organizasyon şeması için doğru iskelet.

**Model neyi henüz yapmıyor:** Toplu aktarım, tarihsel şema sorgulama, kapasite/FTE denetimi, silme koruması, organizasyondan otomatik yetki türetme, sistem kullanıcısı olmayan çalışanların şemada yer alması.

**Karar noktası yöneticide:** Yukarıdaki 14 sınırın hangileri şirket yapısı için kabul edilebilir, hangileri geliştirme gerektiriyor.
