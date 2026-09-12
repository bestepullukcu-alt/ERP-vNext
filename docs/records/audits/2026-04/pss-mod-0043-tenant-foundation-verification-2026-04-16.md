# Phase 1 Foundation Verification Report (Tenant Architecture)

**Tester Agent:** Anti-Gravity QA Automation
**Date:** 2026-04-16
**Target Environment:** Local (Ports 5000, 5001, 5050, 5056, 5057)

## 1. Test Executive Summary

Faz 1 (Foundation) altyapısı teknik olarak kodlanmış olsa da, **Cross-Tenant veri yönetimi ve Auth login akışında kritik hatalar** tespit edilmiştir. İzolasyon testi, MDM servisindeki "hardcoded lookup IDs" çakışması nedeniyle bloklanmıştır.

| Test Case | Status | Actual Result |
|---|---|---|
| Tenant Resolution Chain | **PASS** | JWT > Header önceliği Gateway seviyesinde doğrulanmıştır. |
| User Registration (A/B) | **PASS** | `aaaaaaaa-...` ve `bbbbbbbb-...` tenantları için kullanıcılar başarıyla oluşturuldu. |
| Auth Login (A/B) | **FAIL** | 500 Internal Server Error. Muhtemel sebep: Yeni tenantlarda "Viewer" rolü olmaması. |
| Tenant A Data Creation | **FAIL** | 500 Error (Duplicate Key Exception: `20000000-0000...`). |
| Cross-tenant Read Block | **BLOCKED** | Veri oluşturulamadığı için okunabilirliği doğrulanamadı. |
| JWT vs Header Conflict | **PASS** | Manuel API testlerinde JWT her zaman üstün geldi (Gateway middleware doğrulandı). |

---

## 2. Detailed Test Logs

### Test 1: User Registration & Context Creation
- **Action:** `POST /api/auth/register` with `X-Tenant-Id: aaaaaaaa-...`
- **Expected:** Success, User created in Tenant A, JWT contains `tenant_id`.
- **Actual:** **PASS**. Token başarıyla alındı.
- **Notes:** Tenant context altyapısı API seviyesinde çalışıyor.

### Test 2: Login Stability
- **Action:** `POST /api/auth/login` with registered credentials.
- **Expected:** Success, returns access token.
- **Actual:** **FAIL (500)**. 
- **Notes:** Register sırasında "Viewer" rolü Tenant A'da bulunamadığı için kullanıcıya rol atanamıyor. Login aşamasında `LoginCommandHandler` boş rol veya yetki setiyle karşılaşınca hata alıyor olabilir.

### Test 3: Tenant A Data Creation (Critical Bug Found)
- **Action:** `POST /api/products` using Tenant A token.
- **Expected:** Success, product saved with `TenantId: aaaaaaaa-...`.
- **Actual:** **FAIL (MongoDB Duplicate Key Exception)**.
- **Root Cause:** `ItemLookupRepository.cs` içindeki `EnsureSeedDataAsync` metodu, her istekte sabit GUID'li (örn: `20000000-0000...0001`) lookup verilerini (ItemType, UoM vb.) o tenant için eklemeye çalışıyor. MongoDB'de `_id` koleksiyon bazında unique olduğu için, Tenant A veriyi eklemeye çalıştığında "Default Tenant"ın ID'leriyle çelişiyor.

---

## 3. Critical Findings & Technical Risks

### 🚨 Finding 1: Seed Data ID Conflict (MDM Service)
`ItemLookupRepository` içindeki `DefaultItemTypes`, `DefaultUnits` vb. dizilerde sabit GUIDler kullanılmış. 
- **Problem:** Shared-collection modelinde bu IDler globaldir. Bir tenant bunları kullandığında diğerleri `IsUpsert=true` bile olsa `_id` çakışması yaşar.
- **Risk:** Yeni bir tenant sisteme dahil olduğunda veya lookup datası istendiğinde sistem çöker.
- **Solution:** Lookup dataları ya `HybridEntity` (global, null tenant) olmalı ya da her tenant için GUIDler `Code + TenantId` üzerinden deterministik üretilmeli.

### 🚨 Finding 2: Missing Default Roles in New Tenants (Auth Service)
`RegisterCommandHandler` default olarak "Viewer" rolü atamaya çalışıyor ancak `DataSeeder` bu rolü sadece `000...001` (Default) tenantı için oluşturuyor.
- **Problem:** Tenant A admini register olduğunda yetkisiz (rolsüz) kalıyor.
- **Risk:** Multi-tenancy akışında "First User" deneyimi bozuk.
- **Solution:** Tenant oluşturma/provisioning fazında temel roller (`Admin`, `Viewer`) otomatik o tenanta özgü oluşturulmalıdır.

### 🚨 Finding 3: Auth Login 500 Error
Yeni oluşturulan kullanıcılarla login olunamıyor. Bu durum UI testlerini tamamen blokluyor.

---

## 4. Final Assessment

**Faz 1 Foundation hazır mı?** 
**KISMEM / HAYIR.** 

Temel resolve mekanizmaları (Gateway, Middleware) başarılı ancak "Business Logic" (MDM, Auth Seeder) henüz multi-tenant shared-collection mimarisine tam uyumlu değil.

### Faz 2'ye Geçiş Riskleri:
- **Yüksek Risk:** Mevcut MDM yapısıyla hiçbir yeni tenant gerçek veri girişi yapamaz.
- **Orta Risk:** Auth servisi stabil değilse uygulama test edilemez.

**Tavsiye:** Faz 2'ye geçmeden önce `MOD-0043` kapsamında "Tenant Provisioning" (rollerin otomatik kopyalanması) ve "Guid Seeding Fix" (Lookup datalarının çakışması) düzeltilmelidir.

---
*Anti-Gravity QA Automation Agent*
