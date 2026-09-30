# WORK PACKAGE — WP-CL-BE-2 · MOD-0031 Evidence Linking en küçük dilim (kanıt bağı · sabit sürüm · konum · desteklenen ifade · ters sorgu) (Platform, backend)

> **CT (SoR).** İddialar v2 Faz 1. Kaynaklar:
> - `SCMM-claims-v2-mockup-analysis-plan.md` (CL-BE-2, K7/K8, kullanıcı kararı **D5 = MOD-0031 ortak servis**)
> - `SCMM-claims-approval-evidence-roadmap.md` §B
> - Module pack: `platform-shared-services/module-packs/MOD-0031-evidence-linking-service.md` (yalnız şartname, üretimde %0)
>
> **Amaç:** Herhangi bir yönetişimli nesne (ilk tüketici CRM İddia) Belge Yönetimi'ndeki bir **kontrollü belgenin belirli sürümüne** ya da bir **harici belgeye** kanıt olarak bağlanabilsin. Bağ şunları taşır: kanıt tipi, konum bilgisi, alıntı, desteklenen ifade. Bağ geçmişi değişmez olsun. "Bu belgeyi kim kullanıyor?" sorusu tek sorguyla cevaplansın.
>
> **Kapsam:** Platform backend + gateway route. **Kanıt Paneli arayüzü bu WP'de YOK** (CL-FE-5). CRM DEĞİŞMEZ.
>
> **Çalışma yeri:** worktree `C:\tmp\cl-be-2`, dal `wp/cl-be-2`. Commit bu dala, push YOK.

## Kanıt (CT kod okuması)
- **Kontrollü belge:**
  - `Platform.Domain/Entities/DocumentManagement/ControlledDocument.cs`: Title, DocumentType, CompanyId, FolderId, EffectiveDate…
  - `ControlledDocumentVersion.cs`: DocumentId, **VersionNumber**, FileRef, Checksum, **VersionStatus**, DeletedAt.
- **Harici belge (literatür, mevzuat kaynağı):** `ExternalDocumentRegisterEntry.cs`: ExternalDocumentCode, Title, Type, CountryCode, **SourceVersion**, SourceStatus, izleme alanları.
- **Erişim:** DocMgmt klasör erişim matrisi (MOD-0029 FU04) var. Bilinen tuzak: company claim yokken liste boş dönüyor (memory `mod0029-fu04-collection-instances-empty-regression`, `CanListFolderAsync` düzeltmesi). Kanıt servisi **mevcut DocMgmt erişim değerlendiricisini** kullanmalı.
- **Outbox:** `Contracts/Eventing/ITransactionalOutboxEventWriter.cs` (bkz. WP-CL-BE-3 ile aynı altyapı).
- **Gateway:** `/api/v1/document-management/{everything}` → 5057 var. **`/api/v1/evidence` route'u YOK** → eklenecek.
- **Referans:** `evidence-type` seti CL-REF-1 ile gelir (Global). Kodlar: `smpc-pil`, `clinical-study`, `literature`, `internal-data`, `regulatory-letter`. Bu WP'de doğrulama BRD okuyucusuyla yapılır. Set yoksa **400 `reference_set_missing`**; sessiz kabul YOK.

## NE
1. **Domain — `EvidenceLink`** (`TenantScopedEntity`, koleksiyon `evidence_links`):
   - **ObjectRef** `{ Module (≤32, ör. "crm"), ObjectType (≤64, ör. "claim" / "claim-country-version"), ObjectId (≤64), ObjectVersion (≤32, opsiyonel) }`.
   - **Belge:** `DocumentKind` (`controlled` | `external`). `DocumentId`. `DocumentVersionId` (controlled için zorunlu = ControlledDocumentVersion id; external için null). `DocumentVersionLabel` snapshot (ör. "v5" ya da harici SourceVersion). `DocumentTitle` snapshot.
   - **Kanıt:** `EvidenceTypeCode`.
   - **Locator:** `{ Section (≤120), Page (≤20), Table (≤60), Quote (**zorunlu**, ≤1000) }`.
   - **SupportedSpans** `[ { LanguageCode, Text (≤500), Start?, End? } ]` (≤10). İddia metninde bu kanıtın desteklediği ifade.
   - **Durum:** `Status` (active | removed). `LinkedBy/At`. `RemovedBy/At/Reason`.
   - **Değişmez:** güncelleme ucu yok. Düzeltme = kaldır + yeni bağ.
   - **İndeksler:** `{TenantId, ObjectRef.Module, ObjectRef.ObjectType, ObjectRef.ObjectId}`, `{TenantId, DocumentId}`, `{TenantId, DocumentVersionId}`.
2. **API — `api/v1/evidence`** (thin controller, MediatR, `Response<T>`, `[HasPermission]`):
   - `POST links` → bağ oluştur. Doğrulamalar:
     - Belge bu tenant'ta var ve **çağıran için okunabilir** (DocMgmt erişim değerlendiricisi).
     - Controlled ise sürüm bu belgeye ait ve silinmemiş.
     - Evidence type BRD'de geçerli.
     - Quote zorunlu.
     - **Tekrar kontrolü:** aynı nesne + belge + sürüm + sayfa + alıntı ile aktif bir bağ varsa → **409**.
   - `POST links/{id}/remove` `{reason (zorunlu)}` → kaldır (kayıt kalır).
   - `GET links?module=&objectType=&objectId=&objectVersion=&includeRemoved=` → nesnenin kanıtları.
   - `GET links/{id}`.
   - `GET links/by-document/{documentId}?versionId=` → **ters sorgu**: belgeyi (ya da sürümünü) kullanan aktif bağlar.
   - `GET document-options?search=&kind=controlled|external&take=20` → **seçici kaynağı**:
     - Controlled belge satırı: başlık, tip, geçerli son sürüm id/etiket, durum, etkinlik tarihi.
     - Harici belge satırı: kod, başlık, ülke, SourceVersion, SourceStatus.
     - **Yalnız çağıranın okuyabildiği** belgeler döner.
   - **İzinler:** `platform.evidence.links.read`, `platform.evidence.links.manage`. Anahtarlar Platform permission auto-registration ile kaydolur. **Grant bu WP'de YOK** (Faz 4).
3. **Olaylar** (transactional outbox): `platform.evidence.link.created` (v1) ve `platform.evidence.link.removed` (v1). Payload: tenant, linkId, ObjectRef, documentKind, documentId, documentVersionId, evidenceTypeCode. **Alıntı metni olaya girmez.**
4. **Gateway:** `gateway/Diten.ApiGateway/ocelot*.json` dosyasına `/api/v1/evidence/{everything}` → Platform 5057 (GET + POST, mevcut Platform route'larıyla aynı auth ayarları).
5. **Module pack güncellemesi:**
   - `MOD-0031-evidence-linking-service.md` → §"Current MVP execution status" altına bu dilim (entity, API, olaylar, açık kalanlar: Kanıt Paneli = CL-FE-5, tamlık kuralları, Evidence Register ekranı).
   - `execution/registries/module-implementation-status.md` → MOD-0031 satırı "Kısmi — backend dilim 1".

## KORU / YAPMA
- **Belge içeriği ya da dosyası kopyalanmaz** (pack: "no document storage duplication"). Yalnız id, sürüm ve snapshot etiket/başlık tutulur.
- DocMgmt varlıkları, API'leri ve erişim kuralları DEĞİŞMEZ; yalnız okunur.
- CRM, AuthService, frontend DOKUNMA.
- Tamlık/zorunluluk kuralları (ör. "onay için ≥1 kanıt") **tüketicide** kalır (CRM, CL-BE-5). Bu servis kural uydurmaz.
- Yeni base entity, repository base, eventing altyapısı YAZMA.
- **DUR:**
  - Yeniden kullanılabilir bir DocMgmt okuma-erişim değerlendiricisi yoksa → **fail-closed** davran (erişim doğrulanamıyorsa 403) ve raporla. Erişim kontrolünü atlama.
  - Ocelot dosyasında route düzeni belirsizse → mevcut `/api/v1/document-management/{everything}` bloğunu örnek al ve raporla.

## Acceptance
- **E2:** `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests -c Release --nologo` → **0 kırmızı**. Gateway ve Platform build 0 hata.
- **Yeni testler:**
  - Controlled bağ: sürüm başka belgeye ait → 400.
  - Silinmiş sürüm → 400.
  - Okuma yetkisi yok → 403.
  - Harici bağ: sürüm null, snapshot dolu.
  - Evidence type geçersiz → 400. Set yok → 400 `reference_set_missing`.
  - Quote boş → 400.
  - Tekrar → 409.
  - Kaldır: gerekçe zorunlu, kayıt kalır, `includeRemoved` ile görünür.
  - Ters sorgu yalnız aktif bağları döner.
  - Kiracı izolasyonu (başka tenant'ın bağı ve belgesi görünmez).
  - Oluşturma ve kaldırmada tam 1 outbox olayı; olayda alıntı yok.
  - `document-options` yalnız okunabilir belgeleri döner.
- **Diff:** `services/Diten.Platform/**` + `gateway/**/ocelot*.json` (+1 route) + MOD-0031 pack + registry satırı. CRM, Auth ve Web diff'i YOK.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-CL-BE-2 · MOD-0031 Evidence Linking en küçük dilim (kanıt bağı · sabit sürüm · konum · desteklenen ifade · ters sorgu) (Platform, backend)
Repository: C:\tmp\cl-be-2 (worktree) · Branch: wp/cl-be-2 (taban feature/crm-claims-v2) · commit bu dala, push YOK

Amaç: MOD-0031'in ilk üretim dilimi — yönetişimli bir nesne (ilk tüketici CRM İddia) Belge Yönetimi'ndeki kontrollü belgenin belirli SÜRÜMÜNE ya da harici belgeye kanıt olarak bağlanır (tip, konum, alıntı, desteklenen ifade); bağ geçmişi değişmez; belge üzerinden ters sorgu. Kanıt Paneli UI bu WP'de YOK.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-BE-2-mod0031-evidence-linking.md · execution/domains/platform-shared-services/module-packs/MOD-0031-evidence-linking-service.md · …/SCMM-claims-approval-evidence-roadmap.md §B · services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/{ControlledDocument,ControlledDocumentVersion,ExternalDocumentRegisterEntry}.cs · Features/DocumentManagement* içindeki okuma-erişim değerlendiricisi (klasör erişim matrisi FU04, CanListFolderAsync) · Contracts/Eventing/ITransactionalOutboxEventWriter.cs · gateway/Diten.ApiGateway/ocelot*.json (/api/v1/document-management/{everything} bloğu) · memory: mod0029-fu04-collection-instances-empty-regression, brd-catalog-loader-seed-path (global set = scope_key YOK).

NE:
 1) EvidenceLink (TenantScopedEntity, evidence_links): ObjectRef{Module,ObjectType,ObjectId,ObjectVersion?}, DocumentKind controlled|external, DocumentId, DocumentVersionId (controlled zorunlu), DocumentVersionLabel+DocumentTitle snapshot, EvidenceTypeCode, Locator{Section,Page,Table,Quote zorunlu}, SupportedSpans[{LanguageCode,Text,Start?,End?}] ≤10, Status active|removed, LinkedBy/At, RemovedBy/At/Reason; güncelleme ucu yok; indeksler WP'deki gibi.
 2) api/v1/evidence: POST links (belge tenant'ta var + çağıran okuyabiliyor [DocMgmt erişim değerlendiricisi] + sürüm belgeye ait/silinmemiş + evidence-type BRD'de geçerli [set yoksa 400 reference_set_missing] + quote zorunlu + aynı nesne/belge/sürüm/sayfa/alıntı aktif varsa 409) · POST links/{id}/remove {reason zorunlu} · GET links?module&objectType&objectId&objectVersion&includeRemoved · GET links/{id} · GET links/by-document/{documentId}?versionId · GET document-options?search&kind&take (yalnız okunabilir belgeler). İzinler platform.evidence.links.read / .manage (auto-registration; grant YOK).
 3) Outbox olayları platform.evidence.link.created / .removed v1 (alıntı metni YOK).
 4) Ocelot: /api/v1/evidence/{everything} → 5057 (GET+POST, mevcut Platform auth ayarı).
 5) MOD-0031 pack'ine bu dilimin durumu + registry satırı "Kısmi — backend dilim 1".
KORU/YAPMA: belge içeriği/dosyası kopyalanmaz; DocMgmt varlık/API/erişim kuralları DEĞİŞMEZ (yalnız okuma); CRM/Auth/Web DOKUNMA; tamlık kuralı (≥1 kanıt) tüketicide — burada YOK; yeni base/repo/eventing altyapısı YAZMA.
DOĞRULA (E2): cd C:\tmp\cl-be-2; dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests -c Release --nologo → 0 kırmızı; gateway+Platform build 0 hata; yeni testler WP Acceptance listesindeki gibi (sürüm-belge uyuşmazlığı, silinmiş sürüm, 403, harici snapshot, evidence-type 400/set yok 400, quote boş, 409, kaldır+includeRemoved, ters sorgu yalnız aktif, kiracı izolasyonu, tam 1 olay alıntısız, document-options okunabilir filtre); git diff yalnız services/Diten.Platform/** + ocelot (+1 route) + MOD-0031 pack + registry satırı. Commit ("feat(platform): WP-CL-BE-2 — MOD-0031 evidence linking slice 1 (links, pinned version, locator, supported spans, reverse lookup)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: yeniden kullanılabilir DocMgmt okuma-erişim değerlendiricisi yoksa fail-closed (403) + raporla — erişim kontrolünü ATLAMA; ocelot düzeni belirsizse document-management bloğunu örnek al + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-28) → **ACCEPTED (E2)** — izin kapsamı Faz 4 notu
```
Commit: 28ba1df4 · Agent: PASS-with-note (Platform 4980/173 = taban 4954/173 + 26; gateway 83/1 önceden var) · CT: worktree C:\tmp\cl-be-2 → Platform 4980/173; EvidenceLinking 26/26
```
- ✅ **Kapsam:** 20 dosya, +2044/−1. Değişiklikler: `services/Diten.Platform/**` + `ocelot.json` (+1 route `/api/v1/evidence/{everything}` → 5057, document-management bloğuyla aynı biçim) + MOD-0031 pack + registry satırı. CRM, Auth ve Web diff YOK.
- ✅ **173 kırmızı = ortam.** TRX analizi: 163'ü `*MongoTests` sınıfında. Mesajlar: "local mongod binary is required" ×116, BSON `Timestamp` serileştirme ×49. Kalan 10'u da gerçek-mongod ya da harness testi (Tasks, BRD, Subscription alanları). **EvidenceLinking kırmızısı YOK**, bu WP'nin dokunduğu alanda kırmızı yok. WP'deki "0 kırmızı" kriteri taban ortam sorunu nedeniyle "yeni kırmızı yok" olarak kabul edildi.
- ✅ **Erişim:** kontrollü belgede mevcut `DocumentAccessEvaluator.CanReadControlledDocumentAsync` yeniden kullanılıyor (`EvidenceDocumentAccessGate`). Harici belgede `external-documents.view` izni ya da yönetici; aksi halde fail-closed.
- ✅ **Tekrar:** ön kontrol + **kısmi tekil indeks** (yalnız aktif bağlar). 3 sabotaj kanıtı.
- ⚠ **İzin kapsamı (CT teyit, bloker DEĞİL):**
  - `PlatformPermissionAutoRegistrationWorker.cs:31-52,78-80` — manifest atfı olmayan yeni `platform.*` anahtarı **PlatformAdmin** kapsamıyla kaydolur. Manifest senkronu bunu Tenant'a düşürmez (`InternalPermissionsController.cs:144-152`, en kısıtlayıcı kazanır).
  - **Emsal:** aynı kapsamdaki `platform.workflow.tasks.*` ve `platform.workflow.instances.*` anahtarları 97c5 tenant rollerine (Admin, GQD, QADocumentation, DocumentMasterRegisterLinker) **zaten verilmiş**; 42 rolePermission kaydı var, manuel grant script'leriyle. API tarafı `[HasPermission]` claim'e bakar.
  - **Faz 4'te** `platform.evidence.links.*` ile workflow anahtarları İddia rollerine **script ile** verilir. Kullanıcı çalıştırır.
  - Kalıcı çözüm isteğe bağlı: BL-411 `TenantRouteScopeCorrections` deseniyle tenant-self-service düzeltmesi. Ayrı iş, İddialar'ı bloklamaz.
- ℹ **Olay tanımları** `Platform.Application` içinde. CRM tüketecekse (CL-BE-5) `Platform.Contracts`'a taşınması değerlendirilecek.
- ℹ **Okuma kuralı:** yönetici olmayan kullanıcı yalnız master register'a bağlı ve **Effective** belgeleri görebilir ve bağlayabilir (DocMgmt yaşam döngüsü kuralı). Canlı testte bu doğrultuda belge hazırlanmalı.
- ℹ Gerçek Mongo'da kısmi indeks ve transaction davranışı canlı E4'te doğrulanacak.
