# MOD-0151 FU02B — Authenticated Gateway Live Smoke Closeout

**Tarih:** 2026-07-26  
**Hedef tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`  
**Karar:** **FAIL**

Bu çalışma yalnızca canlı doğrulama ve kanıt toplama kapsamındadır. Runtime kodu, backend, frontend, Gateway, RBAC, reference data ve MongoDB değiştirilmemiştir. Bu rapor dışında dosya oluşturulmamış veya güncellenmemiştir.

## 1. Preflight

| Kontrol | Gerçekleşen | Sonuç |
|---|---|---|
| Gateway `:5000/health` | HTTP 200 | PASS |
| AuthService `:5056/health` | HTTP 200 | PASS |
| Platform `:5057/health` | HTTP 200 | PASS |
| CrmService `:5061/health` | HTTP 200; yalnızca health kontrolü | PASS |
| Web `:5001/` | HTTP 200 | PASS |
| Tenant login | `X-Tenant-Id` header ile, payload içinde `TenantId` olmadan çağrıldı; HTTP 401 | FAIL |
| Gateway route/auth guard | `GET :5000/api/crm/territory-management/contract` tokensız HTTP 401; route mevcut ve auth korumalı | PASS |
| Gateway-only | Business API için direct `:5061` çağrısı yapılmadı | PASS |
| Kod değişikliği | Başlangıç worktree zaten kirliydi (402 status girdisi); mevcut değişikliklere dokunulmadı | PASS |

Login parolası ve token hiçbir dosyaya veya log çıktısına yazılmadı. Chrome'da hedef web uygulamasına ait açık ve yetkili bir oturum bulundu; ancak erişim belirteci tarayıcı JavaScript'ine kapalı sunucu oturumunda tutulduğu için Gateway çağrılarında güvenli biçimde yeniden kullanılamadı.

## 2. Auth / Token Verification

| Check | Expected | Actual | Result |
|---|---|---|---|
| Hedef tenant login | 200 ve access token | 401 | FAIL |
| `crm.territory.read` | Claim mevcut | Token alınamadı | BLOCKED |
| `crm.territory.model.read` | Claim mevcut | Token alınamadı | BLOCKED |
| `crm.territory.model.manage` | Claim mevcut | Token alınamadı | BLOCKED |
| `crm.territory.node.read` | Claim mevcut | Token alınamadı | BLOCKED |
| `crm.territory.node.manage` | Claim mevcut | Token alınamadı | BLOCKED |
| `crm.territory.delete` | Claim yok | Token alınamadı | BLOCKED |
| `crm.micro-zone.manage` | Claim yok | Token alınamadı | BLOCKED |

Önceki `mod-0151-fu01-live-smoke-retry-2026-07-23.md` kanıtı hedef tenant için 5/5 gerekli claim ve 0/2 forbidden claim sonucu içerir; bu çalışmada yeni token ile tekrar doğrulanamadığı için önceki kanıt canlı closeout yerine geçirilmemiştir.

## 3. Contract Smoke

| Check | Expected | Actual | Result |
|---|---|---|---|
| Authenticated response | 200 | Login başarısız olduğu için çalıştırılamadı | BLOCKED |
| `moduleId` | `MOD-0151` | Okunamadı | BLOCKED |
| `isReady` | `true` | Okunamadı | BLOCKED |
| `supportsLifecycleActions` | `true` | Okunamadı | BLOCKED |
| `supportsComputedExpiry` | `true` | Okunamadı | BLOCKED |
| `supportsDraftSoftDelete` | `true` | Okunamadı | BLOCKED |
| `supportsWorkflowActivation` | `false` | Okunamadı | BLOCKED |
| `supportsApprovalTrace` | `false` | Okunamadı | BLOCKED |
| Gateway route/auth guard | Route mevcut, unauthenticated 401 | 401 | PASS |

## 4. Positive Lifecycle Smoke

| Step | Expected | Actual | Result |
|---|---|---|---|
| Draft model create | 201, draft, `isExpired=false` | Auth token alınamadı; çağrı yapılmadı | BLOCKED |
| Hierarchy node create | Draft node ve hierarchy görünümü | Çağrı yapılmadı | BLOCKED |
| Activate | 200, model/node active | Çağrı yapılmadı | BLOCKED |
| Get after activate | Stored/computed active | Çağrı yapılmadı | BLOCKED |
| Deactivate | 200, lifecycle-consistent inactive | Çağrı yapılmadı | BLOCKED |
| Archive inactive | 200, archived/read-only | Çağrı yapılmadı | BLOCKED |

Başarısız authentication sonrasında hiçbir test modeli veya node oluşturulmadı; kısmi ve doğrulanamaz test verisi bırakılmadı.

## 5. Negative Lifecycle Smoke

| Scenario | Expected | Actual | Result |
|---|---|---|---|
| Scope-order-insensitive overlap | Reverse BU order ile activate 409 | Çağrı yapılmadı | BLOCKED |
| Edit archived | Controlled 400/409 | Çağrı yapılmadı | BLOCKED |
| Draft model soft-delete | 200 ve default listten kaybolma | Çağrı yapılmadı | BLOCKED |
| Draft node soft-delete | 200 ve hierarchy'den kaybolma | Çağrı yapılmadı | BLOCKED |
| Active delete-draft | Controlled 400/409, kayıt korunur | Çağrı yapılmadı | BLOCKED |
| Archive active | Controlled 400/409 | Çağrı yapılmadı | BLOCKED |

## 6. Computed Expiry Smoke

| Scenario | Expected | Actual | Result |
|---|---|---|---|
| Draft + past `EffectiveTo` | Stored draft; computed expired / `isExpired=true` | Live API çağrısı yapılamadı | BLOCKED |
| Active + expired | Stored status korunur; computed expired; background job yok | Live API çağrısı yapılamadı | BLOCKED |
| Implementation test evidence | Lifecycle testleri davranışı kanıtlar | Önceki FU02B raporu: Territory 63/63; tüm CrmService 232/232 PASS | PASS (önceki kanıt) |

Unit test kanıtı implementation davranışını destekler; fakat bu taskın amacı olan authenticated live Gateway kanıtının yerini tutmaz.

## 7. Audit / Log Evidence

Çalışan ortamın `.logs`/`logs` dosyaları salt okunur tarandı. Yeni lifecycle mutasyonu çalıştırılmadığı için aşağıdaki eventler görülmedi.

| Event | Seen? | Notes |
|---|---|---|
| `territory.model.activated` | Hayır | Smoke auth aşamasında durdu |
| `territory.model.deactivated` | Hayır | Smoke auth aşamasında durdu |
| `territory.model.archived` | Hayır | Smoke auth aşamasında durdu |
| `territory.model.soft_deleted` | Hayır | Smoke auth aşamasında durdu |
| `territory.node.soft_deleted` | Hayır | Smoke auth aşamasında durdu |
| `territory.model.activation_rejected` | Hayır | Smoke auth aşamasında durdu |
| `territory.model.delete_rejected` | Hayır | Smoke auth aşamasında durdu |
| `territory.node.delete_rejected` | Hayır | Smoke auth aşamasında durdu |

## 8. UI Manual Smoke

Mevcut Chrome web oturumu üzerinden salt okunur kontrol yapıldı; lifecycle butonlarına basılmadı.

| Step | Result | Notes |
|---|---|---|
| Territory Management açılır | PASS | Liste ve DataTable render edildi |
| Draft model lifecycle badge | PASS | `DENEME` modeli `draft` gösterildi |
| Draft model Activate | PASS | Model detayında `Activate` görünür |
| Draft model Delete Draft | PASS | Model detayında `Delete Draft` görünür |
| Draft node badge | PASS | Hierarchy satırları `draft` gösterildi |
| Active model Deactivate | BLOCKED | Canlı active test modeli oluşturulamadı |
| Inactive/expired Archive | BLOCKED | İlgili canlı durum oluşturulamadı |
| Archived read-only | BLOCKED | İlgili canlı durum oluşturulamadı |
| Active node delete gizli | BLOCKED | İlgili canlı durum oluşturulamadı |
| Workflow/approval/evidence/assignment aksiyonları yok | PASS | İncelenen liste ve draft detay yüzeyinde görünmedi |

## 9. Guard Checks

| Check | Result |
|---|---|
| Runtime code changed? | No |
| Backend changed? | No |
| Frontend changed? | No |
| Gateway route changed? | No |
| RBAC seed/grant changed? | No |
| MOD-0048 reference publish changed? | No |
| Mongo hand-edit used? | No |
| Workflow approval çalıştı mı? | No |
| Submit/approve/reject çalıştı mı? | No |
| Assignment/resource/evidence/import-export çalıştı mı? | No |
| Brand Scope eklendi mi? | No |
| Product/Brand master touched? | No |
| Account/Contact touched? | No |
| Hard delete kullanıldı mı? | No |
| Active kayıt silindi mi? | No |
| Test mutasyonu yapıldı mı? | No; auth başarısızlığı sonrası duruldu |
| Background job eklendi mi? | No |
| Direct 5061 business API kullanıldı mı? | No |
| TenantId payload gönderildi mi? | No |
| Forbidden permission eklendi mi? | No |

## 10. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `docs/audits/mod-0151-fu02b-authenticated-gateway-live-smoke-closeout-2026-07-25.md` | Created | Bu closeout kanıt raporu |

Repo başlangıçta mevcut kullanıcı değişiklikleri nedeniyle kirliydi. Bu çalışma bunları değiştirmedi; yalnızca yukarıdaki rapor eklendi.

## 11. Final Verdict

**FAIL**

Health ve Gateway auth-guard preflight kontrolleri geçti; mevcut web oturumunda sınırlı UI kanıtı toplandı. Ancak hedef tenant login çağrısı HTTP 401 döndüğü ve açık web oturumundaki token güvenli biçimde Gateway istemcisine aktarılamadığı için authenticated contract ve lifecycle smoke çalıştırılamadı. Görevdeki açık karar ölçütüne göre authentication başarısızlığı `FAIL` gerektirir.

Bu karar FU02B implementation test sonuçlarını geçersiz kılmaz; yalnızca canlı authenticated Gateway closeout kapısının kapanmadığını belirtir.

## 12. Next Recommended Prompt

`@orchestrator MOD-0151 FU02B Authenticated Gateway Live Smoke Closeout Retry — hedef tenant 97c59330-dbc4-4665-b29c-0c26dbb5cc93 için çalışan bestepullukcu smoke credential/session sağlayarak; TenantId payload göndermeden ve yalnızca Gateway :5000 üzerinden contract, activate/deactivate/archive, overlap, draft soft-delete, active delete/archive reject ve computed-expiry smoke'unu çalıştır; yalnızca mevcut closeout raporunu güncelle.`
