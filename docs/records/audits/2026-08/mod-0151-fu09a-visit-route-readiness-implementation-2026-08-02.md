# MOD-0151 FU09A — Visit/Route Readiness Implementation

Tarih: 2026-08-02  
Tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`  
Sonuç: **PARTIAL**

## 1. Preflight

- `MOD-0151-territory-management.md` durumu `ready-for-dev`, `runtime_code_allowed: true`.
- FU09A authorization raporu PASS ve runtime scope `FU09A-visit-route-readiness-boundaries` olarak doğrulandı.
- Commercial Suite domain sınırları, FU05A lifecycle guard, FU04A current-responsibility policy, FU04B read model ve MOD-0150 availability lookup incelendi.
- Gateway route dosyası protected olduğu için değiştirilmedi.

## 2. Scope confirmation

Uygulama salt okunurdur. Route/visit planı, optimizasyon, GPS, check-in/out, frequency/call-cycle engine,
campaign, workflow, ChangeRequest, patient data, yeni aggregate veya master mutation eklenmedi.

## 3. Implementation summary

- Beş canonical GET endpointi ve Gateway'in mevcut wildcard'ına uyan alias'ları eklendi.
- Ortak candidate/readiness projection ve summary envelope eklendi.
- Coverage, resource, contact-link ve availability okumaları mevcut repository/policy seam'leri üzerinden birleştirildi.
- Contract runtime scope ve additive feature flag'leri güncellendi.
- Yeni menü/sayfa eklenmedi. Authenticated smoke için mevcut Territory controller'a salt-okunur JSON proxy eklendi.

## 4. Contract flags

`supportsVisitRouteReadiness`, `supportsContactDerivedCoverageReadiness`, `supportsRouteCandidateReadiness`,
`supportsContactAvailabilityInputBoundary`, `supportsVisitFrequencyInputBoundary` true; workflow activation false kaldı.
Visit/route planning veya frequency-engine flag'i eklenmedi.

## 5. Readiness response model

`TerritoryRouteCandidateReadModel` persist/cache edilmeyen query DTO'sudur. Account, location, territory model/node,
business unit, current resource/position, optional contact/link, availability/preferences, future frequency/due placeholders,
effective instant, readiness status ve reason code'ları taşır. Route order, distance, travel time, score ve plan id alanları yoktur.

## 6. Reason code behavior

Stabil lowercase snake_case sözlük uygulandı. Bir satır çoklu reason taşıyabilir. `readiness_ok` yalnız reason listesi
boşken üretilir ve başka kodla birleşmez. Blocking reason `not_ready`, eksik provider/input `unknown`, yalnız warning
bulunan satır `ready` olur.

## 7. Coverage readiness

Model ve assignment birlikte `TerritoryCoverageLifecyclePolicy.IsCurrent` ile değerlendirilir. Inactive/archived/
superseded veya effective-window dışındaki model, ended/deleted/window dışı assignment `coverage_not_current` üretir.
Account status, location ve BU scope ayrıca değerlendirilir. Eksik/non-current coverage sessizce düşmez.

## 8. Resource responsibility readiness

`TerritoryCurrentResponsibilityPolicy.IsCurrent` kullanılır. Proposed/ended/replaced eski owner current sayılmaz.
Canonical key `EffectivePositionCode`/PositionCode'dur; RoleCode kullanılmaz. Eşleşme yoksa
`resource_not_current_owner` üretilir.

## 9. Contact derived coverage

Akış `Contact -> AccountContactLink -> Account -> AccountTerritoryAssignment/TerritoryModel` şeklindedir.
Primary link filtre değildir; her link ayrı satır üretebilir. Link provenance olarak `AccountContactLinkId` ve `AccountId`
döner. `ContactTerritoryAssignment` oluşturulmadı.

## 10. Contact availability consumption

MOD-0150 `LookupContactAvailabilityHandler` read-only olarak tüketilir. No matching weekday/no data `unknown` ve
`contact_availability_unknown`; explicit unavailable exception `unavailable` ve `contact_not_available_on_day` olur.
Appointment required ve preferred-window dışı warning'dir, tek başına candidate elemez.

## 11. Frequency / call-cycle placeholder

Route-candidate projection'da provider olmadığı için `FrequencyStatus=unknown`, `SelectedFrequencyPolicyId=null` ve
`frequency_unknown` döner. Varsayılan frequency üretilmez; not-due/overdue hesaplanmaz.

## 12. Last visit / due-overdue placeholder

`LastVisitDate=null`, `DueStatus=unknown`. Visit-history provider bulunmadığından `no_last_visit` yanlış biçimde
üretilmez ve due/overdue hesaplanmaz.

## 13. Route candidate readiness

`includeNonReady=true` tüm ready/not-ready/unknown satırları döndürür. `false` yalnız `ready` satırları döndürür;
`TotalCount`, `ReadyCount`, `NotReadyCount`, `UnknownCount`, `ReturnedCount` filtre öncesi/sonrası görünürlüğü korur.
Frequency provider henüz olmadığı için route-candidate satırları doğal olarak `unknown` olabilir; bu bir plan kararı değildir.

## 14. Permissions

Yeni permission literal/seed/grant eklenmedi. Endpointler mevcut katalogla uyum için `crm.territory.model.read`
fallback'i ile korunur. Contact availability tüketimi mevcut contact-read fallback seam'i üzerinden internal query olarak çalışır.

## 15. Tests

- Focused FU09A + contract: **14/14 PASS**.
- Full CRM Application suite: **508 PASS, 5 SKIP, 0 FAIL** (513 toplam).
- İlk full-suite koşusunda task dışı, rastgele GUID üreten PII masking testi bir kez flake etti; değişiklik yapılmadan
  ikinci tam koşu PASS oldu.
- CrmService build: **PASS, 0 warning, 0 error**.
- Diten.Web build: **PASS**; mevcut repo kaynaklı warning'ler ilk derleme çıktısında görüldü, son incremental build 0 warning/0 error.

## 16. Gateway-only live smoke

- Port health: Gateway 5000, Web 5001, Auth 5056, Platform 5057, CRM 5061 LISTEN.
- Gateway alias contract/account/contact/candidate çağrıları token olmadan beklenen **401** döndürdü; route'ların
  Gateway -> CRM auth katmanına ulaştığı kanıtlandı. Direct 5061 business API çağrısı yapılmadı.
- Chrome'da mevcut authenticated tenant oturumu bulundu. Ancak browser-control katmanı salt-okunur JSON diagnostic
  URL navigasyonlarını `ERR_BLOCKED_BY_CLIENT` ile engelledi. Bu nedenle authenticated response payload/fixture smoke
  bu koşuda kanıtlanamadı; doğrudan servis veya cookie/token çıkarma bypass'ı yapılmadı.

## 17. Guard checks

- Readiness controller yalnız `[HttpGet]` taşır.
- Readiness feature'da insert/update/delete/commit çağrısı yoktur.
- Account, Contact, AccountContactLink ve ContactAvailability mutation yoktur.
- `ContactTerritoryAssignment` yoktur.
- Route/visit/frequency engine, workflow, patient data, hard delete, TenantId payload yoktur.
- Gateway protected config değiştirilmedi; mevcut wildcard için alias kullanıldı.

## 18. Created / updated files

Oluşturulanlar:

- `TerritoryReadinessContracts.cs`
- `TerritoryReadinessQueries.cs`
- `TerritoryReadinessHandlers.cs`
- `TerritoryReadinessController.cs`
- `TerritoryReadinessFu09ATests.cs`
- Bu audit raporu

Güncellenenler:

- `TerritoryContractDto.cs`
- `GetTerritoryContractHandler.cs`
- `TerritoryContractTests.cs`
- `frontend/Diten.Web/Controllers/CRM/TerritoryManagementController.cs`

## 19. Final verdict

**PARTIAL.** Core API, contract, policy reuse, DTO/guard sınırları, build ve automated tests PASS. Gateway route/auth reachability
PASS. Authenticated tenant fixture payload smoke browser-control engeli nedeniyle tamamlanamadı. Frequency/due provider yokluğu
tasarım gereği unknown placeholder ile sınırlıdır; UI yok/API-only yaklaşımı kabul edilebilir.

## 20. Next recommended prompt

`MOD-0151-FU09A — Authenticated Gateway Live Smoke Retry`

Bu retry PASS olduktan sonraki öneri:

`MOD-0165/MOD-0167-FU — Visit Frequency / Call-Cycle Policy Ownership Pack Authorization`
