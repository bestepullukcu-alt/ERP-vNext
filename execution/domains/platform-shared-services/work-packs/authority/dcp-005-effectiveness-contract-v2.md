# Ek v2 — Kontrollü Doküman Yürürlük Okuması: Port + HTTP Sözleşmesi

v1'e göre değişenler: **§1 join kararı** (varsayılan kaldırıldı, 0. adıma bağlandı,
IsSystemAllocated bulgusu), **§4 RBAC** (yeni anahtar = seed zorunluluğu), **§6 aktivasyon
sonrası kapsam dışı ilanı**, **§7 yeni: 0. adım — register tohumu**. Karşı ekibin üç ölçümü
işlendi.

Dayandığı ölçümler: `MasterRegisterEnums.cs:40` · `ControlledDocumentLifecyclePolicy.cs:16` ·
`IDocumentManagementMasterRegisterRepositories.cs:23,26` ·
`DocumentMasterRegisterEntry.cs:37-43` (PermanentUid/DocumentCode/LegacyCode +
`IsSystemAllocated`) · karşı taraf ölçümü: register bugün **0 kayıt**, TaskType UID tutuyor,
UID→kod eşlemesi yalnız CSV'de · AuthService DataSeeder: `master-register.view` **0 tohum**.

---

## 0. Tek resolver ilkesi (pazarlık dışı)

Port da, HTTP ucu da **tek bir uygulama-katmanı sorgusu** üstünde ince adaptör.

    ResolveDocumentEffectivenessQuery / Handler   ← tek doğruluk kaynağı
        ├── IControlledDocumentEffectivenessPort   (in-process, DI — /active kapısı)
        └── HTTP controller action                 (yalnız ekran — RBAC burada)

---

## 1. Join anahtarı — 0. ADIMDA SABİTLENİR (v2: varsayılan kaldırıldı)

Ölçülen gerçek: register bugün **boş** (0 kayıt), TaskType `UID-0000104` gibi UID'ler tutuyor,
ve bu UID'leri `DocumentCode`'a çeviren tek yer emekli edilecek CSV. Dolayısıyla asıl mesele
`by` parametresi değil, şu **uzlaştırma kararı** (0. adım):

- **(a) Register, CSV'nin UID'lerini sahiplenir** (PermanentUid = CSV uid) → UID join çalışır,
  CSV ölür. **← önerilen.**
- **(b) TaskType koda çevrilir** (tek seferlik göç, CSV üzerinden) → kod join çalışır, CSV
  göçten sonra ölür.
- (c) Karar verilmez, `by` sessizce ikisini de taşır → CSV kalıcı çevirmen olur. **← kaçınılan.**

**(a) neden hack değil:** `DocumentMasterRegisterEntry.IsSystemAllocated` yorumu:
*"false = manually entered in this FU; true = allocated by the FU07 engine."* Yani dış
register'ın UID'ini `PermanentUid`'e **elle** koymak modelin desteklediği bir köken —
`IsSystemAllocated=false`. FU07'nin ürettiği ERP-içi UID uzayıyla çakışmaz; "dıştan verilmiş
kimlik" olarak işaretlenir. (`LegacyCode` de CSV `document_code`'u için üçüncü yuva olarak
kullanılabilir — izlenebilirlik.)

**Sözleşme kuralı:** birincil anahtar `by` varsayılanıyla DEĞİL, **0. adımdaki (a)/(b)
kararıyla** sabitlenir. `by` mekanizma olarak ikisini de destekler; ama:

> **Sessiz varsayılan YOK.** Çağıran `by`'ı açıkça verir. (Varsayılan koyulacaksa `uid` olur —
> bugünkü tek gerçek çağıran Tasks ve elinde UID var; `code` varsayılanı tek çağıranı yanlış
> tarafa düşürür.)

---

## 2. Ayrık dönüş tipi (fail-closed tipe gömülü)

| Sonuç | Anlamı | Koşul |
|---|---|---|
| `Effective` | Yürürlükte | LifecycleStatus ∈ {Effective, UnderRevision} |
| `Blocked(reason)` | Defterde var, yürürlükte değil | Diğer 7 LifecycleStatus üyesi |
| `Unresolved` | Defterde yok | Kod/UID hiçbir satıra çözülmedi |

**`Unresolved` ≠ altyapı hatası.** İlki veri gerçeği ("böyle doküman yok"); register
erişilemez/zaman aşımı ise ayrı durum ("kontrol edemedim") — **fırlatılan hata**, sonuç değil.
İkisi de reddettirir; ama kullanıcıya ve denetime farklı cümle gider. Çevirmek yasak.

---

## 3. İç kapı — `IControlledDocumentEffectivenessPort`

```csharp
public interface IControlledDocumentEffectivenessPort
{
    Task<DocumentEffectivenessResult> ResolveAsync(
        DocumentEffectivenessQuery query, CancellationToken ct);
}

public sealed record DocumentEffectivenessQuery(
    IReadOnlyList<string> Identifiers,
    DocumentIdentifierKind By);           // sessiz varsayılan yok — §1

public enum DocumentIdentifierKind { Code, Uid }

public sealed record DocumentEffectivenessItem(
    string Identifier,
    DocumentEffectivenessState State,     // Effective | Blocked | Unresolved
    string? DocumentCode, string? PermanentUid,
    string? LifecycleStatus, string? BlockedReason);

public enum DocumentEffectivenessState { Effective, Blocked, Unresolved }
```

**Fail-closed (kapının kuralı):** etkinleştirmeye YALNIZCA her doküman `Effective` ise izin
verilir. Herhangi bir `Blocked`, `Unresolved` veya `ResolveAsync` istisnası → **RED**. İç kapı
RBAC eklemez — `/active` zaten `TaskTypesManage` altında; iç kapı bir **iş kuralı**.

---

## 4. HTTP ucu — yalnız ekran (v2: yeni anahtar = seed zorunluluğu)

```
POST /api/v1/document-management/document-master-register/effectiveness:batch
Authorize: platform.document-management.master-register.effectiveness.read
Body: { "by":"uid", "identifiers":["UID-0000104","UID-0000118"] }
200 data.items[]: { identifier, state, documentCode, permanentUid, lifecycleStatus, blockedReason }
```

- `Unresolved` HTTP hatası değil — 200 içinde dürüst döner. **400** yalnız bozuk istekte.
- ⚠ **Ölçülen RBAC gerçeği:** AuthService DataSeeder'da `master-register.view` = **0 tohum**
  (yazma anahtarları tohumlu, okuma değil). Pratik sonuç: yeni `…effectiveness.read` eklemek
  "sabit tanımlamak" değil — **seed'e girmek** demek. Aksi hâlde ekran ucu ilk günden herkese
  kapalı olur, sebebi görünmez. İki iş:
  1. Yeni anahtarı seed yoluna ekle (DataSeeder veya katalog sayfa/aksiyon kaydı — hangisi
     kanonikse; anahtar `/active` ekranını gören rollere, yani `TaskTypesManage` sahiplerine).
  2. `master-register.view`'in canlı 200 mü 403 mü olduğunu bir kez doğrula — anahtar katalog
     kaydından geliyor olabilir, ama DataSeeder'da yok.

---

## 5. Hata / reason kodları

| Yer | Kod | Ne zaman |
|---|---|---|
| Tasks `/active` | `task_type_enable_blocked_documents` | ≥1 doküman Effective değil; payload reddedenler. |
| Tasks `/active` | `task_type_enable_register_unavailable` | Port istisna attı — "kontrol edemedim" dalı. |
| HTTP uç | `400 invalid_request` | Boş/bozuk istek. |
| Port | (istisna) | Altyapı hatası `Unresolved`'a ÇEVRİLMEZ; fırlatılır, kapı reddeder. |

---

## 6. Sınırlar — sözleşmenin söylemedikleri (v2: aktivasyon sonrası)

- Port/uç **karar vermez**; durum + gerekçe döndürür.
- `TaskDocumentReference.Status` bu sözleşmenin dışında; Tasks alıntı anında bu handler'dan
  alıp dondurur. **Eski 2 satır olduğu gibi kalır; geçişte onlara dokunulmaz.**
- **Anlık çözümleme — süreklilik DEĞİL (açık karar, gözden kaçma değil):**
  > Bu sözleşme aktivasyon ANINDA çözümler. Aktivasyon sonrası bir dokümanın yürürlükten
  > çıkması (Superseded/Retired) bu sözleşmenin **kapsamı dışındadır**; sürekli gözetim ayrı
  > bir iştir (backlog). Yapıldığında doğru kanca, doküman-yönetimi tarafındaki
  > **lifecycle-transition olay akışıdır** (doküman Effective'den çıkınca olay yayınlanır,
  > tüketiciler tepki verir) — Tasks'ın periyodik sorması DEĞİL. Port anlık kalır.
- Bu bir **arama** ucu değil; verilen tanımlayıcıları çözer, register'da gezinmez.

---

## 7. 0. ADIM — register tohumu (v2 yeni; 1 ve 2'nin ÖN KOŞULU)

Register bugün boş. Testler gerçek veri olmadan boş nöbetçi olur. 1. adımdan önce:

- (a)/(b) kararı verilir (bkz §1) — **(a) öneriliyor.**
- Register, üç dokümanla, **üç FARKLI durumda** tohumlanır: `Effective` ·
  `ApprovedPendingEffective` · (hiç yok = `Unresolved`). Böylece port/uç testleri üç dalı da
  gerçekten kanıtlar.
- (a) seçilirse: bu tohum, CSV dokümanlarının register'a `PermanentUid = CSV uid`,
  `IsSystemAllocated=false`, `DocumentCode`, `LifecycleStatus` ile alınmasıdır — ve bu
  **doküman-yönetimi tarafının (benim) işidir**, CSV'nin yaşamaya devam etmesi değil.

⚠ Ölçülen bağımlılık: SoR (Master Register) şu an bu dokümanları **hiç içermiyor**. CSV'yi
tümüyle emekli etmek, register'ın bu dokümanları önce içermesini gerektirir — bu, 0. adımı
"a/b seç"ten daha ağır kılar ama (a) ile tek hamlede çözülür.
