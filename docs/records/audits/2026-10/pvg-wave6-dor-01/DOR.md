# PVG altıncı dalga — DoR (kart §1) kanıtı: WP-0230-DL4D

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır; dağıtım izni değildir.
> Dağıtım için iki koşul var:
> - `pack_approval.py check --ref origin/main` MOD-0230 için çıkış 0 vermeli (revizyon 13 onayı #290 ile `main`'de; ölçüm 2026-10-30, origin/main 63acdf2ec, çıkış 0: HIGH · onaylayan bestepullukcu-alt · 2026-10-29; insan adımı aracın çıktısında: satırı ekleyen ve merge eden git'ten türetildi, PR #290 GitHub'dan okundu (#295 sonrası)).
> - Sonra `prompt_gate.py check` dağıtım kipinde çıkış 0 vermeli ve sahip işçinin başlatılmasını bu sohbette onaylamalı.
>
> Tarih: 2026-10-30. Karar: `docs/records/decisions/2026-10/pvg-mod0230-secenek-kaynagi-01.md` (SECENEK-01: S-1 seçenekleri servis verir, S-3 bu iş WP-0230-DL4D, DL4B'den sonra).

| DoR maddesi | WP-0230-DL4D |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 13 (`ee175d00…`), `main`'de onaylı (#290, HIGH; Beste'nin merge commit'i `496ab5659`). İnceleme 14 UYGUN. |
| Sözleşme | AC-34. Prompt'taki 11 alıntı Expected HEAD'de birebir (`prompt_gate.py` hazırlık kipi). |
| Bağımlılık | WP-0230-DL4B CT dalında ve `main`'de (INTEGRATED). |
| Kapsam dışı | `ocelot.json` (mevcut öğe route'u taşır; AC-16 aynen). Yeni izin anahtarı. MVC vekili ve ekran (WP-0230-DL7B). Seçenek değerlerinin değişmesi (QPPV AL-4). AC-6 hata zarfı (WP-0230-DL4C). |
| Kapsam | `PvgCaseIntakeTriageEndpoints.cs`: salt okunur `GET …/options`, yanıt kaydı, `options` ayrılmış parça. Yeni `PvgCaseIntakeOptionsTests.cs` (altı test). `PvgServiceApiHostTests.cs`: yalnız üç kesin rota listesi beklentisine options girişi. |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl4d-r1`, dal `feature/pvg/wp-0230-dl4d-r1`, Expected HEAD `99b5049fa`, temiz (oluşturuldu 2026-10-30). |
| Yürütme / şerit | Risk HIGH (yetki, kiracı, sözleşme). Aynı anda başka bir PVG işçisi yok. |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, bildirir ve durur. `origin/main` birleştirmesi yapmaz. İşçi dalını CT dalına CT birleştirir. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL4D-v1.6.md`, sha256 `173245108504b603c0293f8ad043b7e7c1c10aff4f783b3ea19ab3380680f095` @ c92d8e83d. |
| Bağımsız inceleme (HIGH) | Resmî kayıtlar: v1.4 UYGUN DEĞİL (engelleyici 1: üçüncü rota listesi testi), v1.5 UYGUN, v1.6 UYGUN (kritik 0, engelleyici 0, 4 DÜŞÜK) — `docs/records/reviews/MOD-0230/prompts/WP-0230-DL4D-v1.6-inceleme-01.md` @ fc0432ee0. Öncesinde dört ara inceleme (v1.0–v1.3, kayıt dışı). |
| Kabul | AC-34: altı test ve adlı sabotajlar (E2, işçi). **Kanıt sınırı:** DL-4 için gerekli seviye E4; E4'ü CT betiği `dl4d_options_smoke.sh` (sha256 `27fd307e…`) üretir. Kırmızı taban kaydı `docs/records/audits/2026-10/pvg-dl4d-live-01/run-01-kirmizi-taban` (commit 9cf3140b9, çıkış 1: kontrol 2 ve 5 kırmızı, 409). |

## CT okumaları

1. **Kiracı denetimi yalnız kiracıdır.** AC-34 "the same tenant context check as the list endpoint" der ve yalnız kiracıyı sayar. Uç aktör ve korelasyon kullanmadığı için `From(httpContext, requireActor: false, requireCorrelation: false)` çağrılır; kiracı adımları liste ucuyla aynı koddur. Sahibe 2026-10-29 sohbette bildirildi.
2. **`PvgServiceApiHostTests.cs` izinli yolda.** Paketin DL-4 yolu `tests/Diten.PvgService.Api.Tests/**`'tır; prompt bunu üç kesin rota listesi beklentisine options girişiyle daraltır.
3. **İzin kapısı yok.** Paket "Authorization Convention": uç kiracı verisi ve kayıt döndürmez; ekranın okuma anahtarı denetimi MVC vekilindedir (DL7B).

## Dağıtım notu

Yük kuralı: her dotnet komutundan önce `uptime`; 1 dakikalık yük 12'nin üstündeyse bekle. CT'nin yeşil koşusu ve E4 kabul kaydı işçi dalı CT dalına birleştikten sonra yapılır.
