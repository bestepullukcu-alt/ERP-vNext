# DCP-005 — "Adım 1 HAZIR" + Görev Merkezi'nden beklenen karar (G1)

**Kimden:** Doküman-Yönetimi tarafı (MOD-0029)
**Kime:** Görev Merkezi tarafı (MOD-0024)
**Tarih:** 2026-09-04
**Konu:** Kontrollü doküman yürürlük okuması — bizim taraf (Adım 0 tasarımı + Adım 1 kod) tamam; senden **G1 (a/b join)** kararı + Adım 2–3 için hazır bilgi.

---

## 1. Özet

DCP-005'in **doküman-yönetimi tarafı (Adım 1)** kod olarak **tamamlandı ve bağımsız doğrulandı** (build + testler + canlı 200). Görev Merkezi artık bir görev türünü etkinleştirirken, onu yöneten kontrollü dokümanların **canlı yürürlük durumunu** (CSV kopyası değil, Document Master Register) tek bir çözümleyiciden sorabilir.

Senden gereken **tek karar G1 (aşağıda §4)** — ve bu **kodu bloklamıyor**, yalnız register'a gerçek veri girişini (Adım 0) ve senin çağrıda `by` değerini belirliyor.

---

## 2. Sana ne sunuyoruz (sözleşme)

Tek çözümleyici, iki tüketim yolu:

### (A) In-process PORT — `/active` kapısı için (önerilen, aynı assembly)
Tasks ile aynı assembly'de (`Diten.Platform.Application`), düz DI:

```
IControlledDocumentEffectivenessPort.ResolveAsync(
    new DocumentEffectivenessQuery(identifiers, by),   // by: DocumentIdentifierKind.Uid | Code — sessiz varsayılan YOK
    ct)
→ DocumentEffectivenessResult { IReadOnlyList<DocumentEffectivenessItem> Items }
```
Namespace: `...Features.DocumentManagementMasterRegister.Services`. HTTP'ye gerek yok — doğrudan referansla.

### (B) HTTP uç — ekran için
```
POST /api/v1/document-management/document-master-register/effectiveness:batch
Authorization: Bearer <token>   ·   X-Tenant-Id: <tenant>
İzin: platform.document-management.master-register.effectiveness.read
Body: { "by": "uid" | "code", "identifiers": ["UID-0000104", ...] }
```
(Gateway wildcard rotası zaten iletiyor — ek ocelot işi yok.)

### Dönüş — ayrık üç durum
| state (JSON int) | Anlam | Koşul |
|---|---|---|
| `0` Effective | Yürürlükte, kullanılabilir | LifecycleStatus ∈ {Effective, UnderRevision} |
| `1` Blocked | Defterde var, yürürlükte değil (`blockedReason` = durum adı) | Diğer 7 LifecycleStatus üyesi |
| `2` Unresolved | Defterde yok | Kimlik hiçbir satıra çözülmedi |

> ⚠️ `state` JSON'da **sayı** olarak seri hale geliyor (0/1/2) — enum adı değil. Eşlemende buna dikkat.

### Kritik davranışlar (senin `/active` mantığın buna dayanmalı)
- **Fail-closed:** etkinleştirmeye YALNIZCA **her doküman Effective** ise izin ver. Herhangi biri `Blocked` / `Unresolved` → **RED**.
- **`Unresolved` ≠ altyapı hatası.** Register erişilemezse çözümleyici **istisna FIRLATIR** (Unresolved'a çevirmez). Bu ikisine farklı reddetme cümlesi ver:
  - ≥1 doküman Effective değil → `task_type_enable_blocked_documents`
  - Port istisna attı ("kontrol edemedim") → `task_type_enable_register_unavailable`
- **Reddetme kararı sende** (Tasks); çözümleyici yalnız durum + gerekçe döndürür.

---

## 3. Şu an ne çalışıyor, ne eksik
- Uç canlı **200** dönüyor (doğrulandı). Ama register **bugün boş** → her kimlik şu an **Unresolved** döner. Gerçek Effective/Blocked görebilmek için register'ın o dokümanları içermesi gerekir → **Adım 0** (bizim iş) — ama **G1'e bağlı** (§4).

---

## 4. ⭐ SENDEN GEREKEN KARAR — G1 (a/b join)

Ölçülen gerçek: register boş; TaskType `UID-0000104` gibi **UID**'ler tutuyor; bu UID'leri koda çeviren tek yer **emekli edilecek CSV**. Soru: register ile TaskType hangi anahtardan eşleşecek?

| Seçenek | Ne olur | Sonuç |
|---|---|---|
| **(a) Register CSV UID'lerini sahiplenir** (`PermanentUid = CSV uid`, `IsSystemAllocated=false`) | **UID join** çalışır; çağrıda `by="uid"`; CSV ölür | **← ÖNERİMİZ** (tek hamle, bizim Adım 0 işimiz) |
| (b) TaskType koda çevrilir (tek seferlik göç, CSV üzerinden) | **Code join** çalışır; çağrıda `by="code"`; CSV göçten sonra ölür | Daha çok senin tarafında göç işi |
| (c) Karar verilmez | `by` ikisini de taşır, CSV kalıcı çevirmen olur | **← kaçınılan** |

**(a) neden temiz:** `DocumentMasterRegisterEntry.IsSystemAllocated=false` = "bu FU'da elle girilmiş" — dış register UID'ini elle koymak modelin desteklediği köken; FU07'nin ürettiği UID uzayıyla çakışmaz.

**Etkisi dar:** G1 yalnız (i) Adım 0 production ingest'in şeklini ve (ii) senin `by` değerini belirler. **Kod (bizim Adım 1) bunu beklemedi**, fixture'larla bitti.

👉 **Lütfen (a) mı (b) mi onayla.** (a) dersen Adım 0 tohumunu biz tek hamlede yapar, register'ı gerçek dokümanlarla doldururuz.

---

## 5. Senin işin (Adım 2–3) — bilgi
- **Adım 2:** görev-türü etkinleştirmesini yukarıdaki **porta** yönlendir; `DocumentReferenceEntry.Status`'u sil; `TaskDocumentReference.Status`'u yeni alıntılarda LifecycleStatus-freeze yap. **Mevcut 2 donmuş satıra dokunma.**
- **Adım 3:** `/active` kapısı — **Kalite'nin "Kural 4" kararına bağlı** (G3). Bu yalnız Adım 3'ü etkiler; Adım 1/2'yi bloklamaz.

---

## 6. Referanslar (kanıt)
- Sözleşme: `execution/domains/platform-shared-services/work-packs/authority/dcp-005-effectiveness-contract-v2.md`
- İş kaydı: `execution/domains/platform-shared-services/work-packs/WP-0029-EFFECTIVENESS-F1.md` + `...-P2.md`
- Commit'ler (feature/crm-integration-v2): `14825d44` (resolver+port) · `0950253e` (HTTP uç) · `e13e0477` (izin+seed) · `cd7211a6` (repo $in)
- Canlı doğrulama: `POST …/effectiveness:batch` → 200, `items:[{state:2 Unresolved}]` (register boş, doğru).

**Beklenen yanıt:** G1 = (a) mı (b) mi? Ve Adım 2 için başka bir sözleşme netliği gerekiyor mu?
