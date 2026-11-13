---
id: MOD-0262-FU08
name: Document Binary Store Upload Audit Completion
parent: MOD-0262
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: TenantScopedEntity
status: draft
approval_status: pending
revision: "r1 (2026-11-13) — ilk taslak. MOD-0262-FU01 r9 §20'deki iki adlı takip işi (FU-0262-AUDIT-ENTITY ve FU-0262-TRAILING-PART) sahip kararı K-5 (Beste, 2026-11-13) ile tek FU olarak açıldı; yol (b) sahip kararı K-6 (Beste, 2026-11-13)."
status_note: "draft — kullanıcı incelemesi, CT-APR-02 bağımsız inceleme ve CT-APR-01 onay satırı bekliyor. Kod yazılmaz. Ayrıca kod kapısı: MOD-0021 korunan yol / XMC-001 izni (§7, §18)."
owner: platform-shared-services
branch: feature/pss/mod-0262-fu08-upload-audit-completion
started: 2026-11-13
target: TBD
form_field_count: 0
capability_pack: DCP-008
---

# MOD-0262-FU08 — Document Binary Store Upload Audit Completion

> **Durum (r1, 2026-11-13).** `status: draft`, `approval_status: pending`. Bu paket kod izni değildir. Geliştirme için:
> kullanıcı incelemesi → `ready-for-dev`, CT-APR-02 bağımsız incelemesi, CT-APR-01 onay satırı ve §7'deki MOD-0021
> korunan yol izni gerekir. Sahip: Beste (merkez / Platform) — K-5.
>
> **Kimlik (DCP-002, ölçüldü 2026-11-13).** `MOD-0262` (Internal Document Repository Service) altında FU. `MOD-0262-FU02`…`FU07`
> DCP-008 üye tablosunda başka içeriğe ayrılmıştır (`execution/portfolio/delivery-capability-packs/DCP-008-internal-document-repository-service.md`);
> `MOD-0262-FU08` depoda hiçbir pakette, kayıtta ya da HEAD / `origin/main` ağacında kullanılmıyor (tek geçiş: FU01 §19.3'teki
> "depoda `MOD-0262-FU08` ve sonrası yok" cümlesi). Ön kontrol: `verify_module_id.py . --check-id MOD-0262-FU08 --name "Document Binary Store Upload Audit Completion" --parent MOD-0262`
> → `OK  MOD-0262-FU08: proven against Blueprint/registry.` (çıkış 0). Yeni `MOD-xxxx` açılmaz. Ölçüm sınırı §19.
>
> **Kaynak.** Ana paket `execution/domains/platform-shared-services/module-packs/MOD-0262-FU01-document-binary-store.md` r9
> (onaylı: `execution/approvals/approvals.tsv` satır 88; inceleme `docs/records/reviews/MOD-0262-FU01/2026-11-13-71d15dec.md`):
> §20 satırları `FU-0262-AUDIT-ENTITY` ve `FU-0262-TRAILING-PART`, §21 yükleme satırları, AC-21, AC-25. Sahip kararları
> `docs/records/decisions/2026-11/mod0262-fu01-mod0181-owner-decisions-2026-11-12.md` K-2, K-3, K-5 ve K-6.

---

## 1. Module Summary

FU01'in iki yükleme komutu bugün denetim kaydını yazıyor, ama kayıt yüklenen nesneyi adlandırmıyor: `EntityId` ve §21
`AfterState` boş. Nedeni sıradır: `AuditBehavior` metadata'yı handler'dan önce komutun `GetAuditMetadata()`'sından alır
(`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs:143`), `ContentId` ise sağlayıcı
çalışınca doğar. Bugün yükleme kaydını satıra bağlayan yalnız `CorrelationId`'dir (FU01 AC-25 KISMİ).

Bu FU iki işi tek dilimde kapatır (K-5: ikisi aynı yükleme yolunu ve aynı denetim kaydını ölçer):

- **(A) Yükleme denetim kaydının nesnesi.** DL-1 `StoreRepositoryObjectCommand` ve DL-2 `StoreTrustedRepositoryObjectCommand`
  başarı kayıtları `EntityId = ContentId` ve §21 `AfterState`'i taşır. Yol (b) — K-6: `AuditBehavior` başarılı yanıttan
  `EntityId` / `AfterState` alır. Alanları komut seçer (isteğe bağlı arayüz); `AuditBehavior` yalnız birleştirir. İçerik,
  `ObjectKey` ve dosya adı yazılmaz.
- **(B) Dosyadan sonraki bölüm, host'ta.** FU01 AC-21'in gerçek "dört alan + dosya + sonra alan" vakası host'ta ölçülür:
  400 `VALIDATION_FAILED`, sağlayıcı ≤ 1 çağrı, satır yok, yarım dosya yok, tek `AuditBehavior` kaydı. FU01 r9 incelemesinin
  F-1 isteği de burada karşılanır: kaydın yazarı, `Outcome` (`Failed`), `Operation` (`Create`) ve `ResponseStatusCode` (400)
  doğrulanır. Ayrıca `two-files` vakası da bu doğrulamaları alır.

**Hedef kullanıcı:** doğrudan yok; sayfa yok. Fayda denetim kaydını okuyanadır: yükleme kaydından satıra `EntityId` ile
gidilir.

---

## 2. Ownership and Boundaries

**Kapsamda**
- `AuditBehavior`'a başarılı yanıttan durum alma adımı (isteğe bağlı arayüz; arayüzü uygulamayan komut değişmez).
- Yeni sözleşme: `IAuditResponseStateProvider<TResponse>` + `AuditResponseState` (MOD-0021 sözleşme klasörü).
- İki yükleme komutunun bu arayüzü uygulaması ve §21 `AfterState` alanları.
- FU01 DL-2 host testlerine (B) vakaları ve `two-files` vakasına yazar / sonuç doğrulamaları.

**Kapsam dışı**
- `ContentId`'yi yürütücünün önceden üretmesi — K-3 seçenek (a); K-6 ile seçilmedi.
- Telafi ve içerik okuma kayıtları — `EntityId`'leri FU01 DL-2'de zaten yazılıyor ve ölçülüyor.
- Kullanıcı yolunun `MultipartReader`'a geçişi ve kullanıcı yolu içerik okuma denetimi — FU01 K-D'nin ayrı FU'su (bu paket değil).
- FU01 AC-29 (Auth audience `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER`) — Auth'un işi, bu FU'nun kapısı değil (§7).
- Sayan akışın, yürütücünün, handler'ın ve sağlayıcının davranışı — (B) yalnız test ekler; bu dosyalara yalnız geçici sabotaj
  için dokunulur, geri alınır.

**Bu FU'nun sahip olduğu:** iki yükleme kaydının `EntityId` / `AfterState` sözleşmesi ve `AuditBehavior`'daki yanıt durumu adımı.
Başka bir şey değil.

---

## 3. Owned Objects

| Tür | Öğe | Not |
|---|---|---|
| Contract (yeni, MOD-0021 klasörü) | `IAuditResponseStateProvider<in TResponse>` — `AuditResponseState? GetAuditResponseState(TResponse response)` | Yalnız başarılı yanıtta çağrılır. Saf eşlemedir: depo okumaz, servis çağırmaz, yan etkisi yoktur. |
| Contract (yeni, MOD-0021 klasörü) | `AuditResponseState(Guid? EntityId, IReadOnlyDictionary<string, object?>? AfterState)` | Aynı dosyada. |
| Behavior (değişen) | `AuditBehavior<TRequest, TResponse>` | Başarılı sonuçtan sonra, istek arayüzü uyguluyorsa durumu alır; komutun kendi metadata değerleri önceliklidir (§8). |
| Command (değişen, DL-1) | `StoreRepositoryObjectCommand` | `IAuditResponseStateProvider<Response<RepositoryObjectModel>>` uygular. Route, izin ve imza değişmez. |
| Command (değişen, DL-2) | `StoreTrustedRepositoryObjectCommand` | Aynı arayüz; `TrustedConsumerModule` komutun `Consumer.ConsumerModule`'ünden gelir. |
| Model yardımcısı (yeni, iç) | `RepositoryObjectAuditState.For(RepositoryObjectModel model, string? trustedConsumerModule)` | `DocumentRepositoryModels.cs` içinde (DTO'lar tek dosyada). İki komut aynı alan listesini buradan alır. |
| API | **yok** | Yeni uç yok; var olan uçların yanıt gövdesi değişmez. |
| Permission | **yok** | Yeni izin yok. |
| Frontend route | **yok** | `shell: none`. |

Adlar bu paketin kararıdır. Geliştirici farklı bir ad ölçerse (ör. çakışma) raporlar; sessizce değiştirmez.

---

## 4. Entity Fields

Yeni varlık ya da alan yok. `RepositoryObject` (`TenantScopedEntity`) ve `ContentRef` değişmez; şema, indeks ve taşıma yok.

**Denetim kaydı alanları (yalnız iki yükleme komutunun başarı kaydı):**

| Alan | Değer | Biçim |
|---|---|---|
| `EntityId` | yanıtın `ContentId`'si | `Guid` (kayıt alanı) |
| `AfterState.ContentId` | `ContentId` | metin, `"D"` biçimi |
| `AfterState.Scope` | satırın `Scope`'u | metin (enum adı, ör. `Documents`) |
| `AfterState.OwningItemId` | `OwningItemId` | metin, `"D"` |
| `AfterState.OwningVersionId` | `OwningVersionId` | metin, `"D"` |
| `AfterState.ByteSize` | `ByteSize` | `Int64` |
| `AfterState.Checksum` | `Checksum` (SHA-256, depo hesaplar) | metin |
| `AfterState.TrustedConsumerModule` | yalnız DL-2: `Consumer.ConsumerModule` (v1 `MOD-0217`) | metin |

Guid değerleri `AfterState`'te metin yazılır: `Dictionary<string, object?>` içindeki değer nesne serileştiricisinden geçer ve FU01
metadata'sı da Guid'i `"D"` metni yazar. Bu karar çıkarımdır; AC-9 gerçek Mongo gidiş-dönüşüyle ölçer.

**Kayda hiç yazılmayan:** `ObjectKey`, `FileName`, `MediaType`, `StorageProvider`, `CreatedBy`, `CreatedAt`, dosya içeriği, token,
istemci sırrı. Metadata anahtar kümesi değişmez (FU01 §21 "Başarı kayıtlarının metadata anahtarları"): `AfterState` metadata
değildir.

---

## 5. Repo Scope

```text
services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/IAuditResponseStateProvider.cs            ← NEW (arayüz + AuditResponseState; MOD-0021 korunan klasör, §7 kapısı)
services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs                       ← yanıt durumu adımı (FU01 §6'da korunan dosya, §7 kapısı)
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Commands/StoreRepositoryObjectCommand.cs         ← arayüzü uygular
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Commands/StoreTrustedRepositoryObjectCommand.cs  ← arayüzü uygular; sınıf yorumundaki "no entity id" cümlesi güncellenir
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/DocumentRepositoryModels.cs     ← RepositoryObjectAuditState (iç yardımcı)
services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditBehaviorResponseStateTests.cs          ← NEW (AC-4…AC-7, sahte komutlarla)
services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/CentralAuditAppendMongoTests.cs              ← AC-9 vakası (ya da aynı desende yeni dosya)
services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/TrustedDocumentRepositoryConsumerUploadTests.cs   ← AC-1, AC-3, AC-4 (host), AC-8, AC-11, AC-12, AC-13
services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/DocumentRepositoryUserUploadAuditTests.cs          ← NEW (AC-2, AC-10; host altyapısı TrustedDocumentRepositoryConsumerHostTests)
```

Kod tabanı: FU01 DL-2 kodu (`dae798bc9`, FIX-01 `333e91559`) MVP-1 hattında ve CT zincirindedir, `origin/main`'de değildir. Bu FU'nun
dalı bu kodu içeren bir tabandan açılır (§7, CT belirler).

---

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/ocelot.json` — yalnız `integration-agent`; bu FU rota gerektirmez (§15).
- `services/Diten.AuthService/**`, `services/Diten.MdmService/**`, `services/Diten.CrmService/**`, `services/Diten.EnterpriseStrategyService/**` — başka modüllerin şeridi.
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/**` — MOD-0021'in. Bu FU yalnız yeni
  `IAuditResponseStateProvider.cs`'i ekler; başka dosyası değişmez (`AuditActorTypeResolver.cs`, `AuditRequestMetadata.cs`,
  `AuditAppendRequest.cs` dahil). Kapı §7.
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs` — FU01 §6'da "değişmeyen denetim
  dosyası". Bu FU yalnız §8'deki adımı ekler; kapı §7.
- Değişmeyen: `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/AuditService.cs`,
  `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/AuditIdempotencyKeyBuilder.cs`,
  `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/ValidationBehavior.cs`,
  `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/ExceptionBehavior.cs`.
- Değişmeyen (yalnız geçici sabotaj, geri alınır): `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryUploadLimits.cs`,
  `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryConsumerRequestExecutor.cs`,
  `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Handlers/CommandHandlers/StoreTrustedRepositoryObjectHandler.cs`,
  `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/DocumentManagement/LocalFileSystemContentStorageGateway.cs`,
  `services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentRepositoryController.cs`.

---

## 7. Dependencies

| Bağımlılık | Tür | Durum |
|---|---|---|
| **MOD-0021 korunan yol / XMC-001 izni** — `AuditBehavior.cs` değişikliği ve `Contracts/Audit/` altına yeni dosya | **Kod kapısı** (geliştirme başlamadan) | ⛔ kayıtlı izin yok. K-6 yolu seçti; MOD-0021 sahibinin (merkez / Platform — çıkarım, FU01 r9 incelemesi "ölçülmeyenler") bu iki dosya için açık izni karar dosyasına yazılır. K-6'nın bu izni de verip vermediği CT'nin kayıt kararıdır (§19 açık soru S-1). |
| MOD-0262-FU01 DL-2 kodu (`dae798bc9` + `333e91559`) | **HARD** — kod tabanı | ✅ MVP-1 hattında, CT zincirinde; `origin/main`'de değil. Dal tabanı bunu içermeli. |
| MOD-0262-FU01 r9 onayı | **HARD** — sözleşme | ✅ `approvals.tsv` satır 88 (onaylayan Ali, 2026-11-13). |
| MOD-0021 Audit Trail (`AuditBehavior` → `IAuditService` → `audit_outbox`) | altyapı | ✅ `tests/architecture/audit-ledger/Diten.Platform.md` → `platform-merkezi` (yol `a`). |
| FU01 AC-29 — Auth audience `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` | **Bu FU'nun kapısı değil** | ⛔ main'de yok (Ali, Auth). Testler test anahtarıyla imzalı token kullanır; bu FU'nun kabulü AC-29'u beklemez. |
| MOD-0217 DL-2 (tüketici) | ters bağımlılık | Etkilenmez: yanıt gövdesi, ret kodları ve durum kodları değişmez. |

---

## 8. Runtime Constraints

- Servis `Diten.Platform`, port **5057**; Gateway **5000**. Yeni uç yok.
- Kiracı: kayıt kiracısı bugünkü gibi ortam kiracısıdır (kullanıcı yolu: kullanıcının token kiracısı; güvenilen yol: yürütücünün
  `TenantScope.Begin` ile bağladığı token kiracısı — `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryConsumerRequestExecutor.cs:139`).
  Yanıt durumu adımı depo okumaz; kiracılar arası veri okuyamaz.
- Soft delete, şema, indeks: değişmez.

**`AuditBehavior` yanıt durumu kuralı (K-6, yol b).** Bugün başarılı sonuçta kayıt
`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs:69` ile
`:81` arasında yazılır; `EntityId` ve `AfterState` yalnız metadata'dan gelir (`:175`, `:179`). Bu FU'dan sonra:

1. Yalnız `outcome == Succeeded` olduğunda ve istek `IAuditResponseStateProvider<TResponse>` uyguluyorsa
   `GetAuditResponseState(response)` çağrılır. `Failed` sonuçta, çalışma zamanı istisnasında, iptalde, dışlanan ve sorgu
   isteklerinde ve `ITransactionOwnedAuditCommand` yolunda çağrılmaz.
2. Birleştirme: `EntityId = metadata.EntityId ?? state.EntityId`; `AfterState = metadata.AfterState ?? state.AfterState`.
   Komutun kendi değeri önceliklidir. `Metadata`, `CorrelationId`, `Operation`, `Category`, `EntityType`, aktör ve kiracı değişmez.
3. Sağlayıcı istisna atarsa: yanıt aynen döner, kayıt durumsuz (bugünkü gibi) yazılır, uyarı logu yalnız istek türü adını taşır
   (istisna mesajı ve değer yok). `OperationCanceledException` bugünkü gibi yayılır. Gerekçe: baytlar ve satır yazıldıktan sonra
   zenginleştirme hatası iş sonucunu 500'e çevirmemeli. Bu CT önerisidir; inceleme değiştirebilir (§19 S-3).
4. Yansıma yok: `AuditBehavior` yanıtın `Data`'sını ya da bir `ContentId` / `Id` özelliğini genel olarak okumaz. Arayüzü
   uygulamayan komutun kaydı alan alan bugünküyle aynıdır.
5. Tekrarsızlık anahtarı `EntityId`'yi içerir (`services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/AuditIdempotencyKeyBuilder.cs:10`):
   yükleme başarı kaydının anahtarı artık `ContentId`'yi taşır. Etki: istek türleri farklı olduğundan iptal / ret kayıtlarıyla
   anahtar paylaşımı yoktur (ölçüldü: `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryRefusalAudit.cs:30`
   ve `:31`); FU01 D-2 kayıt sayısı sınırı değişmez — AC-14 FU01 AC-33 / AC-35 testleriyle ölçer.

---

## 9. Layout & Shell Contract

`shell: none` — yalnız arka uç. Razor görünümü, layout, `wwwroot` dosyası, RESX yok. DataTable yok; `data_mode` uygulanmaz.

---

## 10. Backend File Convention

`golden_reference: none`; Slim / Compact ön yüz sözleşmesi uygulanmaz. Arka uç FU01 §10 düzenindedir; bu FU yeni komut, sorgu,
handler ya da doğrulayıcı eklemez. Yeni dosyalar:

```text
services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/
└── IAuditResponseStateProvider.cs        (interface + sealed record AuditResponseState)
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/
└── DocumentRepositoryModels.cs           (RepositoryObjectAuditState — DTO'lar tek dosyada kalır)
```

---

## 11. Frontend File Contract

Yok (`shell: none`, `golden_reference: none`).

---

## 12. Validation Rules

Yeni kullanıcı girdisi yok; doğrulayıcı değişmez. Sözleşme kuralları:

| Kural | Nerede | Ölçüm |
|---|---|---|
| `AfterState` anahtar kümesi DL-1'de tam altı, DL-2'de tam yedi anahtar (§4); eksik ve fazla 0 | `RepositoryObjectAuditState.For` | AC-1, AC-2, AC-3 |
| Yasak alan yok (`ObjectKey`, `FileName`, `MediaType`, `StorageProvider`, `CreatedBy`, içerik, token) | aynı | AC-3 |
| `EntityId` = yanıttaki `ContentId`; `Guid.Empty` değil | sağlayıcı | AC-1, AC-2 |
| Başarısız yanıtta durum yok | `AuditBehavior` | AC-4 |
| Metadata anahtar kümesi FU01 §21 ile aynı | `GetAuditMetadata()` değişmez | AC-3 |

---

## 13. Failure Path to Verify

| Senaryo | Beklenen |
|---|---|
| Dört alan + dosya + sonra gelen alan (güvenilen yol) | 400 `VALIDATION_FAILED`; sağlayıcı 1 çağrı; satır yok; yarım dosya yok; tek `AuditBehavior` kaydı (`Failed` / `Create`, `ResponseStatusCode = 400`, `EntityId` ve `AfterState` boş); ret yazıcısı kaydı 0 (AC-11). |
| İki dosya bölümü | aynı (AC-12). |
| Sağlayıcı 400, akış içi 413 | bugünkü sonuç; kayıtta `EntityId` / `AfterState` boş (AC-4). |
| Sağlayıcı (yanıt durumu) istisna atar | iş yanıtı aynen; kayıt durumsuz; uyarı logu yalnız tür adı (AC-7). |
| Yetkisiz kullanıcı (izin yok) DL-1 yüklemesi | 403; satır yok; yükleme kaydı yok (AC-10). |
| Kiracı B'nin yüklemesi | kayıt B'de, `EntityId` B'nin satırı (AC-8). |
| Eşzamanlılık / tekrar | Aynı istek iki kez gelirse iki ayrı `ContentId` ve iki ayrı kayıt (bugünkü davranış; yükleme tekrarsız değildir). Bu FU yeni yarış yaratmaz: durum yalnız o isteğin yanıtından okunur (AC-8). |
| Mükerrer kayıt | yükleme başarısı tam 1 kayıt (sahte yazıcı); FU01 D-2 sınırı değişmez (AC-14). |

---

## 14. Authorization Convention

- Yeni izin ve yeni uç yok. DL-1 yükleme ucu `[HasPermission(DocumentRepositoryPermissions.Manage, DocumentRepositoryPermissions.VisitEvidenceWrite)]`
  ile kalır (`services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentRepositoryController.cs:60`); izin anahtarları
  `platform.document-repository.*` (Platform servisi biçimi).
- Güvenilen yol: `[AllowAnonymous]` denetleyici + yürütücüdeki kapı (servis token'ı, kapalı tüketici listesi, kiracı bağlama) —
  FU01 §8.1, değişmez.
- Aktör türü: kullanıcı yolunda token'ın aktör türü, güvenilen yolda `Service` (FU01 K-B) — değişmez.

---

## 15. Gateway / API Routing Decision

Karar: Gateway değişikliği **gereksiz**. Yeni uç yok; `ocelot.json`'a dokunulmaz; `integration-agent` görevi yok.

---

## 16. Acceptance Criteria

- [ ] AC-1 (zorunlu) — DL-2 güvenilen yükleme başarı kaydı (gerçek HTTP host, gerçek `AuditBehavior` + `AuditService`, sahte yazıcı): 201'den sonra tek `StoreTrustedRepositoryObjectCommand` kaydı vardır; `EntityId` yanıt gövdesindeki `contentId`'ye eşittir; `AfterState` anahtar kümesi birebir {`ContentId`, `Scope`, `OwningItemId`, `OwningVersionId`, `ByteSize`, `Checksum`, `TrustedConsumerModule`} ve değerler satırla aynıdır (Guid'ler `"D"` metni, `ByteSize` Int64, `Scope = Documents`, `Checksum` satırın SHA-256'sı, `TrustedConsumerModule = MOD-0217`). FU01 AC-25 testi (`A_service_token_without_permissions_runs_upload_meta_content_compensate_and_each_success_is_one_service_record`) yükleme kaydı için `EntityId` doğrulamasını da alır. Test: `TrustedDocumentRepositoryConsumerUploadTests.A_trusted_upload_record_carries_the_content_id_and_the_after_state`. Sabotaj S-1: `AuditBehavior`'daki yanıt durumu birleştirmesi kaldırılınca `EntityId` boş kalır, test kırmızı. Sabotaj S-2: `StoreTrustedRepositoryObjectCommand` sağlayıcısı `TrustedConsumerModule`'ü atlayınca anahtar kümesi doğrulaması kırmızı.
- [ ] AC-2 (zorunlu) — DL-1 kullanıcı yolu: `platform.document-repository.manage` izinli kullanıcı token'ıyla `POST api/v1/document-repository/objects` 201 döner; tek `StoreRepositoryObjectCommand` kaydında `EntityId = ContentId`, `AfterState` anahtar kümesi birebir {`ContentId`, `Scope`, `OwningItemId`, `OwningVersionId`, `ByteSize`, `Checksum`} (`TrustedConsumerModule` yok), kayıt kiracısı token kiracısıdır. Test: `DocumentRepositoryUserUploadAuditTests.A_user_upload_record_carries_the_content_id_and_the_after_state`. Sabotaj S-3: `StoreRepositoryObjectCommand`'dan arayüz uygulaması kaldırılınca test kırmızı.
- [ ] AC-3 (zorunlu) — Gizli ve gereksiz bilgi yok: iki yükleme kaydının `AfterState`'inde ve `Metadata`'sında `ObjectKey`, `FileName`, `MediaType`, `StorageProvider`, `CreatedBy`, dosya içeriği ve token yoktur; yükleme metadata anahtar kümesi FU01 §21'deki ortak altı anahtardır (FU01 AC-25 testindeki `Keys(store)` eşitliği yeşil kalır). Test: AC-1 ve AC-2 testlerindeki birebir anahtar kümesi + `Keys(store)` eşitliği. Sabotaj S-4: `RepositoryObjectAuditState.For`'a `FileName` eklenince AC-1 ve AC-2 testleri kırmızı. Sabotaj S-5: durum `AfterState` yerine `Metadata`'ya yazılınca FU01 AC-25 anahtar testi kırmızı.
- [ ] AC-4 (zorunlu) — Yalnız başarıda: `Failed` sonuçta ve çalışma zamanı istisnasında sağlayıcı çağrılmaz; kayıtta `EntityId` ve `AfterState` boştur. Birim testler (`AuditBehaviorResponseStateTests`, sabit durum döndüren sahte sağlayıcılı sahte komut): `A_failed_response_never_asks_the_response_state_provider`, `A_runtime_exception_never_asks_the_response_state_provider`. Host: `A_provider_400_is_one_AuditBehavior_record` ve `One_byte_over_50_MiB_is_413_from_inside_the_command_with_one_AuditBehavior_record_and_no_partial_file` kayıtta `EntityId` / `AfterState` boşluğunu doğrular. Sabotaj S-6: başarı koşulu kaldırılınca sahte sağlayıcının sabit `EntityId`'si `Failed` kayda düşer, birim test kırmızı.
- [ ] AC-5 (zorunlu) — Komutun kendi metadata'sı önceliklidir: `GetAuditMetadata()` dolu `EntityId` / `AfterState` verirse yanıt durumu onları ezmez. Test: `AuditBehaviorResponseStateTests.Command_metadata_entity_id_and_after_state_win_over_the_response_state`. Sabotaj S-7: birleştirme sırası ters çevrilince (`state ?? metadata`) test kırmızı.
- [ ] AC-6 (zorunlu) — İsteğe bağlı; başka komut değişmez: arayüzü uygulamayan sahte komutun `AuditAppendRequest`'i alan alan bugünküyle aynıdır (yanıtında `ContentId` / `Id` özelliği olsa bile); `AuditBehaviorTests`'in tüm vakaları yeşil kalır. Test: `AuditBehaviorResponseStateTests.A_command_without_the_provider_gets_the_same_append_request`. Sabotaj S-8: `AuditBehavior`'a yanıtın `Data.ContentId`'sini yansımayla okuyan genel dal eklenince test kırmızı.
- [ ] AC-7 (zorunlu) — Sağlayıcı hatası iş sonucunu değiştirmez: sağlayıcı istisna atarsa yanıt aynen döner, tek kayıt durumsuz yazılır, uyarı logu istek türü adını taşır ve istisna mesajını taşımaz. Test: `AuditBehaviorResponseStateTests.A_throwing_response_state_provider_keeps_the_answer_and_writes_the_record_without_state`. Sabotaj S-9: sağlayıcı çağrısını saran yakalama kaldırılınca istisna yayılır, test kırmızı.
- [ ] AC-8 (zorunlu) — Kiracı izolasyonu: iki kiracının (A, B) güvenilen token'larıyla iki yükleme; her kaydın kiracısı kendi token kiracısıdır, `EntityId`'si o kiracının satırının `ContentId`'sidir; B'nin kaydında A'nın `ContentId`'si yoktur. Test: `TrustedDocumentRepositoryConsumerUploadTests.Two_tenants_uploads_each_record_names_its_own_tenants_content_id`. Sabotaj S-10: yürütücünün yükleme dalındaki token kiracısı bağlaması (`TenantScope.Begin`) kaldırılınca test kırmızı.
- [ ] AC-9 (zorunlu) — Gerçek Mongo: yükleme kaydı gerçek Mongo'ya yazılıp geri okununca `EntityId` ve `AfterState` değerleri aynıdır (Guid alanları metin, `ByteSize` Int64; serileştirme hatası yok). Test: `CentralAuditAppendMongoTests` deseninde `An_upload_record_with_after_state_round_trips_in_mongo`. Sabotaj S-11: `AuditBehavior`'daki sağlayıcı çağrısı kaldırılınca okunan kayıtta `AfterState` boş, test kırmızı.
- [ ] AC-10 (zorunlu) — Yetki: bu FU yeni uç ve izin açmaz; DL-1 yükleme ucunun `[HasPermission]`'ı aynıdır. İzinsiz kullanıcı token'ıyla yükleme 403 döner, satır yoktur, `StoreRepositoryObjectCommand` kaydı yoktur. Test: `DocumentRepositoryUserUploadAuditTests.A_user_without_manage_cannot_upload_and_no_upload_record_is_written`. Sabotaj S-12: `DocumentRepositoryController.Store`'dan `[HasPermission]` kaldırılınca 201 döner, test kırmızı.
- [ ] AC-11 (zorunlu) — FU01 AC-21'in host vakası (K-2): dört alan + dosya + sonra gelen alan, iki fikstür — `four-fields-file-then-unknown-field` (sonra `note=x`) ve `four-fields-file-then-repeated-scope` (sonra `scope=Documents`). Her biri: 400, ret kodu `VALIDATION_FAILED`; `host.Storage.StoreCalls == 1` (K-2'nin "≤ 1"i; bu vakada tam 1 — fikstürün dosyayı tamamen geçtiğinin kanıtı); satır yok; depolama kökünde dosya yok; toplam kayıt 1; kaydın `RequestType`'ı `StoreTrustedRepositoryObjectCommand`, `Outcome = Failed`, `Operation = Create`, `Metadata["ResponseStatusCode"] = 400`, `EntityId` ve `AfterState` boş; `document-repository.trusted-consumer.denied` kaydı 0. Test: `TrustedDocumentRepositoryConsumerUploadTests.A_field_after_a_complete_file_is_400_with_one_AuditBehavior_record_and_nothing_left` (Theory). Sabotaj S-13: sayan akışta yalnız `FormatViolated = true` ataması kaldırılınca (`IOException` kalır) yanıt 503 olur, test kırmızı. (Dal tümüyle kaldırılırsa kopya başarılı olur, 201 ve satır yazılır; test yine kırmızıdır ama başka bir şeyi ölçer — FU01 r9 incelemesi F-3.) Sabotaj S-14: handler'ın `FormatViolated` denetimi kaldırılınca 503, test kırmızı. Sabotaj S-15: sağlayıcının `IOException` dalındaki `TryRemovePartial` kaldırılınca "dosya yok" doğrulaması kırmızı. Sabotaj S-16 (fikstür): fikstür eski biçime (üç alan + dosya + dördüncü alan) çekilince `StoreCalls` 0 olur, test kırmızı.
- [ ] AC-12 (zorunlu) — FU01 r9 incelemesi F-1: `two-files` vakası AC-11'deki yazar, `Outcome`, `Operation`, `ResponseStatusCode`, boş `EntityId` / `AfterState` ve ret yazıcısı 0 doğrulamalarını alır (`A_form_outside_the_contract_is_400_and_leaves_no_row` içinde ya da ayrı test `Two_file_sections_is_400_with_one_AuditBehavior_record`). Sabotaj: S-13 ve S-14 bu vakayı da kırmızı yapar.
- [ ] AC-13 (isteğe bağlı) — Dürüst fikstür adı: mevcut `field-after-file` fikstürü ölçtüğü şeye göre `missing-field-before-file-then-field` diye adlandırılır; beklentisi değişmez (sağlayıcı 0, ret yazıcısı tek kayıt). Sabotaj S-17: yürütücünün dosyadan önceki eksik alan denetimi kaldırılınca bu vaka kırmızı.
- [ ] AC-14 (zorunlu) — Gerileme yok: Platform test projesi filtresiz tam koşuda yeşil, Platform build 0 hata; FU01 AC-21, AC-25, AC-26, AC-33 ve AC-35 testleri yeşil ve kayıt sayıları aynı (yükleme başarısı 1 kayıt; iptal satırı FU01 D-2 kuralında); denetim defteri mimari testleri yeşil (yeni yazma komutu yok). Sabotaj: S-1 uygulanınca tam koşu kırmızıdır (en az AC-1); geliştirici kırmızı test adlarını raporlar.
- [ ] AC-15 (zorunlu) — Kapsam ve kapı: değişen dosyalar §5 listesinin alt kümesidir (`git diff --name-only` tabana karşı); `AuditBehavior.cs` ve `Contracts/Audit/IAuditResponseStateProvider.cs` değişikliği §7'deki MOD-0021 izni kayıtlıyken yapılır; geliştirme kaydı izin kaydının yolunu yazar. Sabotaj S-18: §5 dışı bir dosya (ör. `ValidationBehavior.cs`) değiştirilince CT'nin dosya listesi karşılaştırması kırmızı.

---

## 17. Test Expectations

| Katman | Beklenti |
|---|---|
| Unit (`AuditBehaviorResponseStateTests`) | AC-4 (iki vaka), AC-5, AC-6, AC-7 — gerçek `AuditBehavior`, yakalayıcı `IAuditService`, sahte komutlar; kayıt alanları tek tek. |
| Host (gerçek HTTP, gerçek `AuditBehavior` + `AuditService`, sahte yazıcı) | AC-1, AC-2, AC-3, AC-4 (host kısmı), AC-8, AC-10, AC-11, AC-12, AC-13 — `TrustedDocumentRepositoryConsumerHostTests` altyapısı (`Host.UserToken`, `host.Records()`, `host.Storage.StoreCalls`). |
| Gerçek Mongo | AC-9 — `CentralAuditAppendMongoTests` deseni; Mongo yoksa test "atlandı" değil kırmızı ya da koşulmadı diye raporlanır. |
| Kiracı | AC-8 (güvenilen yol, iki kiracı); AC-2 kayıt kiracısı. |
| Sabotaj | S-1…S-18: her biri geçici uygulanır, kırmızı test adı ve çıkış raporlanır, geri alınır, yeşil yeniden ölçülür. Sabotaj kanıtı olmayan AC kabul edilmez. |
| Build / suite | Platform build 0 hata; Platform test projesi filtresiz tam koşu (AC-14). |
| Kapsam / kapı | AC-15 — CT, `git diff --name-only`'yi §5 listesiyle karşılaştırır ve MOD-0021 izin kaydının yolunu geliştirme kaydında arar. |
| Soft delete | Değişmez; FU01 testleri kapsar. |
| Browser smoke, RESX, verifier | Uygulanmaz (sayfa yok, metin yok). |

---

## 18. Ready-for-dev Checklist

- [x] Golden Reference: uygulanmaz (`golden_reference: none`, sayfa yok) — FU01 §9 / §10 düzeni okundu.
- [x] Frontmatter zorunlu alanları dolu (`service`, `shell`, `golden_reference`, `entity_base`).
- [x] Layout & Shell Contract: `shell: none`, layout yok.
- [x] Backend File Convention: yeni komut / handler yok; yeni dosyalar §10.
- [x] Frontend File Contract: yok.
- [x] Validation Rules: §12.
- [x] Failure Path: §13 (eksik / bozuk biçim, yetkisiz, kiracı, eşzamanlılık / tekrar).
- [x] Authorization Convention: §14.
- [x] Gateway kararı: gereksiz (§15).
- [x] Lookup kararı: uygulanmaz (lookup yok).
- [x] AC'ler test edilebilir, her birinin adlı sabotajı var (§16).
- [x] Audited Events: iki yükleme komutu (§21).
- [x] Sayfa yardımı / tur: sayfa yok, tur yok.
- [ ] Kullanıcı incelemesi ve `status: ready-for-dev`.
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum) ve `reviews.tsv` satırı.
- [ ] CT-APR-01 onay satırı (risk HIGH: denetim davranışı, korunan yol, ürün kodu yolu).
- [ ] MOD-0021 korunan yol / XMC-001 izni kayıtlı (§7).
- [ ] `execution/registries/module-id-registry.md`'ye `MOD-0262-FU08` satırı (CT).

---

## 19. Implementation Notes

- **Kararlar.** K-5 (Beste, 2026-11-13): FU01 r9 §20'deki iki adlı FU tek FU olarak açılır; sahip Beste (merkez / Platform);
  r9 incelemesinin F-1'i bu FU'nun beklentisidir, F-2 (sahip biçimi) bu paketle kapanır — sahip künyede ve özet kutusunda
  "Beste (merkez / Platform)". K-6 (Beste, 2026-11-13, "Birleşik ek işi açalım, önerini kabul ediyorum"): K-3'ün yolu (b) —
  `AuditBehavior` başarılı yanıttan `EntityId` / `AfterState` alır. K-6'yı karar dosyasına CT yazar. K-2 aynen: dosyadan sonraki
  bölüm 400, sağlayıcı ≤ 1, satır yok, yarım dosya yok, yazar `AuditBehavior`.
- **Neden isteğe bağlı arayüz.** (b)'nin iki biçimi vardı: `AuditBehavior` yanıtı yansımayla okur ya da komut neyin yazılacağını
  söyler. İkincisi seçildi (CT önerisi; K-6 biçimi belirtmedi): alanları komut seçer, yasak alan ihtimali komutun dosyasında
  ölçülür, öteki Platform komutlarının kaydı değişmez (AC-6). İnceleme farklı biçim isterse §19 S-2.
- **Ölçüm (2026-11-13, bu çalışma ağacı, HEAD `f827bf948`).** Okunan kod: `AuditBehavior.cs` (kayıt `:69`–`:81`, metadata `:143`,
  `EntityId` `:175`, `AfterState` `:179`), `AuditRequestMetadata.cs`, `AuditService.cs` (`BuildPayload` `AfterState`'i taşır),
  `AuditIdempotencyKeyBuilder.cs:10`, iki yükleme komutu (`StoreRepositoryObjectCommand.cs:15`–`:19`,
  `StoreTrustedRepositoryObjectCommand.cs:20`–`:26` — ikisinde de `EntityId` / `AfterState` yok), `StoreTrustedRepositoryObjectHandler.cs:51`,
  `TrustedDocumentRepositoryUploadLimits.cs:119`, `LocalFileSystemContentStorageGateway.cs:118`–`:121`, `DocumentRepositoryController.cs:59`–`:84`,
  `TrustedDocumentRepositoryConsumerUploadTests.cs` (`:54`, `:91`, `:283`, `:291`, `:296`, `:315`, `:318`), `TrustedDocumentRepositoryConsumerHostTests.cs:381`.
  DL-1 `StoreRepositoryObjectCommand`'ın denetim kaydını doğrulayan test bugün yok (`grep` 0, test projesi). Test, build ve
  sabotaj koşulmadı.
- **Kimlik ölçümü.** `--discover "document binary store audit entity"` → çıkış 3 ("inceleme gerekli"): blueprint 0, registry 1
  (`MOD-0262-FU01`), paket 1 (FU01 paketi), gateway 2 (`audit`, `document-management` → 5057), görev 1 (PVG-M1-15, MOD-0262
  bağımlılık takibi). Hiçbiri bu FU'nun mükerrer kimliği değil: FU01 ana pakettir, gateway kaynakları var olan Platform rotalarıdır,
  PVG satırı tüketici bağımlılığıdır. `--check-id` FU02, FU07 ve FU08 için çıkış 0 verdi: araç DCP-008'in FU02…FU07
  ayrılmasını görmüyor (yalnız Blueprint ve kayıt satırını okuyor); boş numara bu yüzden DCP-008 tablosu ve depo taramasıyla
  seçildi (FU08).
- **Plan hizalaması.** Plan satırı bu pakete verilmedi (plan dosyası depoda değil): dilim tablosunda `plan satırı yok`. Bu FU yeni plan
  MVP'si ya da yetenek kimliği getirmez; FU01'in AC-21 / AC-25 artığıdır.
- **Açık sorular (sahip / CT).**
  - S-1: K-6, MOD-0021 korunan yol / XMC-001 iznini de veriyor mu, yoksa ayrı izin satırı mı gerekir? MOD-0021 sahibinin
    merkez / Platform (Beste) olduğu çıkarımdır.
  - S-2: Arayüz biçimi (komut sağlar) mi, yoksa `AuditBehavior` içinde belirli yanıt türüne bağlı dal mı? Paket arayüzü önerir.
  - S-3: Sağlayıcı istisnası: yut + uyarı (paket önerisi) mi, yoksa iş yanıtını bozmadan kaydı `Failed` mı yazmalı?
  - S-4: (B) XMC izni gecikirse ayrı, DL-1'den bağımsız bir dilim olarak öne alınsın mı? (B) yalnız test ekler, korunan dosyaya
    dokunmaz; bugün tek dilim önerilir (K-5: ikisi aynı kaydı ölçer).
  - S-5: FU01 §20'deki iki satır `MOD-0262-FU08`'e yönlendirilir (FU01 r10 ya da not) ve kayıt satırı açılır — CT işi.
  - S-6: Dal tabanı (MVP-1 hattı mı, CT zinciri mi) — CT.

---

## 20. Follow-up Items

| Öğe | Hedef |
|---|---|
| Kullanıcı yolu `MultipartReader` geçişi ve kullanıcı yolu içerik okuma denetimi | FU01 K-D'nin ayrı FU'su — sahip Beste, tarih TBD; kimlik DCP-002 ile (bu paket değil) |
| FU01 AC-29 Auth audience | Auth (Ali) — FU01 kabul kapısı, bu FU'nun değil |
| Başka komutların (`Create…`) aynı arayüzü benimsemesi | her modülün kendi işi; bu FU yalnız iki yükleme komutunu bağlar |
| FU01 §20 satırlarının bu kimliğe yönlendirilmesi ve kayıt satırı | CT (§19 S-5) |

---

## 21. Audited Events

Servisin denetim altyapısı var: `tests/architecture/audit-ledger/Diten.Platform.md` → `## İzler` → `platform-merkezi` (yol `a`).
Bu FU yeni yazma komutu eklemez; sahip olduğu yalnız iki var olan yükleme komutunun kayıt içeriğidir. FU01'in öteki komutları
(telafi) ve okuma / ret / iptal satırları FU01 §21'de kalır, değişmez.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `StoreRepositoryObjectCommand` (FU01 DL-1) | `StoreRepositoryObjectCommand` | `RepositoryObject` | a | Başarıda `EntityId = ContentId`; AfterState: `ContentId`, `Scope`, `OwningItemId`, `OwningVersionId`, `ByteSize`, `Checksum` (yanıttan, `IAuditResponseStateProvider`). Başarısızda değişmez (durum yok). | — |
| `StoreTrustedRepositoryObjectCommand` (FU01 DL-2) | `StoreTrustedRepositoryObjectCommand` | `RepositoryObject` | a (`ActorType = Service`, FU01 K-B) | Başarıda `EntityId = ContentId`; AfterState: yukarıdaki alanlar + `TrustedConsumerModule`. Metadata anahtar kümesi FU01 §21 ile aynı. Başarısızda (400 / 413 / dosyadan sonraki bölüm) durum yok, `ResponseStatusCode` `AuditBehavior`'dan. | — |

Kayda yazılmayan: `ObjectKey`, dosya adı, medya türü, sağlayıcı adı, oluşturan, içerik, token, istemci sırrı. K2 sınıfı değil
(FU01 §21 gerekçesi aynen).

---

## 22. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | yükleme denetim kaydının nesnesi (`EntityId` / `AfterState`, yol b) ve dosyadan sonraki bölümün host testi — çekirdek ve tek dilim | Module Summary, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Validation Rules, Failure Path to Verify, Authorization Convention, Acceptance Criteria, Test Expectations, Audited Events | birim + gerçek HTTP host + gerçek Mongo + sabotaj S-1…S-18 + filtresiz tam Platform suite; kapı: MOD-0021 korunan yol izni (§7) | plan satırı yok | plan satırı yok |

**AC ↔ dilim:** DL-1 → AC-1…AC-15.
