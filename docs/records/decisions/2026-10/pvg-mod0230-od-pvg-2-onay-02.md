---
status: approved
decided: 2026-10-27
module: MOD-0230
decisions: OD-PVG-2 (claim adı düzeltmesi)
amends: docs/records/decisions/2026-10/pvg-mod0230-od-pvg-2-onay-01.md
---

# PVG-MOD0230-ODPVG2-02 — Kiracı claim'inin adı `tenant_id` (ODPVG2-01 düzeltmesi)

> **Karar sahibi:** sahip, MOD-0018 / Auth sahibi sıfatıyla. Sahibin sözü: "claim-duzelt" (2026-10-27, PVG Control Tower sohbeti).
> **Kaydı tutan:** PVG Control Tower.

## Hata

ODPVG2-01 kiracı claim'inin adını `TenantId` diye yazdı. Gerekçe olarak "diğer kiracı servisleri de aynısını kullanır" denmişti. Bu ifade ölçülmeden yazıldı ve yanlış; hata Control Tower'ındır. Adın kaynağı, kural metnindeki şu cümleydi: `.antigravity/rules/security-jwt.md:27`: "JWT içindeki `TenantId` claim'i …".

## Ölçüm (CT dalı `7649af7e1`)

- Depodaki tek token üreticisi AuthService'tir. Claim'i `tenant_id` türüyle yazar: `services/Diten.AuthService/src/…/TokenService.cs:40` ve `:114`.
- Gateway (ör. `gateway/Diten.ApiGateway.Tests/TenantContradictionGuardTests.cs:308`), CrmService ve AuthService `tenant_id` okur.
- `TenantId` türünde bir claim hiçbir token'da yok. PVG `TenantId` arasaydı her gerçek token reddedilirdi.
- WP-0230-DL4B'nin bağımsız incelemesi (`docs/records/reviews/MOD-0230/prompts/WP-0230-DL4B-v1.0-inceleme-01.md`, F-1) aynı sonucu ölçtü.

## Karar

1. OD-PVG-2'nin kiracı claim'i, token üzerindeki `tenant_id` türüdür. PVG servisi kiracıyı Gateway'in yazdığı `X-Tenant-Id`'den alır ve bu değerin token'daki `tenant_id` claim'iyle eşleşmesini ister. Sözleşmenin geri kalanı ODPVG2-01'deki gibidir.
2. ODPVG2-01'deki "TenantId" geçen yerler bu kayıtla düzeltilmiş sayılır. ODPVG2-01 değiştirilmez (K4).
3. Paket revizyon 11 (satır 1871 ve OD-PVG-2 karar sütunu) "TenantId" diyor. Bu metin paketin bir sonraki revizyonunda düzeltilir; revizyon 11'in inceleme bulgusu F-1 ile aynı tura girer. AC-5'in ölçüt metni claim'i adıyla anmadığı için ("token tenant claim") ölçüt değişmez.
4. `.antigravity/rules/security-jwt.md:27`'deki ad kurulum alanının konusudur; PVG değiştirmez. Durum "Yönetici · Ofis" aracılığıyla kurulum listesine bildirildi.

## Gerileme değerlendirmesi

Kod değişmiyor. Bu düzeltme olmasaydı WP-0230-DL4B, gerçek token'ların hepsini reddeden bir servis üretirdi.
