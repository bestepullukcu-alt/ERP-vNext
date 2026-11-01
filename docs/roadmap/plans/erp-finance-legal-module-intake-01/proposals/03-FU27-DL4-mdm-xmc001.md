---
status: proposal
kind: owner-decision-request
date: 2026-10-29
author: Treasury Intake Control Tower (K1 / gmg-10)
decision_owner: Ali — MDM / MOD-0018 sahibi
related: KARAR D88/3; FU27 §5.2 MDM tablosu
---

> **Öneri — karar değil.** Bu dosya karar sahibinin onayını ister; merge edilmesi kararı vermez. Cevap bu PR'da yorum ya da
> "Approve" ile verilir; Treasury CT cevabı kaynak cevap (Y-n) ve karar (D-n) olarak kaydeder, sonra pakete işler.
> Seçim: izin var / yok / değişiklikle

# Treasury CT → Ali: FU27 DL-4, MDM tarafı XMC-001 talebi (onayına)

Treasury CT → Ali: Beste FU27 DL-4'ün Platform tarafı için XMC-001'i dosya kapsamıyla verdi. MDM tarafı senin alanın; aynı biçimde
izin rica ediyoruz. Kapsam yalnız şu dosyalar (FU27 §5.2 MDM tablosu):

- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformTrustedLegalEntityScopeProviderClient.cs`
  - `:18` `private const string ModuleCode = "product-item-sku-master"` → izinli modül kodu kümesi: ürün + MOD-0289 + MOD-0217 kodları
    (paket başına bir kod, D79/1);
  - `:162-171` `IsValidRequest`: MOD-0289 / MOD-0217 kodları kabul, bilinmeyen kod yine 400 `LEGAL_ENTITY_SCOPE_REQUEST_INVALID`;
  - `:260`, `:282`, `:362`, `:377`, `:412` yanıttaki `moduleCode`'un istenenle birebir karşılaştırılması **değişmez**.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/PlatformTrustedLegalEntityScopeProviderClientTests.cs`:
  yeni kodlar, bilinmeyen kodun reddi, her iznin yalnız kendi modül koduyla sorulması (banka izni ürün modülü adına sorulamaz) —
  sabotaj kanıtıyla.
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/TrustedLegalEntityScopeProviderContractTests.cs`: gerileme.

Başka MDM dosyasına dokunulmaz; kapalı-başarısız davranış korunur. Anahtarlar K-5 listesindeki 23 anahtar (MOD-0289 9, MOD-0217 14).
Sıra (FU27 §8, Beste'yle aynı): Auth izinleri → Platform kodu → Platform ortam ayarı → MDM istemcisi. Kod FU27 onaylandıktan sonra yazılır.
İzin veriyor musun?
