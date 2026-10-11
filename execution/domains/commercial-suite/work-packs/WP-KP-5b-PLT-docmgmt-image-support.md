# WORK PACKAGE — WP-KP-5b-PLT · Belge Yönetimi'nde görsel desteği (türler, boyut, küçük resim, görsel belge kaydı) — Platform

> **CT (SoR), 2026-10-02.**
> - **Karar:** `docs/decisions/DEC-SCMM-05-brand-kit-approved-asset-placement.md` — görsel dosyaları, sürümleri ve önizleme / türevleri **Platform Belge Yönetimi**'nde (MOD-0262 dosya deposu + MOD-0028 / 0029 belge + sürüm); onay ve kullanım kuralları İçerik Stüdyosu'nda (KP-5b, CRM).
> - **Mockup + analiz:** `mockups/kp-5b-brand-assets/KP-5b-mockup-analysis.md` (E9; K-5: İçerik Stüdyosu'ndan yükleme dosyayı Belge Yönetimi'ne kaydeder + mevcut belgeyi seçme).
> - **Paralel paket:** WP-KP-5b (CRM) — bu paketin §Sözleşme'sini sahte istemciyle kullanır.
> - **Kapsam:** yalnız `services/Diten.Platform` (+ testleri). Web / CRM DOKUNMA.
>
> **Çalışma yeri:** worktree `C:\tmp\kp-5b-plt`, dal `wp/kp-5b-plt`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Kanıt (CT + analiz alt ajanı)
- **Dosya deposu (MOD-0262-FU01):** `Application/Contracts/DocumentRepository/{IContentStorageGateway.cs (:33-59, alanlar :65-80), ContentStorageOptions.cs (:25 256 MB, :27-28 izinli uzantılar — .svg / .webp YOK)}`, tek uygulama `Infrastructure/Services/DocumentManagement/LocalFileSystemContentStorageGateway.cs` (medya tipi yükleyiciden alınıyor, **içerik doğrulanmıyor** :50-58; kök `wwwroot` altında değil → herkese açık adres yok).
- **Dizin kaydı:** `Domain/Entities/DocumentRepository/RepositoryObject.cs:25-55` (kiracı, `Scope`, `OwningItemId / VersionId`, `MediaType`, `ByteSize`, SHA-256) — **genişlik / yükseklik / türev yok**. API `API/Controllers/DocumentRepositoryController.cs:34-111` (`POST objects`, `GET objects/{id}`, `GET objects/{id}/content`), izin `platform.document-repository.read / manage`, başka kiracı → 404.
- **Açık:** kontrollü belge sürümleri dizine yazılmıyor — `Features/DocumentManagementControlledDocuments/Services/DocumentVersioningService.cs:65` depolamayı doğrudan çağırıyor → sürümün içeriği `objects/{id}/content` ile açılamıyor.
- **Kontrollü belge:** `ControlledDocument` (`Title`, `DocumentType`, `Description`, `Tags[]`, tarihler, `CurrentVersionId`, klasör / koleksiyon bağı), `ControlledDocumentVersion` (`FileRef` + `MediaType`, checksum, `VersionStatus`). `DocumentType` = Sop / WorkInstruction / Policy / Form / Template / Other (`Domain/Enums/DocumentManagement/ControlledDocumentEnums.cs:6-14`) — **görsel türü yok**.
- **Kayıt (Record) türü:** `ControlledDocumentRegistrationService.cs:552-555` — Record anında `Effective`, QMS onayı yok (tanıtım görseli için uygun: onay İçerik Stüdyosu'nda).
- **Erişim:** `DocumentAccessEvaluator.cs` (klasör / erişim matrisi) — iki katman (izin + klasör hakkı).
- **Web önizleme:** `frontend/Diten.Web/Controllers/DocumentManagementControlledDocumentsController.cs:166-192` bayt akışı (yetkili proxy) — değişmez.
- **Görsel kütüphanesi yok:** Platform'da ImageSharp / SkiaSharp / System.Drawing referansı yok.

## NE
1. **Görsel türleri + içerik doğrulaması:**
   - izinli uzantılara `.svg`, `.webp` (yapılandırmadan; mevcut liste korunur);
   - görsel türlerinde **içerik imzası** (PNG / JPEG / WEBP magic bytes; SVG = geçerli XML kökü `svg`) — beyan edilen medya tipiyle uyuşmazsa 400 `media_type_mismatch`;
   - **SVG güvenliği:** `script`, `foreignObject`, `on*` öznitelikleri, `javascript:` ve dış kaynak (`href` http/https/data dışı) içeren SVG **reddedilir** (400 `svg_unsafe`); SVG yanıtları `Content-Type: image/svg+xml` + `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'` + `X-Content-Type-Options: nosniff`.
2. **Boyut metadata'sı:** yüklemede raster için başlıktan, SVG için `width / height / viewBox`'tan genişlik × yükseklik; `RepositoryObject`'e `ImageWidth? / ImageHeight?` (eski kayıtlar null; class-map / şema geriye uyumlu).
3. **Türevler (küçük resim / önizleme):**
   - raster görsel yüklenince iki türev: `thumbnail` (uzun kenar 320 px) ve `preview` (uzun kenar 1280 px), WEBP ya da PNG; türev ayrı bir dizin kaydı (`DerivedFromObjectId`, `RenditionKind`) — aynı kiracı, aynı erişim;
   - SVG için raster türev yok: türev istendiğinde temizlenmiş SVG'nin kendisi döner;
   - **kütüphane: SkiaSharp (MIT).** ImageSharp **kullanılmaz** (ticari lisans koşulu). Başka bir kütüphane gerekiyorsa DUR.
   - türev üretimi başarısızsa yükleme başarısız sayılmaz; türev yok işareti + günlük (sonra yeniden üretim ucu).
4. **Kontrollü belge sürümleri dizine:** `DocumentVersioningService` yeni sürüm dosyasını **dizin kaydıyla** yazar (`OwningItemId = documentId`, `VersionId`), görselse boyut + türevler üretilir. Eski sürümler için geri doldurma YOK.
5. **Görsel belge kaydı (İçerik Stüdyosu yükleme yolu):**
   - `DocumentType.Image` eklenir (mevcut değerler aynı);
   - `POST api/v1/document-management/images` (multipart: dosya, başlık, açıklama, etiketler) → kiracının **"Tanıtım görselleri"** koleksiyon / klasöründe **Record** türünde kontrollü belge + v1 (dizinli, türevli). Koleksiyon yoksa ilk yüklemede **idempotent** oluşturulur (kiracı başına tek; ad 7 dilde L10n anahtarı);
   - `POST api/v1/document-management/images/{documentId}/versions` → yeni sürüm (aynı kurallar);
   - yanıt: `documentId, documentCode, versionId, versionLabel, mediaType, byteSize, checksum, width, height, hasThumbnail, hasPreview`;
   - yetki: mevcut kontrollü belge oluşturma / sürüm izinleri + klasör hakkı (yeni izin anahtarı açma; eksikse raporla).
6. **Okuma uçları (yetki = mevcut belge erişim değerlendiricisi):**
   - `GET api/v1/document-management/controlled-documents/{id}/versions/{versionId}/image-info` → boyut, tür, checksum, türev var mı;
   - `GET …/versions/{versionId}/renditions/{thumbnail|preview|original}` → bayt akışı (SVG başlıkları §1);
   - belge seçenek listesi (`controlled-documents` liste ucu ya da `evidence/document-options`) `mediaType=image` / `documentType=Image` filtresi.

## KORU / YAPMA
- Mevcut belge türleri, QMS yaşam döngüsü, onay rotaları, erişim matrisi, mevcut uçların yanıt şekli DEĞİŞMEZ (yalnız ek alan / ek uç).
- Herkese açık adres YOK (dosyalar yine yalnız yetkili akışla).
- Veri göçü / geri doldurma YOK. RBAC / seed'e yazma YOK (koleksiyonun ilk kullanımda oluşturulması uygulama davranışı; idempotent).
- ImageSharp YOK; yeni NuGet yalnız SkiaSharp (+ platform yerel paketi).
- **DUR:** SkiaSharp Windows / Linux dağıtımında yerel bağımlılık sorunu çıkarırsa; mevcut kayıt servisi Record + görsel türünü kabul etmek için QMS kuralını değiştirmeyi gerektirirse; koleksiyonun otomatik oluşturulması mevcut koleksiyon tanım sözleşmesine aykırıysa → raporla.

## Sözleşme (KP-5b CRM ve KP-5b-UI ile paylaşılan)
| Uç | Yanıt / not |
|---|---|
| `POST api/v1/document-management/images` (multipart `file`, `title`, `description?`, `tags?`) | 201 `{documentId, documentCode, versionId, versionLabel, mediaType, byteSize, checksum, width, height, hasThumbnail, hasPreview}`; 400 `media_type_mismatch` / `svg_unsafe` / `file_type_not_allowed` / `file_too_large` |
| `POST api/v1/document-management/images/{documentId}/versions` | aynı yanıt |
| `GET api/v1/document-management/controlled-documents/{id}/versions/{versionId}/image-info` | `{documentId, versionId, mediaType, width, height, checksum, hasThumbnail, hasPreview, isImage}`; erişim yok → 403 / 404 |
| `GET …/versions/{versionId}/renditions/{thumbnail\|preview\|original}` | bayt akışı; türev yoksa `original`'a düşmez → 404 `rendition_not_found` |
| Belge seçenekleri `?mediaType=image` | yalnız görsel belgeler, en güncel sürüm + `isImage` |

## Acceptance
- **E2:** Platform testleri 0 **yeni** kırmızı (taban ölç; ~173 ortam kaynaklı kırmızı — yalnız fark), Web 0 kırmızı (dokunulmadı), build 0 hata.
  - **Yeni testler:** magic-byte eşleşmesi / uyuşmazlık; SVG temizlik reddi (script, foreignObject, onload, javascript:, dış href) + güvenlik başlıkları; boyut çıkarımı (PNG / JPEG / WEBP / SVG); türev üretimi ve boyutları; türev hatası yüklemeyi bozmaz; sürüm dizine yazılıyor (`objects/{id}/content` açılıyor); görsel kaydı → Record + Image + koleksiyon idempotent (iki yükleme tek koleksiyon); kiracı izolasyonu (başka kiracının görseli / türevi 404); erişim değerlendiricisi türev uçlarında da uygulanıyor; seçenek listesi görsel filtresi; mevcut belge testleri yeşil.
  - **Sabotaj:** (1) SVG `script` kontrolü kaldırılınca temizlik testi kırmızı; (2) türev ucunda erişim kontrolü kaldırılınca yetki testi kırmızı.
- **E4 (CT):** KP-5b-UI sonrası, yükleme → galeride küçük resim.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-KP-5b-PLT · Belge Yönetimi'nde görsel desteği (türler, boyut, küçük resim, görsel belge kaydı) — Platform
Repository: C:\tmp\kp-5b-plt (worktree) · Branch: wp/kp-5b-plt · commit bu dala, push YOK · yalnız services/Diten.Platform (+ testleri)

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-KP-5b-PLT-docmgmt-image-support.md — önce tamamını oku (Kanıt / NE / KORU / Sözleşme / Acceptance). Ayrıca: docs/decisions/DEC-SCMM-05-brand-kit-approved-asset-placement.md · execution/domains/commercial-suite/work-packs/mockups/kp-5b-brand-assets/KP-5b-mockup-analysis.md (E9, §7) · services/Diten.Platform/src/Diten.Platform.Application/Contracts/DocumentRepository/** · Infrastructure/Services/DocumentManagement/LocalFileSystemContentStorageGateway.cs · Domain/Entities/DocumentRepository/RepositoryObject.cs · API/Controllers/DocumentRepositoryController.cs · Features/DocumentManagementControlledDocuments/Services/{DocumentVersioningService, DocumentAccessEvaluator}.cs · Features/DocumentManagementControlledDocumentRegistration/** · Domain/Enums/DocumentManagement/ControlledDocumentEnums.cs · memory mongo-partial-index-ne-crash, platform-partial-index-ne-crash.

NE: (1) .svg/.webp izinli (yapılandırma); görsel türlerinde içerik imzası (magic bytes / SVG kökü) → 400 media_type_mismatch; SVG güvenliği: script/foreignObject/on*/javascript:/dış href → 400 svg_unsafe; SVG yanıtlarında CSP + nosniff. (2) RepositoryObject ImageWidth?/ImageHeight? (raster başlık, SVG width/height/viewBox). (3) Türevler thumbnail (320) + preview (1280), ayrı dizin kaydı (DerivedFromObjectId, RenditionKind); SVG türevi = temiz SVG; SkiaSharp (MIT) — ImageSharp YOK; türev hatası yüklemeyi bozmaz. (4) DocumentVersioningService yeni sürüm dosyasını dizin kaydıyla yazar (+ görselse boyut/türev); geri doldurma YOK. (5) DocumentType.Image; POST api/v1/document-management/images (multipart) → kiracının "Tanıtım görselleri" koleksiyonunda Record + v1 (koleksiyon ilk kullanımda idempotent); POST …/images/{documentId}/versions; yanıt WP §Sözleşme. (6) GET …/versions/{versionId}/image-info ve …/renditions/{thumbnail|preview|original} (mevcut erişim değerlendiricisiyle); belge seçenek listesine mediaType=image filtresi.
KORU/YAPMA: mevcut tür/QMS yaşam döngüsü/onay rotaları/erişim matrisi/uç yanıtları değişmez (yalnız ek); herkese açık adres yok; veri göçü yok; RBAC/seed yazımı yok; Web/CRM DOKUNMA.
DOĞRULA (E2): Platform testleri (tabanı ölç; ~173 ortam kaynaklı kırmızı — yalnız FARK) → 0 yeni kırmızı; dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı; build 0 hata. Yeni testler WP Acceptance. Sabotaj: (1) SVG script kontrolünü kaldır → kırmızı; (2) türev ucunda erişim kontrolünü kaldır → kırmızı; geri al. Commit ("feat(platform): WP-KP-5b-PLT — document management image support (types, signature check, svg sanitising, dimensions, renditions, image records)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: SkiaSharp yerel bağımlılık sorunu; Record+görsel için QMS kuralı değişmek zorunda; koleksiyon otomatik oluşturma sözleşmeye aykırı → DUR + raporla.
```
