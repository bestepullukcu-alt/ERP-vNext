# PVG onuncu dalga — bağımsız doğrulama: WP-0230-DL4E

> Control Tower kaydı, 2026-11-02. `DISPATCHED → INTEGRATED` geçişinin kanıtıdır.

- **Teslim:** dal `feature/pvg/wp-0230-dl4e`, commit `73dbdecd38e81bc96d970d9a06cececdc4b90f27`, ebeveyn Expected HEAD `e6e6ef0a3`.
- **İşçi:** alt ajan, Sonnet 5.5 · high istendi, `claude-sonnet-5-5` olarak bildirdi. Kendi yalıtılmış worktree'si. Kural sapması: işçi kendi oluşturduğu geçici sabotaj arşivini (`sabotage-dl4e-01.tar`, CT karalama alanı) açtıktan sonra `rm` ile sildi. Başka hiçbir şey silinmedi; depo, kopya ve kayıtlar etkilenmedi. Sonraki brief'lerde "rm yok" kuralı korunur.
- **Doğrulayıcı:** ayrı alt ajan, Opus 5.5 · high istendi, `claude-opus-5-5` olarak bildirdi; effort doğrulanamadı. İşçinin sabotajları tekrar edilmedi.
- **Karar: KABUL EDİLEBİLİR.** ENGEL 0, ORTA 0, DÜŞÜK 3.

## Ölçülenler (doğrulayıcı)

| Ölçüm | Sonuç |
|---|---|
| Kapsam | `PvgCaseIntakeTriageEndpoints.cs` (+67 −1) ve yeni `PvgCaseIntakeTriageBindingTests.cs` (+261). Host genelinde JSON ayarı yok, enum özniteliği yok; Domain değişmedi; `git diff --check` temiz; sır yok |
| Dönüştürücü | `JsonConverter<PvgTriageOutcome?>`, `HandleNull => true`, alan tipi `PvgTriageOutcome?`. Read: null → null; metin yalnız üç ad (ordinal); sayı `TryGetInt32`; başka her şey `JsonException`. Write: null, ad ya da tanımsız değer için sayı |
| `Diten.PvgService.Api.Tests` | 394 geçti (373 + 21), 0 kırmızı; `known_failures` YENİ=0 |
| `Diten.PvgService.RegPvBase.Tests` | 211 geçti |
| Mimari süit | 176 test, 3 kırmızı, taban adlarıyla aynı; `Diten.PvgService` 13 komut, hepsi denetimli |
| Frontend nav + triyaj web | 57 test, 2 taban kırmızısı (sahip sözü «taban-kabul»); yeni satır yok; `PvgCaseIntakeTriageWebTests` 0 kırmızı |
| Testlerin yolu | Gerçek host (`CreatePvgServiceWebHostBuilder`, Kestrel), HttpClient, JWT ve kiracı başlığı; gövde ham JSON |

## Sabotaj

- **İşçi:** 8 sabotaj, hepsi adıyla kırmızı.
- **Doğrulayıcı:** 13 sabotaj, işçininkiler tekrar edilmedi. 10'u kırmızı, 3'ü yeşil:
  - V2 (`GetInt32`) ve V7 (`HandleNull false`) davranışça eşdeğer mutantlar. V7b ve S8chk kontrolleri `HandleNull true`'nun null sabotajını ölçülebilir kıldığını gösterdi.
  - V6 (izin kapısından önce sessiz depo okuması) teslimden önce de var olan bir test boşluğu (D-1).
- Her sabotaj geri alındı, özetler eşleşti.

Kanıt (CT karalama alanı, depo dışı): işçi `dl4e_sab_1.log`, `dl4e_sab_2.log`, `base/`; doğrulayıcı `dl4e-vfy01-results/`, `dl4e_vfy01_sab.py`.

## Bulgular

- **D-1 (DÜŞÜK, teslimden önce de vardı):** izin kapısından önce eklenen sessiz bir depo okumasını hiçbir test yakalamıyor (`PvgIntakeDraftApplicationService.cs:259-277`). Ucuz düzeltme: AC-39 (a) satırlarına `harness.Store.ReadCalls == 0` eklemek. Takip maddesi olarak yazıldı; DL-4'e bir sonraki dokunuşta kapatılır.
- **D-2 (bilgi):** `GetInt32` ile `TryGetInt32` davranışça eşdeğer; System.Text.Json okuyucu hatasını `JsonException`'a çeviriyor.
- **D-3 (bilgi):** `HandleNull => true` biçim şartını kod okuması tutuyor, hiçbir test sabitlemiyor.

## Birleştirme

`feature/pvg/wp-0230-dl4e` CT dalına `--no-ff` ile birleşti: `561fd2f80`. Çakışma yok. İşçi dalının tabanı `e6e6ef0a3` CT dalının o anki ucuydu; aradaki CT commit'leri yalnız belge. İşçi dalı yerelde kalır, push edilmez (S-125).

## Ölçülmeyenler

- Canlı yol (Gateway, Web vekili, 5011) ve E4 ölçülmedi. DL-4 kabulü canlı yol açılana kadar ertelenir (KK-8). Formdan gelen triyajın canlı ölçümü AC-38 (i)'nindir.
- CaseProcessing, MeddraCoding ve SignalManagement süitlerini işçi koştu (23 / 40 / 43, değişmedi); doğrulayıcı koşmadı.
