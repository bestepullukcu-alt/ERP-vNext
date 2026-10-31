---
status: proposal
kind: owner-decision-request
date: 2026-10-29
author: Treasury Intake Control Tower (K1 / gmg-10)
decision_owner: Beste (K2) — Platform sahibi
related: KARAR D88/2, D88/4; Y-4/6; Y-22
---

> **Öneri — karar değil.** Bu dosya karar sahibinin onayını ister; merge edilmesi kararı vermez. Cevap bu PR'da yorum ya da
> "Approve" ile verilir; Treasury CT cevabı kaynak cevap (Y-n) ve karar (D-n) olarak kaydeder, sonra pakete işler.
> Seçim: A / B / C / D (C ise K-6 "evet"e döner)

# Treasury CT → Beste (bilgi: Ali): FinanceTreasuryService şirket kapsamı (SEC-002) — seçenekler ve öneri (D88/4, onayına)

## Ölçüm (origin/main `63acdf2ec`, kod okuması; çalışma testi yok)

- **Token'da şirket bilgisi yok.** Auth kullanıcı token'ı: `sub`, `email`, `actor_type`, `tenant_id`, rol, `permission` — `legal_entity_id`
  ya da `legal_entities` yok (`services/Diten.AuthService/.../TokenService.cs:33-54`).
- **Kullanıcı → şirket verisinin tek kaynağı Platform:** pozisyon atamaları → pozisyonlar → örgüt birimleri
  (`OrgDataScopeCandidateFactReader.cs:21-80`); `OrgDataScopeResolver` yalnız Platform sürecinde kayıtlı (`Platform.Application/DependencyInjection.cs:63`).
- **Platform kapsam ucunun tek çağıranı MDM:** `ConsumerService = "DITENMDMSERVICE"` (`TrustedLegalEntityScopeCredentialAuthenticator.cs:11`),
  tek kimlik bilgisi bağlaması (`TrustedLegalEntityScopeCredentialOptions.cs:7`).
- **Öteki servisler kullanıcı kapsamı uygulamıyor:** SupplyChain / Inventory / Planning başlıktaki şirketi MDM `lookup-validation` ile
  yalnız "kiracıda etkin mi" diye doğruluyor; kullanıcı üyeliğini doğrulamıyor (Inventory `InventoryScopeResolutionMiddleware.cs:28-29`,
  feragat W-0173-02). Procurement / Manufacturing / SafetyStock başlığa güveniyor. HumanCapital / Talent `legal_entities` claim'ini
  okuyor ama okuma "PENDING: Ali token integration" diye boş döndürülmüş (`HttpLegalEntityContext.cs:89-97`).

## Sorun

Dört finans paketi gerçek kullanıcı kapsamı istiyor (AC-32: aynı izinli iki kullanıcı, iki şirket → karşı şirketin kaydı 404). K-6
"hayır" ile finans servisi Platform ucunu kullanmıyor; ama Platform dışında kullanıcı → şirket verisi yok. "Kendi kapsam yolu"nun
**kaynağı** belirsiz.

## Seçenekler

| | Yol | Artı | Eksi |
|---|---|---|---|
| **A** | Başlık + MDM `lookup-validation` (SupplyChain deseni), servis içi port | Y-22'ye uyar; kod deseni var | Gerçek kullanıcı kapsamı değil — AC-32 karşılanmaz; açık feragat (W-0173-02 gibi) ve dört pakette AC-32 değişikliği gerekir (K1 kararı) |
| **B** | Servis içi port; kaynak gelene kadar boş küme (kapalı-başarısız) | İki karara da uyar; güvenli | Kapsamlı her okuma/yazma reddedilir — modül kullanılamaz |
| **C** | Port + Platform'a ikinci kimlik bilgisi (`DITENFINANCETREASURYSERVICE`) ve finans anahtarları | Gerçek kapsam; AC-32 karşılanır; MDM deseniyle aynı | K-6 "evet" demek — Platform değişikliği (bu kez FU27 §5.2 koşullu dosyaları) |
| **D** | Auth token'ına `legal_entities` claim'i (Auth, Platform org verisinden doldurur) | Her servis aynı claim'i okur; HumanCapital / Talent zaten bekliyor | Auth işi (Ali), token boyutu / tazelik kuralı; bugün plan yok |

## Önerimiz

**C — kısa vadede; D — uzun vadede ortak çözüm.** C, AC-32'yi bugünkü kodla karşılayan tek yol ve MDM'de çalışan desenin aynısı
(her izin kendi modül koduyla; kapalı-başarısız). Bu, K-6'yı "evet"e çevirmek demek — senin kararın. C'yi istemezsen B ile başlayıp
D'yi Ali'yle planlamayı öneririz; A'yı yalnız açık feragatle ve K1'in AC-32 kararıyla kabul ederiz.

Hangisini seçiyorsun? Cevabına göre dört paketin SEC-002 maddesi ve gerekirse AC-32 güncellenir.
