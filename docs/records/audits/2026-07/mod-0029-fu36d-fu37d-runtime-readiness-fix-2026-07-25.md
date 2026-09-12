# MOD-0029-FU36D/FU37D Runtime Readiness Fix — 2026-07-25

## 1. Summary

Verdict: **BLOCKED**. Language ve retention kök nedenleri hedefli olarak
düzeltildi; Controlled Documents legacy/Corporate liste projeksiyonu güvenli hale getirildi; tenant-scoped
Corporate fixture/grant ve bearer tabanlı HTTP fallback scriptleri hazırlandı; iki stale FU06 verifier güncellendi.
Platform/Web build, 14 targeted test ve 17 ilgili verifier yeşildir. Ancak gerçek authenticated tekrar smoke'u
credential/token yazılmadan bu oturumda çalıştırılamadı ve tam Application suite 1933 testten 12 mevcut regression
ile kırmızıdır. Final PASS, completion veya commit-readiness iddia edilmez.

## 2. Original blockers

1. Governed Language 403.
2. Retention Class 500.
3. Corporate Collection Instance listesi boş.
4. Controlled Documents listesi 500.
5. Node v16.20.2 nedeniyle browser automation yok.
6. İki FU06 verifier FU37 `draft` bekliyordu.
7. Authenticated final smoke tamamlanmamıştı.
8. Working tree birden fazla teslimatı içeriyordu.

## 3. Language lookup fix/evidence

Eski MVC proxy `/api/lookups/languages` endpoint'ine gidiyor ve genel `platform.lookups.read` iznini
gerektiriyordu. Form actor'ünün sahip olduğu doküman okuma yetkisi bu genel platform-admin lookup izniyle
örtüşmüyordu.

Yeni read-only contract:

- backend: `GET /api/v1/document-management/controlled-document-registrations/governed-languages`;
- permission: mevcut seeded
  `platform.document-management.controlled-documents.view`;
- frontend: yalnız aynı-origin MVC proxy üzerinden yeni contract'a gider;
- mutation, client TenantId, `X-Tenant-Id` ve free-text fallback yoktur.

Yetkisiz caller için `[Authorize]` + `[HasPermission]` 401/403 fail-closed davranışı korunur.

## 4. Retention lookup fix/evidence

`BusinessReferenceDataExceptionBehavior`, `Response<T>.Fail` metodunu iki parametreli reflection imzasıyla
arıyordu. Gerçek metot dört parametreli olduğundan `KeyNotFoundException` kontrollü 404'e çevrilemiyor ve global
handler'a kaçarak 500 oluyordu. Reflection çağrısı gerçek dört parametreli imzaya düzeltildi.

Kanıt:

- startup catalog doğrulaması `qms-document-retention` setini ve örnek değerini buluyor;
- eksik/published-version-yok koşulu artık 500 değil kontrollü 404 envelope'dur;
- available set 200 kontratı değişmedi;
- targeted exception testleri `reference_data_set_not_found` ve boş mesajı doğrular;
- UI free-text/fake retention fallback kullanmaz ve seçenek olmadan fail-closed kalır.

## 5. Controlled Documents list fix/evidence

Mongo read-only incelemesinde hedef tenant verisinde FU37 scope snapshot alanlarını taşımayan legacy
ControlledDocument bulundu. Liste wire modeli/projeksiyonu şu şekilde uyumlu hale getirildi:

- snapshot alanı olmayan kayıt `LEGACY` gösterilir;
- Company kaydı gerçek CompanyId'yi taşır;
- Corporate kayıt `CompanyId = null` döndürür; dummy `Guid.Empty` wire değeri üretmez;
- `ScopeOwnerId`, `CorporateOwnerId` ve `FolderId` yoksa null-safe döner;
- entity ve registration CompanyId kontratı nullable yapılmadı.

Legacy ve Corporate projeksiyon testleri yeşildir. Gerçek authenticated list 200 kanıtı tekrar smoke'a
devredilmiştir; bu nedenle bu madde runtime-verified olarak kapatılmamıştır.

## 6. Corporate fixture/grant readiness

Boş listenin FU06 access modelinde beklenen iki nedeni vardır: instance verisi yokluğu veya explicit grant
yokluğu. `scripts/prepare-mod0029-fu36d-fu37d-corporate-smoke-fixture.ps1` additive olarak hazırlandı:

- yalnız Gateway 5000 ve caller'ın SecureString administrator bearer token'ını kullanır;
- gerçek `BaselineReleaseId`, `CorporateOwnerId` ve `SmokeUserId` zorunludur;
- idempotent Corporate provisioning çağrısı yapar;
- tamamlanmayan operation'ı başarı saymaz;
- provision edilen gerçek collection node'larına explicit `View` + `CreateDocument` user grant'i ekler;
- TenantId veya `X-Tenant-Id` göndermez;
- dummy CompanyId, fake company, hard delete veya production-like toplu veri işlemi yapmaz.

Gerçek kimlikler ve authorized token bu koşuda verilmediği için fixture mutate edilmedi.

## 7. Stale verifier maintenance

`verify-mod0028-fu06-runtime-smoke-reconciliation.ps1` ve
`verify-mod0028-fu06-mongo-index-compatibility-fix.ps1`, FU37 için artık tarihsel `draft` değil:

- `status: ready-for-dev`;
- `runtime_implementation: implemented-with-runtime-gaps`

kontratını zorunlu tutar. Assertion kaldırılmadı; güncel governance state ve açık runtime gap birlikte
doğrulanır. FU37B verifier da dedicated governed-language route'una güncellendi.

## 8. Browser automation / Node fallback

Ortam: Node `v16.20.2`; browser automation minimum v22.22.0 koşulunu karşılamaz. Node yükseltmesi scope dışıdır.
`scripts/smoke-mod0029-fu36d-fu37d-runtime-readiness.ps1` SecureString bearer token ile Gateway üzerinden:

- governed languages;
- retention;
- Controlled Documents list;
- Corporate Collection Instances list

problarını yapar. Node <22 durumunu server bug/fake fail olarak değil açık browser gap'i olarak raporlar.
Scriptler parser kontrolünden `0` hata ile geçti.

## 9. Build/test/verifier results

| Kontrol | Sonuç |
|---|---|
| Platform API isolated build | PASS, 0 error |
| Web isolated build | PASS, 0 error; 14 mevcut warning |
| Targeted readiness/FU37C tests | PASS, 14/14 |
| Full Application tests | FAIL, 1921 pass / 12 fail / 0 skip |
| FU06 + FU36A/B/C + FU37A/B/C/D verifier set | PASS |
| FU24–FU29 UI verifier set | PASS |
| Toplam ilgili verifier | PASS, 17/17 |
| PowerShell smoke/fixture parser | PASS |

Tam suite failure'ları bu fix'in targeted testlerinde değildir: mevcut training/release/approval beklenti
sapmaları ve `Mod0029Fu29aEndpointAttributionTests` çoklu permission attribute reflection problemidir. Bu task
scope'unda business logic rewrite yapılmadı.

## 10. Runtime smoke retry result

Platform hot reload backend değişikliklerini build-success ile uyguladı ve loglarda daha önce 500 olan missing
reference-data koşulunun 404'e döndüğü görüldü. Credential/token loglamama kuralı nedeniyle authenticated
fallback script bu oturumda koşturulmadı. Language 200, Controlled Documents 200, Corporate accessible instance
ve final Company/Corporate Completed kanıtları hâlâ tekrar smoke gerektirir.

## 11. Guardrails

- AuthService seed, Gateway/Ocelot ve MOD-0028 provisioning business logic değiştirilmedi.
- Client TenantId / `X-Tenant-Id` eklenmedi.
- Direct browser 5057 kullanılmadı; script yalnız Gateway 5000 kullanır.
- Free-text/fake lookup fallback yok.
- Dummy/nullable entity CompanyId yok.
- Hard delete yok.
- Non-Completed operation başarı sayılmaz.
- Credential/token dosyaya veya loga yazılmadı.
- Commit, push ve `git add .` yapılmadı.

## 12. Remaining gaps

1. Authorized bearer ile HTTP fallback smoke'u çalıştırıp dört endpoint'in gerçek status/body kanıtını kaydetmek.
2. Gerçek baseline/owner/user ile Corporate fixture/grant'i hazırlayıp actor listesinde en az bir instance görmek.
3. Company ve Corporate registration'ın `COMPLETED`, retry/idempotency, manual-link ve reverse-navigation smoke'unu
   yeniden çalıştırmak.
4. Node 22+ ortamında görsel browser smoke yapmak.
5. Full Application suite'teki 12 mevcut failure'ı ayrı yetkili regression task'ında kapatmak.

## 13. Files changed

Bu task; BusinessReferenceData exception mapping, dedicated governed-language endpoint/proxy, Controlled Documents
list modeli/projeksiyonu, targeted tests, FU06/FU37B verifiers, iki readiness scripti ve bu audit dosyasını değiştirdi.
FU37C test fixture'ındaki eksik xUnit/required entity alanları full-suite compile için tamamlandı.

## 14. Next recommendation

Önce SecureString bearer ile HTTP fallback smoke'u çalıştırın. Corporate liste boşsa gerçek
BaselineReleaseId/CorporateOwnerId/SmokeUserId ile fixture scriptini yetkili token altında çalıştırın; sonra aynı
smoke'u tekrar edin. Dört read blocker 200/kontrollü 404 ve Corporate erişimi kanıtlandıktan sonra DCP-004 Phase 6
`READY_FOR_RETRY` olarak değerlendirilebilir. Tam suite kırmızı ve final registration smoke eksik olduğu sürece
commit planı/commit-ready veya completed statüsü verilmemelidir.

Commit separation sırasında açık path listesi ve mixed registry/DCP dosyalarında `git add -p` kullanılmalı;
`git add .` yasaktır ve unrelated CRM/HCM değişiklikleri dışarıda kalmalıdır.
