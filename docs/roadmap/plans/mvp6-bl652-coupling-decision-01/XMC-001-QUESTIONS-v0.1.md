# BL-652 Option 2 — XMC-001 questions for the Auth and MDM owners (v0.1)

    created : 2026-10-21 · status: DRAFT questions, not sent · decision: mvp6-bl652-option2-owner-decision-2026-10-21.md
    measured: origin/main 9d44e8a1f · scope: OPTION2-S2S-SCOPE-v0.1.md (same folder)

Format: `.antigravity/rules/cross-module-change-consent.md` §1. SupplyChain (SCE) asks; nothing in Auth or MDM is
touched until the answer.

## 1 · Auth owner

> **Başka modülde düzeltme gerekiyor — bunu da düzeltelim mi?**
> - **Modül:** AuthService — servis istemcisi kaydı · **Dosya:** `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/ServiceClientIdentity.cs:3-16`, `ServiceClientTenantGrant.cs`; token ucu `POST /api/internal/v1/auth/service-tokens/issue`
> - **Sorun:** SupplyChain'in MDM'e kendi kimliğiyle sorabilmesi için kayıtlı bir servis istemcisi yok; bugün kullanıcının token'ı iletiliyor ve `mdm.legal-entities.read` olmayan kullanıcıda Carrier/Load/Return/Claim oluşturma 503 dönüyor.
> - **Neden şimdi:** BL-652 için Seçenek 2 kararlaştırıldı (Natig, 2026-10-21); Admin temel yetkisi genişletilmeyecek. Seçenek 2'nin ilk parçası Auth'taki istemci kaydı.
> - **İstenen:** (1) `Diten.SupplyChain` için bir `ServiceClientIdentity`, tek `AllowedAudience` = yeni MDM audience'ı (öneri `TRUSTED_LEGAL_ENTITY_REFERENCE_CONSUMER`); (2) SupplyChain kullanan kiracılar için `ServiceClientTenantGrant` — **her kiracıya nasıl verileceği** (etkinleştirmede otomatik mi, operasyonel komutla mı) sizin kararınız; (3) ortam başına ClientId/secret ve rotasyon (appsettings'e değil).
> - **Risk:** 🟡 — ertelenirse SupplyChain'in dört modülünde oluşturma, rolünde `mdm.legal-entities.read` olmayan kullanıcı için kapalı kalır (güvenli ama işlevsiz); geçici genişletme yapılmayacak.
> - **Seçenekler:** (a) bu işte, SCE yazar, siz incelersiniz · (b) ayrı iş, Auth ekibi yazar · (c) şimdilik bırak, kayda geç
> - **Önerim:** **(b)** — istemci kaydı ve kiracı izni sahipliği Auth'ta; desen main'de mevcut (MDM'in denetim istemcisi), yeni kod az. SCE çağıran tarafı ve testleri yazar.

## 2 · MDM owner

> **Başka modülde düzeltme gerekiyor — bunu da düzeltelim mi?**
> - **Modül:** MdmService — Legal Entity doğrulama ucu · **Dosya:** `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:48` (`GET api/legal-entities/{id}/lookup-validation`, `[HasPermission("mdm.legal-entities.read")]`)
> - **Sorun:** Uç yalnız kullanıcı iznini kabul ediyor; servis token'ını tanıyan bir doğrulama şeması MDM'de yok.
> - **Neden şimdi:** BL-652 Seçenek 2 (Natig, 2026-10-21). SupplyChain bu uca kendi servis kimliğiyle soracak.
> - **İstenen:** (1) yalnız bu audience için bir servis-token şeması — Platform'daki `TrustedServiceTokenValidationExtensions.cs` ile aynı biçim (zorunlu claim'ler, tek audience, `service_name` = `Diten.SupplyChain`, `tenant_id` = istekteki kiracı); (2) `lookup-validation` **ya kullanıcı iznini ya bu servis token'ını** kabul etsin; MDM'in başka hiçbir ucu bu token'a açılmasın; (3) cevap davranışı aynı kalsın: başka kiracının ya da Active olmayan LE → 404, var olduğuna dair bilgi sızmadan.
> - **Karar sizde:** temsil edilen kullanıcı (`X-Delegated-Authorization`, Platform'un iş akışı deseni) istensin mi — denetim kaydında kullanıcıyı korur, MDM'in cevabını değiştirmez.
> - **Risk:** 🟡 — ertelenirse yukarıdaki 503 durumu sürer.
> - **Seçenekler:** (a) bu işte, SCE yazar, siz incelersiniz · (b) ayrı iş, MDM ekibi yazar · (c) şimdilik bırak, kayda geç
> - **Önerim:** **(b)** — kimlik doğrulama şeması MDM'in güvenlik sınırı; tek uç, desen hazır. Testler: geçerli token → 200; başka kiracı → 404; yanlış audience / `service_name` / süresi dolmuş / iptal edilmiş token → 401; kullanıcı yolu değişmeden.

## 3 · SupplyChain (ours, no XMC)

After both answers: a token provider modelled on MDM's `AuthTrustedSourceAuditServiceIdentityProvider`, the validator
switched to the service token, and the BL-652 test (a user **without** `mdm.legal-entities.read` creates → 201) —
under its own approved scope. Gateway changes, if any, only by integration-agent.
