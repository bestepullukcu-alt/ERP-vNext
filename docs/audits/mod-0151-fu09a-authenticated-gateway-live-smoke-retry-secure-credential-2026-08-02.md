# MOD-0151-FU09A — Authenticated Gateway Live Smoke Retry with Secure Credential

> Tarih: 2026-08-02  
> Tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`  
> Kapsam: Mevcut FU09A salt-okunur readiness endpointlerinin Gateway üzerinden authenticated canlı doğrulaması  
> Sonuç: **PARTIAL — authenticated HTTP smoke 5/5 200; resource readiness mevcut fixture ile kontrollü empty**

## 1. Preflight

| Bileşen | Kontrol | Sonuç |
|---|---|---|
| Gateway | `GET :5000/health` | **200** |
| Web | `GET :5001/` | **200** |
| Auth | `GET :5056/health` | **200** |
| Platform | `GET :5057/health` | **200** |
| CRM | `GET :5061/health` | **200** |
| MongoDB | `127.0.0.1:27017` listener | **LISTENING** |

Doğrudan CRM `5061` üzerinde yalnız `/health` çağrıldı. Tüm business çağrıları Gateway `5000` üzerinden yapıldı.

## 2. Previous FAIL Summary

Önceki retry'da fleet, contract/source guard, token'sız 401, focused test ve build kanıtları geçmesine rağmen güncel
operator credential'ı bulunamadığından Gateway login 401 dönmüş ve authenticated payload smoke **FAIL** kalmıştı.
Bu turda kullanıcı, hedef tenant için yetkili operator credential'ını runtime kullanımı için sağladı.

## 3. Secure Credential Handling

- Credential yalnız tek seferlik PowerShell sürecinin belleğinde kullanıldı.
- Credential, access token, refresh token, cookie ve Authorization header rapora veya dosyaya yazılmadı.
- Çıktıda Authorization değeri `MASKED` olarak tutuldu; JWT'nin yalnız tenant claim'i ve territory-read yetkisi okundu.
- Token/cookie uydurulmadı veya browser profilinden çıkarılmadı.
- Local config, user-secret, repo dosyası, RBAC seed/grant veya Gateway config değiştirilmedi.
- Login body yalnız `email`, `password`, `rememberMe` içerdi; `TenantId` body içinde kullanılmadı.

## 4. Authenticated Gateway Setup

Normal auth akışı:

`POST http://localhost:5000/api/tenant-auth/login` + `X-Tenant-Id: 97c59330-dbc4-4665-b29c-0c26dbb5cc93`

| Kontrol | Sonuç |
|---|---|
| Login | **200** |
| Access token üretimi | **PASS** (değer loglanmadı) |
| JWT `tenant_id` | `97c59330-dbc4-4665-b29c-0c26dbb5cc93` |
| Territory read claim | **Var** (`crm.territory.*.read`) |
| Authorization log | **MASKED** |

## 5. Contract Verification

Authenticated Gateway çağrısı: `GET /api/crm/territory-management/contract` → **200**.

| Flag | Canlı değer |
|---|---|
| `supportsVisitRouteReadiness` | `true` |
| `supportsContactDerivedCoverageReadiness` | `true` |
| `supportsRouteCandidateReadiness` | `true` |
| `supportsContactAvailabilityInputBoundary` | `true` |
| `supportsVisitFrequencyInputBoundary` | `true` |
| `supportsWorkflowActivation` | `false` |
| `supportsVisitPlanning` | Yok |
| `supportsRoutePlanning` | Yok |
| `supportsVisitFrequency` | Yok |

## 6. Account Coverage Readiness

Gerçek Gateway alias:

`GET /api/crm/territory-management/readiness/accounts/88c1b88a-53e5-4098-8c7e-18bb4d7fec02/coverage-readiness?effectiveAt=2026-08-11T09:00:00Z`

Sonuç **200**; `totalCount=2`, `returnedCount=2`, iki satır da `not_ready`. AccountId doğru. Reason-code kümesi
`coverage_not_current`, `account_inactive`. Satırlar model/node bilgisini taşıyor; örnek canlı node
`db51e6ff-ea95-4a54-a44f-375a612760e1`. Endpoint route/plan üretmedi ve yazma yapmadı.

## 7. Node Coverage Accounts

Account cevabından dinamik seçilen node ile gerçek Gateway alias:

`GET /api/crm/territory-management/readiness/nodes/db51e6ff-ea95-4a54-a44f-375a612760e1/coverage-accounts?includeNonReady=true&effectiveAt=2026-08-11T09:00:00Z`

Sonuç **200**; `totalCount=1`, `returnedCount=1`, satır `not_ready`; reason code'lar
`coverage_not_current`, `account_inactive`. `includeNonReady=true` summary ve dönen satır sayısıyla uyumlu.

## 8. Resource Readiness

Mevcut FU04B active responsibility fixture'ı:

- Model: `a461850c-3e6a-4dc0-98e8-8751fbe9f257`
- Resource: `fu04b-mehmet-20260731225851`
- Display name: `Mehmet Bey`
- `positionCode=medical-representative`
- Responsibility status: `active`, 2026-07-31 → 2027-07-31

Gerçek Gateway alias:

`GET /api/crm/territory-management/readiness/resources/fu04b-mehmet-20260731225851/coverage-readiness?includeNonReady=true&effectiveAt=2026-08-11T09:00:00Z`

Sonuç **200**, kontrollü empty: `totalCount=0`, `returnedCount=0`. Active responsibility salt-okunur supporting
endpoint ile doğrulandı, ancak bu resource'a bağlı current account coverage bulunmadığından readiness satırı oluşmadı.
Authorization katmanı geçildi, 401/403/404/500 oluşmadı. Proposed/eski-owner ve canlı PositionCode satır semantiği bu
fixture'da payload seviyesinde kanıtlanamadı; focused testlerle korunuyor. Bu durum final hükmü PARTIAL yapan tek canlı
fixture boşluğudur.

## 9. Contact Derived Coverage

Gerçek Gateway alias:

`GET /api/crm/territory-management/readiness/contacts/13a3c0c0-d060-4651-9f6c-231a139d3b1e/territory-coverage?date=2026-08-11&weekday=tuesday&effectiveAt=2026-08-11T09:00:00Z`

Sonuç **200**; iki satır döndü. Her ikisinde:

- ContactId hedef fixture ile aynı,
- AccountId `88c1b88a-53e5-4098-8c7e-18bb4d7fec02`,
- AccountContactLinkId `8c34d497-cbd0-49e5-be7f-aca41df3e79d`,
- `availabilityStatus=unknown`,
- reason code'lar `contact_availability_unknown`, `coverage_not_current`, `account_inactive`.

Bu, `Contact → AccountContactLink → Account → coverage` türetme zincirini ve no-matching-weekday `unknown`
semantiğini canlıda doğruladı.

## 10. Route Candidate Readiness

Gerçek Gateway alias:

`GET /api/crm/territory-management/readiness/route-candidates?accountId=88c1b88a-53e5-4098-8c7e-18bb4d7fec02&contactId=13a3c0c0-d060-4651-9f6c-231a139d3b1e&date=2026-08-11&weekday=tuesday&includeNonReady=true&effectiveAt=2026-08-11T09:00:00Z`

Sonuç **200**; `totalCount=2`, `returnedCount=2`, iki satır `not_ready`. Her iki satırda:

- `availabilityStatus=unknown`,
- `frequencyStatus=unknown`,
- `selectedFrequencyPolicyId=null`,
- `lastVisitDate=null`,
- `dueStatus=unknown`,
- reason code'lar `coverage_not_current`, `account_inactive`, `resource_not_current_owner`,
  `contact_availability_unknown`, `frequency_unknown`.

Explicit unavailable exception bu canlı fixture'da yoktur; `unavailable` ayrımı focused test ile doğrulandı.

## 11. Negative / Auth Guards

| Guard | Sonuç |
|---|---|
| Beş readiness endpointi token'sız | **401, 401, 401, 401, 401** |
| Yanlış `X-Tenant-Id` + geçerli JWT | **200; JWT tenant claim canonical kaldı, header claim'i ezmedi** |
| POST readiness URL | **404 / unsupported** |
| PUT readiness URL | **404 / unsupported** |
| DELETE readiness URL | **404 / unsupported** |

Yanlış tenant header'ının 200 dönmesi cross-tenant okuma değildir: validated JWT tenant claim'i hedef tenant olarak
kaldı ve response yine istenen hedef AccountId içindi. Bu runtime'da tenant kimliğinin authenticated claim'den
alındığını gösterir. Ayrı eksik-permission kullanıcısı sağlanmadığından 403 senaryosu çalıştırılmadı.

## 12. Response Shape Guard

Beş canlı JSON response'un recursive property-name taramasında aşağıdaki alanların hiçbiri bulunmadı:

`routeOrder`, `suggestedOrder`, `distance`, `travelTime`, `optimizationScore`, `dailyPlanId`, `visitPlanId`,
`routeId`, `gps`, `checkIn`, `checkOut`, `patient`.

Sonuç: **FORBIDDEN_FIELDS=NONE / PASS**.

## 13. Data Mutation Guard

Smoke öncesi ve sonrası Gateway GET cevaplarının SHA-256 karşılaştırması:

| Surface | Önce/Sonra | Hash |
|---|---|---|
| Account master | 200 / 200 | **Aynı** |
| Contact master | 200 / 200 | **Aynı** |
| Contact availability | 200 / 200 | **Aynı** |
| Account territory assignments | 200 / 200 | **Aynı** |

Production kaynakta `ContactTerritoryAssignment` aggregate/type yoktur. Readiness controller yalnız beş `HttpGet`
action içerir. Mongo hand-edit, hard delete veya herhangi bir write çağrısı yapılmadı.

## 14. Tests / Build

| Kontrol | Sonuç |
|---|---|
| FU09A readiness + contract focused tests | **14/14 PASS, 0 fail, 0 skip** |
| CRM API Debug build | **PASS, 0 warning, 0 error** |
| Web Debug build | **PASS, 0 warning, 0 error** |

SDK `NETSDK1057` preview-support mesajı informational olup build warning/error sayısına dahil değildir.

## 15. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Yeni feature/code expansion | **Yok** |
| Yeni fixture/master data | **Yok** |
| Direct 5061 business API | **Yok** |
| Payload `TenantId` | **Yok** |
| RBAC seed/grant değişikliği | **Yok** |
| Gateway config değişikliği | **Yok** |
| Mongo hand-edit / hard delete | **Yok** |
| Route/visit/frequency/campaign/workflow scope | **Açılmadı** |
| Browser bağımlılığı | **Yok** |

## 16. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `docs/audits/mod-0151-fu09a-authenticated-gateway-live-smoke-retry-secure-credential-2026-08-02.md` | Oluşturuldu |

Başka hiçbir dosya bu task kapsamında değiştirilmedi. Çalışma ağacındaki mevcut kullanıcı/diğer görev değişiklikleri
korundu.

## 17. Final Verdict

### **PARTIAL**

Credential/auth blocker kapanmıştır: normal Gateway login 200, tenant claim doğru, contract 200 ve beş FU09A
endpointi authenticated olarak **5/5 200** dönmüştür. Token'sız guard 5/5 401; no-matching-weekday canlıda `unknown`;
frequency/due placeholder'ları `unknown/null`; forbidden planner alanları yok; pre/post master ve assignment hash'leri
aynıdır.

Ancak task'ın PARTIAL kuralı, credential ile login başarılı olsa bile bazı endpointler fixture eksikliği nedeniyle
controlled empty döndüğünde uygulanır. Resource readiness 200/empty kaldı; ayrıca explicit unavailable ayrımı bu
canlı fixture'da değil focused testte doğrulandı. Bu nedenle sonuç PASS'e yükseltilmedi. Ürün hatası veya auth blocker
yoktur; kalan eksik yalnız pozitif resource-to-covered-account fixture kanıtıdır.

## 18. Next Recommended Prompt

`MOD-0151-FU09A — Positive Resource-to-Current-Coverage Live Smoke Closeout`

Amaç yalnız mevcut veya kontrollü smoke fixture ile active resource responsibility + current account coverage
eşleşmesini sağlayıp resource readiness endpointinden en az bir canlı satır almak ve explicit unavailable exception
edge-case'ini canlıda kapatmaktır. Bu closeout PASS olmadan
`MOD-0165/MOD-0167-FU — Visit Frequency / Call-Cycle Policy Ownership Pack Authorization` aşamasına geçilmemelidir.
