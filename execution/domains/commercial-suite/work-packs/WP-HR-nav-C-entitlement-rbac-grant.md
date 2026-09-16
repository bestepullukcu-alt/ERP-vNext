# WORK PACKAGE — WP-HR-nav-C · 97c5 entitlement + HR RBAC grant (ops/CT)

> **CT (SoR).** Branch `fix/hr-integration-gaps`. Plan: PLAN-HR-nav-wiring-module-catalog.md. **Ops/CT action** (kod değil, canlı DB yazımı) — WP-A/B ile açılan HR katalog+page+izinlerini 97c5 tenant'ına açar. WP-A (869dc45f) + WP-B (9b89a7a5) ön koşul; Platform restart → self-registration tamamlanmış olmalı.

## Kapsam
HR modüllerinin **menüde görünmesi** için 4 kapıdan kalan ikisini kapatmak (kapı 1 catalog + kapı 3 page descriptors zaten WP-A/B self-registration ile dolu):
1. **Kapı 2 — Entitlement:** 97c5 tenant'ına 3 HR modülü için `tenant_module_entitlements` satırı (Platform DB `diten_personalization_dev`).
2. **Kapı 4 — Permission:** 180 HR iznini (hcm.* 73 + tep.* 93 + mod0251.* 14) 97c5 **Admin** rolüne grant (`diten_auth_v3.rolePermissions`).

## Ölçülen mekanizma (kod okundu, K13)
- `GET /api/platform/navigation/menu` → `GetTenantNavigationMenuQueryHandler` → her assignable modül için **canlı** `_accessService.HasAccessAsync(tenant, code)` (satır 57; **projeksiyon cache yok**).
- `TenantModuleAccessService.GetEffectiveAccessDetailAsync`: baseline değilse + system-tenant değilse → tek girdi `physicalRows = GetByTenantAndModuleAsync(tenant, code)` + plan + isCoreModule → `TenantModuleEntitlementAccessEvaluator.Evaluate`. HR = IsBaseline:false, plana dahil değil, isCoreModule:false → **erişim yalnız fiziksel entitlement satırıyla açılır.**
- Evaluator: `Source=ManualOverride(0)` + `IsEnabled=true` + `!IsDeleted` + expiry yok → **`EnabledByOverride` (hasAccess=true)**.
- Entity `TenantModuleEntitlement : GlobalEntity` — alanlar: `_id, CreatedAt[ticks,off], CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, Version, TenantId, ModuleCode, Source(int), IsEnabled, ExpiryDateUtc, Reason, RowVersion(bytes subtype-0)`. GUID'ler **subtype-4**.
- `rolePermissions` doc: `_id, CreatedAt[ticks,off], CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, TenantId, RoleId, PermissionId, AssignedAt(native datetime), AssignedBy, GrantSource(int), SourceModuleCode`. Bağ **RoleId→PermissionId** (GUID id, key değil). GUID'ler subtype-4.

## Uygulanan (2026-09-15, direct-Mongo, idempotent, subtype-4)
- **Entitlement (3 satır):** 97c5 × {HUMAN-CAPITAL, HCM-EMPLOYEE-MASTER, TALENT-ECOSYSTEM}, Source=ManualOverride, IsEnabled=true, ExpiryDateUtc=null, Reason="HR nav wiring gap #4 (WP-C)". Mevcut aktif satır varsa atla (HR'de yoktu → 3 insert).
- **RBAC grant (180 satır):** 97c5 Admin rolü (`_id 6a3154677d804ad8bd7678f8f762fe8a`, TenantId 97c5) → 180 HR PermissionId. Zaten atanmışı atla (0 atlanmış). rolePermission satırları mevcut 238 çalışan satırın **birebir aynası** (aynı alan seti + subtype-4 + TenantId=97c5).
- Script: `scratchpad/wpc_grant.py`. `py`+pymongo, replica set rs0. API yerine direct-Mongo tercih edildi: (a) auth grant zaten direct-Mongo yolu, (b) nav okuma yolu quota/plan/state-version'a bağlı değil, (c) tam BSON kontrolü.

## §37 CT bağımsız doğrulama (2026-09-15) → **ACCEPTED (E4 canlı)**
- ✅ **Entitlement Evaluate simülasyonu:** 3/3 HR modülü → `EnabledByOverride` (guid subtypes = {4}).
- ✅ **RBAC:** 97c5 Admin rolePermission 238 → **418** (+180); 180 HR grant gerçek key'lere çözülüyor (ör. mod0251.employee.export, tep.skills-gap-heatmap.manage); Admin satırlarında **non-subtype-4 GUID = 0**.
- ✅ **Login güvenliği:** GLOBAL `rolePermissions` **subtype-3 = 0** (login kırılma modu [[rolepermission-guid-subtype-login-500]] temiz); satırlar 238 çalışan satırın aynası → invariant-güvenli. (Kullanıcı gerçek re-login = son insan adımı.)
- ✅ **E4 CANLI (asıl kanıt):** `GET /api/platform/navigation/menu` (97c5 tenant dev-token, 180 HR izni) → **HTTP 200**. Menüde:
  - HUMAN-CAPITAL (domain "Human Capital") **23 sayfa**
  - HCM-EMPLOYEE-MASTER (domain "Human Capital") **1 sayfa** (/HCM/Employees)
  - TALENT-ECOSYSTEM (domain "Talent Ecosystem") **30 sayfa**
  - **TOPLAM 54 HR sayfası** + 54 distinct requiredPermission çözülüyor. Menü grubu 8→**11**. 4 kapının hepsi canlı geçti.

## Kalan (bu WP dışı)
- **WP-D (sıradaki):** L10n — 54 HR sayfası için `Nav.Page.*` resx anahtarları 7 dilde (eksik: fr/es/zh/ar/ru); frontend `NavManifestL10nGuardTests` bunu ister (WP-A/B sonrası fail eder).
- Sonra: PR `fix/hr-integration-gaps` → main (merge = Ali). Ardından main → `feature/scmm-content-studio` sync (option X).
