---
status: approved
decided: 2026-10-27
module: MOD-0230
decisions: OD-PVG-2
---

# PVG-MOD0230-ODPVG2-01 — Kiracı ve aktör sözleşmesi onaylandı (OD-PVG-2)

> **Karar sahibi:** sahip, iki sıfatla:
> - Enterprise Architecture, `docs/records/decisions/2026-10/pvg-mod0230-mimari-sahibi-01.md` (MIMARI-01) gereği;
> - MOD-0018 / Auth sahibi (claim adları).
>
> Sahibin sözü: "kimlik-evet" (2026-10-27, PVG Control Tower sohbeti).
> **Kaydı tutan:** PVG Control Tower.

## Onaylanan

Paket `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` revizyon 10, satır 169 (OD-PVG-2, "Recommended option"). PVG tarafı bu seçeneği 2026-10-21'de kabul etmişti (`docs/records/decisions/2026-10/pvg-mod0230-open-decisions-01.md`). Bekleyen iki onay bu kayıtla verildi:

1. **Sözleşme (Enterprise Architecture).**
   - PVG servisi JWT'yi kendisi doğrular. Issuer ve audience, diğer kiracı servislerindeki gibidir.
   - Aktörü doğrulanmış token'ın konu (subject) claim'inden alır.
   - Kiracıyı Gateway'in yazdığı `X-Tenant-Id` başlığından alır ve bu değerin token'daki kiracı claim'iyle aynı olmasını ister. Eşleşmezse istek reddedilir.
   - `X-Diten-Tenant-Context` ve `X-Diten-Actor-*` başlıkları yetkili kaynak olmaktan çıkar ve yok sayılır.
   - Gateway kodu değişmez.
2. **Claim adları (MOD-0018 / Auth sahibi).**
   - Kiracı claim'i `TenantId`'dir; `.antigravity/rules/security-jwt.md:27` bu adı kullanır ve diğer kiracı servisleri de aynısını kullanır.
   - Aktör, doğrulanmış token'ın konu claim'idir.

## Sonuç

- AC-5 (DL-4) artık OD-PVG-2'yi beklemiyor. WP-0230-DL4B'nin kimlik doğrulama kısmı bu sözleşmeyle yazılabilir.
- AC-6 (hata zarfı) OD-PVG-3'ü, yani MOD-0041 / Ops onayını beklemeye devam ediyor. DL4B planlanırken bu iki kısmın ayrılıp ayrılmayacağına ayrıca karar verilir.
- Paket metni (satır 169 ve AC-5'in "after OD-PVG-2 …" ifadesi) bu onayı henüz göstermiyor. Paket, sahibin diğer mimari kararlarıyla birlikte tek bir revizyonla güncellenir. Bu revizyon onaylanmadan DL4B prompt'u yazılmaz (ders: `feedback-no-pack-change-before-dispatch`).

## Gerileme değerlendirmesi

Bu kayıt kod değiştirmiyor. Uygulandığında ise, başlıkla kiracı seçen istemciler (bugün MVC proxy `X-Diten-*` başlıklarını dolduruyor) token'a geçmek zorunda kalacak. DL4B ve DL-7 bunu birlikte ele alır.
