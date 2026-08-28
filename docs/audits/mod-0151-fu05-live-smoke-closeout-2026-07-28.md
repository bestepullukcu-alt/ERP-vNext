# MOD-0151 FU05 — Live Smoke Closeout

> Tarih: 2026-07-28  
> Hedef tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`  
> Verdict: **FAIL** — canlı oturum model aktivasyonunda sunucu iletişim hatası, preview çağrısında `Unauthorized`
> döndürdü; matched preview row üretilemedi ve apply zinciri başlatılamadı.

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Gateway `http://localhost:5000/health` | HTTP 200 |
| AuthService `http://localhost:5056/health` | HTTP 200 |
| Platform `http://localhost:5057/health` | HTTP 200 |
| CrmService `http://localhost:5061/health` | HTTP 200; yalnız health check, business API çağrısı yapılmadı |
| Web `http://localhost:5001/` | HTTP 200 |
| Hedef tenant oturumu | Authenticated Web tenant-shell açıldı |
| İstenen beş permission claim'i | UI'da Territory read/manage yüzeyleri görünür olmakla birlikte token payload'ı bu koşuda güvenli biçimde okunamadı; canlı mutasyon çağrısı `Unauthorized` döndüğü için **doğrulanamadı** |
| Yasak claim'ler | `crm.territory.delete` ve `crm.micro-zone.manage` eklenmedi/kullanılmadı; token payload düzeyinde **doğrulanamadı** |
| Başlangıç git durumu | Kirli çalışma ağacı önceden mevcuttu: 71 tracked diff, 918 untracked; hedef closeout raporu yoktu |

Health kontrolleri business smoke değildir. CrmService için doğrudan `:5061` üzerinde yalnız `/health` çağrıldı;
tüm denenmiş business işlemleri Web'in Gateway proxy yüzeyi üzerinden yürütüldü.

## 2. Contract Smoke

Canlı authenticated Web sayfası Territory Management contract'ını yükleyip FU05 Assignment History yüzeyini
render etti. Önceki implementation evidence'ında contract değerleri
`supportsAssignmentRules=true`, `supportsAssignmentPreview=true`, `supportsResourceAssignments=true`,
`supportsAccountAssignmentApply=true`, `supportsAssignmentHistory=true`, `supportsCoverageSummary=true`,
`supportsWorkflowActivation=false` ve `supportsApprovalTrace=false` olarak kayıtlıdır.

Ancak bu closeout koşusunda `GET /api/crm/territory-management/contract` yanıt gövdesi bağımsız olarak
yakalanamadığı için `isReady` dahil tüm flag seti **canlı API kanıtı olarak tamamlanmış sayılmadı**.

## 3. Smoke Data Setup

Web UI üzerinden aşağıdaki tenant-scoped smoke kayıtları oluşturuldu:

| Nesne | Değer |
|---|---|
| Model code | `SMOKE-MOD0151-FU05-20260728CLOSE1` |
| Model id | `7b2918a3-2a93-4874-bc66-a7982eb4244e` |
| Country / BU | `tr` / `alpha` |
| Effective window | `2026-07-28` – `2026-12-31` |
| Node | `FU05-ZONE-1` / `8f8e827b-1f82-4995-a280-dad35cf3dcae` / level `zone` |
| Rule | `FU05-RULE-1`, geography `country=tr`, priority 10, conflict policy `block` |

Model aktivasyonu UI'da sunucu iletişim hatasıyla sonuçlandı. Model ve node `draft` kaldı. Tek-active-model guard
veya authorization ayrımı bu koşuda API hata gövdesi alınamadığından kesinleştirilemedi. Yasak
`crm.territory.delete` kullanılmadığı için smoke taslağı silinmedi.

## 4. Preview Smoke

`Run Preview` işlemi Web/Gateway yolu üzerinden çağrıldı ve UI açıkça `Unauthorized` gösterdi. HTTP 200 ve
`matchedAccounts >= 1` elde edilemedi. Preview sonucu üretilemediği için bu aşamada
`AccountTerritoryAssignment` oluşturulmadı.

## 5. Apply Smoke

**Çalıştırılmadı.** Apply için gerekli active model, active node ve matched preview row önkoşulları oluşmadı.
Assignment persistence, provenance ve business scope değerleri canlıda doğrulanamadı.

## 6. History Smoke

Model-level history UI'ı `No assignment history` gösterdi. Apply oluşmadığı için yeni kayıt beklenmiyordu.
Account-level history canlıda çalıştırılmadı.

## 7. Coverage Summary Smoke

Apply oluşmadığı için account-level Coverage Summary canlıda çalıştırılmadı. Account master'a territory alanı
yazılmadı.

## 8. Conflict Smoke

**Çalıştırılmadı.** İlk assignment oluşmadığından aynı account/scope/window için duplicate 409 senaryosu kurulamadı.
Dolayısıyla canlı partial-write yokluğu kanıtlanamadı.

## 9. Override Smoke

**Çalıştırılmadı.** İlk assignment ve duplicate conflict oluşmadığından reason'sız 400 ile reason'lı override
senaryoları yürütülemedi.

## 10. End Assignment Smoke

**Çalıştırılmadı.** Current assignment oluşmadı. End endpoint çağrılmadı; hard delete kullanılmadı.

## 11. Mongo Transaction Readiness

Canlı apply/duplicate/override işlemleri authorization ve lifecycle önkoşulunda bloke olduğu için transaction
commit/rollback davranışı canlıda kanıtlanamadı. Önceki implementation evidence'ındaki testler overlap 409,
all-or-nothing ve override transaction davranışını kapsar; fakat bu closeout'un canlı kanıtının yerine geçmez.

## 12. Guard Checks

| Guard | Sonuç |
|---|---|
| Runtime code changed? | No |
| Account master changed? | No |
| Contact changed? | No |
| Resource assignment changed? | No |
| Workflow/evidence/import-export opened? | No |
| Brand Scope opened? | No |
| Product/Brand master touched? | No |
| Hard delete used? | No |
| Mongo hand-edit used? | No |
| Direct 5061 business API used? | No |
| TenantId payload used? | No |
| RBAC seed/grant changed? | No |
| MOD-0048 publish changed? | No |
| Forbidden permission added? | No |
| History preserved? | Yes; mevcut history silinmedi |
| CoverageSummary separate from Account master? | Yes; runtime tasarımı ayrı projection, Account mutate edilmedi |

## 13. Created / Updated Files

Bu koşuda yalnız şu dosya oluşturuldu:

- `docs/audits/mod-0151-fu05-live-smoke-closeout-2026-07-28.md`

Repo başlangıçta kirliydi. Önceden mevcut runtime ve diğer çalışma ağacı değişikliklerine dokunulmadı.

## 14. Final Verdict

### FAIL

Görevdeki açık FAIL kuralı gerçekleşti: matched preview row üretilemedi. Kök canlı belirti, model aktivasyonundaki
sunucu iletişim hatası ve ardından preview endpoint'inin `Unauthorized` dönmesidir. Bu nedenle apply, persisted
assignment, history, coverage, duplicate 409, override ve end zinciri kanıtlanamadı. Account/Contact/resource
masterları ve yasak kapsamlar değiştirilmedi.

## 15. Next Recommended Prompt

`@orchestrator MOD-0151 FU05 Live Smoke Closeout Retry — hedef tenant oturumunun Territory model read/manage
claim aktarımını ve Gateway/Web authorization zincirini salt okunur teşhis et; model aktivasyonundaki gerçek
Gateway hata status/body'sini kaydet; runtime/RBAC/MOD-0048/Mongo değişikliği yapmadan authorization hazır olduğunda
active model → matched preview → apply → 409 → override → end zincirini tekrar çalıştır.`
