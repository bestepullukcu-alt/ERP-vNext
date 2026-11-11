---
status: approved
decided: 2026-10-27
module: MOD-0230
decisions: OD-PVG-7
amends: docs/records/decisions/2026-10/pvg-mod0230-od-pvg-7-port-01.md
---

# PVG-MOD0230-ODPVG7-02 — Port tablosu satırı bekletiliyor (AGENTS.md boyut sınırı)

> **Kaydı tutan:** PVG Control Tower. Sahibin "port-evet" kararı değişmedi. Bu kayıt yalnız kararın bir parçasının ne zaman uygulanacağını değiştirir.

## Ölçüm

- Ölçüm `origin/main` bc687b701 üzerinde yapıldı.
- `scripts/tests/test_sync_adapters_budget.py::test_the_real_instruction_files_pass`, `check_instruction_budget(...) == ([], [])` bekliyor. Bu yüzden uyarı eşiğini aşmak da testi kırar, yalnız üst sınırı aşmak değil. Bilgiyi "Yönetici · Ofis" verdi; kod okunarak doğrulandı.
- `AGENTS.md`, `main`'de 21 969 bayt. Uyarı eşiği 22 000, yani 31 bayt boşluk var.
- §3 tablosuna eklenebilecek en kısa satır 78 bayt: `| PVG Service | 5011 | `services/Diten.PvgService/src/Diten.PvgService.Api` |`. Bekleme listesinden ` 5011 (`PvgService`),` çıkarılınca 21 bayt kazanılıyor. Net artış 57 bayt; 31 baytlık boşluğa sığmıyor.

## Karar

1. `AGENTS.md` `main`'deki hâline geri alındı. 5011 bekleme listesinde kalıyor. Adaptörler yeniden üretildi; `sync_adapters.py --check` yeşil.
2. ODPVG7-01'in diğer iki kısmı yürürlükte kalıyor:
   - `web-verify` 5002'ye taşındı (`.claude/launch.json`).
   - Gateway CORS listesinde artık 5002 var (`gateway/Diten.ApiGateway/Program.cs`).

   Sahibin kararı, 5011'in PVG servisine ait olduğunu bağlamaya devam ediyor.
3. Tablo satırı için `AGENTS.md`'de yer açılması gerekiyor. Bu, ortak sözleşmenin kurulum alanına giriyor; PVG CT `AGENTS.md`'yi sıkıştırmaz. Yer açma işi kurulum sohbetinin işidir. Sahip o sohbeti uyandırır; iş ayrı bir PR olarak PVG'nin PR'ından önce birleşir. Sonra satır eklenir.

## Gerileme değerlendirmesi

`AGENTS.md` `main` ile aynı, yani phase1'deki bütçe testi bu dal yüzünden kırmızıya dönmez. 5011'in tabloda olmaması bugünkü durumla aynı.
