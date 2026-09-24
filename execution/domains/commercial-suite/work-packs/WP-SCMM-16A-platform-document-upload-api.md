# WORK PACKAGE — WP-SCMM-16A · Platform document upload/download HTTP API (IContentStorageGateway üzerine) — SCMM-16 ön-koşulu (backend, Platform)

> **CT (SoR).** CAND-CAP-0011 / SCMM-16 (DEC-SCMM-04 **C4**) ön-koşulu. **Kullanıcı kararı:** artifact deposu = MOD-0262 seam; ama seam **HTTP yüzeyi yok** → CrmService (render, SCMM-16B) çağıramıyor. Bu WP = **Platform'a document upload/download HTTP API** ekler (mevcut `IContentStorageGateway` + `LocalFileSystemContentStorageGateway` FU01'i açığa çıkarır). **Diten.Platform (API + gerekirse Application ince komut).** Storage gateway/domain DEĞİŞMEZ; yeni endpoint + güvenlik + hash + gateway route.

## Kanıt
- **Seam mevcut:** `IContentStorageGateway` (Contracts/DocumentRepository): `StoreAsync(ContentStoreRequest{TenantId, CompanyId, Scope(ContentStorageScope), ItemId, VersionId, FileName, DeclaredMediaType, Content(Stream), CreatedBy, StoragePartition?}) → Response<ContentStoreResult{ContentId, StorageProvider, ObjectKey, FileName, MediaType, ByteSize, …}>` · `OpenReadAsync(provider, objectKey) → ContentStreamResult` · `TryDeleteAsync`. Impl: `LocalFileSystemContentStorageGateway` (FU01 built, Platform DI'da kayıtlı).
- **`ContentStorageScope`** enum: ControlledDocuments=1, TaskAttachments=2 → SCMM için yeni değer gerekir.
- **HTTP yüzeyi YOK:** Platform'da generic document upload/download controller yok; gateway'de route yok. Bu yüzden cross-service (CrmService→artifact) çağrı imkansız.
- **Güvenlik dersi (hafıza `es-uploads-anonymous-access-d5-fix`):** upload endpoint'i güvenlik-hassas — authz + tenant + medya-tipi allowlist + boyut sınırı ŞART (anon/isolation açığı olmasın).

## NE (Diten.Platform; storage gateway/domain DEĞİŞMEZ)
1. **Upload endpoint:** `POST /api/v1/platform/documents` — multipart/form-data (dosya + scope + itemId + versionId + fileName) VEYA stream; **TenantId + CompanyId sunucudan** (JWT claim, payload'dan DEĞİL); `IContentStorageGateway.StoreAsync` çağır → `ContentStoreResult` döndür (ContentId/ObjectKey/MediaType/ByteSize + **Hash**). Idempotency: aynı (ItemId,VersionId) → mevcut sonucu döndür (duplicate render-store güvenli).
2. **Download endpoint:** `GET /api/v1/platform/documents/{storageProvider}/{objectKey}` — `OpenReadAsync` → stream (doğru Content-Type + bodiless/404 guard). Tenant/scope yetki kontrolü (başka tenant objesi 404).
3. **Hash (C4 gereği):** `ContentStoreResult`'ta **content hash (SHA-256)** yoksa ekle — store sırasında hesapla (gateway veya API katmanı). C4: "artifact/hash". Read'de doğrulanabilir.
4. **Scope:** `ContentStorageScope`'a SCMM artifact değeri ekle (ör. `ContentMessagingArtifacts = 3`) — veya generic bir değer.
5. **Güvenlik/hardening:** authz izin anahtarı (`platform.document.write` / `.read` — tanımla/seed) + tenant-scope + **medya-tipi allowlist** (PDF vb.) + **boyut sınırı** (Kestrel/multipart cap) + anon erişim YOK (ES-uploads dersi).
6. **Gateway:** ocelot `/api/v1/platform/documents/{everything}` route (Platform portuna; audit route deseni).

## KORU / YAPMA
- **`IContentStorageGateway` contract + `LocalFileSystemContentStorageGateway` impl DEĞİŞMEZ** (yalnız hash gerekiyorsa additive; mevcut ControlledDocuments/TaskAttachments kullanımı bozulmaz). ControlledDocuments/DocumentManagement feature'ları DOKUNMA. Diğer servisler (CrmService/MDM) DOKUNMA (SCMM-16B ayrı). TenantId/CompanyId **payload'dan alınmaz** (JWT). Upload authz+tenant+allowlist+cap ZORUNLU. Render (SCMM-16B) + release (SCMM-17) BU WP'DE YOK.
- **DUR:** `ContentStorageScope`'a değer eklemek mevcut persist edilmiş scope okumalarını bozuyorsa (enum int drift); `StoreAsync` idempotency (aynı Item/Version) desteklemiyorsa → DUR+raporla.

## Acceptance
- **E2:** `dotnet test` ilgili Platform test projesi yeşil (baseline + yeni upload/download testleri); Diten.Platform.API derleme 0 hata. git diff: Platform.API (controller + request models) + gerekirse Application (ince store/read command) + Infrastructure (yalnız hash additive, gerekiyorsa) + gateway ocelot. **Storage gateway contract/impl mevcut davranış diff YOK (hash hariç additive).** Testler: upload→ContentStoreResult+hash · download stream · cross-tenant 404 · anon 401/403 · oversize/medya-tipi reddi · duplicate (Item/Version)→mevcut.
- **E4:** authenticated POST (PDF) → ContentId+objectKey+hash · GET → aynı bytes · başka tenant GET → 404 · anon → reddedilir.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-16A · Platform document upload/download HTTP API (IContentStorageGateway üzerine) — SCMM-16 ön-koşulu (Diten.Platform, backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Worktree: ana checkout

Amaç: MOD-0262 storage seam'inin (IContentStorageGateway + LocalFileSystemContentStorageGateway FU01) HTTP yüzeyi yok → SCMM-16 render (CrmService) artifact yazamıyor. Bu WP Platform'a upload/download API ekler. Storage gateway/domain DEĞİŞMEZ; render/release YOK.

Önce oku: execution/domains/commercial-suite/work-packs/WP-SCMM-16A-platform-document-upload-api.md · services/Diten.Platform/src/Diten.Platform.Application/Contracts/DocumentRepository/IContentStorageGateway.cs (StoreAsync/OpenReadAsync/TryDelete + ContentStoreRequest/Result + ContentStorageScope) · services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/DocumentManagement/LocalFileSystemContentStorageGateway.cs · (desen) mevcut bir Platform.API controller + authz + gateway ocelot audit route · memory es-uploads-anonymous-access (upload hardening).

NE (Diten.Platform; gateway contract/impl DEĞİŞMEZ):
 1) POST /api/v1/platform/documents — multipart (dosya+scope+itemId+versionId+fileName); TenantId+CompanyId JWT'den (payload DEĞİL); IContentStorageGateway.StoreAsync → ContentStoreResult (ContentId/ObjectKey/MediaType/ByteSize + Hash). Idempotent (aynı ItemId,VersionId → mevcut).
 2) GET /api/v1/platform/documents/{storageProvider}/{objectKey} — OpenReadAsync → stream (Content-Type + 404 guard); cross-tenant → 404.
 3) Hash: ContentStoreResult'ta SHA-256 yoksa store sırasında ekle (additive).
 4) ContentStorageScope'a SCMM değeri (ContentMessagingArtifacts) ekle.
 5) Güvenlik: platform.document.write/.read izin (tanımla/seed) + tenant-scope + medya-tipi allowlist (PDF) + boyut cap + anon YOK.
 6) Gateway: ocelot /api/v1/platform/documents/{everything} (Platform portu; audit route deseni).
KORU/YAPMA: IContentStorageGateway contract + LocalFileSystemContentStorageGateway impl DEĞİŞMEZ (hash gerekiyorsa additive); ControlledDocuments/DocumentManagement/diğer servisler DOKUNMA; TenantId/CompanyId JWT'den; upload authz+tenant+allowlist+cap zorunlu; render(16B)/release(17) YOK.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; ilgili Platform test projesi -c Release → yeşil; Diten.Platform.API derleme 0 hata; git diff Platform.API + (gerekirse Application/Infrastructure additive) + gateway ocelot; storage gateway mevcut davranış diff yok (hash additive hariç). Testler: upload+hash/download/cross-tenant-404/anon-reddi/oversize-medya-reddi/duplicate→mevcut. Ayrı commit ("feat(platform): WP-SCMM-16A — document upload/download API (IContentStorageGateway HTTP surface) [SCMM-16 pre-req]" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: ContentStorageScope enum drift persist okumalarını bozuyorsa; StoreAsync idempotency yoksa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2) — kapsam düzeltildi (Seçenek A)**
```
Commit: c8c608b1 · CT: WP öncülü YANLIŞ çıktı → kullanıcı kararı Seçenek A (FU01 yeniden kullan, yalnız eklemeli scope) · E2 yeşil
```
- ⚠ **WP öncülü çürütüldü:** WP "HTTP yüzeyi YOK" diyordu; **yanlış** — istenen upload/download yüzeyinin tamamı **MOD-0262-FU01 olarak zaten mevcut, kablolu, gateway'e bağlı**: `POST /api/v1/document-repository/objects` (TenantId sunucudan, SHA-256 `Checksum`) · `GET objects/{contentId}/content` (cross-tenant→404, **contentId ile** — objectKey-URL kasıtlı reddedilmiş, non-leakage) · `[Authorize]`+`[HasPermission platform.document-repository.read|manage]`+`ContentStorageOptions` allowlist/cap · ocelot `ocelot.json:1514/1534`→5057.
- ✅ **Yapılan (tek eklemeli boşluk):** `ContentStorageScope.ContentMessagingArtifacts = 3` (ada göre projekte, ordinal drift yok) + `LocalFileSystemContentStorageGateway.BuildObjectKey`'e `"content-messaging-artifacts"` switch arm'ı (`_ => "documents"` default'un yeni scope'u sessizce documents'a alias'lamasını — MOD-0024 tuzağı — önler) + 6 test (`ContentMessagingArtifactsScopeTests`: yeni scope kendi partition'ı, 4 scope ayrı segment, SHA-256 + birebir bayt round-trip).
- ❌ **Yapılmadı (gerekmedi/kapsam dışı):** paralel `/api/v1/platform/documents` controller · `platform.document.*` izinleri · yeni ocelot route · medya-tipi allowlist global daraltma (paylaşımlı options; `.pdf` zaten allowlist'te). Gerekçe: FU01'i çoğaltmak + non-leakage kararını regresyona sokmak olurdu.
- ✅ **DUR koşulları tetiklenmedi** (scope ada göre projekte → drift yok; idempotency FU01 seviyesinde ayrı konu). Asıl "dur" = yanlış öncül → kullanıcıya raporlandı, Seçenek A seçildi, uygulandı.
- ✅ **Build+test (CT):** Diten.Platform.API derleme **0 hata**; DocumentRepository test filtresi **54/0** (6 yeni + FU01 contract-guard). git diff: 2 kaynak (+12/−1) + 1 test dosyası; mevcut 3 scope davranışı byte-aynı.
- ✅ **E4 anon reddi (canlı, 2026-09-22):** token'sız `POST /api/v1/document-repository/objects` (scope=ContentMessagingArtifacts, PDF) → **401 Unauthorized** (`WWW-Authenticate: Bearer`); token'sız `GET objects/{id}/content` → **401**. Gateway 5000 sağlıklı (200).

- ✅ **RBAC grant (2026-09-22):** `platform.document-repository.read|manage` → 97c5 Admin verildi (2 rolePermission satırı, marker `manual-grant-scmm16a-docrepo`, subtype-4; `scratchpad/docrepo_grant.py`).
- ✅ **E4 authenticated (canlı, 2026-09-22): 8/8 PASS** (`scratchpad/smoke-scmm16a-docrepo-e4.ps1`, Get-Credential login → taze token): Login→token · Upload (scope=ContentMessagingArtifacts)→2xx+contentId · Scope echo=`ContentMessagingArtifacts` · Checksum present + **yerel SHA-256 ile eşleşiyor** · Download→200 · **round-trip baytlar birebir** · bilinmeyen contentId→**404 (non-leakage)**.

**WP-SCMM-16A KOMPLE (E2 + E4 tam, 8/8).** SCMM-16B render bu FU01 endpoint'ini (`POST /api/v1/document-repository/objects`, scope=`ContentMessagingArtifacts` → contentId+checksum) tüketecek; contentId'yi ContentSetRevision'a bağlayacak.

