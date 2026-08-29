# MOD-0151-FU09A — Positive Resource-to-Current-Coverage Live Smoke Closeout

> Tarih: 2026-08-02  
> Tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`  
> Sonuç: **PARTIAL — positive row canlıda kanıtlandı; bu task içinde minimum fixture setup gerekti**

## 1. Preflight

| Bileşen | Sonuç |
|---|---|
| Gateway `5000 /health` | **200** |
| Web `5001 /` | **200** |
| Auth `5056 /health` | **200** |
| Platform `5057 /health` | **200** |
| CRM `5061 /health` | **200** |
| MongoDB `27017` | **LISTENING** |
| Gateway login | **200** |
| JWT tenant claim | `97c59330-dbc4-4665-b29c-0c26dbb5cc93` |

Credential yalnız process belleğinde kullanıldı; parola/token/cookie/Authorization değeri rapora veya dosyaya
yazılmadı. Doğrudan `5061` üzerinde yalnız `/health` çağrıldı. Tüm business çağrıları Gateway `5000` üzerinden yapıldı.

## 2. Previous PARTIAL Summary

Önceki secure-credential retry'da login, contract ve beş FU09A endpointi authenticated olarak geçmiş; resource
readiness ise active resource'a bağlı current account coverage olmadığı için **200/empty** kalmıştı. Son açık,
aynı active model/node/BusinessUnit/effective window üzerinde account coverage ile resource responsibility'nin canlı
eşleşmesiydi.

## 3. Fixture Discovery

Tenant'taki active modeller, account assignments ve 2026-08-11 current resource responsibilities Gateway GET'leriyle
taranmıştır. Setup öncesinde aynı model/node/scope/effective window üzerinde eşleşen zincir sayısı **0** idi.

Minimum setup için mevcut smoke nesneleri seçildi:

| Nesne | Değer |
|---|---|
| Active model | `a461850c-3e6a-4dc0-98e8-8751fbe9f257` · `SMOKE-MOD0151-FU04B-20260731225851` |
| Model window/scope | 2026-07-30 → 2027-07-31 · `business-unit/gamma` |
| Active node | `06c8cef8-7435-4bda-b045-ff67ac8a7b76` · `FU04B-SULEYMANPASA-20260731225851` |
| Active resource assignment | `98cb09d6-3f39-401e-9650-46646e5826b8` |
| Resource | `fu04b-mehmet-20260731225851` · Mehmet Bey |
| Position | `medical-representative` · Medical Representative |
| Coverage scope | `exact-territory` · `gamma` |
| Existing active account | `25464183-95d0-4bae-bf26-9dbe79d56063` · `ACC-2026-000016` · Büyükşehir Eczanesi |

Account master üzerinde hiçbir değişiklik yapılmadı.

## 4. Fixture Setup

Yalnız public Gateway API kullanıldı:

`POST /api/crm/territory-models/a461850c-3e6a-4dc0-98e8-8751fbe9f257/assignment-preview/apply`

Setup body kapsamı:

- seçili account + target node,
- `business-unit/gamma`,
- 2026-08-02 → 2027-07-31,
- `conflictPolicy=block`, `override=false`,
- correlation: `fu09a-positive-resource-current-coverage-20260802`,
- `TenantId` yok.

Sonuç **201**; oluşturulan `AccountTerritoryAssignment`:
`aa7e1291-9e24-4aa4-a55b-1b3e3e489620`.

Bu, stable smoke fixture setup'tır. Account assignment sayısı ilgili modelde `0 → 1` oldu. Başka runtime nesnesi
oluşturulmadı/değiştirilmedi; hard delete veya Mongo hand-edit yapılmadı. Fixture sonraki salt-okunur closeout için
active bırakıldı.

## 5. Resource Readiness Positive Smoke

Authenticated Gateway çağrısı:

`GET /api/crm/territory-management/readiness/resources/fu04b-mehmet-20260731225851/coverage-readiness?includeNonReady=true&effectiveAt=2026-08-11T09:00:00Z&businessUnit=gamma`

Sonuç **200**:

| Alan | Canlı değer |
|---|---|
| `totalCount / returnedCount` | `1 / 1` |
| `readyCount / notReadyCount / unknownCount` | `1 / 0 / 0` |
| AccountId | `25464183-95d0-4bae-bf26-9dbe79d56063` |
| TerritoryModelId | `a461850c-3e6a-4dc0-98e8-8751fbe9f257` |
| TerritoryNodeId | `06c8cef8-7435-4bda-b045-ff67ac8a7b76` |
| BusinessUnit | `gamma` |
| ResourceId | `fu04b-mehmet-20260731225851` |
| PositionCode | `medical-representative` |
| Readiness | `ready` |
| Reason | `readiness_ok` |

Ended eski owner `fu04b-ayse-20260731225851` için aynı tarihte resource readiness `returnedCount=0`; current owner
olarak sızmadı. Proposed/transfer edge-case'leri focused testlerle korunuyor.

## 6. Account Coverage Cross-check

`GET /api/crm/territory-management/readiness/accounts/25464183-95d0-4bae-bf26-9dbe79d56063/coverage-readiness?effectiveAt=2026-08-11T09:00:00Z&businessUnit=gamma`

Sonuç **200**. History dahil beş satır içinden yeni fixture satırı:

- aynı model/node/BusinessUnit/resource/PositionCode,
- `readinessStatus=ready`,
- `reasonCodes=[readiness_ok]`.

Coverage Summary cross-check **200**, `hasCurrentCoverage=true`. FU05A active-model lifecycle guard ile uyumludur;
geçmiş/non-current satırlar ayrı `not_ready` reason code'larıyla korunmuştur.

## 7. Node Coverage Cross-check

`GET /api/crm/territory-management/readiness/nodes/06c8cef8-7435-4bda-b045-ff67ac8a7b76/coverage-accounts?includeNonReady=true&effectiveAt=2026-08-11T09:00:00Z&businessUnit=gamma`

Sonuç **200**; `totalCount=1`, `returnedCount=1`, `readyCount=1`. Account aynı node altında, Mehmet Bey resource'u ve
canonical `medical-representative` PositionCode'u ile `readiness_ok` döndü.

## 8. Route Candidate Cross-check

`GET /api/crm/territory-management/readiness/route-candidates?accountId=25464183-95d0-4bae-bf26-9dbe79d56063&resourceId=fu04b-mehmet-20260731225851&includeNonReady=true&effectiveAt=2026-08-11T09:00:00Z&businessUnit=gamma&date=2026-08-11&weekday=tuesday`

Sonuç **200**. Pozitif coverage/resource satırı aynı model/node/resource/PositionCode ile candidate response'a yansıdı:

| Alan | Değer |
|---|---|
| `frequencyStatus` | `unknown` |
| `selectedFrequencyPolicyId` | `null` |
| `lastVisitDate` | `null` |
| `dueStatus` | `unknown` |
| `readinessStatus` | `unknown` |
| `reasonCodes` | yalnız `frequency_unknown` |

Coverage ve resource ownership bloklamıyor; gerçek frequency provider bulunmadığı için overall sonuç bilinçli olarak
`unknown` kalıyor. Route/visit planı üretilmedi.

## 9. Response Shape Guard

Resource, Account, Node ve Route Candidate canlı JSON cevapları recursive property-name taramasından geçirildi.
Aşağıdaki alanların hiçbiri bulunmadı:

`routeOrder`, `suggestedOrder`, `distance`, `travelTime`, `optimizationScore`, `dailyPlanId`, `visitPlanId`,
`routeId`, `gps`, `checkIn`, `checkOut`, `patient`.

Sonuç: **FORBIDDEN_FIELDS=NONE / PASS**.

## 10. Data Mutation Guard

Setup sonrası tamamen salt-okunur ikinci smoke turunda dört readiness endpointi tekrar **200,200,200,200** döndü.
Bu tur öncesi/sonrası SHA-256 cevap hash'leri:

| Surface | Aynı mı? |
|---|---|
| Account master | **Evet** |
| Contact master | **Evet** |
| ContactAvailability | **Evet** |
| AccountAssignments | **Evet** |
| ResourceAssignments | **Evet** |

Setup sırasında beklenen tek fark AccountAssignments `0 → 1` olmuştur. Production kaynakta
`ContactTerritoryAssignment` aggregate/type yoktur. Readiness GET'leri hiçbir write üretmemiştir.

## 11. Negative / Auth Guards

| Guard | Sonuç |
|---|---|
| Token'sız resource readiness | **401** |
| POST resource readiness URL | **404 / unsupported** |
| PUT resource readiness URL | **404 / unsupported** |
| DELETE resource readiness URL | **404 / unsupported** |
| Direct `5061` business API | **Yok** |
| Payload `TenantId` | **Yok** |
| RBAC seed/grant değişikliği | **Yok** |
| Gateway config değişikliği | **Yok** |

## 12. Tests / Build

| Kontrol | Sonuç |
|---|---|
| FU09A readiness + contract focused tests | **14/14 PASS, 0 fail, 0 skip** |
| CRM API Debug build | **PASS, 0 warning, 0 error** |
| Web Debug build | **PASS, 0 warning, 0 error** |

SDK `NETSDK1057` preview-support mesajı informational olup build warning/error sayısına dahil değildir.

## 13. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Feature/code expansion | **Yok** |
| Account/Contact/Availability master mutation | **Yok** |
| Resource assignment mutation | **Yok** |
| ContactTerritoryAssignment | **Yok** |
| Hard delete / Mongo hand-edit | **Yok** |
| Route/visit/frequency/campaign/workflow scope | **Açılmadı** |
| Credential/token raporda | **Yok** |

## 14. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `docs/audits/mod-0151-fu09a-positive-resource-current-coverage-live-smoke-closeout-2026-08-02.md` | Oluşturuldu |

Runtime fixture olarak bir `AccountTerritoryAssignment` oluşturuldu:
`aa7e1291-9e24-4aa4-a55b-1b3e3e489620`. Repo kodu/config/module pack değiştirilmedi.

## 15. Final Verdict

### **PARTIAL**

Teknik closeout hedefi gerçekleşti: resource readiness artık authenticated Gateway üzerinden **200 + 1 positive
ready row** döndü; Account, Node ve Route Candidate cross-check'leri aynı model/node/scope/resource bağlantısını
kanıtladı. Shape, auth, mutation, gateway-only, test ve build guard'ları geçti.

Ancak görev rubriği, positive row yalnız bu task içinde oluşturulan fixture setup ile kanıtlandığında PARTIAL hükmünü
zorunlu kılar. Bu nedenle PASS verilmedi. Replacement/transfer/proposed ayrımlarının bir kısmı da canlı pozitif zincir
yerine focused testlerle sınırlıdır. Ürün kusuru yoktur; artık stabil ve yeniden kullanılabilir pozitif smoke fixture'ı
mevcuttur.

## 16. Next Recommended Prompt

`MOD-0151-FU09A — Read-Only Reverification on Stable Positive Resource-Coverage Fixture`

Yeni setup yapmadan mevcut assignment `aa7e1291-9e24-4aa4-a55b-1b3e3e489620` üzerinden Resource/Account/Node/Route
Candidate zincirini tekrar doğrula. Bu salt-okunur revalidation PASS olursa sıradaki prompt:

`MOD-0165/MOD-0167-FU — Visit Frequency / Call-Cycle Policy Ownership Pack Authorization`
