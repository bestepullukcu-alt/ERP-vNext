# MOD-0031 kanıt bağlama — dedupe ölçümü 01 (salt okuma, 2026-10-29)

Ölçen: Treasury Intake CT (oturum `f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Ölçülen sürüm: `origin/main@317a48b13`.
Yöntem: kod okuma. **Çalışma testi yapılmadı**; aşağıdaki testler okundu, koşulmadı.

Soru (paketler): aynı uzak anahtarla `POST /api/v1/evidence/links` tekrarı tek bağlantı mı bırakır?
MOD-0126 (§ kapanış tablosu, DL-3), MOD-0124 (DL-4, O-17), MOD-0119 ve MOD-0120 (DL-3) bu ölçümü kurtarmada yeniden
çağrının önkoşulu sayar.

## Olgular

Yollar `services/Diten.Platform/src/Diten.Platform.` altındadır.

1. **İçerik tabanlı tekillik var.** Anahtar: nesne (`Module`, `ObjectType`, `ObjectId`, `ObjectVersion`) + `DocumentId` +
   `DocumentVersionId` + `Locator.Page` (kırpılmış, küçük harf) + `Locator.Quote` (kırpılmış) → SHA-256
   (`Application/Features/EvidenceLinking/EvidenceLinkingHandlers.cs:28-35`).
2. **Garanti depoda.** `{TenantId, ActiveDedupKey}` üzerinde, yalnız `Status == active` için kısmi UNIQUE indeks
   `ux_evidence_links_tenant_active_dedup` (`Infrastructure/Persistence/Schema/PlatformSchemaManifest.EvidenceLinking.cs:34-39`).
3. **Tekrar → 409 `duplicate_link`, ikinci bağlantı ve olay yok.** Ön kontrol (`EvidenceLinkingHandlers.cs:264-274`) ve
   yarış durumunda indeks hatasının çevrilmesi (`:307-311`). Testler:
   `tests/Diten.Platform.Application.Tests/EvidenceLinking/EvidenceLinkingTests.cs:384-396` (aynı içerik 409, başka sayfa
   201) ve `:398-414` (depo düzeyi yarış 409, olay yok).
4. **Çağıranın uzak anahtarı yok.** İstekte idempotency ya da dış anahtar alanı yoktur
   (`API/Models/EvidenceLinking/EvidenceLinkRequests.cs:11-18`). MOD-0124'ün `RemoteKey`'i MOD-0031'e gitmez; tekillik
   yalnız aynı içerik (aynı sayfa ve alıntı) gönderilirse çalışır. `Locator.Quote` zorunludur (`EvidenceLinkingHandlers.cs:105-109`).
5. **409 yanıtı var olan bağlantının kimliğini taşımaz** (`:272-273`). Kimlik okuma ile bulunur: `POST links/query`
   (≤100 nesne, `API/Controllers/EvidenceLinksController.cs:72-80`) ya da `GET links?module=…` (`:60-68`).
6. **Silinen bağlantı anahtarı serbest bırakır:** arada silinen bağlantıda tekrar yeni bir bağlantı oluşturur (test `:443-444`).
7. **503:** işlem açılamazsa "hiçbir şey yazılmadı" (`EvidenceLinkingHandlers.cs:312-315`) — kesin sonuç, belirsiz değil.
8. İzinler: yazma `platform.evidence.links.manage`, okuma `platform.evidence.links.read` (`EvidenceLinkingModels.cs:10-11`).

## Çıkarım (karar değil)

- Paketlerin sorusunun yanıtı **koşullu evet**: bağdaştırıcı her denemede aynı nesne, belge, sürüm, sayfa ve alıntıyı
  gönderirse tekrar ikinci bağlantı bırakmaz (409). Yeniden çağrı ancak şu üç kuralla güvenli olur:
  (a) bağdaştırıcının gönderdiği locator deterministik; (b) 409 `duplicate_link` = "zaten kayıtlı" → kimlik
  `links/query` ile okunur → `Recorded`; (c) silinmiş bağlantı için yeniden çağrı yeni bağlantı açar — kurtarma bunu
  yalnız okuma sonrası, bağlantı yoksa yapar.
- Bu ölçüm **DL-1'i etkilemez**: madde MOD-0126'da DL-3, MOD-0124'te DL-4 dilim kapısıdır.
- Pakette kutu işaretlemek ve yeniden çağrıyı açmak paket değişikliğidir (yukarıdaki üç kural pakete yazılır) → sahip
  kararı ve kısa yeniden inceleme gerekir. O zamana dek paketlerin mevcut kuralı geçerlidir: kurtarma MOD-0031'e yeniden
  yazmaz, yalnız okur.
