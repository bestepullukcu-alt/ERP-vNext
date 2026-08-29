# MOD-0151-FU09A — Authenticated Gateway Live Smoke Retry

> Tarih: 2026-08-02  
> Tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`  
> Kapsam: Mevcut FU09A salt-okunur readiness endpointlerinin Gateway üzerinden authenticated canlı smoke retry doğrulaması  
> Sonuç: **FAIL — yetkili tenant tokenı üretilemedi; beş authenticated 200/payload smoke tamamlanmadı**

## 1. Preflight

| Bileşen | Kontrol | Sonuç |
|---|---|---|
| Gateway | `GET http://localhost:5000/health` | **200 / PASS** |
| Web | `GET http://localhost:5001/` | **200 / PASS** (`/health` tanımlı değil, 404) |
| Auth | `GET http://localhost:5056/health` | **200 / PASS** |
| Platform | `GET http://localhost:5057/health` | **200 / PASS** |
| CRM | `GET http://localhost:5061/health` | **200 / PASS** |
| MongoDB | `127.0.0.1:27017` listener | **LISTENING / PASS** |

Doğrudan `5061` üzerinde yalnız `/health` çağrıldı. Hiçbir business API çağrısı doğrudan CRM portuna yapılmadı.

## 2. Previous PARTIAL

Referans rapor `mod-0151-fu09a-visit-route-readiness-implementation-2026-08-02.md` core API, contract, focused
testler, build ve Gateway alias erişimini doğrulamış; authenticated JSON payload smoke ise Chrome kontrol katmanındaki
`ERR_BLOCKED_BY_CLIENT` nedeniyle **PARTIAL** kalmıştı.

Bu retry yeni feature, route/visit planning veya frequency engine geliştirmedi. Amaç yalnız bu eksik canlı HTTP
kanıtını tarayıcıdan bağımsız kapatmaktı.

## 3. Retry Strategy

- PowerShell `Invoke-WebRequest` / `Invoke-RestMethod` kullanıldı; Chrome, extension ve ad-block katmanı devre dışı bırakıldı.
- Normal login endpointi kullanıldı: `POST http://localhost:5000/api/tenant-auth/login`.
- Login tenant seçimi yalnız `X-Tenant-Id` header'ı ile yapıldı; body içinde `TenantId` gönderilmedi.
- Token üretimi atlanmadı; JWT üretilmedi, taklit edilmedi, browser cookie/token çıkarılmadı.
- Tüm readiness business çağrıları Gateway `5000` alias'larına yöneltildi.
- Fixture/master data oluşturulmadı veya değiştirilmedi.

## 4. Authenticated Gateway Setup

Hedef operatör `bestepullukcu@gmail.com` için parola görev girdisinde, environment değişkenlerinde veya repo içindeki
güvenli smoke credential kaynağında bulunamadı. Repoda bulunan sabit local-development seed hash'i yalnız repoda
belgelenen development placeholder adaylarıyla yerel olarak karşılaştırıldı; eşleşen değer hiçbir çıktıya veya dosyaya
yazılmadan normal hedef-operatör login'inde bir kez kullanıldı. Sonuç **401** oldu. Bu, hedef operatörün güncel
parolasının seed değeri olmadığını gösterir.

Tenant-97c5 seed mock kullanıcısıyla aynı normal login akışı da **401** verdi; dolayısıyla kontrollü eksik-permission
403 smoke için dahi geçerli bir tenant JWT üretilemedi.

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| Hedef operatör login | 200 + access token | 401 | **FAIL** |
| JWT `tenant_id` | `97c59330-…-cc93` | Token üretilemedi | **NOT RUN** |
| JWT permission | `crm.territory.model.read` | Token üretilemedi | **NOT RUN** |
| Payload `TenantId` | Yok | Yok | **PASS** |

Güvenli kimlik bilgisi olmadan token/cookie bypass yapılmadı. Bu nedenle aşağıdaki beş endpoint için authenticated
200/payload smoke dürüst biçimde tamamlanmış sayılamaz.

## 5. Contract

Canlı contract çağrısı da aynı authenticated token kapısına bağlı olduğundan payload seviyesinde çalıştırılamadı.
Kaynak sözleşmesi ve geçen contract testleri şu durumu doğruladı:

| Flag | Değer |
|---|---|
| `supportsVisitRouteReadiness` | `true` |
| `supportsContactDerivedCoverageReadiness` | `true` |
| `supportsRouteCandidateReadiness` | `true` |
| `supportsContactAvailabilityInputBoundary` | `true` |
| `supportsVisitFrequencyInputBoundary` | `true` |
| `supportsWorkflowActivation` | `false` |
| `supportsVisitPlanning` | Sözleşmede yok |
| `supportsRoutePlanning` | Sözleşmede yok |
| `supportsVisitFrequency` | Sözleşmede yok |

Runtime scope `FU09A-visit-route-readiness-read-only`; limitation metni route, visit plan, frequency policy, score
ve workflow oluşturulmadığını açıkça koruyor. **Statik/test contract PASS, authenticated canlı contract NOT RUN.**

## 6. Account Coverage Readiness

Gerçek kullanılan Gateway alias:

`GET /api/crm/territory-management/readiness/accounts/88c1b88a-53e5-4098-8c7e-18bb4d7fec02/coverage-readiness?effectiveAt=2026-08-11T09:00:00Z`

Token'sız sonuç **401 / PASS**. Authenticated 200, AccountId, current coverage ve reason-code payload doğrulaması
token üretilemediği için **NOT RUN**.

## 7. Node Coverage Accounts

Gerçek kullanılan Gateway alias:

`GET /api/crm/territory-management/readiness/nodes/84012f9d-f404-489a-ac03-e1e32f72c225/coverage-accounts?includeNonReady=true&effectiveAt=2026-08-11T09:00:00Z`

Node kimliği mevcut FU05 smoke fixture raporundan alındı. Token'sız sonuç **401 / PASS**. Account satırları,
non-ready reason code ve planner-alanı içermeyen canlı payload **NOT RUN**.

## 8. Resource Readiness

Gerçek kullanılan Gateway alias:

`GET /api/crm/territory-management/readiness/resources/fu04b-mehmet-20260731225851/coverage-readiness?effectiveAt=2026-08-11T09:00:00Z`

Token'sız sonuç **401 / PASS**. Auth filter domain lookup'tan önce çalıştığı için bu retry kaynak kimliğinin güncel
fixture varlığını iddia etmez. Current/proposed/replaced/transferred semantics ile canonical `PositionCode` payload
doğrulaması **NOT RUN**; bunlar focused test kapsamındadır.

## 9. Contact Derived Coverage

Gerçek kullanılan Gateway alias:

`GET /api/crm/territory-management/readiness/contacts/13a3c0c0-d060-4651-9f6c-231a139d3b1e/territory-coverage?effectiveAt=2026-08-11T09:00:00Z`

Token'sız sonuç **401 / PASS**. `Contact → AccountContactLink → Account → current coverage` canlı zinciri ve çoklu
satır payload'ı **NOT RUN**. Focused test, coverage'ın link üzerinden türetildiğini ve ayrı
`ContactTerritoryAssignment` kullanılmadığını doğruluyor.

## 10. Route Candidate Readiness

Gerçek kullanılan Gateway alias:

`GET /api/crm/territory-management/readiness/route-candidates?date=2026-08-11&weekday=tuesday&includeNonReady=true&effectiveAt=2026-08-11T09:00:00Z`

Token'sız sonuç **401 / PASS**. Authenticated canlı payload **NOT RUN**. Focused testlerde:

- no-matching-weekday → `AvailabilityStatus=unknown` + `contact_availability_unknown`,
- explicit unavailable exception → `unavailable` + `not_ready`,
- provider yok → `FrequencyStatus=unknown`, `SelectedFrequencyPolicyId=null`, `DueStatus=unknown`, `LastVisitDate=null`

sonuçları geçmektedir. Bunlar canlı fixture payload kanıtının yerine geçirilmemiştir.

## 11. Negative / Auth Guards

| Guard | Sonuç |
|---|---|
| Beş readiness alias'ı token'sız | **401, 5/5 PASS** |
| Eksik/yanlış permission → controlled 403 | Token üretilemediği için **NOT RUN** |
| Controller auth | `[Authorize]` + her action'da `crm.territory.model.read` fallback permission |
| Payload `TenantId` | **Yok / PASS** |
| Direct `5061` business API | **Yok / PASS** |
| POST/PUT/DELETE action | Controller yalnız beş `[HttpGet]` action içeriyor / **PASS (source guard)** |

## 12. Response Shape Guard

`TerritoryRouteCandidateReadModel` reflection testi ve kaynak modeli şu yasak alanların bulunmadığını doğruladı:

`routeOrder`, `suggestedOrder`, `distance`, `travelTime`, `optimizationScore`, `dailyPlanId`, `visitPlanId`,
`routeId`, `gps`, `checkIn`, `checkOut`, `patient`.

**Test/source guard PASS; authenticated canlı JSON shape NOT RUN.** Modelde yalnız readiness inputları ve
`Latitude`/`Longitude` account-location inputları vardır; route, GPS/check-in kaydı veya optimizasyon çıktısı değildir.

## 13. Tests / Build

| Komut | Sonuç |
|---|---|
| `dotnet test ... --filter "FullyQualifiedName~TerritoryReadinessFu09ATests|FullyQualifiedName~TerritoryContractTests"` | **14/14 PASS, 0 fail, 0 skip** |
| `dotnet build services/Diten.CrmService/src/Diten.CrmService.Api/Diten.CrmService.Api.csproj -c Debug --no-restore` | **PASS, 0 warning, 0 error** |
| `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug --no-restore` | **PASS, 0 warning, 0 error** |

.NET 10 preview SDK'nin `NETSDK1057` destek-politikası mesajı informational olup build warning/error sayılmadı.

## 14. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Feature/code expansion | **Yok** |
| Yeni master/fixture data | **Yok** |
| Mongo hand-edit | **Yok** |
| Hard delete | **Yok** |
| Account/Contact mutation | **Yok; authenticated business request çalışmadı** |
| `ContactTerritoryAssignment` oluşturma | **Yok** |
| Route/visit/frequency/campaign/workflow scope açılması | **Yok** |
| Readiness handler/controller yazma API'si | **Yok** |
| Browser/cookie/token bypass | **Yok** |
| Gateway `ocelot.json` değişikliği | **Yok** |

## 15. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `docs/audits/mod-0151-fu09a-authenticated-gateway-live-smoke-retry-2026-08-02.md` | Oluşturuldu |

Başka hiçbir dosya bu retry kapsamında değiştirilmedi. Çalışma ağacındaki mevcut kapsam dışı kullanıcı/diğer görev
değişiklikleri korunmuştur.

## 16. Final Verdict

### **FAIL**

Fleet, Gateway route auth guard, contract/source guard, focused testler ve buildler geçmektedir. Bununla birlikte görev
PASS tanımı beş FU09A GET endpointinin tenant-97c5 için yetkili JWT ile **200 ve beklenen payload** vermesini zorunlu
kılar. Güncel operatör credential'ı sağlanmadığı ve normal login 401 döndüğü için:

- tenant claim doğrulanamadı,
- beş authenticated 200 alınamadı,
- canlı response shape ve fixture semantics doğrulanamadı,
- controlled missing-permission 403 canlı kanıtı üretilemedi.

Bu eksikler PARTIAL değil, görevde açıkça tanımlanan `authenticated Gateway smoke tamamlanamadı` **FAIL** koşuludur.
Chrome bu retry'da kullanılmadı ve sonuçta rol oynamadı; önceki Chrome engeli başarıyla izole edilmiştir. Ürün defect'i
kanıtlanmamıştır; operasyonel blocker güncel yetkili smoke credential'ıdır.

## 17. Next Recommended Prompt

`MOD-0151-FU09A — Authenticated Gateway Live Smoke Retry with Secure Operator Credential Injection`

Retry önkoşulu: tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93` içinde aktif ve
`crm.territory.model.read` claim'ine sahip kullanıcının parolasını rapora/komut satırına yazmadan geçici bir process
environment secret olarak sağlamak. Yetkili beşli smoke PASS olmadan
`MOD-0165/MOD-0167-FU — Visit Frequency / Call-Cycle Policy Ownership Pack Authorization` aşamasına geçilmemelidir.
