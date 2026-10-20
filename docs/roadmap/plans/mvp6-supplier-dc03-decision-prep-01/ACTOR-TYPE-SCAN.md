# DC-03 ön koşulu — `actor_type` sızma taraması (salt okunur)

> Soru: DC-03 D2'deki yeni `supplier_user` token'ı bugün verilse, kiracı API'lerine ulaşabilir mi? Ölçüm: `origin/main`
> `673995c15`, kod okuması; **çalışma testi yapılmadı**. Hiçbir dosya değiştirilmedi.

## 1. Sonuç (kısa)

- **Gateway kiracı uçlarını zaten kilitliyor.** `tenant_user` dışındaki her `actor_type` kiracı uçlarında **403**
  alıyor. `supplier_user` token'ı gateway üzerinden hiçbir kiracı API'sine ulaşamaz — ama **portalın kendi uçlarına da
  ulaşamaz**. DC-03 inşasında gateway'e "`supplier_user` yalnız portal rotalarına" kuralı eklenmesi **zorunlu**
  (gateway / `ocelot.json` → `integration-agent` sahibinin işi).
- **Servislerdeki ikinci hat izin claim'leri.** SupplyChain, Procurement ve Inventory yalnız token'da tam `permission`
  claim'i varsa geçiriyor. D2 gereği `supplier_user` kiracı rollerini/izinlerini taşımayacağı için bu uçlar da reddeder.
- **Kalan risk:** izin özniteliği taşımayan uçlar ve `actor_type` claim'i olmayan token'lar (aşağıda). DC-03 token'ı
  verilmeden önce servis bazında kapatılmalı.

## 2. Olgular

| # | Olgu | Kaynak |
|---|---|---|
| F1 | Gateway JWT'yi doğruluyor, `actor_type`'ı yalnız logluyor (kimlik doğrulamada karar yok) | `gateway/Diten.ApiGateway/Authentication/GatewayJwtAuthenticationHandler.cs:58-63` |
| F2 | Gateway kiracı uçlarında: `actor_type` boş değilse ve `tenant_user` değilse → 403 "Tenant endpoints require tenant_user tokens." | `gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs:186-189, 229-235` |
| F3 | Gateway platform uçlarında yalnız `platform_admin` / `partner_admin` | aynı dosya `:169-171, 523-526` |
| F4 | **Boşluk:** F2'deki kontrol `!IsNullOrWhiteSpace(actorType) &&` ile başlıyor — `actor_type` claim'i **olmayan** imzalı bir token kiracı kullanıcısı gibi geçer. Auth'un verdiği bugünkü token'lar claim'i taşıyor (`TokenService.cs:39,113`); risk eski/başka kaynaklı token'larda | aynı satırlar |
| F5 | `actor_type`'a hiç bakmayan servisler: SupplyChain, Procurement, Inventory, Planning, Manufacturing, CRM, HCM ve diğerleri (bakanlar: gateway, Auth, Platform, Platform.Common, MDM, Web) | `git grep -l actor_type` |
| F6 | SupplyChain `HasPermissionAttribute`: kimliksiz → 401, `permission` claim'i yoksa → 403 (rol kısa yolu yok); modül öznitelikleri (`[LoadPermission]` vb.) aynı kalıp | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Authorization/HasPermissionAttribute.cs`; `LoadsController.cs:12-16` |
| F7 | Procurement `PermissionAuthorizationHandler`: `permission` claim'i ya da **`SuperAdmin` rolü** | `services/Diten.ProcurementService/src/Diten.ProcurementService.Infrastructure/Authorization/PermissionAuthorizationHandler.cs:11-23` |
| F8 | Inventory: yalnız tam `permission` claim'i, rol kısa yolu yok | `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Authorization/PermissionAuthorizationHandler.cs:6-22` |

## 3. İzin özniteliği taşımayan uçlar — sezgisel sayım (kanıt değil)

Tarama yalnız `[HasPermission]`, `[Authorize(Policy…)]`, `[RequirePermission]` adlarını ve `[AllowAnonymous]`'u tanır.
Modüle özel öznitelikleri ya da handler içi kontrolleri **tanımaz**; "öznitelik yok" ≠ "korumasız".

| Servis | öznitelikli | anonim | tanınmayan | Not |
|---|---|---|---|---|
| SupplyChainService | 10 | 0 | 24 | **yanlış alarm** — `[LoadPermission]` vb. modül öznitelikleri (F6) |
| PpmService | 0 | 2 | 108 | sınıf düzeyinde yalnız `[Authorize]` görülen denetleyiciler var (ör. `BenefitCommitmentGateIReferencesController.cs:10`); handler içi kontrol **ölçülmedi** |
| EnterpriseStrategyService | 0 | 0 | 294 | kalıp ölçülmedi |
| ManagementGovernanceService | 0 | 0 | 58 | kalıp ölçülmedi (`ProcessModelingLocalTestController` dahil) |
| AuthService | 56 | 30 | 42 | çoğu `InternalEvents*` (iç API anahtarı) — ölçülmedi |
| Platform | 1645 | 54 | 38 | Platform'un kendi `LoginOnlyEndpointGuard` guard'ı var (main'de 2 kırmızı, MOD-0022) |
| Diğerleri | — | — | 0–30 | ayrıntı için tarama yeniden koşulabilir |

## 4. DC-03 inşa planına eklenecekler (öneri)

1. **Gateway kuralı:** `supplier_user` yalnız MOD-0148 portal rotalarına; diğer her yerde 403. Gateway / `ocelot.json`
   sahibi (`integration-agent`) — XMC-001.
2. **F4 boşluğu:** kiracı uçlarında `actor_type` claim'i **zorunlu** olsun (claim yoksa 403). Bu, DC-03'ten bağımsız bir
   sertleştirme; gateway sahibine ayrı bildirim.
3. **Servis savunması:** `supplier_user`'ın hiçbir kiracı izni taşımadığını bir Auth testiyle sabitlemek (token'da yalnız
   portal izinleri) — servislerin izin claim'i kontrolü (F6–F8) bunu ikinci hat yapar.
4. **İzin özniteliği olmayan uçlar:** PPM, EnterpriseStrategy, ManagementGovernance için sahiplerine "yalnız
   `[Authorize]` ile korunan uç var mı?" sorusu; `supplier_user` token'ı verilmeden önce cevaplanmalı.
5. Procurement'ın `SuperAdmin` rol kısa yolu (F7) `supplier_user`'ı etkilemez (rol taşımaz), ama not edilmeli.
