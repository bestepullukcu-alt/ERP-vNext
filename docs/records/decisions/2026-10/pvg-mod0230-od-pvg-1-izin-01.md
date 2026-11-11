---
status: approved
decided: 2026-10-27
module: MOD-0230
decisions: OD-PVG-1
---

# PVG-MOD0230-ODPVG1-01 — İzin anahtarı ailesi ve `pvg.*` ad alanı (OD-PVG-1)

> **Karar sahibi:** sahip, MOD-0018 / Auth sahibi sıfatıyla. Sahip bunu bir başka Control Tower sohbetinde "auth-ben" diye söylemişti; aynı kararı bu sohbette "izin-evet" sözüyle verdi (2026-10-27, PVG Control Tower sohbeti). Bu söz `.antigravity/rules/permission-key-standard.md` (PKS-001) değişikliğinin kullanıcı onayını da kapsar (AGENTS.md §4).
> **Kaydı tutan:** PVG Control Tower.

## Önceki durum

- Paket revizyon 10, satır 168 (OD-PVG-1): PVG tarafı hedef aileyi 2026-10-21'de seçti: `pvg.case-intake-triage.*`.
- Bekleyen kısım, MOD-0018 / AuthService'in iki onayıydı: katalog ve grant sahipliği, `pvg.*` ad alanı.
- Ölçüm: bugün hiçbir anahtar seed edilmiyor, grant edilmiyor, JWT'de taşınmıyor ve `[HasPermission]` ile uygulanmıyor. Bu yüzden PKS-001 §2'ye göre yayımlanmış bir anahtar yok ve takma ada gerek yok. `pvg.*` PKS-001 §4'te yoktu.

## Onaylanan

1. Anahtarlar: `pvg.case-intake-triage.read`, `pvg.case-intake-triage.create`, `pvg.case-intake-triage.update`, `pvg.case-intake-triage.triage`, `pvg.case-intake-triage.route`.
   - `triage` ve `route`, PKS-001 Tier-3 eylemleri olarak paketin kendisinde kaydedilir.
2. `pvg.*` ad alanı `Diten.PvgService`'e aittir. PKS-001 §4 tablosuna bir satır eklendi; bu değişiklik bu kaydın commit'indedir.
3. Katalog ve grant, diğer modüllerdeki yoldan yürür: modül kataloğundaki sayfa/eylem kaydı yetki servisine izin olarak eklenir. Ayrı bir seed yazılmaz.
4. Uygulama DL-6'nın işidir: servisteki beş sabit, şekil testi (`PvgApplicationContractShapeTests`) ve AC-4 taraması. MVC tarafındaki `[HasPermission]` ise DL-7'nin işidir (AC-2).

## Sonuç

- AC-2 ve AC-4 artık OD-PVG-1'i beklemiyor. DL-6 yazılabilir; DL-6'nın ardından ekranın izin kısmı (DL-7) gelir.
- Paket metni (satır 168, AC-2 ve AC-4'teki "after OD-PVG-1 …" ifadeleri) bu onayı henüz göstermiyor. Paket, OD-PVG-2, OD-PVG-6 ve OD-PVG-7 onaylarıyla birlikte tek bir revizyonla güncellenir.

## Gerileme değerlendirmesi

- PKS-001'e yalnız bir satır eklendi; başka hiçbir ad alanı değişmedi.
- Bugün `pvg.mod0230.intake.*` anahtarları yalnız servis sabitlerinde ve testlerde duruyor. Hiçbir rol bu anahtarlara sahip olmadığı için anahtar değişikliği kullanıcıyı etkilemez.
