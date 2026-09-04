# İş Planı — 0. + 1. Adım (doküman-yönetimi tarafı)

Kapsam: BENİM tarafım — register tohumu (0) + port/resolver/HTTP uç/RBAC/testler (1).
Adım 2–3 Görev Merkezi'nde, bu planın dışında. Sözleşme: `ek-okuma-sozlesmesi-v2.md`.

**Taşıyıcı bulgular (doğrulandı):**
- `IDocumentMasterRegisterRepository` — `GetByPermanentUidAsync` / `GetByDocumentCodeAsync`
  **tekil** var, batch **yok** (`IDocumentManagementMasterRegisterRepositories.cs:23,26`).
- Tasks + DocumentManagement **aynı assembly** (`Diten.Platform.Application`) → port düz bir
  interface, Tasks DI ile referanslar; cross-assembly tesisat yok.
- Eşleme: `ControlledDocumentLifecyclePolicy.IsOperationallyEffective` = Effective ∨ UnderRevision.

---

## Değişmez ilke — resolver a/b-agnostik

Handler hem `by:code` hem `by:uid` destekler. **(a)/(b) join kararı, 1. adımın KODUNU
bloklamaz** — yalnız (i) 0. adım production ingest'in şeklini ve (ii) çağıranın `by`'ını
etkiler. Kod fixture'larla ilerler; production ingest a/b'yi bekler. Bu ayrım planın belkemiği.

---

## Karar kapıları (kod öncesi / paralel)

| # | Karar | Kimden | Neyi bloklar |
|---|---|---|---|
| G1 | (a)/(b) join | Görev Merkezi | Yalnız Faz 5 production ingest + caller `by`. Faz 1–4'ü bloklamaz. |
| G2 | RBAC anahtarı: yeni `…effectiveness.read` mi `…view` reuse mi + seed yolu | Biz | Faz 4. Canlı `master-register.view` 200/403 doğrulaması. |
| G3 | Kalite Kural 4 zorlanacak mı | Kalite | Yalnız adım 3 (Görev Merkezi). Bizim 0–1'i bloklamaz. |

---

## Faz 1 — Uygulama katmanı (tek resolver)  ·  yeni dosyalar

`Features/DocumentManagement/MasterRegister/` altında:

- `Queries/ResolveDocumentEffectivenessQuery.cs`
  `record ResolveDocumentEffectivenessQuery(IReadOnlyList<string> Identifiers, DocumentIdentifierKind By, string CorrelationId) : IRequest<Response<DocumentEffectivenessResult>>`
- `Handlers/QueryHandlers/ResolveDocumentEffectivenessHandler.cs`
  - `By`'a göre batch fetch (Faz 2 metodu).
  - Her tanımlayıcı → `IsOperationallyEffective` ? `Effective` : `Blocked(reason)`; hiç yoksa `Unresolved`.
  - `reason` = LifecycleStatus (register'ın kelimesi).
  - **Fail-closed:** repo istisnası YAKALANMAZ; yukarı fırlar. Altyapı hatası asla `Unresolved`'a çevrilmez.
- `Models/DocumentEffectivenessModels.cs` — `DocumentEffectivenessResult`, `DocumentEffectivenessItem`, enum `DocumentEffectivenessState { Effective, Blocked, Unresolved }`, enum `DocumentIdentifierKind { Code, Uid }`.
- `Ports/IControlledDocumentEffectivenessPort.cs` + `ControlledDocumentEffectivenessPort.cs`
  (impl `IMediator`'ı sarar; iç kapının tek bağımlılığı bu arayüz).
- DI kaydı (`DependencyInjection.cs`): handler otomatik (MediatR), port `AddScoped`.

## Faz 2 — Repository batch  ·  mevcut dosyaları düzenle

- `IDocumentMasterRegisterRepository`'ye ekle: `GetByPermanentUidsAsync(IReadOnlyList<string>)`,
  `GetByDocumentCodesAsync(IReadOnlyList<string>)` — Mongo `$in`, tenant filtresi mevcut
  ExecutionFilter'dan.
- Mongo impl'de karşılıkları.
- (Alternatif: register küçük olduğundan `GetAllForTenantAsync` + bellek-içi eşleme — fallback
  olarak not; ama `$in` indeksli ve temiz, tercih bu.)

## Faz 3 — HTTP uç (yalnız ekran)  ·  controller'a aksiyon

- `DocumentManagementMasterRegisterController`'a:
  `POST document-master-register/effectiveness:batch`
  `[HasPermission(DocumentMasterRegisterPermissions.EffectivenessRead)]`
  Body `{ by, identifiers[] }` → aynı query → gateway zarfı passthrough.
- Boş/bozuk istek → 400. `Unresolved` bir hata değil, 200 içinde döner.

## Faz 4 — RBAC  ·  sabit + seed

- Permission sabiti: `EffectivenessRead = "platform.document-management.master-register.effectiveness.read"`.
- **Seed** (kanonik yol — DataSeeder veya katalog sayfa/aksiyon kaydı): anahtar
  `TaskTypesManage` sahibi rollere (task-type ekranını görenler).
- **Canlı doğrulama:** `master-register.view`'i normal tenant kullanıcısıyla aç → 200 mü 403 mü
  (ölçüm: DataSeeder'da 0 tohum; katalogdan gelmiş olabilir). Sonuca göre view'i de tohumla.

## Faz 5 — 0. adım: register tohumu

- **Test tohumu (Faz 1–3 testleri için, hemen):** 3 doküman, 3 FARKLI durum —
  `Effective` · `ApprovedPendingEffective` · (hiç yok = `Unresolved`). Fixture olarak.
- **Production ingest (G1 = (a) ise):** CSV dokümanları register'a
  `PermanentUid = CSV uid`, `IsSystemAllocated=false`, `DocumentCode`, `LifecycleStatus` ile
  alınır. ⚠ Alt-karar: ingest yolu — tek seferlik script mi, registration controller mı,
  baseline import mı. Bu, "CSV'yi öldüren" hamle; koddan sonra gelebilir, kodu bloklamaz.

---

## Test matrisi (Faz 1–3 ile birlikte)

| Test | Beklenen |
|---|---|
| LifecycleStatus = Effective / UnderRevision | `State=Effective` |
| Diğer 7 üye (Draft…ObsoleteCopy) | `State=Blocked`, `reason`=durum |
| Tanımlayıcı register'da yok | `State=Unresolved` |
| `by=code` ve `by=uid` ayrı ayrı | Doğru alandan çözer |
| Repo istisna atar | İstisna yukarı fırlar (Unresolved'a ÇEVRİLMEZ) — fail-closed |
| Karışık batch (Effective+Blocked+Unresolved) | Her öğe kendi dalında |
| HTTP: izinsiz çağrı | 403 |
| HTTP: boş `identifiers` | 400 |
| Port contract | Query ile aynı sonucu döndürür (tek resolver kanıtı) |

---

## Sıra ve teslim

  Faz 1 → 2 → 3 → 4 birbirine bağlı, **a/b'yi beklemez** (fixture'larla).
  Faz 5 test-tohumu Faz 1 ile paralel; production ingest G1'i bekler.
  Hepsi bitince Görev Merkezi'ne "1. adım hazır" → onların adım 2'si tetiklenir.

## Efor kabası

- Faz 1–3: küçük-orta (yeni sorgu + batch repo + tek aksiyon).
- Faz 4: küçük ama **risk seed'de** — canlı doğrulama şart, yoksa uç sessizce herkese kapalı.
- Faz 5: test-tohumu küçük; production ingest orta + yol alt-kararı.

---

## Onayına bağlı başlangıç

Onaylarsan **Faz 1**'den başlarım (sorgu + DTO + port + handler, fail-closed dahil), tek
commit'te testleriyle. G1/G2 kararları paralel ilerleyebilir; kodu bloklamıyorlar.
